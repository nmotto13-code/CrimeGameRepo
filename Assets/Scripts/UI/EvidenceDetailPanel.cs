using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CasebookGame.Data;

namespace CasebookGame.UI
{
    /// <summary>
    /// Full-screen evidence detail popup. Contains the EvidenceViewer for pinch/zoom/pan.
    /// </summary>
    public class EvidenceDetailPanel : MonoBehaviour
    {
        public static EvidenceDetailPanel Instance { get; private set; }

        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text descriptionText;
        [SerializeField] TMP_Text tagsText;
        [SerializeField] Button closeButton;
        [SerializeField] Button enhanceButton;
        [SerializeField] Viewers.EvidenceViewer viewer;

        EvidenceData currentEvidence;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            closeButton?.onClick.AddListener(Hide);
            enhanceButton?.onClick.AddListener(OnEnhanceTapped);
            panel?.SetActive(false);
        }

        public void Show(EvidenceData evidence)
        {
            currentEvidence = evidence;
            if (titleText) titleText.text = evidence.displayName;
            if (descriptionText) descriptionText.text = evidence.descriptionText;
            RefreshTags(evidence);
            viewer?.Load(evidence);

            bool canEnhance = evidence.enhanceOverlayMaskSprite != null
                           && !evidence.isEnhanced
                           && Tools.ToolsController.Instance != null
                           && Tools.ToolsController.Instance.CanEnhance();
            if (enhanceButton) enhanceButton.gameObject.SetActive(canEnhance);

            panel?.SetActive(true);
        }

        void Hide()
        {
            panel?.SetActive(false);
            currentEvidence = null;
        }

        void OnEnhanceTapped()
        {
            if (currentEvidence == null) return;
            Tools.ToolsController.Instance?.ApplyEnhance(currentEvidence);
            RefreshTags(currentEvidence);
            viewer?.ShowEnhanceOverlay(currentEvidence.enhanceOverlayMaskSprite);
            if (enhanceButton) enhanceButton.gameObject.SetActive(false);

            // Refresh the list card too
            var cards = FindObjectsOfType<EvidenceCardUI>();
            foreach (var c in cards)
                if (c.Data == currentEvidence) c.RefreshTags();
        }

        void RefreshTags(EvidenceData e)
        {
            if (tagsText) tagsText.text = string.Join("  ·  ", e.runtimeTags);
        }
    }
}
