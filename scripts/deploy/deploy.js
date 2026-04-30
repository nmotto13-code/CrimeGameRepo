/**
 * deploy.js
 *
 * Automates the full Pocket Casebook iOS deploy pipeline:
 *   1. Trigger a new Unity Cloud Build (or use an existing build with --build <n>)
 *   2. Poll until success
 *   3. Download the IPA
 *   4. Commit + push to repo
 *   5. Trigger the upload-testflight.yml GitHub Actions workflow
 *
 * Usage:
 *   node deploy.js                  trigger new build, wait, download, upload
 *   node deploy.js --build 42       use existing successful build #42 (for testing)
 *   node deploy.js --skip-upload    stop after committing IPA (skip GitHub workflow)
 *   node deploy.js --dry-run        show what would happen, no API calls or git ops
 */

import fs   from 'fs';
import path from 'path';
import https from 'https';
import http  from 'http';
import { execSync }    from 'child_process';
import { fileURLToPath } from 'url';
import 'dotenv/config';

const __dirname  = path.dirname(fileURLToPath(import.meta.url));
const REPO_ROOT  = path.resolve(__dirname, '../..');
const IPA_DEST   = path.join(REPO_ROOT, 'PocketCasebook.ipa');
const WORKFLOW   = 'upload-testflight.yml';
const POLL_MS    = 300_000; // poll every 5 min
const API_BASE   = 'https://build-api.cloud.unity3d.com/api/v1';
const GH_REPO    = 'nmotto13-code/CrimeGameRepo';

// ── CLI args ─────────────────────────────────────────────────────────────────

const args       = process.argv.slice(2);
const buildArg   = args.includes('--build')
  ? parseInt(args[args.indexOf('--build') + 1], 10)
  : null;
const skipUpload = args.includes('--skip-upload');
const dryRun     = args.includes('--dry-run');

// ── Env ───────────────────────────────────────────────────────────────────────

const { UCB_ORG_ID, UCB_PROJECT_ID, UCB_BUILD_TARGET, UCB_API_KEY } = process.env;

function validateEnv() {
  const missing = ['UCB_ORG_ID','UCB_PROJECT_ID','UCB_BUILD_TARGET','UCB_API_KEY']
    .filter(k => !process.env[k]);
  if (missing.length) {
    console.error(`\n  ERROR: missing env vars: ${missing.join(', ')}`);
    console.error('  Add them to scripts/deploy/.env\n');
    process.exit(1);
  }
}

// ── HTTP helpers ──────────────────────────────────────────────────────────────

function ucbHeaders() {
  const token = Buffer.from(`${UCB_API_KEY}:`).toString('base64');
  return { Authorization: `Basic ${token}`, 'Content-Type': 'application/json' };
}

function apiUrl(suffix = '') {
  return `${API_BASE}/orgs/${UCB_ORG_ID}/projects/${UCB_PROJECT_ID}/buildtargets/${UCB_BUILD_TARGET}/builds${suffix}`;
}

function request(url, { method = 'GET', headers = {}, body } = {}) {
  return new Promise((resolve, reject) => {
    const u   = new URL(url);
    const mod = u.protocol === 'https:' ? https : http;
    const req = mod.request(
      { hostname: u.hostname, path: u.pathname + u.search, method, headers },
      res => {
        if (res.statusCode >= 300 && res.statusCode < 400 && res.headers.location)
          return resolve(request(res.headers.location, { method: 'GET', headers: {} }));
        let data = '';
        res.on('data', c => data += c);
        res.on('end', () => resolve({ status: res.statusCode, body: data }));
      }
    );
    req.on('error', reject);
    if (body) req.write(body);
    req.end();
  });
}

function downloadFile(url, dest) {
  return new Promise((resolve, reject) => {
    const file = fs.createWriteStream(dest);
    function get(u) {
      const p = new URL(u);
      const m = p.protocol === 'https:' ? https : http;
      m.get(u, res => {
        if (res.statusCode >= 300 && res.statusCode < 400 && res.headers.location)
          return get(res.headers.location);
        res.pipe(file);
        file.on('finish', () => file.close(resolve));
      }).on('error', err => { fs.unlink(dest, () => {}); reject(err); });
    }
    get(url);
  });
}

function sleep(ms) { return new Promise(r => setTimeout(r, ms)); }

function sh(cmd) {
  if (dryRun) { console.log(`  [dry-run] $ ${cmd}`); return; }
  console.log(`  $ ${cmd}`);
  execSync(cmd, { cwd: REPO_ROOT, stdio: 'inherit' });
}

// ── Unity Cloud Build ─────────────────────────────────────────────────────────

async function triggerBuild() {
  console.log('  Triggering Unity Cloud Build...');
  if (dryRun) { console.log('  [dry-run] POST /builds'); return 0; }
  const res = await request(apiUrl(), {
    method: 'POST',
    headers: ucbHeaders(),
    body: JSON.stringify({ clean: false }),
  });
  if (res.status !== 202) throw new Error(`Trigger failed (HTTP ${res.status}): ${res.body}`);
  const [build] = JSON.parse(res.body);
  console.log(`  Build #${build.build} queued.`);
  return build.build;
}

