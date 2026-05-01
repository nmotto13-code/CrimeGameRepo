import OpenAI from 'openai';
import { GoogleGenAI } from '@google/genai';
import fs from 'fs';
import path from 'path';
import https from 'https';
import { execFileSync } from 'child_process';
import { fileURLToPath } from 'url';
import 'dotenv/config';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const CONFIG = {
  evidence: {
    manifestFile: path.resolve(__dirname, 'prompts.json'),
    itemKey: 'evidence',
    localOutput: path.resolve(__dirname, 'output'),
    unityOutput: path.resolve(__dirname, '../../Assets/Sprites/Evidence'),
    caseCode: item => item.case,
    fileName: item => `${item.id}.png`,
    displayId: item => item.id,
    displayName: item => item.name,
    interactivePath: item => `${item.id}.png`,
  },
  backgrounds: {
    manifestFile: path.resolve(__dirname, 'background-prompts.json'),
    itemKey: 'backgrounds',
    localOutput: path.resolve(__dirname, 'output-backgrounds'),
    unityOutput: path.resolve(__dirname, '../../Assets/Sprites/Backgrounds'),
    caseCode: item => `C${item.id.slice(4)}`,
    fileName: item => `${item.id}_${item.location}.jpg`,
    displayId: item => item.id,
    displayName: item => item.location,
    interactivePath: item => `${item.id}_${item.location}.jpg`,
  },
};

const OPENAI_MODEL = 'dall-e-3';
const OPENAI_SIZE = '1024x1024';
const OPENAI_QUALITY = 'standard';

const GOOGLE_MODEL_IMAGEN = 'imagen-4.0-generate-001';
const GOOGLE_MODEL_GEMINI = 'gemini-2.5-flash-image';

const COST = {
  openai: { standard: 0.04, hd: 0.08 },
  google: 0.04,
};

const args = process.argv.slice(2);
const flagAll = args.includes('--all');
const flagUnity = args.includes('--unity');
const flagForce = args.includes('--force');
const flagDry = args.includes('--dry-run');

function getFlagValue(flagName, fallback) {
  const index = args.indexOf(flagName);
  return index >= 0 ? args[index + 1] : fallback;
}

const typeArg = getFlagValue('--type', 'evidence');
const type = typeArg === 'backgrounds' ? 'backgrounds' : 'evidence';
const settings = CONFIG[type];

const providerArg = getFlagValue('--provider', 'openai');
const provider = ['google', 'openai'].includes(providerArg) ? providerArg : 'openai';

const caseFlag = normalizeCaseCode(getFlagValue('--case'));
const ignoredArgs = new Set(['--all', '--unity', '--force', '--dry-run', '--provider', '--case', '--type']);
if (args.includes('--provider')) ignoredArgs.add(providerArg);
if (args.includes('--case')) ignoredArgs.add(getFlagValue('--case'));
if (args.includes('--type')) ignoredArgs.add(getFlagValue('--type'));
const singleId = args.find(arg => !arg.startsWith('--') && !ignoredArgs.has(arg));

const outputDir = flagUnity ? settings.unityOutput : settings.localOutput;
const { stylePrefix, [settings.itemKey]: allPrompts } = JSON.parse(fs.readFileSync(settings.manifestFile, 'utf8'));

function normalizeCaseCode(value) {
  if (!value) return undefined;
  const match = value.toUpperCase().match(/C?(\d{3})/);
  return match ? `C${match[1]}` : value.toUpperCase();
}

function getCaseCode(item) {
  return settings.caseCode(item).toUpperCase();
}

function buildFullPrompt(item) {
  return `${stylePrefix}, ${item.prompt}`;
}

function resolveSingleItem(input) {
  if (!input) return null;
  const lowered = input.toLowerCase();

  return allPrompts.find(item => {
    const displayId = settings.displayId(item).toLowerCase();
    const fileStem = path.parse(settings.fileName(item)).name.toLowerCase();
    return displayId === lowered || fileStem === lowered;
  }) ?? null;
}

let targets = [];

