#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using CasebookGame.Data;

namespace CasebookGame.Editor
{
    public static class CaseContentValidator
    {
        const string ReportRelativePath = "Docs/validation-report.json";
        const int RequiredMinHotspotsPerCase = 1;
        const int RecommendedMinHotspotsPerCase = 3;
        const int RecommendedMaxHotspotsPerCase = 5;

        [MenuItem("Casebook/Validate All Cases")]
        public static void ValidateAllCases()
        {
            var report = BuildValidationReport();
            WriteReport(report);
            LogReport(report);
        }

        [MenuItem("Casebook/Upgrade Case Schema")]
        public static void UpgradeCaseSchema()
        {
            int updatedAssets = 0;
            updatedAssets += UpgradeAssets<CaseData>(asset => asset.schemaVersion = CaseSchemaVersions.Current);
            updatedAssets += UpgradeAssets<EvidenceData>(asset => asset.schemaVersion = CaseSchemaVersions.Current);
            updatedAssets += UpgradeAssets<ClaimData>(asset => asset.schemaVersion = CaseSchemaVersions.Current);
            updatedAssets += UpgradeAssets<SuspectData>(asset => asset.schemaVersion = CaseSchemaVersions.Current);
            updatedAssets += UpgradeAssets<InterrogationNode>(asset => asset.schemaVersion = CaseSchemaVersions.Current);

            if (updatedAssets > 0)
                AssetDatabase.SaveAssets();

            Debug.Log($"[Casebook] Schema upgrade complete. {updatedAssets} asset(s) updated to version {CaseSchemaVersions.Current}.");
        }

        static int UpgradeAssets<T>(Action<T> upgradeAction) where T : ScriptableObject
        {
            int updated = 0;
            foreach (var asset in LoadAssets<T>())
            {
                int version = GetSchemaVersion(asset);
                if (version == CaseSchemaVersions.Current)
                    continue;

                upgradeAction(asset);
                EditorUtility.SetDirty(asset);
                updated++;
            }

            return updated;
        }

        static int GetSchemaVersion(ScriptableObject asset) => asset switch
        {
            CaseData caseData => caseData.schemaVersion,
            EvidenceData evidenceData => evidenceData.schemaVersion,
            ClaimData claimData => claimData.schemaVersion,
            SuspectData suspectData => suspectData.schemaVersion,
            InterrogationNode interrogationNode => interrogationNode.schemaVersion,
            _ => 0
        };

        static ValidationReport BuildValidationReport()
        {
            var report = new ValidationReport
            {
                generatedAtUtc = DateTime.UtcNow.ToString("O"),
                validatorVersion = CaseSchemaVersions.Current
            };

            var allCases = LoadAssets<CaseData>().OrderBy(caseData => caseData.caseId).ToList();
            var suspectLookup = BuildUniqueLookup(
                LoadAssets<SuspectData>(),
                suspect => suspect.suspectId,
                report.globalWarnings,
                "Suspect");
            var evidenceLookup = BuildUniqueLookup(
                LoadAssets<EvidenceData>(),
                evidence => evidence.evidenceId,
                report.globalWarnings,
                "Evidence");
            var interrogationLookup = BuildUniqueLookup(
                LoadAssets<InterrogationNode>(),
                node => node.nodeId,
                report.globalWarnings,
                "Interrogation node");

            foreach (var caseData in allCases)
            {
                var entry = ValidateCase(caseData, suspectLookup, evidenceLookup, interrogationLookup);
                report.cases.Add(entry);
                report.summary.totalCases++;
                report.summary.errorCount += entry.errors.Count;
                report.summary.warningCount += entry.warnings.Count;
            }

            return report;
        }

