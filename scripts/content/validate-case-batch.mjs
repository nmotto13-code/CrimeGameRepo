import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const projectRoot = path.resolve(__dirname, "../..");

const casesPath = path.join(projectRoot, "Docs", "content", "cases_C011_C030.json");
const suspectsPath = path.join(projectRoot, "Docs", "content", "suspects_C011_C030.json");
const promptsOutPath = path.join(projectRoot, "Docs", "content", "prompts_C011_C030.json");

const stylePrefix = "dark moody detective game evidence photograph, photorealistic, slightly desaturated colours, subtle film grain, cinematic lighting, no UI chrome or borders, square crop";
const expectedCaseIds = Array.from({ length: 20 }, (_, i) => `C${String(i + 11).padStart(3, "0")}`);

function fail(message) {
  throw new Error(message);
}

function assert(condition, message) {
  if (!condition) fail(message);
}

function flattenCases(data) {
  return data.departments.flatMap(department =>
    department.cases.map(caseData => ({ department, caseData }))
  );
}

const casesData = JSON.parse(fs.readFileSync(casesPath, "utf8"));
const suspects = JSON.parse(fs.readFileSync(suspectsPath, "utf8"));
const suspectIds = new Set(suspects.map(suspect => suspect.suspectId));

assert(Array.isArray(casesData.departments), "departments array missing");
assert(Array.isArray(suspects), "suspects array missing");

const flat = flattenCases(casesData);
assert(flat.length === 20, `expected 20 cases, found ${flat.length}`);

const foundCaseIds = flat.map(item => item.caseData.caseId).sort();
assert(JSON.stringify(foundCaseIds) === JSON.stringify([...expectedCaseIds].sort()), "case IDs are not exactly C011-C030");

const recurringSuspectId = casesData.recurringSuspectId;
assert(recurringSuspectId === "S011", "recurringSuspectId must be S011");

let recurringInFraud = false;
let recurringInMissing = false;
const evidenceIds = new Set();
const prompts = [];

for (const { department, caseData } of flat) {
  const { caseId } = caseData;
  const isFraud = department.departmentId === "FRAUD";
  const isCapstone = caseId === "C020" || caseId === "C030";

  assert(Array.isArray(caseData.hotspots) && caseData.hotspots.length >= 3 && caseData.hotspots.length <= 5, `${caseId}: hotspots must be 3-5`);
  assert(Array.isArray(caseData.evidence) && caseData.evidence.length >= 4 && caseData.evidence.length <= 7, `${caseId}: evidence must be 4-7`);
  assert(Array.isArray(caseData.claims) && caseData.claims.length >= 3 && caseData.claims.length <= 5, `${caseId}: claims must be 3-5`);
  assert(caseData.basePoints === (isCapstone ? 750 : 500), `${caseId}: invalid basePoints`);
  assert(caseData.toolConfig?.enhanceCooldownSeconds === 12, `${caseId}: enhanceCooldownSeconds must be 12`);
  assert(caseData.toolConfig?.crossCheckCharges === (isFraud ? 3 : 2), `${caseId}: invalid crossCheckCharges`);
  assert(isCapstone ? caseData.timeLimitSeconds >= 300 && caseData.timeLimitSeconds <= 480 : caseData.timeLimitSeconds === 0, `${caseId}: invalid timeLimitSeconds`);
  assert(Array.isArray(caseData.involvedSuspects), `${caseId}: involvedSuspects missing`);
  assert(Array.isArray(caseData.interrogationNodes), `${caseId}: interrogationNodes missing`);

  if (caseData.involvedSuspects.includes(recurringSuspectId)) {
    if (isFraud) recurringInFraud = true;
    if (department.departmentId === "MISSING_PERSONS") recurringInMissing = true;
  }

  const claimIds = new Set(caseData.claims.map(claim => claim.claimId));
  assert(claimIds.has(caseData.contradictionClaimId), `${caseId}: contradictionClaimId not found`);

  const caseEvidenceIds = new Set();
  for (const evidence of caseData.evidence) {
    assert(/^C0\d{2}_E00[1-7]$/.test(evidence.evidenceId), `${caseId}: invalid evidenceId ${evidence.evidenceId}`);
    assert(evidence.evidenceId.startsWith(`${caseId}_`), `${caseId}: evidenceId ${evidence.evidenceId} not in case namespace`);
    assert(!evidenceIds.has(evidence.evidenceId), `duplicate evidenceId ${evidence.evidenceId}`);
    evidenceIds.add(evidence.evidenceId);
    caseEvidenceIds.add(evidence.evidenceId);
    assert(typeof evidence.imagePrompt === "string" && evidence.imagePrompt.trim().length > 0, `${evidence.evidenceId}: imagePrompt missing`);
    prompts.push({
      id: evidence.evidenceId,
      name: evidence.displayName,
      case: caseId,
      prompt: evidence.imagePrompt
    });
  }

  for (const hotspot of caseData.hotspots) {
    assert(caseEvidenceIds.has(hotspot.linkedEvidenceId), `${caseId}: hotspot references unknown evidence ${hotspot.linkedEvidenceId}`);
  }

  assert(caseEvidenceIds.has(caseData.primaryEvidenceIdA), `${caseId}: primaryEvidenceIdA missing`);
  assert(caseEvidenceIds.has(caseData.primaryEvidenceIdB), `${caseId}: primaryEvidenceIdB missing`);
  assert(caseData.primaryEvidenceIdA !== caseData.primaryEvidenceIdB, `${caseId}: primary evidence IDs must differ`);
  assert(typeof caseData.explanation === "string" && caseData.explanation.includes(caseData.primaryEvidenceIdA) && caseData.explanation.includes(caseData.primaryEvidenceIdB), `${caseId}: explanation must reference primary evidence IDs`);

  for (const suspectId of caseData.involvedSuspects) {
    assert(suspectIds.has(suspectId), `${caseId}: unknown suspect ${suspectId}`);
  }

  for (const node of caseData.interrogationNodes) {
    assert(/^C0\d{2}_INT00[1-3]$/.test(node.nodeId), `${caseId}: invalid interrogation node id ${node.nodeId}`);
    assert(Array.isArray(node.responses) && node.responses.length === 3, `${node.nodeId}: must have exactly 3 responses`);
    assert(node.correctResponseIndex >= 0 && node.correctResponseIndex <= 2, `${node.nodeId}: invalid correctResponseIndex`);
    assert(Array.isArray(node.unlockConditionTags), `${node.nodeId}: unlockConditionTags missing`);
    assert(Array.isArray(node.evidenceRequiredIds), `${node.nodeId}: evidenceRequiredIds missing`);
    for (const evidenceId of node.evidenceRequiredIds) {
      assert(caseEvidenceIds.has(evidenceId) || evidenceIds.has(evidenceId), `${node.nodeId}: unknown evidenceRequiredId ${evidenceId}`);
    }
  }
}

