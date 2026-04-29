import OpenAI from 'openai';
import { GoogleGenAI } from '@google/genai';
import fs from 'fs';
import path from 'path';
import https from 'https';
import { fileURLToPath } from 'url';
import 'dotenv/config';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

// ── Config ──────────────────────────────────────────────────────────────────

const UNITY_OUTPUT = path.resolve(__dirname, '../../Assets/Sprites/Evidence');
const LOCAL_OUTPUT = path.resolve(__dirname, 'output');
const PROMPTS_FILE = path.resolve(__dirname, 'prompts.json');

// OpenAI settings
const OPENAI_MODEL   = 'dall-e-3';
const OPENAI_SIZE    = '1024x1024';
const OPENAI_QUALITY = 'standard'; // or 'hd'

// Google settings
const GOOGLE_MODEL_IMAGEN  = 'imagen-4.0-generate-001';
const GOOGLE_MODEL_GEMINI  = 'gemini-2.5-flash-image';

const COST = {
  openai: { standard: 0.04, hd: 0.08 },
  google: 0.04,
};

// ── CLI args ────────────────────────────────────────────────────────────────
//
//  node generate.js                             interactive list
//  node generate.js C003_E001                   single item
//  node generate.js --case C003                 all for one case
//  node generate.js --all                       everything missing
//  node generate.js --all --provider google     use Google Imagen 3
//  node generate.js --all --provider openai     use DALL-E 3 (default)
//  node generate.js --all --unity               write to Assets/Sprites/Evidence/
//  node generate.js --all --force               regenerate existing files
//  node generate.js --all --dry-run             preview cost, no API calls
//
// ────────────────────────────────────────────────────────────────────────────

const args     = process.argv.slice(2);
const flagAll  = args.includes('--all');
const flagUnity = args.includes('--unity');
const flagForce = args.includes('--force');
const flagDry   = args.includes('--dry-run');

const providerArg = args.includes('--provider') ? args[args.indexOf('--provider') + 1] : 'openai';
const provider    = ['google', 'openai'].includes(providerArg) ? providerArg : 'openai';

const caseFlag = args.includes('--case') ? args[args.indexOf('--case') + 1] : undefined;
const singleId = args.find(a => !a.startsWith('--') && a !== caseFlag && a !== providerArg);

const outputDir = flagUnity ? UNITY_OUTPUT : LOCAL_OUTPUT;

// ── Load prompts ─────────────────────────────────────────────────────────────

const { stylePrefix, evidence: allPrompts } = JSON.parse(fs.readFileSync(PROMPTS_FILE, 'utf8'));

function buildFullPrompt(item) {
  return `${stylePrefix}, ${item.prompt}`;
}

// ── Select targets ────────────────────────────────────────────────────────────

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
  if (!targets.length) { console.error(`\n  ERROR: No prompts for case "${caseFlag}"\n`); process.exit(1); }
} else if (flagAll) {
  targets = allPrompts;
} else {
  // Interactive list
  console.log('\n  Available evidence IDs:\n');
  allPrompts.forEach(e => {
    const exists = fs.existsSync(path.join(outputDir, `${e.id}.png`));
    console.log(`  ${exists ? '✓' : '○'} ${e.id.padEnd(12)} ${e.name} (${e.case})`);
  });
  console.log('\n  Usage:');
  console.log('    node generate.js C003_E001                    single item');
  console.log('    node generate.js --case C003                  all for case 3');
  console.log('    node generate.js --all                        everything missing');
  console.log('    node generate.js --all --provider google      use Google Imagen 3');
  console.log('    node generate.js --all --force                regenerate all');
  console.log('    node generate.js --all --dry-run              preview cost only\n');
  process.exit(0);
}

// Skip existing unless --force
if (!flagForce) {
  const before = targets.length;
  targets = targets.filter(e => !fs.existsSync(path.join(outputDir, `${e.id}.png`)));
  const skipped = before - targets.length;
  if (skipped > 0) console.log(`\n  Skipping ${skipped} already-generated file(s). Use --force to regenerate.`);
}

if (!targets.length) {
  console.log('\n  Nothing to generate — all files already exist.\n');
  process.exit(0);
}

// ── Validate env ──────────────────────────────────────────────────────────────

if (provider === 'openai' && !process.env.OPENAI_API_KEY) {
  console.error('\n  ERROR: OPENAI_API_KEY not set in .env\n'); process.exit(1);
}
if (provider === 'google' && !process.env.GOOGLE_API_KEY) {
  console.error('\n  ERROR: GOOGLE_API_KEY not set in .env\n'); process.exit(1);
}

// ── Cost estimate ─────────────────────────────────────────────────────────────

const costPer  = provider === 'google' ? COST.google : COST.openai[OPENAI_QUALITY];
const totalCost = (targets.length * costPer).toFixed(2);

