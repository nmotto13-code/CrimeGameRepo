using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CasebookGame.Core
{
    /// <summary>
    /// Draggable magnifying glass. When held over a hidden hotspot for dwellTime seconds,
    /// the hotspot is discovered. Attach to the ScenePanel (which has an Image for raycasts).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class MagnifyingGlass : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        [Header("Visual")]
        [SerializeField] RectTransform glassVisual;   // circle image, child of canvas root
        [SerializeField] Image         glassRing;     // outer ring — tints when over a hotspot
        [SerializeField] Color         idleColor      = new Color(0.95f, 0.80f, 0.20f, 0.85f);
        [SerializeField] Color         activeColor    = new Color(0.20f, 0.90f, 0.40f, 1.00f);

        [Header("Detection")]
        [SerializeField] float dwellTime    = 0.5f;   // seconds hovering to trigger discovery
        [SerializeField] float searchRadius = 90f;    // screen-pixel radius

        Canvas  rootCanvas;
        bool    isHeld;
        Vector2 currentScreenPos;

        readonly Dictionary<HotspotController, float> dwellTimers = new();

        void Awake()
        {
            rootCanvas = GetComponentInParent<Canvas>();
            HideGlass();
        }

        // ── Pointer events ─────────────────────────────────────────────

        public void OnPointerDown(PointerEventData e)
        {
            isHeld           = true;
            currentScreenPos = e.position;
            PlaceGlass(e.position);
            if (glassVisual) glassVisual.gameObject.SetActive(true);
            SetRingColor(idleColor);
        }

        public void OnPointerUp(PointerEventData e)
        {
            isHeld = false;
            dwellTimers.Clear();
            HideGlass();
        }

        public void OnDrag(PointerEventData e)
        {
            currentScreenPos = e.position;
            PlaceGlass(e.position);
        }

        // ── Per-frame hotspot check ────────────────────────────────────

        void Update()
        {
            if (!isHeld) return;

            var hotspots = EvidenceDiscoverySystem.Instance?.ActiveHotspots;
            if (hotspots == null) return;

            bool overAny = false;

            foreach (var h in hotspots)
            {
                if (h == null || h.IsDiscovered)
                {
                    dwellTimers.Remove(h);
                    continue;
                }

                float dist = ScreenDistance(h);
                if (dist <= searchRadius)
                {
                    overAny = true;
                    dwellTimers.TryGetValue(h, out float elapsed);
                    elapsed += Time.deltaTime;
                    dwellTimers[h] = elapsed;

                    if (elapsed >= dwellTime)
                    {
                        dwellTimers.Remove(h);
                        h.TriggerDiscovery();
                    }
                }
                else
                {
                    dwellTimers.Remove(h);
                }
            }

            SetRingColor(overAny ? activeColor : idleColor);
        }

        // ── Helpers ────────────────────────────────────────────────────

        void PlaceGlass(Vector2 screenPos)
        {
            if (!glassVisual || rootCanvas == null) return;
            var cam = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null : rootCanvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rootCanvas.GetComponent<RectTransform>(), screenPos, cam, out Vector2 local);
            glassVisual.anchoredPosition = local;
        }

        float ScreenDistance(HotspotController h)
        {
            var cam = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null : rootCanvas.worldCamera;
            Vector2 hotspotScreen = RectTransformUtility.WorldToScreenPoint(cam, h.transform.position);
            return Vector2.Distance(currentScreenPos, hotspotScreen);
        }

        void HideGlass()
        {
            if (glassVisual) glassVisual.gameObject.SetActive(false);
        }

        void SetRingColor(Color c)
        {
            if (glassRing) glassRing.color = c;
        }
    }
}
