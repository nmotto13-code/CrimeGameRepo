import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const PROMPTS_FILE = path.resolve(__dirname, 'prompts.json');
const BACKGROUND_FILE = path.resolve(__dirname, 'background-prompts.json');
const OUTPUT_EVIDENCE_DIR = path.resolve(__dirname, 'output');
const OUTPUT_BACKGROUND_DIR = path.resolve(__dirname, 'output-backgrounds');
const UNITY_EVIDENCE_DIR = path.resolve(__dirname, '../../Assets/Sprites/Evidence');
const UNITY_BACKGROUND_DIR = path.resolve(__dirname, '../../Assets/Sprites/Backgrounds');
const EVIDENCE_ASSET_DIR = path.resolve(__dirname, '../../Assets/ScriptableObjects/Cases/Evidence');
const CASE_ASSET_DIR = path.resolve(__dirname, '../../Assets/Resources/Cases');
const REPORT_DIR = path.resolve(__dirname, 'reports');
const REPORT_FILE = path.resolve(REPORT_DIR, 'asset_status.md');

function normalizeCaseCode(value) {
  if (!value) return undefined;
  const match = value.toUpperCase().match(/C?(\d{3})/);
  return match ? `C${match[1]}` : value.toUpperCase();
}

function readJson(filePath) {
  return JSON.parse(fs.readFileSync(filePath, 'utf8'));
}

function readTextIfExists(filePath) {
  return fs.existsSync(filePath) ? fs.readFileSync(filePath, 'utf8') : null;
}

function listFilesIfExists(dirPath) {
  return fs.existsSync(dirPath) ? fs.readdirSync(dirPath) : [];
}

function parseSpriteReference(content, fieldName) {
  if (!content) {
    return { kind: 'asset-missing' };
  }

  const match = content.match(new RegExp(`${fieldName}: \\{fileID: (\\d+)(?:, guid: ([a-f0-9]{32}), type: 3)?\\}`));
  if (!match) {
    return { kind: 'unknown' };
  }

  if (match[1] === '0') {
    return { kind: 'null' };
  }

  return { kind: 'linked', guid: match[2] };
}

function parseMetaGuid(metaPath) {
  const content = readTextIfExists(metaPath);
  const match = content?.match(/^guid: ([a-f0-9]{32})/m);
  return match ? match[1] : null;
}

function hasPlaceholderFlag(content) {
  if (!content) {
    return false;
  }

  return /usesPlaceholderSprite:\s*(1|true)\b/i.test(content);
}

function caseNumber(caseCode) {
  return caseCode.slice(1);
}

function compareCaseCodes(a, b) {
  return Number(caseNumber(a)) - Number(caseNumber(b));
}

function evidenceItemsByCase(prompts) {
  const map = new Map();
  for (const item of prompts.evidence) {
    const caseCode = normalizeCaseCode(item.case);
    if (!map.has(caseCode)) {
      map.set(caseCode, []);
    }
    map.get(caseCode).push(item);
  }
  return map;
}

function backgroundsByCase(backgrounds) {
  const map = new Map();
  for (const item of backgrounds.backgrounds) {
    map.set(`C${item.id.slice(4)}`, item);
  }
  return map;
}

function findCaseCodes() {
  const prompts = readJson(PROMPTS_FILE);
  const backgrounds = readJson(BACKGROUND_FILE);
  const caseCodes = new Set();

  prompts.evidence.forEach(item => caseCodes.add(normalizeCaseCode(item.case)));
  backgrounds.backgrounds.forEach(item => caseCodes.add(`C${item.id.slice(4)}`));

  listFilesIfExists(EVIDENCE_ASSET_DIR)
    .filter(file => /^C\d{3}_E\d{3}\.asset$/i.test(file))
    .forEach(file => caseCodes.add(file.slice(0, 4).toUpperCase()));

  listFilesIfExists(CASE_ASSET_DIR)
    .filter(file => /^Case_\d{3}\.asset$/i.test(file))
    .forEach(file => caseCodes.add(`C${file.match(/\d{3}/)[0]}`));

  listFilesIfExists(OUTPUT_EVIDENCE_DIR)
    .filter(file => /^C\d{3}_E\d{3}\.png$/i.test(file))
    .forEach(file => caseCodes.add(file.slice(0, 4).toUpperCase()));

  listFilesIfExists(OUTPUT_BACKGROUND_DIR)
    .filter(file => /^case\d{3}_/i.test(file))
    .forEach(file => caseCodes.add(`C${file.match(/^case(\d{3})_/i)[1]}`));

  listFilesIfExists(UNITY_BACKGROUND_DIR)
    .filter(file => /^case\d{3}_/i.test(file) && !file.endsWith('.meta'))
    .forEach(file => caseCodes.add(`C${file.match(/^case(\d{3})_/i)[1]}`));

  return Array.from(caseCodes).sort(compareCaseCodes);
}

