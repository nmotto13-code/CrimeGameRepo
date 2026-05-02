using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CasebookGame.Core;
using CasebookGame.Data;

namespace CasebookGame.UI
{
    public class CaseVisitFlowController : MonoBehaviour
    {
        [SerializeField] TMP_Text currentLocationText;
        [SerializeField] TMP_Text routeSummaryText;
        [SerializeField] TMP_Text suspectPresenceText;
        [SerializeField] TMP_Text solveGateText;
        [SerializeField] TMP_Text sceneHintText;
        [SerializeField] Transform visitButtonParent;
        [SerializeField] Button leadActionButton;
        [SerializeField] TMP_Text leadActionLabel;

        readonly Color currentColor = new Color(0.90f, 0.72f, 0.28f);
        readonly Color availableColor = new Color(0.22f, 0.52f, 0.78f);
        readonly Color completedColor = new Color(0.22f, 0.56f, 0.32f);
        readonly Color lockedColor = new Color(0.26f, 0.28f, 0.34f);
        readonly Color neutralColor = new Color(0.14f, 0.16f, 0.22f);
        readonly List<Button> visitButtons = new();

        void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        void Start()
        {
            Subscribe();
            Refresh();
        }

        void OnDisable() => Unsubscribe();

        void OnDestroy() => Unsubscribe();

        void Subscribe()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.CaseStateChanged -= Refresh;
            if (GameManager.Instance != null)
                GameManager.Instance.CaseStateChanged += Refresh;
        }

