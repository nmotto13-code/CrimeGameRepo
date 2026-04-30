using System;
using System.Collections.Generic;
using System.Linq;
using CasebookGame.Data;
using UnityEngine;

namespace CasebookGame.Core
{
    public static class ProgressionRules
    {
        public const int MaxRank = 30;

        public const int BaseCaseXp = 100;
        public const int StarOneBonusXp = 20;
        public const int StarTwoBonusXp = 35;
        public const int StarThreeBonusXp = 45;
        public const int PerfectSolveBonusXp = 50;

        static readonly AchievementDefinition[] AchievementDefinitions =
        {
            new("first_case", "Open and Shut", "Solve your first case file."),
            new("five_stars", "Five-Star Beat", "Earn 5 total case stars."),
            new("perfect_one", "Clean Collar", "Record your first 3-star solve."),
            new("no_wrong_three", "Steady Hand", "Solve 3 cases without a wrong guess."),
            new("all_evidence_five", "Scene Sweeper", "Fully clear the evidence on 5 cases."),
            new("speed_three", "Clockwork", "Earn the speed star on 3 cases."),
            new("no_tools_one", "Bare Hands", "Solve a case without using any tools."),
            new("streak_three", "Three-Day Run", "Keep a 3-day daily case streak."),
            new("streak_seven", "Week on the Beat", "Keep a 7-day daily case streak."),
            new("rank_ten", "Rising Shield", "Reach detective rank 10.")
        };

        static DepartmentData[] cachedFallbackDepartments;

        public static IReadOnlyList<AchievementDefinition> GetAchievementDefinitions() => AchievementDefinitions;

        public static RankProgressInfo GetRankProgress(int xp)
        {
            int rank = GetRankForXp(xp);
            return new RankProgressInfo
            {
                Rank = rank,
                CurrentXp = xp,
                CurrentRankStartXp = GetXpThresholdForRank(rank),
                NextRankXp = rank >= MaxRank ? GetXpThresholdForRank(MaxRank) : GetXpThresholdForRank(rank + 1)
            };
        }

        public static int GetRankForXp(int xp)
        {
            int clampedXp = Mathf.Max(0, xp);
            for (int rank = MaxRank; rank >= 1; rank--)
            {
                if (clampedXp >= GetXpThresholdForRank(rank))
                    return rank;
            }

            return 1;
        }

        public static int GetXpThresholdForRank(int rank)
        {
            int clampedRank = Mathf.Clamp(rank, 1, MaxRank);
            int total = 0;

            for (int level = 2; level <= clampedRank; level++)
                total += 150 + ((level - 2) * 50);

            return total;
        }

        public static int CalculateStars(CaseData caseData, bool solved, bool noWrongGuesses, bool allEvidenceFound, float elapsedSeconds)
        {
            if (!solved || caseData == null)
                return 0;

            int stars = 1;
            if (noWrongGuesses)
                stars++;
            if (EarnedThirdStar(caseData, allEvidenceFound, elapsedSeconds))
                stars++;
            return Mathf.Clamp(stars, 0, 3);
        }

        public static bool EarnedThirdStar(CaseData caseData, bool allEvidenceFound, float elapsedSeconds)
        {
            if (caseData == null)
                return false;

            switch (caseData.thirdStarRequirement)
            {
                case ThirdStarRequirementType.UnderParTime:
                    float parSeconds = caseData.GetThirdStarParSeconds();
                    return parSeconds > 0f && elapsedSeconds > 0f && elapsedSeconds <= parSeconds;

                case ThirdStarRequirementType.AllEvidenceDiscovered:
                default:
                    return allEvidenceFound;
            }
        }

        public static int CalculateCaseMasteryXpValue(int starsEarned)
        {
            int xp = BaseCaseXp;

            if (starsEarned >= 1) xp += StarOneBonusXp;
            if (starsEarned >= 2) xp += StarTwoBonusXp;
            if (starsEarned >= 3) xp += StarThreeBonusXp + PerfectSolveBonusXp;

            return xp;
        }

