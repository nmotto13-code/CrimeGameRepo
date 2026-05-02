import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(__dirname, '../..');

const matrixPath = path.join(repoRoot, 'Docs', 'content', 'precinct_map_case_matrix_C001_C030.json');
const suspectsPath = path.join(repoRoot, 'Docs', 'content', 'suspects_C011_C030.json');
const outputPath = path.join(__dirname, 'presentation-prompts.json');

const matrix = JSON.parse(fs.readFileSync(matrixPath, 'utf8'));
const suspectData = JSON.parse(fs.readFileSync(suspectsPath, 'utf8'));

const presentationRoot = 'Assets/Sprites/Presentation';

const departmentConfig = {
  Patrol: {
    color: '#D1A24A',
    accent: '#342A1C',
    iconCode: 'PT',
    output: `${presentationRoot}/DepartmentIcons/dept_patrol.png`,
    assetPath: 'Assets/Resources/Departments/Patrol_Training.asset',
  },
  Fraud: {
    color: '#4E8E8A',
    accent: '#1D2A2B',
    iconCode: 'FR',
    output: `${presentationRoot}/DepartmentIcons/dept_fraud.png`,
    assetPath: 'Assets/Resources/Departments/Fraud.asset',
  },
  MissingPersons: {
    color: '#8B5C6A',
    accent: '#241A20',
    iconCode: 'MP',
    output: `${presentationRoot}/DepartmentIcons/dept_missing_persons.png`,
    assetPath: 'Assets/Resources/Departments/Missing_Persons.asset',
  },
};

const districtStyle = {
  DIST_OLD_QUARTER: { color: '#8F5D4A', accent: '#2A1D1A', code: 'OQ', anchor: [0.27, 0.25], glyph: 'OLD' },
  DIST_CIVIC_CORE: { color: '#5F7F92', accent: '#1D2730', code: 'CC', anchor: [0.50, 0.28], glyph: 'CIV' },
  DIST_MARKET_ROW: { color: '#C58C3B', accent: '#3A2A18', code: 'MR', anchor: [0.34, 0.63], glyph: 'MKT' },
  DIST_RIVERSIDE: { color: '#4D7FA5', accent: '#1A2734', code: 'RV', anchor: [0.72, 0.52], glyph: 'RIV' },
  DIST_SKYLINE: { color: '#7073B8', accent: '#202244', code: 'SK', anchor: [0.74, 0.22], glyph: 'SKY' },
  DIST_NORTH_QUAY: { color: '#5B7B6E', accent: '#1A2923', code: 'NQ', anchor: [0.78, 0.76], glyph: 'NQ' },
  DIST_OUTER_REACH: { color: '#7B8E4E', accent: '#243018', code: 'OR', anchor: [0.14, 0.58], glyph: 'OUT' },
};

const nodeArchetypes = {
  estate_private: { label: 'Estate / Private', color: '#8F5D4A', accent: '#2A1D1A', code: 'EST' },
  civic_secure: { label: 'Civic / Secure', color: '#5F7F92', accent: '#1D2730', code: 'CIV' },
  culture_hospitality: { label: 'Culture / Hospitality', color: '#B07A56', accent: '#312118', code: 'CUL' },
  medical_transit: { label: 'Medical / Transit', color: '#4D7FA5', accent: '#1A2734', code: 'TRN' },
  residential_indoor: { label: 'Residential / Indoor', color: '#867267', accent: '#241D19', code: 'RES' },
  market_public: { label: 'Market / Public', color: '#C58C3B', accent: '#3A2A18', code: 'PUB' },
  corporate_highrise: { label: 'Corporate / Highrise', color: '#7073B8', accent: '#202244', code: 'COR' },
  industrial_logistics: { label: 'Industrial / Logistics', color: '#5B7B6E', accent: '#1A2923', code: 'IND' },
  media_press: { label: 'Media / Press', color: '#8A6FA8', accent: '#241C30', code: 'PRS' },
  nightlife_stage: { label: 'Nightlife / Stage', color: '#9C556D', accent: '#2E1821', code: 'STG' },
  outdoor_frontier: { label: 'Outdoor / Frontier', color: '#7B8E4E', accent: '#243018', code: 'OUT' },
};