        static CaseValidationEntry ValidateCase(
            CaseData caseData,
            Dictionary<string, SuspectData> suspectLookup,
            Dictionary<string, EvidenceData> evidenceLookup,
            Dictionary<string, InterrogationNode> interrogationLookup)
        {
            var entry = new CaseValidationEntry
            {
                caseId = caseData != null ? caseData.caseId : "<null>",
                title = caseData != null ? caseData.title : "<null>"
            };

            if (caseData == null)
            {
                entry.errors.Add("Case asset is null.");
                return entry;
            }

            if (caseData.schemaVersion != CaseSchemaVersions.Current)
                entry.warnings.Add($"Schema version is {caseData.schemaVersion}; expected {CaseSchemaVersions.Current}. Run Casebook/Upgrade Case Schema.");

            if (caseData.sceneBackground == null)
                entry.errors.Add("Background sprite is missing.");

            int hotspotCount = caseData.hotspots?.Count ?? 0;
            if (hotspotCount < RequiredMinHotspotsPerCase)
            {
                entry.errors.Add($"Hotspot count must be at least {RequiredMinHotspotsPerCase}.");
            }
            else if (hotspotCount < RecommendedMinHotspotsPerCase || hotspotCount > RecommendedMaxHotspotsPerCase)
            {
                entry.warnings.Add(
                    $"Hotspot count is {hotspotCount}. Recommended range for newly-authored content is " +
                    $"{RecommendedMinHotspotsPerCase} to {RecommendedMaxHotspotsPerCase}.");
            }

            var evidenceById = new Dictionary<string, EvidenceData>(StringComparer.OrdinalIgnoreCase);
            foreach (var evidence in caseData.evidence ?? new List<EvidenceData>())
            {
                if (evidence == null)
                {
                    entry.errors.Add("Case references a null EvidenceData asset.");
                    continue;
                }

                if (evidence.schemaVersion != CaseSchemaVersions.Current)
                    entry.warnings.Add($"Evidence {evidence.name} is on schema version {evidence.schemaVersion}.");

                if (string.IsNullOrWhiteSpace(evidence.evidenceId))
                {
                    entry.errors.Add($"Evidence asset {evidence.name} is missing evidenceId.");
                }
                else if (!evidenceById.TryAdd(evidence.evidenceId, evidence))
                {
                    entry.errors.Add($"Duplicate evidenceId detected in case: {evidence.evidenceId}.");
                }

                if (evidence.imageSprite == null)
                    entry.errors.Add($"Evidence {evidence.name} has no sprite assigned.");
                else if (evidence.usesPlaceholderSprite || LooksLikePlaceholder(evidence.imageSprite.name))
                    entry.warnings.Add($"Evidence {evidence.name} is using a placeholder sprite.");
            }

            foreach (var hotspot in caseData.hotspots ?? new List<HotspotData>())
            {
                if (hotspot == null)
                {
                    entry.errors.Add("Case contains a null hotspot entry.");
                    continue;
                }

                if (hotspot.normalizedPosition.x < 0f || hotspot.normalizedPosition.x > 1f ||
                    hotspot.normalizedPosition.y < 0f || hotspot.normalizedPosition.y > 1f)
                {
                    entry.errors.Add($"Hotspot {hotspot.hotspotId} is outside normalized bounds.");
                }

                if (hotspot.radius < 0f)
                    entry.errors.Add($"Hotspot {hotspot.hotspotId} has a negative radius.");

                if (string.IsNullOrWhiteSpace(hotspot.evidenceId) || !evidenceById.ContainsKey(hotspot.evidenceId))
                    entry.errors.Add($"Hotspot {hotspot.hotspotId} maps to unknown evidenceId '{hotspot.evidenceId}'.");
            }

            if (caseData.claims == null || caseData.claims.Count == 0)
                entry.errors.Add("Claims list is empty.");

            var claimIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var claim in caseData.claims ?? new List<ClaimData>())
            {
                if (claim == null)
                {
                    entry.errors.Add("Case references a null ClaimData asset.");
                    continue;
                }

                if (claim.schemaVersion != CaseSchemaVersions.Current)
                    entry.warnings.Add($"Claim {claim.name} is on schema version {claim.schemaVersion}.");

                if (string.IsNullOrWhiteSpace(claim.claimId))
                {
                    entry.errors.Add($"Claim asset {claim.name} is missing claimId.");
                    continue;
                }

                if (!claimIds.Add(claim.claimId))
                    entry.errors.Add($"Duplicate claimId detected in case: {claim.claimId}.");
            }

