import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const projectRoot = path.resolve(__dirname, "../..");

const matrixPath = path.join(projectRoot, "Docs", "content", "precinct_map_case_matrix_C001_C030.json");
const caseDir = path.join(projectRoot, "Assets", "Resources", "Cases");

const matrix = JSON.parse(fs.readFileSync(matrixPath, "utf8"));
const expectedCaseIds = Array.from({ length: 30 }, (_, i) => `C${String(i + 1).padStart(3, "0")}`);
const expectedAssetCaseIds = new Set(expectedCaseIds.map(caseId => `Case_${caseId.slice(1)}`));

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

assert(Array.isArray(matrix.departments), "departments missing");
assert(Array.isArray(matrix.districts), "districts missing");
assert(Array.isArray(matrix.cityLocations), "cityLocations missing");
assert(Array.isArray(matrix.cases), "cases missing");

const districtIds = new Set(matrix.districts.map(district => district.districtId));
const cityLocationIds = new Set(matrix.cityLocations.map(location => location.locationId));
const caseIds = new Set();
const caseLocationIds = new Set();
const multiLocationCaseIds = new Set(matrix.multiLocationCaseIds);
const singleLocationCaseIds = new Set(matrix.singleLocationCaseIds);
const interrogationForwardCaseIds = new Set(matrix.interrogationForwardCaseIds);

assert(matrix.cases.length === 30, `expected 30 case mappings, found ${matrix.cases.length}`);
assert(matrix.cityLocations.length === 30, `expected 30 city locations, found ${matrix.cityLocations.length}`);
assert(matrix.districts.length >= 1, "no districts authored");

for (const cityLocation of matrix.cityLocations) {
  assert(districtIds.has(cityLocation.districtId), `city location ${cityLocation.locationId} points to unknown district ${cityLocation.districtId}`);
  assert(expectedCaseIds.includes(cityLocation.launchCaseId), `city location ${cityLocation.locationId} points to unknown launch case ${cityLocation.launchCaseId}`);
}

for (const caseEntry of matrix.cases) {
  assert(expectedCaseIds.includes(caseEntry.caseId), `unexpected caseId ${caseEntry.caseId}`);
  assert(!caseIds.has(caseEntry.caseId), `duplicate caseId ${caseEntry.caseId}`);
  caseIds.add(caseEntry.caseId);

  assert(expectedAssetCaseIds.has(caseEntry.assetCaseId), `${caseEntry.caseId}: invalid assetCaseId ${caseEntry.assetCaseId}`);
  assert(["Patrol", "Fraud", "MissingPersons"].includes(caseEntry.departmentId), `${caseEntry.caseId}: invalid departmentId ${caseEntry.departmentId}`);
  assert(districtIds.has(caseEntry.districtId), `${caseEntry.caseId}: unknown districtId ${caseEntry.districtId}`);
  assert(cityLocationIds.has(caseEntry.cityLocationId), `${caseEntry.caseId}: unknown cityLocationId ${caseEntry.cityLocationId}`);
  assert(typeof caseEntry.caseArcId === "string" && caseEntry.caseArcId.length > 0, `${caseEntry.caseId}: missing caseArcId`);
  assert(typeof caseEntry.arcBeatSummary === "string" && caseEntry.arcBeatSummary.length > 0, `${caseEntry.caseId}: missing arcBeatSummary`);
  assert(["single_location", "multi_location"].includes(caseEntry.locationMode), `${caseEntry.caseId}: invalid locationMode`);
  assert(caseEntry.interrogationUsage && typeof caseEntry.interrogationUsage.mode === "string", `${caseEntry.caseId}: interrogationUsage missing`);
  assert(Array.isArray(caseEntry.caseLocations) && caseEntry.caseLocations.length >= 1, `${caseEntry.caseId}: caseLocations missing`);
  assert(Array.isArray(caseEntry.suspectRelevance) && caseEntry.suspectRelevance.length >= 1, `${caseEntry.caseId}: suspectRelevance missing`);

  if (caseEntry.locationMode === "single_location") {
    assert(singleLocationCaseIds.has(caseEntry.caseId), `${caseEntry.caseId}: single_location case missing from singleLocationCaseIds`);
    assert(caseEntry.caseLocations.length === 1, `${caseEntry.caseId}: single_location case should have exactly 1 caseLocation`);
  } else {
    assert(multiLocationCaseIds.has(caseEntry.caseId), `${caseEntry.caseId}: multi_location case missing from multiLocationCaseIds`);
    assert(caseEntry.caseLocations.length >= 2, `${caseEntry.caseId}: multi_location case should have at least 2 caseLocations`);
  }

  if (caseEntry.interrogationUsage.isMilestoneForwardCase) {
    assert(interrogationForwardCaseIds.has(caseEntry.caseId), `${caseEntry.caseId}: flagged interrogation-forward but missing from interrogationForwardCaseIds`);
  }

  for (const location of caseEntry.caseLocations) {
    assert(typeof location.locationId === "string" && location.locationId.length > 0, `${caseEntry.caseId}: caseLocation missing locationId`);
    assert(typeof location.displayName === "string" && location.displayName.length > 0, `${caseEntry.caseId}: caseLocation missing displayName`);
    assert(typeof location.entryText === "string" && location.entryText.length > 0, `${caseEntry.caseId}: caseLocation missing entryText`);
    assert(Array.isArray(location.unlockEvidenceIds), `${caseEntry.caseId}: caseLocation unlockEvidenceIds missing`);
    assert(Array.isArray(location.unlockTags), `${caseEntry.caseId}: caseLocation unlockTags missing`);
    assert(typeof location.isRequiredForSolve === "boolean", `${caseEntry.caseId}: caseLocation isRequiredForSolve missing`);
    assert(!caseLocationIds.has(location.locationId), `duplicate caseLocation locationId ${location.locationId}`);
    caseLocationIds.add(location.locationId);
  }
}

for (const expectedCaseId of expectedCaseIds) {
  assert(caseIds.has(expectedCaseId), `missing case mapping for ${expectedCaseId}`);
}

for (const fileName of fs.readdirSync(caseDir)) {
  if (!fileName.endsWith(".asset")) continue;
  const assetCaseId = fileName.replace(".asset", "");
  assert(expectedAssetCaseIds.has(assetCaseId), `unexpected runtime case asset ${assetCaseId}`);
}

console.log(`Validated precinct/map matrix for ${matrix.cases.length} cases.`);
console.log(`Interrogation-forward cases: ${matrix.interrogationForwardCaseIds.length}`);
console.log(`Single-location cases: ${matrix.singleLocationCaseIds.length}`);
console.log(`Multi-location cases: ${matrix.multiLocationCaseIds.length}`);
