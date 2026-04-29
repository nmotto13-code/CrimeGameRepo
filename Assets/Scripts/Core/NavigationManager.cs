using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CasebookGame.UI;

namespace CasebookGame.Core
{
    public enum ScreenId { Home, CaseSelect, Account, Game, InGameMenu }
    public enum TransitionType { None, SlideLeft, SlideRight, FadeUp }

    public class NavigationManager : MonoBehaviour
    {
        public static NavigationManager Instance { get; private set; }

        [SerializeField] BaseScreen   homeScreen;
        [SerializeField] BaseScreen   caseSelectScreen;
        [SerializeField] BaseScreen   accountScreen;
        [SerializeField] BaseScreen   gameScreen;
        [SerializeField] BaseScreen   inGameMenuScreen;
        [SerializeField] ConfirmDialog confirmDialog;
        [SerializeField] RectTransform canvasRT;

        readonly Stack<BaseScreen> _stack        = new();
        bool                       _transitioning;

        public bool IsInGame
        {
            get { foreach (var s in _stack) if (s.ScreenId == ScreenId.Game) return true; return false; }
        }

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            // Boot into HomeScreen — active by default from SceneBuilder
            _stack.Push(homeScreen);
            homeScreen.gameObject.SetActive(true);
            EnsureCanvasGroup(homeScreen, 1f);
            homeScreen.OnScreenEnter();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                HandleAndroidBack();
        }

        // ── Public navigation API ──────────────────────────────────────

        public void Push(ScreenId id, TransitionType transition = TransitionType.SlideLeft)
        {
            if (_transitioning) return;
            var next = ScreenForId(id);
            if (next == null) return;

            var prev = _stack.Count > 0 ? _stack.Peek() : null;
            _stack.Push(next);
            next.OnScreenEnter();

            if (transition == TransitionType.None)
            {
                if (prev != null) { prev.gameObject.SetActive(false); prev.OnScreenExit(); }
                next.gameObject.SetActive(true);
                EnsureCanvasGroup(next, 1f);
            }
            else
            {
                StartCoroutine(DoTransition(next, prev, transition));
            }
        }

        public void Pop(TransitionType transition = TransitionType.SlideRight)
        {
            if (_transitioning || _stack.Count <= 1) return;

            var outgoing = _stack.Pop();
            var incoming = _stack.Peek();
            outgoing.OnScreenExit();
            incoming.OnScreenEnter();

            if (transition == TransitionType.None)
            {
                outgoing.gameObject.SetActive(false);
                incoming.gameObject.SetActive(true);
                EnsureCanvasGroup(incoming, 1f);
            }
            else
            {
                StartCoroutine(DoTransition(incoming, outgoing, transition, popMode: true));
            }
        }

        public void PopToRoot()
        {
            if (_transitioning) return;

            // Unwind the stack — hide intermediate screens immediately, animate only the last one out
            BaseScreen outgoing = null;
            while (_stack.Count > 1)
            {
                var s = _stack.Pop();
                s.OnScreenExit();
                if (_stack.Count > 1)
                    s.gameObject.SetActive(false); // intermediate — hide now
                else
                    outgoing = s;                  // last popped — will animate out
            }

            var incoming = _stack.Peek();
            incoming.OnScreenEnter();

            if (outgoing != null)
                StartCoroutine(DoTransition(incoming, outgoing, TransitionType.SlideRight, popMode: true));
            else
            {
                incoming.gameObject.SetActive(true);
                EnsureCanvasGroup(incoming, 1f);
            }
        }

        public void PopToRootImmediate()
        {
            while (_stack.Count > 1)
            {
                var s = _stack.Pop();
                s.gameObject.SetActive(false);
                s.OnScreenExit();
            }
            var root = _stack.Peek();
            root.gameObject.SetActive(false); // will be re-shown by Push(Game) immediately after
        }

        public void ConfirmLeaveCase(Action onConfirmed)
        {
            if (!IsInGame)
            {
                onConfirmed?.Invoke();
                return;
            }
            confirmDialog.Show(
                "Leaving now will lose your current progress.\n\nAre you sure?",
                onConfirm: onConfirmed,
                onCancel: null);
        }

        // ── Android back button ────────────────────────────────────────

        void HandleAndroidBack()
        {
            // 1. Evidence detail open → close it
            if (EvidenceDetailPanel.Instance != null && EvidenceDetailPanel.Instance.IsOpen)
            {
                EvidenceDetailPanel.Instance.Hide();
                return;
            }

            // 2. Confirm dialog showing → dismiss it
            if (confirmDialog != null && confirmDialog.IsShowing)
            {
                confirmDialog.Dismiss();
                return;
            }

            if (_stack.Count == 0) return;
            var top = _stack.Peek();

            // 3. In-game menu → pop back to game
            if (top.ScreenId == ScreenId.InGameMenu)
            {
                Pop(TransitionType.SlideRight);
                return;
            }

            // 4. Game screen → open in-game menu
            if (top.ScreenId == ScreenId.Game)
            {
                Push(ScreenId.InGameMenu, TransitionType.SlideLeft);
                return;
            }

            // 5. Any other pushed screen → pop back
            if (_stack.Count > 1)
            {
                Pop(TransitionType.SlideRight);
                return;
            }

            // 6. At home root → quit
            Application.Quit();
        }

