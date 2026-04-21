using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace CasebookGame.Core
{
    /// <summary>
    /// Handles pan (drag) on the crime scene background image.
    /// Works alongside EvidenceDiscoverySystem — tap is handled by individual HotspotControllers.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SceneViewController : MonoBehaviour, IDragHandler, IScrollHandler
    {
        [SerializeField] RectTransform sceneImageRT;

        [SerializeField] float panSpeed   = 1f;
        [SerializeField] float minX = -300f, maxX = 300f;
        [SerializeField] float minY = -300f, maxY = 300f;

        public void OnDrag(PointerEventData e)
        {
            if (!sceneImageRT) return;
            var pos = sceneImageRT.anchoredPosition + e.delta * panSpeed;
            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = Mathf.Clamp(pos.y, minY, maxY);
            sceneImageRT.anchoredPosition = pos;
        }

        public void OnScroll(PointerEventData e) { }   // reserved for pinch-zoom upgrade

        public void ResetView()
        {
            if (sceneImageRT) sceneImageRT.anchoredPosition = Vector2.zero;
        }
    }
}
