using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using CasebookGame.Data;

namespace CasebookGame.UI
{
    public class FoundEvidenceCard : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] Image    thumbnail;
        [SerializeField] TMP_Text nameLabel;

        static readonly Color NormalBg        = new Color(0.14f, 0.14f, 0.22f);
        static readonly Color HighlightBg     = new Color(0.20f, 0.55f, 1.00f, 0.55f);
        static readonly Color PinnedBg        = new Color(0.15f, 0.55f, 0.30f, 0.60f);

        EvidenceData evidence;
        Image bgImage;

        public EvidenceData Evidence => evidence;

        public void Initialize(EvidenceData e)
        {
            evidence = e;
            bgImage  = GetComponent<Image>();

            if (thumbnail)
            {
                thumbnail.sprite         = e.imageSprite;
                thumbnail.color          = e.imageSprite ? Color.white : new Color(0.3f, 0.3f, 0.4f);
                thumbnail.preserveAspect = true;
            }
            if (nameLabel) nameLabel.text = e.displayName;
            RefreshPinnedTint();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            EvidenceDetailPanel.Instance?.Show(evidence);
        }

        public void SetHighlight(bool on)
        {
            if (bgImage) bgImage.color = on ? HighlightBg : NormalBg;
        }

        public void RefreshPinnedTint()
        {
            if (bgImage == null) return;
            bool pinned = BoardController.Instance?.IsPinned(evidence) ?? false;
            bgImage.color = pinned ? PinnedBg : NormalBg;
        }
    }
}
