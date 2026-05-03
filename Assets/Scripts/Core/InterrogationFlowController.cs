using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CasebookGame.Data;

namespace CasebookGame.Core
{
    public class InterrogationFlowController : MonoBehaviour
    {
        public static InterrogationFlowController Instance { get; private set; }

        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text progressText;
        [SerializeField] TMP_Text promptText;
        [SerializeField] TMP_Text feedbackText;
        [SerializeField] Button triggerButton;
        [SerializeField] Button[] responseButtons = new Button[0];
        [SerializeField] TMP_Text[] responseLabels = new TMP_Text[0];
        [SerializeField] float feedbackDurationSeconds = 0.75f;

        [SerializeField] Color neutralButtonColor = new Color(0.18f, 0.22f, 0.32f);
        [SerializeField] Color correctButtonColor = new Color(0.20f, 0.60f, 0.36f);
        [SerializeField] Color incorrectButtonColor = new Color(0.62f, 0.22f, 0.24f);
        [SerializeField] Color feedbackNeutralColor = new Color(0.88f, 0.88f, 0.92f);
        [SerializeField] Color feedbackCorrectColor = new Color(0.64f, 0.88f, 0.66f);
        [SerializeField] Color feedbackIncorrectColor = new Color(0.92f, 0.62f, 0.62f);

        readonly List<InterrogationNode> activeNodes = new();
        readonly Dictionary<string, InterrogationNode> nodeLookup = new();
        readonly HashSet<string> completedNodeIds = new();
        bool polishApplied;

        CaseData preparedCase;
        Action completionCallback;
        int currentNodeIndex;
        bool transitionLocked;
        InterrogationNode currentNode;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            ApplyPolish();

            for (int i = 0; i < responseButtons.Length; i++)
            {
                int capturedIndex = i;
                responseButtons[i]?.onClick.AddListener(() => OnResponseSelected(capturedIndex));
            }

            triggerButton?.onClick.AddListener(() =>
            {
                if (preparedCase != null)
                    TryBegin(preparedCase, null, null);
            });

            if (panel != null)
                panel.SetActive(false);
        }

        public void PrepareCase(CaseData caseData)
        {
            preparedCase = caseData;
            RefreshAvailability();
        }

        public bool TryBegin(CaseData caseData, Action onComplete)
        {
            return TryBegin(caseData, null, onComplete);
        }

        public bool TryBegin(CaseData caseData, string preferredNodeId, Action onComplete)
        {
            if (caseData == null || caseData.interrogationNodes == null || caseData.interrogationNodes.Count == 0)
                return false;

            preparedCase = caseData;
            activeNodes.Clear();
            nodeLookup.Clear();
            completedNodeIds.Clear();
            foreach (var node in caseData.interrogationNodes)
            {
                if (node != null && IsNodeUnlocked(node))
                {
                    activeNodes.Add(node);
                    if (!string.IsNullOrWhiteSpace(node.nodeId))
                        nodeLookup[node.nodeId] = node;
                }
            }

            if (activeNodes.Count == 0)
                return false;

            if (!string.IsNullOrWhiteSpace(preferredNodeId))
            {
                currentNodeIndex = activeNodes.FindIndex(node =>
                    node != null && string.Equals(node.nodeId, preferredNodeId, StringComparison.OrdinalIgnoreCase));
                if (currentNodeIndex < 0)
                    return false;
            }
            else
            {
                currentNodeIndex = 0;
            }

            completionCallback = onComplete;
            currentNode = activeNodes[currentNodeIndex];
            transitionLocked = false;

            if (panel != null)
                panel.SetActive(true);

            ShowCurrentNode();
            return true;
        }

        public void RefreshAvailability()
        {
            if (triggerButton == null)
                return;

            bool hasAvailableNode = false;
            if (preparedCase != null && preparedCase.interrogationNodes != null)
            {
                foreach (var node in preparedCase.interrogationNodes)
                {
                    if (node != null && IsNodeUnlocked(node))
                    {
                        hasAvailableNode = true;
                        break;
                    }
                }
            }

            triggerButton.gameObject.SetActive(hasAvailableNode);
            triggerButton.interactable = hasAvailableNode && !transitionLocked;
        }

