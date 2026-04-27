using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CasebookGame.Core;

namespace CasebookGame.UI
{
    public class AccountScreenController : BaseScreen
    {
        [Header("Panels")]
        [SerializeField] GameObject infoPanel;
        [SerializeField] GameObject resultsPanel;
        [SerializeField] Button     closeBtn;

        [Header("Tab buttons")]
        [SerializeField] Button infoTabBtn;
        [SerializeField] Button resultsTabBtn;

        [Header("Info tab")]
        [SerializeField] TMP_Text totalScoreText;
        [SerializeField] TMP_Text casesCompletedText;
        [SerializeField] TMP_Text perfectSolvesText;

        [Header("Case Results tab")]
        [SerializeField] Transform resultsListParent;

        static readonly Color TAB_ACTIVE   = new Color(0.90f, 0.50f, 0.10f);
        static readonly Color TAB_INACTIVE = new Color(0.30f, 0.30f, 0.40f);

        public override ScreenId ScreenId => ScreenId.Account;

        protected override void Awake()
        {
            base.Awake();
            closeBtn?.onClick.AddListener(() =>
                NavigationManager.Instance?.Pop(TransitionType.SlideRight));
            infoTabBtn?.onClick.AddListener(() => ShowTab(info: true));
            resultsTabBtn?.onClick.AddListener(() => ShowTab(info: false));
        }

        void Start()
        {
            ShowTab(info: true);
        }

        public override void OnScreenEnter()
        {
            gameObject.SetActive(true);
            Refresh();
        }

        public void Refresh()
        {
            if (totalScoreText)     totalScoreText.text     = $"{PlayerProfile.GetTotalScore():N0}";
            if (casesCompletedText) casesCompletedText.text = $"{PlayerProfile.GetCasesCompleted()}";
            if (perfectSolvesText)  perfectSolvesText.text  = $"{PlayerProfile.GetPerfectSolves()}";

            PopulateResults();
            ShowTab(info: true);
        }

        void ShowTab(bool info)
        {
            infoPanel?.SetActive(info);
            resultsPanel?.SetActive(!info);

            SetTabActive(infoTabBtn,    info);
            SetTabActive(resultsTabBtn, !info);
        }

        static void SetTabActive(Button btn, bool active)
        {
            var img = btn?.GetComponent<Image>();
            if (img) img.color = active ? TAB_ACTIVE : TAB_INACTIVE;
            var txt = btn?.GetComponentInChildren<TMP_Text>();
            if (txt) txt.color = active ? Color.white : new Color(0.70f, 0.70f, 0.75f);
        }

        void PopulateResults()
        {
            if (resultsListParent == null) return;

            foreach (Transform child in resultsListParent)
                Destroy(child.gameObject);

            var cases = GameManager.Instance?.availableCases;
            if (cases == null) return;

            bool anyResult = false;
            foreach (var c in cases)
            {
                int best = PlayerProfile.GetCaseBestScore(c.caseId);
                if (best <= 0) continue;
                anyResult = true;

                var rowGo = new GameObject($"Result_{c.caseId}");
                rowGo.transform.SetParent(resultsListParent, false);
                var rowLE = rowGo.AddComponent<LayoutElement>();
                rowLE.preferredHeight = 100;
                rowLE.flexibleWidth   = 1;
                rowGo.AddComponent<Image>().color = new Color(0.14f, 0.14f, 0.20f);

                var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
                hlg.padding              = new RectOffset(20, 20, 16, 16);
                hlg.spacing              = 12;
                hlg.childControlHeight   = true;
                hlg.childControlWidth    = true;
                hlg.childForceExpandHeight = true;

                var nameGo  = new GameObject("CaseName");
                nameGo.transform.SetParent(rowGo.transform, false);
                var nameTxt = nameGo.AddComponent<TextMeshProUGUI>();
                nameTxt.text      = c.title;
                nameTxt.fontSize  = 26;
                nameTxt.fontStyle = TMPro.FontStyles.Bold;
                nameTxt.color     = new Color(0.95f, 0.88f, 0.65f);
                nameTxt.textWrappingMode = TMPro.TextWrappingModes.Normal;
                nameGo.AddComponent<LayoutElement>().flexibleWidth = 1;

                var scoreGo  = new GameObject("Score");
                scoreGo.transform.SetParent(rowGo.transform, false);
                var scoreTxt = scoreGo.AddComponent<TextMeshProUGUI>();
                scoreTxt.text      = $"{best:N0} pts";
                scoreTxt.fontSize  = 26;
                scoreTxt.fontStyle = TMPro.FontStyles.Bold;
                scoreTxt.color     = new Color(0.40f, 0.90f, 0.50f);
                scoreTxt.alignment = TMPro.TextAlignmentOptions.Right;
                scoreGo.AddComponent<LayoutElement>().preferredWidth = 180;
            }

            if (!anyResult)
            {
                var emptyGo  = new GameObject("Empty");
                emptyGo.transform.SetParent(resultsListParent, false);
                var emptyTxt = emptyGo.AddComponent<TextMeshProUGUI>();
                emptyTxt.text      = "No cases solved yet.";
                emptyTxt.fontSize  = 26;
                emptyTxt.color     = new Color(0.55f, 0.55f, 0.60f);
                emptyTxt.alignment = TMPro.TextAlignmentOptions.Center;
                emptyGo.AddComponent<LayoutElement>().preferredHeight = 80;
            }
        }
    }
}
