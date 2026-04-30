using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CasebookGame.Data;

namespace CasebookGame.Core
{
    public static class PlayerProfile
    {
        const string KEY_TOTAL_SCORE = "Profile_TotalScore";
        const string KEY_CASES_COMPLETED = "Profile_CasesCompleted";
        const string KEY_PERFECT_SOLVES = "Profile_PerfectSolves";
        const string KEY_PROFILE_SAVE_V2 = "Profile_SaveV2";

        static string BestScoreKey(string caseId) => $"BestScore_{caseId}";

        static ProfileSaveData saveCache;
        static bool saveLoaded;

        public static void AddCaseResult(string caseId, int score, bool perfect)
        {
            RegisterLegacyScore(caseId, score, perfect);
        }

        public static int GetTotalScore() => PlayerPrefs.GetInt(KEY_TOTAL_SCORE, 0);
        public static int GetCasesCompleted() => PlayerPrefs.GetInt(KEY_CASES_COMPLETED, 0);
        public static int GetPerfectSolves() => PlayerPrefs.GetInt(KEY_PERFECT_SOLVES, 0);

        public static int GetCaseBestScore(string caseId) =>
            PlayerPrefs.GetInt(BestScoreKey(caseId), 0);

        public static int GetCaseStars(string caseId) =>
            GetOrCreateCaseProgress(caseId).bestStars;

        public static int GetXp()
        {
            EnsureSaveLoaded();
            return saveCache.xp;
        }

        public static int GetRank() => ProgressionRules.GetRankForXp(GetXp());

        public static RankProgressInfo GetRankProgress() =>
            ProgressionRules.GetRankProgress(GetXp());

        public static int GetTotalStars(IEnumerable<CaseData> cases)
        {
            if (cases == null)
                return 0;

            int total = 0;
            foreach (var caseData in cases)
            {
                if (caseData == null)
                    continue;
                total += GetCaseStars(caseData.caseId);
            }

            return total;
        }

        public static int GetCurrentStreak()
        {
            EnsureSaveLoaded();
            return saveCache.currentStreak;
        }

        public static int GetBestStreak()
        {
            EnsureSaveLoaded();
            return saveCache.bestStreak;
        }

        public static int GetSafeTodayOrdinal()
        {
            EnsureSaveLoaded();

            int today = DateTime.Now.Date.Subtract(DateTime.UnixEpoch.Date).Days;
            int safeToday = Mathf.Max(today, saveCache.lastSafeDayOrdinal);
            if (safeToday != saveCache.lastSafeDayOrdinal)
            {
                saveCache.lastSafeDayOrdinal = safeToday;
                SaveProgress();
            }

            return safeToday;
        }

        public static DailyCaseInfo GetDailyCaseInfo(CaseData[] allCases, IReadOnlyList<DepartmentData> departments)
        {
            int today = GetSafeTodayOrdinal();
            return ProgressionRules.GetDailyCaseInfo(
                allCases,
                departments,
                GetRank(),
                GetTotalStars(allCases),
                today,
                GetLastDailySolvedDayOrdinal());
        }

        public static IReadOnlyList<AchievementStatus> GetAchievements(CaseData[] allCases)
        {
            EnsureSaveLoaded();
            var newUnlocks = UnlockAchievementsIfNeeded(allCases);
            if (newUnlocks.Count > 0)
                SaveProgress();

            var unlockedLookup = saveCache.unlockedAchievements
                .Where(a => a != null && !string.IsNullOrWhiteSpace(a.achievementId))
                .ToDictionary(a => a.achievementId, a => a.unlockedDayOrdinal);

            return ProgressionRules.GetAchievementDefinitions()
                .Select(def => new AchievementStatus
                {
                    Definition = def,
                    IsUnlocked = unlockedLookup.TryGetValue(def.Id, out _),
                    UnlockedDayOrdinal = unlockedLookup.TryGetValue(def.Id, out int dayOrdinal) ? dayOrdinal : -1
                })
                .ToList();
        }

