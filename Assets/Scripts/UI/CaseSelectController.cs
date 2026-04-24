using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CasebookGame.Core;
using CasebookGame.Data;

namespace CasebookGame.UI
{
    public class CaseSelectController : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] Transform  listParent;
        [SerializeField] Button     closeBtn;
        [SerializeField] HomeScreenController homeScreen;

        [Header("Row prefab built at runtime")]
        [SerializeField] Color rowColor      = new Color(0.16f, 0.16f, 0.24f);
        [SerializeField] Color rowHoverColor = new Color(0.22f, 0.22f, 0.34f);

        bool populated;

        void Start()
        {
            closeBtn.onClick.AddListener(() => panel.SetActive(false));
        }

        public void Populate()
        {
            if (populated) return;
            populated = true;

            var cases = GameManager.Instance?.availableCases;
            if (cases == null) return;

            for (int i = 0; i < cases.Length; i++)
            {
                var caseData = cases[i];
                int index    = i;

                var rowGo = new GameObject($"CaseRow_{i}");
                rowGo.transform.SetParent(listParent, false);

                var rowRT  = rowGo.AddComponent<RectTransform>();
                rowRT.sizeDelta = new Vector2(0, 120);
                var rowLE  = rowGo.AddComponent<LayoutElement>();
                rowLE.preferredHeight = 120;
                rowLE.flexibleWidth   = 1;

                var rowImg = rowGo.AddComponent<Image>();
                rowImg.color = rowColor;

                var rowBtn = rowGo.AddComponent<Button>();
                rowBtn.targetGraphic = rowImg;
                var colors = rowBtn.colors;
                colors.normalColor      = rowColor;
                colors.highlightedColor = rowHoverColor;
                colors.pressedColor     = new Color(0.10f, 0.10f, 0.16f);
                rowBtn.colors = colors;
                rowBtn.onClick.AddListener(() => SelectCase(caseData, index));

                var vlg = rowGo.AddComponent<VerticalLayoutGroup>();
                vlg.padding              = new RectOffset(20, 20, 16, 12);
                vlg.spacing              = 4;
                vlg.childControlHeight   = true;
                vlg.childControlWidth    = true;
                vlg.childForceExpandWidth = true;

                var titleGo  = new GameObject("Title");
                titleGo.transform.SetParent(rowGo.transform, false);
                var titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
                titleTxt.text      = caseData.title;
                titleTxt.fontSize  = 30;
                titleTxt.fontStyle = TMPro.FontStyles.Bold;
                titleTxt.color     = new Color(0.95f, 0.88f, 0.65f);
                titleGo.AddComponent<LayoutElement>().preferredHeight = 44;

                var briefGo  = new GameObject("Brief");
                briefGo.transform.SetParent(rowGo.transform, false);
                var briefTxt = briefGo.AddComponent<TextMeshProUGUI>();
                briefTxt.text                = caseData.briefText?.Length > 80
                                             ? caseData.briefText[..80] + "…"
                                             : caseData.briefText;
                briefTxt.fontSize            = 22;
                briefTxt.color               = new Color(0.70f, 0.70f, 0.75f);
                briefTxt.textWrappingMode    = TMPro.TextWrappingModes.Normal;
                briefGo.AddComponent<LayoutElement>().flexibleHeight = 1;

                // Best score badge
                int best = PlayerProfile.GetCaseBestScore(caseData.caseId);
                if (best > 0)
                {
                    var scoreGo  = new GameObject("BestScore");
                    scoreGo.transform.SetParent(rowGo.transform, false);
                    var scoreTxt = scoreGo.AddComponent<TextMeshProUGUI>();
                    scoreTxt.text      = $"Best: {best:N0} pts";
                    scoreTxt.fontSize  = 20;
                    scoreTxt.color     = new Color(0.40f, 0.90f, 0.50f);
                    scoreTxt.alignment = TMPro.TextAlignmentOptions.Right;
                    scoreGo.AddComponent<LayoutElement>().preferredHeight = 28;
                }
            }
        }

        void SelectCase(CaseData caseData, int index)
        {
            panel.SetActive(false);
            homeScreen.EnterGame();
            GameManager.Instance?.LoadCaseByIndex(index);
        }
    }
}
