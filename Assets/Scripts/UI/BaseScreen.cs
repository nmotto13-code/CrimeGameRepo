using UnityEngine;
using CasebookGame.Core;

namespace CasebookGame.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class BaseScreen : MonoBehaviour
    {
        public abstract ScreenId ScreenId { get; }

        public CanvasGroup CanvasGroup { get; private set; }
        public RectTransform RT        { get; private set; }

        protected virtual void Awake()
        {
            CanvasGroup = GetComponent<CanvasGroup>();
            RT          = (RectTransform)transform;
        }

        public virtual void OnScreenEnter() { }
        public virtual void OnScreenExit()  { }
    }
}
