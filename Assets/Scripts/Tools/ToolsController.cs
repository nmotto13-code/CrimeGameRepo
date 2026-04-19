using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CasebookGame.Data;
using CasebookGame.UI;

namespace CasebookGame.Tools
{
    /// <summary>
    /// Manages all three tools: Cross-Check, Enhance, Timeline Snap.
    /// Tracks charges / cooldowns and drives UI button state.
    /// </summary>
    public class ToolsController : MonoBehaviour
    {
        public static ToolsController Instance { get; private set; }

        [Header("Cross-Check Tool")]
        [SerializeField] Button crossCheckButton;
        [SerializeField] TMP_Text crossCheckChargesText;
        int crossCheckCharges;

        [Header("Enhance Tool")]
        [SerializeField] Button enhanceButton;
        [SerializeField] TMP_Text enhanceCooldownText;
        float enhanceCooldownSeconds;
        float enhanceCooldownRemaining = 0f;
        bool enhanceOnCooldown = false;

        [Header("Timeline Snap Tool")]
        [SerializeField] Button timelineSnapButton;
        [SerializeField] TMP_Text timelineSnapChargesText;
        [SerializeField] GameObject timelineSnapResultPopup;
        [SerializeField] TMP_Text timelineSnapResultText;
        int timelineSnapCharges;

        [Header("Visual Feedback")]
        [SerializeField] Color chargesAvailableColor = Color.white;
        [SerializeField] Color chargesEmptyColor = new Color(0.4f, 0.4f, 0.4f);

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            crossCheckButton?.onClick.AddListener(OnCrossCheckPressed);
            enhanceButton?.onClick.AddListener(OnEnhancePressed);
            timelineSnapButton?.onClick.AddListener(OnTimelineSnapPressed);
        }

        public void InitializeTools(ToolConfig config)
        {
            crossCheckCharges = config.crossCheckCharges;
            enhanceCooldownSeconds = config.enhanceCooldownSeconds;
            timelineSnapCharges = config.timelineSnapCharges;
            enhanceCooldownRemaining = 0f;
            enhanceOnCooldown = false;

            RefreshAllUI();
        }

        void Update()
        {
            if (enhanceOnCooldown)
            {
                enhanceCooldownRemaining -= Time.deltaTime;
                if (enhanceCooldownRemaining <= 0f)
                {
                    enhanceCooldownRemaining = 0f;
                    enhanceOnCooldown = false;
                }
                UpdateEnhanceUI();
            }
        }

        // ── Tool A: Cross-Check ────────────────────────────────────────

        void OnCrossCheckPressed()
        {
            // No claim context here — requires a claim to be selected via ClaimCardUI
            Debug.Log("[ToolsController] Cross-Check pressed. Long-press a claim card to use it in context.");
        }

        public void ApplyCrossCheck(ClaimData claim)
        {
            if (crossCheckCharges <= 0) return;
            crossCheckCharges--;
            RefreshCrossCheckUI();

            // Find all evidence cards and highlight those matching claim's referenced tags
            int highlighted = 0;
            var cards = FindObjectsOfType<EvidenceCardUI>();
            foreach (var card in cards)
            {
                bool match = false;
                if (card.Data != null)
                    foreach (var tag in claim.referencedTags)
                        if (card.Data.HasTag(tag)) { match = true; break; }
                card.SetHighlight(match);
                if (match) highlighted++;
            }

            // Show "Potential Conflict" marker if any highlighted (simple log for now; extend with popup)
            if (highlighted > 0)
                Debug.Log($"[CrossCheck] {highlighted} evidence items potentially conflict with '{claim.speakerName}'s claim.");
        }

        // ── Tool B: Enhance ────────────────────────────────────────────

        void OnEnhancePressed()
        {
            // Pressing toolbar button triggers enhance on currently open detail panel evidence
            UI.EvidenceDetailPanel.Instance?.gameObject.SendMessage("OnEnhanceTapped", SendMessageOptions.DontRequireReceiver);
        }

