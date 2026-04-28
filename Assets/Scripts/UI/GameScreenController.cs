using UnityEngine;
using UnityEngine.UI;
using CasebookGame.Core;

namespace CasebookGame.UI
{
    public class GameScreenController : BaseScreen
    {
        public static GameScreenController Instance { get; private set; }

        [SerializeField] Button hamburgerBtn;

        public override ScreenId ScreenId => ScreenId.Game;

        protected override void Awake()
        {
            base.Awake();
            Instance = this;
            hamburgerBtn?.onClick.AddListener(() =>
                NavigationManager.Instance?.Push(ScreenId.InGameMenu, TransitionType.SlideLeft));
        }

        bool _hasEntered;

        public override void OnScreenEnter()
        {
            gameObject.SetActive(true);
            // Only reset to Brief tab on first entry (new case load), not when returning from InGameMenu
            if (!_hasEntered)
            {
                _hasEntered = true;
                TabController.Instance?.SwitchToTab(0);
            }
        }

        public void ResetEntryState() => _hasEntered = false;

        public override void OnScreenExit()
        {
            // InGameMenu is an overlay pushed on top — GameScreen stays active beneath it.
            // Only deactivate when fully leaving the game context.
        }
    }
}
