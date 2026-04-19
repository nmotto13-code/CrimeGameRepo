using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CasebookGame.Data;

namespace CasebookGame.UI
{
    public class BoardSlotUI : MonoBehaviour
    {
        [SerializeField] Image thumbnailImage;
        [SerializeField] TMP_Text labelText;
        [SerializeField] Button removeButton;
        [SerializeField] GameObject emptyIndicator;

        public EvidenceData PinnedEvidence { get; private set; }

        void Awake()
        {
            removeButton?.onClick.AddListener(Clear);
            ShowEmpty();
        }

        public bool TryPin(EvidenceData evidence)
        {
            if (PinnedEvidence != null) return false;
            PinnedEvidence = evidence;
            if (thumbnailImage) { thumbnailImage.sprite = evidence.imageSprite; thumbnailImage.enabled = true; }
            if (labelText) labelText.text = evidence.displayName;
            if (emptyIndicator) emptyIndicator.SetActive(false);
            if (removeButton) removeButton.gameObject.SetActive(true);
            return true;
        }

        public void Clear()
        {
            PinnedEvidence = null;
            ShowEmpty();
        }

        void ShowEmpty()
        {
            if (thumbnailImage) thumbnailImage.enabled = false;
            if (labelText) labelText.text = string.Empty;
            if (emptyIndicator) emptyIndicator.SetActive(true);
            if (removeButton) removeButton.gameObject.SetActive(false);
        }
    }
}
