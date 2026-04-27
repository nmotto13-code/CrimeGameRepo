using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CasebookGame.Core;
using CasebookGame.Data;

namespace CasebookGame.UI
{
    public class CaseSelectController : BaseScreen
    {
        [SerializeField] Transform listParent;
        [SerializeField] Button    closeBtn;

        [SerializeField] Color rowColor      = new Color(0.16f, 0.16f, 0.24f);
        [SerializeField] Color rowHoverColor = new Color(0.22f, 0.22f, 0.34f);

        public override ScreenId ScreenId => ScreenId.CaseSelect;

        bool populated;

        protected override void Awake()
        {
            base.Awake();
            closeBtn?.onClick.AddListener(() =>
                NavigationManager.Instance?.Pop(TransitionType.SlideRight));
        }

        public override void OnScreenEnter()
        {
            gameObject.SetActive(true);
            Populate();
        }

        void Populate()
        {
            if (populated) return;
            populated = true;

            var cases = GameManager.Instance?.availableCases;
            if (cases == null) { Debug.LogError("[CaseSelect] availableCases is null"); return; }
            Debug.Log($"[CaseSelect] Building {cases.Length} rows, listParent={listParent?.name}");

            var existingTMP = FindFirstObjectByType<TextMeshProUGUI>();
            TMP_FontAsset font = existingTMP != null ? existingTMP.font : null;

            for (int i = 0; i < cases.Length; i++)
            {
                var caseData = cases[i];
                int index    = i;

                var rowGo = new GameObject($"CaseRow_{i}");
                rowGo.transform.SetParent(listParent, false);
                var rowRT = rowGo.AddComponent<RectTransform>();
                rowRT.anchorMin = new Vector2(0, 1);
                rowRT.anchorMax = new Vector2(1, 1);
                rowRT.pivot     = new Vector2(0.5f, 1f);
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

                var textGo = new GameObject("RowText");
                textGo.transform.SetParent(rowGo.transform, false);
                var textRT = textGo.AddComponent<RectTransform>();
                textRT.anchorMin = Vector2.zero;
                textRT.anchorMax = Vector2.one;
                textRT.offsetMin = new Vector2(20, 10);
                textRT.offsetMax = new Vector2(-20, -10);

                var brief = caseData.briefText?.Length > 70
                          ? caseData.briefText[..70] + "…"
                          : caseData.briefText ?? "";

                var tmp = textGo.AddComponent<TextMeshProUGUI>();
                if (font != null) tmp.font = font;
                tmp.text             = $"<b>{caseData.title}</b>\n<size=20><color=#B2B2BF>{brief}</color></size>";
                tmp.fontSize         = 28;
                tmp.color            = Color.white;
                tmp.textWrappingMode = TextWrappingModes.Normal;
                tmp.overflowMode     = TextOverflowModes.Ellipsis;
                tmp.raycastTarget    = false;
            }

            Canvas.ForceUpdateCanvases();
            if (listParent is RectTransform listRT)
                LayoutRebuilder.ForceRebuildLayoutImmediate(listRT);
        }

        void SelectCase(CaseData caseData, int index)
        {
            NavigationManager.Instance?.PopToRootImmediate();
            GameManager.Instance?.LoadCaseByIndex(index);
            NavigationManager.Instance?.Push(ScreenId.Game, TransitionType.FadeUp);
        }
    }
}