assert(recurringInFraud && recurringInMissing, "S011 must appear in both departments");

const fraudLeak = flat.find(item => item.caseData.evidence.some(evidence => evidence.evidenceId === casesData.departments[0].internalLeakEvidenceId));
const missingLeak = flat.find(item => item.caseData.evidence.some(evidence => evidence.evidenceId === casesData.departments[1].internalLeakEvidenceId));
assert(fraudLeak, "fraud internal leak evidence not found");
assert(missingLeak, "missing-persons internal leak evidence not found");

const c030 = flat.find(item => item.caseData.caseId === "C030")?.caseData;
assert(c030?.interrogationNodes.length >= 2 && c030?.interrogationNodes.length <= 3, "C030 must have 2-3 interrogation nodes");

for (const suspect of suspects) {
  assert(/^S0\d{2}$/.test(suspect.suspectId), `invalid suspectId ${suspect.suspectId}`);
  assert(suspect.credibilityScore >= 0 && suspect.credibilityScore <= 1, `${suspect.suspectId}: credibilityScore must be 0.0-1.0`);
  assert(Array.isArray(suspect.knownAssociates), `${suspect.suspectId}: knownAssociates missing`);
  assert(Array.isArray(suspect.linkedCaseIds), `${suspect.suspectId}: linkedCaseIds missing`);
  for (const associateId of suspect.knownAssociates) {
    assert(suspectIds.has(associateId), `${suspect.suspectId}: unknown associate ${associateId}`);
  }
  for (const linkedCaseId of suspect.linkedCaseIds) {
    assert(expectedCaseIds.includes(linkedCaseId), `${suspect.suspectId}: unknown linked case ${linkedCaseId}`);
  }
}

const promptsData = {
  stylePrefix,
  evidence: prompts.sort((a, b) => a.id.localeCompare(b.id))
};

fs.writeFileSync(promptsOutPath, `${JSON.stringify(promptsData, null, 2)}\n`, "utf8");

console.log(`Validated ${flat.length} cases, ${suspects.length} suspects, ${prompts.length} evidence prompts.`);
console.log(`Wrote ${path.relative(projectRoot, promptsOutPath)}`);