if (singleId) {
  const item = resolveSingleItem(singleId);
  if (!item) {
    console.error(`\n  ERROR: No prompt found for ID "${singleId}"`);
    console.error(`  Available IDs:\n  ${allPrompts.map(item => settings.displayId(item)).join(', ')}\n`);
    process.exit(1);
  }
  targets = [item];
} else if (caseFlag) {
  targets = allPrompts.filter(item => getCaseCode(item) === caseFlag);
  if (!targets.length) {
    console.error(`\n  ERROR: No prompts for case "${caseFlag}"\n`);
    process.exit(1);
  }
} else if (flagAll) {
  targets = allPrompts;
} else {
  console.log(`\n  Available ${type} IDs:\n`);
  allPrompts.forEach(item => {
    const exists = fs.existsSync(path.join(outputDir, settings.fileName(item)));
    console.log(`  ${exists ? '✓' : '○'} ${settings.displayId(item).padEnd(16)} ${settings.displayName(item)} (${getCaseCode(item)})`);
  });
  console.log('\n  Usage:');
  console.log('    node generate.js C003_E001                    single evidence item');
  console.log('    node generate.js --case C003                  all evidence for one case');
  console.log('    node generate.js --all                        everything missing');
  console.log('    node generate.js --all --provider google      use Google Imagen 4');
  console.log('    node generate.js --all --force                regenerate existing');
  console.log('    node generate.js --all --dry-run              preview cost only');
  console.log('    node generate.js --type backgrounds --case C011\n');
  process.exit(0);
}

if (!flagForce) {
  const before = targets.length;
  targets = targets.filter(item => !fs.existsSync(path.join(outputDir, settings.fileName(item))));
  const skipped = before - targets.length;
  if (skipped > 0) {
    console.log(`\n  Skipping ${skipped} already-generated file(s). Use --force to regenerate.`);
  }
}

if (!targets.length) {
  console.log('\n  Nothing to generate - all files already exist.\n');
  process.exit(0);
}

const costPer = provider === 'google' ? COST.google : COST.openai[OPENAI_QUALITY];
const totalCost = (targets.length * costPer).toFixed(2);

console.log(`\n  Type: ${type}`);
console.log(`  Provider: ${provider === 'google' ? 'Google Imagen 4 / Gemini Flash fallback' : 'OpenAI DALL-E 3'}`);
console.log(`  Generating ${targets.length} image(s) -> ~$${totalCost} USD`);
console.log(`  Output: ${outputDir}\n`);

if (flagDry) {
  targets.forEach((item, index) => {
    console.log(`  [${index + 1}/${targets.length}] ${settings.displayId(item)} - ${settings.displayName(item)}`);
    console.log(`       ${buildFullPrompt(item).slice(0, 120)}...\n`);
  });
  console.log('  Dry run complete. No images generated.\n');
  process.exit(0);
}

if (provider === 'openai' && !process.env.OPENAI_API_KEY) {
  console.error('\n  ERROR: OPENAI_API_KEY not set in .env\n');
  process.exit(1);
}

if (provider === 'google' && !process.env.GOOGLE_API_KEY) {
  console.error('\n  ERROR: GOOGLE_API_KEY not set in .env\n');
  process.exit(1);
}

fs.mkdirSync(outputDir, { recursive: true });

const openaiClient = provider === 'openai'
  ? new OpenAI({ apiKey: process.env.OPENAI_API_KEY })
  : null;

const googleClient = provider === 'google'
  ? new GoogleGenAI({ apiKey: process.env.GOOGLE_API_KEY })
  : null;

