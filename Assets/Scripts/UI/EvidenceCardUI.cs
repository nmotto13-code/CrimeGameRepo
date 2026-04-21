using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using CasebookGame.Data;

namespace CasebookGame.UI
{
    /// <summary>
    /// Evidence list card. Tap -> detail panel. Drag -> board slot.
    /// </summary>
    public class EvidenceCardUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] Image    thumbnail;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text descriptionText;
        [SerializeField] TMP_Text tagsText;
        [SerializeField] Image    crossCheckHighlight;

        EvidenceData data;
        GameObject dragProxy;
        Canvas rootCanvas;
        RectTransform rectTransform;

        public EvidenceData Data => data;

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            rootCanvas = GetComponentInParent<Canvas>();
        }

        public void Initialize(EvidenceData evidenceData)
        {
            data = evidenceData;
            if (thumbnail)        thumbnail.sprite        = evidenceData.imageSprite;
            if (nameText)         nameText.text           = evidenceData.displayName;
            if (descriptionText)  descriptionText.text    = evidenceData.descriptionText;
            RefreshTags();
            SetHighlight(false);
        }

        public void RefreshTags()
        {
            if (!tagsText || data == null) return;
            tagsText.text = string.Join(" · ", data.runtimeTags);
        }

        public void SetHighlight(bool on)
        {
            if (crossCheckHighlight) crossCheckHighlight.enabled = on;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            EvidenceDetailPanel.Instance?.Show(data);
        }

        // ── Drag to Board ──────────────────────────────────────────────

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!rootCanvas) return;
            dragProxy = new GameObject("DragProxy", typeof(RectTransform), typeof(Image));
            dragProxy.transform.SetParent(rootCanvas.transform, false);
            dragProxy.transform.SetAsLastSibling();

            var proxyImg = dragProxy.GetComponent<Image>();
            proxyImg.sprite = data?.imageSprite;
            proxyImg.raycastTarget = false;

            var rt = dragProxy.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(80, 80);
            MoveDragProxy(eventData);
        }

        public void OnDrag(PointerEventData eventData) => MoveDragProxy(eventData);

        public void OnEndDrag(PointerEventData eventData)
        {
            if (dragProxy) Destroy(dragProxy);

            // Check if dropped onto a BoardSlot
            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            foreach (var hit in results)
            {
                var slot = hit.gameObject.GetComponent<BoardSlotUI>();
                if (slot != null)
                {
                    slot.TryPin(data);
                    return;
                }
            }
        }

        void MoveDragProxy(PointerEventData e)
        {
            if (!dragProxy) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rootCanvas.GetComponent<RectTransform>(),
                e.position, rootCanvas.worldCamera,
                out Vector2 local);
            dragProxy.GetComponent<RectTransform>().anchoredPosition = local;
        }
    }
}
