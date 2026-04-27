using UnityEngine;
using UnityEngine.UI;
using CasebookGame.Core;

namespace CasebookGame.UI
{
    public class GameScreenController : BaseScreen
    {
        [SerializeField] Button hamburgerBtn;

        public override ScreenId ScreenId => ScreenId.Game;

        protected override void Awake()
        {
            base.Awake();
            hamburgerBtn?.onClick.AddListener(() =>
                NavigationManager.Instance?.Push(ScreenId.InGameMenu, TransitionType.SlideLeft));
        }

        public override void OnScreenEnter()
        {
            gameObject.SetActive(true);
            TabController.Instance?.SwitchToTab(0);
        }

        public override void OnScreenExit()
        {
            // InGameMenu is an overlay pushed on top — GameScreen stays active beneath it.
            // Only deactivate when fully leaving the game context.
        }
    }
}