function classifyEvidenceItem(item) {
  const fileName = `${item.id}.png`;
  const generatedPath = path.join(OUTPUT_EVIDENCE_DIR, fileName);
  const unityPath = path.join(UNITY_EVIDENCE_DIR, fileName);
  const metaPath = `${unityPath}.meta`;
  const assetPath = path.join(EVIDENCE_ASSET_DIR, `${item.id}.asset`);
  const assetContent = readTextIfExists(assetPath);
  const reference = parseSpriteReference(assetContent, 'imageSprite');
  const spriteGuid = parseMetaGuid(metaPath);
  const generated = fs.existsSync(generatedPath);
  const wired = fs.existsSync(unityPath) && !!spriteGuid;
  const placeholder = hasPlaceholderFlag(assetContent);

  let state = 'prompt-only';
  let note = 'prompt present, no generated output';

  if (placeholder) {
    state = 'placeholder';
    note = 'asset flagged with usesPlaceholderSprite';
  } else if (generated && !wired) {
    state = 'generated-only';
    note = 'generated in output/, not copied into Unity';
  } else if (wired && reference.kind === 'null') {
    state = 'wired-unlinked';
    note = 'sprite exists in Unity, asset reference is null';
  } else if (wired && reference.kind === 'linked' && spriteGuid === reference.guid) {
    state = 'linked';
    note = 'sprite copied, meta present, and asset GUID matches';
  } else if (reference.kind === 'linked' && !spriteGuid) {
    state = 'linking-unknown';
    note = 'asset has GUID but target sprite meta is missing';
  } else if (wired && reference.kind === 'linked' && spriteGuid !== reference.guid) {
    state = 'linking-unknown';
    note = 'asset links to a different GUID than the local sprite meta';
  } else if (reference.kind === 'asset-missing') {
    state = generated || wired ? 'linking-unknown' : 'prompt-only';
    note = generated || wired ? 'sprite exists but EvidenceData asset is missing' : note;
  } else if (reference.kind === 'unknown') {
    state = 'linking-unknown';
    note = 'could not parse imageSprite reference';
  }

  return {
    id: item.id,
    name: item.name,
    state,
    note,
    generated,
    wired,
    linked: state === 'linked',
    placeholder,
  };
}

