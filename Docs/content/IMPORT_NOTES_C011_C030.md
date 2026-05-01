# C011-C030 Content Import Notes

## What This Batch Contains
- `Docs/content/cases_C011_C030.json`
  Department-grouped structured case payload for `C011` through `C030`
- `Docs/content/suspects_C011_C030.json`
  New dossier profiles `S011` through `S030`
- `Docs/content/prompts_C011_C030.json`
  Generated prompt block for evidence image generation

## Important Runtime Mapping
- External staged case IDs use `C011` style.
  Current Unity runtime case assets still use `Case_011.asset` and typically store `caseId = Case_011`.
- External staged evidence IDs use full IDs like `C011_E001`.
  Current Unity runtime evidence assets are usually named `C011_E001.asset` but store short internal IDs like `E001`.
- External staged claim IDs use full IDs like `C011_CL001`.
  Current runtime claim assets usually store short IDs like `CL001`.
- External staged hotspot entries do not include coordinates by design.
  Author hotspot positions in Unity after selecting the final background art.
- External staged `credibilityScore` values are normalized `0.0-1.0`.
  Current `SuspectData` runtime field is `0-100`, so multiply by `100` during import.

## Preflight
1. From repo root, run:
   ```bash
   node scripts/content/validate-case-batch.mjs
   ```
2. Expected clean result:
   - `Validated 20 cases, 20 suspects, 104 evidence prompts.`
   - `Docs/content/prompts_C011_C030.json` is regenerated with no thrown errors.

## Evidence Image Pipeline
1. Open `Docs/content/prompts_C011_C030.json`.
2. Copy its `evidence` entries into `scripts/image-gen/prompts.json` under the existing shared `stylePrefix`.
3. Generate one case at a time for review:
   ```bash
   node scripts/image-gen/generate.js --case C011 --provider openai
   ```
4. Review outputs in `scripts/image-gen/output/`.
5. When approved, wire each evidence sprite:
   ```bash
   node scripts/image-gen/wire-sprites.js C011_E001
   ```
6. Repeat for the rest of the case evidence.

## Unity Asset Authoring Sequence
1. Create new `SuspectData` assets for `S011` through `S030`.
2. Populate dossier fields from `Docs/content/suspects_C011_C030.json`.
3. For each case `C011-C030`, create:
   - `CaseData`
   - `EvidenceData` assets for each evidence row
   - `ClaimData` assets for each claim row
   - `InterrogationNode` assets only where present in the JSON
4. Runtime field mapping:
   - `title` -> `CaseData.title`
   - `brief` -> `CaseData.briefText`
   - `contradictionClaimId` -> `CaseData.contradictoryClaimId`
   - `explanation` -> `CaseData.explanationText`
   - `hotspots[].label` -> `HotspotData.hotspotLabel`
   - `hotspots[].linkedEvidenceId` -> `HotspotData.evidenceId`
   - `claims[].suspectName` -> `ClaimData.speakerName`
   - `evidence[].description` -> `EvidenceData.descriptionText`
5. Keep asset filenames aligned with the existing convention:
   - Cases: `Case_011.asset` ... `Case_030.asset`
   - Evidence: `C011_E001.asset` ... `C030_E00X.asset`
   - Claims: `C011_CL001.asset` ... `C030_CL00X.asset`
   - Interrogation: `C020_INT001.asset`, `C030_INT001.asset`, etc.

## Department Wiring
1. Add `C011-C020` to the Fraud department content list in your progression assets or next-stage progression factory.
2. Add `C021-C030` to the Missing Persons department content list when that department asset exists.
3. The staged JSON already preserves the capstone split:
   - Fraud capstone: `C020`
   - Missing Persons capstone: `C030`

## Scene and Validation Pass
1. After the assets and sprites are wired, open Unity.
2. Run `Casebook -> Validate All Cases`.
3. A clean pass means:
   - `Docs/validation-report.json` is regenerated
   - `0 error(s)` in the Console summary
   - placeholder sprite warnings are acceptable only if intentionally shipping temporary art
4. Run `Casebook -> Build Scene`.
5. Open [CaseScene.unity](/C:/Users/blued/OneDrive/Desktop/CrimeGame/Assets/Scenes/CaseScene.unity) and playtest:
   - hotspot discovery
   - evidence review
   - pin board contradiction
   - optional interrogation where authored
   - results
   - dossier screen round-trip from the hamburger menu

## Notes For Engineering
- No schema migration is required for runtime code because this batch is staged as external JSON only.
- If an importer is built later, keep it additive and map normalized suspect credibility into the existing `0-100` runtime field.
- The strongest hidden dependency is hotspot placement, because the staged schema intentionally omits coordinates until final backgrounds are selected.