function downloadImage(url, destPath) {
  return new Promise((resolve, reject) => {
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

async function generateOpenAI(item, outPath) {
  const response = await openaiClient.images.generate({
    model: OPENAI_MODEL,
    prompt: buildFullPrompt(item),
    n: 1,
    size: OPENAI_SIZE,
    quality: OPENAI_QUALITY,
  });
  await downloadImage(response.data[0].url, outPath);
}

async function generateGoogle(item, outPath) {
  const prompt = buildFullPrompt(item);

  try {
    const response = await googleClient.models.generateImages({
      model: GOOGLE_MODEL_IMAGEN,
      prompt,
      config: { numberOfImages: 1, outputMimeType: 'image/png' },
    });

    const bytes = response.generatedImages?.[0]?.image?.imageBytes;
    if (!bytes) {
      throw new Error('No image bytes in Imagen response');
    }

    fs.writeFileSync(outPath, Buffer.from(bytes, 'base64'));
  } catch (error) {
    const message = error?.message ?? String(error);
    const isUnavailable = message.includes('NOT_FOUND')
      || message.includes('not found')
      || message.includes('paid')
      || message.includes('upgrade')
      || message.includes('INVALID_ARGUMENT');

    if (!isUnavailable) {
      throw error;
    }

    process.stdout.write('(Imagen unavailable, using Gemini Flash) ');
    const response = await googleClient.models.generateContent({
      model: GOOGLE_MODEL_GEMINI,
      contents: [{ role: 'user', parts: [{ text: prompt }] }],
      config: { responseModalities: ['IMAGE', 'TEXT'] },
    });

    const parts = response.candidates?.[0]?.content?.parts ?? [];
    const imagePart = parts.find(part => part.inlineData?.mimeType?.startsWith('image/'));
    if (!imagePart) {
      throw new Error('No image returned from Gemini Flash');
    }

    fs.writeFileSync(outPath, Buffer.from(imagePart.inlineData.data, 'base64'));
  }
}

function convertPngToJpeg(pngPath, jpgPath) {
  if (process.platform !== 'win32') {
    throw new Error('Background JPEG conversion currently requires Windows PowerShell');
  }

  const escape = value => value.replace(/'/g, "''");
  const command = [
    'Add-Type -AssemblyName System.Drawing',
    `$src = '${escape(pngPath)}'`,
    `$dst = '${escape(jpgPath)}'`,
    '$img = [System.Drawing.Image]::FromFile($src)',
    '$img.Save($dst, [System.Drawing.Imaging.ImageFormat]::Jpeg)',
    '$img.Dispose()',
  ].join('; ');

  execFileSync('powershell', ['-NoProfile', '-Command', command], { stdio: 'ignore' });
}

async function generateOne(item, index, total) {
  const finalPath = path.join(outputDir, settings.fileName(item));
  const tempPath = type === 'backgrounds'
    ? `${path.join(outputDir, `${settings.displayId(item)}_${item.location}`)}.tmp.png`
    : finalPath;

  process.stdout.write(`  [${index + 1}/${total}] ${settings.displayId(item)} - ${settings.displayName(item)} ... `);

  try {
    if (provider === 'google') {
      await generateGoogle(item, tempPath);
    } else {
      await generateOpenAI(item, tempPath);
    }

    if (type === 'backgrounds') {
      convertPngToJpeg(tempPath, finalPath);
      fs.unlinkSync(tempPath);
    }

    console.log('✓ saved');
    return { id: settings.displayId(item), status: 'ok' };
  } catch (error) {
    if (type === 'backgrounds' && fs.existsSync(tempPath)) {
      fs.unlinkSync(tempPath);
    }
    console.log('✗ FAILED');
    console.error(`     ${error.message}`);
    return { id: settings.displayId(item), status: 'error', error: error.message };
  }
}

async function run() {
  const results = [];

  for (let index = 0; index < targets.length; index++) {
    results.push(await generateOne(targets[index], index, targets.length));
    if (index < targets.length - 1) {
      await new Promise(resolve => setTimeout(resolve, 500));
    }
  }

  const ok = results.filter(result => result.status === 'ok').length;
  const failed = results.filter(result => result.status === 'error');

  console.log(`\n  Done. ${ok}/${targets.length} generated successfully.`);

  if (failed.length) {
    console.log('\n  Failed:');
    failed.forEach(result => console.log(`    ✗ ${result.id}: ${result.error}`));
  }

  if (!flagUnity && ok > 0) {
    console.log(`\n  Images saved to: ${path.relative(__dirname, settings.localOutput)}/`);
    console.log(`  Review, then run wire-sprites.js${type === 'backgrounds' ? ' --type backgrounds' : ''} to import into Unity.\n`);
  } else if (flagUnity && ok > 0) {
    console.log(`\n  Images saved to ${settings.unityOutput}.\n`);
  }
}

run().catch(error => {
  console.error('\n  Fatal error:', error.message);
  process.exit(1);
});
