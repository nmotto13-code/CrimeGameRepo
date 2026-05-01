# Schema Migration

## Version
- Current schema version: `1`

## What Changed
- `CaseData`
  Added `schemaVersion`
  Added `involvedSuspects`
  Added `interrogationNodes`
- `EvidenceData`
  Added `schemaVersion`
  Added `usesPlaceholderSprite`
- `ClaimData`
  Added `schemaVersion`
- New asset types
  `SuspectData`
  `InterrogationNode`

## Backward Compatibility
- Existing assets without the new fields load with default values.
- Existing 10 cases continue to use the old contradiction-to-results path unless interrogation nodes are explicitly linked.
- Empty `involvedSuspects` lists do not change gameplay.
- Empty `interrogationNodes` lists do not change gameplay.

## Required Upgrade Step
Run `Casebook -> Upgrade Case Schema` once to stamp existing assets to version `1`.

## Validation Expectations
- `Casebook -> Validate All Cases` warns on stale schema versions.
- Validator errors should block merge.
- Validator warnings are expected for intentional placeholders or incomplete future-content scaffolding.
