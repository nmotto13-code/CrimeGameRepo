using System.Collections.Generic;
using System.Linq;
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
        [SerializeField] Button closeBtn;
        [SerializeField] Button openMapBtn;
        [SerializeField] TMP_Text headerTitleText;
        [SerializeField] TMP_Text headerSubtitleText;

        [SerializeField] Color rowColor = new Color(0.16f, 0.16f, 0.24f);
        [SerializeField] Color rowHoverColor = new Color(0.22f, 0.22f, 0.34f);

        public override ScreenId ScreenId => ScreenId.CaseSelect;

        protected override void Awake()
        {
            base.Awake();
            closeBtn?.onClick.AddListener(() =>
                NavigationManager.Instance?.Pop(TransitionType.SlideRight));
            openMapBtn?.onClick.AddListener(() =>
                NavigationManager.Instance?.ShowScreen(ScreenId.CityMap, TransitionType.SlideLeft));
        }

        public override void OnScreenEnter()
        {
            gameObject.SetActive(true);
            Populate();
            RefreshHeader();
        }

        void Populate()
        {
            if (listParent == null)
                return;

            foreach (Transform child in listParent)
                Destroy(child.gameObject);

            var cases = GameManager.Instance?.availableCases;
            if (cases == null)
            {
                Debug.LogError("[CaseSelect] availableCases is null");
                return;
            }

            var departments = ProgressionRules.GetDepartments(cases);
            int rank = PlayerProfile.GetRank();
            int totalStars = PlayerProfile.GetTotalStars(cases);

            var existingTMP = FindFirstObjectByType<TextMeshProUGUI>();
            TMP_FontAsset font = existingTMP != null ? existingTMP.font : null;

            var caseLookup = cases.Where(c => c != null).ToDictionary(c => c.caseId, c => c);
            var caseIndexLookup = new Dictionary<string, int>();
            for (int i = 0; i < cases.Length; i++)
            {
                if (cases[i] != null)
                    caseIndexLookup[cases[i].caseId] = i;
            }

            var seen = new HashSet<string>();
            foreach (var department in departments)
            {
                if (department == null)
                    continue;

                var departmentCases = department.caseIds
                    .Where(caseLookup.ContainsKey)
                    .Select(caseId => caseLookup[caseId])
                    .ToList();

                if (departmentCases.Count == 0)
                    continue;

                var lockStatus = ProgressionRules.GetDepartmentLockStatus(department, rank, totalStars);
                CreateDepartmentHeader(font, lockStatus, null);

                foreach (var caseData in departmentCases)
                {
                    seen.Add(caseData.caseId);
                    CreateCaseRow(caseData, caseIndexLookup[caseData.caseId], font, lockStatus.IsUnlocked, lockStatus.RequirementText);
                }
            }

            var unassignedCases = cases.Where(c => c != null && !seen.Contains(c.caseId)).ToList();
            if (unassignedCases.Count > 0)
            {
                CreateDepartmentHeader(font, new DepartmentLockStatus
                {
                    Department = null,
                    IsUnlocked = true,
                    RequirementText = "Unlocked"
                }, "Unassigned Cases");

                foreach (var caseData in unassignedCases)
                    CreateCaseRow(caseData, caseIndexLookup[caseData.caseId], font, true, "Unlocked");
            }

            Canvas.ForceUpdateCanvases();
            if (listParent is RectTransform listRT)
                LayoutRebuilder.ForceRebuildLayoutImmediate(listRT);
        }

        void RefreshHeader()
        {
            if (headerTitleText != null)
                headerTitleText.text = "DEPARTMENT DESK";

            if (headerSubtitleText == null)
                return;

            var cases = GameManager.Instance?.availableCases ?? System.Array.Empty<CaseData>();
            var departments = ProgressionRules.GetDepartments(cases);
            int rank = PlayerProfile.GetRank();
            int stars = PlayerProfile.GetTotalStars(cases);
            int unlocked = departments.Count(d => d != null && ProgressionRules.IsDepartmentUnlocked(d, rank, stars));
            var nextLocked = departments
                .Where(d => d != null && !ProgressionRules.IsDepartmentUnlocked(d, rank, stars))
                .OrderBy(d => d.requiredRank)
                .ThenBy(d => d.requiredStarsCount)
                .FirstOrDefault();

            headerSubtitleText.text = nextLocked != null
                ? $"{unlocked}/{departments.Count} desks unlocked | Next promotion: {nextLocked.displayName} at Rank {nextLocked.requiredRank} / {nextLocked.requiredStarsCount} stars"
                : $"{unlocked}/{departments.Count} desks unlocked | Open the city map to launch fieldwork";
        }

        void CreateDepartmentHeader(TMP_FontAsset font, DepartmentLockStatus lockStatus, string overrideTitle)
        {
            string titleText = overrideTitle ?? lockStatus.Department?.displayName ?? "Department";

            var headerGo = new GameObject($"Department_{titleText}");
            headerGo.transform.SetParent(listParent, false);
            headerGo.AddComponent<LayoutElement>().preferredHeight = 122;
            headerGo.AddComponent<Image>().color = lockStatus.IsUnlocked
                ? new Color(0.20f, 0.16f, 0.10f)
                : new Color(0.14f, 0.12f, 0.12f);

            var layout = headerGo.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 12, 12);
            layout.spacing = 6;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;

            var topRow = new GameObject("TopRow");
            topRow.transform.SetParent(headerGo.transform, false);
            var topLayout = topRow.AddComponent<HorizontalLayoutGroup>();
            topLayout.spacing = 12;
            topLayout.childControlHeight = true;
            topLayout.childControlWidth = true;
            topLayout.childForceExpandWidth = false;

            if (lockStatus.Department?.mapIcon != null)
            {
                var iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(topRow.transform, false);
                iconGo.AddComponent<LayoutElement>().preferredWidth = 44;
                var icon = iconGo.AddComponent<Image>();
                icon.sprite = lockStatus.Department.mapIcon;
                icon.preserveAspect = true;
            }

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(topRow.transform, false);
            var title = titleGo.AddComponent<TextMeshProUGUI>();
            if (font != null) title.font = font;
            title.fontSize = 28;
            title.fontStyle = FontStyles.Bold;
            title.color = lockStatus.IsUnlocked ? new Color(0.95f, 0.88f, 0.65f) : new Color(0.85f, 0.78f, 0.72f);
            title.text = titleText;
            titleGo.AddComponent<LayoutElement>().flexibleWidth = 1;

            var requirementGo = new GameObject("Requirement");
            requirementGo.transform.SetParent(topRow.transform, false);
            var requirement = requirementGo.AddComponent<TextMeshProUGUI>();
            if (font != null) requirement.font = font;
            requirement.fontSize = 20;
            requirement.alignment = TextAlignmentOptions.Right;
            requirement.color = lockStatus.IsUnlocked ? new Color(0.62f, 0.86f, 0.66f) : new Color(0.86f, 0.60f, 0.52f);
            requirement.text = lockStatus.RequirementText;
            requirementGo.AddComponent<LayoutElement>().preferredWidth = 360;

            if (lockStatus.Department != null)
            {
                var summaryGo = new GameObject("Summary");
                summaryGo.transform.SetParent(headerGo.transform, false);
                var summary = summaryGo.AddComponent<TextMeshProUGUI>();
                if (font != null) summary.font = font;
                summary.fontSize = 18;
                summary.color = new Color(0.78f, 0.78f, 0.82f);
                summary.textWrappingMode = TextWrappingModes.Normal;
                summary.text = string.IsNullOrWhiteSpace(lockStatus.Department.summaryText)
                    ? "Open the city map to launch cases from this desk."
                    : lockStatus.Department.summaryText;

                var blurbGo = new GameObject("UnlockBlurb");
                blurbGo.transform.SetParent(headerGo.transform, false);
                var blurb = blurbGo.AddComponent<TextMeshProUGUI>();
                if (font != null) blurb.font = font;
                blurb.fontSize = 16;
                blurb.color = lockStatus.IsUnlocked ? new Color(0.66f, 0.84f, 0.70f) : new Color(0.86f, 0.72f, 0.66f);
                blurb.textWrappingMode = TextWrappingModes.Normal;
                blurb.text = lockStatus.IsUnlocked
                    ? $"Arc: {lockStatus.Department.arcLabel}"
                    : lockStatus.Department.unlockBlurb;
            }
        }

        void CreateCaseRow(CaseData caseData, int index, TMP_FontAsset font, bool unlocked, string lockReason)
        {
            var rowGo = new GameObject($"CaseRow_{caseData.caseId}");
            rowGo.transform.SetParent(listParent, false);
            rowGo.AddComponent<LayoutElement>().preferredHeight = 132;

            var rowImg = rowGo.AddComponent<Image>();
            rowImg.color = unlocked ? rowColor : new Color(0.12f, 0.12f, 0.16f);

            var rowBtn = rowGo.AddComponent<Button>();
            rowBtn.targetGraphic = rowImg;
            rowBtn.interactable = unlocked;
            var colors = rowBtn.colors;
            colors.normalColor = unlocked ? rowColor : new Color(0.12f, 0.12f, 0.16f);
            colors.highlightedColor = unlocked ? rowHoverColor : new Color(0.12f, 0.12f, 0.16f);
            colors.pressedColor = new Color(0.10f, 0.10f, 0.16f);
            colors.disabledColor = new Color(0.12f, 0.12f, 0.16f);
            rowBtn.colors = colors;
            if (unlocked)
                rowBtn.onClick.AddListener(() => SelectCase(caseData, index));

            var layout = rowGo.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 14, 14);
            layout.spacing = 12;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;

            var leftGo = new GameObject("Left");
            leftGo.transform.SetParent(rowGo.transform, false);
            leftGo.AddComponent<LayoutElement>().flexibleWidth = 1;
            var leftLayout = leftGo.AddComponent<VerticalLayoutGroup>();
            leftLayout.childControlHeight = true;
            leftLayout.childControlWidth = true;
            leftLayout.childForceExpandHeight = false;
            leftLayout.spacing = 6;

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(leftGo.transform, false);
            var title = titleGo.AddComponent<TextMeshProUGUI>();
            if (font != null) title.font = font;
            title.fontSize = 30;
            title.fontStyle = FontStyles.Bold;
            title.color = unlocked ? Color.white : new Color(0.78f, 0.78f, 0.82f);
            title.text = caseData.title;

            var briefGo = new GameObject("Brief");
            briefGo.transform.SetParent(leftGo.transform, false);
            var briefText = briefGo.AddComponent<TextMeshProUGUI>();
            if (font != null) briefText.font = font;
            briefText.fontSize = 20;
            briefText.color = unlocked ? new Color(0.70f, 0.70f, 0.75f) : new Color(0.54f, 0.54f, 0.60f);
            briefText.textWrappingMode = TextWrappingModes.Normal;
            briefText.overflowMode = TextOverflowModes.Ellipsis;
            briefText.text = GetBriefPreview(caseData);

            var flowGo = new GameObject("Flow");
            flowGo.transform.SetParent(leftGo.transform, false);
            var flowText = flowGo.AddComponent<TextMeshProUGUI>();
            if (font != null) flowText.font = font;
            flowText.fontSize = 16;
            flowText.color = unlocked ? new Color(0.86f, 0.82f, 0.68f) : new Color(0.64f, 0.62f, 0.62f);
            flowText.textWrappingMode = TextWrappingModes.Normal;
            flowText.text = BuildFlowSummary(caseData);

            var rightGo = new GameObject("Right");
            rightGo.transform.SetParent(rowGo.transform, false);
            rightGo.AddComponent<LayoutElement>().preferredWidth = 260;
            var rightLayout = rightGo.AddComponent<VerticalLayoutGroup>();
            rightLayout.childControlHeight = true;
            rightLayout.childControlWidth = true;
            rightLayout.childForceExpandHeight = false;
            rightLayout.childAlignment = TextAnchor.MiddleRight;
            rightLayout.spacing = 8;

            var starsGo = new GameObject("Stars");
            starsGo.transform.SetParent(rightGo.transform, false);
            var stars = starsGo.AddComponent<TextMeshProUGUI>();
            if (font != null) stars.font = font;
            stars.fontSize = 28;
            stars.alignment = TextAlignmentOptions.Right;
            stars.color = Color.white;
            stars.text = BuildStars(PlayerProfile.GetCaseStars(caseData.caseId));

            var locationGo = new GameObject("Location");
            locationGo.transform.SetParent(rightGo.transform, false);
            var location = locationGo.AddComponent<TextMeshProUGUI>();
            if (font != null) location.font = font;
            location.fontSize = 16;
            location.alignment = TextAlignmentOptions.Right;
            location.color = new Color(0.86f, 0.82f, 0.68f);
            location.text = caseData.GetResolvedLocation(0)?.displayName ?? "Launch via city map";

            var statusGo = new GameObject("Status");
            statusGo.transform.SetParent(rightGo.transform, false);
            var status = statusGo.AddComponent<TextMeshProUGUI>();
            if (font != null) status.font = font;
            status.fontSize = 18;
            status.textWrappingMode = TextWrappingModes.Normal;
            status.alignment = TextAlignmentOptions.Right;
            status.color = unlocked ? new Color(0.62f, 0.86f, 0.66f) : new Color(0.86f, 0.60f, 0.52f);
            status.text = unlocked
                ? BuildLaunchStatus(caseData)
                : lockReason;
        }

        static string GetBriefPreview(CaseData caseData)
        {
            if (caseData == null || string.IsNullOrWhiteSpace(caseData.briefText))
                return string.Empty;

            return caseData.briefText.Length > 78
                ? caseData.briefText[..78] + "..."
                : caseData.briefText;
        }

        static string BuildFlowSummary(CaseData caseData)
        {
            string suspectSummary = caseData.HasSuspectPresentation()
                ? $"{caseData.suspectSummaries.Count} suspect lead(s)"
                : "Scene-only contradiction";
            string interrogationSummary = caseData.HasInterrogationPresentation()
                ? (caseData.interrogationNodes.Count > 0 ? "interrogation ready" : "interrogation-forward")
                : "evidence-first";
            return $"{suspectSummary} • {interrogationSummary}";
        }

        static string BuildLaunchStatus(CaseData caseData)
        {
            int bestScore = PlayerProfile.GetCaseBestScore(caseData.caseId);
            string scoreText = bestScore > 0 ? $"{bestScore:N0} pts" : "Unplayed";
            string locationText = caseData.GetResolvedLocationCount() > 1
                ? $"{caseData.GetResolvedLocationCount()} visit route"
                : "Single location";
            return $"{scoreText} • {locationText}";
        }

        static string BuildStars(int stars)
        {
            const string filled = "<color=#F2C94C>★</color>";
            const string empty = "<color=#5D6070>★</color>";
            int clamped = Mathf.Clamp(stars, 0, 3);
            return string.Concat(Enumerable.Repeat(filled, clamped)) +
                   string.Concat(Enumerable.Repeat(empty, 3 - clamped));
        }

        void SelectCase(CaseData caseData, int index)
        {
            GameManager.Instance?.LoadCaseByIndex(index);
            GameScreenController.Instance?.ResetEntryState();
            NavigationManager.Instance?.ResetToRootChild(ScreenId.Game);
        }
    }
}
