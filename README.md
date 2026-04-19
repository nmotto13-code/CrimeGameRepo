# Pocket Casebook: Contradiction Engine

iOS-first micro-mystery mobile game. Each case is solved by identifying one provable contradiction between evidence and statements.

## Quick Start

1. Open Unity Hub → **Add Project from Disk** → select this `CrimeGame` folder
2. Unity version: **2022.3 LTS** (or latest 2022.3.x)
3. Required packages (add via Package Manager):
   - **TextMeshPro** (built-in, just import TMP Essentials when prompted)
4. In Unity: **Casebook → Generate Starter Cases** to create all 10 case assets
5. Open `Assets/Scenes/CaseScene.unity`
6. Press **Play** — Case 001 loads automatically

## Project Structure

```
Assets/
  Scripts/
    Data/          — ScriptableObject data models (CaseData, EvidenceData, ClaimData…)
    Core/          — Runtime systems (CaseLoader, GameManager, ContradictionEvaluator…)
    UI/            — UI controllers (TabController, EvidenceCardUI, ClaimCardUI…)
    Tools/         — Tool system (ToolsController: Cross-Check, Enhance, Timeline Snap)
    Viewers/       — EvidenceViewer (pinch/zoom/pan + enhance overlay)
    Editor/        — CaseGeneratorEditor (menu item to build all 10 cases)
  Resources/Cases/ — Runtime-loadable CaseData assets (Case_001 … Case_010)
  ScriptableObjects/Cases/ — Source assets
  Scenes/          — CaseScene.unity (single reused scene)
  Prefabs/         — EvidenceCard, ClaimCard, Hotspot, BoardSlot prefabs
```

## Scene Setup (Manual — one time)

Create `CaseScene.unity` with this hierarchy:

```
Canvas (Screen Space - Overlay)
  Background (Image) ← assign to CaseLoader.sceneBackground
  HotspotLayer (RectTransform, full screen) ← assign to CaseLoader.hotspotParent
  TabBar
    BriefBtn / EvidenceBtn / ClaimsBtn / BoardBtn ← assign to TabController.tabButtons
  TabContent ← assign IBeginDrag/IDrag/IEndDrag to TabController
    BriefPanel
      CaseTitleText (TMP) ← CaseLoader.caseTitleText
      BriefText (TMP)     ← CaseLoader.briefText
    EvidencePanel
      ScrollView/Content  ← CaseLoader.evidenceListParent
    ClaimsPanel
      ScrollView/Content  ← CaseLoader.claimsListParent
    BoardPanel
      Slot_A / Slot_B / Slot_C (BoardSlotUI) ← BoardController.slots
  ToolsBar
    CrossCheckBtn ← ToolsController.crossCheckButton
    EnhanceBtn    ← ToolsController.enhanceButton
    TimelineBtn   ← ToolsController.timelineSnapButton
  SubmitButton (SubmitButton component)
  ResultPanel (hidden) ← ResultsController.resultPanel
  EvidenceDetailPanel (hidden) ← EvidenceDetailPanel.panel

GameManager (GameObject — also attach CaseLoader, ContradictionEvaluator, ResultsController,
             ToolsController, BoardController, TabController, EvidenceDetailPanel)
```

## Prefabs Needed

- **EvidenceCard.prefab** — attach `EvidenceCardUI`
- **ClaimCard.prefab** — attach `ClaimCardUI`
- **Hotspot.prefab** — attach `HotspotController` + circular Image (pulse ring child)
- **BoardSlot.prefab** — attach `BoardSlotUI`

## Tools

| Tool | Charges | Effect |
|------|---------|--------|
| CROSS-CHECK | 2/case | Long-press a claim → highlights matching evidence |
| ENHANCE | 12s cooldown | Reveals hidden overlay + unlocks new evidence tags |
| TIMELINE SNAP | 1/case | Pin 2 TIME-tagged items to Board → checks for conflict |

## Adding New Cases

Cases are data-only — no new scenes or code needed:
1. **Casebook → Generate Starter Cases** to see the pattern
2. Duplicate a `Case_00X` asset in `Assets/Resources/Cases/`
3. Create linked `EvidenceData` and `ClaimData` assets
4. Assign to the new `CaseData`
5. Add to `GameManager.availableCases[]` array

## iOS Build

1. File → Build Settings → iOS
2. Player Settings → Bundle ID: `com.yourname.pocketcasebook`
3. Orientation: Portrait only
4. Target minimum iOS: 15.0
