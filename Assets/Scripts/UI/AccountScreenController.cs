using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CasebookGame.Core;
using CasebookGame.Data;

namespace CasebookGame.UI
{
    public class AccountScreenController : BaseScreen
    {
        bool generatedSupplementalUi;

        [Header("Panels")]
        [SerializeField] GameObject infoPanel;
        [SerializeField] GameObject resultsPanel;
        [SerializeField] Button closeBtn;

        [Header("Tab buttons")]
        [SerializeField] Button infoTabBtn;
        [SerializeField] Button resultsTabBtn;

        [Header("Info tab")]
        [SerializeField] TMP_Text totalScoreText;
        [SerializeField] TMP_Text casesCompletedText;
        [SerializeField] TMP_Text perfectSolvesText;
        [SerializeField] TMP_Text totalStarsText;
        [SerializeField] TMP_Text rankText;
        [SerializeField] TMP_Text xpProgressText;
        [SerializeField] Image xpProgressFill;
        [SerializeField] TMP_Text currentStreakText;
        [SerializeField] TMP_Text bestStreakText;
        [SerializeField] TMP_Text dailyCaseTitleText;
        [SerializeField] TMP_Text dailyCaseStatusText;
        [SerializeField] TMP_Text achievementsSummaryText;
        [SerializeField] Button playDailyCaseButton;
        [SerializeField] Transform achievementsListParent;

        [Header("Case Results tab")]
        [SerializeField] Transform resultsListParent;

        static readonly Color TAB_ACTIVE = new Color(0.90f, 0.50f, 0.10f);
        static readonly Color TAB_INACTIVE = new Color(0.30f, 0.30f, 0.40f);

        public override ScreenId ScreenId => ScreenId.Account;

        protected override void Awake()
        {
            base.Awake();
            closeBtn?.onClick.AddListener(() =>
                NavigationManager.Instance?.Pop(TransitionType.SlideRight));
            infoTabBtn?.onClick.AddListener(() => ShowTab(info: true));
            resultsTabBtn?.onClick.AddListener(() => ShowTab(info: false));
            playDailyCaseButton?.onClick.AddListener(PlayDailyCase);
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
            EnsureSupplementalInfoUi();

            var cases = GameManager.Instance?.availableCases ?? System.Array.Empty<CaseData>();
            var departments = ProgressionRules.GetDepartments(cases);
            var rankProgress = PlayerProfile.GetRankProgress();
            var achievements = PlayerProfile.GetAchievements(cases);
            var dailyCase = PlayerProfile.GetDailyCaseInfo(cases, departments);
            var dailyCaseData = cases.FirstOrDefault(c => c != null && c.caseId == dailyCase.CaseId);

            if (rankText) rankText.text = $"Rank {rankProgress.Rank}";
            if (xpProgressText)
            {
                xpProgressText.text = rankProgress.Rank >= ProgressionRules.MaxRank
                    ? $"{rankProgress.CurrentXp:N0} XP"
                    : $"{rankProgress.CurrentXp - rankProgress.CurrentRankStartXp:N0} / {rankProgress.NextRankXp - rankProgress.CurrentRankStartXp:N0} XP";
            }
            if (xpProgressFill) xpProgressFill.fillAmount = rankProgress.Normalized;

            if (totalScoreText) totalScoreText.text = $"{PlayerProfile.GetTotalScore():N0}";
            if (casesCompletedText) casesCompletedText.text = $"{PlayerProfile.GetCasesCompleted()}";
            if (perfectSolvesText) perfectSolvesText.text = $"{PlayerProfile.GetPerfectSolves()}";
            if (totalStarsText) totalStarsText.text = $"{PlayerProfile.GetTotalStars(cases)}";
            if (currentStreakText) currentStreakText.text = $"{PlayerProfile.GetCurrentStreak()}";
            if (bestStreakText) bestStreakText.text = $"{PlayerProfile.GetBestStreak()}";
            if (dailyCaseTitleText) dailyCaseTitleText.text = dailyCaseData != null ? dailyCaseData.title : "No unlocked case";
            if (dailyCaseStatusText)
            {
                dailyCaseStatusText.text = dailyCase.IsSolvedToday
                    ? "Solved today. Streak counted."
                    : "Solve this case today to extend your streak.";
            }
            if (playDailyCaseButton) playDailyCaseButton.interactable = dailyCaseData != null;
            if (achievementsSummaryText)
                achievementsSummaryText.text = $"{achievements.Count(a => a.IsUnlocked)} / {achievements.Count} unlocked";

            PopulateAchievements(achievements);
            PopulateResults(cases);
            ShowTab(info: true);
        }