            if (string.IsNullOrWhiteSpace(caseData.contradictoryClaimId) || !claimIds.Contains(caseData.contradictoryClaimId))
                entry.errors.Add("Correct contradiction ID does not resolve to a claim in this case.");

            bool hasPrimaryA = !string.IsNullOrWhiteSpace(caseData.primaryEvidenceIdA) && evidenceById.ContainsKey(caseData.primaryEvidenceIdA);
            bool hasPrimaryB = !string.IsNullOrWhiteSpace(caseData.primaryEvidenceIdB) && evidenceById.ContainsKey(caseData.primaryEvidenceIdB);
            if (!hasPrimaryA || !hasPrimaryB || string.Equals(caseData.primaryEvidenceIdA, caseData.primaryEvidenceIdB, StringComparison.OrdinalIgnoreCase))
                entry.errors.Add("Explanation must reference two distinct evidence IDs via primaryEvidenceIdA/B.");

            if (caseData.toolConfig == null)
            {
                entry.errors.Add("Tool config is missing.");
            }
            else
            {
                if (caseData.toolConfig.crossCheckCharges < 0)
                    entry.errors.Add("Tool config crossCheckCharges must be >= 0.");
                if (caseData.toolConfig.timelineSnapCharges < 0)
                    entry.errors.Add("Tool config timelineSnapCharges must be >= 0.");
                if (caseData.toolConfig.enhanceCooldownSeconds < 0f)
                    entry.errors.Add("Tool config enhanceCooldownSeconds must be >= 0.");
            }

            foreach (var suspect in caseData.involvedSuspects ?? new List<SuspectData>())
            {
                if (suspect == null)
                {
                    entry.errors.Add("Case references a null SuspectData asset.");
                    continue;
                }

                if (suspect.schemaVersion != CaseSchemaVersions.Current)
                    entry.warnings.Add($"Suspect {suspect.name} is on schema version {suspect.schemaVersion}.");

                if (string.IsNullOrWhiteSpace(suspect.suspectId) || !suspectLookup.TryGetValue(suspect.suspectId, out var resolvedSuspect) || resolvedSuspect != suspect)
                    entry.errors.Add($"Suspect reference {suspect.name} does not resolve cleanly by suspectId.");
            }

            foreach (var node in caseData.interrogationNodes ?? new List<InterrogationNode>())
            {
                if (node == null)
                {
                    entry.errors.Add("Case references a null InterrogationNode asset.");
                    continue;
                }

                if (node.schemaVersion != CaseSchemaVersions.Current)
                    entry.warnings.Add($"Interrogation node {node.name} is on schema version {node.schemaVersion}.");

                if (string.IsNullOrWhiteSpace(node.nodeId) || !interrogationLookup.TryGetValue(node.nodeId, out var resolvedNode) || resolvedNode != node)
                    entry.errors.Add($"Interrogation node reference {node.name} does not resolve cleanly by nodeId.");

                if (node.responses == null || node.responses.Count != 3)
                    entry.errors.Add($"Interrogation node {node.name} must define exactly 3 responses.");

                if (node.correctResponseIndex < 0 || node.correctResponseIndex > 2)
                    entry.errors.Add($"Interrogation node {node.name} has an invalid correctResponseIndex.");

                foreach (var evidenceId in node.evidenceRequiredIds ?? new List<string>())
                {
                    if (!string.IsNullOrWhiteSpace(evidenceId)
                        && !evidenceById.ContainsKey(evidenceId)
                        && !evidenceLookup.ContainsKey(evidenceId))
                        entry.errors.Add($"Interrogation node {node.name} references unknown evidenceId '{evidenceId}'.");
                }
            }

