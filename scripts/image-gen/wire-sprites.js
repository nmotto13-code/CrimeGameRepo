/**
 * wire-sprites.js
 *
 * For each generated evidence PNG:
 *   1. Copies it to Assets/Sprites/Evidence/
 *   2. Creates a Unity .meta file (Sprite import settings) with a unique GUID
 *   3. Patches the matching EvidenceData .asset file to reference the new sprite
 *
 * Usage:
 *   node wire-sprites.js             -- wire all images in output/
 *   node wire-sprites.js C003_E001   -- wire a single item
 */

import fs   from 'fs';
import path from 'path';
import crypto from 'crypto';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const OUTPUT_DIR   = path.resolve(__dirname, 'output');
const SPRITES_DIR  = path.resolve(__dirname, '../../Assets/Sprites/Evidence');
const EVIDENCE_DIR = path.resolve(__dirname, '../../Assets/ScriptableObjects/Cases/Evidence');

// ── Helpers ──────────────────────────────────────────────────────────────────

function newGuid() {
  return crypto.randomBytes(16).toString('hex');
}

function makeMeta(guid) {
  // Minimal Unity TextureImporter meta that imports as Sprite (textureType: 8)
  // Matches the format used by existing project sprites
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

function patchAsset(assetPath, guid) {
  if (!fs.existsSync(assetPath)) {
    console.log(`    ⚠  asset not found: ${path.basename(assetPath)}`);
    return false;
  }

  let content = fs.readFileSync(assetPath, 'utf8');
  const newRef = `imageSprite: {fileID: 21300000, guid: ${guid}, type: 3}`;

  // Replace either null ref or any existing sprite ref
  const updated = content
    .replace(/imageSprite: \{fileID: 0\}/, newRef)
    .replace(/imageSprite: \{fileID: 21300000, guid: \w+, type: 3\}/, newRef);

  if (updated === content) {
    console.log(`    ⚠  imageSprite line not found in ${path.basename(assetPath)}`);
    return false;
  }

  fs.writeFileSync(assetPath, updated, 'utf8');
  return true;
}

// ── Main ──────────────────────────────────────────────────────────────────────

fs.mkdirSync(SPRITES_DIR, { recursive: true });

const singleArg = process.argv[2];
const pngFiles  = singleArg
  ? [`${singleArg.toUpperCase()}.png`]
  : fs.readdirSync(OUTPUT_DIR).filter(f => f.endsWith('.png'));

if (!pngFiles.length) {
  console.log('\n  No PNG files found in output/. Run generate.js first.\n');
  process.exit(0);
}

console.log(`\n  Wiring ${pngFiles.length} evidence sprite(s)...\n`);

let ok = 0, failed = 0;

for (const file of pngFiles) {
  const id        = path.basename(file, '.png');           // e.g. C003_E001
  const srcPng    = path.join(OUTPUT_DIR, file);
  const dstPng    = path.join(SPRITES_DIR, file);
  const dstMeta   = dstPng + '.meta';
  const assetFile = path.join(EVIDENCE_DIR, `${id}.asset`);

  process.stdout.write(`  ${id} … `);

  if (!fs.existsSync(srcPng)) {
    console.log('✗ source PNG missing in output/');
    failed++; continue;
  }

  // Reuse existing GUID if .meta already exists (idempotent re-runs)
  let guid;
  if (fs.existsSync(dstMeta)) {
    const match = fs.readFileSync(dstMeta, 'utf8').match(/^guid: ([a-f0-9]{32})/m);
    guid = match ? match[1] : newGuid();
  } else {
    guid = newGuid();
  }

  fs.copyFileSync(srcPng, dstPng);
  fs.writeFileSync(dstMeta, makeMeta(guid), 'utf8');

  const patched = patchAsset(assetFile, guid);
  if (patched) {
    console.log(`✓  (guid: ${guid.slice(0, 8)}…)`);
    ok++;
  } else {
    failed++;
  }
}

console.log(`\n  Done. ${ok} wired, ${failed} failed.\n`);
if (ok > 0) {
  console.log('  Next steps:');
  console.log('  1. Open Unity — it will auto-import the new sprites');
  console.log('  2. Run Casebook → Build Scene');
  console.log('  3. Build and push to device\n');
}
