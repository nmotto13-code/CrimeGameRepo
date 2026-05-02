import OpenAI from 'openai';
import fs from 'fs';
import path from 'path';
import https from 'https';
import { fileURLToPath } from 'url';
import 'dotenv/config';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(__dirname, '../..');
const manifestPath = path.join(__dirname, 'presentation-prompts.json');
const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));

const OPENAI_MODEL = 'dall-e-3';

const args = process.argv.slice(2);
const flagAll = args.includes('--all');
const flagDry = args.includes('--dry-run');
const flagForce = args.includes('--force');
const categoryArg = args.includes('--category') ? args[args.indexOf('--category') + 1] : null;
const explicitIds = args.filter(arg => !arg.startsWith('--') && arg !== categoryArg);

const entries = manifest.artEntries;

let targets = [];
if (explicitIds.length) {
  const requestedIds = new Set(explicitIds.map(arg => arg.toLowerCase()));
  targets = entries.filter(entry => requestedIds.has(entry.id.toLowerCase()));
} else if (categoryArg) {
  targets = entries.filter(entry => entry.category.toLowerCase() === categoryArg.toLowerCase());
} else if (flagAll) {
  targets = entries;
} else {
  console.log('\n  Usage:');
  console.log('    node generate-presentation.js --all');
  console.log('    node generate-presentation.js --all --force');
  console.log('    node generate-presentation.js --category precinct');
  console.log('    node generate-presentation.js --category suspect_portrait');
  console.log('    node generate-presentation.js S011');
  console.log('    node generate-presentation.js precinct_home_hub city_map_base --force\n');
  process.exit(0);
}

if (!targets.length) {
  console.log('\n  No matching presentation entries.\n');
  process.exit(0);
}

if (!flagForce) {
  targets = targets.filter(entry => !fs.existsSync(path.join(repoRoot, entry.output)));
}

if (!targets.length) {
  console.log('\n  Nothing to generate - matching presentation assets already exist.\n');
  process.exit(0);
}

if (flagDry) {
  console.log(`\n  Presentation generation preview: ${targets.length} asset(s)\n`);
  targets.forEach(entry => {
    console.log(`  ${entry.id} [${entry.category}] -> ${entry.output}`);
    console.log(`     ${entry.prompt.slice(0, 140)}...\n`);
  });
  process.exit(0);
}

if (!process.env.OPENAI_API_KEY) {
  console.error('\n  ERROR: OPENAI_API_KEY not set in .env\n');
  process.exit(1);
}

const openai = new OpenAI({ apiKey: process.env.OPENAI_API_KEY });

function downloadImage(url, destPath) {
  return new Promise((resolve, reject) => {
    fs.mkdirSync(path.dirname(destPath), { recursive: true });
    const file = fs.createWriteStream(destPath);
    https.get(url, response => {
      response.pipe(file);
      file.on('finish', () => file.close(resolve));
    }).on('error', error => {
      fs.unlink(destPath, () => {});
      reject(error);
    });
  });
}

async function generateOne(entry, index, total) {
  const destPath = path.join(repoRoot, entry.output);
  process.stdout.write(`  [${index + 1}/${total}] ${entry.id} ... `);
  try {
    const response = await openai.images.generate({
      model: OPENAI_MODEL,
      prompt: entry.prompt,
      n: 1,
      size: entry.size,
      quality: 'standard',
    });
    await downloadImage(response.data[0].url, destPath);
    console.log('✓ saved');
  } catch (error) {
    console.log('✗ FAILED');
    console.error(`     ${error.message}`);
  }
}

console.log(`\n  Generating ${targets.length} presentation asset(s)\n`);
for (let index = 0; index < targets.length; index++) {
  await generateOne(targets[index], index, targets.length);
  if (index < targets.length - 1) {
    await new Promise(resolve => setTimeout(resolve, 500));
  }
}