console.log(`\n  Provider: ${provider === 'google' ? 'Google Imagen 4 / Gemini Flash fallback' : 'OpenAI DALL-E 3'}`);
console.log(`  Generating ${targets.length} image(s) → ~$${totalCost} USD`);
console.log(`  Output: ${outputDir}\n`);

if (flagDry) {
  targets.forEach((e, i) => {
    console.log(`  [${i + 1}/${targets.length}] ${e.id} — ${e.name}`);
    console.log(`       ${buildFullPrompt(e).slice(0, 100)}…\n`);
  });
  console.log('  Dry run complete. No images generated.\n');
  process.exit(0);
}

// ── Ensure output dir ─────────────────────────────────────────────────────────

fs.mkdirSync(outputDir, { recursive: true });

// ── Provider: OpenAI ──────────────────────────────────────────────────────────

const openaiClient = provider === 'openai'
  ? new OpenAI({ apiKey: process.env.OPENAI_API_KEY })
  : null;

function downloadImage(url, destPath) {
  return new Promise((resolve, reject) => {
    const file = fs.createWriteStream(destPath);
    https.get(url, res => {
      res.pipe(file);
      file.on('finish', () => file.close(resolve));
    }).on('error', err => { fs.unlink(destPath, () => {}); reject(err); });
  });
}

async function generateOpenAI(item, outPath) {
  const response = await openaiClient.images.generate({
    model:   OPENAI_MODEL,
    prompt:  buildFullPrompt(item),
    n:       1,
    size:    OPENAI_SIZE,
    quality: OPENAI_QUALITY,
  });
  await downloadImage(response.data[0].url, outPath);
}

// ── Provider: Google Imagen 3 ─────────────────────────────────────────────────

const googleClient = provider === 'google'
  ? new GoogleGenAI({ apiKey: process.env.GOOGLE_API_KEY })
  : null;

async function generateGoogle(item, outPath) {
  const prompt = buildFullPrompt(item);

  // Try Imagen 3 first (requires billing); fall back to Gemini Flash (free tier)
  try {
    const response = await googleClient.models.generateImages({
      model:  GOOGLE_MODEL_IMAGEN,
      prompt,
      config: { numberOfImages: 1, outputMimeType: 'image/png' },
    });
    const b64 = response.generatedImages?.[0]?.image?.imageBytes;
    if (!b64) throw new Error('No image bytes in Imagen response');
    fs.writeFileSync(outPath, Buffer.from(b64, 'base64'));
  } catch (err) {
    // Fall back if Imagen is unavailable (not on paid plan, or not found)
    const isUnavailable = err.message.includes('NOT_FOUND') || err.message.includes('not found')
                       || err.message.includes('paid') || err.message.includes('upgrade')
                       || err.message.includes('INVALID_ARGUMENT');
    if (!isUnavailable) throw err;

    // Fall back to Gemini 2.0 Flash image generation (free tier)
    process.stdout.write('(Imagen unavailable, using Gemini Flash) ');
    const response = await googleClient.models.generateContent({
      model:    GOOGLE_MODEL_GEMINI,
      contents: [{ role: 'user', parts: [{ text: prompt }] }],
      config:   { responseModalities: ['IMAGE', 'TEXT'] },
    });

    const parts    = response.candidates?.[0]?.content?.parts ?? [];
    const imgPart  = parts.find(p => p.inlineData?.mimeType?.startsWith('image/'));
    if (!imgPart) throw new Error('No image returned from Gemini Flash');
    fs.writeFileSync(outPath, Buffer.from(imgPart.inlineData.data, 'base64'));
  }
}

// ── Generate loop ─────────────────────────────────────────────────────────────

async function generateOne(item, index, total) {
  const outPath = path.join(outputDir, `${item.id}.png`);
  process.stdout.write(`  [${index + 1}/${total}] ${item.id} — ${item.name} … `);

  try {
    if (provider === 'google') await generateGoogle(item, outPath);
    else                        await generateOpenAI(item, outPath);
    console.log('✓ saved');
    return { id: item.id, status: 'ok' };
  } catch (err) {
    console.log('✗ FAILED');
    console.error(`     ${err.message}`);
    return { id: item.id, status: 'error', error: err.message };
  }
}

async function run() {
  const results = [];
  for (let i = 0; i < targets.length; i++) {
    results.push(await generateOne(targets[i], i, targets.length));
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
    console.log('  Review, then re-run with --unity to copy to Assets/Sprites/Evidence/\n');
  } else if (flagUnity && ok > 0) {
    console.log(`\n  Images saved to Assets/Sprites/Evidence/ — run "Casebook → Wire Case Backgrounds" in Unity.\n`);
  }
}

run().catch(err => { console.error('\n  Fatal error:', err.message); process.exit(1); });