        public bool IsEntryNodeAvailable(string nodeId)
        {
            var caseData = preparedCase ?? GameManager.Instance?.CurrentCase;
            if (caseData?.interrogationNodes == null || string.IsNullOrWhiteSpace(nodeId))
                return false;

            foreach (var node in caseData.interrogationNodes)
            {
                if (node != null
                    && string.Equals(node.nodeId, nodeId, StringComparison.OrdinalIgnoreCase)
                    && IsNodeUnlocked(node))
                    return true;
            }

            return false;
        }

        bool IsNodeUnlocked(InterrogationNode node)
        {
            var discovery = EvidenceDiscoverySystem.Instance;
            var currentCase = GameManager.Instance?.CurrentCase;
            var currentLocation = GameManager.Instance?.CurrentLocation;

            if (!string.IsNullOrWhiteSpace(node?.nodeId)
                && GameManager.Instance != null
                && GameManager.Instance.HasCompletedInterrogationNode(node.nodeId))
                return false;

            if (!string.IsNullOrWhiteSpace(node.locationContextId)
                && !string.Equals(node.locationContextId, currentLocation?.locationId, StringComparison.OrdinalIgnoreCase))
                return false;

            foreach (var evidenceId in node.evidenceRequiredIds)
            {
                if (string.IsNullOrWhiteSpace(evidenceId))
                    continue;

                if (!CaseProgressionResolver.IsEvidenceRequirementMet(evidenceId, currentCase, discovery))
                    return false;
            }

            foreach (var tag in node.unlockConditionTags)
            {
                if (discovery == null || !discovery.HasFoundTag(tag))
                    return false;
            }

            return true;
        }

        void ShowCurrentNode()
        {
            ApplyPolish();

            if (currentNodeIndex < 0 || currentNodeIndex >= activeNodes.Count)
            {
                Finish();
                return;
            }

            var node = currentNode ?? activeNodes[currentNodeIndex];
            if (titleText != null)
                titleText.text = "INTERROGATION";
            if (progressText != null)
                progressText.text = $"QUESTION {completedNodeIds.Count + 1}/{activeNodes.Count}";
            if (promptText != null)
                promptText.text = node.promptText;
            if (feedbackText != null)
            {
                feedbackText.text = "Choose the line to push.";
                feedbackText.color = feedbackNeutralColor;
            }

            for (int i = 0; i < responseButtons.Length; i++)
            {
                var button = responseButtons[i];
                if (button == null)
                    continue;

                button.gameObject.SetActive(true);
                button.interactable = true;
                SetButtonVisual(button, neutralButtonColor, "Panels/interrogation_response_plate");

                if (i < responseLabels.Length && responseLabels[i] != null)
                    responseLabels[i].text = GetResponseText(node, i);
            }
        }

        static string GetResponseText(InterrogationNode node, int index)
        {
            if (node.responses != null && index >= 0 && index < node.responses.Count && !string.IsNullOrWhiteSpace(node.responses[index]))
                return node.responses[index];

            return "...";
        }

        void OnResponseSelected(int responseIndex)
        {
            if (transitionLocked || currentNodeIndex < 0 || currentNodeIndex >= activeNodes.Count)
                return;

            StartCoroutine(HandleResponseSelection(responseIndex));
        }

        IEnumerator HandleResponseSelection(int responseIndex)
        {
            transitionLocked = true;

            var node = currentNode ?? activeNodes[currentNodeIndex];
            bool isCorrect = responseIndex == node.correctResponseIndex;

            for (int i = 0; i < responseButtons.Length; i++)
            {
                var button = responseButtons[i];
                if (button == null)
                    continue;

                button.interactable = false;

                if (i == node.correctResponseIndex)
                    SetButtonVisual(button, correctButtonColor, "Panels/interrogation_response_success");
                else if (i == responseIndex)
                    SetButtonVisual(button, incorrectButtonColor, "Panels/interrogation_response_failure");
                else
                    SetButtonVisual(button, neutralButtonColor, "Panels/interrogation_response_plate");
            }

            if (feedbackText != null)
            {
                feedbackText.text = isCorrect
                    ? "That pressure point lands. Move to the next angle."
                    : "They absorb it. Press from a different angle.";
                feedbackText.color = isCorrect ? feedbackCorrectColor : feedbackIncorrectColor;
            }

            yield return new WaitForSecondsRealtime(feedbackDurationSeconds);

            ApplyRewards(node);
            GameManager.Instance?.RegisterInterrogationNodeCompleted(node.nodeId);
            GameManager.Instance?.ApplyInterrogationOutcome(isCorrect ? node.outcomeIdOnCorrect : node.outcomeIdOnWrong);
            completedNodeIds.Add(node.nodeId);
            AdvanceToNextNode(node, isCorrect);
            transitionLocked = false;
            ShowCurrentNode();
        }

