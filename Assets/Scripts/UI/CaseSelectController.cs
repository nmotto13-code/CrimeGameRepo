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

                // Row — fixed height, anchor-based children (avoids VLG sizing issues at runtime)
                var rowGo = new GameObject($"CaseRow_{i}");
                rowGo.transform.SetParent(listParent, false);
                var rowRT = rowGo.AddComponent<RectTransform>();
                rowRT.sizeDelta = new Vector2(0, 110);
                rowGo.AddComponent<LayoutElement>().preferredHeight = 110;

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

                // Title — top half of row
                var titleGo  = new GameObject("Title");
                titleGo.transform.SetParent(rowGo.transform, false);
                var titleRT  = titleGo.AddComponent<RectTransform>();
                titleRT.anchorMin = new Vector2(0, 0.48f);
                titleRT.anchorMax = new Vector2(1, 1f);
                titleRT.offsetMin = new Vector2(20, 0);
                titleRT.offsetMax = new Vector2(-20, -10);
                var titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
                titleTxt.text      = caseData.title;
                titleTxt.fontSize  = 28;
                titleTxt.fontStyle = FontStyles.Bold;
                titleTxt.color     = new Color(0.95f, 0.88f, 0.65f);
                titleTxt.alignment = TextAlignmentOptions.BottomLeft;

                // Brief — bottom half of row
                var briefGo  = new GameObject("Brief");
                briefGo.transform.SetParent(rowGo.transform, false);
                var briefRT  = briefGo.AddComponent<RectTransform>();
                briefRT.anchorMin = new Vector2(0, 0f);
                briefRT.anchorMax = new Vector2(1, 0.48f);
                briefRT.offsetMin = new Vector2(20, 8);
                briefRT.offsetMax = new Vector2(-20, 0);
                var briefTxt = briefGo.AddComponent<TextMeshProUGUI>();
                briefTxt.text             = caseData.briefText?.Length > 80
                                          ? caseData.briefText[..80] + "…"
                                          : caseData.briefText;
                briefTxt.fontSize         = 20;
                briefTxt.color            = new Color(0.70f, 0.70f, 0.75f);
                briefTxt.textWrappingMode = TextWrappingModes.NoWrap;
                briefTxt.overflowMode     = TextOverflowModes.Ellipsis;
                briefTxt.alignment        = TextAlignmentOptions.TopLeft;

                // Best score badge — top-right corner
                int best = PlayerProfile.GetCaseBestScore(caseData.caseId);
                if (best > 0)
                {
                    var scoreGo  = new GameObject("BestScore");
                    scoreGo.transform.SetParent(rowGo.transform, false);
                    var scoreRT  = scoreGo.AddComponent<RectTransform>();
                    scoreRT.anchorMin = new Vector2(0.6f, 0.52f);
                    scoreRT.anchorMax = new Vector2(1f, 1f);
                    scoreRT.offsetMin = Vector2.zero;
                    scoreRT.offsetMax = new Vector2(-16, -10);
                    var scoreTxt = scoreGo.AddComponent<TextMeshProUGUI>();
                    scoreTxt.text      = $"★ {best:N0}";
                    scoreTxt.fontSize  = 20;
                    scoreTxt.color     = new Color(0.40f, 0.90f, 0.50f);
                    scoreTxt.alignment = TextAlignmentOptions.BottomRight;
                }
            }

            // Force layout recalculation so all rows size correctly
            Canvas.ForceUpdateCanvases();
            if (listParent is RectTransform listRT)
                LayoutRebuilder.ForceRebuildLayoutImmediate(listRT);
        }

        void SelectCase(CaseData caseData, int index)
        {
            panel.SetActive(false);
            homeScreen.EnterGame();
            GameManager.Instance?.LoadCaseByIndex(index);
        }
    }
}
