#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using CasebookGame.Data;

namespace CasebookGame.Editor
{
    /// <summary>
    /// Casebook/Import Assets — scans Assets/Sprites for files named by convention
    /// and auto-assigns sprites to the matching CaseData / EvidenceData assets.
    ///
    /// Naming convention:
    ///   Background : case001_bg.png          → CaseData.sceneBackground
    ///   Evidence   : case001_e001_watch.png  → EvidenceData (caseId=Case_001, evidenceId=E001)
    /// </summary>
    public static class AssetImportHelper
    {
        static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".tga", ".psd", ".tif", ".tiff" };

        [MenuItem("Casebook/Import Assets (Auto-Assign Sprites)")]
        public static void ImportAssets()
        {
            int assigned = 0;

            // ── Backgrounds ────────────────────────────────────────────
            string bgFolder = "Assets/Sprites/Backgrounds";
            if (AssetDatabase.IsValidFolder(bgFolder))
            {
                // Search all textures — works for png, jpg, jpeg, etc.
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { bgFolder }))
                {
                    string path  = AssetDatabase.GUIDToAssetPath(guid);
                    string ext   = Path.GetExtension(path).ToLower();
                    if (System.Array.IndexOf(ImageExtensions, ext) < 0) continue;

                    string fname = Path.GetFileNameWithoutExtension(path).ToLower();

                    // Matches: case001_bg, case001_study, case002_bank, etc.
                    if (!fname.StartsWith("case") || fname.Length < 7) continue;
                    string caseNum = fname.Substring(4, 3);   // "001"
                    string caseId  = $"Case_{caseNum}";

                    var caseData = LoadCase(caseId);
                    if (caseData == null) continue;

                    var sprite = EnsureSprite(path);
                    if (sprite == null) continue;

                    var so = new SerializedObject(caseData);
                    so.FindProperty("sceneBackground").objectReferenceValue = sprite;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(caseData);
                    assigned++;
                    Debug.Log($"[AssetImport] {caseId} background ← {fname}{ext}");
                }
            }

            // ── Evidence sprites ───────────────────────────────────────
            string evFolder = "Assets/Sprites/Evidence";
            if (AssetDatabase.IsValidFolder(evFolder))
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { evFolder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string ext  = Path.GetExtension(path).ToLower();
                    if (System.Array.IndexOf(ImageExtensions, ext) < 0) continue;

                    string fname = Path.GetFileNameWithoutExtension(path).ToLower();

                    // Matches: case001_e001_watch, case001_e002_clock, etc.
                    var parts = fname.Split('_');
                    if (parts.Length < 2 || !parts[0].StartsWith("case") || parts[0].Length < 7) continue;

                    string caseNum = parts[0].Substring(4, 3);
                    string caseId  = $"Case_{caseNum}";
                    string evId    = parts[1].ToUpper();   // "E001"

                    string evAssetPath = $"Assets/ScriptableObjects/Cases/Evidence/{caseId}_{evId}.asset";
                    var evData = AssetDatabase.LoadAssetAtPath<EvidenceData>(evAssetPath);
                    if (evData == null)
                    {
                        Debug.LogWarning($"[AssetImport] No EvidenceData at {evAssetPath}");
                        continue;
                    }

                    var sprite = EnsureSprite(path);
                    if (sprite == null) continue;

                    var so = new SerializedObject(evData);
                    so.FindProperty("imageSprite").objectReferenceValue = sprite;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(evData);
                    assigned++;
                    Debug.Log($"[AssetImport] {caseId}/{evId} evidence sprite ← {fname}{ext}");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Asset Import Complete",
                $"{assigned} sprite(s) auto-assigned.\n\n" +
                "Check Console for details. Unmatched files are logged as warnings.",
                "OK");
        }

        // Returns the Sprite at path, auto-reimporting as Sprite if the texture was imported as Default type.
        static Sprite EnsureSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;

            importer.textureType    = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static CaseData LoadCase(string caseId)
            => AssetDatabase.LoadAssetAtPath<CaseData>($"Assets/Resources/Cases/{caseId}.asset");
    }
}
#endif
