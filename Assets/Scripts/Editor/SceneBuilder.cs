#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using CasebookGame.Core;
using CasebookGame.UI;
using CasebookGame.Tools;
using CasebookGame.Viewers;
using CasebookGame.Data;

namespace CasebookGame.Editor
{
    public static class SceneBuilder
    {
        const string SCENE_PATH   = "Assets/Scenes/CaseScene.unity";
        const string PREFABS_PATH = "Assets/Prefabs";

        // Palette
        static readonly Color C_BG      = new Color(0.08f, 0.08f, 0.12f);
        static readonly Color C_PANEL   = new Color(0.12f, 0.12f, 0.18f);
        static readonly Color C_ACCENT  = new Color(0.90f, 0.50f, 0.10f);
        static readonly Color C_DIM     = new Color(0.50f, 0.50f, 0.50f);

        // ── Entry point ────────────────────────────────────────────────

        [MenuItem("Casebook/Build Scene")]
        public static void BuildScene()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Stop Play Mode", "Exit Play Mode before building the scene.", "OK");
                return;
            }

            EnsureFolder("Assets/Scenes");
            EnsureFolder(PREFABS_PATH);

            // Build prefabs first (referenced by CaseLoader)
            var evidenceCardPrefab = BuildEvidenceCardPrefab();
            var claimCardPrefab    = BuildClaimCardPrefab();
            var hotspotPrefab      = BuildHotspotPrefab();

            // Fresh empty scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── Camera ─────────────────────────────────────────────────
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags       = CameraClearFlags.SolidColor;
            cam.backgroundColor  = new Color(0.08f, 0.08f, 0.12f);
            cam.orthographic     = true;
            cam.depth            = -1;
            camGo.AddComponent<AudioListener>();

            // ── EventSystem ────────────────────────────────────────────
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();

            // ── Canvas ─────────────────────────────────────────────────
            var canvasGo = new GameObject("Canvas");
            var canvas   = canvasGo.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight  = 1.0f; // height-based: consistent across all iPhone sizes
            canvasGo.AddComponent<GraphicRaycaster>();

            // ── Background ─────────────────────────────────────────────
            var bgGo  = MakeImage(canvasGo, "Background", C_BG);
            StretchFull(bgGo);

            // ── GameScreen wrapper — groups all in-game UI so SetActive hides everything ──
            var gameScreenGo = new GameObject("GameScreen");
            gameScreenGo.transform.SetParent(canvasGo.transform, false);
            StretchFull(gameScreenGo);
            gameScreenGo.AddComponent<Image>().color = Color.clear;
            var gameScreenCtrl = gameScreenGo.AddComponent<GameScreenController>();
            // GameScreen starts hidden; HomeScreen is shown first
            gameScreenGo.SetActive(false);

            // ── Tab bar — anchored top strip, scales with Canvas Scaler ──
            var tabBarGo = new GameObject("TabBar");
            tabBarGo.transform.SetParent(gameScreenGo.transform, false);
            var tabBarRT  = tabBarGo.AddComponent<RectTransform>();
            tabBarRT.anchorMin        = new Vector2(0, 1);
            tabBarRT.anchorMax        = new Vector2(1, 1);
            tabBarRT.pivot            = new Vector2(0.5f, 1f);
            tabBarRT.anchoredPosition = Vector2.zero;
            tabBarRT.sizeDelta        = new Vector2(0, 100);  // thinner for more content space
            tabBarGo.AddComponent<Image>().color = C_PANEL;
            var tabBarHLG = tabBarGo.AddComponent<HorizontalLayoutGroup>();
            tabBarHLG.childControlHeight    = true;
            tabBarHLG.childControlWidth     = true;
            tabBarHLG.childForceExpandHeight = true;
            tabBarHLG.childForceExpandWidth  = true;

            var briefBtn    = MakeTabButton(tabBarGo, "BriefBtn",    "BRIEF");
            var sceneBtn    = MakeTabButton(tabBarGo, "SceneBtn",    "SCENE");
            var evidenceBtn = MakeTabButton(tabBarGo, "EvidenceBtn", "EVIDENCE");
            var solveBtn    = MakeTabButton(tabBarGo, "SolveBtn",    "SOLVE");

            // Hamburger — right-pinned, fixed width, opens in-game menu
            var hamburgerGo = MakeSimpleButton(tabBarGo, "HamburgerBtn", "≡", new Color(0.25f, 0.25f, 0.38f));
            hamburgerGo.AddComponent<LayoutElement>().preferredWidth = 80;

            // ── Tab content (between tab bar and tools bar) ────────────
            var tabContentGo = new GameObject("TabContent");
            tabContentGo.transform.SetParent(gameScreenGo.transform, false);
            var tabContentRT    = tabContentGo.AddComponent<RectTransform>();
            tabContentRT.anchorMin = new Vector2(0, 0);
            tabContentRT.anchorMax = new Vector2(1, 1);
            tabContentRT.offsetMin = new Vector2(0, 80);    // above submit only
            tabContentRT.offsetMax = new Vector2(0, -100);  // below tab bar

            // Brief panel — simple VLG, no ScrollRect (avoids ContentSizeFitter timing issues)
            var briefPanel   = MakePanel(tabContentGo, "BriefPanel", C_BG);
            var briefContent = AddSimpleContent(briefPanel);

            var caseTitleText = MakeText(briefContent, "CaseTitleText", "CASE TITLE", 44, FontStyles.Bold);
            caseTitleText.textWrappingMode = TextWrappingModes.Normal;
            caseTitleText.alignment        = TextAlignmentOptions.TopLeft;
            var caseTitleLE = caseTitleText.gameObject.AddComponent<LayoutElement>();
            caseTitleLE.preferredHeight = 90;
            caseTitleLE.flexibleWidth   = 1;

            var briefBodyText = MakeText(briefContent, "BriefText", "Brief text...", 28, FontStyles.Normal);
            briefBodyText.textWrappingMode = TextWrappingModes.Normal;
            briefBodyText.alignment        = TextAlignmentOptions.TopLeft;
            var briefBodyLE = briefBodyText.gameObject.AddComponent<LayoutElement>();
            briefBodyLE.preferredHeight = 800;   // tall enough for any brief
            briefBodyLE.flexibleWidth   = 1;

            // ── Scene panel — crime scene background + hotspots ────────
            var scenePanel = MakePanel(tabContentGo, "ScenePanel", new Color(0.04f, 0.04f, 0.08f));
            scenePanel.SetActive(false);

