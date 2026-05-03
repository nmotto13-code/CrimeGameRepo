using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CasebookGame.UI;

namespace CasebookGame.Core
{
    public enum ScreenId { Home, CaseSelect, CityMap, Account, Game, InGameMenu, Dossier }
    public enum TransitionType { None, SlideLeft, SlideRight, FadeUp }

    public class NavigationManager : MonoBehaviour
    {
        public static NavigationManager Instance { get; private set; }

        [SerializeField] BaseScreen homeScreen;
        [SerializeField] BaseScreen caseSelectScreen;
        [SerializeField] BaseScreen cityMapScreen;
        [SerializeField] BaseScreen accountScreen;
        [SerializeField] BaseScreen gameScreen;
        [SerializeField] BaseScreen inGameMenuScreen;
        [SerializeField] BaseScreen dossierScreen;
        [SerializeField] ConfirmDialog confirmDialog;
        [SerializeField] RectTransform canvasRT;

        readonly Stack<BaseScreen> stack = new();
        bool transitioning;

        public bool IsInGame
        {
            get
            {
                foreach (var screen in stack)
                {
                    if (screen != null && screen.ScreenId == ScreenId.Game)
                        return true;
                }

                return false;
            }
        }

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            stack.Clear();

            foreach (var screen in EnumerateScreens())
            {
                if (screen == null)
                    continue;

                screen.gameObject.SetActive(false);
                if (screen.RT != null)
                    screen.RT.anchoredPosition = Vector2.zero;
                EnsureCanvasGroup(screen, 1f);
            }

            if (homeScreen == null)
                return;

            stack.Push(homeScreen);
            homeScreen.gameObject.SetActive(true);
            EnsureCanvasGroup(homeScreen, 1f);
            homeScreen.OnScreenEnter();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                HandleAndroidBack();
        }

        public void Push(ScreenId id, TransitionType transition = TransitionType.SlideLeft)
        {
            if (transitioning)
                return;

            var next = ScreenForId(id);
            if (next == null)
                return;

            if (stack.Count > 0 && stack.Peek().ScreenId == id)
                return;

            var prev = stack.Count > 0 ? stack.Peek() : null;
            stack.Push(next);
            next.OnScreenEnter();

            if (transition == TransitionType.None)
            {
                if (prev != null)
                {
                    prev.gameObject.SetActive(false);
                    prev.OnScreenExit();
                }

                next.gameObject.SetActive(true);
                EnsureCanvasGroup(next, 1f);
                return;
            }

            StartCoroutine(DoTransition(next, prev, transition, false));
        }

        public void ShowScreen(ScreenId id, TransitionType transition = TransitionType.SlideLeft)
        {
            if (transitioning)
                return;

            if (stack.Count > 0 && stack.Peek().ScreenId == id)
                return;

            var previous = GetPreviousScreen();
            if (previous != null && previous.ScreenId == id)
            {
                Pop(FlipTransition(transition));
                return;
            }

            Push(id, transition);
        }

        public void Pop(TransitionType transition = TransitionType.SlideRight)
        {
            if (transitioning || stack.Count <= 1)
                return;

            var outgoing = stack.Pop();
            var incoming = stack.Peek();
            outgoing.OnScreenExit();
            incoming.OnScreenEnter();

            if (transition == TransitionType.None)
            {
                outgoing.gameObject.SetActive(false);
                incoming.gameObject.SetActive(true);
                EnsureCanvasGroup(incoming, 1f);
                return;
            }

            StartCoroutine(DoTransition(incoming, outgoing, transition, true));
        }

        public void PopToRoot()
        {
            if (transitioning || stack.Count == 0)
                return;

            BaseScreen outgoing = null;
            while (stack.Count > 1)
            {
                var screen = stack.Pop();
                screen.OnScreenExit();
                if (stack.Count > 1)
                    screen.gameObject.SetActive(false);
                else
                    outgoing = screen;
            }

            var incoming = stack.Peek();
            incoming.OnScreenEnter();

            if (outgoing == null)
            {
                incoming.gameObject.SetActive(true);
                EnsureCanvasGroup(incoming, 1f);
                return;
            }

            StartCoroutine(DoTransition(incoming, outgoing, TransitionType.SlideRight, true));
        }

