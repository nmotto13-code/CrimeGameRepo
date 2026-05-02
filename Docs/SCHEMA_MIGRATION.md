# Schema Migration

## Version
- Current schema version: `2`

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

## Backward Compatibility
- Existing assets without the new fields load with default values.
- Existing 10 cases continue to use the old contradiction-to-results path unless interrogation nodes are explicitly linked.
- Empty `involvedSuspects` lists do not change gameplay.
- Empty `interrogationNodes` lists do not change gameplay.
- Empty `caseLocations` lists synthesize one visit from the legacy root background and hotspots.
- Blank `districtId` and `cityLocationId` values can be backfilled by the progression bootstrapper until content is resaved.

## Required Upgrade Step
Run `Casebook -> Upgrade Case Schema` once to stamp existing assets to version `2`.

## Validation Expectations
- `Casebook -> Validate All Cases` warns on stale schema versions.
- Validator errors should block merge.
- Validator warnings are expected for intentional placeholders or incomplete future-content scaffolding.
- Run `Casebook -> Build Scene` after schema upgrade so the world-map bootstrapper can populate default district/location data where content has not been explicitly authored yet.