            return entry;
        }

        static Dictionary<string, T> BuildUniqueLookup<T>(
            IEnumerable<T> assets,
            Func<T, string> getId,
            List<string> globalWarnings,
            string label) where T : UnityEngine.Object
        {
            var lookup = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);

            foreach (var asset in assets)
            {
                if (asset == null)
                    continue;

                var id = getId(asset);
                if (string.IsNullOrWhiteSpace(id))
                {
                    globalWarnings.Add($"{label} asset {asset.name} is missing its ID.");
                    continue;
                }

                if (!lookup.TryAdd(id, asset))
                    globalWarnings.Add($"{label} ID '{id}' is duplicated across assets.");
            }

            return lookup;
        }

        static void WriteReport(ValidationReport report)
        {
            var projectRootDir = Directory.GetParent(Application.dataPath);
            if (projectRootDir == null)
            {
                Debug.LogError("[Casebook] Unable to resolve project root for validation report export.");
                return;
            }

            string projectRoot = projectRootDir.FullName;
            string absolutePath = Path.Combine(projectRoot, ReportRelativePath);
            string reportDirectory = Path.GetDirectoryName(absolutePath);
            if (string.IsNullOrWhiteSpace(reportDirectory))
            {
                Debug.LogError("[Casebook] Unable to resolve validation report directory.");
                return;
            }

            Directory.CreateDirectory(reportDirectory);
            File.WriteAllText(absolutePath, JsonUtility.ToJson(report, true));
            Debug.Log($"[Casebook] Validation report exported to {ReportRelativePath}");
        }

        static void LogReport(ValidationReport report)
        {
            string summary = $"[Casebook] Validated {report.summary.totalCases} case(s): {report.summary.errorCount} error(s), {report.summary.warningCount} warning(s).";
            if (report.summary.errorCount > 0)
                Debug.LogError(summary);
            else if (report.summary.warningCount > 0)
                Debug.LogWarning(summary);
            else
                Debug.Log(summary);

            foreach (var warning in report.globalWarnings)
                Debug.LogWarning($"[Casebook][Global] {warning}");

            foreach (var entry in report.cases)
            {
                if (entry.errors.Count == 0 && entry.warnings.Count == 0)
                {
                    Debug.Log($"[Casebook][{entry.caseId}] OK");
                    continue;
                }

                if (entry.errors.Count > 0)
                    Debug.LogError($"[Casebook][{entry.caseId}] Errors:\n- {string.Join("\n- ", entry.errors)}");

                if (entry.warnings.Count > 0)
                    Debug.LogWarning($"[Casebook][{entry.caseId}] Warnings:\n- {string.Join("\n- ", entry.warnings)}");
            }
        }

        static bool LooksLikePlaceholder(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            string lowered = name.ToLowerInvariant();
            return lowered.Contains("placeholder") || lowered.Contains("temp") || lowered.Contains("missing");
        }

        static List<T> LoadAssets<T>() where T : ScriptableObject
        {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null)
                .ToList();
        }

        [Serializable]
        class ValidationReport
        {
            public string generatedAtUtc;
            public int validatorVersion;
            public ValidationSummary summary = new ValidationSummary();
            public List<string> globalWarnings = new List<string>();
            public List<CaseValidationEntry> cases = new List<CaseValidationEntry>();
        }

        [Serializable]
        class ValidationSummary
        {
            public int totalCases;
            public int errorCount;
            public int warningCount;
        }

        [Serializable]
        class CaseValidationEntry
        {
            public string caseId;
            public string title;
            public List<string> errors = new List<string>();
            public List<string> warnings = new List<string>();
        }
    }
}
#endif
