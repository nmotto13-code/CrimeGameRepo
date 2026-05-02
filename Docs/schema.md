# Casebook Schema Contract

## Status
- Runtime now implements schema version `3`.
- Version `3` is additive and preserves the shipped roster through default fallback behavior.
- This document remains the source contract for authoring and integration in the `Casebook V2 follow-up - Multi-Location Cases + Suspect-Driven Progression` milestone.

## Goals For Version 3
- Turn `caseLocations` from a light ordered list into an authored investigation route.
- Keep current 30 cases valid without resave or content rewrite.
- Support optional player choice between visits, not only strict linear order.
- Let suspect presence be authored per location instead of only at case scope.
- Let interrogation outcomes unlock locations, reveal suspects, or mark a case ready for solve.
- Keep one-location fallback behavior intact when authored visit flow is absent.

## Current Version 2 Baseline
- `CaseData.caseLocations` provides a location list with:
  - `locationId`
  - `displayName`
  - `sceneBackground`
  - `hotspots`
  - `entryText`
  - `visitOrder`
  - `unlockEvidenceIds`
  - `unlockTags`
  - `isRequiredForSolve`
- `SuspectData.currentLocationId` is currently a macro-level hint used by dossier presentation.
- `InterrogationNode` already supports:
  - per-node unlock by evidence IDs and tags
  - branching by next node on correct or wrong
  - local rewards via `grantedEvidenceIds` and `grantedTags`
- This is enough for fallback map flow, but not enough for authored visit routing or suspect-driven progression.

## Version 3 Design Rule
- `SuspectData` remains global dossier data.
- Case-specific suspect placement belongs on case/location data, not on `SuspectData`, so one suspect can appear in different places across different cases without mutating the global asset.
- Existing v2 fields remain valid and continue to load.
- New v3 fields must default to the exact current behavior.

## Version 3 Additions

### CaseData
- Add `visitFlowMode: CaseVisitFlowMode`
  - Default: `LegacyFallback`
  - Purpose: declares whether the case uses current fallback behavior, authored unlocked-choice behavior, or authored sequence graph behavior.
- Add `startingLocationId: string`
  - Default: blank
  - Resolution: blank means `GetResolvedLocation(0)`.
- Add `allowMapRevisit: bool`
  - Default: `true`
  - Purpose: global fallback for whether players may reopen previously visited locations from the map.
- Add `locationReadyForSolveMode: CaseSolveGateMode`
  - Default: `LegacyContradictionOnly`
  - Purpose: allows future cases to require visit/interrogation outcomes before contradiction submission becomes final.
- Add `interrogationOutcomes: List<InterrogationOutcomeData>`
  - Default: empty
  - Purpose: central authored table for progression-changing interrogation results.

### CaseLocationData
- Keep existing fields unchanged.
- Add `unlockCondition: CaseProgressConditionData`
  - Default: empty condition
  - Purpose: authored replacement for simple unlock lists.
- Keep `unlockEvidenceIds` and `unlockTags`
  - Purpose: backward-compatible legacy fields that hydrate the empty `unlockCondition` when no explicit v3 condition is authored.
- Add `nextLocationIds: List<string>`
  - Default: empty
  - Purpose: optional graph edges for authored location sequencing.
- Add `revisitRule: LocationRevisitRule`
  - Default: `Always`
  - Purpose: per-location override for revisit behavior.
- Add `presentSuspects: List<LocationSuspectPresenceData>`
  - Default: empty
  - Purpose: case-specific suspect presence, availability, and interrogation entry at this location.
- Add `autoCompleteOnEnter: bool`
  - Default: `false`
  - Purpose: explicit route-only visit completion marker for no-hotspot locations.
- Add `completionOutcomeId: string`
  - Default: blank
  - Purpose: optional authored outcome fired when this visit is completed.
- Add `autoUnlocksSolve: bool`
  - Default: `false`
  - Purpose: allows a required location to mark the case solve-ready without forcing interrogation to do it.

### InterrogationNode
- Keep existing fields unchanged.
- Add `outcomeIdOnCorrect: string`
  - Default: blank
  - Purpose: apply a named case-level interrogation outcome after a correct answer.
- Add `outcomeIdOnWrong: string`
  - Default: blank
  - Purpose: apply a named case-level interrogation outcome after a wrong answer.
- Add `locationContextId: string`
  - Default: blank
  - Purpose: optional authored link to the visit where the interrogation is surfaced.

### SuspectData
- No required new runtime field is needed for version `3`.
- `currentLocationId` remains a global dossier/map hint.
- Case-specific placement, availability, and interrogation entry move to `CaseLocationData.presentSuspects`.
- Optional future addition, only if UI needs it later:
  - `defaultPresenceLabel: string`
  - This is not required for the v3 contract.

## New Helper Types For Version 3

### CaseVisitFlowMode
- `LegacyFallback`
  - Current behavior. First resolved location acts as the playable location. Existing 30 cases remain here by default.
- `UnlockedChoice`
  - Any unlocked location can be launched from the map or visit picker.
- `SequenceGraph`
  - Location access follows authored `nextLocationIds` plus unlock conditions.