        public static IReadOnlyList<DepartmentData> GetDepartments(CaseData[] cases)
        {
            var loaded = Resources.LoadAll<DepartmentData>("Departments")
                .Where(d => d != null)
                .OrderBy(d => d.requiredRank)
                .ThenBy(d => d.requiredStarsCount)
                .ThenBy(d => d.displayName)
                .ToArray();

            if (loaded.Length > 0)
                return loaded;

            return GetFallbackDepartments(cases);
        }

        public static DepartmentData GetDepartmentForCase(string caseId, IReadOnlyList<DepartmentData> departments)
        {
            if (departments == null || string.IsNullOrWhiteSpace(caseId))
                return null;

            for (int i = 0; i < departments.Count; i++)
            {
                var department = departments[i];
                if (department != null && department.ContainsCase(caseId))
                    return department;
            }

            return null;
        }

        public static bool IsDepartmentUnlocked(DepartmentData department, int rank, int totalStars)
        {
            if (department == null)
                return true;

            return rank >= Mathf.Max(1, department.requiredRank)
                && totalStars >= Mathf.Max(0, department.requiredStarsCount);
        }

        public static DepartmentLockStatus GetDepartmentLockStatus(DepartmentData department, int rank, int totalStars)
        {
            bool unlocked = IsDepartmentUnlocked(department, rank, totalStars);
            return new DepartmentLockStatus
            {
                Department = department,
                IsUnlocked = unlocked,
                RequirementText = unlocked
                    ? "Unlocked"
                    : $"Requires Rank {Mathf.Max(1, department.requiredRank)} and {Mathf.Max(0, department.requiredStarsCount)} stars"
            };
        }

        public static IReadOnlyList<CaseData> GetUnlockedCases(CaseData[] allCases, IReadOnlyList<DepartmentData> departments, int rank, int totalStars)
        {
            if (allCases == null || allCases.Length == 0)
                return Array.Empty<CaseData>();

            var unlocked = new List<CaseData>(allCases.Length);
            foreach (var caseData in allCases)
            {
                if (caseData == null)
                    continue;

                var department = GetDepartmentForCase(caseData.caseId, departments);
                if (department == null || IsDepartmentUnlocked(department, rank, totalStars))
                    unlocked.Add(caseData);
            }

            return unlocked;
        }

        public static DailyCaseInfo GetDailyCaseInfo(CaseData[] allCases, IReadOnlyList<DepartmentData> departments, int rank, int totalStars, int dayOrdinal, int lastSolvedDayOrdinal)
        {
            var unlocked = GetUnlockedCases(allCases, departments, rank, totalStars)
                .Where(c => c != null)
                .OrderBy(c => c.caseId)
                .ToList();

            if (unlocked.Count == 0)
            {
                return new DailyCaseInfo
                {
                    CaseId = string.Empty,
                    DayOrdinal = dayOrdinal,
                    IsSolvedToday = false
                };
            }

            int index = Mathf.Abs((dayOrdinal * 97) + (unlocked.Count * 13)) % unlocked.Count;
            return new DailyCaseInfo
            {
                CaseId = unlocked[index].caseId,
                DayOrdinal = dayOrdinal,
                IsSolvedToday = lastSolvedDayOrdinal == dayOrdinal
            };
        }

        static IReadOnlyList<DepartmentData> GetFallbackDepartments(CaseData[] cases)
        {
            bool needsRefresh = cachedFallbackDepartments == null
                || (cases != null
                    && cases.Length > 0
                    && cachedFallbackDepartments.Length > 0
                    && (cachedFallbackDepartments[0] == null || cachedFallbackDepartments[0].caseIds == null || cachedFallbackDepartments[0].caseIds.Count == 0));

            if (!needsRefresh)
                return cachedFallbackDepartments;

            var fallback = ScriptableObject.CreateInstance<DepartmentData>();
            fallback.departmentId = DepartmentId.Patrol;
            fallback.displayName = "Patrol (Training)";
            fallback.requiredRank = 1;
            fallback.requiredStarsCount = 0;
            fallback.caseIds = new List<string>();

            if (cases != null)
            {
                foreach (var caseData in cases.Where(c => c != null).OrderBy(c => c.caseId))
                    fallback.caseIds.Add(caseData.caseId);
            }

            cachedFallbackDepartments = new[] { fallback };
            return cachedFallbackDepartments;
        }
    }
}