async function fetchBuild(buildNumber) {
  const res = await request(apiUrl(`/${buildNumber}`), { headers: ucbHeaders() });
  if (res.status !== 200) throw new Error(`Fetch build failed (HTTP ${res.status}): ${res.body}`);
  return JSON.parse(res.body);
}

async function waitForBuild(buildNumber) {
  const terminal = new Set(['success', 'failure', 'canceled']);
  console.log(`  Polling build #${buildNumber} every ${POLL_MS / 1000}s...`);
  while (true) {
    const build  = await fetchBuild(buildNumber);
    const status = build.buildStatus;
    console.log(`  [${new Date().toLocaleTimeString()}] status: ${status}`);
    if (status === 'success') return build;
    if (terminal.has(status)) throw new Error(`Build ended with status "${status}".`);
    await sleep(POLL_MS);
  }
}

async function downloadIpa(build) {
  const url = build.links?.download_primary?.href;
  if (!url) throw new Error('No download URL in build response — build may lack an artifact.');
  console.log(`  Downloading IPA (build #${build.build})...`);
  if (dryRun) { console.log(`  [dry-run] would download to PocketCasebook.ipa`); return; }
  await downloadFile(url, IPA_DEST);
  const mb = (fs.statSync(IPA_DEST).size / 1024 / 1024).toFixed(1);
  console.log(`  ✓ Downloaded ${mb} MB → PocketCasebook.ipa`);
}

// ── Git + GitHub ──────────────────────────────────────────────────────────────

function commitAndPush(buildNumber) {
  console.log('  Committing IPA...');
  if (dryRun) { sh('git add PocketCasebook.ipa'); sh(`git commit -m "build: IPA from Unity Cloud Build #${buildNumber}"`); sh('git push'); return; }
  execSync('git add PocketCasebook.ipa', { cwd: REPO_ROOT, stdio: 'inherit' });
  const status = execSync('git status --porcelain PocketCasebook.ipa', { cwd: REPO_ROOT }).toString().trim();
  if (!status) {
    console.log('  IPA unchanged — skipping commit, pushing existing HEAD.');
    execSync('git push', { cwd: REPO_ROOT, stdio: 'inherit' });
    return;
  }
  execSync(`git commit -m "build: IPA from Unity Cloud Build #${buildNumber}"`, { cwd: REPO_ROOT, stdio: 'inherit' });
  execSync('git push', { cwd: REPO_ROOT, stdio: 'inherit' });
}

async function triggerUpload() {
  console.log('  Triggering TestFlight upload workflow...');
  if (dryRun) { console.log(`  [dry-run] POST /repos/${GH_REPO}/actions/workflows/${WORKFLOW}/dispatches`); return; }
  const token = process.env.GITHUB_PAT;
  if (!token) { console.error('  ERROR: GITHUB_PAT not set in .env — skipping workflow trigger.'); return; }
  const res = await request(`https://api.github.com/repos/${GH_REPO}/actions/workflows/${WORKFLOW}/dispatches`, {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${token}`,
      Accept: 'application/vnd.github+json',
      'X-GitHub-Api-Version': '2022-11-28',
      'User-Agent': 'pocket-casebook-deploy',
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ ref: 'master' }),
  });
  if (res.status === 204) {
    console.log('  ✓ Workflow triggered.');
  } else {
    console.error(`  ERROR triggering workflow (HTTP ${res.status}): ${res.body}`);
  }
  console.log(`  Track at: https://github.com/${GH_REPO}/actions`);
}

// ── Main ──────────────────────────────────────────────────────────────────────

async function main() {
  validateEnv();

  console.log('\n  === Pocket Casebook Deploy ===');
  if (dryRun)     console.log('  [dry-run mode — no API calls or git ops]\n');
  if (skipUpload) console.log('  [--skip-upload — will stop after git push]\n');

  let build;

  if (buildArg) {
    console.log(`  Using existing build #${buildArg}...`);
    if (!dryRun) {
      build = await fetchBuild(buildArg);
      if (build.buildStatus !== 'success')
        throw new Error(`Build #${buildArg} status is "${build.buildStatus}" — must be "success".`);
      console.log(`  ✓ Build #${buildArg} confirmed successful.`);
    } else {
      build = { build: buildArg, buildStatus: 'success', links: { download_primary: { href: 'https://example.com/fake.ipa' } } };
    }
  } else {
    const buildNumber = await triggerBuild();
    build = dryRun ? { build: buildNumber } : await waitForBuild(buildNumber);
  }

  await downloadIpa(build);
  commitAndPush(build.build);

  if (!skipUpload) await triggerUpload();

  console.log('\n  Done.\n');
}

main().catch(err => {
  console.error('\n  Fatal:', err.message);
  process.exit(1);
});
