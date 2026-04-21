using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CasebookGame.Data;
using CasebookGame.UI;

namespace CasebookGame.Tools
{
    public class ToolsController : MonoBehaviour
    {
        public static ToolsController Instance { get; private set; }

        [Header("Cross-Check Tool")]
        [SerializeField] Button crossCheckButton;
        [SerializeField] TMP_Text crossCheckChargesText;
        int crossCheckCharges;
        bool crossCheckModeActive;
        public bool IsCrossCheckModeActive => crossCheckModeActive;

        [Header("Enhance Tool")]
        [SerializeField] Button enhanceButton;
        [SerializeField] TMP_Text enhanceCooldownText;
        float enhanceCooldownSeconds;
        float enhanceCooldownRemaining;
        bool enhanceOnCooldown;

        [Header("Timeline Snap Tool")]
        [SerializeField] Button timelineSnapButton;
        [SerializeField] TMP_Text timelineSnapChargesText;
        [SerializeField] GameObject timelineSnapResultPopup;
        [SerializeField] TMP_Text timelineSnapResultText;
        int timelineSnapCharges;

        static readonly Color ActiveModeColor = new Color(1f, 0.90f, 0.15f, 1f);

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
            crossCheckCharges      = config.crossCheckCharges;
            enhanceCooldownSeconds = config.enhanceCooldownSeconds;
            timelineSnapCharges    = config.timelineSnapCharges;
            enhanceCooldownRemaining = 0f;
            enhanceOnCooldown      = false;
            crossCheckModeActive   = false;
            RefreshAllUI();
        }

        void Update()
        {
            if (!enhanceOnCooldown) return;
            enhanceCooldownRemaining -= Time.deltaTime;
            if (enhanceCooldownRemaining <= 0f)
            {
                enhanceCooldownRemaining = 0f;
                enhanceOnCooldown = false;
            }
            UpdateEnhanceUI();
        }

        // ── Cross-Check ────────────────────────────────────────────────

        void OnCrossCheckPressed()
        {
            if (crossCheckCharges <= 0) return;

            crossCheckModeActive = !crossCheckModeActive;

            // Switch to Solve tab so the player can tap a claim
            if (crossCheckModeActive)
                TabController.Instance?.SwitchToTab(3);

            RefreshCrossCheckUI();
        }

        // Called by ClaimCardUI when cross-check mode is active and a claim is tapped.
        public void ApplyCrossCheck(ClaimData claim)
        {
            if (crossCheckCharges <= 0) return;
            crossCheckModeActive = false;
            crossCheckCharges--;
            RefreshCrossCheckUI();

            // Highlight Evidence-tab cards that share tags with this claim
            int highlighted = 0;
            var evidenceCards = FindObjectsByType<UI.EvidenceCardUI>(FindObjectsSortMode.None);
            foreach (var card in evidenceCards)
            {
                bool match = false;
                if (card.Data != null)
                    foreach (var tag in claim.referencedTags)
                        if (card.Data.HasTag(tag)) { match = true; break; }
                card.SetHighlight(match);
                if (match) highlighted++;
            }

            // Switch to Evidence tab so the player sees the highlighted cards
            if (highlighted > 0)
                UI.TabController.Instance?.SwitchToTab(2);

            Debug.Log($"[CrossCheck] {highlighted} found evidence items linked to '{claim.speakerName}'.");
        }

        public void CancelCrossCheckMode()
        {
            crossCheckModeActive = false;
            RefreshCrossCheckUI();
        }

        // ── Enhance ────────────────────────────────────────────────────

        void OnEnhancePressed()
        {
            var panel = EvidenceDetailPanel.Instance;
            if (panel == null || panel.CurrentEvidence == null)
            {
                Debug.Log("[Enhance] Open an evidence item in the Scene tab first.");
                return;
            }
            panel.TriggerEnhance();
        }

        public bool CanEnhance() => !enhanceOnCooldown;

        public void ApplyEnhance(EvidenceData evidence)
        {
            if (enhanceOnCooldown) return;
            evidence.ApplyEnhance();
            enhanceOnCooldown        = true;
            enhanceCooldownRemaining = enhanceCooldownSeconds;
            UpdateEnhanceUI();
        }

        // ── Timeline Snap ──────────────────────────────────────────────

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
            var sol = Core.GameManager.Instance?.CurrentCase;
            if (sol == null) return false;
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
            if (crossCheckButton)
            {
                var img = crossCheckButton.GetComponent<Image>();
                if (img)
                {
                    if (crossCheckModeActive)
                        img.color = ActiveModeColor;
                    else
                    {
                        var c = img.color;
                        c.a = crossCheckCharges > 0 ? 1f : 0.35f;
                        img.color = c;
                    }
                }
                crossCheckButton.interactable = crossCheckCharges > 0;
            }
        }

        void UpdateEnhanceUI()
        {
            if (enhanceCooldownText)
                enhanceCooldownText.text = enhanceOnCooldown
                    ? $"{Mathf.CeilToInt(enhanceCooldownRemaining)}s"
                    : "READY";
            SetButtonAlpha(enhanceButton, !enhanceOnCooldown);
            if (enhanceButton) enhanceButton.interactable = !enhanceOnCooldown;
        }

        void RefreshTimelineSnapUI()
        {
            if (timelineSnapChargesText) timelineSnapChargesText.text = $"{timelineSnapCharges}";
            SetButtonAlpha(timelineSnapButton, timelineSnapCharges > 0);
            if (timelineSnapButton) timelineSnapButton.interactable = timelineSnapCharges > 0;
        }

        static void SetButtonAlpha(Button btn, bool available)
        {
            if (!btn) return;
            var img = btn.GetComponent<Image>();
            if (!img) return;
            var c = img.color;
            c.a = available ? 1f : 0.35f;
            img.color = c;
        }
    }
}
