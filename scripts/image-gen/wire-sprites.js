/**
 * wire-sprites.js
 *
 * Evidence:
 *   1. Copies generated PNGs to Assets/Sprites/Evidence/
 *   2. Creates Unity .meta files with stable GUID reuse
 *   3. Patches the matching EvidenceData asset to reference the sprite
 *
 * Backgrounds:
 *   1. Copies generated background images to Assets/Sprites/Backgrounds/
 *   2. Creates Unity .meta files with stable GUID reuse
 *   3. Patches the matching CaseData asset sceneBackground reference
 *
 * Usage:
 *   node wire-sprites.js
 *   node wire-sprites.js C003_E001
 *   node wire-sprites.js --case C011
 *   node wire-sprites.js --type backgrounds --case C011
 *   node wire-sprites.js --dry-run
 */

import fs from 'fs';
import path from 'path';
import crypto from 'crypto';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const TYPE_CONFIG = {
  evidence: {
    outputDir: path.resolve(__dirname, 'output'),
    spritesDir: path.resolve(__dirname, '../../Assets/Sprites/Evidence'),
    itemExtensions: ['.png'],
  },
  backgrounds: {
    outputDir: path.resolve(__dirname, 'output-backgrounds'),
    spritesDir: path.resolve(__dirname, '../../Assets/Sprites/Backgrounds'),
    itemExtensions: ['.jpg', '.jpeg', '.png'],
  },
};

const EVIDENCE_DIR = path.resolve(__dirname, '../../Assets/ScriptableObjects/Cases/Evidence');
const CASE_DIR = path.resolve(__dirname, '../../Assets/Resources/Cases');

const args = process.argv.slice(2);
function getFlagValue(flagName, fallback) {
  const index = args.indexOf(flagName);
  return index >= 0 ? args[index + 1] : fallback;
}

const typeArg = getFlagValue('--type', 'evidence');
const type = typeArg === 'backgrounds' ? 'backgrounds' : 'evidence';
const caseFlag = normalizeCaseCode(getFlagValue('--case'));
const flagDry = args.includes('--dry-run');
const ignoredArgs = new Set(['--type', '--case', '--dry-run']);
if (args.includes('--type')) ignoredArgs.add(typeArg);
if (args.includes('--case')) ignoredArgs.add(getFlagValue('--case'));
const singleArg = args.find(arg => !arg.startsWith('--') && !ignoredArgs.has(arg));

const config = TYPE_CONFIG[type];

function normalizeCaseCode(value) {
  if (!value) return undefined;
  const match = value.toUpperCase().match(/C?(\d{3})/);
  return match ? `C${match[1]}` : value.toUpperCase();
}

function newGuid() {
  return crypto.randomBytes(16).toString('hex');
}

function makeMeta(guid) {
  return `fileFormatVersion: 2
guid: ${guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {x: 0.5, y: 0.5}
  spritePixelsToUnits: 100
  spriteBorder: {x: 0, y: 0, z: 0, w: 0}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 4
    buildTarget: Standalone
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 4
    buildTarget: iOS
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData:
    physicsShape: []
    bones: []
    spriteID: 5e97eb03825dee720800000000000000
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
`;
}

function patchEvidenceAsset(assetPath, guid) {
  if (!fs.existsSync(assetPath)) {
    return { ok: false, reason: `asset not found: ${path.basename(assetPath)}` };
  }

  const content = fs.readFileSync(assetPath, 'utf8');
  const newRef = `imageSprite: {fileID: 21300000, guid: ${guid}, type: 3}`;
  const updated = content
    .replace(/imageSprite: \{fileID: 0\}/, newRef)
    .replace(/imageSprite: \{fileID: 21300000, guid: \w+, type: 3\}/, newRef);

  if (updated === content) {
    return { ok: false, reason: `imageSprite line not found in ${path.basename(assetPath)}` };
  }

  if (!flagDry) {
    fs.writeFileSync(assetPath, updated, 'utf8');
  }

  return { ok: true };
}

