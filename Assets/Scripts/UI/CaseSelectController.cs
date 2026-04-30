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

        [SerializeField] Color rowColor = new Color(0.16f, 0.16f, 0.24f);
        [SerializeField] Color rowHoverColor = new Color(0.22f, 0.22f, 0.34f);

        public override ScreenId ScreenId => ScreenId.CaseSelect;

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
                CreateDepartmentHeader(font, lockStatus, overrideTitle: null);

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
                    CreateCaseRow(caseData, caseIndexLookup[caseData.caseId], font, unlocked: true, lockReason: "Unlocked");
            }

            Canvas.ForceUpdateCanvases();
            if (listParent is RectTransform listRT)
                LayoutRebuilder.ForceRebuildLayoutImmediate(listRT);
        }

        void CreateDepartmentHeader(TMP_FontAsset font, DepartmentLockStatus lockStatus, string overrideTitle)
        {
            string titleText = overrideTitle ?? lockStatus.Department?.displayName ?? "Department";

            var headerGo = new GameObject($"Department_{titleText}");
            headerGo.transform.SetParent(listParent, false);
            headerGo.AddComponent<LayoutElement>().preferredHeight = 78;
            headerGo.AddComponent<Image>().color = lockStatus.IsUnlocked
                ? new Color(0.20f, 0.16f, 0.10f)
                : new Color(0.14f, 0.12f, 0.12f);

            var layout = headerGo.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 14, 14);
            layout.spacing = 12;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = false;

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(headerGo.transform, false);
            var title = titleGo.AddComponent<TextMeshProUGUI>();
            if (font != null) title.font = font;
            title.fontSize = 28;
            title.fontStyle = FontStyles.Bold;
            title.color = lockStatus.IsUnlocked ? new Color(0.95f, 0.88f, 0.65f) : new Color(0.85f, 0.78f, 0.72f);
            title.text = titleText;
            titleGo.AddComponent<LayoutElement>().flexibleWidth = 1;

            var requirementGo = new GameObject("Requirement");
            requirementGo.transform.SetParent(headerGo.transform, false);
            var requirement = requirementGo.AddComponent<TextMeshProUGUI>();
            if (font != null) requirement.font = font;
            requirement.fontSize = 20;
            requirement.alignment = TextAlignmentOptions.Right;
            requirement.color = lockStatus.IsUnlocked ? new Color(0.62f, 0.86f, 0.66f) : new Color(0.86f, 0.60f, 0.52f);
            requirement.text = lockStatus.RequirementText;
            requirementGo.AddComponent<LayoutElement>().preferredWidth = 360;
        }

        void CreateCaseRow(CaseData caseData, int index, TMP_FontAsset font, bool unlocked, string lockReason)
        {
            var rowGo = new GameObject($"CaseRow_{caseData.caseId}");
            rowGo.transform.SetParent(listParent, false);
            rowGo.AddComponent<LayoutElement>().preferredHeight = 124;

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

            var rightGo = new GameObject("Right");
            rightGo.transform.SetParent(rowGo.transform, false);
            rightGo.AddComponent<LayoutElement>().preferredWidth = 240;
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

            var statusGo = new GameObject("Status");
            statusGo.transform.SetParent(rightGo.transform, false);
            var status = statusGo.AddComponent<TextMeshProUGUI>();
            if (font != null) status.font = font;
            status.fontSize = 18;
            status.alignment = TextAlignmentOptions.Right;
            status.color = unlocked ? new Color(0.62f, 0.86f, 0.66f) : new Color(0.86f, 0.60f, 0.52f);
            status.text = unlocked
                ? $"{PlayerProfile.GetCaseBestScore(caseData.caseId):N0} pts"
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
            NavigationManager.Instance?.PopToRootImmediate();
            GameManager.Instance?.LoadCaseByIndex(index);
            GameScreenController.Instance?.ResetEntryState();
            NavigationManager.Instance?.Push(ScreenId.Game, TransitionType.FadeUp);
        }
    }
}
