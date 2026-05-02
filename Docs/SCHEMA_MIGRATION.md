# Schema Migration

## Version
- Current schema version: `3`
- Previous schema version: `2`

## What Changed
- `CaseData`
  Added `schemaVersion`
  Added `involvedSuspects`
  Added `interrogationNodes`
  Added `departmentId`
  Added `districtId`
  Added `cityLocationId`
  Added `caseArcId`
  Added `arcBeatSummary`
  Added `caseLocations`
- `EvidenceData`
  Added `schemaVersion`
  Added `usesPlaceholderSprite`
- `ClaimData`
  Added `schemaVersion`
- `DepartmentData`
  Added summary/presentation fields for precinct/map flow
- New asset types
  `SuspectData`
  `InterrogationNode`
  `DistrictData`
  `CityLocationData`
  `CaseLocationData`
- `SuspectData`
  Added `roleLabel`, `statusSummary`, `currentLocationId`, `interrogationEntryNodeId`
- `InterrogationNode`
  Added `suspectId`, branching fields, and reward fields
  Added `outcomeIdOnCorrect`
  Added `outcomeIdOnWrong`
  Added `locationContextId`
- `CaseData` version `3`
  Added `visitFlowMode`
  Added `startingLocationId`
  Added `allowMapRevisit`
  Added `locationReadyForSolveMode`
  Added `interrogationOutcomes`
- `CaseLocationData` version `3`
  Added `unlockCondition`
  Added `nextLocationIds`
  Added `revisitRule`
  Added `presentSuspects`
  Added `autoCompleteOnEnter`
  Added `completionOutcomeId`
  Added `autoUnlocksSolve`
- New helper types for version `3`
  `CaseProgressConditionData`
  `LocationSuspectPresenceData`
  `InterrogationOutcomeData`
  `CaseVisitFlowMode`
  `CaseSolveGateMode`
  `LocationRevisitRule`
  `ConditionMatchMode`

## Backward Compatibility
- Existing assets without the new fields load with default values.
- Existing 10 cases continue to use the old contradiction-to-results path unless interrogation nodes are explicitly linked.
- Existing 30 shipped cases remain valid under `LegacyFallback` visit behavior.
- Empty `involvedSuspects` lists do not change gameplay.
- Empty `interrogationNodes` lists do not change gameplay.
- Empty `caseLocations` lists synthesize one visit from the legacy root background and hotspots.
- Blank `districtId` and `cityLocationId` values can be backfilled by the progression bootstrapper until content is resaved.
- Blank `startingLocationId` resolves to the first authored visit or legacy fallback location.
- Empty `unlockCondition` continues to use legacy `unlockEvidenceIds` and `unlockTags`.
- Empty `interrogationOutcomes` and blank node outcome IDs preserve existing interrogation behavior.
- `allowMapRevisit` defaults to `true`, preserving shipped revisit behavior.

## Required Upgrade Step
Run `Casebook -> Upgrade Case Schema` once to stamp existing assets to version `3`.

## Validation Expectations
- `Casebook -> Validate All Cases` warns on stale schema versions.
- Validator errors should block merge.
- Validator warnings are expected for intentional placeholders or incomplete future-content scaffolding.
- Run `Casebook -> Build Scene` after schema upgrade so the world-map bootstrapper can populate default district/location data where content has not been explicitly authored yet.
## Version 3 Defaults
- `visitFlowMode = LegacyFallback`
- `startingLocationId = ""`
- `allowMapRevisit = true`
- `locationReadyForSolveMode = LegacyContradictionOnly`
- `interrogationOutcomes = []`
- `unlockCondition = empty`
- `nextLocationIds = []`
- `revisitRule = Always`
- `presentSuspects = []`
- `autoCompleteOnEnter = false`
- `completionOutcomeId = ""`
- `autoUnlocksSolve = false`
- `outcomeIdOnCorrect = ""`
- `outcomeIdOnWrong = ""`
- `locationContextId = ""`

## Dependency Notes
- `CONTENT GENERATION` will need the final field names and enum names before authoring visit graph JSON.
- `PROGRESSION LAYER` will need the runtime-ready contract before implementing revisit UX, solve gating, and location progression UI.
- `BUILD` should validate the shipped 30-case roster under schema version `3` default fallback behavior before promoting pilot visit-graph content.
