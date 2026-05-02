# Workstream Status

## Shared Milestone Summary
- Last updated: 2026-05-01
- Current milestone: `Casebook V2 - Precinct Hub + City Map + Deeper Suspect Flow`
- Integration summary:
  - Schema v2 scaffolding is in code.
  - Runtime fallback for `caseLocations` is in code.
  - SceneBuilder now targets a precinct home, department desk, and city map flow.
  - Thread owners should update only their own section below plus this summary when the milestone materially changes.

## ASSET_GEN
- Last updated: 2026-05-01
- Owner thread: `ASSET_GEN`
- Current milestone: `Casebook V2 - Precinct Hub + City Map + Deeper Suspect Flow`
- Completed:
  - Existing case backgrounds and evidence art batches are on disk for the 30-case roster.
  - Added manifest-driven presentation asset pipeline at `scripts/image-gen/build-presentation-manifest.js`, `generate-presentation.js`, `build-presentation-icons.py`, and `wire-presentation.js`.
  - Generated precinct shell art at `Assets/Sprites/Presentation/Precinct/precinct_home_hub.png` and `Assets/Sprites/Presentation/Precinct/precinct_department_board.png`.
  - Generated a deterministic city map base at `Assets/Sprites/Presentation/CityMap/city_map_base.png` from authored district/location mapping to avoid label/text artifacts.
  - Generated reusable icon sets on disk:
    - `Assets/Sprites/Presentation/DepartmentIcons/` (`3`)
    - `Assets/Sprites/Presentation/DistrictMarkers/` (`7`)
    - `Assets/Sprites/Presentation/LocationNodes/` (`11`)
  - Generated and wired suspect portraits for `S011-S030` at `Assets/Sprites/Presentation/Suspects/`.
  - Wired existing tracked sprite fields:
    - `DepartmentData.mapIcon` for Patrol / Fraud / Missing Persons
    - `SuspectData.portraitSprite` for `S011-S030`
  - Manifest now includes `30` authored location entries with district IDs, node archetypes, map positions, and default case background references for Integration wiring.
- In progress:
  - none in this thread
- Blocked:
  - Runtime `DistrictData` / `CityLocationData` asset creation is still owned by `Case Schema`; local script files exist in workspace but are not yet landed here with stable Unity `.meta` coverage, so this thread did not author district/location ScriptableObjects.
- Needs from other threads:
  - `Case Schema`: land tracked `DistrictData` / `CityLocationData` scripts and expected import contract so district markers and node metadata can be wired into runtime assets without GUID churn.
  - `PROGRESSION LAYER`: bind `city_map_base`, district markers, node icons, and suspect portraits into the precinct/map shell and confirm final slot sizing.
- Ready for integration: yes
- Notes for build thread:
  - Required precinct/map/location presentation assets now exist on disk and can be pulled by Integration without additional generation work.
  - Placeholder usage: there are no missing-slot placeholders in the new presentation pack; the two precinct shell backplates are first-pass AI art and may receive a later polish pass, but they are integration-ready for this milestone.
  - Use `scripts/image-gen/presentation-prompts.json` as the source of truth for asset paths, district marker IDs, node archetypes, and authored map positions.

## Case Schema
- Last updated: 2026-05-01
- Owner thread: `Case Schema`
- Current milestone: `Casebook V2 - Precinct Hub + City Map + Deeper Suspect Flow`
- Completed:
  - `CaseData` v2 fields for department/district/location/arc/case visits
  - `CaseLocationData`, `DistrictData`, and `CityLocationData` types
  - `SuspectData` and `InterrogationNode` v2 additions
  - bootstrapper fallback for first-pass district/location/case-visit migration
- In progress:
  - validator hardening for authored multi-location cases
  - smoke coverage expansion for city map flow
- Blocked:
  - none
- Needs from other threads:
  - `CONTENT GENERATION`: final authored district/location mappings for non-fallback data
