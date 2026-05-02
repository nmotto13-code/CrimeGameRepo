import fs from "node:fs";
import path from "node:path";

const repoRoot = process.cwd();
const matrixPath = path.join(repoRoot, "Docs", "content", "precinct_map_case_matrix_C001_C030.json");
const pilotPath = path.join(repoRoot, "Docs", "content", "showcase_pilot_case_matrix_C001_C030.json");

const roster = JSON.parse(fs.readFileSync(matrixPath, "utf8"));
const pilot = JSON.parse(fs.readFileSync(pilotPath, "utf8"));

function fail(message) {
  throw new Error(message);
}

if (!Array.isArray(pilot.pilotCases) || pilot.pilotCases.length < 3 || pilot.pilotCases.length > 5) {
  fail("Pilot matrix must author between 3 and 5 pilot cases.");
}

const rosterCaseIds = new Set(roster.cases.map((entry) => entry.caseId));
const categorySets = [
  pilot.pilotReadyNowCaseIds ?? [],
  pilot.needsExtraAssetSupportCaseIds ?? [],
  pilot.deferredMultiLocationNotInPilotCaseIds ?? [],
  pilot.legacySingleLocationForNowCaseIds ?? []
];

const categorizedCaseIds = new Set();
for (const group of categorySets) {
  for (const caseId of group) {
    if (!rosterCaseIds.has(caseId)) {
      fail(`Category references unknown caseId ${caseId}.`);
    }
    if (categorizedCaseIds.has(caseId)) {
      fail(`CaseId ${caseId} appears in more than one status category.`);
    }
    categorizedCaseIds.add(caseId);
  }
}

if (categorizedCaseIds.size !== rosterCaseIds.size) {
  fail(`Status categories cover ${categorizedCaseIds.size} cases, expected ${rosterCaseIds.size}.`);
}

const pilotCaseIds = new Set(pilot.pilotCaseIds ?? []);
if (pilotCaseIds.size !== pilot.pilotCases.length) {
  fail("pilotCaseIds must match the authored pilotCases length with unique IDs.");
}

for (const entry of pilot.pilotCases) {
  if (!pilotCaseIds.has(entry.caseId)) {
    fail(`Pilot case ${entry.caseId} is missing from pilotCaseIds.`);
  }
  if (!rosterCaseIds.has(entry.caseId)) {
    fail(`Pilot case ${entry.caseId} does not exist in the 30-case roster.`);
  }
  const requiredStrings = [
    "caseId",
    "assetCaseId",
    "title",
    "departmentId",
    "districtId",
    "cityLocationId",
    "caseArcId",
    "startingLocationId",
    "visitFlowMode",
    "locationReadyForSolveMode",
    "pilotStatus"
  ];
  for (const key of requiredStrings) {
    if (typeof entry[key] !== "string" || entry[key].trim() === "") {
      fail(`Pilot case ${entry.caseId} is missing ${key}.`);
    }
  }
  if (!Array.isArray(entry.visits) || entry.visits.length < 2) {
    fail(`Pilot case ${entry.caseId} must define at least 2 visits.`);
  }
  const visitIds = new Set(entry.visits.map((visit) => visit.locationId));
  if (!visitIds.has(entry.startingLocationId)) {
    fail(`Pilot case ${entry.caseId} startingLocationId must resolve to one of its visits.`);
  }
  if (!Array.isArray(entry.interrogationOutcomes) || entry.interrogationOutcomes.length === 0) {
    fail(`Pilot case ${entry.caseId} must define at least one interrogation outcome.`);
  }
  if (!entry.dependencies || !Array.isArray(entry.dependencies.assetGen) || !Array.isArray(entry.dependencies.progressionLayer)) {
    fail(`Pilot case ${entry.caseId} must include dependency notes for assetGen and progressionLayer.`);
  }
  for (const visit of entry.visits) {
    if (typeof visit.locationId !== "string" || visit.locationId.trim() === "") {
      fail(`Pilot case ${entry.caseId} has a visit with no locationId.`);
    }
    if (!Array.isArray(visit.presentSuspects) || visit.presentSuspects.length === 0) {
      fail(`Pilot case ${entry.caseId} visit ${visit.locationId} must author suspect presence.`);
    }
    if (!Array.isArray(visit.progressionBeats) || visit.progressionBeats.length === 0) {
      fail(`Pilot case ${entry.caseId} visit ${visit.locationId} must author progression beats.`);
    }
  }
}

console.log(`Validated showcase pilot matrix for ${pilot.pilotCases.length} cases.`);
console.log(`Pilot-ready now: ${pilot.pilotReadyNowCaseIds.length}`);
console.log(`Needs extra asset support: ${pilot.needsExtraAssetSupportCaseIds.length}`);
console.log(`Deferred multi-location not in pilot: ${pilot.deferredMultiLocationNotInPilotCaseIds.length}`);
console.log(`Legacy single-location for now: ${pilot.legacySingleLocationForNowCaseIds.length}`);
