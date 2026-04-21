using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace CasebookGame.UI
{
    /// <summary>
    /// Title / home screen. Fades in, then waits for a tap to start.
    /// </summary>
    public class TitleScreenController : MonoBehaviour
    {
        [Header("Background")]
        [SerializeField] Image backgroundImage;
        [SerializeField] Image vignetteOverlay;      // dark radial gradient image over bg

        [Header("Title Text")]
        [SerializeField] TMP_Text titleLine1;        // "POCKET CASEBOOK"
        [SerializeField] TMP_Text titleLine2;        // "CONTRADICTION ENGINE"
        [SerializeField] TMP_Text tapPrompt;         // "TAP TO BEGIN"
        [SerializeField] TMP_Text versionText;       // "CASE FILES: 10"

        [Header("Fade")]
        [SerializeField] CanvasGroup fadeGroup;
        [SerializeField] float fadeInDuration  = 1.8f;
        [SerializeField] float tapBlinkSpeed   = 1.2f;

        [Header("Scene")]
        [SerializeField] string caseSceneName = "CaseScene";

        bool canTap = false;

        void Start()
        {
            if (fadeGroup) fadeGroup.alpha = 0f;
            StartCoroutine(FadeIn());
        }

        IEnumerator FadeIn()
        {
            float t = 0f;
            while (t < fadeInDuration)
            {
                t += Time.deltaTime;
                if (fadeGroup) fadeGroup.alpha = t / fadeInDuration;
                yield return null;
            }
            if (fadeGroup) fadeGroup.alpha = 1f;
            canTap = true;
            StartCoroutine(BlinkTapPrompt());
        }

        IEnumerator BlinkTapPrompt()
        {
            if (!tapPrompt) yield break;
            while (true)
            {
                float t = 0f;
                while (t < 1f)
                {
                    t += Time.deltaTime * tapBlinkSpeed;
                    var c = tapPrompt.color;
                    c.a = Mathf.PingPong(t, 1f);
                    tapPrompt.color = c;
                    yield return null;
                }
            }
        }

        void Update()
        {
            if (!canTap) return;
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
                StartGame();
            else if (Input.GetMouseButtonDown(0))
                StartGame();
        }

        void StartGame()
        {
            if (!canTap) return;
            canTap = false;
            StartCoroutine(FadeOutAndLoad());
        }

        IEnumerator FadeOutAndLoad()
        {
            float t = 1f;
            while (t > 0f)
            {
                t -= Time.deltaTime * 2f;
                if (fadeGroup) fadeGroup.alpha = t;
                yield return null;
            }
            SceneManager.LoadScene(caseSceneName);
        }
    }
}
