using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CasebookGame.Data;

namespace CasebookGame.UI
{
    public class EvidenceDetailPanel : MonoBehaviour
    {
        public static EvidenceDetailPanel Instance { get; private set; }

        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text descriptionText;
        [SerializeField] TMP_Text tagsText;
        [SerializeField] Button closeButton;
        [SerializeField] Button enhanceButton;
        [SerializeField] Button pinButton;
        [SerializeField] TMP_Text pinButtonLabel;
        [SerializeField] Viewers.EvidenceViewer viewer;

        [Header("Text Mode (Document / Terminal / Keycard)")]
        [SerializeField] GameObject textModeRoot;
        [SerializeField] RawImage   textModeBg;
        [SerializeField] TMP_Text   textModeBody;

        EvidenceData currentEvidence;
        public EvidenceData CurrentEvidence => currentEvidence;
        public bool IsOpen => panel != null && panel.activeSelf;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            closeButton?.onClick.AddListener(Hide);
            enhanceButton?.onClick.AddListener(OnEnhanceTapped);
            pinButton?.onClick.AddListener(OnPinTapped);
            panel?.SetActive(false);
        }

        public void Show(EvidenceData evidence)
        {
            currentEvidence = evidence;
            if (titleText)       titleText.text       = evidence.displayName;
            if (descriptionText) descriptionText.text  = evidence.descriptionText;
            RefreshTags(evidence);

            bool isTextMode = evidence.displayMode != EvidenceDisplayMode.Default;
            if (viewer)          viewer.gameObject.SetActive(!isTextMode);
            if (textModeRoot)    textModeRoot.SetActive(isTextMode);
            // Plain description only shown for Default-mode evidence; text-mode uses the overlay
            if (descriptionText) descriptionText.gameObject.SetActive(!isTextMode);

            if (isTextMode)
                ApplyTextMode(evidence);
            else
                viewer?.Load(evidence);

            bool canEnhance = !isTextMode
                           && evidence.enhanceOverlayMaskSprite != null
                           && !evidence.isEnhanced
                           && Tools.ToolsController.Instance != null
                           && Tools.ToolsController.Instance.CanEnhance();
            if (enhanceButton) enhanceButton.gameObject.SetActive(canEnhance);

            RefreshPinButton(evidence);

            panel?.SetActive(true);
        }

        void ApplyTextMode(EvidenceData evidence)
        {
            if (textModeBg && evidence.imageSprite)
                textModeBg.texture = evidence.imageSprite.texture;

            if (!textModeBody) return;
            textModeBody.text = evidence.descriptionText;
            textModeBody.textWrappingMode = TextWrappingModes.Normal;
            textModeBody.alignment        = TextAlignmentOptions.TopLeft;

            switch (evidence.displayMode)
            {
                case EvidenceDisplayMode.Document:
                    textModeBody.color    = new Color(0.18f, 0.11f, 0.04f);
                    textModeBody.fontSize = 22;
                    break;
                case EvidenceDisplayMode.Terminal:
                    textModeBody.color    = new Color(0.18f, 0.95f, 0.28f);
                    textModeBody.fontSize = 21;
                    break;
                case EvidenceDisplayMode.Keycard:
                    textModeBody.color    = new Color(0.08f, 0.18f, 0.52f);
                    textModeBody.fontSize = 20;
                    break;
            }
        }

        public void Hide()
        {
            panel?.SetActive(false);
            currentEvidence = null;
        }

        public void TriggerEnhance() => OnEnhanceTapped();

        void OnEnhanceTapped()
        {
            if (currentEvidence == null) return;
            Tools.ToolsController.Instance?.ApplyEnhance(currentEvidence);
            RefreshTags(currentEvidence);
            viewer?.ShowEnhanceOverlay(currentEvidence.enhanceOverlayMaskSprite);
            if (enhanceButton) enhanceButton.gameObject.SetActive(false);

            var cards = FindObjectsByType<EvidenceCardUI>(FindObjectsSortMode.None);
            foreach (var c in cards)
                if (c.Data == currentEvidence) c.RefreshTags();
        }

        void OnPinTapped()
        {
            if (currentEvidence == null) return;
            bool pinned = BoardController.Instance?.TryPinToFirstFreeSlot(currentEvidence) ?? false;
            if (pinned)
            {
                RefreshPinButton(currentEvidence);
                // Refresh the found-strip card tint
                var foundCards = FindObjectsByType<FoundEvidenceCard>(FindObjectsSortMode.None);
                foreach (var c in foundCards)
                    if (c.Evidence == currentEvidence) c.RefreshPinnedTint();
                // Switch to Board tab so the player sees it was added
                TabController.Instance?.SwitchToTab(3);
                Hide();
            }
        }

        void RefreshPinButton(EvidenceData e)
        {
            if (!pinButton) return;
            bool alreadyPinned = BoardController.Instance?.IsPinned(e) ?? false;
            bool hasSlot       = BoardController.Instance?.HasFreeSlot ?? false;

            pinButton.gameObject.SetActive(true);
            pinButton.interactable = !alreadyPinned && hasSlot;

            if (pinButtonLabel)
                pinButtonLabel.text = alreadyPinned ? "PINNED" : hasSlot ? "PIN TO BOARD" : "BOARD FULL";
        }

        void RefreshTags(EvidenceData e)
        {
            if (tagsText) tagsText.text = string.Join("  ·  ", e.runtimeTags);
        }
    }
}