        public static ProfileUpdateResult RegisterCaseCompletion(CaseCompletionSummary summary)
        {
            EnsureSaveLoaded();

            var allCases = GameManager.Instance?.availableCases;
            int previousRank = GetRank();
            int previousStars = GetTotalStars(allCases);
            var record = GetOrCreateCaseProgress(summary.caseId);

            RegisterLegacyScore(summary.caseId, summary.score, false);

            record.solved = true;
            record.completionCount++;
            record.bestScore = Mathf.Max(record.bestScore, summary.score);
            record.bestStars = Mathf.Max(record.bestStars, summary.starsEarned);
            record.hasNoWrongGuessSolve |= summary.noWrongGuesses;
            record.hasAllEvidenceSolve |= summary.allEvidenceFound;
            record.hasUnderParSolve |= summary.earnedUnderParStar;
            record.hasNoToolsSolve |= summary.noToolsUsed;
            record.bestElapsedSeconds = record.bestElapsedSeconds <= 0f
                ? summary.elapsedSeconds
                : Mathf.Min(record.bestElapsedSeconds, summary.elapsedSeconds);

            int xpGain = Mathf.Max(0, summary.masteryXpValue - record.bestXpAwarded);
            if (xpGain > 0)
            {
                saveCache.xp += xpGain;
                record.bestXpAwarded = summary.masteryXpValue;
            }

            if (summary.earnedPerfectSolve && !record.perfectSolved)
            {
                record.perfectSolved = true;
                PlayerPrefs.SetInt(KEY_PERFECT_SOLVES, GetPerfectSolves() + 1);
            }

            bool dailySolved = RegisterDailySolve(summary.caseId);
            var newlyUnlockedAchievements = UnlockAchievementsIfNeeded(allCases);
            SaveProgress();

            return new ProfileUpdateResult
            {
                XpGained = xpGain,
                PreviousRank = previousRank,
                NewRank = GetRank(),
                PreviousStars = previousStars,
                NewStars = GetTotalStars(allCases),
                DailyCaseSolved = dailySolved,
                CurrentStreak = GetCurrentStreak(),
                NewlyUnlockedAchievementIds = newlyUnlockedAchievements
            };
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(KEY_TOTAL_SCORE);
            PlayerPrefs.DeleteKey(KEY_CASES_COMPLETED);
            PlayerPrefs.DeleteKey(KEY_PERFECT_SOLVES);
            PlayerPrefs.DeleteKey(KEY_PROFILE_SAVE_V2);
            PlayerPrefs.Save();
            saveCache = null;
            saveLoaded = false;
        }

        public static void HardReset()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            saveCache = null;
            saveLoaded = false;
        }

        static void EnsureSaveLoaded()
        {
            if (saveLoaded)
                return;

            string json = PlayerPrefs.GetString(KEY_PROFILE_SAVE_V2, string.Empty);
            saveCache = string.IsNullOrWhiteSpace(json)
                ? new ProfileSaveData()
                : JsonUtility.FromJson<ProfileSaveData>(json);

            if (saveCache == null)
                saveCache = new ProfileSaveData();

            saveCache.caseProgress ??= new List<CaseProgressRecord>();
            saveCache.unlockedAchievements ??= new List<AchievementUnlockRecord>();
            saveLoaded = true;
        }

        static void SaveProgress()
        {
            EnsureSaveLoaded();
            PlayerPrefs.SetString(KEY_PROFILE_SAVE_V2, JsonUtility.ToJson(saveCache));
            PlayerPrefs.Save();
        }

        static CaseProgressRecord GetOrCreateCaseProgress(string caseId)
        {
            EnsureSaveLoaded();

            for (int i = 0; i < saveCache.caseProgress.Count; i++)
            {
                var record = saveCache.caseProgress[i];
                if (record != null && record.caseId == caseId)
                    return record;
            }

            int legacyBestScore = GetCaseBestScore(caseId);
            var migrated = new CaseProgressRecord
            {
                caseId = caseId,
                bestScore = legacyBestScore,
                solved = legacyBestScore > 0,
                bestStars = legacyBestScore > 0 ? 1 : 0
            };
            saveCache.caseProgress.Add(migrated);
            SaveProgress();
            return migrated;
        }

        static void RegisterLegacyScore(string caseId, int score, bool incrementPerfectSolve)
        {
            int previousScore = GetCaseBestScore(caseId);
            bool improved = score > previousScore;

            if (improved)
            {
                PlayerPrefs.SetInt(KEY_TOTAL_SCORE, GetTotalScore() + (score - previousScore));
                PlayerPrefs.SetInt(KEY_CASES_COMPLETED, GetCasesCompleted() + (previousScore == 0 ? 1 : 0));
                PlayerPrefs.SetInt(BestScoreKey(caseId), score);
            }

            if (incrementPerfectSolve)
                PlayerPrefs.SetInt(KEY_PERFECT_SOLVES, GetPerfectSolves() + 1);

            PlayerPrefs.Save();
        }