        void Unsubscribe()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.CaseStateChanged -= Refresh;
        }

        void Refresh()
        {
            var gameManager = GameManager.Instance;
            var caseData = gameManager?.CurrentCase;
            var currentLocation = gameManager?.CurrentLocation;
            if (caseData == null || currentLocation == null)
                return;

            if (currentLocationText != null)
                currentLocationText.text = currentLocation.displayName;

            if (routeSummaryText != null)
                routeSummaryText.text = BuildRouteSummary(gameManager, caseData);

            if (suspectPresenceText != null)
                suspectPresenceText.text = BuildSuspectSummary(gameManager, caseData);

            if (solveGateText != null)
                solveGateText.text = gameManager.GetSolveGateStatusText();

            if (sceneHintText != null)
                sceneHintText.text = gameManager.GetSceneHintText();

            RefreshVisitButtons(gameManager, caseData);
            RefreshLeadAction(gameManager, caseData);
        }

        void RefreshVisitButtons(GameManager gameManager, CaseData caseData)
        {
            if (visitButtonParent == null)
                return;

            foreach (Transform child in visitButtonParent)
                Destroy(child.gameObject);
            visitButtons.Clear();

            int locationCount = caseData.GetResolvedLocationCount();
            for (int index = 0; index < locationCount; index++)
            {
                var location = caseData.GetResolvedLocation(index);
                if (location == null)
                    continue;

                int capturedIndex = index;
                bool isCurrent = gameManager.CurrentLocationIndex == index;
                bool canVisit = gameManager.CanVisitLocationIndex(index);
                bool visited = gameManager.HasVisitedLocation(location.locationId);
                bool completed = gameManager.HasCompletedLocation(location.locationId);
                string status = gameManager.GetLocationVisitStatusText(index);

                var buttonGo = new GameObject($"Visit_{location.locationId}");
                buttonGo.transform.SetParent(visitButtonParent, false);
                buttonGo.AddComponent<RectTransform>();
                buttonGo.AddComponent<LayoutElement>().preferredHeight = 84;

                var background = buttonGo.AddComponent<Image>();
                background.color = ResolveVisitColor(isCurrent, canVisit, completed, visited);

                var button = buttonGo.AddComponent<Button>();
                button.targetGraphic = background;
                button.interactable = canVisit;
                button.onClick.AddListener(() =>
                {
                    gameManager.SetCurrentLocationIndex(capturedIndex);
                    TabController.Instance?.SwitchToTab(1);
                });

                var label = new GameObject("Label");
                label.transform.SetParent(buttonGo.transform, false);
                var labelText = label.AddComponent<TextMeshProUGUI>();
                labelText.alignment = TextAlignmentOptions.Center;
                labelText.textWrappingMode = TextWrappingModes.Normal;
                labelText.fontSize = 18;
                labelText.fontStyle = FontStyles.Bold;
                labelText.color = Color.white;
                labelText.text =
                    $"<size=22>{location.displayName}</size>\n<size=15>{status}</size>";
                var labelRT = labelText.rectTransform;
                labelRT.anchorMin = Vector2.zero;
                labelRT.anchorMax = Vector2.one;
                labelRT.offsetMin = new Vector2(12f, 8f);
                labelRT.offsetMax = new Vector2(-12f, -8f);

                visitButtons.Add(button);
            }
        }

        void RefreshLeadAction(GameManager gameManager, CaseData caseData)
        {
            if (leadActionButton == null)
                return;

            leadActionButton.onClick.RemoveAllListeners();

            var availablePresence = gameManager.GetCurrentLocationSuspectPresence()
                .FirstOrDefault(presence =>
                    presence != null
                    && !string.IsNullOrWhiteSpace(presence.interrogationEntryNodeId)
                    && InterrogationFlowController.Instance != null
                    && InterrogationFlowController.Instance.IsEntryNodeAvailable(presence.interrogationEntryNodeId));

            if (availablePresence != null)
            {
                leadActionButton.gameObject.SetActive(true);
                if (leadActionLabel != null)
                    leadActionLabel.text = $"QUESTION {gameManager.GetSuspectDisplayName(availablePresence.suspectId).ToUpperInvariant()}";

                leadActionButton.onClick.AddListener(() =>
                    InterrogationFlowController.Instance?.TryBegin(caseData, availablePresence.interrogationEntryNodeId, null));
                return;
            }

            if (caseData.HasSuspectPresentation())
            {
                leadActionButton.gameObject.SetActive(true);
                if (leadActionLabel != null)
                    leadActionLabel.text = "OPEN DOSSIER";

                leadActionButton.onClick.AddListener(() =>
                    NavigationManager.Instance?.Push(ScreenId.Dossier, TransitionType.SlideLeft));
                return;
            }

            leadActionButton.gameObject.SetActive(false);
        }

        static string BuildRouteSummary(GameManager gameManager, CaseData caseData)
        {
            int totalLocations = caseData.GetResolvedLocationCount();
            int visitedCount = 0;
            int completedCount = 0;
            int openCount = 0;

            for (int index = 0; index < totalLocations; index++)
            {
                var location = caseData.GetResolvedLocation(index);
                if (location == null)
                    continue;

                if (gameManager.HasVisitedLocation(location.locationId))
                    visitedCount++;
                if (gameManager.HasCompletedLocation(location.locationId))
                    completedCount++;
                if (gameManager.CanVisitLocationIndex(index))
                    openCount++;
            }

            if (caseData.visitFlowMode == CaseVisitFlowMode.LegacyFallback && totalLocations <= 1)
                return "Single-scene case file. Follow the contradiction loop and use the dossier when suspect pressure appears.";

            return $"Visit {gameManager.CurrentLocationIndex + 1}/{totalLocations} | {openCount} open | {visitedCount} visited | {completedCount} cleared";
        }

        static string BuildSuspectSummary(GameManager gameManager, CaseData caseData)
        {
            var visiblePresence = gameManager.GetCurrentLocationSuspectPresence();
            if (visiblePresence.Count == 0)
            {
                return caseData.HasSuspectPresentation()
                    ? "No suspect is actively surfaced at this visit. Open the dossier for broader case context."
                    : "No suspect presence authored for this location.";
            }

            var lines = new List<string>();
            foreach (var presence in visiblePresence)
            {
                if (presence == null)
                    continue;

                string name = gameManager.GetSuspectDisplayName(presence.suspectId);
                string label = string.IsNullOrWhiteSpace(presence.presenceLabel)
                    ? "Present at this visit"
                    : presence.presenceLabel;
                bool interrogationReady =
                    !string.IsNullOrWhiteSpace(presence.interrogationEntryNodeId)
                    && InterrogationFlowController.Instance != null
                    && InterrogationFlowController.Instance.IsEntryNodeAvailable(presence.interrogationEntryNodeId);
                string suffix = interrogationReady ? " Questioning ready." : string.Empty;
                lines.Add($"- {name} - {label}.{suffix}");
            }

            return string.Join("\n", lines);
        }

        Color ResolveVisitColor(bool isCurrent, bool canVisit, bool completed, bool visited)
        {
            if (isCurrent)
                return currentColor;
            if (!canVisit)
                return lockedColor;
            if (completed)
                return completedColor;
            if (visited)
                return neutralColor;
            return availableColor;
        }
    }
}
