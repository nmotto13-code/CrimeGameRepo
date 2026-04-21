using UnityEngine;

namespace CasebookGame.Core
{
    public static class PlayerProfile
    {
        const string KEY_TOTAL_SCORE     = "Profile_TotalScore";
        const string KEY_CASES_COMPLETED = "Profile_CasesCompleted";
        const string KEY_PERFECT_SOLVES  = "Profile_PerfectSolves";

        static string BestScoreKey(string caseId) => $"BestScore_{caseId}";

        public static void AddCaseResult(string caseId, int score, bool perfect)
        {
            int prev = GetCaseBestScore(caseId);
            bool improved = score > prev;

            // Accumulate totals only when this is the player's best score for this case
            // (prevents retry inflation from adding score multiple times)
            if (improved)
            {
                PlayerPrefs.SetInt(KEY_TOTAL_SCORE,     GetTotalScore() + (score - prev));
                PlayerPrefs.SetInt(KEY_CASES_COMPLETED, GetCasesCompleted() + (prev == 0 ? 1 : 0));
                if (perfect && prev == 0)
                    PlayerPrefs.SetInt(KEY_PERFECT_SOLVES, GetPerfectSolves() + 1);
                PlayerPrefs.SetInt(BestScoreKey(caseId), score);
                PlayerPrefs.Save();
            }

            Debug.Log($"[PlayerProfile] Case {caseId} — score {score} (best {Mathf.Max(prev, score)})" +
                      $" | Total: {GetTotalScore()} | Cases: {GetCasesCompleted()} | Perfect: {GetPerfectSolves()}");
        }

        public static int GetTotalScore()     => PlayerPrefs.GetInt(KEY_TOTAL_SCORE,     0);
        public static int GetCasesCompleted() => PlayerPrefs.GetInt(KEY_CASES_COMPLETED, 0);
        public static int GetPerfectSolves()  => PlayerPrefs.GetInt(KEY_PERFECT_SOLVES,  0);

        public static int GetCaseBestScore(string caseId) =>
            PlayerPrefs.GetInt(BestScoreKey(caseId), 0);

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(KEY_TOTAL_SCORE);
            PlayerPrefs.DeleteKey(KEY_CASES_COMPLETED);
            PlayerPrefs.DeleteKey(KEY_PERFECT_SOLVES);
            PlayerPrefs.Save();
        }

        public static void HardReset()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }
    }
}