        void AdvanceToNextNode(InterrogationNode node, bool isCorrect)
        {
            string nextNodeId = isCorrect ? node.nextNodeIdOnCorrect : node.nextNodeIdOnWrong;
            if (!string.IsNullOrWhiteSpace(nextNodeId)
                && nodeLookup.TryGetValue(nextNodeId, out var branchedNode)
                && !completedNodeIds.Contains(branchedNode.nodeId))
            {
                currentNode = branchedNode;
                currentNodeIndex = Mathf.Max(0, activeNodes.IndexOf(branchedNode));
                return;
            }

            currentNodeIndex++;
            while (currentNodeIndex < activeNodes.Count)
            {
                var candidate = activeNodes[currentNodeIndex];
                if (candidate != null && !completedNodeIds.Contains(candidate.nodeId))
                {
                    currentNode = candidate;
                    return;
                }

                currentNodeIndex++;
            }

            currentNode = null;
        }

        static void ApplyRewards(InterrogationNode node)
        {
            if (node == null)
                return;

            var discovery = EvidenceDiscoverySystem.Instance;
            if (discovery == null)
                return;

            foreach (var evidenceId in node.grantedEvidenceIds ?? Enumerable.Empty<string>())
                discovery.GrantEvidenceById(evidenceId);

            foreach (var tag in node.grantedTags ?? Enumerable.Empty<EvidenceTag>())
                discovery.GrantTag(tag);
        }

        void Finish()
        {
            if (panel != null)
                panel.SetActive(false);

            activeNodes.Clear();
            nodeLookup.Clear();
            completedNodeIds.Clear();
            currentNodeIndex = 0;
            currentNode = null;
            transitionLocked = false;
            RefreshAvailability();

            var callback = completionCallback;
            completionCallback = null;
            callback?.Invoke();
        }

        void ApplyPolish()
        {
            if (polishApplied)
                return;

            polishApplied = true;

            PresentationPolishCatalog.ApplyTextPlate(promptText, "Panels/interrogation_prompt_plate",
                new Color(0.92f, 0.94f, 0.98f), new Vector4(24f, 20f, 20f, 20f));
            PresentationPolishCatalog.ApplyTextPlate(feedbackText, "Panels/interrogation_feedback_plate",
                new Color(0.88f, 0.90f, 0.94f), new Vector4(24f, 18f, 18f, 18f));

            if (triggerButton?.targetGraphic is Image triggerImage)
                PresentationPolishCatalog.ApplySprite(triggerImage, "Panels/interrogation_trigger_plate", Color.white);

            PresentationPolishCatalog.EnsureChildImage(
                triggerButton != null ? triggerButton.transform : null,
                "TriggerBadge",
                "Interrogation/interrogation_ready_badge",
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(18f, 10f),
                new Vector2(68f, -10f),
                new Color(0.16f, 0.12f, 0.08f));

            for (int i = 0; i < responseButtons.Length; i++)
            {
                if (responseButtons[i]?.targetGraphic is Image image)
                    PresentationPolishCatalog.ApplySprite(image, "Panels/interrogation_response_plate", Color.white);

                if (i < responseLabels.Length && responseLabels[i] != null)
                {
                    responseLabels[i].margin = new Vector4(24f, 14f, 24f, 14f);
                    responseLabels[i].color = Color.white;
                }
            }
        }

        static void SetButtonVisual(Button button, Color color, string resourceKey)
        {
            if (button?.targetGraphic is Graphic graphic)
            {
                graphic.color = color;
                if (graphic is Image image)
                    PresentationPolishCatalog.ApplySprite(image, resourceKey, Color.white);
            }
        }
    }
}
