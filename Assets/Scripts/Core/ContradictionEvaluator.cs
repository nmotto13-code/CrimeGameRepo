using UnityEngine;
using CasebookGame.Data;

namespace CasebookGame.Core
{
    public class ContradictionEvaluator : MonoBehaviour
    {
        public static ContradictionEvaluator Instance { get; private set; }

        CaseData currentCase;
        string selectedClaimId;
        bool awaitingSelection = false;
        int  wrongGuessCount   = 0;

        public int WrongGuessCount => wrongGuessCount;
        public System.Action<bool, string> OnEvaluationComplete;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void SetCase(CaseData caseData)
        {
            currentCase     = caseData;
            wrongGuessCount = 0;
        }

        public void BeginSubmit()
        {
            if (currentCase == null || awaitingSelection) return;
            awaitingSelection = true;
            UI.TabController.Instance?.SwitchToTab(3);
            UI.ClaimCardUI.OnClaimTapped += HandleClaimSelected;
        }

        void HandleClaimSelected(string claimId)
        {
            if (!awaitingSelection) return;
            awaitingSelection = false;
            UI.ClaimCardUI.OnClaimTapped -= HandleClaimSelected;

            bool correct = claimId == currentCase.contradictoryClaimId;
            if (!correct) wrongGuessCount++;
            OnEvaluationComplete?.Invoke(correct, currentCase.explanationText);
        }

        public void CancelSubmit()
        {
            if (!awaitingSelection) return;
            awaitingSelection = false;
            UI.ClaimCardUI.OnClaimTapped -= HandleClaimSelected;
        }
    }
}
