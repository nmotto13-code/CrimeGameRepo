#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CasebookGame.Core;
using CasebookGame.Data;
using CasebookGame.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CasebookGame.Editor
{
    [InitializeOnLoad]
    public static class PresentationPolishRuntimeVerifier
    {
        const string RequestKey = "PresentationPolishRuntimeVerifier.Requested";
        const string LogPath = "Temp/PresentationPolishRuntimeVerifier.md";
        const string ScenePath = "Assets/Scenes/CaseScene.unity";

        static readonly Dictionary<string, string> ExpectedSecondVisitBackgrounds = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Case_003", "Assets/Sprites/Backgrounds/case003_harwick_gallery_side_entrance.jpg" },
            { "Case_013", "Assets/Sprites/Backgrounds/case013_brasslight_loading_alley.jpg" },
            { "Case_023", "Assets/Sprites/Backgrounds/case023_marina_ramp.jpg" }
        };

        static readonly string[] PilotCaseIds = { "Case_020", "Case_030" };
        static readonly string[] SecondVisitCaseIds = { "Case_003", "Case_013", "Case_023" };

        static readonly string[] RequiredPolishKeys =
        {
            "Panels/current_location_plate",
            "Panels/route_summary_plate",
            "Panels/visit_list_plate",
            "Panels/suspect_presence_plate",
            "Panels/solve_gate_plate",
            "Panels/scene_hint_plate",
            "Panels/lead_action_plate",
            "Panels/map_selection_plate",
            "Panels/map_legend_plate",
            "Panels/dossier_card_plate",
            "Panels/dossier_header_plate",
            "Panels/dossier_detail_plate",
            "Panels/dossier_summary_plate",
            "Panels/portrait_frame_plate",
            "Panels/interrogation_prompt_plate",
            "Panels/interrogation_feedback_plate",
            "Panels/interrogation_trigger_plate",
            "Panels/interrogation_response_plate",
            "Panels/interrogation_response_success",
            "Panels/interrogation_response_failure",
            "VisitState/visit_current_plate",
            "VisitState/visit_available_plate",
            "VisitState/visit_completed_plate",
            "VisitState/visit_locked_plate",
            "VisitState/visit_visited_plate",
            "VisitState/state_current",
            "VisitState/state_available",
            "VisitState/state_completed",
            "VisitState/state_locked",
            "VisitState/state_visited",
            "Interrogation/interrogation_ready_badge",
            "Map/node_unlocked_plate",
            "Map/node_locked_plate",
            "Map/node_selected_plate"
        };

        static int stepIndex = -1;
        static double nextStepAt;
        static int failureCount;
        static int warningCount;
        static bool finalizeRequested;
        static CaseScenario activeScenario;

        static PresentationPolishRuntimeVerifier()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        [MenuItem("Casebook/Verify/Presentation Polish Runtime")]
        public static void RunFromMenu() => RunInternal();

        public static void RunFromCommandLine() => RunInternal();

        static void RunInternal()
        {
            failureCount = 0;
            warningCount = 0;
            finalizeRequested = false;
            stepIndex = -1;
            activeScenario = null;

            Directory.CreateDirectory(Path.GetDirectoryName(LogPath) ?? "Temp");
            File.WriteAllText(LogPath,
                $"# Presentation Polish Runtime Verification{Environment.NewLine}{Environment.NewLine}" +
                $"- Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}");

            SessionState.SetBool(RequestKey, true);
            Append($"Requested runtime verification. Unity batch mode: {Application.isBatchMode}.");

            SceneBuilder.BuildSceneForAutomation();
            EditorSceneManager.OpenScene(ScenePath);

            if (!EditorApplication.isPlaying)
                EditorApplication.EnterPlaymode();
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(RequestKey, false))
                return;

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                Append("Entered Play Mode.");
                stepIndex = 0;
                nextStepAt = EditorApplication.timeSinceStartup + 1.0d;
            }
            else if (state == PlayModeStateChange.EnteredEditMode && finalizeRequested)
            {
                finalizeRequested = false;
                SessionState.SetBool(RequestKey, false);
                Append($"Completed with {failureCount} failure(s) and {warningCount} warning(s).");
                if (Application.isBatchMode)
                    EditorApplication.Exit(failureCount > 0 ? 1 : 0);
            }
        }

        static void OnEditorUpdate()
        {
            if (!SessionState.GetBool(RequestKey, false) || !EditorApplication.isPlaying)
                return;

            if (EditorApplication.timeSinceStartup < nextStepAt)
                return;

            try
            {
                Advance();
            }
            catch (Exception ex)
            {
                Fail($"Unhandled verifier exception: {ex}");
                Finish();
            }
        }

        static void Advance()
        {
            switch (stepIndex)
            {
                case 0:
                    VerifyBoot();
                    Wait(0.25d);
                    stepIndex++;
                    break;
                case 1:
                    VerifyPresentationPolishLoads();
                    Wait(0.15d);
                    stepIndex++;
                    break;
                case 2:
                    VerifyCityMapReadability();
                    Wait(0.15d);
                    stepIndex++;
                    break;
                case 3:
                    activeScenario = CaseScenario.ForSecondVisit("Case_003");
                    Append("Starting second-visit verification for Case_003.");
                    stepIndex++;
                    Wait(0.05d);
                    break;
                case 4:
                    if (AdvanceCaseScenario())
                    {
                        activeScenario = CaseScenario.ForSecondVisit("Case_013");
                        Append("Starting second-visit verification for Case_013.");
                        stepIndex++;
                    }
                    break;
                case 5:
                    if (AdvanceCaseScenario())
                    {
                        activeScenario = CaseScenario.ForSecondVisit("Case_023");
                        Append("Starting second-visit verification for Case_023.");
                        stepIndex++;
                    }
                    break;
                case 6:
                    if (AdvanceCaseScenario())
                    {
                        activeScenario = CaseScenario.ForPilot("Case_020");
                        Append("Starting full pilot-flow verification for Case_020.");
                        stepIndex++;
                    }
                    break;
                case 7:
                    if (AdvanceCaseScenario())
                    {
                        activeScenario = CaseScenario.ForPilot("Case_030");
                        Append("Starting full pilot-flow verification for Case_030.");
                        stepIndex++;
                    }
                    break;
                case 8:
                    if (AdvanceCaseScenario())
                    {
                        stepIndex++;
                        Wait(0.05d);
                    }
                    break;
                case 9:
                    Finish();
                    break;
            }
        }

        static void VerifyBoot()
        {
            var gameManager = GameManager.Instance;
            var navigation = NavigationManager.Instance;
            var tabController = TabController.Instance;
            var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            var scaler = canvas != null ? canvas.GetComponent<CanvasScaler>() : null;

            Require(gameManager != null, "GameManager instance exists.");
            Require(navigation != null, "NavigationManager instance exists.");
            Require(tabController != null, "TabController instance exists.");
            Require(canvas != null, "Canvas exists in runtime scene.");
            if (scaler != null)
            {
                Append($"Canvas scaler reference: {scaler.referenceResolution.x} x {scaler.referenceResolution.y}; screen: {Screen.width} x {Screen.height}.");
            }

            VerifyActiveScreens("Boot state", ScreenId.Home);
        }

        static void VerifyPresentationPolishLoads()
        {
            int loaded = 0;
            foreach (var key in RequiredPolishKeys)
            {
                var sprite = PresentationPolishCatalog.Load(key);
                if (sprite == null)
                    Fail($"Missing runtime polish sprite: {key}");
                else
                    loaded++;
            }

            Append($"Loaded {loaded}/{RequiredPolishKeys.Length} required PresentationPolish sprites at runtime.");
        }

        static void VerifyCityMapReadability()
        {
            var navigation = NavigationManager.Instance;
            navigation?.ResetToRootChild(ScreenId.CityMap);
            Canvas.ForceUpdateCanvases();

            var controller = UnityEngine.Object.FindFirstObjectByType<CityMapScreenController>();
            Require(controller != null, "CityMapScreenController exists.");
            VerifyActiveScreens("City map open", ScreenId.CityMap);

            var mapRoot = GetField<RectTransform>(controller, "mapRoot");
            var summaryText = GetField<TMP_Text>(controller, "summaryText");
            var selectionDistrictText = GetField<TMP_Text>(controller, "selectionDistrictText");
            var selectionLocationText = GetField<TMP_Text>(controller, "selectionLocationText");
            var selectionBodyText = GetField<TMP_Text>(controller, "selectionBodyText");
            var selectionStatusText = GetField<TMP_Text>(controller, "selectionStatusText");
            var mapBackgroundImage = GetField<Image>(controller, "mapBackgroundImage");

            Require(mapRoot != null, "City map root is wired.");
            Require(mapBackgroundImage != null && mapBackgroundImage.sprite != null, "City map base sprite is visible at runtime.");

            VerifyTextNotOverflowing(summaryText, "City map summary");
            VerifyTextNotOverflowing(selectionDistrictText, "City map district selection");
            VerifyTextNotOverflowing(selectionLocationText, "City map location selection");
            VerifyTextNotOverflowing(selectionBodyText, "City map body selection");
            VerifyTextNotOverflowing(selectionStatusText, "City map status selection");

            var nodeRects = new List<RectTransform>();
            int nodeCount = 0;
            foreach (Transform child in mapRoot)
            {
                if (!child.name.StartsWith("Node_", StringComparison.Ordinal))
                    continue;

                nodeCount++;
                var rt = child as RectTransform;
                if (rt != null)
                {
                    nodeRects.Add(rt);
                    if (rt.rect.width < 110f || rt.rect.height < 110f)
                        Warn($"Map node {child.name} is smaller than expected polish target: {rt.rect.size}.");
                }

                var background = child.GetComponent<Image>();
                Require(background != null && background.sprite != null, $"Map node {child.name} has a runtime plate sprite.");

                var label = child.GetComponentsInChildren<TextMeshProUGUI>(true)
                    .FirstOrDefault(text => text.name == "Label");
                VerifyTextNotOverflowing(label, $"Map node label {child.name}");
            }

            Require(nodeCount > 0, "City map built at least one case node.");
            VerifyRectsDoNotOverlap(nodeRects, "City map nodes");
            Append($"City map runtime check found {nodeCount} case nodes.");
        }

        static bool AdvanceCaseScenario()
        {
            if (activeScenario == null)
                return true;

            var gameManager = GameManager.Instance;
            var navigation = NavigationManager.Instance;
            if (gameManager == null || navigation == null)
            {
                Fail("GameManager or NavigationManager missing during case verification.");
                return true;
            }

            switch (activeScenario.Phase)
            {
                case 0:
                    gameManager.LoadCaseById(activeScenario.CaseId);
                    navigation.ResetToRootChild(ScreenId.Game);
                    TabController.Instance?.SwitchToTab(0);
                    Canvas.ForceUpdateCanvases();
                    VerifyActiveScreens($"{activeScenario.CaseId} game open", ScreenId.Game);
                    activeScenario.Phase++;
                    Wait(0.10d);
                    return false;

                case 1:
                    VerifyBriefReadability(activeScenario.CaseId);
                    if (activeScenario.Mode == CaseScenarioMode.Pilot)
                    {
                        activeScenario.Phase++;
                        OpenDossierAndVerify();
                        Wait(0.10d);
                        return false;
                    }

                    activeScenario.Phase = 3;
                    return false;

                case 2:
                    VerifyOverlayClosed(activeScenario.CaseId, ScreenId.Dossier);
                    activeScenario.Phase = 3;
                    Wait(0.05d);
                    return false;

                case 3:
                    if (activeScenario.Mode == CaseScenarioMode.SecondVisit)
                    {
                        if (EnsureLocationUnlockedAndSelected(activeScenario, 1))
                            activeScenario.Phase++;
                        return false;
                    }

                    activeScenario.Phase++;
                    return false;

                case 4:
                    if (activeScenario.Mode == CaseScenarioMode.SecondVisit)
                    {
                        VerifySecondVisitBackground(activeScenario.CaseId);
                        VerifyBriefReadability(activeScenario.CaseId);
                        Append($"{activeScenario.CaseId} second-visit background verified.");
                        activeScenario = null;
                        Wait(0.05d);
                        return true;
                    }

                    if (AdvancePilotFlow(activeScenario))
                    {
                        VerifyBriefReadability(activeScenario.CaseId);
                        OpenDossierAndVerify();
                        activeScenario.Phase++;
                    }
                    return false;

                case 5:
                    VerifyOverlayClosed(activeScenario.CaseId, ScreenId.Dossier);
                    Append($"{activeScenario.CaseId} pilot flow reached solve-ready state.");
                    activeScenario = null;
                    Wait(0.05d);
                    return true;
            }

            activeScenario = null;
            return true;
        }

        static bool EnsureLocationUnlockedAndSelected(CaseScenario scenario, int locationIndex)
        {
            var gameManager = GameManager.Instance;
            if (gameManager?.CurrentCase == null)
            {
                Fail($"No current case loaded while checking {scenario.CaseId} second visit.");
                scenario.Phase = int.MaxValue;
                return true;
            }

            if (gameManager.CurrentCase.GetResolvedLocationCount() <= locationIndex)
            {
                Fail($"{scenario.CaseId} does not expose location index {locationIndex}.");
                scenario.Phase = int.MaxValue;
                return true;
            }

            if (gameManager.CurrentLocationIndex == locationIndex)
                return true;

            if (gameManager.CanVisitLocationIndex(locationIndex))
            {
                gameManager.SetCurrentLocationIndex(locationIndex);
                Canvas.ForceUpdateCanvases();
                Wait(0.10d);
                return false;
            }

            if (AdvanceProgressOneStep(gameManager, scenario))
                return false;

            Fail($"{scenario.CaseId} could not unlock visit index {locationIndex} during verification.");
            scenario.Phase = int.MaxValue;
            return true;
        }

        static bool AdvancePilotFlow(CaseScenario scenario)
        {
            var gameManager = GameManager.Instance;
            if (gameManager?.CurrentCase == null)
            {
                Fail($"No current case loaded while advancing pilot flow for {scenario.CaseId}.");
                return true;
            }

            if (gameManager.IsCaseReadyForSolve())
                return true;

            if (scenario.GuardSteps++ > 30)
            {
                Fail($"{scenario.CaseId} pilot flow did not reach solve-ready state within 30 verification steps.");
                return true;
            }

            if (AdvanceProgressOneStep(gameManager, scenario))
                return false;

            Fail($"{scenario.CaseId} pilot flow stalled before solve-ready state.");
            return true;
        }

        static bool AdvanceProgressOneStep(GameManager gameManager, CaseScenario scenario)
        {
            var interrogation = InterrogationFlowController.Instance;
            var panel = GetField<GameObject>(interrogation, "panel");
            if (panel != null && panel.activeSelf)
            {
                var currentNode = GetField<InterrogationNode>(interrogation, "currentNode");
                var responseButtons = GetField<Button[]>(interrogation, "responseButtons");
                if (currentNode != null && responseButtons != null
                    && currentNode.correctResponseIndex >= 0
                    && currentNode.correctResponseIndex < responseButtons.Length
                    && responseButtons[currentNode.correctResponseIndex] != null)
                {
                    responseButtons[currentNode.correctResponseIndex].onClick.Invoke();
                    double waitSeconds = GetField<float>(interrogation, "feedbackDurationSeconds") + 0.20d;
                    Wait(waitSeconds);
                    return true;
                }

                Warn($"{scenario.CaseId} interrogation panel was open but no valid correct response button could be clicked.");
                Wait(0.20d);
                return true;
            }

            var availablePresence = gameManager.GetCurrentLocationSuspectPresence()
                .FirstOrDefault(presence =>
                    presence != null
                    && !string.IsNullOrWhiteSpace(presence.interrogationEntryNodeId)
                    && interrogation != null
                    && interrogation.IsEntryNodeAvailable(presence.interrogationEntryNodeId));
            if (availablePresence != null)
            {
                bool started = interrogation.TryBegin(gameManager.CurrentCase, availablePresence.interrogationEntryNodeId, null);
                Require(started, $"{scenario.CaseId} started interrogation for {availablePresence.suspectId}.");
                Wait(0.15d);
                return true;
            }

            var currentLocation = gameManager.CurrentLocation;
            if (currentLocation != null && !gameManager.HasCompletedLocation(currentLocation.locationId))
            {
                gameManager.MarkLocationCompleted(currentLocation.locationId);
                Wait(0.10d);
                return true;
            }

            for (int index = 0; index < gameManager.CurrentCase.GetResolvedLocationCount(); index++)
            {
                if (index == gameManager.CurrentLocationIndex)
                    continue;

                var location = gameManager.CurrentCase.GetResolvedLocation(index);
                if (location == null)
                    continue;

                if (gameManager.CanVisitLocationIndex(index)
                    && (!gameManager.HasVisitedLocation(location.locationId) || !gameManager.HasCompletedLocation(location.locationId)))
                {
                    gameManager.SetCurrentLocationIndex(index);
                    Wait(0.10d);
                    return true;
                }
            }

            return false;
        }

        static void OpenDossierAndVerify()
        {
            var navigation = NavigationManager.Instance;
            navigation?.ShowScreen(ScreenId.Dossier, TransitionType.None);
            Canvas.ForceUpdateCanvases();

            VerifyActiveScreens("Dossier overlay open", ScreenId.Game, ScreenId.Dossier);

            var controller = UnityEngine.Object.FindFirstObjectByType<DossierScreenController>();
            Require(controller != null, "DossierScreenController exists.");

            var listParent = GetField<Transform>(controller, "listParent");
            var emptyStateText = GetField<TMP_Text>(controller, "emptyStateText");
            if (emptyStateText != null && emptyStateText.gameObject.activeSelf)
                Fail("Dossier overlay opened but only the empty state is visible.");

            Require(listParent != null && listParent.childCount > 0, "Dossier list contains rendered suspect cards.");

            if (listParent != null)
            {
                var textComponents = listParent.GetComponentsInChildren<TextMeshProUGUI>(true)
                    .Where(text => text.gameObject.activeInHierarchy)
                    .ToArray();
                foreach (var text in textComponents)
                    VerifyTextNotOverflowing(text, $"Dossier text {text.name}");
            }

            navigation?.Pop(TransitionType.None);
        }

        static void VerifyOverlayClosed(string caseId, ScreenId overlayId)
        {
            VerifyActiveScreens($"{caseId} overlay close", ScreenId.Game);
            Append($"{caseId} overlay {overlayId} opened and closed without leaving extra active screens.");
        }

        static void VerifySecondVisitBackground(string caseId)
        {
            var caseLoader = CaseLoader.Instance;
            var backgroundImage = GetField<Image>(caseLoader, "sceneBackground");
            Require(backgroundImage != null && backgroundImage.sprite != null, $"{caseId} scene background image is populated at second visit.");
            if (backgroundImage?.sprite == null)
                return;

            string actualPath = AssetDatabase.GetAssetPath(backgroundImage.sprite);
            string expectedPath = ExpectedSecondVisitBackgrounds.TryGetValue(caseId, out var value) ? value : string.Empty;
            if (!string.Equals(actualPath, expectedPath, StringComparison.OrdinalIgnoreCase))
                Fail($"{caseId} second-visit background mismatch. Expected `{expectedPath}` but found `{actualPath}`.");
        }

        static void VerifyBriefReadability(string label)
        {
            var controller = UnityEngine.Object.FindFirstObjectByType<CaseVisitFlowController>();
            Require(controller != null, $"CaseVisitFlowController exists for {label}.");
            if (controller == null)
                return;

            var currentLocationText = GetField<TMP_Text>(controller, "currentLocationText");
            var routeSummaryText = GetField<TMP_Text>(controller, "routeSummaryText");
            var suspectPresenceText = GetField<TMP_Text>(controller, "suspectPresenceText");
            var solveGateText = GetField<TMP_Text>(controller, "solveGateText");
            var sceneHintText = GetField<TMP_Text>(controller, "sceneHintText");
            var visitButtonParent = GetField<Transform>(controller, "visitButtonParent");
            var leadActionButton = GetField<Button>(controller, "leadActionButton");
            var leadActionLabel = GetField<TMP_Text>(controller, "leadActionLabel");

            var topLevelRects = new List<RectTransform>();
            AddRect(topLevelRects, currentLocationText);
            AddRect(topLevelRects, routeSummaryText);
            AddRect(topLevelRects, suspectPresenceText);
            AddRect(topLevelRects, solveGateText);
            AddRect(topLevelRects, sceneHintText);
            AddRect(topLevelRects, leadActionButton);
            AddRect(topLevelRects, visitButtonParent);

            VerifyTextNotOverflowing(currentLocationText, $"{label} current location");
            VerifyTextNotOverflowing(routeSummaryText, $"{label} route summary");
            VerifyTextNotOverflowing(suspectPresenceText, $"{label} suspect presence");
            VerifyTextNotOverflowing(solveGateText, $"{label} solve gate");
            VerifyTextNotOverflowing(sceneHintText, $"{label} scene hint");
            VerifyTextNotOverflowing(leadActionLabel, $"{label} lead action");

            var routePlate = currentLocationText != null ? currentLocationText.GetComponent<Image>() : null;
            Require(routePlate != null && routePlate.sprite != null, $"{label} current-location plate loaded at runtime.");

            if (visitButtonParent != null)
            {
                foreach (Transform child in visitButtonParent)
                {
                    var buttonImage = child.GetComponent<Image>();
                    Require(buttonImage != null && buttonImage.sprite != null, $"{label} visit button {child.name} has plate sprite.");

                    var textComponents = child.GetComponentsInChildren<TextMeshProUGUI>(true);
                    foreach (var text in textComponents)
                        VerifyTextNotOverflowing(text, $"{label} visit button text {child.name}");
                }
            }

            VerifyRectsDoNotOverlap(topLevelRects, $"{label} brief stack");
        }

        static void VerifyActiveScreens(string context, params ScreenId[] expected)
        {
            var expectedSet = new HashSet<ScreenId>(expected);
            var screens = UnityEngine.Object.FindObjectsByType<BaseScreen>(FindObjectsSortMode.None);
            var active = screens
                .Where(screen => screen != null && screen.gameObject.activeInHierarchy && screen.CanvasGroup != null && screen.CanvasGroup.alpha > 0.01f)
                .Select(screen => screen.ScreenId)
                .Distinct()
                .ToList();

            foreach (var screenId in expectedSet)
            {
                if (!active.Contains(screenId))
                    Fail($"{context}: expected active screen `{screenId}` was not active.");
            }

            foreach (var screenId in active)
            {
                if (!expectedSet.Contains(screenId))
                    Fail($"{context}: unexpected active screen `{screenId}` remained visible.");
            }
        }

        static void VerifyTextNotOverflowing(TMP_Text text, string label)
        {
            if (text == null)
            {
                Fail($"{label}: text reference missing.");
                return;
            }

            if (!text.gameObject.activeInHierarchy)
                return;

            Canvas.ForceUpdateCanvases();
            if (text.isTextOverflowing)
                Fail($"{label}: text is overflowing at runtime.");
        }

        static void VerifyRectsDoNotOverlap(IReadOnlyList<RectTransform> rects, string label)
        {
            for (int i = 0; i < rects.Count; i++)
            {
                for (int j = i + 1; j < rects.Count; j++)
                {
                    if (rects[i] == null || rects[j] == null)
                        continue;

                    if (RectsOverlap(rects[i], rects[j]))
                        Fail($"{label}: `{rects[i].name}` overlaps `{rects[j].name}`.");
                }
            }
        }

        static bool RectsOverlap(RectTransform left, RectTransform right)
        {
            if (left == null || right == null || !left.gameObject.activeInHierarchy || !right.gameObject.activeInHierarchy)
                return false;

            var leftRect = ToScreenRect(left);
            var rightRect = ToScreenRect(right);
            if (leftRect.width <= 1f || leftRect.height <= 1f || rightRect.width <= 1f || rightRect.height <= 1f)
                return false;

            return leftRect.Overlaps(rightRect);
        }

        static Rect ToScreenRect(RectTransform rectTransform)
        {
            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            return new Rect(
                corners[0].x,
                corners[0].y,
                corners[2].x - corners[0].x,
                corners[2].y - corners[0].y);
        }

        static void AddRect(List<RectTransform> rects, Component component)
        {
            if (component == null)
                return;

            if (component.transform is RectTransform rectTransform)
                rects.Add(rectTransform);
        }

        static T GetField<T>(object target, string fieldName) where T : class
        {
            if (target == null)
                return null;

            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(target) as T;
        }

        static TValue GetValueField<TValue>(object target, string fieldName)
        {
            if (target == null)
                return default;

            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                return default;

            object value = field.GetValue(target);
            return value is TValue typed ? typed : default;
        }

        static void Require(bool condition, string message)
        {
            if (condition)
                Append($"PASS: {message}");
            else
                Fail(message);
        }

        static void Warn(string message)
        {
            warningCount++;
            Append($"WARN: {message}");
        }

        static void Fail(string message)
        {
            failureCount++;
            Append($"FAIL: {message}");
        }

        static void Append(string line)
        {
            File.AppendAllText(LogPath, $"- {line}{Environment.NewLine}");
            Debug.Log($"[PresentationPolishRuntimeVerifier] {line}");
        }

        static void Wait(double seconds) => nextStepAt = EditorApplication.timeSinceStartup + seconds;

        static void Finish()
        {
            if (finalizeRequested)
                return;

            finalizeRequested = true;
            Append("Exiting Play Mode.");
            EditorApplication.ExitPlaymode();
        }

        sealed class CaseScenario
        {
            public string CaseId;
            public CaseScenarioMode Mode;
            public int Phase;
            public int GuardSteps;

            public static CaseScenario ForSecondVisit(string caseId) => new()
            {
                CaseId = caseId,
                Mode = CaseScenarioMode.SecondVisit,
                Phase = 0,
                GuardSteps = 0
            };

            public static CaseScenario ForPilot(string caseId) => new()
            {
                CaseId = caseId,
                Mode = CaseScenarioMode.Pilot,
                Phase = 0,
                GuardSteps = 0
            };
        }

        enum CaseScenarioMode
        {
            SecondVisit,
            Pilot
        }
    }
}
#endif