function classifyBackground(caseCode, item) {
  if (!item) {
    const generated = listFilesIfExists(OUTPUT_BACKGROUND_DIR).some(file => file.toLowerCase().startsWith(`case${caseNumber(caseCode).toLowerCase()}_`));
    const wired = listFilesIfExists(UNITY_BACKGROUND_DIR).some(file => file.toLowerCase().startsWith(`case${caseNumber(caseCode).toLowerCase()}_`) && !file.endsWith('.meta'));
    return {
      id: `case${caseNumber(caseCode)}`,
      name: 'background',
      state: 'missing-prompt',
      note: generated || wired ? 'background exists on disk but no background prompt entry' : 'no background prompt entry',
      generated,
      wired,
      linked: false,
      placeholder: false,
    };
  }

  const fileBase = `${item.id}_${item.location}`;
  const outputFile = findExistingFile(OUTPUT_BACKGROUND_DIR, fileBase);
  const unityFile = findExistingFile(UNITY_BACKGROUND_DIR, fileBase);
  const generated = !!outputFile;
  const wired = !!unityFile;
  const spriteGuid = unityFile ? parseMetaGuid(path.join(UNITY_BACKGROUND_DIR, `${unityFile}.meta`)) : null;
  const caseAssetPath = path.join(CASE_ASSET_DIR, `Case_${caseNumber(caseCode)}.asset`);
  const caseAssetContent = readTextIfExists(caseAssetPath);
  const reference = parseSpriteReference(caseAssetContent, 'sceneBackground');

  let state = 'prompt-only';
  let note = 'prompt present, no generated output';

  if (generated && !wired) {
    state = 'generated-only';
    note = 'generated in output-backgrounds/, not copied into Unity';
  } else if (wired && reference.kind === 'null') {
    state = 'wired-unlinked';
    note = 'background image exists in Unity, sceneBackground is null';
  } else if (wired && reference.kind === 'linked' && spriteGuid === reference.guid) {
    state = 'linked';
    note = 'background copied, meta present, and CaseData GUID matches';
  } else if (reference.kind === 'linked' && !spriteGuid) {
    state = 'linking-unknown';
    note = 'CaseData has GUID but target background meta is missing';
  } else if (wired && reference.kind === 'linked' && spriteGuid !== reference.guid) {
    state = 'linking-unknown';
    note = 'CaseData links to a different GUID than the local background meta';
  } else if (reference.kind === 'asset-missing') {
    state = generated || wired ? 'linking-unknown' : 'prompt-only';
    note = generated || wired ? 'background image exists but CaseData asset is missing' : note;
  } else if (reference.kind === 'unknown') {
    state = 'linking-unknown';
    note = 'could not parse sceneBackground reference';
  }

  return {
    id: item.id,
    name: item.location,
    state,
    note,
    generated,
    wired,
    linked: state === 'linked',
    placeholder: false,
  };
}

function findExistingFile(dirPath, baseName) {
  const candidates = ['.jpg', '.jpeg', '.png'];
  for (const extension of candidates) {
    const fileName = `${baseName}${extension}`;
    if (fs.existsSync(path.join(dirPath, fileName))) {
      return fileName;
    }
  }
  return null;
}

function summarizeStates(items) {
  const summary = {
    missingPrompts: items.filter(item => item.state === 'missing-prompt').length,
    promptOnly: items.filter(item => item.state === 'prompt-only').length,
    generatedOnly: items.filter(item => item.state === 'generated-only').length,
    wiredUnlinked: items.filter(item => item.state === 'wired-unlinked').length,
    linked: items.filter(item => item.state === 'linked').length,
    placeholder: items.filter(item => item.state === 'placeholder').length,
    linkingUnknown: items.filter(item => item.state === 'linking-unknown').length,
  };

  summary.pending = summary.missingPrompts
    + summary.promptOnly
    + summary.generatedOnly
    + summary.wiredUnlinked
    + summary.placeholder
    + summary.linkingUnknown;

  return summary;
}

function collectMissingPromptEvidence(caseCode, promptIds) {
  const ids = new Map();
  const prefix = `${caseCode}_`;

  const add = (id, source) => {
    if (promptIds.has(id)) {
      return;
    }

    const existing = ids.get(id);
    if (existing) {
      existing.sources.add(source);
    } else {
      ids.set(id, { id, sources: new Set([source]) });
    }
  };

  listFilesIfExists(EVIDENCE_ASSET_DIR)
    .filter(file => file.toUpperCase().startsWith(prefix) && file.toLowerCase().endsWith('.asset'))
    .forEach(file => add(path.basename(file, '.asset').toUpperCase(), 'EvidenceData asset'));

  listFilesIfExists(OUTPUT_EVIDENCE_DIR)
    .filter(file => file.toUpperCase().startsWith(prefix) && file.toLowerCase().endsWith('.png'))
    .forEach(file => add(path.basename(file, '.png').toUpperCase(), 'generated output'));

  listFilesIfExists(UNITY_EVIDENCE_DIR)
    .filter(file => file.toUpperCase().startsWith(prefix) && file.toLowerCase().endsWith('.png'))
    .forEach(file => add(path.basename(file, '.png').toUpperCase(), 'Unity sprite'));

  return Array.from(ids.values())
    .sort((a, b) => a.id.localeCompare(b.id))
    .map(entry => ({
      id: entry.id,
      name: 'missing prompt entry',
      state: 'missing-prompt',
      note: `${Array.from(entry.sources).join(', ')} exists but prompts.json has no matching entry`,
      generated: entry.sources.has('generated output'),
      wired: entry.sources.has('Unity sprite'),
      linked: false,
      placeholder: false,
    }));
}

