import fs from 'fs';
import path from 'path';
import crypto from 'crypto';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(__dirname, '../..');
const manifest = JSON.parse(fs.readFileSync(path.join(__dirname, 'presentation-prompts.json'), 'utf8'));
const cityLocationScriptGuid = fs.readFileSync(path.join(repoRoot, 'Assets/Scripts/Data/CityLocationData.cs.meta'), 'utf8')
  .match(/^guid: ([a-f0-9]{32})/m)?.[1] ?? null;

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

function makeNativeAssetMeta(guid) {
  return `fileFormatVersion: 2
guid: ${guid}
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`;
}

function ensureMeta(assetRelativePath) {
  const absPath = path.join(repoRoot, assetRelativePath);
  if (!fs.existsSync(absPath)) {
    return null;
  }

  const metaPath = `${absPath}.meta`;
  let guid = null;
  if (fs.existsSync(metaPath)) {
    guid = fs.readFileSync(metaPath, 'utf8').match(/^guid: ([a-f0-9]{32})/m)?.[1] ?? null;
  }
  if (!guid) {
    guid = newGuid();
    fs.writeFileSync(metaPath, makeMeta(guid), 'utf8');
  }
  return guid;
}

function ensureNativeAssetMeta(assetRelativePath) {
  const absPath = path.join(repoRoot, assetRelativePath);
  if (!fs.existsSync(absPath)) {
    return null;
  }

  const metaPath = `${absPath}.meta`;
  let guid = null;
  if (fs.existsSync(metaPath)) {
    guid = fs.readFileSync(metaPath, 'utf8').match(/^guid: ([a-f0-9]{32})/m)?.[1] ?? null;
  }
  if (!guid) {
    guid = newGuid();
    fs.writeFileSync(metaPath, makeNativeAssetMeta(guid), 'utf8');
  }
  return guid;
}

function patchField(assetRelativePath, fieldName, guid) {
  const absPath = path.join(repoRoot, assetRelativePath);
  if (!fs.existsSync(absPath)) {
    console.log(`  Missing asset: ${assetRelativePath}`);
    return false;
  }

  const content = fs.readFileSync(absPath, 'utf8');
  const newRef = `${fieldName}: {fileID: 21300000, guid: ${guid}, type: 3}`;
  let updated = content;

  if (new RegExp(`${fieldName}: \\{fileID: 0\\}`).test(content)) {
    updated = updated.replace(new RegExp(`${fieldName}: \\{fileID: 0\\}`), newRef);
  } else if (new RegExp(`${fieldName}: \\{fileID: 21300000, guid: [a-f0-9]{32}, type: 3\\}`).test(content)) {
    updated = updated.replace(new RegExp(`${fieldName}: \\{fileID: 21300000, guid: [a-f0-9]{32}, type: 3\\}`), newRef);
  } else {
    const anchor = fieldName === 'mapIcon' ? /arcLabel:.*\n/ : /displayName:.*\n/;
    if (anchor.test(updated)) {
      updated = updated.replace(anchor, match => `${match}  ${newRef}\n`);
    } else {
      return false;
    }
  }

  if (updated !== content) {
    fs.writeFileSync(absPath, updated, 'utf8');
  }
  return true;
}

function yamlString(value) {
  return JSON.stringify(value ?? '');
}

function buildLocationIconLookup() {
  const lookup = new Map();
  for (const entry of manifest.locationNodeIcons ?? []) {
    const iconPath = entry.output;
    for (const locationId of entry.locationIds ?? []) {
      if (locationId) {
        lookup.set(locationId, iconPath);
      }
    }
  }
  return lookup;
}

