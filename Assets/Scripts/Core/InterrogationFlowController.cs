using System;
using System.Collections;
using System.Collections.Generic;
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

        Action completionCallback;
        int currentNodeIndex;
        bool transitionLocked;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            for (int i = 0; i < responseButtons.Length; i++)
            {
                int capturedIndex = i;
                responseButtons[i]?.onClick.AddListener(() => OnResponseSelected(capturedIndex));
            }

            if (panel != null)
                panel.SetActive(false);
        }

        public bool TryBegin(CaseData caseData, Action onComplete)
        {
            if (caseData == null || caseData.interrogationNodes == null || caseData.interrogationNodes.Count == 0)
                return false;

            activeNodes.Clear();
            foreach (var node in caseData.interrogationNodes)
            {
                if (node != null && IsNodeUnlocked(node))
                    activeNodes.Add(node);
            }

            if (activeNodes.Count == 0)
                return false;

            completionCallback = onComplete;
            currentNodeIndex = 0;
            transitionLocked = false;

            if (panel != null)
                panel.SetActive(true);

            ShowCurrentNode();
            return true;
        }

        bool IsNodeUnlocked(InterrogationNode node)
        {
            var discovery = EvidenceDiscoverySystem.Instance;
            var currentCase = GameManager.Instance?.CurrentCase;

            foreach (var evidenceId in node.evidenceRequiredIds)
            {
                if (string.IsNullOrWhiteSpace(evidenceId))
                    continue;

                if (!IsEvidenceRequirementMet(evidenceId, currentCase, discovery))
                    return false;
            }

            foreach (var tag in node.unlockConditionTags)
            {
                if (discovery == null || !discovery.HasFoundTag(tag))
                    return false;
            }

            return true;
        }

        static bool IsEvidenceRequirementMet(string evidenceId, CaseData currentCase, EvidenceDiscoverySystem discovery)
        {
            if (string.IsNullOrWhiteSpace(evidenceId))
                return true;

            if (currentCase?.evidence != null)
            {
                foreach (var evidence in currentCase.evidence)
                {
                    if (evidence != null && evidence.evidenceId == evidenceId)
                        return discovery != null && discovery.HasFoundEvidence(evidenceId);
                }
            }

            string relatedCaseId = ExtractCaseId(evidenceId);
            return !string.IsNullOrWhiteSpace(relatedCaseId) && PlayerProfile.HasSolvedCase(relatedCaseId);
        }

        static string ExtractCaseId(string evidenceId)
        {
            int separatorIndex = evidenceId.IndexOf("_E", StringComparison.OrdinalIgnoreCase);
            return separatorIndex > 0 ? evidenceId[..separatorIndex] : string.Empty;
        }

        void ShowCurrentNode()
        {
            if (currentNodeIndex < 0 || currentNodeIndex >= activeNodes.Count)
            {
                Finish();
                return;
            }

            var node = activeNodes[currentNodeIndex];
            if (titleText != null)
                titleText.text = "INTERROGATION";
            if (progressText != null)
                progressText.text = $"QUESTION {currentNodeIndex + 1}/{activeNodes.Count}";
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
                SetButtonColor(button, neutralButtonColor);

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

            var node = activeNodes[currentNodeIndex];
            bool isCorrect = responseIndex == node.correctResponseIndex;

            for (int i = 0; i < responseButtons.Length; i++)
            {
                var button = responseButtons[i];
                if (button == null)
                    continue;

                button.interactable = false;

                if (i == node.correctResponseIndex)
                    SetButtonColor(button, correctButtonColor);
                else if (i == responseIndex)
                    SetButtonColor(button, incorrectButtonColor);
                else
                    SetButtonColor(button, neutralButtonColor);
            }

            if (feedbackText != null)
            {
                feedbackText.text = isCorrect
                    ? "That pressure point lands. Move to the next angle."
                    : "They absorb it. Press from a different angle.";
                feedbackText.color = isCorrect ? feedbackCorrectColor : feedbackIncorrectColor;
            }

            yield return new WaitForSecondsRealtime(feedbackDurationSeconds);

            currentNodeIndex++;
            transitionLocked = false;
            ShowCurrentNode();
        }

        void Finish()
        {
            if (panel != null)
                panel.SetActive(false);

            activeNodes.Clear();
            currentNodeIndex = 0;
            transitionLocked = false;

            var callback = completionCallback;
            completionCallback = null;
            callback?.Invoke();
        }

        static void SetButtonColor(Button button, Color color)
        {
            if (button?.targetGraphic is Graphic graphic)
                graphic.color = color;
        }
    }
}