- Ready for integration: yes
- Notes for build thread:
  - Run schema upgrade and validation after pulling new authored content.

## CONTENT GENERATION
- Last updated: 2026-05-01
- Owner thread: `CONTENT GENERATION`
- Current milestone: `Casebook V2 - Precinct Hub + City Map + Deeper Suspect Flow`
- Completed:
  - 30-case precinct/map content matrix authored in `Docs/content/precinct_map_case_matrix_C001_C030.json`
  - all 30 cases now have authored department, district, city location, case arc, arc beat, and case-visit metadata in source form
  - interrogation-forward milestone list is explicitly authored for 10 cases
  - single-location vs multi-location milestone split is explicitly authored for the full roster
  - district naming/theme pass is authored for `Old Quarter`, `Civic Core`, `Market Row`, `Riverside`, `Skyline`, `North Quay`, and `Outer Reach`
  - validator added at `scripts/content/validate-precinct-map-matrix.mjs` and passing
- In progress:
  - none in this thread
- Blocked:
  - none
- Needs from other threads:
  - `Case Schema`: keep runtime validator/import expectations aligned with the authored source matrix when case assets are backfilled
  - `ASSET_GEN`: apply authored district/location naming to final map markers, node labels, and hub presentation
  - `PROGRESSION LAYER`: wire the authored city node and district labels into the precinct/map UI flow
- Ready for integration: yes
- Notes for build thread:
  - Use the authored matrix as the source of truth instead of bootstrap fallback names when wiring district and city location assets.

## PROGRESSION LAYER
- Last updated: 2026-05-01
- Owner thread: `PROGRESSION LAYER`
- Current milestone: `Casebook V2 - Precinct Hub + City Map + Deeper Suspect Flow`
- Completed:
  - XP/rank/star/daily case/achievement systems remain intact under the new macro loop.
  - Home now presents the precinct hub flow with city-map-first investigation entry, daily case context, active department copy, and promotion messaging.
  - Case Select now reads as a department desk hierarchy with lock requirements, department summaries, map-aware case rows, and suspect/interrogation flow summaries.
  - City Map now binds the authored base art, district markers, node icons, lock messaging, daily-case emphasis, and map-positioned case launch nodes.
  - Progression bootstrap now imports the authored precinct/map matrix plus presentation manifest into runtime department, district, city-location, suspect-summary, and case-route metadata during `Casebook -> Build Scene`.
  - Suspect/interrogation presentation is surfaced in play through case metadata, dossier fallback cards for summary-only suspects, and clearer interrogation-forward status messaging.
- In progress:
  - none in this thread
- Blocked:
  - none
- Needs from other threads:
  - none
- Ready for integration: yes
- Notes for build thread:
  - Run `Casebook -> Build Scene` after pulling this thread so the bootstrapper materializes district and city-location assets from authored content before smoke testing.
  - Generated runtime world-map assets are build-time derived from `Docs/content/precinct_map_case_matrix_C001_C030.json` and `scripts/image-gen/presentation-prompts.json`; no manual prefab editing is required.
  - Recommended smoke path: Home -> City Map -> unlocked node launch -> in-game menu -> Suspect Dossier -> resume -> solve -> return to desk/map.

## BUILD
- Last updated: 2026-05-01
- Owner thread: `BUILD`
- Current milestone: `Casebook V2 - Precinct Hub + City Map + Deeper Suspect Flow`
- Completed:
  - Existing build/deploy path already exists in repo
- In progress:
  - waiting for integration-ready signal
- Blocked:
  - milestone not yet marked ready by content/progression/asset threads
- Needs from other threads:
  - `Case Schema`, `CONTENT GENERATION`, `ASSET_GEN`, `PROGRESSION LAYER`: all sections must read `Ready for integration: yes`
- Ready for integration: no
- Notes for build thread:
  - When activated, run validation, scene build, smoke test, manual hub flow check, then packaging.