function upsertCityLocationAsset(entry, nodeIconGuid, backgroundGuid) {
  if (!cityLocationScriptGuid) {
    console.log('  Missing CityLocationData.cs.meta guid; cannot create CityLocation assets.');
    return;
  }

  const assetRelativePath = `Assets/Resources/CityLocations/${entry.locationId}.asset`;
  const absPath = path.join(repoRoot, assetRelativePath);
  fs.mkdirSync(path.dirname(absPath), { recursive: true });

  const content = `%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: ${cityLocationScriptGuid}, type: 3}
  m_Name: ${entry.locationId}
  m_EditorClassIdentifier: Assembly-CSharp::CasebookGame.Data.CityLocationData
  locationId: ${yamlString(entry.locationId)}
  districtId: ${yamlString(entry.districtId)}
  displayName: ${yamlString(entry.displayName)}
  mapPosition: {x: ${Number(entry.mapPosition?.x ?? 0.5).toFixed(3)}, y: ${Number(entry.mapPosition?.y ?? 0.5).toFixed(3)}}
  nodeIcon: ${nodeIconGuid ? `{fileID: 21300000, guid: ${nodeIconGuid}, type: 3}` : '{fileID: 0}'}
  defaultBackground: ${backgroundGuid ? `{fileID: 21300000, guid: ${backgroundGuid}, type: 3}` : '{fileID: 0}'}
`;

  fs.writeFileSync(absPath, content, 'utf8');
  ensureNativeAssetMeta(assetRelativePath);
}

function patchCaseVisitBackground(caseVisitEntry, backgroundGuid) {
  if (!backgroundGuid || !caseVisitEntry?.caseAssetId || !caseVisitEntry.locationId) {
    return false;
  }

  const assetRelativePath = `Assets/Resources/Cases/${caseVisitEntry.caseAssetId}.asset`;
  const absPath = path.join(repoRoot, assetRelativePath);
  if (!fs.existsSync(absPath)) {
    console.log(`  Missing case asset for visit background: ${assetRelativePath}`);
    return false;
  }

  const content = fs.readFileSync(absPath, 'utf8');
  const escapedLocationId = caseVisitEntry.locationId.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const blockPattern = new RegExp(`(- locationId: ${escapedLocationId}\\n\\s+displayName: .*\\n\\s+sceneBackground: )\\{fileID: [^}]+\\}`, 'm');
  const replacement = `$1{fileID: 21300000, guid: ${backgroundGuid}, type: 3}`;
  const updated = content.replace(blockPattern, replacement);

  if (updated !== content) {
    fs.writeFileSync(absPath, updated, 'utf8');
    return true;
  }

  console.log(`  Could not patch visit background for ${caseVisitEntry.locationId}`);
  return false;
}

for (const department of manifest.departmentIcons) {
  const guid = ensureMeta(department.output);
  if (!guid) {
    console.log(`  Missing department icon: ${department.output}`);
    continue;
  }
  patchField(department.assetPath, 'mapIcon', guid);
}

for (const suspect of manifest.suspectPortraits) {
  const guid = ensureMeta(suspect.spritePath);
  if (!guid) {
    console.log(`  Missing suspect portrait: ${suspect.spritePath}`);
    continue;
  }
  patchField(suspect.assetPath, 'portraitSprite', guid);
}

for (const asset of [...manifest.artEntries, ...manifest.districtMarkers, ...manifest.locationNodeIcons]) {
  ensureMeta(asset.output);
}

const locationIconLookup = buildLocationIconLookup();
for (const location of manifest.locations ?? []) {
  const nodeIconPath = locationIconLookup.get(location.locationId) ?? null;
  const nodeIconGuid = nodeIconPath ? ensureMeta(nodeIconPath) : null;
  const backgroundGuid = location.defaultBackgroundPath ? ensureMeta(location.defaultBackgroundPath) : null;
  upsertCityLocationAsset(location, nodeIconGuid, backgroundGuid);
}

for (const visitBackground of manifest.caseVisitBackgrounds ?? []) {
  const backgroundGuid = ensureMeta(visitBackground.output);
  if (!backgroundGuid) {
    console.log(`  Missing visit background: ${visitBackground.output}`);
    continue;
  }
  patchCaseVisitBackground(visitBackground, backgroundGuid);
}

function ensureFolderMetas(dirRelativePath) {
  const absDir = path.join(repoRoot, dirRelativePath);
  if (!fs.existsSync(absDir)) {
    return;
  }
  for (const name of fs.readdirSync(absDir, { withFileTypes: true })) {
    const childRelative = path.posix.join(dirRelativePath.replace(/\\/g, '/'), name.name);
    if (name.isDirectory()) {
      ensureFolderMetas(childRelative);
    } else if (/\.(png|jpg|jpeg)$/i.test(name.name)) {
      ensureMeta(childRelative);
    }
  }
}

ensureFolderMetas('Assets/Resources/PresentationPolish');

console.log('Presentation assets wired for department, suspect, city-location, and pilot visit background fields.');
