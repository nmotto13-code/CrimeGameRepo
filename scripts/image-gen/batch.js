import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { spawnSync } from 'child_process';
import { collectStatus, printStatus } from './status.js';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PROMPTS_FILE = path.join(__dirname, 'prompts.json');
const BACKGROUND_FILE = path.join(__dirname, 'background-prompts.json');

const args = process.argv.slice(2);
const typeArg = args.includes('--type') ? args[args.indexOf('--type') + 1] : 'evidence';
const type = typeArg === 'backgrounds' ? 'backgrounds' : 'evidence';
const caseArg = args.includes('--case') ? normalizeCaseCode(args[args.indexOf('--case') + 1]) : undefined;
const flagAll = args.includes('--all');
const flagDry = args.includes('--dry-run');
const flagForce = args.includes('--force');
const providerArg = args.includes('--provider') ? args[args.indexOf('--provider') + 1] : undefined;

function normalizeCaseCode(value) {
  if (!value) return undefined;
  const match = value.toUpperCase().match(/C?(\d{3})/);
  return match ? `C${match[1]}` : value.toUpperCase();
}

function usage() {
  console.log('\n  Usage:');
  console.log('    node batch.js --case C011');
  console.log('    node batch.js --case C011 --dry-run');
  console.log('    node batch.js --all');
  console.log('    node batch.js --type backgrounds --case C011\n');
}

function needsWork(caseStatus) {
  const evidencePending = caseStatus.evidence.summary.pending > 0;
  const backgroundPending = caseStatus.background.summary.pending > 0;
  return type === 'backgrounds' ? backgroundPending : evidencePending;
}

function manifestCases() {
  if (type === 'backgrounds') {
    const manifest = JSON.parse(fs.readFileSync(BACKGROUND_FILE, 'utf8'));
    return new Set(manifest.backgrounds.map(item => `C${item.id.slice(4)}`));
  }

  const manifest = JSON.parse(fs.readFileSync(PROMPTS_FILE, 'utf8'));
  return new Set(manifest.evidence.map(item => normalizeCaseCode(item.case)));
}

function casesToRun() {
  if (caseArg) {
    return [caseArg];
  }

  if (!flagAll) {
    usage();
    process.exit(1);
  }

  const allowedCases = manifestCases();

  return collectStatus()
    .filter(result => allowedCases.has(result.caseCode))
    .filter(needsWork)
    .map(result => result.caseCode);
}

function runNodeScript(scriptName, extraArgs) {
  const result = spawnSync(process.execPath, [path.join(__dirname, scriptName), ...extraArgs], {
    cwd: __dirname,
    stdio: 'inherit',
  });

  if (result.status !== 0) {
    process.exit(result.status ?? 1);
  }
}

const targets = casesToRun();

if (!targets.length) {
  console.log('\n  Nothing to do. All targeted assets are already linked.\n');
  process.exit(0);
}

console.log(`\n  Running batch for ${targets.length} case(s) [type=${type}]${flagDry ? ' [dry-run]' : ''}\n`);

for (const caseCode of targets) {
  console.log(`  === ${caseCode} ===`);

  const generateArgs = ['--case', caseCode];
  if (type === 'backgrounds') {
    generateArgs.push('--type', 'backgrounds');
  }
  if (providerArg) {
    generateArgs.push('--provider', providerArg);
  }
  if (flagForce) {
    generateArgs.push('--force');
  }
  if (flagDry) {
    generateArgs.push('--dry-run');
  }

  runNodeScript('generate.js', generateArgs);

  if (flagDry) {
    console.log('  Skipping wire step in dry-run mode.\n');
    continue;
  }

  const wireArgs = ['--case', caseCode];
  if (type === 'backgrounds') {
    wireArgs.push('--type', 'backgrounds');
  }

  runNodeScript('wire-sprites.js', wireArgs);
  console.log('');
}

const results = collectStatus(caseArg);
console.log('  Status summary:');
printStatus(results);