        static bool RegisterDailySolve(string caseId)
        {
            var allCases = GameManager.Instance?.availableCases;
            var departments = ProgressionRules.GetDepartments(allCases);
            var dailyCase = GetDailyCaseInfo(allCases, departments);
            if (string.IsNullOrWhiteSpace(dailyCase.CaseId) || dailyCase.CaseId != caseId)
                return false;

            EnsureSaveLoaded();
            if (saveCache.lastDailySolvedDayOrdinal == dailyCase.DayOrdinal)
                return true;

            if (saveCache.lastDailySolvedDayOrdinal == dailyCase.DayOrdinal - 1)
                saveCache.currentStreak++;
            else
                saveCache.currentStreak = 1;

            saveCache.lastDailySolvedDayOrdinal = dailyCase.DayOrdinal;
            saveCache.bestStreak = Mathf.Max(saveCache.bestStreak, saveCache.currentStreak);
            return true;
        }

        static int GetLastDailySolvedDayOrdinal()
        {
            EnsureSaveLoaded();
            return saveCache.lastDailySolvedDayOrdinal;
        }

        static List<string> UnlockAchievementsIfNeeded(CaseData[] allCases)
        {
            EnsureSaveLoaded();

            var newlyUnlocked = new List<string>();
            var alreadyUnlocked = new HashSet<string>(
                saveCache.unlockedAchievements
                    .Where(a => a != null && !string.IsNullOrWhiteSpace(a.achievementId))
                    .Select(a => a.achievementId));

            var progress = new AchievementProgressSnapshot
            {
                CasesCompleted = GetCasesCompleted(),
                TotalStars = GetTotalStars(allCases),
                PerfectSolves = GetPerfectSolves(),
                NoWrongGuessCases = CountCases(r => r.hasNoWrongGuessSolve),
                AllEvidenceCases = CountCases(r => r.hasAllEvidenceSolve),
                SpeedStarCases = CountCases(r => r.hasUnderParSolve),
                NoToolCases = CountCases(r => r.hasNoToolsSolve),
                CurrentStreak = GetCurrentStreak(),
                Rank = GetRank()
            };

            foreach (var definition in ProgressionRules.GetAchievementDefinitions())
            {
                if (alreadyUnlocked.Contains(definition.Id) || !IsAchievementMet(definition.Id, progress))
                    continue;

                saveCache.unlockedAchievements.Add(new AchievementUnlockRecord
                {
                    achievementId = definition.Id,
                    unlockedDayOrdinal = GetSafeTodayOrdinal()
                });
                newlyUnlocked.Add(definition.Id);
            }

            return newlyUnlocked;
        }

        static int CountCases(Func<CaseProgressRecord, bool> predicate)
        {
            EnsureSaveLoaded();

            int count = 0;
            foreach (var record in saveCache.caseProgress)
            {
                if (record != null && predicate(record))
                    count++;
            }

            return count;
        }

        static bool IsAchievementMet(string achievementId, AchievementProgressSnapshot progress) => achievementId switch
        {
            "first_case" => progress.CasesCompleted >= 1,
            "five_stars" => progress.TotalStars >= 5,
            "perfect_one" => progress.PerfectSolves >= 1,
            "no_wrong_three" => progress.NoWrongGuessCases >= 3,
            "all_evidence_five" => progress.AllEvidenceCases >= 5,
            "speed_three" => progress.SpeedStarCases >= 3,
            "no_tools_one" => progress.NoToolCases >= 1,
            "streak_three" => progress.CurrentStreak >= 3,
            "streak_seven" => progress.CurrentStreak >= 7,
            "rank_ten" => progress.Rank >= 10,
            _ => false
        };

        struct AchievementProgressSnapshot
        {
            public int CasesCompleted;
            public int TotalStars;
            public int PerfectSolves;
            public int NoWrongGuessCases;
            public int AllEvidenceCases;
            public int SpeedStarCases;
            public int NoToolCases;
            public int CurrentStreak;
            public int Rank;
        }
    }
}
