using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CasebookGame.Core;

namespace CasebookGame.Core
{
    public class ResultsController : MonoBehaviour
    {
        [SerializeField] GameObject resultPanel;
        [SerializeField] TMP_Text  resultHeaderText;
        [SerializeField] TMP_Text  explanationText;
        [SerializeField] TMP_Text  scoreText;
        [SerializeField] Button    nextCaseButton;
        [SerializeField] Button    retryButton;
        [SerializeField] Button    hintButton;

        [SerializeField] Color correctColor   = new Color(0.2f, 0.8f, 0.4f);
        [SerializeField] Color incorrectColor = new Color(0.9f, 0.3f, 0.3f);
        [SerializeField] Image resultHeaderBg;

        void Awake()
        {
            nextCaseButton?.onClick.AddListener(() => { Hide(); GameManager.Instance.NextCase(); });
            retryButton?.onClick.AddListener(() => { Hide(); GameManager.Instance.RetryCase(); });
        }

        void Start()
        {
            // Deferred to Start so ContradictionEvaluator.Instance is guaranteed set
            if (ContradictionEvaluator.Instance != null)
                ContradictionEvaluator.Instance.OnEvaluationComplete += ShowResult;
        }

        void ShowResult(bool correct, string explanation)
        {
            resultPanel?.SetActive(true);

            if (resultHeaderText)
                resultHeaderText.text = correct ? "CONTRADICTION IDENTIFIED" : "NOT QUITE";

            if (resultHeaderBg)
                resultHeaderBg.color = correct ? correctColor : incorrectColor;

            if (explanationText)
                explanationText.text = correct
                    ? explanation
                    : "That claim doesn't hold the contradiction. Try another — or use a tool for a hint.";

            if (scoreText)
            {
                if (correct && ScoringSystem.Instance != null)
                {
                    var s = ScoringSystem.Instance;
                    scoreText.text = $"{s.LastBasePoints} + {s.LastEvidenceBonus} + {s.LastTimeBonus} x{s.LastMultiplier} = {s.LastCaseScore:N0}";
                    scoreText.gameObject.SetActive(true);
                }
                else
                {
                    scoreText.gameObject.SetActive(false);
                }
            }

            if (nextCaseButton) nextCaseButton.gameObject.SetActive(correct);
            if (retryButton)    retryButton.gameObject.SetActive(!correct);
            if (hintButton)     hintButton.gameObject.SetActive(!correct);
        }

        void Hide() => resultPanel?.SetActive(false);

        void OnDestroy()
        {
            if (ContradictionEvaluator.Instance)
                ContradictionEvaluator.Instance.OnEvaluationComplete -= ShowResult;
        }
    }
}
