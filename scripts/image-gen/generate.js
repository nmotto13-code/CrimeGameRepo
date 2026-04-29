import OpenAI from 'openai';
import fs from 'fs';
import path from 'path';
import https from 'https';
import { fileURLToPath } from 'url';
import 'dotenv/config';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

// ── Config ─────────────────────────────────────────────────────────────────

const UNITY_OUTPUT  = path.resolve(__dirname, '../../Assets/Sprites/Evidence');
const LOCAL_OUTPUT  = path.resolve(__dirname, 'output');
const PROMPTS_FILE  = path.resolve(__dirname, 'prompts.json');

const MODEL   = 'dall-e-3';
const SIZE    = '1024x1024';
const QUALITY = 'standard'; // 'hd' for higher quality at 2× cost

const COST_PER_IMAGE = { standard: 0.04, hd: 0.08 };

// ── CLI args ────────────────────────────────────────────────────────────────
//
//  node generate.js                        — interactive: list all, pick one
//  node generate.js C003_E001              — generate single ID
//  node generate.js --case C003            — generate all evidence for case C003
//  node generate.js --all                  — generate entire prompts.json
//  node generate.js --all --unity          — output directly to Assets/Sprites/Evidence/
//  node generate.js --all --force          — re-generate even if file exists
//  node generate.js --all --dry-run        — print what would be generated + cost, don't call API
//
// ────────────────────────────────────────────────────────────────────────────

const args    = process.argv.slice(2);
const flagAll    = args.includes('--all');
const flagUnity  = args.includes('--unity');
const flagForce  = args.includes('--force');
const flagDry    = args.includes('--dry-run');
const caseFlag   = args[args.indexOf('--case') + 1];
const singleId   = args.find(a => !a.startsWith('--') && a !== caseFlag);

const outputDir = flagUnity ? UNITY_OUTPUT : LOCAL_OUTPUT;

// ── Load prompts ─────────────────────────────────────────────────────────────

const { stylePrefix, evidence: allPrompts } = JSON.parse(fs.readFileSync(PROMPTS_FILE, 'utf8'));

function buildFullPrompt(item) {
  return `${stylePrefix}, ${item.prompt}`;
}

// ── Select which items to generate ───────────────────────────────────────────

let targets = [];

if (singleId) {
  const item = allPrompts.find(e => e.id.toLowerCase() === singleId.toLowerCase());
  if (!item) {
    console.error(`\n  ERROR: No prompt found for ID "${singleId}"`);
    console.error(`  Available IDs:\n  ${allPrompts.map(e => e.id).join(', ')}\n`);
    process.exit(1);
  }
  targets = [item];
} else if (caseFlag) {
  targets = allPrompts.filter(e => e.case.toLowerCase() === caseFlag.toLowerCase());
  if (!targets.length) {
    console.error(`\n  ERROR: No prompts found for case "${caseFlag}"\n`);
    process.exit(1);
  }
} else if (flagAll) {
  targets = allPrompts;
} else {
  // Interactive list mode — show what's available and exit with usage hint
  console.log('\n  Available evidence IDs:\n');
  allPrompts.forEach(e => {
    const exists = fs.existsSync(path.join(outputDir, `${e.id}.png`));
    console.log(`  ${exists ? '✓' : '○'} ${e.id.padEnd(12)} ${e.name} (${e.case})`);
  });
  console.log('\n  Usage:');
  console.log('    node generate.js C003_E001          generate single item');
  console.log('    node generate.js --case C003        generate all for case 3');
  console.log('    node generate.js --all              generate everything missing');
  console.log('    node generate.js --all --force      regenerate everything');
  console.log('    node generate.js --all --dry-run    preview cost, no API calls\n');
  process.exit(0);
}

// Skip already-generated files unless --force
if (!flagForce) {
  const before = targets.length;
  targets = targets.filter(e => !fs.existsSync(path.join(outputDir, `${e.id}.png`)));
  const skipped = before - targets.length;
  if (skipped > 0) console.log(`  Skipping ${skipped} already-generated file(s). Use --force to regenerate.`);
}