const locationArchetypeByCase = {
  C001: 'estate_private',
  C002: 'civic_secure',
  C003: 'culture_hospitality',
  C004: 'medical_transit',
  C005: 'residential_indoor',
  C006: 'estate_private',
  C007: 'residential_indoor',
  C008: 'medical_transit',
  C009: 'market_public',
  C010: 'culture_hospitality',
  C011: 'culture_hospitality',
  C012: 'market_public',
  C013: 'culture_hospitality',
  C014: 'corporate_highrise',
  C015: 'civic_secure',
  C016: 'industrial_logistics',
  C017: 'civic_secure',
  C018: 'corporate_highrise',
  C019: 'industrial_logistics',
  C020: 'civic_secure',
  C021: 'medical_transit',
  C022: 'residential_indoor',
  C023: 'nightlife_stage',
  C024: 'outdoor_frontier',
  C025: 'corporate_highrise',
  C026: 'industrial_logistics',
  C027: 'market_public',
  C028: 'industrial_logistics',
  C029: 'media_press',
  C030: 'civic_secure',
};

const offsetPattern = [
  [0.00, 0.00],
  [-0.07, -0.03],
  [0.08, -0.02],
  [-0.05, 0.08],
  [0.07, 0.08],
  [0.00, -0.11],
  [-0.10, 0.12],
  [0.11, 0.14],
];

function clamp01(value) {
  return Math.max(0.04, Math.min(0.96, value));
}

function normalizeSlug(value) {
  return value.toLowerCase().replace(/[^a-z0-9]+/g, '_').replace(/^_|_$/g, '');
}

function findBackgroundPath(caseId) {
  const caseNumber = caseId.slice(1);
  const backgroundsDir = path.join(repoRoot, 'Assets', 'Sprites', 'Backgrounds');
  const entries = fs.existsSync(backgroundsDir) ? fs.readdirSync(backgroundsDir) : [];
  const match = entries.find(name => name.toLowerCase().startsWith(`case${caseNumber.toLowerCase()}_`) && !name.endsWith('.meta'));
  return match ? `Assets/Sprites/Backgrounds/${match}` : null;
}

const launchLocationByCase = new Map(matrix.cityLocations.map(location => [location.launchCaseId, location]));
const suspectsById = new Map(suspectData.map(item => [item.suspectId, item]));
const districtLocations = new Map(matrix.districts.map(district => [district.districtId, []]));

for (const location of matrix.cityLocations) {
  districtLocations.get(location.districtId)?.push(location);
}

const locationEntries = [];
for (const district of matrix.districts) {
  const locations = (districtLocations.get(district.districtId) ?? []).sort((a, b) => a.launchCaseId.localeCompare(b.launchCaseId));
  const districtVisual = districtStyle[district.districtId];

  locations.forEach((location, index) => {
    const [dx, dy] = offsetPattern[index % offsetPattern.length] ?? [0, 0];
    const x = clamp01(districtVisual.anchor[0] + dx);
    const y = clamp01(districtVisual.anchor[1] + dy);
    const archetypeId = locationArchetypeByCase[location.launchCaseId] ?? 'civic_secure';

    locationEntries.push({
      locationId: location.locationId,
      districtId: location.districtId,
      displayName: location.displayName,
      launchCaseId: location.launchCaseId,
      nodeArchetypeId: archetypeId,
      mapPosition: { x: Number(x.toFixed(3)), y: Number(y.toFixed(3)) },
      defaultBackgroundPath: findBackgroundPath(location.launchCaseId),
    });
  });
}

const suspectPortraits = suspectData.map(suspect => {
  const slug = normalizeSlug(suspect.displayName);
  const shortBio = suspect.bio.split('. ')[0].replace(/\.$/, '');
  return {
    suspectId: suspect.suspectId,
    displayName: suspect.displayName,
    assetPath: `Assets/ScriptableObjects/Cases/Suspects/${suspect.suspectId}.asset`,
    spritePath: `${presentationRoot}/Suspects/${suspect.suspectId}_${slug}.png`,
    prompt: `dark moody detective dossier portrait, photorealistic, slightly desaturated colours, subtle film grain, cinematic lighting, bust portrait, neutral backdrop, no text, no UI, ${shortBio.toLowerCase()}, traits suggested through expression and wardrobe: ${suspect.traits.join(', ')}`,
  };
});