        void ShowTab(bool info)
        {
            infoPanel?.SetActive(info);
            resultsPanel?.SetActive(!info);

            SetTabActive(infoTabBtn, info);
            SetTabActive(resultsTabBtn, !info);
        }

        static void SetTabActive(Button btn, bool active)
        {
            var img = btn?.GetComponent<Image>();
            if (img) img.color = active ? TAB_ACTIVE : TAB_INACTIVE;
            var txt = btn?.GetComponentInChildren<TMP_Text>();
            if (txt) txt.color = active ? Color.white : new Color(0.70f, 0.70f, 0.75f);
        }

        void EnsureSupplementalInfoUi()
        {
            if (generatedSupplementalUi || infoPanel == null)
                return;

            generatedSupplementalUi = true;

            var font = totalScoreText != null
                ? totalScoreText.font
                : FindFirstObjectByType<TextMeshProUGUI>()?.font;

            if (rankText == null || xpProgressText == null || xpProgressFill == null)
                CreateRankCard(font);

            if (totalStarsText == null)
                totalStarsText = CreateStatRow("Total Stars", font);
            if (currentStreakText == null)
                currentStreakText = CreateStatRow("Current Streak", font);
            if (bestStreakText == null)
                bestStreakText = CreateStatRow("Best Streak", font);

            if (dailyCaseTitleText == null || dailyCaseStatusText == null || playDailyCaseButton == null)
                CreateDailyCaseCard(font);

            if (achievementsSummaryText == null)
                CreateAchievementsSummary(font);
        }

        void CreateRankCard(TMP_FontAsset font)
        {
            var cardGo = new GameObject("GeneratedRankCard");
            cardGo.transform.SetParent(infoPanel.transform, false);
            cardGo.AddComponent<LayoutElement>().preferredHeight = 220;
            cardGo.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.20f);
            if (infoPanel.transform.childCount > 1)
                cardGo.transform.SetSiblingIndex(1);

            var layout = cardGo.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 20, 20);
            layout.spacing = 10;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            CreateLabel(cardGo.transform, "DETECTIVE RANK", 26, FontStyles.Bold, new Color(0.95f, 0.88f, 0.65f), font)
                .alignment = TextAlignmentOptions.MidlineLeft;

            rankText = CreateLabel(cardGo.transform, "Rank 1", 42, FontStyles.Bold, Color.white, font);
            rankText.alignment = TextAlignmentOptions.MidlineLeft;

            xpProgressText = CreateLabel(cardGo.transform, "0 / 150 XP", 22, FontStyles.Normal, new Color(0.75f, 0.75f, 0.80f), font);
            xpProgressText.alignment = TextAlignmentOptions.MidlineLeft;

            var barBg = new GameObject("XpBarBg");
            barBg.transform.SetParent(cardGo.transform, false);
            barBg.AddComponent<LayoutElement>().preferredHeight = 22;
            barBg.AddComponent<Image>().color = new Color(0.20f, 0.20f, 0.28f);