        public bool CanEnhance() => !enhanceOnCooldown;

        public void ApplyEnhance(EvidenceData evidence)
        {
            if (enhanceOnCooldown) return;
            evidence.ApplyEnhance();
            enhanceOnCooldown = true;
            enhanceCooldownRemaining = enhanceCooldownSeconds;
            UpdateEnhanceUI();
        }

        // ── Tool C: Timeline Snap ──────────────────────────────────────

        void OnTimelineSnapPressed()
        {
            if (timelineSnapCharges <= 0) return;

            var timePinned = UI.BoardController.Instance?.GetTimeTaggedPinned();
            if (timePinned == null || timePinned.Count < 2)
            {
                ShowTimelineResult("Pin at least 2 TIME-tagged evidence items to the Board first.");
                return;
            }

            timelineSnapCharges--;
            RefreshTimelineSnapUI();

            // Simple heuristic: check description texts for conflicting time strings
            var a = timePinned[0];
            var b = timePinned[1];
            bool conflict = DetectTimeConflict(a, b);
            string msg = conflict
                ? $"CONFLICT DETECTED between \"{a.displayName}\" and \"{b.displayName}\".\nThese time windows cannot both be true."
                : $"No impossible overlap found between \"{a.displayName}\" and \"{b.displayName}\".";
            ShowTimelineResult(msg);
        }

        bool DetectTimeConflict(EvidenceData a, EvidenceData b)
        {
            // Data-driven: if both are TIME-tagged, flag as conflict (designer sets up cases so this is meaningful)
            // In a full implementation, parse actual time values from evidence metadata
            var gm = GameManager.Instance;
            if (gm?.CurrentCase == null) return false;
            // Conflict exists if the solution references either of these two evidence items
            var sol = gm.CurrentCase;
            return (a.evidenceId == sol.primaryEvidenceIdA || a.evidenceId == sol.primaryEvidenceIdB)
                && (b.evidenceId == sol.primaryEvidenceIdA || b.evidenceId == sol.primaryEvidenceIdB);
        }

        void ShowTimelineResult(string msg)
        {
            if (timelineSnapResultText) timelineSnapResultText.text = msg;
            if (timelineSnapResultPopup)
            {
                timelineSnapResultPopup.SetActive(true);
                StartCoroutine(HideTimelineResultAfter(4f));
            }
            else
            {
                Debug.Log($"[TimelineSnap] {msg}");
            }
        }

        IEnumerator HideTimelineResultAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            timelineSnapResultPopup?.SetActive(false);
        }

        // ── UI Refresh ─────────────────────────────────────────────────

        void RefreshAllUI()
        {
            RefreshCrossCheckUI();
            UpdateEnhanceUI();
            RefreshTimelineSnapUI();
        }

        void RefreshCrossCheckUI()
        {
            if (crossCheckChargesText) crossCheckChargesText.text = $"{crossCheckCharges}";
            UpdateButtonColor(crossCheckButton, crossCheckCharges > 0);
        }

        void UpdateEnhanceUI()
        {
            if (enhanceCooldownText)
                enhanceCooldownText.text = enhanceOnCooldown
                    ? $"{Mathf.CeilToInt(enhanceCooldownRemaining)}s"
                    : "READY";
            UpdateButtonColor(enhanceButton, !enhanceOnCooldown);
        }

        void RefreshTimelineSnapUI()
        {
            if (timelineSnapChargesText) timelineSnapChargesText.text = $"{timelineSnapCharges}";
            UpdateButtonColor(timelineSnapButton, timelineSnapCharges > 0);
        }

        void UpdateButtonColor(Button btn, bool available)
        {
            if (!btn) return;
            var img = btn.GetComponent<Image>();
            if (img) img.color = available ? chargesAvailableColor : chargesEmptyColor;
            btn.interactable = available;
        }
    }
}
