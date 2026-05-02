using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CasebookGame.Core;
using CasebookGame.Data;

namespace CasebookGame.UI
{
    public class HomeScreenController : BaseScreen
    {
        [SerializeField] Button investigateBtn;
        [SerializeField] Button viewProfileBtn;
        [SerializeField] Button testCasesBtn;
        [SerializeField] TMP_Text rankSummaryText;
        [SerializeField] TMP_Text streakSummaryText;
        [SerializeField] TMP_Text dailyCaseTitleText;
        [SerializeField] TMP_Text dailyCaseStatusText;
        [SerializeField] TMP_Text activeDepartmentText;
        [SerializeField] TMP_Text arcTeaserText;

        [SerializeField] Button[] caseJumpBtns;
        [SerializeField] string[] caseJumpIds;

        public override ScreenId ScreenId => ScreenId.Home;

        protected override void Awake()
        {
            base.Awake();

            investigateBtn?.onClick.AddListener(() =>
                NavigationManager.Instance?.Push(ScreenId.CityMap, TransitionType.SlideLeft));

            viewProfileBtn?.onClick.AddListener(() =>
                NavigationManager.Instance?.Push(ScreenId.Account, TransitionType.SlideLeft));

            testCasesBtn?.onClick.AddListener(() =>
                NavigationManager.Instance?.Push(ScreenId.CaseSelect, TransitionType.SlideLeft));

            if (caseJumpBtns == null || caseJumpIds == null)
                return;

            for (int i = 0; i < caseJumpBtns.Length; i++)
            {
                if (caseJumpBtns[i] == null)
                    continue;

                string id = i < caseJumpIds.Length ? caseJumpIds[i] : null;
                if (string.IsNullOrEmpty(id))
                    continue;

                string capturedId = id;
                caseJumpBtns[i].onClick.AddListener(() =>
                {
                    int idx = GameManager.Instance?.IndexOfCase(capturedId) ?? -1;
                    if (idx < 0)
                    {
                        Debug.LogWarning($"[Home] Case not found: {capturedId}");
                        return;
                    }

                    GameManager.Instance.LoadCaseByIndex(idx);
                    NavigationManager.Instance?.Push(ScreenId.Game, TransitionType.FadeUp);
                });
            }
        }

        public override void OnScreenEnter()
        {
            gameObject.SetActive(true);
            RefreshOverview();
        }

        void RefreshOverview()
        {
            var cases = GameManager.Instance?.availableCases ?? Array.Empty<CaseData>();
            var departments = ProgressionRules.GetDepartments(cases);
            var dailyCase = PlayerProfile.GetDailyCaseInfo(cases, departments);
            var dailyCaseData = Array.Find(cases, c => c != null && c.caseId == dailyCase.CaseId);
            var activeDepartment = ResolveActiveDepartment(cases, departments, dailyCaseData);
            var nextDepartment = ResolveNextLockedDepartment(cases, departments);

            if (rankSummaryText != null)
            {
                var progress = PlayerProfile.GetRankProgress();
                rankSummaryText.text = $"Rank {progress.Rank} | {PlayerProfile.GetXp():N0} XP";
            }

            if (streakSummaryText != null)
                streakSummaryText.text = $"Streak {PlayerProfile.GetCurrentStreak()} | Stars {PlayerProfile.GetTotalStars(cases)}";

            if (dailyCaseTitleText != null)
                dailyCaseTitleText.text = dailyCaseData != null ? dailyCaseData.title : "No daily case available";

            if (dailyCaseStatusText != null)
                dailyCaseStatusText.text = dailyCase.IsSolvedToday
                    ? "Daily case solved. Return tomorrow to extend the streak."
                    : nextDepartment != null
                        ? $"Open the city map to work today's file. Next promotion: {nextDepartment.displayName}."
                        : "Open the city map to work today's file.";

            if (activeDepartmentText != null)
                activeDepartmentText.text = activeDepartment != null
                    ? $"{activeDepartment.displayName} active"
                    : "Patrol board active";

            if (arcTeaserText != null)
                arcTeaserText.text = dailyCaseData != null && !string.IsNullOrWhiteSpace(dailyCaseData.arcBeatSummary)
                    ? dailyCaseData.arcBeatSummary
                    : nextDepartment != null && !string.IsNullOrWhiteSpace(nextDepartment.unlockBlurb)
                        ? nextDepartment.unlockBlurb
                        : "Follow department leads, uncover patterns, and connect recurring suspects.";
        }

        static DepartmentData ResolveActiveDepartment(CaseData[] cases, IReadOnlyList<DepartmentData> departments, CaseData dailyCaseData)
        {
            if (dailyCaseData != null)
            {
                var dailyDepartment = ProgressionRules.GetDepartmentForCase(dailyCaseData.caseId, departments);
                if (dailyDepartment != null)
                    return dailyDepartment;
            }

            int rank = PlayerProfile.GetRank();
            int stars = PlayerProfile.GetTotalStars(cases);
            foreach (var department in departments)
            {
                if (department != null && ProgressionRules.IsDepartmentUnlocked(department, rank, stars))
                    return department;
            }

            return null;
        }

        static DepartmentData ResolveNextLockedDepartment(CaseData[] cases, IReadOnlyList<DepartmentData> departments)
        {
            int rank = PlayerProfile.GetRank();
            int stars = PlayerProfile.GetTotalStars(cases);
            return departments
                .Where(department => department != null && !ProgressionRules.IsDepartmentUnlocked(department, rank, stars))
                .OrderBy(department => department.requiredRank)
                .ThenBy(department => department.requiredStarsCount)
                .FirstOrDefault();
        }
    }
}
