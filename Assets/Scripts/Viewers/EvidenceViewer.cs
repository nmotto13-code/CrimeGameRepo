using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using CasebookGame.Data;

namespace CasebookGame.Viewers
{
    /// <summary>
    /// Pinch-to-zoom, drag-to-pan, and enhance overlay for the evidence image.
    /// Works with both touch (iOS) and mouse (editor).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class EvidenceViewer : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Image")]
        [SerializeField] RawImage mainImage;
        [SerializeField] RawImage enhanceOverlay;

        [Header("Zoom Limits")]
        [SerializeField] float minZoom = 1f;
        [SerializeField] float maxZoom = 4f;

        RectTransform imageRect;
        float currentZoom = 1f;

        // Touch tracking
        Vector2 lastSingleTouchPos;
        float lastPinchDist = 0f;
        bool isPinching = false;

        // Mouse drag
        Vector2 mouseDragStart;
        Vector2 anchorAtDragStart;

        void Awake()
        {
            imageRect = mainImage ? mainImage.GetComponent<RectTransform>() : null;
        }

        public void Load(EvidenceData evidence)
        {
            currentZoom = 1f;
            if (imageRect)
            {
                imageRect.localScale = Vector3.one;
                imageRect.anchoredPosition = Vector2.zero;
            }

            if (mainImage && evidence.imageSprite)
                mainImage.texture = evidence.imageSprite.texture;

            if (enhanceOverlay)
            {
                enhanceOverlay.texture = null;
                enhanceOverlay.enabled = false;
            }
        }

        public void ShowEnhanceOverlay(Sprite maskSprite)
        {
            if (!enhanceOverlay || !maskSprite) return;
            enhanceOverlay.texture = maskSprite.texture;
            enhanceOverlay.enabled = true;
        }

        // ── Touch ──────────────────────────────────────────────────────

        void Update()
        {
            if (Input.touchCount == 2)
            {
                HandlePinch();
            }
            else if (Input.touchCount == 1 && !isPinching)
            {
                var t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Moved)
                    Pan(t.deltaPosition);
            }
            else
            {
                isPinching = false;
                lastPinchDist = 0f;
            }
        }

        void HandlePinch()
        {
            var t0 = Input.GetTouch(0);
            var t1 = Input.GetTouch(1);
            float dist = Vector2.Distance(t0.position, t1.position);

            if (!isPinching)
            {
                isPinching = true;
                lastPinchDist = dist;
                return;
            }

            float delta = dist - lastPinchDist;
            lastPinchDist = dist;
            ApplyZoom(delta * 0.01f);
        }

        // ── Mouse (editor) ─────────────────────────────────────────────

        public void OnPointerDown(PointerEventData e) { }
        public void OnPointerUp(PointerEventData e) { }

        public void OnDrag(PointerEventData e)
        {
            // Mouse scroll wheel zoom
            float scroll = Input.mouseScrollDelta.y;
            if (scroll != 0) ApplyZoom(scroll * 0.2f);

            if (e.button == PointerEventData.InputButton.Left)
                Pan(e.delta);
        }

        // ── Core ───────────────────────────────────────────────────────

        void ApplyZoom(float delta)
        {
            if (!imageRect) return;
            currentZoom = Mathf.Clamp(currentZoom + delta, minZoom, maxZoom);
            imageRect.localScale = Vector3.one * currentZoom;
        }

        void Pan(Vector2 delta)
        {
            if (!imageRect) return;
            imageRect.anchoredPosition += delta;
            ClampPan();
        }

        void ClampPan()
        {
            if (!imageRect) return;
            var parentRT = imageRect.parent as RectTransform;
            if (!parentRT) return;

            float halfW = parentRT.rect.width * 0.5f * (currentZoom - 1f);
            float halfH = parentRT.rect.height * 0.5f * (currentZoom - 1f);

            var pos = imageRect.anchoredPosition;
            pos.x = Mathf.Clamp(pos.x, -halfW, halfW);
            pos.y = Mathf.Clamp(pos.y, -halfH, halfH);
            imageRect.anchoredPosition = pos;
        }
    }
}