        // ── Transition coroutines ──────────────────────────────────────

        IEnumerator DoTransition(BaseScreen incoming, BaseScreen outgoing,
                                  TransitionType type, bool popMode = false)
        {
            _transitioning = true;

            // Ensure both are active before animating
            if (incoming != null) { incoming.gameObject.SetActive(true); EnsureCanvasGroup(incoming, popMode ? 1f : 0f); }
            if (outgoing != null)   outgoing.gameObject.SetActive(true);

            if (type == TransitionType.FadeUp)
                yield return StartCoroutine(FadeUp(incoming, outgoing));
            else
                yield return StartCoroutine(Slide(incoming, outgoing, slideRight: type == TransitionType.SlideRight));

            _transitioning = false;
        }

        IEnumerator Slide(BaseScreen incoming, BaseScreen outgoing, bool slideRight)
        {
            float width = canvasRT != null ? canvasRT.rect.width : 1080f;
            float dir   = slideRight ? 1f : -1f;
            const float DURATION = 0.22f;
            float t = 0f;

            // Place incoming off-screen — skip if it's already active and centered (overlay case)
            if (incoming != null)
            {
                bool alreadyInPlace = incoming.gameObject.activeSelf
                                   && incoming.RT.anchoredPosition == Vector2.zero;
                if (!alreadyInPlace)
                    incoming.RT.anchoredPosition = new Vector2(width * -dir, 0);
                EnsureCanvasGroup(incoming, 1f);
            }

            while (t < 1f)
            {
                t = Mathf.Min(t + Time.unscaledDeltaTime / DURATION, 1f);
                float ease = 1f - Mathf.Pow(1f - t, 3f); // ease-out cubic

                if (outgoing != null)
                    outgoing.RT.anchoredPosition = new Vector2(width * dir * ease, 0);
                if (incoming != null)
                    incoming.RT.anchoredPosition = new Vector2(width * -dir * (1f - ease), 0);

                yield return null;
            }

            if (outgoing != null)
            {
                outgoing.RT.anchoredPosition = Vector2.zero;
                // Keep Game screen alive — InGameMenu overlays it rather than replacing it
                if (outgoing.ScreenId != ScreenId.Game)
                    outgoing.gameObject.SetActive(false);
            }
            if (incoming != null)
                incoming.RT.anchoredPosition = Vector2.zero;
        }

        IEnumerator FadeUp(BaseScreen incoming, BaseScreen outgoing)
        {
            const float DURATION = 0.28f;
            float t = 0f;

            if (incoming != null)
            {
                EnsureCanvasGroup(incoming, 0f);
                incoming.RT.anchoredPosition = new Vector2(0, -60f);
            }

            while (t < 1f)
            {
                t = Mathf.Min(t + Time.unscaledDeltaTime / DURATION, 1f);
                float ease = 1f - Mathf.Pow(1f - t, 2f); // ease-out quad

                if (incoming != null)
                {
                    EnsureCanvasGroup(incoming, ease);
                    incoming.RT.anchoredPosition = new Vector2(0, -60f * (1f - ease));
                }
                if (outgoing != null)
                    EnsureCanvasGroup(outgoing, 1f - ease);

                yield return null;
            }

            if (incoming != null)
            {
                EnsureCanvasGroup(incoming, 1f);
                incoming.RT.anchoredPosition = Vector2.zero;
            }
            if (outgoing != null)
            {
                EnsureCanvasGroup(outgoing, 1f);
                outgoing.gameObject.SetActive(false);
            }
        }

        // ── Helpers ────────────────────────────────────────────────────

        BaseScreen ScreenForId(ScreenId id) => id switch
        {
            ScreenId.Home       => homeScreen,
            ScreenId.CaseSelect => caseSelectScreen,
            ScreenId.Account    => accountScreen,
            ScreenId.Game       => gameScreen,
            ScreenId.InGameMenu => inGameMenuScreen,
            _                   => null
        };

        static TransitionType FlipTransition(TransitionType t) => t switch
        {
            TransitionType.SlideLeft  => TransitionType.SlideRight,
            TransitionType.SlideRight => TransitionType.SlideLeft,
            _                         => t
        };

        static void EnsureCanvasGroup(BaseScreen s, float alpha)
        {
            if (s?.CanvasGroup == null) return;
            s.CanvasGroup.alpha          = alpha;
            s.CanvasGroup.interactable   = alpha >= 1f;
            s.CanvasGroup.blocksRaycasts = alpha >= 1f;
        }
    }
}
