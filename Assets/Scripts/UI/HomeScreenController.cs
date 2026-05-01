using UnityEngine;
using UnityEngine.UI;
using CasebookGame.Core;

namespace CasebookGame.UI
{
    public class HomeScreenController : BaseScreen
    {
        [SerializeField] Button investigateBtn;
        [SerializeField] Button viewProfileBtn;
        [SerializeField] Button testCasesBtn;

        // Direct case-jump buttons wired by SceneBuilder (C011–C030)
        [SerializeField] Button[] caseJumpBtns;
        [SerializeField] string[] caseJumpIds;

        public override ScreenId ScreenId => ScreenId.Home;

        protected override void Awake()
        {
            base.Awake();

            investigateBtn?.onClick.AddListener(() =>
                NavigationManager.Instance?.Push(ScreenId.CaseSelect, TransitionType.SlideLeft));

            viewProfileBtn?.onClick.AddListener(() =>
                NavigationManager.Instance?.Push(ScreenId.Account, TransitionType.SlideLeft));

            testCasesBtn?.onClick.AddListener(() =>
                NavigationManager.Instance?.Push(ScreenId.CaseSelect, TransitionType.SlideLeft));

            if (caseJumpBtns != null && caseJumpIds != null)
            {
                for (int i = 0; i < caseJumpBtns.Length; i++)
                {
                    if (caseJumpBtns[i] == null) continue;
                    string id = i < caseJumpIds.Length ? caseJumpIds[i] : null;
                    if (string.IsNullOrEmpty(id)) continue;
                    int captured = i;
                    string capturedId = id;
                    caseJumpBtns[i].onClick.AddListener(() =>
                    {
                        int idx = GameManager.Instance?.IndexOfCase(capturedId) ?? -1;
                        if (idx < 0) { Debug.LogWarning($"[Home] Case not found: {capturedId}"); return; }
                        GameManager.Instance.LoadCaseByIndex(idx);
                        NavigationManager.Instance?.Push(ScreenId.Game, TransitionType.FadeUp);
                    });
                }
            }
        }
    }
}
