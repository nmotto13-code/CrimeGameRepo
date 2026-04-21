#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CasebookGame.UI;

namespace CasebookGame.Editor
{
    /// <summary>
    /// Casebook/Build Title Screen — creates TitleScene.unity with the noir office background.
    /// Assign your background sprite via the TitleScreenController Inspector after building.
    /// </summary>
    public static class TitleSceneBuilder
    {
        const string SCENE_PATH = "Assets/Scenes/TitleScene.unity";

        [MenuItem("Casebook/Build Title Screen")]
        public static void Build()
        {
            EnsureFolder("Assets/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic    = true;
            cam.depth           = -1;
            camGo.AddComponent<AudioListener>();

            // Canvas
            var canvasGo = new GameObject("Canvas");
            var canvas   = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight  = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // CanvasGroup for fade
            var fadeGroup = canvasGo.AddComponent<CanvasGroup>();

            // Background image (full screen)
            var bgGo  = new GameObject("Background");
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgRT  = bgGo.AddComponent<RectTransform>();
            Stretch(bgRT);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = Color.white;
            // Load the study background if already imported
            var studySprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Backgrounds/case001_study.png");
            if (studySprite) bgImg.sprite = studySprite;
            bgImg.preserveAspect = false;

            // Dark vignette overlay (semi-transparent black gradient)
            var vigGo  = new GameObject("Vignette");
            vigGo.transform.SetParent(canvasGo.transform, false);
            var vigRT  = vigGo.AddComponent<RectTransform>();
            Stretch(vigRT);
            var vigImg = vigGo.AddComponent<Image>();
            vigImg.color = new Color(0f, 0f, 0f, 0.55f);  // darken bg so text reads clearly

            // Title lines container
            var titleContainer = new GameObject("TitleContainer");
            titleContainer.transform.SetParent(canvasGo.transform, false);
            var tcRT = titleContainer.AddComponent<RectTransform>();
            tcRT.anchorMin = new Vector2(0, 0.55f);
            tcRT.anchorMax = new Vector2(1, 0.85f);
            tcRT.offsetMin = Vector2.zero; tcRT.offsetMax = Vector2.zero;

            var line1 = MakeText(titleContainer, "TitleLine1", "POCKET CASEBOOK", 72, FontStyles.Bold);
            line1.color = new Color(0.95f, 0.88f, 0.65f);   // warm parchment gold
            var l1RT = line1.gameObject.GetComponent<RectTransform>();
            l1RT.anchorMin = new Vector2(0, 0.55f); l1RT.anchorMax = new Vector2(1, 1);
            l1RT.offsetMin = new Vector2(40, 0); l1RT.offsetMax = new Vector2(-40, 0);

            var line2 = MakeText(titleContainer, "TitleLine2", "CONTRADICTION ENGINE", 36, FontStyles.Normal);
            line2.color = new Color(0.75f, 0.65f, 0.50f);   // muted gold subtitle
            line2.characterSpacing = 8f;
            var l2RT = line2.gameObject.GetComponent<RectTransform>();
            l2RT.anchorMin = new Vector2(0, 0.1f); l2RT.anchorMax = new Vector2(1, 0.52f);
            l2RT.offsetMin = new Vector2(40, 0); l2RT.offsetMax = new Vector2(-40, 0);

            // Divider line
            var divGo  = new GameObject("Divider");
            divGo.transform.SetParent(canvasGo.transform, false);
            var divRT  = divGo.AddComponent<RectTransform>();
            divRT.anchorMin = new Vector2(0.15f, 0.53f);
            divRT.anchorMax = new Vector2(0.85f, 0.535f);
            divRT.offsetMin = Vector2.zero; divRT.offsetMax = Vector2.zero;
            var divImg = divGo.AddComponent<Image>();
            divImg.color = new Color(0.95f, 0.88f, 0.65f, 0.6f);

            // Tap prompt
            var tapGo = MakeText(canvasGo, "TapPrompt", "TAP TO BEGIN", 34, FontStyles.Normal);
            tapGo.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            tapGo.characterSpacing = 6f;
            var tapRT = tapGo.gameObject.GetComponent<RectTransform>();
            tapRT.anchorMin = new Vector2(0.2f, 0.22f);
            tapRT.anchorMax = new Vector2(0.8f, 0.30f);
            tapRT.offsetMin = Vector2.zero; tapRT.offsetMax = Vector2.zero;

            // Case count badge
            var badgeGo = new GameObject("CaseBadge");
            badgeGo.transform.SetParent(canvasGo.transform, false);
            var badgeRT = badgeGo.AddComponent<RectTransform>();
            badgeRT.anchorMin = new Vector2(0.25f, 0.12f);
            badgeRT.anchorMax = new Vector2(0.75f, 0.19f);
            badgeRT.offsetMin = Vector2.zero; badgeRT.offsetMax = Vector2.zero;
            badgeGo.AddComponent<Image>().color = new Color(0.15f, 0.10f, 0.05f, 0.8f);
            var badgeTxt = MakeText(badgeGo, "BadgeText", "10 CASE FILES AVAILABLE", 26, FontStyles.Normal);
            badgeTxt.color = new Color(0.95f, 0.88f, 0.65f);
            Stretch(badgeTxt.gameObject.GetComponent<RectTransform>());

            // TitleScreenController
            var ctrl = canvasGo.AddComponent<TitleScreenController>();
            Wire(ctrl, so => {
                so.FindProperty("backgroundImage").objectReferenceValue  = bgImg;
                so.FindProperty("vignetteOverlay").objectReferenceValue  = vigImg;
                so.FindProperty("titleLine1").objectReferenceValue       = line1;
                so.FindProperty("titleLine2").objectReferenceValue       = line2;
                so.FindProperty("tapPrompt").objectReferenceValue        = tapGo;
                so.FindProperty("fadeGroup").objectReferenceValue        = fadeGroup;
            });

            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Title Screen Built",
                "TitleScene.unity saved.\n\n" +
                "Add it as Scene 0 in Build Settings (File → Build Settings → Add Open Scenes).\n" +
                "CaseScene should be Scene 1.",
                "OK");
        }

        static TextMeshProUGUI MakeText(GameObject parent, string name, string text, float size, FontStyles style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>();
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = size; tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return tmp;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        static void Wire(Object target, System.Action<SerializedObject> apply)
        {
            var so = new SerializedObject(target);
            apply(so);
            so.ApplyModifiedProperties();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            AssetDatabase.CreateFolder(string.Join("/", parts, 0, parts.Length - 1), parts[parts.Length - 1]);
        }
    }
}
#endif
