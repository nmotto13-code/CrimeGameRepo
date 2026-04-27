using UnityEngine;
using UnityEngine.UI;
using CasebookGame.Core;

namespace CasebookGame.UI
{
    public class HomeScreenController : BaseScreen
    {
        [SerializeField] Button selectCaseBtn;
        [SerializeField] Button viewProfileBtn;

        public override ScreenId ScreenId => ScreenId.Home;

        protected override void Awake()
        {
            base.Awake();
            selectCaseBtn?.onClick.AddListener(() =>
                NavigationManager.Instance?.Push(ScreenId.CaseSelect, TransitionType.SlideLeft));
            viewProfileBtn?.onClick.AddListener(() =>
                NavigationManager.Instance?.Push(ScreenId.Account, TransitionType.SlideLeft));
        }
    }
}
