# Content Guide

## Purpose
Use this workflow to add or extend cases without breaking the existing shell, scene builder, or the first 10 playable cases.

## Naming Rules
- Case assets: `Case_0XX`
- Evidence assets: `C0XX_E00X`
- Claim assets: `C0XX_CL00X`
- Suspect assets: `S0XX_Name` or another stable asset filename, with a unique `suspectId`
- Interrogation assets: `C0XX_INT00X`
- Evidence prompt IDs in `scripts/image-gen/prompts.json` must match the evidence asset filename stem exactly

## New Case Workflow
1. Create a new `CaseData` asset from `Casebook/Case Data`.
2. Set `caseId`, title, brief, background, hotspot list, evidence list, claims list, contradiction ID, explanation, and tool overrides.
3. Keep hotspot count in the default validator range: 3 to 5.
4. Set `primaryEvidenceIdA` and `primaryEvidenceIdB` to the two evidence IDs that anchor the contradiction explanation.

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
2. Set `suspectId`, display name, portrait, bio, traits, associates, linked case IDs, credibility score, and notes.
3. Link involved suspects on the case through `CaseData.involvedSuspects`.
4. The in-game dossier screen reads only the suspects linked on the current case.

## Interrogation Workflow
1. Create `InterrogationNode` assets from `Casebook/Interrogation Node`.
2. Fill `nodeId`, `promptText`, exactly 3 responses, and `correctResponseIndex`.
3. Use `evidenceRequiredIds` to require specific found evidence before a node appears.
4. Use `unlockConditionTags` when a node should depend on discovered evidence tags instead of a specific clue.
5. Link nodes through `CaseData.interrogationNodes`.
6. If a case has no interrogation nodes, it still goes straight to results with no regression path.

## Validation Workflow
1. Run `Casebook -> Validate All Cases`.
2. The validator always writes `Docs/validation-report.json`.
3. Treat errors as block-merge issues.
4. Treat warnings as author review items. Placeholder sprites and old schema versions should be resolved intentionally, not ignored.

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
- Version `1` is additive only:
  `CaseData` gained `schemaVersion`, `involvedSuspects`, and `interrogationNodes`
  `EvidenceData` gained `schemaVersion` and `usesPlaceholderSprite`
  `ClaimData` gained `schemaVersion`
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
