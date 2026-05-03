using UnityEngine;
using UnityEngine.UI;
using CasebookGame.Core;

namespace CasebookGame.UI
{
    public class InGameMenuController : BaseScreen
    {
        [SerializeField] Button resumeBtn;
        [SerializeField] Button dossierBtn;
        [SerializeField] Button caseSelectBtn;
        [SerializeField] Button homeBtn;

        public override ScreenId ScreenId => ScreenId.InGameMenu;

        protected override void Awake()
        {
            base.Awake();

            resumeBtn?.onClick.AddListener(() =>
                NavigationManager.Instance?.Pop(TransitionType.FadeUp));

            dossierBtn?.onClick.AddListener(() =>
                NavigationManager.Instance?.ShowScreen(ScreenId.Dossier, TransitionType.FadeUp));

            caseSelectBtn?.onClick.AddListener(() =>
                NavigationManager.Instance?.ConfirmLeaveCase(() =>
                    NavigationManager.Instance.ResetToRootChild(ScreenId.CaseSelect)));

            homeBtn?.onClick.AddListener(() =>
                NavigationManager.Instance?.ConfirmLeaveCase(() =>
                    NavigationManager.Instance.ResetToRootChild(ScreenId.Home)));
        }

        public override void OnScreenEnter()
        {
            gameObject.SetActive(true);
        }
    }
}
