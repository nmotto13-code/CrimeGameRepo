#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using CasebookGame.Data;

namespace CasebookGame.Editor
{
    public static class EvidenceSpriteRepairTool
    {
        const string SpriteFolder = "Assets/Sprites/Evidence";

        [MenuItem("Casebook/Assets/Repair Evidence Sprites")]
        public static void RepairEvidenceSprites()
        {
            int repairedCount = 0;
            var missingEntries = new List<string>();

            foreach (var evidence in LoadEvidenceAssets())
            {
                if (evidence == null)
                    continue;

                bool needsRepair = evidence.imageSprite == null ||
                                   evidence.usesPlaceholderSprite ||
                                   LooksLikePlaceholder(evidence.imageSprite != null ? evidence.imageSprite.name : string.Empty);
                if (!needsRepair)
                    continue;

                string assetPath = AssetDatabase.GetAssetPath(evidence);
                string assetStem = Path.GetFileNameWithoutExtension(assetPath);
                string spritePath = Path.Combine(SpriteFolder, $"{assetStem}.png").Replace("\\", "/");
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);

                if (sprite != null)
                {
                    evidence.imageSprite = sprite;
                    evidence.usesPlaceholderSprite = false;
                    EditorUtility.SetDirty(evidence);
                    repairedCount++;
                    continue;
                }

                string caseCode = assetStem.Contains('_') ? assetStem.Split('_')[0] : "C0XX";
                string displayName = string.IsNullOrWhiteSpace(evidence.displayName) ? assetStem : evidence.displayName;
                missingEntries.Add(
                    $"{{ \"id\": \"{assetStem}\", \"name\": \"{displayName}\", \"case\": \"{caseCode}\", \"prompt\": \"TODO describe {displayName.ToLowerInvariant()}\" }}");
            }

            if (repairedCount > 0)
                AssetDatabase.SaveAssets();

            Debug.Log($"[Casebook] Evidence sprite repair complete. {repairedCount} asset(s) auto-assigned.");

            if (missingEntries.Count > 0)
            {
                Debug.LogWarning(
                    "[Casebook] Missing evidence sprites remain. Add prompt entries like:\n" +
                    string.Join("\n", missingEntries.OrderBy(entry => entry)));
            }
        }

        static List<EvidenceData> LoadEvidenceAssets()
        {
            return AssetDatabase.FindAssets($"t:{typeof(EvidenceData).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<EvidenceData>)
                .Where(asset => asset != null)
                .ToList();
        }

        static bool LooksLikePlaceholder(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            string lowered = name.ToLowerInvariant();
            return lowered.Contains("placeholder") || lowered.Contains("temp") || lowered.Contains("missing");
        }
    }
}
#endif
