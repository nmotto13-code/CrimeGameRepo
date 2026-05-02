using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CasebookGame.Core;

namespace CasebookGame.UI
{
    /// <summary>
    /// The submit button transitions between idle / awaiting-selection states.
    /// </summary>
    public class SubmitButton : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] TMP_Text label;
        [SerializeField] Color idleColor = new Color(0.9f, 0.5f, 0.1f);
        [SerializeField] Color awaitingColor = new Color(0.3f, 0.7f, 1f);
        [SerializeField] Color lockedColor = new Color(0.32f, 0.34f, 0.40f);

        bool awaitingSelection = false;

        void Awake()
        {
            button?.onClick.AddListener(OnClick);
        }

        void Start()
        {
            // Listen for evaluation triggered by either SUBMIT or ANALYSE
            if (ContradictionEvaluator.Instance != null)
                ContradictionEvaluator.Instance.OnEvaluationComplete += OnEvaluated;

            if (GameManager.Instance != null)
                GameManager.Instance.CaseStateChanged += RefreshState;

            RefreshState();
        }

        void OnClick()
        {
            if (!awaitingSelection)
            {
                awaitingSelection = true;
                SetState("CANCEL", awaitingColor);
                ContradictionEvaluator.Instance?.BeginSubmit();
            }
            else
            {
                awaitingSelection = false;
                SetState("SUBMIT", idleColor);
                ContradictionEvaluator.Instance?.CancelSubmit();
            }
        }

        void OnEvaluated(bool correct, string explanation)
        {
            awaitingSelection = false;
            RefreshState();
        }

        void OnDestroy()
        {
            if (ContradictionEvaluator.Instance)
                ContradictionEvaluator.Instance.OnEvaluationComplete -= OnEvaluated;
            if (GameManager.Instance != null)
                GameManager.Instance.CaseStateChanged -= RefreshState;
        }

        void SetState(string lbl, Color c)
        {
            if (label) label.text = lbl;
            var img = button?.GetComponent<Image>();
            if (img) img.color = c;
        }

        void RefreshState()
        {
            if (button == null)
                return;

            bool solveLocked = IsSolveLocked();
            if (solveLocked)
            {
                if (awaitingSelection)
                {
                    awaitingSelection = false;
                    ContradictionEvaluator.Instance?.CancelSubmit();
                }

                button.interactable = false;
                SetState("FOLLOW LEADS", lockedColor);
                return;
            }

            button.interactable = true;
            SetState(awaitingSelection ? "CANCEL" : "SUBMIT", awaitingSelection ? awaitingColor : idleColor);
        }

        static bool IsSolveLocked()
        {
            var caseData = GameManager.Instance?.CurrentCase;
            return caseData != null
                && caseData.RequiresSolveProgressionGate()
                && GameManager.Instance != null
                && !GameManager.Instance.IsCaseReadyForSolve();
        }
    }
}