### CaseSolveGateMode
- `LegacyContradictionOnly`
  - Current behavior. Correct contradiction can end the case immediately.
- `RequireRequiredVisits`
  - All required visits must be completed before final solve.
- `RequireInterrogationOutcome`
  - One or more authored interrogation outcomes must be earned before final solve.

### LocationRevisitRule
- `Always`
- `AfterNewProgress`
- `Never`

### CaseProgressConditionData
- Serializable helper used by visit unlocks and suspect availability.
- Fields:
  - `requiredEvidenceIds: List<string>`
  - `requiredTags: List<EvidenceTag>`
  - `requiredVisitedLocationIds: List<string>`
  - `requiredCompletedLocationIds: List<string>`
  - `requiredCompletedInterrogationNodeIds: List<string>`
  - `requiredInterrogationOutcomeIds: List<string>`
  - `requiredSuspectIds: List<string>`
  - `matchMode: ConditionMatchMode`
- Default behavior:
  - empty condition means unlocked/available immediately
  - `matchMode` default is `All`

### ConditionMatchMode
- `All`
- `Any`

### LocationSuspectPresenceData
- Serializable helper for case-specific suspect placement.
- Proposed fields:
  - `suspectId: string`
  - `presenceLabel: string`
  - `isVisibleOnEntry: bool`
  - `availabilityCondition: CaseProgressConditionData`
  - `interrogationEntryNodeId: string`
  - `departureOutcomeId: string`
  - `notes: string`
- Authoring intent:
  - supports suspects who are present immediately
  - supports suspects who appear only after evidence or interrogation progress
  - supports suspects who can be questioned at one location but only referenced at another

### InterrogationOutcomeData
- Serializable helper stored on `CaseData.interrogationOutcomes`.
- Proposed fields:
  - `outcomeId: string`
  - `displayLabel: string`
  - `summaryText: string`
  - `unlockLocationIds: List<string>`
  - `lockLocationIds: List<string>`
  - `revealSuspectIds: List<string>`
  - `hideSuspectIds: List<string>`
  - `grantEvidenceIds: List<string>`
  - `grantTags: List<EvidenceTag>`
  - `markCaseReadyForSolve: bool`
  - `redirectToLocationId: string`
- Design intent:
  - node-level rewards stay local and immediate
  - outcome-level effects change case progression state

## Authoring Semantics

### Minimal Multi-Location Authored Case
- Set `visitFlowMode = UnlockedChoice`
- Set `startingLocationId`
- Author `caseLocations`
- Leave `nextLocationIds` empty if the player may choose any unlocked location
- Use `unlockCondition` only where visits should appear later

### Ordered Route Case
- Set `visitFlowMode = SequenceGraph`
- Author `startingLocationId`
- For each visit, author `nextLocationIds`
- Use `revisitRule = Never` on one-shot scenes if needed

### Suspect-Driven Case
- Add suspect assets to `involvedSuspects`
- Author `presentSuspects` on each relevant location
- Use `availabilityCondition` to reveal a suspect only after a clue or prior interrogation outcome

### Interrogation-Driven Progression Case
- Author `interrogationOutcomes` at case scope
- Point nodes at `outcomeIdOnCorrect` or `outcomeIdOnWrong`
- Use outcomes to unlock visits, reveal suspects, or mark the case solve-ready

## Backward Compatibility Contract
- If `visitFlowMode` is absent or defaulted, runtime must behave exactly like version `2`.
- If `startingLocationId` is blank, the first resolved location remains the entry point.
- If `unlockCondition` is empty, v2 `unlockEvidenceIds` and `unlockTags` remain valid.
- If `presentSuspects` is empty, dossier and suspect presentation continue to use `involvedSuspects` and `SuspectData.currentLocationId`.
- If `interrogationOutcomes` is empty and node outcome IDs are blank, interrogation continues to use only existing rewards and branching.
- Existing 30 cases require no authored v3 data and should continue to run unchanged.

## Validation Requirements
- `startingLocationId` must resolve to a case location when non-blank.
- `nextLocationIds` must resolve within the same case.
- `presentSuspects.suspectId` must resolve to an `involvedSuspects` entry.
- `presentSuspects.interrogationEntryNodeId` must resolve to a case interrogation node when non-blank.
- `completionOutcomeId`, `outcomeIdOnCorrect`, and `outcomeIdOnWrong` must resolve to `CaseData.interrogationOutcomes`.
- no-hotspot locations are valid when they have explicit progression hooks, and required no-hotspot locations should use `autoCompleteOnEnter` when visit completion must count immediately.
- If `locationReadyForSolveMode != LegacyContradictionOnly`, validator should require at least one authored route to readiness.

## Implementation Boundary For Other Threads
- `CONTENT GENERATION`
  - can author visit graphs, unlock conditions, suspect presence, and interrogation outcomes once v3 fields exist
- `PROGRESSION LAYER`
  - owns runtime visit picker, revisit UX, and solve-ready gating presentation
- `ASSET_GEN`
  - only needed if additional visit-state icons or suspect-location markers are requested by UX
- `BUILD`
  - should validate schema `3` fallback behavior on the shipped roster before promoting pilot-authored visit graphs