function patchCaseAsset(assetPath, guid) {
  if (!fs.existsSync(assetPath)) {
    return { ok: false, reason: `case asset not found: ${path.basename(assetPath)}` };
  }

  const content = fs.readFileSync(assetPath, 'utf8');
  const newRef = `sceneBackground: {fileID: 21300000, guid: ${guid}, type: 3}`;
  const updated = content
    .replace(/sceneBackground: \{fileID: 0\}/, newRef)
    .replace(/sceneBackground: \{fileID: 21300000, guid: \w+, type: 3\}/, newRef);

  if (updated === content) {
    return { ok: false, reason: `sceneBackground line not found in ${path.basename(assetPath)}` };
  }

  if (!flagDry) {
    fs.writeFileSync(assetPath, updated, 'utf8');
  }

  return { ok: true };
}

function listSourceFiles() {
  if (!fs.existsSync(config.outputDir)) {
    return [];
  }

  const files = fs.readdirSync(config.outputDir)
    .filter(file => config.itemExtensions.includes(path.extname(file).toLowerCase()));

  if (singleArg) {
    const needle = singleArg.toLowerCase();
    return files.filter(file => {
      const lowerFile = file.toLowerCase();
      const stem = path.parse(lowerFile).name;
      return lowerFile === needle || stem === needle || lowerFile.startsWith(`${needle}_`);
    });
  }

  if (caseFlag) {
    const prefix = type === 'backgrounds'
      ? `case${caseFlag.slice(1).toLowerCase()}_`
      : `${caseFlag.toUpperCase()}_`;

    return files.filter(file => file.toLowerCase().startsWith(prefix.toLowerCase()));
  }

  return files;
}

function getEvidencePlan(file) {
  const id = path.basename(file, path.extname(file));
  return {
    label: id,
    srcPath: path.join(config.outputDir, file),
    dstPath: path.join(config.spritesDir, file),
    assetPath: path.join(EVIDENCE_DIR, `${id}.asset`),
    patch: patchEvidenceAsset,
  };
}

function getBackgroundPlan(file) {
  const lowerFile = file.toLowerCase();
  const match = lowerFile.match(/^case(\d{3})_/);
  if (!match) {
    return null;
  }

  const caseNumber = match[1];
  return {
    label: `case${caseNumber}`,
    srcPath: path.join(config.outputDir, file),
    dstPath: path.join(config.spritesDir, file),
    assetPath: path.join(CASE_DIR, `Case_${caseNumber}.asset`),
    patch: patchCaseAsset,
  };
}

function getPlan(file) {
  return type === 'backgrounds' ? getBackgroundPlan(file) : getEvidencePlan(file);
}

fs.mkdirSync(config.spritesDir, { recursive: true });

const sourceFiles = listSourceFiles();

if (!sourceFiles.length) {
  console.log(`\n  No source image files found in ${path.relative(__dirname, config.outputDir)}/. Run generate.js first.\n`);
  process.exit(0);
}

console.log(`\n  ${flagDry ? 'Previewing' : 'Wiring'} ${sourceFiles.length} ${type} sprite(s)...\n`);

let ok = 0;
let failed = 0;

for (const file of sourceFiles) {
  const plan = getPlan(file);
  if (!plan) {
    console.log(`  ${file} ... ✗ unrecognized filename format`);
    failed++;
    continue;
  }

  process.stdout.write(`  ${plan.label} ... `);

  if (!fs.existsSync(plan.srcPath)) {
    console.log('✗ source image missing');
    failed++;
    continue;
  }

  const dstMeta = `${plan.dstPath}.meta`;
  let guid;

  if (fs.existsSync(dstMeta)) {
    const match = fs.readFileSync(dstMeta, 'utf8').match(/^guid: ([a-f0-9]{32})/m);
    guid = match ? match[1] : newGuid();
  } else {
    guid = newGuid();
  }

  if (!flagDry) {
    fs.copyFileSync(plan.srcPath, plan.dstPath);
    fs.writeFileSync(dstMeta, makeMeta(guid), 'utf8');
  }

  const patched = plan.patch(plan.assetPath, guid);
  if (patched.ok) {
    console.log(`${flagDry ? '✓ would wire' : '✓ wired'} (guid: ${guid.slice(0, 8)}...)`);
    ok++;
  } else {
    console.log(`✗ ${patched.reason}`);
    failed++;
  }
}

console.log(`\n  Done. ${ok} ${flagDry ? 'would wire' : 'wired'}, ${failed} failed.\n`);

if (!flagDry && ok > 0) {
  console.log('  Next steps:');
  console.log('  1. Open Unity so it imports the updated sprites');
  console.log('  2. Run Casebook -> Build Scene');
  console.log('  3. Re-run status.js to confirm links\n');
}