            var fillGo = new GameObject("XpBarFill");
            fillGo.transform.SetParent(barBg.transform, false);
            var fillRT = fillGo.AddComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            xpProgressFill = fillGo.AddComponent<Image>();
            xpProgressFill.color = new Color(0.90f, 0.50f, 0.10f);
            xpProgressFill.type = Image.Type.Filled;
            xpProgressFill.fillMethod = Image.FillMethod.Horizontal;
            xpProgressFill.fillAmount = 0f;
        }

        void CreateDailyCaseCard(TMP_FontAsset font)
        {
            var cardGo = new GameObject("GeneratedDailyCaseCard");
            cardGo.transform.SetParent(infoPanel.transform, false);
            cardGo.AddComponent<LayoutElement>().preferredHeight = 190;
            cardGo.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.20f);

            var layout = cardGo.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 20, 20);
            layout.spacing = 8;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            CreateLabel(cardGo.transform, "DAILY CASE", 26, FontStyles.Bold, new Color(0.95f, 0.88f, 0.65f), font)
                .alignment = TextAlignmentOptions.MidlineLeft;

            dailyCaseTitleText = CreateLabel(cardGo.transform, "No unlocked case", 30, FontStyles.Bold, Color.white, font);
            dailyCaseTitleText.alignment = TextAlignmentOptions.MidlineLeft;
            dailyCaseTitleText.textWrappingMode = TextWrappingModes.Normal;

            dailyCaseStatusText = CreateLabel(cardGo.transform, "Solve this case today to extend your streak.", 20, FontStyles.Normal, new Color(0.72f, 0.72f, 0.78f), font);
            dailyCaseStatusText.alignment = TextAlignmentOptions.MidlineLeft;
            dailyCaseStatusText.textWrappingMode = TextWrappingModes.Normal;

            var buttonGo = new GameObject("DailyPlayButton");
            buttonGo.transform.SetParent(cardGo.transform, false);
            buttonGo.AddComponent<LayoutElement>().preferredHeight = 54;
            var buttonImage = buttonGo.AddComponent<Image>();
            buttonImage.color = new Color(0.20f, 0.65f, 0.40f);
            playDailyCaseButton = buttonGo.AddComponent<Button>();
            playDailyCaseButton.targetGraphic = buttonImage;
            playDailyCaseButton.onClick.AddListener(PlayDailyCase);

            var label = CreateLabel(buttonGo.transform, "PLAY DAILY CASE", 22, FontStyles.Bold, new Color(0.06f, 0.06f, 0.10f), font);
            label.alignment = TextAlignmentOptions.Center;
            StretchToParent(label.rectTransform);
        }

        void CreateAchievementsSummary(TMP_FontAsset font)
        {
            var header = CreateLabel(infoPanel.transform, "ACHIEVEMENTS", 28, FontStyles.Bold, new Color(0.90f, 0.50f, 0.10f), font);
            header.alignment = TextAlignmentOptions.MidlineLeft;

            achievementsSummaryText = CreateLabel(infoPanel.transform, "0 / 10 unlocked", 20, FontStyles.Normal, new Color(0.72f, 0.72f, 0.78f), font);
            achievementsSummaryText.alignment = TextAlignmentOptions.MidlineLeft;
        }

        TMP_Text CreateStatRow(string label, TMP_FontAsset font)
        {
            var rowGo = new GameObject($"Generated_{label.Replace(" ", string.Empty)}");
            rowGo.transform.SetParent(infoPanel.transform, false);
            rowGo.AddComponent<LayoutElement>().preferredHeight = 72;
            rowGo.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.20f);

            var layout = rowGo.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 0, 0);
            layout.spacing = 12;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = false;

            var labelText = CreateLabel(rowGo.transform, label, 28, FontStyles.Normal, new Color(0.75f, 0.75f, 0.80f), font);
            labelText.alignment = TextAlignmentOptions.MidlineLeft;
            labelText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            var valueText = CreateLabel(rowGo.transform, "0", 28, FontStyles.Bold, new Color(0.95f, 0.88f, 0.65f), font);
            valueText.alignment = TextAlignmentOptions.Right;
            valueText.gameObject.AddComponent<LayoutElement>().preferredWidth = 200;
            return valueText;
        }

        TMP_Text CreateLabel(Transform parent, string text, float fontSize, FontStyles style, Color color, TMP_FontAsset font)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            return tmp;
        }

        static void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        void PopulateResults(CaseData[] cases)
        {
            if (resultsListParent == null)
                return;

            foreach (Transform child in resultsListParent)
                Destroy(child.gameObject);

            if (cases == null)
                return;

            foreach (var caseData in cases)
            {
                if (caseData == null)
                    continue;

                int bestScore = PlayerProfile.GetCaseBestScore(caseData.caseId);
                int stars = PlayerProfile.GetCaseStars(caseData.caseId);

                var rowGo = new GameObject($"Result_{caseData.caseId}");
                rowGo.transform.SetParent(resultsListParent, false);
                var rowLE = rowGo.AddComponent<LayoutElement>();
                rowLE.preferredHeight = 100;
                rowLE.flexibleWidth = 1;
                rowGo.AddComponent<Image>().color = new Color(0.14f, 0.14f, 0.20f);

                var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
                hlg.padding = new RectOffset(20, 20, 16, 16);
                hlg.spacing = 12;
                hlg.childControlHeight = true;
                hlg.childControlWidth = true;
                hlg.childForceExpandHeight = true;

                var nameGo = new GameObject("CaseName");
                nameGo.transform.SetParent(rowGo.transform, false);
                var nameTxt = nameGo.AddComponent<TextMeshProUGUI>();
                nameTxt.text = caseData.title;
                nameTxt.fontSize = 26;
                nameTxt.fontStyle = FontStyles.Bold;
                nameTxt.color = new Color(0.95f, 0.88f, 0.65f);
                nameTxt.textWrappingMode = TextWrappingModes.Normal;
                nameGo.AddComponent<LayoutElement>().flexibleWidth = 1;

                var scoreGo = new GameObject("Score");
                scoreGo.transform.SetParent(rowGo.transform, false);
                var scoreTxt = scoreGo.AddComponent<TextMeshProUGUI>();
                scoreTxt.text = bestScore > 0 ? $"{bestScore:N0} pts" : "Unsolved";
                scoreTxt.fontSize = 26;
                scoreTxt.fontStyle = FontStyles.Bold;
                scoreTxt.color = bestScore > 0 ? new Color(0.40f, 0.90f, 0.50f) : new Color(0.65f, 0.65f, 0.72f);
                scoreTxt.alignment = TextAlignmentOptions.Right;
                scoreGo.AddComponent<LayoutElement>().preferredWidth = 180;

                var starsGo = new GameObject("Stars");
                starsGo.transform.SetParent(rowGo.transform, false);
                var starsTxt = starsGo.AddComponent<TextMeshProUGUI>();
                starsTxt.text = BuildStars(stars);
                starsTxt.fontSize = 26;
                starsTxt.color = Color.white;
                starsTxt.alignment = TextAlignmentOptions.Right;
                starsGo.AddComponent<LayoutElement>().preferredWidth = 140;
            }
        }

        void PopulateAchievements(System.Collections.Generic.IReadOnlyList<AchievementStatus> achievements)
        {
            if (achievementsListParent == null)
                return;

            foreach (Transform child in achievementsListParent)
                Destroy(child.gameObject);

            foreach (var achievement in achievements)
            {
                var rowGo = new GameObject($"Achievement_{achievement.Definition.Id}");
                rowGo.transform.SetParent(achievementsListParent, false);
                rowGo.AddComponent<LayoutElement>().preferredHeight = 92;
                rowGo.AddComponent<Image>().color = achievement.IsUnlocked
                    ? new Color(0.16f, 0.20f, 0.14f)
                    : new Color(0.12f, 0.12f, 0.18f);

                var layout = rowGo.AddComponent<HorizontalLayoutGroup>();
                layout.padding = new RectOffset(18, 18, 14, 14);
                layout.spacing = 12;
                layout.childControlHeight = true;
                layout.childControlWidth = true;

                var bodyGo = new GameObject("Body");
                bodyGo.transform.SetParent(rowGo.transform, false);
                bodyGo.AddComponent<LayoutElement>().flexibleWidth = 1;
                var bodyLayout = bodyGo.AddComponent<VerticalLayoutGroup>();
                bodyLayout.childControlHeight = true;
                bodyLayout.childControlWidth = true;
                bodyLayout.childForceExpandHeight = false;
                bodyLayout.spacing = 4;

                var titleGo = new GameObject("Title");
                titleGo.transform.SetParent(bodyGo.transform, false);
                var title = titleGo.AddComponent<TextMeshProUGUI>();
                title.text = achievement.Definition.Title;
                title.fontSize = 24;
                title.fontStyle = FontStyles.Bold;
                title.color = achievement.IsUnlocked ? new Color(0.95f, 0.88f, 0.65f) : Color.white;

                var descGo = new GameObject("Desc");
                descGo.transform.SetParent(bodyGo.transform, false);
                var desc = descGo.AddComponent<TextMeshProUGUI>();
                desc.text = achievement.Definition.Description;
                desc.fontSize = 18;
                desc.color = new Color(0.72f, 0.72f, 0.78f);
                desc.textWrappingMode = TextWrappingModes.Normal;

                var statusGo = new GameObject("Status");
                statusGo.transform.SetParent(rowGo.transform, false);
                var status = statusGo.AddComponent<TextMeshProUGUI>();
                status.text = achievement.IsUnlocked ? "UNLOCKED" : "LOCKED";
                status.fontSize = 18;
                status.alignment = TextAlignmentOptions.Right;
                status.color = achievement.IsUnlocked ? new Color(0.62f, 0.86f, 0.66f) : new Color(0.84f, 0.58f, 0.52f);
                statusGo.AddComponent<LayoutElement>().preferredWidth = 150;
            }
        }

        void PlayDailyCase()
        {
            var cases = GameManager.Instance?.availableCases ?? System.Array.Empty<CaseData>();
            var departments = ProgressionRules.GetDepartments(cases);
            var dailyCase = PlayerProfile.GetDailyCaseInfo(cases, departments);
            int index = GameManager.Instance?.IndexOfCase(dailyCase.CaseId) ?? -1;
            if (index < 0)
                return;

            GameManager.Instance?.LoadCaseByIndex(index);
            GameScreenController.Instance?.ResetEntryState();
            NavigationManager.Instance?.ResetToRootChild(ScreenId.Game);
        }

        static string BuildStars(int stars)
        {
            const string filled = "<color=#F2C94C>★</color>";
            const string empty = "<color=#5D6070>★</color>";
            int clamped = Mathf.Clamp(stars, 0, 3);
            return string.Concat(Enumerable.Repeat(filled, clamped)) +
                   string.Concat(Enumerable.Repeat(empty, 3 - clamped));
        }
    }
}
