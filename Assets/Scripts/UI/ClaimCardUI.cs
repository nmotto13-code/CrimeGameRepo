using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using CasebookGame.Data;

namespace CasebookGame.UI
{
    public class ClaimCardUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public static System.Action<string> OnClaimTapped;

        [SerializeField] TMP_Text speakerText;
        [SerializeField] TMP_Text claimText;
        [SerializeField] TMP_Text tagHintText;
        [SerializeField] Image background;
        [SerializeField] GameObject focusActionsPanel;
        [SerializeField] Button crossCheckBtn;
        [SerializeField] Button highlightBtn;

        [SerializeField] Color normalColor   = new Color(0.15f, 0.15f, 0.2f);
        [SerializeField] Color selectedColor = new Color(0.2f, 0.5f, 0.9f, 0.4f);
        [SerializeField] Color crossCheckReadyColor = new Color(1f, 0.90f, 0.15f, 0.25f);

        [SerializeField] float longPressDuration = 0.6f;

        ClaimData data;
        Coroutine longPressRoutine;
        bool longPressTriggered;

        public void Initialize(ClaimData claimData)
        {
            data = claimData;
            if (speakerText) speakerText.text = claimData.speakerName;
            if (claimText)   claimText.text   = claimData.claimText;

            if (tagHintText)
            {
                tagHintText.text = claimData.referencedTags != null && claimData.referencedTags.Count > 0
                    ? "Evidence: " + string.Join(" · ", claimData.referencedTags)
                    : "";
                tagHintText.gameObject.SetActive(tagHintText.text.Length > 0);
            }

            if (background) background.color = normalColor;
            if (focusActionsPanel) focusActionsPanel.SetActive(false);

            crossCheckBtn?.onClick.AddListener(OnCrossCheck);
            highlightBtn?.onClick.AddListener(OnHighlight);
        }

        public void OnPointerDown(PointerEventData e)
        {
            longPressTriggered = false;
            longPressRoutine   = StartCoroutine(LongPressRoutine());
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (longPressRoutine != null) StopCoroutine(longPressRoutine);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (longPressTriggered) return;

            // Cross-check mode: this tap applies the cross-check to this claim
            if (Tools.ToolsController.Instance != null && Tools.ToolsController.Instance.IsCrossCheckModeActive)
            {
                Tools.ToolsController.Instance.ApplyCrossCheck(data);
                if (background) background.color = crossCheckReadyColor;
                return;
            }

            OnClaimTapped?.Invoke(data.claimId);
            if (background) background.color = selectedColor;
        }

        IEnumerator LongPressRoutine()
        {
            yield return new WaitForSeconds(longPressDuration);
            longPressTriggered = true;
            if (focusActionsPanel) focusActionsPanel.SetActive(true);
        }

        void OnCrossCheck()
        {
            focusActionsPanel?.SetActive(false);
            Tools.ToolsController.Instance?.ApplyCrossCheck(data);
        }

        void OnHighlight()
        {
            focusActionsPanel?.SetActive(false);
            var cards = FindObjectsByType<EvidenceCardUI>(FindObjectsSortMode.None);
            foreach (var card in cards)
            {
                bool match = false;
                if (card.Data != null)
                    foreach (var tag in data.referencedTags)
                        if (card.Data.HasTag(tag)) { match = true; break; }
                card.SetHighlight(match);
            }
        }
    }
}
