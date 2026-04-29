using UnityEngine;

namespace CasebookGame.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        RectTransform _rt;
        Rect          _lastSafeArea;

        void Awake()
        {
            _rt = (RectTransform)transform;
            Apply();
        }

        void OnRectTransformDimensionsChange() => Apply();

        void Apply()
        {
            if (_rt == null) _rt = (RectTransform)transform;

            var safeArea = Screen.safeArea;
            if (safeArea == _lastSafeArea) return;
            _lastSafeArea = safeArea;

            var screenSize = new Vector2(Screen.width, Screen.height);
            _rt.offsetMin = new Vector2(safeArea.xMin, safeArea.yMin);
            _rt.offsetMax = new Vector2(safeArea.xMax - screenSize.x, safeArea.yMax - screenSize.y);
        }
    }
}