if (!targets.length) {
  console.log('\n  Nothing to generate — all files already exist.\n');
  process.exit(0);
}

// ── Cost estimate ─────────────────────────────────────────────────────────────

const cost = (targets.length * COST_PER_IMAGE[QUALITY]).toFixed(2);
console.log(`\n  Generating ${targets.length} image(s) at ${QUALITY} quality → ~$${cost} USD`);
console.log(`  Output: ${outputDir}\n`);

// ── Validate env (only needed for actual generation) ─────────────────────────

if (!process.env.OPENAI_API_KEY) {
  console.error('\n  ERROR: OPENAI_API_KEY not set.');
  console.error('  Copy .env.example → .env and add your key.\n');
  process.exit(1);
}

const client = new OpenAI({ apiKey: process.env.OPENAI_API_KEY });

if (flagDry) {
  targets.forEach((e, i) => {
    console.log(`  [${i + 1}/${targets.length}] ${e.id} — ${e.name}`);
    console.log(`       ${buildFullPrompt(e).slice(0, 100)}…\n`);
  });
  console.log('  Dry run complete. No images generated.\n');
  process.exit(0);
}

// ── Ensure output directory exists ────────────────────────────────────────────

fs.mkdirSync(outputDir, { recursive: true });

// ── Generate ──────────────────────────────────────────────────────────────────

function downloadImage(url, destPath) {
  return new Promise((resolve, reject) => {
    const file = fs.createWriteStream(destPath);
    https.get(url, res => {
      res.pipe(file);
      file.on('finish', () => file.close(resolve));
    }).on('error', err => {
      fs.unlink(destPath, () => {});
      reject(err);
    });
  });
}

async function generateOne(item, index, total) {
  const label   = `[${index + 1}/${total}] ${item.id} — ${item.name}`;
  const outPath = path.join(outputDir, `${item.id}.png`);
  const prompt  = buildFullPrompt(item);

  process.stdout.write(`  ${label} … `);

  try {
    const response = await client.images.generate({
      model:   MODEL,
      prompt,
      n:       1,
      size:    SIZE,
      quality: QUALITY,
    });

    const imageUrl = response.data[0].url;
    await downloadImage(imageUrl, outPath);
    console.log('✓ saved');
    return { id: item.id, status: 'ok', path: outPath };
  } catch (err) {
    console.log('✗ FAILED');
    console.error(`     ${err.message}`);
    return { id: item.id, status: 'error', error: err.message };
  }
}

async function run() {
  const results = [];

  for (let i = 0; i < targets.length; i++) {
    const result = await generateOne(targets[i], i, targets.length);
    results.push(result);

    // Small pause between requests to avoid rate-limit spikes
    if (i < targets.length - 1) await new Promise(r => setTimeout(r, 500));
  }

  const ok     = results.filter(r => r.status === 'ok').length;
  const failed = results.filter(r => r.status === 'error');

  console.log(`\n  Done. ${ok}/${targets.length} generated successfully.`);

  if (failed.length) {
    console.log('\n  Failed:');
    failed.forEach(f => console.log(`    ✗ ${f.id}: ${f.error}`));
  }

  if (!flagUnity && ok > 0) {
    console.log(`\n  Images saved to: scripts/image-gen/output/`);
    console.log('  Review them, then re-run with --unity to copy to Assets/Sprites/Evidence/');
    console.log('  or manually drag them into Unity.\n');
  } else if (flagUnity && ok > 0) {
    console.log(`\n  Images saved directly to Assets/Sprites/Evidence/`);
    console.log('  Run "Casebook → Wire Case Backgrounds" in Unity to assign sprites.\n');
  }
}

run().catch(err => {
  console.error('\n  Fatal error:', err.message);
  process.exit(1);
});