export function collectStatus(caseFilter) {
  const prompts = readJson(PROMPTS_FILE);
  const backgrounds = readJson(BACKGROUND_FILE);
  const evidenceMap = evidenceItemsByCase(prompts);
  const backgroundMap = backgroundsByCase(backgrounds);
  const allCases = findCaseCodes();
  const selectedCases = caseFilter
    ? allCases.filter(code => code === normalizeCaseCode(caseFilter))
    : allCases;

  return selectedCases.map(caseCode => {
    const manifestItems = evidenceMap.get(caseCode) ?? [];
    const promptIds = new Set(manifestItems.map(item => item.id.toUpperCase()));
    const evidenceItems = [
      ...manifestItems.map(classifyEvidenceItem),
      ...collectMissingPromptEvidence(caseCode, promptIds),
    ].sort((a, b) => a.id.localeCompare(b.id));
    const backgroundItem = classifyBackground(caseCode, backgroundMap.get(caseCode));

    return {
      caseCode,
      evidence: {
        items: evidenceItems,
        summary: summarizeStates(evidenceItems),
      },
      background: {
        item: backgroundItem,
        summary: summarizeStates([backgroundItem]),
      },
    };
  });
}

function consoleSummaryLine(caseStatus) {
  const evidence = caseStatus.evidence.summary;
  const background = caseStatus.background.summary;
  return [
    `${caseStatus.caseCode}:`,
    `evidence linked ${evidence.linked}/${caseStatus.evidence.items.length || 0}`,
    `pending ${evidence.pending}`,
    `background ${caseStatus.background.item.state}`,
    background.pending ? '(attention)' : '(ok)',
  ].join(' ');
}

function markdownForCase(caseStatus) {
  const evidence = caseStatus.evidence.summary;
  const background = caseStatus.background.summary;

  const lines = [
    `## ${caseStatus.caseCode}`,
    '',
    `Evidence: linked ${evidence.linked}/${caseStatus.evidence.items.length}, pending ${evidence.pending}, placeholder ${evidence.placeholder}, unknown ${evidence.linkingUnknown}`,
    '',
  ];

  if (caseStatus.evidence.items.length) {
    lines.push('| Evidence ID | Status | Note |');
    lines.push('| --- | --- | --- |');
    for (const item of caseStatus.evidence.items) {
      lines.push(`| ${item.id} | ${item.state} | ${item.note} |`);
    }
    lines.push('');
  } else {
    lines.push('No evidence prompt entries for this case.', '');
  }

  lines.push(`Background: ${caseStatus.background.item.state} (${caseStatus.background.item.note})`);
  lines.push('');
  lines.push('| Background ID | Status | Note |');
  lines.push('| --- | --- | --- |');
  lines.push(`| ${caseStatus.background.item.id} | ${caseStatus.background.item.state} | ${caseStatus.background.item.note} |`);
  lines.push('');

  return lines.join('\n');
}

function buildMarkdownReport(results) {
  const lines = [
    '# Asset Status Report',
    '',
    `Generated: ${new Date().toISOString()}`,
    '',
  ];

  for (const result of results) {
    lines.push(markdownForCase(result));
  }

  return lines.join('\n');
}

export function printStatus(results) {
  console.log('');
  results.forEach(result => console.log(`  ${consoleSummaryLine(result)}`));
  console.log('');
}

export function writeStatusReport(results) {
  fs.mkdirSync(REPORT_DIR, { recursive: true });
  fs.writeFileSync(REPORT_FILE, `${buildMarkdownReport(results)}\n`, 'utf8');
}

function runCli() {
  const args = process.argv.slice(2);
  const caseArg = args.includes('--case') ? args[args.indexOf('--case') + 1] : undefined;
  const dryRun = args.includes('--dry-run');
  const results = collectStatus(caseArg);

  printStatus(results);

  if (!dryRun) {
    writeStatusReport(results);
    console.log(`  Wrote ${REPORT_FILE}\n`);
  } else {
    console.log('  Dry run: report file not written.\n');
  }
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  runCli();
}
