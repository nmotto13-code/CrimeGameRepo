# Casebook Vision

## North Star
Casebook should stay fast and legible on mobile while growing from a single contradiction loop into a scalable detective platform:

- Standalone cases remain short, clean, and data-driven.
- Department progression adds long-term structure without breaking pick-up-and-play sessions.
- Meta arcs layer across batches of cases instead of forcing every case into a single serialized plot.
- New mechanics arrive by department so complexity ramps with player mastery.

## Why Department Progression
The department ladder gives the game a natural content spine:

- `Patrol` teaches scene reading, simple contradiction logic, and low-stakes evidence review.
- `Fraud` introduces denser document evidence, timeline inconsistencies, and tool-dependent reveals.
- `Missing Persons` expands suspect webs, multi-location reasoning, and dossier cross-reference.
- `Homicide` adds deeper interrogation structure, motive chains, and higher evidence ambiguity.
- `Major Crimes` supports multi-case arcs, recurring suspects, and combined mechanics from prior departments.

This model fits the current repo because it does not require replacing the working shell. The existing `GameManager`, `CaseSelectController`, and `Resources/Cases` flow can keep loading cases normally while a future department layer decides which subset is unlocked and how the roster is grouped.

## Case Scaling Model
The current playable unit is `CaseData` plus linked `EvidenceData` and `ClaimData` assets. To scale from 10 cases to 50-200+, content should grow through templates instead of bespoke logic.

Recommended content structure:

- `DepartmentDefinition`: progression order, unlock requirements, mechanic flags, palette/theme, future city linkage.
- `CaseTemplateDefinition`: reusable structure for a case family such as apartment homicide, office fraud, transit witness case, or staged disappearance.
- `CaseData`: the authored playable instance, still the runtime payload loaded by the scene.
- `ArcBeatDefinition`: optional narrative payload that can attach to a case without changing the core solve loop.

Recommended difficulty tiers:

- `Tier 1`: 3 claims, 4-5 evidence items, 1 clear contradiction, minimal tool dependency.
- `Tier 2`: 3-4 claims, 5-6 evidence items, at least one misleading interpretation, tool usage encouraged.
- `Tier 3`: 4-5 claims, 6-7 evidence items, overlapping timelines, stronger red herrings, optional multi-step reveal.

Authoring rules for scale:

- Keep case logic declarative inside assets.
- Reuse evidence display modes and tag taxonomies rather than case-specific code paths.
- Treat tools as department-tuned parameters, not hard-coded per-case mechanics.
- Add validation early so large content batches fail in editor, not at runtime.

## Meta-Arc Layering
Casebook should separate the case-of-the-day from the larger conspiracy:

- Every case must still resolve as a satisfying standalone episode.
- Meta arcs should appear as optional overlays: recurring names, symbol patterns, department politics, suspect reappearances, tampered reports.
- A season-sized arc should span roughly 10-15 cases, usually crossing at least two departments.

Suggested cadence:

- Cases 1-5: seed names, locations, and one unresolved anomaly.
- Cases 6-10: escalate pattern recognition and add an internal or criminal-network connective thread.
- Cases 11-15: pay off one major question while opening the next season hook.

This keeps the current mobile loop intact. The contradiction solve remains the moment-to-moment mechanic; the meta layer only changes what content gets attached to a case, not how the shell navigates or how the scene is built.

## Retention Systems Direction
Retention should reinforce detective identity rather than feel bolted on:

- Rank/XP tied to department progress and case mastery.
- Stars based on solve quality, evidence completeness, and wrong-guess count.
- Achievements for clean solves, full sweeps, streaks, and arc milestones.
- Daily case rotation built from validated case assets, not special-case logic.
- Streaks and challenge modifiers as profile-level systems outside the base case schema.

## Integration Guidance
Keep these boundaries stable as main evolves:

- The navigation shell and safe-area behavior stay intact.
- `SceneBuilder` remains the source of truth for the playable scene.
- Runtime case execution continues to consume data assets rather than hard-coded branches.
- New progression or narrative layers should wrap the existing case loop, not replace it.
