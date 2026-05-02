# Merge Checklist

## Play Mode Verification
1. Open the project in Unity `6000.3.8f1`.
2. Run `Tools/SmokeTest/Run` and confirm the Console ends with a PASS summary and no FAIL lines.
3. Open `Assets/Scenes/CaseScene.unity`.
4. Enter Play Mode.
5. Confirm the app boots to Home, not directly into Game.
6. From Home, open `City Map` and confirm mapped case nodes appear.
7. From Home, open `Department Desk` and confirm cases are grouped by department with lock state.
8. Launch `Case_001` or another unlocked case from the map or department desk.
9. On the Brief tab, confirm title and briefing text match the selected case.
10. On the Scene tab, confirm the active case-visit background loads and hotspots can be discovered.
11. Find at least one clue and confirm an evidence card appears in the Evidence tab.
12. If the case has interrogation content, confirm `INTERROGATE` can open before final solve.
13. Enter the solve flow, select a claim, and confirm the results panel appears.
14. Retry the case once and confirm the scene reloads cleanly.
15. Solve correctly once and confirm the next-case flow still advances.

## Navigation Regression Checks
Verify the existing shell and back-stack behavior did not regress:

- Home -> Case Select -> Back returns to Home.
- Home -> City Map -> Back returns to Home.
- Home -> Profile -> Back returns to Home.
- Game screen opens only after case launch and keeps the 4-tab layout intact.
- Hamburger button opens the in-game menu without destroying the underlying Game screen.
- In-game interrogation can close cleanly back to the active case.
- Android back or `Esc` from Game opens the in-game menu.
- Android back or `Esc` from the in-game menu returns to Game.
- If Evidence Detail is open, back closes the detail panel before touching screen navigation.
- If the leave-case confirm dialog is open, back dismisses the dialog before touching screen navigation.
- Returning to Home from an active case does not leave stale overlays or duplicate screens behind.

## Case Integrity Checks
Run these whenever case content or schema-adjacent systems change:

- All `Assets/Resources/Cases/Case_*.asset` files load in the scene builder.
- Each case has a background sprite, at least one hotspot, at least one evidence asset, and at least one claim asset.
- Each case resolves to at least one valid case visit, either authored or fallback-generated.
- Every hotspot points at evidence that exists inside the same case.
- `contradictoryClaimId` maps to a linked claim.
- `primaryEvidenceIdA` and `primaryEvidenceIdB` map to linked evidence.
- Cases intended for the city map have department, district, and city location values authored or bootstrap-generated.
- Existing cases still use the same IDs unless an explicit migration updates references.
- New schema fields are additive or have an editor migration path documented in `Docs/schema.md`.

## Safe Merge Scope
Safe to merge from this thread:

- `Docs/vision.md`
- `Docs/schema.md`
- `Docs/merge_checklist.md`
- Editor-only smoke-test code under `Assets/Scripts/Editor/`
- Editor-only `SceneBuilder` automation refactor that suppresses modal dialogs in batchmode

Not covered by this thread:

- Department progression runtime UI
- Meta-arc runtime systems
- Dossier/interrogation feature implementation
- Any monetization, analytics, or IAP work
