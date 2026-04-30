using System;
using System.Collections.Generic;
using CasebookGame.Data;

namespace CasebookGame.Core
{
    [Serializable]
    public class CaseCompletionSummary
    {
        public string caseId;
        public int score;
        public bool solved;
        public bool noWrongGuesses;
        public bool allEvidenceFound;
        public bool noToolsUsed;
        public float elapsedSeconds;
        public int starsEarned;
        public bool earnedUnderParStar;
        public bool earnedPerfectSolve;
        public int masteryXpValue;
    }

    [Serializable]
    public class CaseProgressRecord
    {
        public string caseId;
        public int bestScore;
        public int bestStars;
        public int bestXpAwarded;
        public int completionCount;
        public float bestElapsedSeconds;
        public bool solved;
        public bool perfectSolved;
        public bool hasNoWrongGuessSolve;
        public bool hasAllEvidenceSolve;
        public bool hasUnderParSolve;
        public bool hasNoToolsSolve;
    }

    [Serializable]
    public class AchievementUnlockRecord
    {
        public string achievementId;
        public int unlockedDayOrdinal;
    }

    [Serializable]
    public class ProfileSaveData
    {
        public int version = 2;
        public int xp;
        public int currentStreak;
        public int bestStreak;
        public int lastDailySolvedDayOrdinal = -1;
        public int lastSafeDayOrdinal = -1;
        public List<CaseProgressRecord> caseProgress = new List<CaseProgressRecord>();
        public List<AchievementUnlockRecord> unlockedAchievements = new List<AchievementUnlockRecord>();
    }

    public struct RankProgressInfo
    {
        public int Rank;
        public int CurrentXp;
        public int CurrentRankStartXp;
        public int NextRankXp;

        public float Normalized =>
            NextRankXp <= CurrentRankStartXp
                ? 1f
                : UnityEngine.Mathf.Clamp01((CurrentXp - CurrentRankStartXp) / (float)(NextRankXp - CurrentRankStartXp));
    }

    public struct DailyCaseInfo
    {
        public string CaseId;
        public int DayOrdinal;
        public bool IsSolvedToday;
    }

    public struct ProfileUpdateResult
    {
        public int XpGained;
        public int PreviousRank;
        public int NewRank;
        public int PreviousStars;
        public int NewStars;
        public bool DailyCaseSolved;
        public int CurrentStreak;
        public List<string> NewlyUnlockedAchievementIds;
    }

    public struct AchievementDefinition
    {
        public string Id;
        public string Title;
        public string Description;

        public AchievementDefinition(string id, string title, string description)
        {
            Id = id;
            Title = title;
            Description = description;
        }
    }

    public struct AchievementStatus
    {
        public AchievementDefinition Definition;
        public bool IsUnlocked;
        public int UnlockedDayOrdinal;
    }

    public struct DepartmentLockStatus
    {
        public DepartmentData Department;
        public bool IsUnlocked;
        public string RequirementText;
    }
}
