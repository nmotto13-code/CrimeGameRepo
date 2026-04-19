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

        bool awaitingSelection = false;

        void Awake()
        {
            button?.onClick.AddListener(OnClick);
        }

        void OnClick()
        {
            if (!awaitingSelection)
            {
                awaitingSelection = true;
                SetState("TAP A CLAIM", awaitingColor);
                ContradictionEvaluator.Instance?.BeginSubmit();
                ContradictionEvaluator.Instance.OnEvaluationComplete += OnEvaluated;
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
            SetState("SUBMIT", idleColor);
            ContradictionEvaluator.Instance.OnEvaluationComplete -= OnEvaluated;
        }

        void SetState(string lbl, Color c)
        {
            if (label) label.text = lbl;
            var img = button?.GetComponent<Image>();
            if (img) img.color = c;
        }
    }
}