            // Found-counter bar at the top of the scene panel
            var sceneFoundBar = new GameObject("FoundCounterBar");
            sceneFoundBar.transform.SetParent(scenePanel.transform, false);
            var sceneFoundBarRT = sceneFoundBar.AddComponent<RectTransform>();
            sceneFoundBarRT.anchorMin        = new Vector2(0, 1);
            sceneFoundBarRT.anchorMax        = new Vector2(1, 1);
            sceneFoundBarRT.pivot            = new Vector2(0.5f, 1f);
            sceneFoundBarRT.anchoredPosition = Vector2.zero;
            sceneFoundBarRT.sizeDelta        = new Vector2(0, 56);
            sceneFoundBar.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.08f, 0.92f);
            var sfbHLG = sceneFoundBar.AddComponent<HorizontalLayoutGroup>();
            sfbHLG.childControlHeight    = true; sfbHLG.childControlWidth     = true;
            sfbHLG.childForceExpandHeight = true; sfbHLG.childForceExpandWidth = false;
            sfbHLG.spacing = 8; sfbHLG.padding = new RectOffset(16, 8, 6, 6);

            var invFoundTxt = MakeText(sceneFoundBar, "FoundCounterText", "FOUND  0 / 0", 26, FontStyles.Bold,
                new Color(0.95f, 0.88f, 0.65f));
            invFoundTxt.alignment = TextAlignmentOptions.MidlineLeft;
            invFoundTxt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            // Timer — hidden by default, shown by ScoringSystem when case has timeLimitSeconds > 0
            var timerTxt = MakeText(sceneFoundBar, "TimerText", "00:00", 26, FontStyles.Bold,
                new Color(0.95f, 0.88f, 0.65f));
            timerTxt.alignment = TextAlignmentOptions.MidlineLeft;
            var timerLE = timerTxt.gameObject.AddComponent<LayoutElement>();
            timerLE.preferredWidth = 110; timerLE.flexibleWidth = 0;
            timerTxt.gameObject.SetActive(false);

            var invHintTxt = MakeText(sceneFoundBar, "InvestigationHintText", "Tap the scene to find clues",
                20, FontStyles.Normal, new Color(0.60f, 0.60f, 0.60f));
            invHintTxt.alignment = TextAlignmentOptions.MidlineLeft;
            var invHintLE = invHintTxt.gameObject.AddComponent<LayoutElement>();
            invHintLE.preferredWidth = 260; invHintLE.flexibleWidth = 0;

            var analyseBtn = MakeSimpleButton(sceneFoundBar, "AnalyseButton", "ANALYSE", new Color(0.20f, 0.65f, 0.40f));
            analyseBtn.SetActive(false);
            analyseBtn.AddComponent<LayoutElement>().preferredWidth = 170;

            // ── Scene image — fills panel below counter bar ────────────
            var sceneImgGo = MakeImage(scenePanel, "SceneImage", new Color(0.12f, 0.10f, 0.18f));
            var sceneImgRT = sceneImgGo.GetComponent<RectTransform>();
            sceneImgRT.anchorMin = Vector2.zero; sceneImgRT.anchorMax = Vector2.one;
            sceneImgRT.offsetMin = new Vector2(0, 0); sceneImgRT.offsetMax = new Vector2(0, -56);
            sceneImgGo.GetComponent<Image>().preserveAspect = false;

            var noImgTxt = MakeText(sceneImgGo, "NoImagePlaceholder",
                "No scene image assigned.\nDrop image in Assets/Sprites/Backgrounds/\nthen run  Casebook → Import Assets",
                24, FontStyles.Normal, new Color(0.45f, 0.45f, 0.55f));
            noImgTxt.textWrappingMode = TextWrappingModes.Normal;
            noImgTxt.alignment = TextAlignmentOptions.Center;
            StretchFull(noImgTxt.gameObject);

            // ── Hotspot container — same bounds as scene image ─────────
            var sceneHotspotGo = new GameObject("SceneHotspotContainer");
            sceneHotspotGo.transform.SetParent(scenePanel.transform, false);
            var sceneHotspotRT = sceneHotspotGo.AddComponent<RectTransform>();
            sceneHotspotRT.anchorMin = Vector2.zero; sceneHotspotRT.anchorMax = Vector2.one;
            sceneHotspotRT.offsetMin = new Vector2(0, 0); sceneHotspotRT.offsetMax = new Vector2(0, -56);

            // ── Magnifying glass visual (child of GameScreen, floats on top of scene) ──
            var glassVisualGo = new GameObject("GlassVisual");
            glassVisualGo.transform.SetParent(gameScreenGo.transform, false);
            var glassVisualRT = glassVisualGo.AddComponent<RectTransform>();
            glassVisualRT.sizeDelta = new Vector2(140, 140);
            glassVisualRT.anchorMin = glassVisualRT.anchorMax = new Vector2(0.5f, 0.5f);
            glassVisualGo.SetActive(false);

            // Circular outer ring — uses built-in Knob sprite (white circle, tinted gold)
            var circleSpr    = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
            var glassRingImg = glassVisualGo.AddComponent<Image>();
            glassRingImg.sprite        = circleSpr;
            glassRingImg.color         = new Color(0.95f, 0.80f, 0.20f, 1.0f);
            glassRingImg.raycastTarget = false;

            // Dark lens interior (circle, inset 10% each side from ring edge)
            var lensDarkGo  = MakeImage(glassVisualGo, "LensDark", new Color(0.06f, 0.06f, 0.10f, 0.88f));
            var lensDarkRT  = lensDarkGo.GetComponent<RectTransform>();
            lensDarkRT.anchorMin = new Vector2(0.10f, 0.10f);
            lensDarkRT.anchorMax = new Vector2(0.90f, 0.90f);
            lensDarkRT.offsetMin = Vector2.zero; lensDarkRT.offsetMax = Vector2.zero;
            var lensDarkImg = lensDarkGo.GetComponent<Image>();
            lensDarkImg.sprite        = circleSpr;
            lensDarkImg.raycastTarget = false;

            // Subtle lens glint — small ellipse in upper-left quadrant
            var shineGo  = MakeImage(glassVisualGo, "LensShine", new Color(1f, 1f, 1f, 0.10f));
            var shineRT  = shineGo.GetComponent<RectTransform>();
            shineRT.anchorMin = new Vector2(0.15f, 0.52f);
            shineRT.anchorMax = new Vector2(0.55f, 0.82f);
            shineRT.offsetMin = Vector2.zero; shineRT.offsetMax = Vector2.zero;
            shineRT.localEulerAngles = new Vector3(0f, 0f, 25f);
            var shineImg = shineGo.GetComponent<Image>();
            shineImg.sprite        = circleSpr;
            shineImg.raycastTarget = false;

            // Handle — thin rotated rectangle, pivoting from ring edge
            var handleGo = MakeImage(glassVisualGo, "Handle", new Color(0.55f, 0.38f, 0.10f, 1.0f));
            var hRT = handleGo.GetComponent<RectTransform>();
            hRT.anchorMin = hRT.anchorMax = new Vector2(0.5f, 0.5f);
            hRT.pivot     = new Vector2(0.5f, 1.0f);
            hRT.sizeDelta = new Vector2(18f, 56f);
            hRT.anchoredPosition    = new Vector2(50f, -48f);
            hRT.localEulerAngles    = new Vector3(0f, 0f, -42f);
            handleGo.GetComponent<Image>().raycastTarget = false;

            // ── MagnifyingGlass component on the scene panel ───────────
            // ScenePanel has an Image so it already intercepts raycasts.
            // Remove SceneViewController (replaced by MagnifyingGlass for input).
            var glass = scenePanel.AddComponent<MagnifyingGlass>();
            Wire(glass, so => {
                so.FindProperty("glassVisual").objectReferenceValue = glassVisualRT;
                so.FindProperty("glassRing").objectReferenceValue   = glassRingImg;
            });

            // ── Evidence panel (index 2) — collected evidence list ────────
            var evidencePanel = MakePanel(tabContentGo, "EvidencePanel", C_BG);
            evidencePanel.SetActive(false);

            var evHeaderGo = new GameObject("EvidenceHeader");
            evHeaderGo.transform.SetParent(evidencePanel.transform, false);
            var evHeaderRT = evHeaderGo.AddComponent<RectTransform>();
            evHeaderRT.anchorMin = new Vector2(0, 1); evHeaderRT.anchorMax = new Vector2(1, 1);
            evHeaderRT.pivot = new Vector2(0.5f, 1f);
            evHeaderRT.anchoredPosition = Vector2.zero;
            evHeaderRT.sizeDelta = new Vector2(0, 70);
            evHeaderGo.AddComponent<Image>().color = C_PANEL;
            var evHeaderTxt = MakeText(evHeaderGo, "HeaderText", "EVIDENCE COLLECTED",
                28, FontStyles.Bold, new Color(0.92f, 0.85f, 0.65f));
            evHeaderTxt.alignment = TextAlignmentOptions.MidlineLeft;
            StretchFull(evHeaderTxt.gameObject, new Vector2(24, 0));

            var evListGo = new GameObject("EvidenceListPanel");
            evListGo.transform.SetParent(evidencePanel.transform, false);
            var evListRT = evListGo.AddComponent<RectTransform>();
            evListRT.anchorMin = Vector2.zero; evListRT.anchorMax = Vector2.one;
            evListRT.offsetMin = Vector2.zero; evListRT.offsetMax = new Vector2(0, -70);
            evListGo.AddComponent<Image>().color = C_BG;
            var evidenceTabContent = AddScrollList(evListGo);

            // ── Solve panel (index 3) — instructions + claims list ────────
            var solvePanel = MakePanel(tabContentGo, "SolvePanel", C_BG);
            solvePanel.SetActive(false);

            // Instruction card fixed at the top
            var solveInstrGo = new GameObject("InstructionCard");
            solveInstrGo.transform.SetParent(solvePanel.transform, false);
            var solveInstrRT = solveInstrGo.AddComponent<RectTransform>();
            solveInstrRT.anchorMin = new Vector2(0, 1); solveInstrRT.anchorMax = new Vector2(1, 1);
            solveInstrRT.pivot = new Vector2(0.5f, 1f);
            solveInstrRT.anchoredPosition = Vector2.zero;
            solveInstrRT.sizeDelta = new Vector2(0, 200);
            solveInstrGo.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.18f);

            var solveTitle = MakeText(solveInstrGo, "SolveTitleText", "SOLVE THE CASE",
                32, FontStyles.Bold, new Color(0.95f, 0.70f, 0.20f));
            solveTitle.alignment = TextAlignmentOptions.Center;
            var sTitleRT = solveTitle.gameObject.GetComponent<RectTransform>();
            sTitleRT.anchorMin = new Vector2(0, 0.58f); sTitleRT.anchorMax = new Vector2(1, 1);
            sTitleRT.offsetMin = new Vector2(16, 0); sTitleRT.offsetMax = new Vector2(-16, -8);

            var solveInstr = MakeText(solveInstrGo, "InstructionText",
                "One witness is lying.\n\nReview each statement — find the claim the evidence disproves.\n\nTap it to expose the lie.",
                21, FontStyles.Normal, new Color(0.72f, 0.72f, 0.78f));
            solveInstr.textWrappingMode = TextWrappingModes.Normal;
            solveInstr.alignment = TextAlignmentOptions.Center;
            var sInstrRT = solveInstr.gameObject.GetComponent<RectTransform>();
            sInstrRT.anchorMin = new Vector2(0, 0); sInstrRT.anchorMax = new Vector2(1, 0.58f);
            sInstrRT.offsetMin = new Vector2(16, 6); sInstrRT.offsetMax = new Vector2(-16, 0);

            // Claims scroll list fills remaining space below instruction card
            var claimsListGo = new GameObject("ClaimsListPanel");
            claimsListGo.transform.SetParent(solvePanel.transform, false);
            var claimsListRT = claimsListGo.AddComponent<RectTransform>();
            claimsListRT.anchorMin = Vector2.zero; claimsListRT.anchorMax = Vector2.one;
            claimsListRT.offsetMin = Vector2.zero; claimsListRT.offsetMax = new Vector2(0, -200);
            claimsListGo.AddComponent<Image>().color = C_BG;
            var claimsContent = AddScrollList(claimsListGo);

            // ── Submit button (bottom 80 px) ───────────────────────────
            var submitGo = new GameObject("SubmitButton");
            submitGo.transform.SetParent(gameScreenGo.transform, false);
            var submitRT = submitGo.AddComponent<RectTransform>();
            submitRT.anchorMin        = new Vector2(0, 0);
            submitRT.anchorMax        = new Vector2(1, 0);
            submitRT.pivot            = new Vector2(0.5f, 0f);
            submitRT.anchoredPosition = Vector2.zero;
            submitRT.sizeDelta        = new Vector2(0, 80);
            var submitImg  = submitGo.AddComponent<Image>();
            submitImg.color = C_ACCENT;
            var submitBtn  = submitGo.AddComponent<Button>();
            submitBtn.targetGraphic = submitImg;
            var submitLabel = MakeText(submitGo, "Label", "SUBMIT", 36, FontStyles.Bold);
            StretchFull(submitLabel.gameObject);
            var submitComp = submitGo.AddComponent<SubmitButton>();

            // ── Result panel (fullscreen, hidden) ─────────────────────
            var resultPanelGo = MakePanel(canvasGo, "ResultPanel", new Color(0, 0, 0, 0.88f));
            StretchFull(resultPanelGo);
            resultPanelGo.SetActive(false);

            var resultHeaderBgGo = MakeImage(resultPanelGo, "ResultHeaderBg", new Color(0.20f, 0.75f, 0.40f));
            var rhRT = resultHeaderBgGo.GetComponent<RectTransform>();
            rhRT.anchorMin = new Vector2(0, 0.65f); rhRT.anchorMax = new Vector2(1, 1);
            rhRT.offsetMin = Vector2.zero; rhRT.offsetMax = Vector2.zero;
            var resultHeaderTxt = MakeText(resultHeaderBgGo, "ResultHeaderText", "CONTRADICTION IDENTIFIED", 40, FontStyles.Bold);
            StretchFull(resultHeaderTxt.gameObject);

            var explanationTxt = MakeText(resultPanelGo, "ExplanationText", "", 28, FontStyles.Normal);
            explanationTxt.textWrappingMode = TextWrappingModes.Normal;
            var exRT = explanationTxt.gameObject.GetComponent<RectTransform>();
            exRT.anchorMin = new Vector2(0.05f, 0.28f); exRT.anchorMax = new Vector2(0.95f, 0.63f);
            exRT.offsetMin = Vector2.zero; exRT.offsetMax = Vector2.zero;

            // Score breakdown — shown only on correct solve
            var scoreTxt = MakeText(resultPanelGo, "ScoreText", "", 32, FontStyles.Bold,
                new Color(0.95f, 0.88f, 0.30f));
            scoreTxt.alignment = TextAlignmentOptions.Center;
            var scoreRT = scoreTxt.gameObject.GetComponent<RectTransform>();
            scoreRT.anchorMin = new Vector2(0.05f, 0.22f);
            scoreRT.anchorMax = new Vector2(0.95f, 0.29f);
            scoreRT.offsetMin = Vector2.zero; scoreRT.offsetMax = Vector2.zero;
            scoreTxt.gameObject.SetActive(false);

            var nextCaseBtn = MakeActionButton(resultPanelGo, "NextCaseBtn", "NEXT CASE", new Color(0.20f, 0.65f, 0.40f),
                new Vector2(0.1f, 0.08f), new Vector2(0.9f, 0.20f));
            var retryBtn    = MakeActionButton(resultPanelGo, "RetryBtn",    "TRY AGAIN", new Color(0.75f, 0.25f, 0.25f),
                new Vector2(0.1f, 0.08f), new Vector2(0.9f, 0.20f));
            retryBtn.gameObject.SetActive(false);
            var hintBtn     = MakeActionButton(resultPanelGo, "HintBtn",     "HINT",      new Color(0.45f, 0.45f, 0.75f),
                new Vector2(0.3f, 0.02f), new Vector2(0.7f, 0.09f));
            hintBtn.gameObject.SetActive(false);

            // ── Evidence Detail Panel (fullscreen, hidden) ─────────────
            var detailPanelGo = new GameObject("EvidenceDetailPanel");
            detailPanelGo.transform.SetParent(canvasGo.transform, false);
            StretchFull(detailPanelGo);
            detailPanelGo.SetActive(false);
            detailPanelGo.AddComponent<Image>().color = new Color(0, 0, 0, 0.90f);

            var detailCard   = MakeImage(detailPanelGo, "DetailCard", C_PANEL);
            var dcRT = detailCard.GetComponent<RectTransform>();
            dcRT.anchorMin = new Vector2(0.04f, 0.06f); dcRT.anchorMax = new Vector2(0.96f, 0.96f);
            dcRT.offsetMin = Vector2.zero; dcRT.offsetMax = Vector2.zero;

            var detailTitle = MakeText(detailCard, "TitleText", "Evidence Title", 34, FontStyles.Bold);
            var dtRT = detailTitle.gameObject.GetComponent<RectTransform>();
            dtRT.anchorMin = new Vector2(0, 0.90f); dtRT.anchorMax = new Vector2(1, 1);
            dtRT.offsetMin = new Vector2(16, 0); dtRT.offsetMax = new Vector2(-16, 0);

            // Viewer (RawImage + EvidenceViewer)
            var viewerGo = new GameObject("ViewerArea");
            viewerGo.transform.SetParent(detailCard.transform, false);
            var viewerRT2 = viewerGo.AddComponent<RectTransform>();
            viewerRT2.anchorMin = new Vector2(0, 0.46f); viewerRT2.anchorMax = new Vector2(1, 0.90f);
            viewerRT2.offsetMin = new Vector2(10, 0); viewerRT2.offsetMax = new Vector2(-10, 0);
            var mainRawImg  = viewerGo.AddComponent<RawImage>();
            mainRawImg.color = new Color(0.18f, 0.18f, 0.18f);
            var viewer = viewerGo.AddComponent<EvidenceViewer>();

            var enhOverlayGo  = new GameObject("EnhanceOverlay");
            enhOverlayGo.transform.SetParent(viewerGo.transform, false);
            StretchFull(enhOverlayGo);
            var enhOverlayImg = enhOverlayGo.AddComponent<RawImage>();
            enhOverlayImg.color = new Color(1f, 0.95f, 0.2f, 0.55f);
            enhOverlayGo.SetActive(false);

            // Text-mode view — same bounds as viewer, shown instead for Document/Terminal/Keycard evidence
            var textModeRootGo = new GameObject("TextModeView");
            textModeRootGo.transform.SetParent(detailCard.transform, false);
            var tmRT = textModeRootGo.AddComponent<RectTransform>();
            tmRT.anchorMin = new Vector2(0, 0.46f); tmRT.anchorMax = new Vector2(1, 0.90f);
            tmRT.offsetMin = new Vector2(10, 0); tmRT.offsetMax = new Vector2(-10, 0);
            var textModeBgImg = textModeRootGo.AddComponent<RawImage>();
            textModeBgImg.color = Color.white;
            textModeRootGo.SetActive(false);

            var textBodyGo = new GameObject("TextBody");
            textBodyGo.transform.SetParent(textModeRootGo.transform, false);
            StretchFull(textBodyGo, new Vector2(18, 14));
            var textBodyTmp = textBodyGo.AddComponent<TextMeshProUGUI>();
            textBodyTmp.fontSize         = 22;
            textBodyTmp.textWrappingMode = TextWrappingModes.Normal;
            textBodyTmp.alignment        = TextAlignmentOptions.TopLeft;
            textBodyTmp.color            = new Color(0.18f, 0.11f, 0.04f);

            // Description — below viewer, clearly separated from tags
            var detailDesc = MakeText(detailCard, "DescriptionText", "", 24, FontStyles.Normal);
            detailDesc.textWrappingMode = TextWrappingModes.Normal;
            detailDesc.alignment        = TextAlignmentOptions.TopLeft;
            var ddRT = detailDesc.gameObject.GetComponent<RectTransform>();
            ddRT.anchorMin = new Vector2(0, 0.24f); ddRT.anchorMax = new Vector2(1, 0.46f);
            ddRT.offsetMin = new Vector2(16, 4); ddRT.offsetMax = new Vector2(-16, 0);

            // Tags — clearly below description, no overlap
            var detailTags = MakeText(detailCard, "TagsText", "", 21, FontStyles.Normal);
            detailTags.color     = new Color(0.55f, 0.80f, 1f);
            detailTags.alignment = TextAlignmentOptions.Left;
            var dtagRT = detailTags.gameObject.GetComponent<RectTransform>();
            dtagRT.anchorMin = new Vector2(0, 0.16f); dtagRT.anchorMax = new Vector2(1, 0.24f);
            dtagRT.offsetMin = new Vector2(16, 0); dtagRT.offsetMax = new Vector2(-16, 0);

            // Three action buttons — bottom strip, clear of tags
            var closeBtn   = MakeActionButton(detailCard, "CloseBtn",   "CLOSE",   C_DIM,
                new Vector2(0.03f, 0.02f), new Vector2(0.30f, 0.13f));
            var pinDetailBtn = MakeActionButton(detailCard, "PinButton", "PIN TO BOARD", new Color(0.20f, 0.65f, 0.90f),
                new Vector2(0.34f, 0.02f), new Vector2(0.67f, 0.13f));
            var pinDetailLabel = pinDetailBtn.GetComponentInChildren<TextMeshProUGUI>();
            var enhDetailBtn = MakeActionButton(detailCard, "EnhanceBtn", "ENHANCE", new Color(0.9f, 0.7f, 0.1f),
                new Vector2(0.70f, 0.02f), new Vector2(0.97f, 0.13f));

            // ── SafeAreaRoot — wraps all navigation screens, respects iPhone notch/home bar ──
            var safeAreaRoot = new GameObject("SafeAreaRoot");
            safeAreaRoot.transform.SetParent(canvasGo.transform, false);
            StretchFull(safeAreaRoot);
            safeAreaRoot.AddComponent<SafeAreaFitter>();

            // ── Home Screen ────────────────────────────────────────────
            var homeScreenGo = new GameObject("HomeScreen");
            homeScreenGo.transform.SetParent(safeAreaRoot.transform, false);
            StretchFull(homeScreenGo);
            homeScreenGo.AddComponent<Image>().color = new Color(0.06f, 0.06f, 0.10f, 0.98f);

            var hsVLG = homeScreenGo.AddComponent<VerticalLayoutGroup>();
            hsVLG.childControlHeight    = true;
            hsVLG.childControlWidth     = true;
            hsVLG.childForceExpandWidth = true;
            hsVLG.padding               = new RectOffset(60, 60, 200, 120);
            hsVLG.spacing               = 32;

            var hsLogoTxt = MakeText(homeScreenGo, "LogoText", "POCKET CASEBOOK", 64, FontStyles.Bold,
                new Color(0.95f, 0.80f, 0.20f));
            hsLogoTxt.alignment = TextAlignmentOptions.Center;
            hsLogoTxt.gameObject.AddComponent<LayoutElement>().preferredHeight = 100;

            var hsSubTxt = MakeText(homeScreenGo, "SubtitleText", "Contradiction Engine", 32, FontStyles.Normal,
                new Color(0.65f, 0.65f, 0.70f));
            hsSubTxt.alignment = TextAlignmentOptions.Center;
            hsSubTxt.gameObject.AddComponent<LayoutElement>().preferredHeight = 50;

            // Spacer
            var hsSpacer = new GameObject("Spacer");
            hsSpacer.transform.SetParent(homeScreenGo.transform, false);
            hsSpacer.AddComponent<LayoutElement>().flexibleHeight = 1;

            var selectCaseBtn = MakeActionButton(homeScreenGo, "SelectCaseBtn", "SELECT A CASE",
                new Color(0.90f, 0.50f, 0.10f), Vector2.zero, Vector2.zero);
            selectCaseBtn.gameObject.AddComponent<LayoutElement>().preferredHeight = 100;

            var viewProfileBtn = MakeActionButton(homeScreenGo, "ViewProfileBtn", "VIEW USER PROFILE",
                new Color(0.22f, 0.45f, 0.75f), Vector2.zero, Vector2.zero);
            viewProfileBtn.gameObject.AddComponent<LayoutElement>().preferredHeight = 100;

            // ── Case Select Panel ──────────────────────────────────────
            var caseSelectGo = new GameObject("CaseSelectPanel");
            caseSelectGo.transform.SetParent(safeAreaRoot.transform, false);
            StretchFull(caseSelectGo);
            caseSelectGo.AddComponent<Image>().color = new Color(0.06f, 0.06f, 0.10f, 0.98f);
            caseSelectGo.SetActive(false);

            // Header bar
            var csHeaderGo = new GameObject("CSHeader");
            csHeaderGo.transform.SetParent(caseSelectGo.transform, false);
            var csHeaderRT = csHeaderGo.AddComponent<RectTransform>();
            csHeaderRT.anchorMin = new Vector2(0, 1); csHeaderRT.anchorMax = new Vector2(1, 1);
            csHeaderRT.pivot     = new Vector2(0.5f, 1f);
            csHeaderRT.anchoredPosition = Vector2.zero;
            csHeaderRT.sizeDelta = new Vector2(0, 100);
            csHeaderGo.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.20f);
            var csHeaderHLG = csHeaderGo.AddComponent<HorizontalLayoutGroup>();
            csHeaderHLG.childControlHeight    = true; csHeaderHLG.childControlWidth     = true;
            csHeaderHLG.childForceExpandHeight = true; csHeaderHLG.childForceExpandWidth = false;
            csHeaderHLG.padding  = new RectOffset(20, 20, 0, 0);
            csHeaderHLG.spacing  = 12;

            var csTitleTxt = MakeText(csHeaderGo, "CSTitle", "SELECT A CASE", 36, FontStyles.Bold,
                new Color(0.95f, 0.88f, 0.65f));
            csTitleTxt.alignment = TextAlignmentOptions.MidlineLeft;
            csTitleTxt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            var csBackBtn = MakeActionButton(csHeaderGo, "BackBtn", "← BACK",
                new Color(0.30f, 0.30f, 0.42f), Vector2.zero, Vector2.zero);
            csBackBtn.gameObject.AddComponent<LayoutElement>().preferredWidth = 140;

            // Scroll view for case list
            var csScrollGo = new GameObject("CaseScrollView");
            csScrollGo.transform.SetParent(caseSelectGo.transform, false);
            var csScrollRT = csScrollGo.AddComponent<RectTransform>();
            csScrollRT.anchorMin = Vector2.zero; csScrollRT.anchorMax = Vector2.one;
            csScrollRT.offsetMin = new Vector2(0, 0); csScrollRT.offsetMax = new Vector2(0, -100);
            var csScroll = csScrollGo.AddComponent<ScrollRect>();
            csScroll.horizontal = false;

            var csViewportGo = new GameObject("Viewport");
            csViewportGo.transform.SetParent(csScrollGo.transform, false);
            StretchFull(csViewportGo);
            csViewportGo.AddComponent<RectMask2D>();
            csScroll.viewport = csViewportGo.GetComponent<RectTransform>();

            var csContentGo = new GameObject("Content");
            csContentGo.transform.SetParent(csViewportGo.transform, false);
            var csContentRT = csContentGo.AddComponent<RectTransform>();
            csContentRT.anchorMin = new Vector2(0, 1); csContentRT.anchorMax = new Vector2(1, 1);
            csContentRT.pivot     = new Vector2(0.5f, 1f);
            csContentRT.offsetMin = Vector2.zero; csContentRT.offsetMax = Vector2.zero;
            var csVLG = csContentGo.AddComponent<VerticalLayoutGroup>();
            csVLG.childControlHeight    = true; csVLG.childControlWidth     = true;
            csVLG.childForceExpandWidth = true;
            csVLG.spacing = 4; csVLG.padding = new RectOffset(12, 12, 12, 12);
            var csCSF = csContentGo.AddComponent<ContentSizeFitter>();
            csCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csScroll.content  = csContentRT;

            // ── Account Panel ──────────────────────────────────────────
            var accountGo = new GameObject("AccountPanel");
            accountGo.transform.SetParent(safeAreaRoot.transform, false);
            StretchFull(accountGo);
            accountGo.AddComponent<Image>().color = new Color(0.06f, 0.06f, 0.10f, 0.98f);
            accountGo.SetActive(false);

            // Account header
            var acHeaderGo = new GameObject("ACHeader");
            acHeaderGo.transform.SetParent(accountGo.transform, false);
            var acHeaderRT = acHeaderGo.AddComponent<RectTransform>();
            acHeaderRT.anchorMin = new Vector2(0, 1); acHeaderRT.anchorMax = new Vector2(1, 1);
            acHeaderRT.pivot     = new Vector2(0.5f, 1f);
            acHeaderRT.anchoredPosition = Vector2.zero;
            acHeaderRT.sizeDelta = new Vector2(0, 100);
            acHeaderGo.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.20f);
            var acHeaderHLG = acHeaderGo.AddComponent<HorizontalLayoutGroup>();
            acHeaderHLG.childControlHeight    = true; acHeaderHLG.childControlWidth     = true;
            acHeaderHLG.childForceExpandHeight = true; acHeaderHLG.childForceExpandWidth = false;
            acHeaderHLG.padding = new RectOffset(20, 20, 0, 0); acHeaderHLG.spacing = 12;

            var acTitleTxt = MakeText(acHeaderGo, "ACTitle", "ACCOUNT", 36, FontStyles.Bold,
                new Color(0.95f, 0.88f, 0.65f));
            acTitleTxt.alignment = TextAlignmentOptions.MidlineLeft;
            acTitleTxt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            var acBackBtn = MakeActionButton(acHeaderGo, "ACBackBtn", "← BACK",
                new Color(0.30f, 0.30f, 0.42f), Vector2.zero, Vector2.zero);
            acBackBtn.gameObject.AddComponent<LayoutElement>().preferredWidth = 140;

            // Account tab bar
            var acTabBarGo = new GameObject("ACTabBar");
            acTabBarGo.transform.SetParent(accountGo.transform, false);
            var acTabBarRT = acTabBarGo.AddComponent<RectTransform>();
            acTabBarRT.anchorMin = new Vector2(0, 1); acTabBarRT.anchorMax = new Vector2(1, 1);
            acTabBarRT.pivot     = new Vector2(0.5f, 1f);
            acTabBarRT.anchoredPosition = new Vector2(0, -100);
            acTabBarRT.sizeDelta = new Vector2(0, 70);
            acTabBarGo.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.16f);
            var acTabHLG = acTabBarGo.AddComponent<HorizontalLayoutGroup>();
            acTabHLG.childControlHeight    = true; acTabHLG.childControlWidth     = true;
            acTabHLG.childForceExpandHeight = true; acTabHLG.childForceExpandWidth = true;

            var infoTabBtn    = MakeTabButton(acTabBarGo, "InfoTabBtn",    "INFO");
            var resultsTabBtn = MakeTabButton(acTabBarGo, "ResultsTabBtn", "CASE RESULTS");

            // Account content area (below header + tab bar = 170px from top)
            var acContentGo = new GameObject("ACContent");
            acContentGo.transform.SetParent(accountGo.transform, false);
            var acContentRT = acContentGo.AddComponent<RectTransform>();
            acContentRT.anchorMin = Vector2.zero; acContentRT.anchorMax = Vector2.one;
            acContentRT.offsetMin = Vector2.zero; acContentRT.offsetMax = new Vector2(0, -170);

            // ── INFO tab panel ─────────────────────────────────────────
            var infoPanel = MakePanel(acContentGo, "InfoPanel", C_BG);
            var infoPanelVLG = infoPanel.AddComponent<VerticalLayoutGroup>();
            infoPanelVLG.padding              = new RectOffset(40, 40, 40, 40);
            infoPanelVLG.spacing              = 20;
            infoPanelVLG.childControlHeight   = true;
            infoPanelVLG.childControlWidth    = true;
            infoPanelVLG.childForceExpandWidth = true;

            var infoHeaderTxt = MakeText(infoPanel, "InfoHeader", "PLAYER STATS", 34, FontStyles.Bold,
                new Color(0.90f, 0.50f, 0.10f));
            infoHeaderTxt.gameObject.AddComponent<LayoutElement>().preferredHeight = 60;

            var totalScoreTxt     = MakeStatRow(infoPanel, "Total Score",     "0");
            var casesCompletedTxt = MakeStatRow(infoPanel, "Cases Completed", "0");
            var perfectSolvesTxt  = MakeStatRow(infoPanel, "Perfect Solves",  "0");

            // ── CASE RESULTS tab panel ─────────────────────────────────
            var resultsPanel = MakePanel(acContentGo, "CaseResultsPanel", C_BG);
            resultsPanel.SetActive(false);

            var resScrollGo = new GameObject("ResultsScrollView");
            resScrollGo.transform.SetParent(resultsPanel.transform, false);
            StretchFull(resScrollGo);
            var resScroll = resScrollGo.AddComponent<ScrollRect>();
            resScroll.horizontal = false;

            var resViewportGo = new GameObject("Viewport");
            resViewportGo.transform.SetParent(resScrollGo.transform, false);
            StretchFull(resViewportGo);
            resViewportGo.AddComponent<RectMask2D>();
            resScroll.viewport = resViewportGo.GetComponent<RectTransform>();

            var resContentGo = new GameObject("Content");
            resContentGo.transform.SetParent(resViewportGo.transform, false);
            var resContentRT = resContentGo.AddComponent<RectTransform>();
            resContentRT.anchorMin = new Vector2(0, 1); resContentRT.anchorMax = new Vector2(1, 1);
            resContentRT.pivot     = new Vector2(0.5f, 1f);
            resContentRT.offsetMin = Vector2.zero; resContentRT.offsetMax = Vector2.zero;
            var resVLG = resContentGo.AddComponent<VerticalLayoutGroup>();
            resVLG.childControlHeight    = true; resVLG.childControlWidth     = true;
            resVLG.childForceExpandWidth = true;
            resVLG.spacing = 4; resVLG.padding = new RectOffset(12, 12, 12, 12);
            var resCSF = resContentGo.AddComponent<ContentSizeFitter>();
            resCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            resScroll.content  = resContentRT;

            // ── In-Game Menu Panel (slide-in overlay, inside SafeAreaRoot) ──
            var inGameMenuGo = new GameObject("InGameMenuPanel");
            inGameMenuGo.transform.SetParent(safeAreaRoot.transform, false);
            StretchFull(inGameMenuGo);
            inGameMenuGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.82f);
            inGameMenuGo.SetActive(false);

            var menuCardGo = MakeImage(inGameMenuGo, "MenuCard", C_PANEL);
            var mcRT = menuCardGo.GetComponent<RectTransform>();
            mcRT.anchorMin = new Vector2(0.10f, 0.25f); mcRT.anchorMax = new Vector2(0.90f, 0.82f);
            mcRT.offsetMin = Vector2.zero;               mcRT.offsetMax = Vector2.zero;

            var menuTitle = MakeText(menuCardGo, "MenuTitle", "GAME MENU", 36, FontStyles.Bold,
                new Color(0.95f, 0.80f, 0.20f));
            menuTitle.alignment = TextAlignmentOptions.Center;
            var mtRT = menuTitle.gameObject.GetComponent<RectTransform>();
            mtRT.anchorMin = new Vector2(0, 0.82f); mtRT.anchorMax = new Vector2(1, 1);
            mtRT.offsetMin = new Vector2(20, 0);    mtRT.offsetMax = new Vector2(-20, -8);

            var resumeMenuBtn = MakeActionButton(menuCardGo, "ResumeBtn", "RESUME INVESTIGATION",
                new Color(0.20f, 0.65f, 0.40f), new Vector2(0.08f, 0.58f), new Vector2(0.92f, 0.74f));
            var caseSelectMenuBtn = MakeActionButton(menuCardGo, "CaseSelectBtn", "SELECT CASE",
                new Color(0.22f, 0.45f, 0.75f), new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.54f));
            var homeMenuBtn = MakeActionButton(menuCardGo, "HomeBtn", "MAIN MENU",
                new Color(0.55f, 0.18f, 0.18f), new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.34f));

            var inGameMenuCtrl = inGameMenuGo.AddComponent<InGameMenuController>();

            // ── Confirm Dialog — added last so it renders above everything ──
            var confirmGo = new GameObject("ConfirmDialog");
            confirmGo.transform.SetParent(safeAreaRoot.transform, false);
            StretchFull(confirmGo);
            confirmGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);
            confirmGo.SetActive(false);

            var dialogCard = MakeImage(confirmGo, "DialogCard", C_PANEL);
            var dCardRT = dialogCard.GetComponent<RectTransform>();
            dCardRT.anchorMin = new Vector2(0.07f, 0.32f); dCardRT.anchorMax = new Vector2(0.93f, 0.68f);
            dCardRT.offsetMin = Vector2.zero;               dCardRT.offsetMax = Vector2.zero;

            var dialogMsgTxt = MakeText(dialogCard, "MessageText",
                "Leaving now will lose your current progress.\n\nAre you sure?",
                28, FontStyles.Normal);
            dialogMsgTxt.textWrappingMode = TextWrappingModes.Normal;
            dialogMsgTxt.alignment        = TextAlignmentOptions.Center;
            var dmRT = dialogMsgTxt.gameObject.GetComponent<RectTransform>();
            dmRT.anchorMin = new Vector2(0.05f, 0.45f); dmRT.anchorMax = new Vector2(0.95f, 0.95f);
            dmRT.offsetMin = Vector2.zero;               dmRT.offsetMax = Vector2.zero;

            var dialogConfirmBtn = MakeActionButton(dialogCard, "ConfirmBtn", "LEAVE",
                new Color(0.80f, 0.22f, 0.22f), new Vector2(0.04f, 0.06f), new Vector2(0.46f, 0.38f));
            var dialogCancelBtn  = MakeActionButton(dialogCard, "CancelBtn", "STAY",
                new Color(0.22f, 0.55f, 0.22f), new Vector2(0.54f, 0.06f), new Vector2(0.96f, 0.38f));

            var confirmDialogComp = confirmGo.AddComponent<ConfirmDialog>();

            // ── Systems GameObject ─────────────────────────────────────
            var systemsGo = new GameObject("Systems");

            var gm              = systemsGo.AddComponent<GameManager>();
            var caseLoader      = systemsGo.AddComponent<CaseLoader>();
            var evaluator       = systemsGo.AddComponent<ContradictionEvaluator>();
            var results         = systemsGo.AddComponent<ResultsController>();
            var discoverySystem = systemsGo.AddComponent<EvidenceDiscoverySystem>();
            var scoringSystem   = systemsGo.AddComponent<ScoringSystem>();
            var navMgr          = systemsGo.AddComponent<NavigationManager>();

            var tabCtrl    = tabContentGo.AddComponent<TabController>();
            var detailComp = detailPanelGo.AddComponent<EvidenceDetailPanel>();

            var homeCtrl    = homeScreenGo.AddComponent<HomeScreenController>();
            var caseSelCtrl = caseSelectGo.AddComponent<CaseSelectController>();
            var accountCtrl = accountGo.AddComponent<AccountScreenController>();

            // ── Wire serialized fields ─────────────────────────────────

            Wire(caseLoader, so => {
                so.FindProperty("sceneBackground").objectReferenceValue    = sceneImgGo.GetComponent<Image>();
                so.FindProperty("caseTitleText").objectReferenceValue      = caseTitleText;
                so.FindProperty("briefText").objectReferenceValue          = briefBodyText;
                so.FindProperty("evidenceListParent").objectReferenceValue = null;
                so.FindProperty("evidenceCardPrefab").objectReferenceValue = null;
                so.FindProperty("claimsListParent").objectReferenceValue   = claimsContent.transform;
                so.FindProperty("claimCardPrefab").objectReferenceValue    = claimCardPrefab;
                so.FindProperty("hotspotParent").objectReferenceValue      = sceneHotspotGo.transform;
                so.FindProperty("hotspotLayerRect").objectReferenceValue   = sceneHotspotRT;
                so.FindProperty("hotspotPrefab").objectReferenceValue      = hotspotPrefab;
            });

            Wire(tabCtrl, so => {
                // Brief=0, Scene=1, Evidence=2, Solve=3
                SetObjectArray(so, "tabPanels",  briefPanel, scenePanel, evidencePanel, solvePanel);
                SetObjectArray(so, "tabButtons", briefBtn, sceneBtn, evidenceBtn, solveBtn);
            });

            Wire(results, so => {
                so.FindProperty("resultPanel").objectReferenceValue      = resultPanelGo;
                so.FindProperty("resultHeaderText").objectReferenceValue = resultHeaderTxt;
                so.FindProperty("explanationText").objectReferenceValue  = explanationTxt;
                so.FindProperty("scoreText").objectReferenceValue        = scoreTxt;
                so.FindProperty("nextCaseButton").objectReferenceValue   = nextCaseBtn;
                so.FindProperty("retryButton").objectReferenceValue      = retryBtn;
                so.FindProperty("hintButton").objectReferenceValue       = hintBtn;
                so.FindProperty("resultHeaderBg").objectReferenceValue   = resultHeaderBgGo.GetComponent<Image>();
            });

            Wire(scoringSystem, so => {
                so.FindProperty("timerText").objectReferenceValue = timerTxt;
            });

            Wire(detailComp, so => {
                so.FindProperty("panel").objectReferenceValue           = detailPanelGo;
                so.FindProperty("titleText").objectReferenceValue       = detailTitle;
                so.FindProperty("descriptionText").objectReferenceValue = detailDesc;
                so.FindProperty("tagsText").objectReferenceValue        = detailTags;
                so.FindProperty("closeButton").objectReferenceValue     = closeBtn;
                so.FindProperty("enhanceButton").objectReferenceValue   = enhDetailBtn;
                so.FindProperty("viewer").objectReferenceValue          = viewer;
                so.FindProperty("textModeRoot").objectReferenceValue    = textModeRootGo;
                so.FindProperty("textModeBg").objectReferenceValue      = textModeBgImg;
                so.FindProperty("textModeBody").objectReferenceValue    = textBodyTmp;
            });

            Wire(viewer, so => {
                so.FindProperty("mainImage").objectReferenceValue    = mainRawImg;
                so.FindProperty("enhanceOverlay").objectReferenceValue = enhOverlayImg;
            });

            Wire(submitComp, so => {
                so.FindProperty("button").objectReferenceValue = submitBtn;
                so.FindProperty("label").objectReferenceValue  = submitLabel;
            });

            Wire(discoverySystem, so => {
                so.FindProperty("foundCounterText").objectReferenceValue      = invFoundTxt;
                so.FindProperty("analyseButton").objectReferenceValue         = analyseBtn;
                so.FindProperty("investigationOverlay").objectReferenceValue  = sceneFoundBar;
                so.FindProperty("investigationHintText").objectReferenceValue = invHintTxt;
                so.FindProperty("evidenceTabParent").objectReferenceValue     = evidenceTabContent.transform;
                so.FindProperty("evidenceTabCardPrefab").objectReferenceValue = evidenceCardPrefab;
            });

            Wire(homeCtrl, so => {
                so.FindProperty("selectCaseBtn").objectReferenceValue = selectCaseBtn;
                so.FindProperty("viewProfileBtn").objectReferenceValue = viewProfileBtn;
            });

            Wire(caseSelCtrl, so => {
                so.FindProperty("listParent").objectReferenceValue = csContentGo.transform;
                so.FindProperty("closeBtn").objectReferenceValue   = csBackBtn;
            });

            Wire(accountCtrl, so => {
                so.FindProperty("infoPanel").objectReferenceValue       = infoPanel;
                so.FindProperty("resultsPanel").objectReferenceValue    = resultsPanel;
                so.FindProperty("closeBtn").objectReferenceValue        = acBackBtn;
                so.FindProperty("infoTabBtn").objectReferenceValue      = infoTabBtn;
                so.FindProperty("resultsTabBtn").objectReferenceValue   = resultsTabBtn;
                so.FindProperty("totalScoreText").objectReferenceValue     = totalScoreTxt;
                so.FindProperty("casesCompletedText").objectReferenceValue = casesCompletedTxt;
                so.FindProperty("perfectSolvesText").objectReferenceValue  = perfectSolvesTxt;
                so.FindProperty("resultsListParent").objectReferenceValue  = resContentGo.transform;
            });

            Wire(gameScreenCtrl, so => {
                so.FindProperty("hamburgerBtn").objectReferenceValue = hamburgerGo.GetComponent<Button>();
            });

            Wire(inGameMenuCtrl, so => {
                so.FindProperty("resumeBtn").objectReferenceValue      = resumeMenuBtn;
                so.FindProperty("caseSelectBtn").objectReferenceValue  = caseSelectMenuBtn;
                so.FindProperty("homeBtn").objectReferenceValue        = homeMenuBtn;
            });

            Wire(confirmDialogComp, so => {
                so.FindProperty("messageText").objectReferenceValue = dialogMsgTxt;
                so.FindProperty("confirmBtn").objectReferenceValue  = dialogConfirmBtn;
                so.FindProperty("cancelBtn").objectReferenceValue   = dialogCancelBtn;
            });

            Wire(navMgr, so => {
                so.FindProperty("homeScreen").objectReferenceValue      = homeCtrl;
                so.FindProperty("caseSelectScreen").objectReferenceValue = caseSelCtrl;
                so.FindProperty("accountScreen").objectReferenceValue   = accountCtrl;
                so.FindProperty("gameScreen").objectReferenceValue      = gameScreenCtrl;
                so.FindProperty("inGameMenuScreen").objectReferenceValue = inGameMenuCtrl;
                so.FindProperty("confirmDialog").objectReferenceValue   = confirmDialogComp;
                so.FindProperty("canvasRT").objectReferenceValue        = canvasGo.GetComponent<RectTransform>();
            });

            // Load cases from Resources and assign to GameManager
            var cases = Resources.LoadAll<CaseData>("Cases");
            Wire(gm, so => {
                var prop = so.FindProperty("availableCases");
                prop.arraySize = cases.Length;
                for (int i = 0; i < cases.Length; i++)
                    prop.GetArrayElementAtIndex(i).objectReferenceValue = cases[i];
            });

            // ── Save scene ─────────────────────────────────────────────
            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Scene Built",
                $"CaseScene.unity saved to Assets/Scenes/\n\n" +
                $"{cases.Length} case(s) loaded into GameManager.\n\n" +
                "Press Play to test Case 001.",
                "Let's Go");

            Debug.Log($"[SceneBuilder] Done — {cases.Length} cases wired.");
        }

        // ══════════════════════════════════════════════════════════════
        // PREFAB BUILDERS
        // ══════════════════════════════════════════════════════════════

        static GameObject BuildEvidenceCardPrefab()
        {
            var root = new GameObject("EvidenceCard");
            root.AddComponent<RectTransform>();
            root.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.18f);

            // Root HLG — text left (58%), photo right (42%), fixed 280px height
            var hlg = root.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlHeight     = true;
            hlg.childControlWidth      = true;
            hlg.childForceExpandHeight = true;
            hlg.childForceExpandWidth  = false;
            hlg.spacing = 0;

            var rootLE = root.AddComponent<LayoutElement>();
            rootLE.preferredHeight = 280;
            rootLE.minHeight       = 280;
            rootLE.flexibleWidth   = 1;

            // Highlight overlay — ignoreLayout so HLG skips it, StretchFull to cover card
            var hl = MakeImage(root, "Highlight", new Color(0.30f, 0.70f, 1f, 0.22f));
            StretchFull(hl);
            hl.GetComponent<Image>().enabled = false;
            hl.AddComponent<LayoutElement>().ignoreLayout = true;

            // ── Left: info region (58% width) ─────────────────────────
            var infoRegion = new GameObject("InfoRegion");
            infoRegion.transform.SetParent(root.transform, false);
            infoRegion.AddComponent<RectTransform>();
            infoRegion.AddComponent<LayoutElement>().flexibleWidth = 58;

            var infoVLG = infoRegion.AddComponent<VerticalLayoutGroup>();
            infoVLG.childControlHeight     = true;
            infoVLG.childControlWidth      = true;
            infoVLG.childForceExpandHeight = false;
            infoVLG.childForceExpandWidth  = true;
            infoVLG.padding = new RectOffset(14, 10, 12, 12);
            infoVLG.spacing = 6;

            var nameText = MakeText(infoRegion, "NameText", "Evidence Name", 30, FontStyles.Bold);
            nameText.alignment        = TextAlignmentOptions.TopLeft;
            nameText.textWrappingMode = TextWrappingModes.Normal;
            nameText.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;

            var sep1 = MakeImage(infoRegion, "Sep1", new Color(0.28f, 0.28f, 0.40f));
            sep1.AddComponent<LayoutElement>().preferredHeight = 1;

            // Description fills remaining height with ellipsis if too long
            var descText = MakeText(infoRegion, "DescriptionText", "", 20,
                FontStyles.Normal, new Color(0.78f, 0.78f, 0.84f));
            descText.alignment        = TextAlignmentOptions.TopLeft;
            descText.textWrappingMode = TextWrappingModes.Normal;
            descText.overflowMode     = TextOverflowModes.Ellipsis;
            descText.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;

            var sep2 = MakeImage(infoRegion, "Sep2", new Color(0.28f, 0.28f, 0.40f));
            sep2.AddComponent<LayoutElement>().preferredHeight = 1;

            var tagsText = MakeText(infoRegion, "TagsText", "", 19,
                FontStyles.Normal, new Color(0.55f, 0.80f, 1f));
            tagsText.alignment        = TextAlignmentOptions.TopLeft;
            tagsText.textWrappingMode = TextWrappingModes.Normal;
            tagsText.gameObject.AddComponent<LayoutElement>().preferredHeight = 36;

            // ── Right: image region (42% width, full height via HLG) ──
            var imgRegion = new GameObject("ImageRegion");
            imgRegion.transform.SetParent(root.transform, false);
            imgRegion.AddComponent<RectTransform>();
            imgRegion.AddComponent<LayoutElement>().flexibleWidth = 42;
            var thumb = imgRegion.AddComponent<Image>();
            thumb.color          = new Color(0.18f, 0.18f, 0.26f);
            thumb.preserveAspect = true;

            var comp = root.AddComponent<EvidenceCardUI>();
            Wire(comp, so => {
                so.FindProperty("thumbnail").objectReferenceValue           = thumb;
                so.FindProperty("nameText").objectReferenceValue            = nameText;
                so.FindProperty("descriptionText").objectReferenceValue     = descText;
                so.FindProperty("tagsText").objectReferenceValue            = tagsText;
                so.FindProperty("crossCheckHighlight").objectReferenceValue = hl.GetComponent<Image>();
            });

            return SavePrefab(root, "EvidenceCard");
        }

        static GameObject BuildClaimCardPrefab()
        {
            var root = new GameObject("ClaimCard");
            root.AddComponent<RectTransform>();
            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.14f, 0.14f, 0.20f);

            // Speaker name — top 18%
            var speakerTxt = MakeText(root, "SpeakerText", "Speaker", 26, FontStyles.Bold);
            speakerTxt.color = new Color(0.90f, 0.70f, 0.30f);
            speakerTxt.alignment = TextAlignmentOptions.Left;
            var sRT = speakerTxt.gameObject.GetComponent<RectTransform>();
            sRT.anchorMin = new Vector2(0, 0.82f); sRT.anchorMax = new Vector2(1, 1);
            sRT.offsetMin = new Vector2(14, 0); sRT.offsetMax = new Vector2(-14, -6);

            // Claim body text — middle 42%
            var claimTxt = MakeText(root, "ClaimText", "Claim text...", 24, FontStyles.Normal);
            claimTxt.textWrappingMode = TextWrappingModes.Normal;
            claimTxt.alignment = TextAlignmentOptions.TopLeft;
            var cRT = claimTxt.gameObject.GetComponent<RectTransform>();
            cRT.anchorMin = new Vector2(0, 0.40f); cRT.anchorMax = new Vector2(1, 0.82f);
            cRT.offsetMin = new Vector2(14, 4); cRT.offsetMax = new Vector2(-14, 0);

            // Tag hint row — always visible, shows referenced evidence tags
            var tagTxt = MakeText(root, "TagHintText", "", 20, FontStyles.Normal, new Color(0.45f, 0.75f, 1f));
            tagTxt.alignment = TextAlignmentOptions.Left;
            var tRT = tagTxt.gameObject.GetComponent<RectTransform>();
            tRT.anchorMin = new Vector2(0, 0.22f); tRT.anchorMax = new Vector2(1, 0.40f);
            tRT.offsetMin = new Vector2(14, 0); tRT.offsetMax = new Vector2(-14, 0);

            // Focus panel — long-press actions (Cross-Check / Highlight), bottom 22%
            var focusPanel = new GameObject("FocusActionsPanel");
            focusPanel.transform.SetParent(root.transform, false);
            var fpRT = focusPanel.AddComponent<RectTransform>();
            fpRT.anchorMin = new Vector2(0, 0); fpRT.anchorMax = new Vector2(1, 0.22f);
            fpRT.offsetMin = Vector2.zero; fpRT.offsetMax = Vector2.zero;
            focusPanel.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.20f, 0.96f);
            var fpHLG = focusPanel.AddComponent<HorizontalLayoutGroup>();
            fpHLG.childControlHeight = true; fpHLG.childControlWidth = true;
            fpHLG.childForceExpandHeight = true; fpHLG.childForceExpandWidth = true;
            fpHLG.spacing = 4; fpHLG.padding = new RectOffset(4, 4, 4, 4);
            var ccBtn = MakeSimpleButton(focusPanel, "CrossCheckBtn", "CROSS-CHECK", new Color(0.20f, 0.50f, 0.90f));
            var hlBtn = MakeSimpleButton(focusPanel, "HighlightBtn",  "HIGHLIGHT",  new Color(0.50f, 0.50f, 0.80f));
            focusPanel.SetActive(false);

            var le = root.AddComponent<LayoutElement>();
            le.preferredHeight = 230;
            le.minHeight       = 230;
            le.flexibleWidth   = 1;

            var comp = root.AddComponent<ClaimCardUI>();
            Wire(comp, so => {
                so.FindProperty("speakerText").objectReferenceValue      = speakerTxt;
                so.FindProperty("claimText").objectReferenceValue        = claimTxt;
                so.FindProperty("tagHintText").objectReferenceValue      = tagTxt;
                so.FindProperty("background").objectReferenceValue       = bg;
                so.FindProperty("focusActionsPanel").objectReferenceValue = focusPanel;
                so.FindProperty("crossCheckBtn").objectReferenceValue    = ccBtn.GetComponent<Button>();
                so.FindProperty("highlightBtn").objectReferenceValue     = hlBtn.GetComponent<Button>();
            });

            return SavePrefab(root, "ClaimCard");
        }

        static GameObject BuildHotspotPrefab()
        {
            // Hotspots are INVISIBLE — the magnifying glass mechanic reveals them.
            // Only a discovered badge appears after finding.
            var root = new GameObject("Hotspot");
            var rt = root.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(160, 160);   // detection area

            // Transparent Image required so OnPointerClick (tap fallback) fires
            var hitImg = root.AddComponent<Image>();
            hitImg.color = new Color(0, 0, 0, 0);

            // Discovered badge — green ✓, hidden until found by the glass
            var badge = new GameObject("DiscoveredBadge");
            badge.transform.SetParent(root.transform, false);
            var badgeRT = badge.AddComponent<RectTransform>();
            badgeRT.anchorMin = new Vector2(0.15f, 0.15f);
            badgeRT.anchorMax = new Vector2(0.85f, 0.85f);
            badgeRT.offsetMin = Vector2.zero; badgeRT.offsetMax = Vector2.zero;
            badge.AddComponent<Image>().color = new Color(0.15f, 0.65f, 0.30f, 0.90f);
            var checkGo = new GameObject("Check");
            checkGo.transform.SetParent(badge.transform, false);
            var ckRT = checkGo.AddComponent<RectTransform>();
            ckRT.anchorMin = Vector2.zero; ckRT.anchorMax = Vector2.one;
            ckRT.offsetMin = Vector2.zero; ckRT.offsetMax = Vector2.zero;
            var ckTmp = checkGo.AddComponent<TextMeshProUGUI>();
            ckTmp.text = "OK"; ckTmp.fontSize = 22; ckTmp.fontStyle = FontStyles.Bold;
            ckTmp.alignment = TextAlignmentOptions.Center;
            ckTmp.color = Color.white; ckTmp.raycastTarget = false;
            badge.SetActive(false);

            var comp = root.AddComponent<HotspotController>();
            Wire(comp, so => {
                so.FindProperty("discoveredBadge").objectReferenceValue = badge;
            });

            return SavePrefab(root, "Hotspot");
        }

        static GameObject BuildFoundEvidenceCardPrefab()
        {
            var root = new GameObject("FoundEvidenceCard");
            var rt = root.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(110, 110);

            root.AddComponent<Image>().color = new Color(0.14f, 0.14f, 0.22f);

            // Thumbnail fills most of the card
            var thumb = MakeImage(root, "Thumbnail", new Color(0.25f, 0.25f, 0.32f));
            var tRT = thumb.GetComponent<RectTransform>();
            tRT.anchorMin = new Vector2(0, 0.28f); tRT.anchorMax = new Vector2(1, 1);
            tRT.offsetMin = new Vector2(6, 0); tRT.offsetMax = new Vector2(-6, -6);
            thumb.GetComponent<Image>().preserveAspect = true;

            // Name label at the bottom
            var lbl = MakeText(root, "NameLabel", "", 17, FontStyles.Normal,
                new Color(0.85f, 0.85f, 0.90f));
            lbl.textWrappingMode = TextWrappingModes.Normal;
            lbl.alignment = TextAlignmentOptions.Center;
            var lRT = lbl.gameObject.GetComponent<RectTransform>();
            lRT.anchorMin = new Vector2(0, 0); lRT.anchorMax = new Vector2(1, 0.28f);
            lRT.offsetMin = new Vector2(4, 2); lRT.offsetMax = new Vector2(-4, 0);

            var le = root.AddComponent<LayoutElement>();
            le.preferredWidth  = 110;
            le.preferredHeight = 110;
            le.flexibleWidth   = 0;

            var comp = root.AddComponent<CasebookGame.UI.FoundEvidenceCard>();
            Wire(comp, so => {
                so.FindProperty("thumbnail").objectReferenceValue = thumb.GetComponent<Image>();
                so.FindProperty("nameLabel").objectReferenceValue = lbl;
            });

            return SavePrefab(root, "FoundEvidenceCard");
        }

        static GameObject BuildBoardSlotPrefab()
        {
            var root = new GameObject("BoardSlot");
            root.AddComponent<RectTransform>();
            root.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.17f);

            var emptyGo = new GameObject("EmptyIndicator");
            emptyGo.AddComponent<RectTransform>();   // must be explicit before SetParent in Unity 6
            emptyGo.transform.SetParent(root.transform, false);
            StretchFull(emptyGo, new Vector2(10, 10));
            var emptyTxt = emptyGo.AddComponent<TextMeshProUGUI>();
            emptyTxt.text = "+ PIN"; emptyTxt.fontSize = 26;
            emptyTxt.alignment = TextAlignmentOptions.Center;
            emptyTxt.color = new Color(0.40f, 0.40f, 0.50f);

            var thumb = MakeImage(root, "Thumbnail", new Color(0.25f, 0.25f, 0.30f));
            var tRT = thumb.GetComponent<RectTransform>();
            tRT.anchorMin = new Vector2(0, 0.30f); tRT.anchorMax = new Vector2(1, 1f);
            tRT.offsetMin = new Vector2(8, 0); tRT.offsetMax = new Vector2(-8, -8);
            thumb.GetComponent<Image>().enabled = false;

            var labelTxt = MakeText(root, "LabelText", "", 20, FontStyles.Normal);
            var lRT = labelTxt.gameObject.GetComponent<RectTransform>();
            lRT.anchorMin = new Vector2(0, 0); lRT.anchorMax = new Vector2(1, 0.30f);
            lRT.offsetMin = new Vector2(4, 4); lRT.offsetMax = new Vector2(-4, 0);

            var removeBtn = MakeSimpleButton(root, "RemoveBtn", "✕", new Color(0.70f, 0.20f, 0.20f));
            var rbRT = removeBtn.GetComponent<RectTransform>();
            rbRT.anchorMin = new Vector2(0.65f, 0.78f); rbRT.anchorMax = new Vector2(1f, 1f);
            rbRT.offsetMin = Vector2.zero; rbRT.offsetMax = Vector2.zero;
            removeBtn.SetActive(false);

            var comp = root.AddComponent<BoardSlotUI>();
            Wire(comp, so => {
                so.FindProperty("thumbnailImage").objectReferenceValue = thumb.GetComponent<Image>();
                so.FindProperty("labelText").objectReferenceValue      = labelTxt;
                so.FindProperty("removeButton").objectReferenceValue   = removeBtn.GetComponent<Button>();
                so.FindProperty("emptyIndicator").objectReferenceValue = emptyGo;
            });

            return SavePrefab(root, "BoardSlot");
        }

        // ══════════════════════════════════════════════════════════════
        // UTILITY HELPERS
        // ══════════════════════════════════════════════════════════════

        static GameObject MakePanel(GameObject parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = color;
            return go;
        }

        static GameObject MakeImage(GameObject parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>();
            go.AddComponent<Image>().color = color;
            return go;
        }

        static TextMeshProUGUI MakeText(GameObject parent, string name, string text, float size, FontStyles style,
            Color? color = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>();
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = size; tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color ?? new Color(0.92f, 0.92f, 0.95f);  // near-white, readable on dark panels
            return tmp;
        }

        static Button MakeTabButton(GameObject parent, string name, string label)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>();
            var img = go.AddComponent<Image>();
            img.color = new Color(0.18f, 0.18f, 0.28f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var lbl = MakeText(go, "Label", label, 22, FontStyles.Bold);
            lbl.color = new Color(0.85f, 0.85f, 0.85f);
            // Auto-size so the label always fits regardless of device scale
            lbl.enableAutoSizing  = true;
            lbl.fontSizeMin       = 12;
            lbl.fontSizeMax       = 22;
            lbl.textWrappingMode  = TextWrappingModes.NoWrap;
            lbl.overflowMode      = TextOverflowModes.Ellipsis;
            StretchFull(lbl.gameObject, new Vector2(4, 4));
            return btn;
        }

        static GameObject MakeToolButton(GameObject parent, string name, string label, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>();
            var img = go.AddComponent<Image>();
            img.color = color;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var lbl = MakeText(go, "Label", label, 18, FontStyles.Bold, new Color(0.06f, 0.06f, 0.10f));
            lbl.raycastTarget = false;
            StretchFull(lbl.gameObject, new Vector2(4, 4));
            return go;
        }

        // Returns the value TMP_Text (right side) so controllers can update it.
        static TMP_Text MakeStatRow(GameObject parent, string labelStr, string valueStr)
        {
            var rowGo = new GameObject($"Stat_{labelStr}");
            rowGo.transform.SetParent(parent.transform, false);
            var le = rowGo.AddComponent<LayoutElement>();
            le.preferredHeight = 72;
            le.flexibleWidth   = 1;
            rowGo.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.20f);

            var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlHeight    = true; hlg.childControlWidth     = true;
            hlg.childForceExpandHeight = true; hlg.childForceExpandWidth = false;
            hlg.padding = new RectOffset(24, 24, 0, 0); hlg.spacing = 12;

            var lGo  = new GameObject("Label");
            lGo.transform.SetParent(rowGo.transform, false);
            var lTxt = lGo.AddComponent<TextMeshProUGUI>();
            lTxt.text      = labelStr;
            lTxt.fontSize  = 28;
            lTxt.color     = new Color(0.75f, 0.75f, 0.80f);
            lGo.AddComponent<LayoutElement>().flexibleWidth = 1;

            var vGo  = new GameObject("Value");
            vGo.transform.SetParent(rowGo.transform, false);
            var vTxt = vGo.AddComponent<TextMeshProUGUI>();
            vTxt.text      = valueStr;
            vTxt.fontSize  = 28;
            vTxt.fontStyle = FontStyles.Bold;
            vTxt.color     = new Color(0.95f, 0.88f, 0.65f);
            vTxt.alignment = TextAlignmentOptions.Right;
            vGo.AddComponent<LayoutElement>().preferredWidth = 200;

            return vTxt;
        }

        static Button MakeActionButton(GameObject parent, string name, string label, Color color,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var lbl = MakeText(go, "Label", label, 26, FontStyles.Bold);
            StretchFull(lbl.gameObject);
            return btn;
        }

        static GameObject MakeSimpleButton(GameObject parent, string name, string label, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>();
            var img = go.AddComponent<Image>();
            img.color = color;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var lbl = MakeText(go, "Label", label, 20, FontStyles.Bold, new Color(0.06f, 0.06f, 0.10f));
            lbl.raycastTarget = false;
            StretchFull(lbl.gameObject, new Vector2(4, 4));
            return go;
        }

        // Brief/Board: simple VLG, no scroll — content always visible, no timing issues.
        static GameObject AddSimpleContent(GameObject panel)
        {
            var content = new GameObject("Content");
            content.transform.SetParent(panel.transform, false);
            var cRT = content.AddComponent<RectTransform>();
            cRT.anchorMin = Vector2.zero; cRT.anchorMax = Vector2.one;
            cRT.offsetMin = new Vector2(16, 16); cRT.offsetMax = new Vector2(-16, -16);
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.childControlHeight    = true;
            vlg.childControlWidth     = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth  = true;
            vlg.spacing = 16;
            return content;
        }

        // Evidence/Claims: panel IS the viewport — ScrollRect with fixed tall content.
        // Avoids ContentSizeFitter first-frame height=0 problem.
        static GameObject AddScrollList(GameObject panel)
        {
            // Panel image acts as the mask source
            panel.AddComponent<Mask>().showMaskGraphic = true;
            var panelRT = panel.GetComponent<RectTransform>();

            var sr = panel.AddComponent<ScrollRect>();
            sr.horizontal       = false;
            sr.vertical         = true;
            sr.scrollSensitivity = 40f;
            sr.viewport         = panelRT;

            var content = new GameObject("Content");
            content.transform.SetParent(panel.transform, false);
            var cRT = content.AddComponent<RectTransform>();
            // Anchor to top of panel, grow downward
            cRT.anchorMin = new Vector2(0, 1);
            cRT.anchorMax = new Vector2(1, 1);
            cRT.pivot     = new Vector2(0.5f, 1f);
            cRT.offsetMin = Vector2.zero;
            cRT.offsetMax = Vector2.zero;
            // Fixed tall height — cards stack from the top, scroll shows them all.
            // No ContentSizeFitter: avoids the frame-0 height=0 clip problem.
            cRT.sizeDelta = new Vector2(0, 6000f);

            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.childControlHeight    = true;
            vlg.childControlWidth     = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth  = true;
            vlg.spacing  = 10;
            vlg.padding  = new RectOffset(10, 10, 10, 10);

            sr.content = cRT;
            return content;
        }

        static void StretchFull(GameObject go, Vector2? pad = null)
        {
            // Unity fake-null means ?? won't trigger — must use if (rt == null) instead
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            var p = pad ?? Vector2.zero;
            rt.offsetMin = new Vector2(p.x, p.y);
            rt.offsetMax = new Vector2(-p.x, -p.y);
        }

        static GameObject InstantiatePrefabChild(GameObject prefab, GameObject parent, string name)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        static GameObject SavePrefab(GameObject go, string name)
        {
            string path = $"{PREFABS_PATH}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // SerializedObject wiring helpers
        static void Wire(Object target, System.Action<SerializedObject> apply)
        {
            var so = new SerializedObject(target);
            apply(so);
            so.ApplyModifiedProperties();
        }

        static void SetObjectArray(SerializedObject so, string propName, params Object[] items)
        {
            var prop = so.FindProperty(propName);
            prop.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            string parent = string.Join("/", parts, 0, parts.Length - 1);
            AssetDatabase.CreateFolder(parent, parts[parts.Length - 1]);
        }
    }
}
#endif
