#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using CasebookGame.Data;

namespace CasebookGame.Editor
{
    public static class BackgroundWirer
    {
        const string BG_PATH = "Assets/Sprites/Backgrounds";

        // Maps Case asset name → background filename (no extension — Unity finds it regardless of jpg/png)
        static readonly Dictionary<string, string> Map = new()
        {
            { "Case_001", "case001_study.png" },   // has double extension — try both
            { "Case_002", "case002_bank" },
            { "Case_003", "case003_cafe" },
            { "Case_004", "case004_garage" },
            { "Case_005", "case005_stairwell" },
            { "Case_006", "case006_doorstep" },
            { "Case_007", "case007_apartment" },
            { "Case_008", "case008_platform" },
            { "Case_009", "case009_gym" },
            { "Case_010", "case010_gallery" },
        };

        [MenuItem("Casebook/Wire Case Backgrounds")]
        public static void WireAll()
        {
            int wired = 0, skipped = 0;

            var caseGuids = AssetDatabase.FindAssets("t:CaseData", new[] { "Assets/Resources/Cases" });

            foreach (var guid in caseGuids)
            {
                var casePath = AssetDatabase.GUIDToAssetPath(guid);
                var caseData = AssetDatabase.LoadAssetAtPath<CaseData>(casePath);
                if (caseData == null) continue;

                var caseName = System.IO.Path.GetFileNameWithoutExtension(casePath);
                if (!Map.TryGetValue(caseName, out var bgFile)) { skipped++; continue; }

                // Try to find the sprite — check jpg then png then the exact name given
                Sprite sprite = TryLoad(bgFile)
                             ?? TryLoad(bgFile + ".jpg")
                             ?? TryLoad(bgFile + ".png")
                             ?? TryLoad(bgFile + ".png.jpg"); // case001 double-extension

                if (sprite == null)
                {
                    Debug.LogWarning($"[BackgroundWirer] Could not find background for {caseName}: tried '{bgFile}' (jpg/png)");
                    skipped++;
                    continue;
                }

                var so   = new SerializedObject(caseData);
                var prop = so.FindProperty("sceneBackground");

                if (prop.objectReferenceValue != null)
                {
                    Debug.Log($"[BackgroundWirer] {caseName} already has a background — skipping");
                    skipped++;
                    continue;
                }

                prop.objectReferenceValue = sprite;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(caseData);
                Debug.Log($"[BackgroundWirer] {caseName} ← {sprite.name}");
                wired++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Background Wirer",
                $"Done.\n\nWired:   {wired}\nSkipped: {skipped}", "OK");
        }

        static Sprite TryLoad(string filename)
        {
            // Search for the file anywhere under BG_PATH
            var guids = AssetDatabase.FindAssets(
                System.IO.Path.GetFileNameWithoutExtension(filename),
                new[] { BG_PATH });

            foreach (var g in guids)
            {
                var path   = AssetDatabase.GUIDToAssetPath(g);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) return sprite;
            }
            return null;
        }
    }
}
#endif
