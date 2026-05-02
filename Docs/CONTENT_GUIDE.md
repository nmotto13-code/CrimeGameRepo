# Content Guide

## Purpose
Use this workflow to add or extend cases without breaking the existing shell, scene builder, or the first 10 playable cases.

## Shared Progress
- Every thread updates `Docs/WORKSTREAM_STATUS.md`.
- Each thread edits only its own section plus the shared milestone summary when the milestone materially changes.
- Integration decisions should read the status doc before assigning more work.

## Naming Rules
- Case assets: `Case_0XX`
- Evidence assets: `C0XX_E00X`
- Claim assets: `C0XX_CL00X`
- Suspect assets: `S0XX_Name` or another stable asset filename, with a unique `suspectId`
- Interrogation assets: `C0XX_INT00X`
- Evidence prompt IDs in `scripts/image-gen/prompts.json` must match the evidence asset filename stem exactly

## New Case Workflow
1. Create a new `CaseData` asset from `Casebook/Case Data`.
2. Set `caseId`, title, brief, department, district, city location, contradiction ID, explanation, and tool overrides.
3. Author at least one `caseLocations` entry with background, hotspot list, and entry text.
4. If `caseLocations` is left empty, runtime falls back to the legacy root `sceneBackground` and `hotspots`, but authored content should prefer explicit case visits.
5. Set `primaryEvidenceIdA` and `primaryEvidenceIdB` to the two evidence IDs that anchor the contradiction explanation.
6. Optional: set `caseArcId` and `arcBeatSummary` when the case participates in a longer narrative thread.
7. Keep hotspot count in the default validator range: 3 to 5 across the authored case visits.

## Department and City Workflow
1. Cases should belong to a department desk and a city location, even if they still play as a single-location case.
2. Prefer authored `districtId` and `cityLocationId` values over bootstrapper fallbacks.
3. `CityLocationData` assets should carry the final display name and map position when content is ready.
4. Use one map node per case for now unless a later milestone explicitly changes that rule.

## Evidence Workflow
1. Create `EvidenceData` assets from `Casebook/Evidence Data`.
2. Name each asset with the `C0XX_E00X` stem and give it a short in-case `evidenceId` such as `E001`.
3. Add the evidence prompt entry to `scripts/image-gen/prompts.json` using the same `id` as the asset stem.
4. Generate review images:
   ```bash
   node scripts/image-gen/generate.js --case C0XX --provider openai
   ```
5. Review outputs in `scripts/image-gen/output/`.
6. Wire accepted sprites into Unity assets:
   ```bash
   node scripts/image-gen/wire-sprites.js C0XX_E00X
   ```
7. If an evidence item must ship with a temporary image, assign the sprite and set `usesPlaceholderSprite = true` so the validator reports a warning instead of an error.
8. If sprite references drift, run `Casebook -> Assets -> Repair Evidence Sprites`.

## Suspect Workflow
1. Create `SuspectData` assets from `Casebook/Suspect Data`.
2. Set `suspectId`, display name, portrait, bio, traits, associates, linked case IDs, credibility score, notes, role label, and status summary.
3. Set `currentLocationId` when the suspect should read as anchored to a case visit or city location.
4. Set `interrogationEntryNodeId` when the suspect is the owner of an interrogation sequence.
5. Link involved suspects on the case through `CaseData.involvedSuspects`.
6. The in-game dossier screen reads only the suspects linked on the current case.

## Interrogation Workflow
1. Create `InterrogationNode` assets from `Casebook/Interrogation Node`.
2. Fill `nodeId`, `suspectId`, `promptText`, exactly 3 responses, and `correctResponseIndex`.
3. Use `evidenceRequiredIds` to require specific found evidence before a node appears.
4. Use `unlockConditionTags` when a node should depend on discovered evidence tags instead of a specific clue.
5. Use `nextNodeIdOnCorrect` and `nextNodeIdOnWrong` for authored branching; leaving them blank keeps the legacy linear order.
6. Use `grantedEvidenceIds` or `grantedTags` when interrogation should feed the solve path before final contradiction submission.
7. Link nodes through `CaseData.interrogationNodes`.
8. If a case has no interrogation nodes, it still goes straight to results with no regression path.

## Validation Workflow
1. Run `Casebook -> Validate All Cases`.
2. The validator always writes `Docs/validation-report.json`.
3. Treat errors as block-merge issues.
4. Treat warnings as author review items. Placeholder sprites and old schema versions should be resolved intentionally, not ignored.
5. For new world-map content, fix blank `districtId`, `cityLocationId`, or case-visit issues before merge rather than relying permanently on fallback bootstrap data.

## Scene Workflow
1. Run `Casebook -> Build Scene`.
2. For hierarchy stability checks, run `Casebook -> Verify Scene Builder Determinism`.
3. The builder should produce the same scene hierarchy on consecutive runs and should not accumulate duplicate hidden objects.

## Playtest Workflow
1. Open `Assets/Scenes/CaseScene.unity`.
2. Press Play.
3. Verify the normal flow:
   hotspot discovery -> evidence review -> pin board -> contradiction submission -> optional interrogation -> results
4. Open the in-game hamburger menu and verify `Suspect Dossier` returns cleanly to the menu stack.

## Schema Migration
- Existing cases/assets without `schemaVersion` deserialize as `0`.
- Run `Casebook -> Upgrade Case Schema` once after pulling this change set.
- Version `2` is additive only:
  `CaseData` gained `schemaVersion`, `involvedSuspects`, and `interrogationNodes`
  `EvidenceData` gained `schemaVersion` and `usesPlaceholderSprite`
  `ClaimData` gained `schemaVersion`
- `CaseData` also gained world-placement and case-visit fields
- `SuspectData` and `InterrogationNode` gained v2 presentation/branching fields
- The first 10 cases remain valid without populating suspects or interrogation nodes.

## Deployment Handoff
1. Resolve validator errors.
2. Build the scene.
3. Playtest in Editor.
4. Bump `ProjectSettings/ProjectSettings.asset` build number for iPhone.
5. Run:
   ```bash
   node scripts/deploy/deploy.js
   ```