        public void PopToRootImmediate()
        {
            while (stack.Count > 1)
            {
                var screen = stack.Pop();
                screen.gameObject.SetActive(false);
                screen.OnScreenExit();
            }

            if (stack.Count == 0)
                return;

            var root = stack.Peek();
            root.gameObject.SetActive(false);
        }

        public void ResetToRootChild(ScreenId id)
        {
            if (transitioning || homeScreen == null)
                return;

            var activeScreens = new HashSet<BaseScreen>();
            foreach (var screen in stack)
            {
                if (screen != null)
                    activeScreens.Add(screen);
            }

            foreach (var screen in activeScreens)
            {
                if (screen != null && (screen != homeScreen || id != ScreenId.Home))
                    screen.OnScreenExit();
            }

            foreach (var screen in EnumerateScreens())
            {
                if (screen == null)
                    continue;

                bool shouldShow = id == ScreenId.Home
                    ? screen == homeScreen
                    : screen.ScreenId == id;

                screen.gameObject.SetActive(shouldShow);
                if (screen.RT != null)
                    screen.RT.anchoredPosition = Vector2.zero;
                EnsureCanvasGroup(screen, 1f);
            }

            stack.Clear();
            stack.Push(homeScreen);

            if (id == ScreenId.Home)
            {
                homeScreen.gameObject.SetActive(true);
                homeScreen.OnScreenEnter();
                return;
            }

            var target = ScreenForId(id);
            if (target == null)
                return;

            homeScreen.gameObject.SetActive(false);
            target.gameObject.SetActive(true);
            stack.Push(target);
            target.OnScreenEnter();
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

        void HandleAndroidBack()
        {
            if (EvidenceDetailPanel.Instance != null && EvidenceDetailPanel.Instance.IsOpen)
            {
                EvidenceDetailPanel.Instance.Hide();
                return;
            }

            if (confirmDialog != null && confirmDialog.IsShowing)
            {
                confirmDialog.Dismiss();
                return;
            }

            if (stack.Count == 0)
                return;

            var top = stack.Peek();

            if (top.ScreenId == ScreenId.InGameMenu)
            {
                Pop(TransitionType.FadeUp);
                return;
            }

            if (top.ScreenId == ScreenId.Game)
            {
                ShowScreen(ScreenId.InGameMenu, TransitionType.FadeUp);
                return;
            }

            if (stack.Count > 1)
            {
                Pop(IsOverlayScreen(top.ScreenId) ? TransitionType.FadeUp : TransitionType.SlideRight);
                return;
            }

            Application.Quit();
        }

        IEnumerator DoTransition(BaseScreen incoming, BaseScreen outgoing, TransitionType type, bool popMode)
        {
            transitioning = true;

            if (incoming != null)
            {
                incoming.gameObject.SetActive(true);
                EnsureCanvasGroup(incoming, popMode ? 1f : 0f);
            }

            if (outgoing != null)
                outgoing.gameObject.SetActive(true);

            if (type == TransitionType.FadeUp)
                yield return StartCoroutine(FadeUp(incoming, outgoing, popMode));
            else
                yield return StartCoroutine(Slide(incoming, outgoing, type == TransitionType.SlideRight));

            transitioning = false;
        }

        IEnumerator Slide(BaseScreen incoming, BaseScreen outgoing, bool slideRight)
        {
            float width = canvasRT != null ? canvasRT.rect.width : 1080f;
            float dir = slideRight ? 1f : -1f;
            const float Duration = 0.22f;
            float t = 0f;

            if (incoming != null)
            {
                bool alreadyInPlace = incoming.gameObject.activeSelf && incoming.RT.anchoredPosition == Vector2.zero;
                if (!alreadyInPlace)
                    incoming.RT.anchoredPosition = new Vector2(width * -dir, 0f);

                EnsureCanvasGroup(incoming, 1f);
            }

            while (t < 1f)
            {
                t = Mathf.Min(t + Time.unscaledDeltaTime / Duration, 1f);
                float ease = 1f - Mathf.Pow(1f - t, 3f);

                if (outgoing != null)
                    outgoing.RT.anchoredPosition = new Vector2(width * dir * ease, 0f);
                if (incoming != null)
                    incoming.RT.anchoredPosition = new Vector2(width * -dir * (1f - ease), 0f);

                yield return null;
            }

            if (outgoing != null)
            {
                outgoing.RT.anchoredPosition = Vector2.zero;
                if (outgoing.ScreenId != ScreenId.Game)
                    outgoing.gameObject.SetActive(false);
            }

            if (incoming != null)
                incoming.RT.anchoredPosition = Vector2.zero;
        }

        IEnumerator FadeUp(BaseScreen incoming, BaseScreen outgoing, bool popMode)
        {
            const float Duration = 0.24f;
            float t = 0f;
            bool overlayOpen = incoming != null
                && IsOverlayScreen(incoming.ScreenId)
                && (outgoing == null || !IsOverlayScreen(outgoing.ScreenId));
            bool overlayClose = popMode
                && outgoing != null
                && IsOverlayScreen(outgoing.ScreenId)
                && incoming != null;

            if (incoming != null)
            {
                if (overlayClose)
                {
                    EnsureCanvasGroup(incoming, 1f);
                    incoming.RT.anchoredPosition = Vector2.zero;
                }
                else
                {
                    EnsureCanvasGroup(incoming, 0f);
                    incoming.RT.anchoredPosition = new Vector2(0f, -48f);
                }
            }

            while (t < 1f)
            {
                t = Mathf.Min(t + Time.unscaledDeltaTime / Duration, 1f);
                float ease = 1f - Mathf.Pow(1f - t, 2f);

                if (incoming != null && !overlayClose)
                {
                    EnsureCanvasGroup(incoming, ease);
                    incoming.RT.anchoredPosition = new Vector2(0f, -48f * (1f - ease));
                }

                if (outgoing != null)
                {
                    if (!overlayOpen)
                        EnsureCanvasGroup(outgoing, 1f - ease);
                    else
                        EnsureCanvasGroup(outgoing, 1f);

                    if (IsOverlayScreen(outgoing.ScreenId))
                        outgoing.RT.anchoredPosition = new Vector2(0f, 20f * ease);
                }

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
                outgoing.RT.anchoredPosition = Vector2.zero;
                outgoing.gameObject.SetActive(false);
            }
        }

        BaseScreen ScreenForId(ScreenId id) => id switch
        {
            ScreenId.Home => homeScreen,
            ScreenId.CaseSelect => caseSelectScreen,
            ScreenId.CityMap => cityMapScreen,
            ScreenId.Account => accountScreen,
            ScreenId.Game => gameScreen,
            ScreenId.InGameMenu => inGameMenuScreen,
            ScreenId.Dossier => dossierScreen,
            _ => null
        };

        static TransitionType FlipTransition(TransitionType transition) => transition switch
        {
            TransitionType.SlideLeft => TransitionType.SlideRight,
            TransitionType.SlideRight => TransitionType.SlideLeft,
            _ => transition
        };

        BaseScreen GetPreviousScreen()
        {
            if (stack.Count < 2)
                return null;

            var screens = stack.ToArray();
            return screens.Length > 1 ? screens[1] : null;
        }

        IEnumerable<BaseScreen> EnumerateScreens()
        {
            yield return homeScreen;
            yield return caseSelectScreen;
            yield return cityMapScreen;
            yield return accountScreen;
            yield return gameScreen;
            yield return inGameMenuScreen;
            yield return dossierScreen;
        }

        static bool IsOverlayScreen(ScreenId id) =>
            id == ScreenId.InGameMenu || id == ScreenId.Dossier;

        static void EnsureCanvasGroup(BaseScreen screen, float alpha)
        {
            if (screen?.CanvasGroup == null)
                return;

            screen.CanvasGroup.alpha = alpha;
            screen.CanvasGroup.interactable = alpha >= 1f;
            screen.CanvasGroup.blocksRaycasts = alpha >= 1f;
        }
    }
}
