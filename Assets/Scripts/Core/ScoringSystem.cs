using System;
using UnityEngine;
using TMPro;
using CasebookGame.Data;

namespace CasebookGame.Core
{
    [DefaultExecutionOrder(-10)]
    public class ScoringSystem : MonoBehaviour
    {
        public static ScoringSystem Instance { get; private set; }

        [SerializeField] TMP_Text timerText;

        public event Action<int, int, int, int> OnScoreCalculated;
        public event Action                     OnTimeLimitExpired;

        public int LastCaseScore     { get; private set; }
        public int LastBasePoints    { get; private set; }
        public int LastEvidenceBonus { get; private set; }
        public int LastTimeBonus     { get; private set; }
        public int LastMultiplier    { get; private set; }

        float    caseStartTime;
        int      evidenceFoundCount;
        CaseData currentCase;
        bool     timerActive;
        bool     timeExpired;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            if (EvidenceDiscoverySystem.Instance != null)
                EvidenceDiscoverySystem.Instance.OnEvidenceFound += HandleEvidenceFound;
            if (ContradictionEvaluator.Instance != null)
                ContradictionEvaluator.Instance.OnEvaluationComplete += HandleEvaluationComplete;
        }

        public void StartCase(CaseData caseData)
        {
            currentCase        = caseData;
            caseStartTime      = Time.realtimeSinceStartup;
            evidenceFoundCount = 0;
            timeExpired        = false;
            timerActive        = caseData.timeLimitSeconds > 0f;

            if (timerText) timerText.gameObject.SetActive(timerActive);
        }

        void Update()
        {
            if (!timerActive || timeExpired || currentCase == null) return;

            float elapsed   = Time.realtimeSinceStartup - caseStartTime;
            float remaining = Mathf.Max(0f, currentCase.timeLimitSeconds - elapsed);

            if (timerText)
            {
                int mins = (int)(remaining / 60f);
                int secs = (int)(remaining % 60f);
                timerText.text  = $"{mins:00}:{secs:00}";
                timerText.color = remaining < 30f
                    ? new Color(0.95f, 0.30f, 0.20f)
                    : new Color(0.95f, 0.88f, 0.65f);
            }

            if (remaining <= 0f)
            {
                timeExpired = true;
                timerActive = false;
                OnTimeLimitExpired?.Invoke();
                ContradictionEvaluator.Instance?.BeginSubmit();
            }
        }

        void HandleEvidenceFound(EvidenceData _) => evidenceFoundCount++;

        void HandleEvaluationComplete(bool correct, string _)
        {
            if (!correct) return;
            CalculateAndPublish();
        }

        void CalculateAndPublish()
        {
            if (currentCase == null) return;

            int basePoints    = currentCase.basePoints;
            int evidenceBonus = evidenceFoundCount * 100;

            float elapsed = Time.realtimeSinceStartup - caseStartTime;
            int timeBonus = 0;
            if (currentCase.timeLimitSeconds > 0f)
            {
                float remaining = Mathf.Max(0f, currentCase.timeLimitSeconds - elapsed);
                timeBonus = Mathf.RoundToInt((remaining / currentCase.timeLimitSeconds) * 500f);
            }

            int claimCount   = currentCase.claims?.Count ?? 1;
            int wrongGuesses = ContradictionEvaluator.Instance?.WrongGuessCount ?? 0;
            int multiplier   = Mathf.Max(1, claimCount - wrongGuesses);

            int total = (basePoints + evidenceBonus + timeBonus) * multiplier;

            LastBasePoints    = basePoints;
            LastEvidenceBonus = evidenceBonus;
            LastTimeBonus     = timeBonus;
            LastMultiplier    = multiplier;
            LastCaseScore     = total;

            OnScoreCalculated?.Invoke(basePoints, evidenceBonus, timeBonus, multiplier);

            PlayerProfile.AddCaseResult(currentCase.caseId, total, wrongGuesses == 0);
        }

        void OnDestroy()
        {
            if (EvidenceDiscoverySystem.Instance != null)
                EvidenceDiscoverySystem.Instance.OnEvidenceFound -= HandleEvidenceFound;
            if (ContradictionEvaluator.Instance != null)
                ContradictionEvaluator.Instance.OnEvaluationComplete -= HandleEvaluationComplete;
        }
    }
}
