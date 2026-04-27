using UnityEngine;
using UnityEngine.UI;
using CasebookGame.Core;

namespace CasebookGame.UI
{
    public class InGameMenuController : BaseScreen
    {
        [SerializeField] Button resumeBtn;
        [SerializeField] Button caseSelectBtn;
        [SerializeField] Button homeBtn;

        public override ScreenId ScreenId => ScreenId.InGameMenu;

        protected override void Awake()
        {
            base.Awake();

            resumeBtn?.onClick.AddListener(() =>
                NavigationManager.Instance?.Pop(TransitionType.SlideRight));

            caseSelectBtn?.onClick.AddListener(() =>
                NavigationManager.Instance?.ConfirmLeaveCase(() =>
                {
                    NavigationManager.Instance.PopToRoot();
                    NavigationManager.Instance.Push(ScreenId.CaseSelect, TransitionType.SlideLeft);
                }));

            homeBtn?.onClick.AddListener(() =>
                NavigationManager.Instance?.ConfirmLeaveCase(() =>
                    NavigationManager.Instance.PopToRoot()));
        }

        public override void OnScreenEnter()
        {
            gameObject.SetActive(true);
        }
    }
}
