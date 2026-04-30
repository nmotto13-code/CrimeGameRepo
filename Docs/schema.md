# Casebook Schema

## Current Runtime Shape
The live content model is asset-driven and centered on `CaseData`.

Runtime asset locations:

- Cases: `Assets/Resources/Cases/Case_XXX.asset`
- Evidence assets: `Assets/ScriptableObjects/Cases/Evidence/`
- Claim assets: `Assets/ScriptableObjects/Cases/Claims/`

The scene builder loads all cases from `Resources/Cases` and wires them into `GameManager.availableCases`.

## CaseData
Source: `Assets/Scripts/Data/CaseData.cs`

Current fields:

- `string caseId`: stable case identifier such as `Case_001`.
- `string title`: display title used in UI.
- `string briefText`: mission briefing text shown on the brief tab and case select summary.
- `Sprite sceneBackground`: crime-scene background image.
- `List<HotspotData> hotspots`: discovery points placed in normalized scene space.
- `List<EvidenceData> evidence`: evidence assets linked into the case.
- `List<ClaimData> claims`: suspect/witness claims presented on the solve tab.
- `string contradictoryClaimId`: the claim ID that should be selected as the contradiction.
- `string explanationText`: solve explanation shown in results.
- `string primaryEvidenceIdA`: first key evidence referenced by the explanation.
- `string primaryEvidenceIdB`: second key evidence referenced by the explanation.
- `int basePoints`: base score awarded on a correct solve.
- `float timeLimitSeconds`: optional timer. `0` means untimed.
- `ToolConfig toolConfig`: per-case tuning for tool availability and cooldowns.

## HotspotData
Source: `Assets/Scripts/Data/HotspotData.cs`

Current fields:

- `string hotspotId`: stable hotspot identifier inside the case.
- `Vector2 normalizedPosition`: 0-1 anchored position on the background image.
- `float radius`: normalized interaction radius.
- `string evidenceId`: ID of the evidence granted when found.
- `string hotspotLabel`: author-facing and player-facing hotspot label.

## EvidenceData
Source: `Assets/Scripts/Data/EvidenceData.cs`

Current fields:

- `string evidenceId`: stable identifier such as `E001`.
- `string displayName`: evidence card title.
- `string descriptionText`: body text shown in evidence detail.
- `Sprite imageSprite`: primary visual.
- `EvidenceDisplayMode displayMode`: default image mode, document mode, terminal mode, or keycard mode.
- `List<EvidenceTag> tags`: baseline tags available on case load.
- `Sprite enhanceOverlayMaskSprite`: optional overlay shown after enhance.
- `List<EvidenceTag> tagsUnlockedOnEnhance`: additional tags unlocked by enhance.

Runtime-only state:

- `bool isEnhanced`
- `List<EvidenceTag> runtimeTags`

Runtime behavior:

- `ResetRuntimeState()` rebuilds `runtimeTags` from `tags`.
- `ApplyEnhance()` flips `isEnhanced` and merges `tagsUnlockedOnEnhance` into `runtimeTags`.

## ClaimData
Source: `Assets/Scripts/Data/ClaimData.cs`

Current fields:

- `string claimId`: stable identifier such as `CL001`.
- `string speakerName`: displayed speaker label.
- `string claimText`: core statement being evaluated.
- `List<EvidenceTag> referencedTags`: tags used by cross-check logic.
- `bool isRedHerring`: authoring hint for misleading or non-solution claims.

## ToolConfig
Source: `Assets/Scripts/Data/ToolConfig.cs`

Current fields:

- `int crossCheckCharges`: allowed cross-check uses for the case.
- `float enhanceCooldownSeconds`: enhance cooldown duration.
- `int timelineSnapCharges`: allowed timeline snap uses for the case.

Current tool integration notes:

- `ToolConfig` is embedded directly inside `CaseData`, so tool tuning is case-local today.
- This is sufficient for the current 10-case set.
- If departments later override default tool budgets, add department-level defaults above `CaseData` and let case assets override selectively.

## Current Validation Expectations
Each case should satisfy these invariants:

- `sceneBackground` is assigned.
- `hotspots.Count > 0`
- `evidence.Count > 0`
- `claims.Count > 0`
- `contradictoryClaimId` matches one of the linked claim IDs.
- `primaryEvidenceIdA/B` reference evidence inside the same case.
- Every `HotspotData.evidenceId` maps to one of the linked evidence assets.

The smoke test added in this thread validates these expectations for the current roster.

## Versioning Strategy
The current live schema is effectively pre-versioned. There is no serialized `schemaVersion` field yet, so all existing assets should be treated as legacy version `0`.

Recommended migration plan:

1. Add `int schemaVersion = 1` to `CaseData` as the root contract field.
2. Interpret missing values on old assets as `0`.
3. Run editor-only migration code that upgrades legacy assets to the latest schema and writes them back to disk.
4. Keep runtime readers backward-compatible for at least one release so unsaved legacy assets still load in editor and CI.

Why root versioning on `CaseData` first:

- `CaseData` is the asset that binds evidence, claims, scoring, and tool tuning into a playable unit.
- Most future migrations will be case-centric even when nested assets change.
- It avoids forcing every nested asset type to version independently before there is a real need.

Recommended migration rules:

- Additive fields should always have safe defaults.
- Renames should be handled by editor migration, not by fragile runtime reflection.
- Structural changes should preserve old IDs so cross-asset references survive.
- Validation should run after migration and fail the editor build if required links are still missing.

## Back-Compat Plan
When `schemaVersion` is introduced:

- Existing 10 cases remain valid as legacy `0` assets.
- A migration utility should populate new fields without changing case logic.
- The contradiction loop should continue reading old cases until every asset has been resaved and validated.
- Future department, arc, or dossier metadata should prefer additive fields or wrapper assets over breaking changes to the current contradiction schema.