const departmentIcons = matrix.departments.map(department => ({
  departmentId: department.departmentId,
  displayName: department.displayName,
  output: departmentConfig[department.departmentId].output,
  iconCode: departmentConfig[department.departmentId].iconCode,
  color: departmentConfig[department.departmentId].color,
  accent: departmentConfig[department.departmentId].accent,
  assetPath: departmentConfig[department.departmentId].assetPath,
}));

const districtMarkers = matrix.districts.map(district => ({
  districtId: district.districtId,
  displayName: district.displayName,
  sortOrder: district.sortOrder,
  themeSummary: district.themeSummary,
  output: `${presentationRoot}/DistrictMarkers/${normalizeSlug(district.displayName)}.png`,
  color: districtStyle[district.districtId].color,
  accent: districtStyle[district.districtId].accent,
  iconCode: districtStyle[district.districtId].code,
  glyph: districtStyle[district.districtId].glyph,
}));

const locationNodeIcons = Object.entries(nodeArchetypes).map(([id, config]) => ({
  archetypeId: id,
  label: config.label,
  output: `${presentationRoot}/LocationNodes/${id}.png`,
  color: config.color,
  accent: config.accent,
  iconCode: config.code,
  locationIds: locationEntries.filter(location => location.nodeArchetypeId === id).map(location => location.locationId),
}));

const artEntries = [
  {
    id: 'precinct_home_hub',
    category: 'precinct',
    output: `${presentationRoot}/Precinct/precinct_home_hub.png`,
    size: '1024x1792',
    prompt: 'mobile detective game precinct hub background, atmospheric precinct briefing room at dusk, polished but worn wood desks, glowing evidence boards with abstract photos and blank documents, muted city lights through tall windows, no people, no text, no signage, no letterforms, no numbers, no readable papers, cinematic composition with negative space for UI, slightly desaturated palette',
  },
  {
    id: 'precinct_department_board',
    category: 'precinct',
    output: `${presentationRoot}/Precinct/precinct_department_board.png`,
    size: '1024x1792',
    prompt: 'mobile detective game department board background, close interior view of a precinct caseboard wall with pinned photos, blank folders, abstract map shapes, filing trays, and desk lamp pools, no people, no text, no signage, no alphabetic characters, no numbers, no readable papers, moody cinematic lighting, clear open space for overlay UI',
  },
  {
    id: 'city_map_base',
    category: 'city_map',
    output: `${presentationRoot}/CityMap/city_map_base.png`,
    size: '1024x1792',
    prompt: 'stylized top-down city map for a noir detective mobile game, seven visually distinct districts connected by roads and bridges, river, civic core, market blocks, skyline towers, docks, parks, and outskirts, unlabeled map art only, no labels, no legend, no callout boxes, no text, no letterforms, no numbers, moody night lighting, readable negative space for markers, portrait composition',
  },
  ...suspectPortraits.map(entry => ({
    id: entry.suspectId,
    category: 'suspect_portrait',
    output: entry.spritePath,
    size: '1024x1024',
    prompt: entry.prompt,
  })),
];

const manifest = {
  version: 1,
  sourceFiles: [
    'Docs/content/precinct_map_case_matrix_C001_C030.json',
    'Docs/content/suspects_C011_C030.json',
  ],
  notes: [
    'DistrictData and CityLocationData runtime asset creation is intentionally excluded here because those script files are currently untracked and have no stable Unity .meta GUIDs in this thread.',
    'Integration can consume this manifest to wire district markers, node icons, map positions, and default backgrounds once the schema thread lands tracked district/location scripts.',
  ],
  artEntries,
  departmentIcons,
  districtMarkers,
  locationNodeIcons,
  locations: locationEntries,
  suspectPortraits,
};

fs.writeFileSync(outputPath, `${JSON.stringify(manifest, null, 2)}\n`, 'utf8');
console.log(`Wrote ${outputPath}`);
