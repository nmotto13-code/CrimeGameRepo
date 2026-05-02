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
            var evidenceLookup = BuildEvidenceLookup(allCases, report.globalWarnings);
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

            DeduplicateMessages(report.globalWarnings);
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

            if (string.IsNullOrWhiteSpace(caseData.districtId))
                entry.warnings.Add("districtId is blank; bootstrapper fallback will be used until the case is resaved.");
            if (string.IsNullOrWhiteSpace(caseData.cityLocationId))
                entry.warnings.Add("cityLocationId is blank; bootstrapper fallback will be used until the case is resaved.");

            var resolvedLocations = new List<CaseLocationData>();
            int resolvedLocationCount = Mathf.Max(1, caseData.GetResolvedLocationCount());
            for (int locationIndex = 0; locationIndex < resolvedLocationCount; locationIndex++)
                resolvedLocations.Add(caseData.GetResolvedLocation(locationIndex));
            var resolvedLocationIds = new HashSet<string>(
                resolvedLocations.Where(location => location != null && !string.IsNullOrWhiteSpace(location.locationId))
                    .Select(location => location.locationId),
                StringComparer.OrdinalIgnoreCase);
            var declaredNodeIds = new HashSet<string>(
                (caseData.interrogationNodes ?? new List<InterrogationNode>())
                    .Where(node => node != null && !string.IsNullOrWhiteSpace(node.nodeId))
                    .Select(node => node.nodeId),
                StringComparer.OrdinalIgnoreCase);
            var outcomeIds = new HashSet<string>(
                (caseData.interrogationOutcomes ?? new List<InterrogationOutcomeData>())
                    .Where(outcome => outcome != null && !string.IsNullOrWhiteSpace(outcome.outcomeId))
                    .Select(outcome => outcome.outcomeId),
                StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(caseData.startingLocationId) && !resolvedLocationIds.Contains(caseData.startingLocationId))
                entry.errors.Add($"startingLocationId '{caseData.startingLocationId}' does not resolve to a case location.");

            if (caseData.visitFlowMode != CaseVisitFlowMode.LegacyFallback && resolvedLocationCount < 2)
                entry.warnings.Add("visitFlowMode is authored but the case resolves to fewer than 2 locations.");

            if (resolvedLocations.All(location => location == null || location.sceneBackground == null))
                entry.errors.Add("Background sprite is missing.");

            int hotspotCount = resolvedLocations
                .Where(location => location != null && location.hotspots != null)
                .Sum(location => location.hotspots.Count);
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

            var evidenceByRuntimeId = new Dictionary<string, EvidenceData>(StringComparer.OrdinalIgnoreCase);
            var caseEvidenceReferences = new Dictionary<string, EvidenceData>(StringComparer.OrdinalIgnoreCase);
            foreach (var evidence in caseData.evidence ?? new List<EvidenceData>())
            {
                if (evidence == null)
                {
                    entry.errors.Add("Case references a null EvidenceData asset.");
                    continue;
                }

                if (evidence.schemaVersion != CaseSchemaVersions.Current)
                    entry.warnings.Add($"Evidence {evidence.name} is on schema version {evidence.schemaVersion}.");

                string runtimeEvidenceId = GetRuntimeEvidenceId(caseData, evidence.evidenceId);
                if (string.IsNullOrWhiteSpace(runtimeEvidenceId))
                {
                    entry.errors.Add($"Evidence asset {evidence.name} is missing evidenceId.");
                }
                else if (!evidenceByRuntimeId.TryAdd(runtimeEvidenceId, evidence))
                {
                    entry.errors.Add($"Duplicate evidenceId detected in case: {runtimeEvidenceId}.");
                }

                AddCaseEvidenceReference(caseEvidenceReferences, evidence.evidenceId, evidence);
                AddCaseEvidenceReference(caseEvidenceReferences, runtimeEvidenceId, evidence);

                if (evidence.imageSprite == null)
                    entry.errors.Add($"Evidence {evidence.name} has no sprite assigned.");
                else if (evidence.usesPlaceholderSprite || LooksLikePlaceholder(evidence.imageSprite.name))
                    entry.warnings.Add($"Evidence {evidence.name} is using a placeholder sprite.");
            }

            for (int locationIndex = 0; locationIndex < resolvedLocations.Count; locationIndex++)
            {
                var location = resolvedLocations[locationIndex];
                if (location == null)
                {
                    entry.errors.Add($"Resolved case location {locationIndex} is null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(location.locationId))
                    entry.errors.Add($"Case location {locationIndex + 1} is missing locationId.");

                if (string.IsNullOrWhiteSpace(location.displayName))
                    entry.warnings.Add($"Case location {location.locationId} is missing displayName.");

                if (location.sceneBackground == null)
                    entry.errors.Add($"Case location {location.locationId} is missing a background sprite.");

                bool hasNoHotspots = location.hotspots == null || location.hotspots.Count == 0;
                bool hasIntentionalNoHotspotProgression = HasIntentionalNoHotspotProgression(location, caseData);
                if (hasNoHotspots && !hasIntentionalNoHotspotProgression)
                    entry.warnings.Add(
                        $"Case location {location.locationId} has no hotspots. It will need an explicit completion or progression trigger.");

                if (caseData.locationReadyForSolveMode == CaseSolveGateMode.RequireRequiredVisits
                    && location.isRequiredForSolve
                    && hasNoHotspots
                    && !location.autoCompleteOnEnter)
                {
                    entry.warnings.Add(
                        $"Case location {location.locationId} is required for solve but has no hotspots and does not auto-complete on entry.");
                }

                ValidateConditionReferences(
                    caseData,
                    location.unlockCondition,
                    $"Case location {location.locationId} unlockCondition",
                    entry.errors,
                    caseEvidenceReferences,
                    evidenceLookup,
                    suspectLookup,
                    resolvedLocationIds,
                    declaredNodeIds,
                    outcomeIds);

                foreach (var nextLocationId in location.nextLocationIds ?? new List<string>())
                {
                    if (!string.IsNullOrWhiteSpace(nextLocationId) && !resolvedLocationIds.Contains(nextLocationId))
                        entry.errors.Add($"Case location {location.locationId} points to unknown nextLocationId '{nextLocationId}'.");
                }

                if (!string.IsNullOrWhiteSpace(location.completionOutcomeId) && !outcomeIds.Contains(location.completionOutcomeId))
                    entry.errors.Add($"Case location {location.locationId} has unknown completionOutcomeId '{location.completionOutcomeId}'.");

                foreach (var hotspot in location.hotspots ?? new List<HotspotData>())
                {
                    if (hotspot == null)
                    {
                        entry.errors.Add($"Case location {location.locationId} contains a null hotspot entry.");
                        continue;
                    }

                    if (hotspot.normalizedPosition.x < 0f || hotspot.normalizedPosition.x > 1f ||
                        hotspot.normalizedPosition.y < 0f || hotspot.normalizedPosition.y > 1f)
                    {
                        entry.errors.Add($"Hotspot {hotspot.hotspotId} is outside normalized bounds.");
                    }

                    if (hotspot.radius < 0f)
                        entry.errors.Add($"Hotspot {hotspot.hotspotId} has a negative radius.");

                    if (string.IsNullOrWhiteSpace(hotspot.evidenceId) || !TryResolveCaseEvidence(caseData, caseEvidenceReferences, hotspot.evidenceId, out _))
                        entry.errors.Add($"Hotspot {hotspot.hotspotId} maps to unknown evidenceId '{hotspot.evidenceId}'.");
                }

                foreach (var presence in location.presentSuspects ?? new List<LocationSuspectPresenceData>())
                {
                    if (presence == null)
                    {
                        entry.errors.Add($"Case location {location.locationId} contains a null suspect presence entry.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(presence.suspectId) || !suspectLookup.ContainsKey(presence.suspectId))
                    {
                        entry.errors.Add($"Case location {location.locationId} references unknown suspectId '{presence?.suspectId}'.");
                    }
                    else if (caseData.involvedSuspects == null || !caseData.involvedSuspects.Any(suspect =>
                        suspect != null && string.Equals(suspect.suspectId, presence.suspectId, StringComparison.OrdinalIgnoreCase)))
                    {
                        entry.errors.Add($"Case location {location.locationId} suspect presence '{presence.suspectId}' is not linked in involvedSuspects.");
                    }

                    ValidateConditionReferences(
                        caseData,
                        presence.availabilityCondition,
                        $"Case location {location.locationId} suspect presence {presence.suspectId} availabilityCondition",
                        entry.errors,
                        caseEvidenceReferences,
                        evidenceLookup,
                        suspectLookup,
                        resolvedLocationIds,
                        declaredNodeIds,
                        outcomeIds);

                    if (!string.IsNullOrWhiteSpace(presence.interrogationEntryNodeId) && !declaredNodeIds.Contains(presence.interrogationEntryNodeId))
                        entry.errors.Add($"Case location {location.locationId} suspect presence {presence.suspectId} references unknown interrogationEntryNodeId '{presence.interrogationEntryNodeId}'.");

                    if (!string.IsNullOrWhiteSpace(presence.departureOutcomeId) && !outcomeIds.Contains(presence.departureOutcomeId))
                        entry.errors.Add($"Case location {location.locationId} suspect presence {presence.suspectId} references unknown departureOutcomeId '{presence.departureOutcomeId}'.");
                }
            }

            if (caseData.claims == null || caseData.claims.Count == 0)
                entry.errors.Add("Claims list is empty.");

            var seenOutcomeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var outcome in caseData.interrogationOutcomes ?? new List<InterrogationOutcomeData>())
            {
                if (outcome == null)
                {
                    entry.errors.Add("Case references a null interrogation outcome entry.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(outcome.outcomeId))
                {
                    entry.errors.Add("Interrogation outcome entry is missing outcomeId.");
                    continue;
                }

                if (!seenOutcomeIds.Add(outcome.outcomeId))
                    entry.errors.Add($"Duplicate interrogation outcomeId detected in case: {outcome.outcomeId}.");

                foreach (var locationId in outcome.unlockLocationIds ?? new List<string>())
                {
                    if (!string.IsNullOrWhiteSpace(locationId) && !resolvedLocationIds.Contains(locationId))
                        entry.errors.Add($"Interrogation outcome {outcome.outcomeId} unlocks unknown locationId '{locationId}'.");
                }

                foreach (var locationId in outcome.lockLocationIds ?? new List<string>())
                {
                    if (!string.IsNullOrWhiteSpace(locationId) && !resolvedLocationIds.Contains(locationId))
                        entry.errors.Add($"Interrogation outcome {outcome.outcomeId} locks unknown locationId '{locationId}'.");
                }

                foreach (var suspectId in outcome.revealSuspectIds ?? new List<string>())
                {
                    if (!string.IsNullOrWhiteSpace(suspectId) && !suspectLookup.ContainsKey(suspectId))
                        entry.errors.Add($"Interrogation outcome {outcome.outcomeId} reveals unknown suspectId '{suspectId}'.");
                }

                foreach (var suspectId in outcome.hideSuspectIds ?? new List<string>())
                {
                    if (!string.IsNullOrWhiteSpace(suspectId) && !suspectLookup.ContainsKey(suspectId))
                        entry.errors.Add($"Interrogation outcome {outcome.outcomeId} hides unknown suspectId '{suspectId}'.");
                }

                foreach (var evidenceId in outcome.grantEvidenceIds ?? new List<string>())
                {
                    if (!string.IsNullOrWhiteSpace(evidenceId)
                        && !TryResolveCaseEvidence(caseData, caseEvidenceReferences, evidenceId, out _)
                        && !evidenceLookup.ContainsKey(evidenceId))
                        entry.errors.Add($"Interrogation outcome {outcome.outcomeId} grants unknown evidenceId '{evidenceId}'.");
                }

                if (!string.IsNullOrWhiteSpace(outcome.redirectToLocationId) && !resolvedLocationIds.Contains(outcome.redirectToLocationId))
                    entry.errors.Add($"Interrogation outcome {outcome.outcomeId} redirects to unknown locationId '{outcome.redirectToLocationId}'.");
            }

            if (caseData.locationReadyForSolveMode == CaseSolveGateMode.RequireInterrogationOutcome && outcomeIds.Count == 0)
                entry.errors.Add("locationReadyForSolveMode requires interrogation outcomes, but none are authored.");
            if (caseData.locationReadyForSolveMode == CaseSolveGateMode.RequireRequiredVisits
                && !resolvedLocations.Any(location => location != null && location.isRequiredForSolve))
                entry.errors.Add("locationReadyForSolveMode requires at least one location marked isRequiredForSolve.");

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

            bool hasPrimaryA = !string.IsNullOrWhiteSpace(caseData.primaryEvidenceIdA)
                && TryResolveCaseEvidence(caseData, caseEvidenceReferences, caseData.primaryEvidenceIdA, out _);
            bool hasPrimaryB = !string.IsNullOrWhiteSpace(caseData.primaryEvidenceIdB)
                && TryResolveCaseEvidence(caseData, caseEvidenceReferences, caseData.primaryEvidenceIdB, out _);
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

                if (!string.IsNullOrWhiteSpace(node.suspectId))
                {
                    bool suspectExists = caseData.involvedSuspects != null && caseData.involvedSuspects.Any(suspect =>
                        suspect != null && string.Equals(suspect.suspectId, node.suspectId, StringComparison.OrdinalIgnoreCase));
                    if (!suspectExists)
                        entry.errors.Add($"Interrogation node {node.name} references suspectId '{node.suspectId}' that is not linked on the case.");
                }

                if (node.responses == null || node.responses.Count != 3)
                    entry.errors.Add($"Interrogation node {node.name} must define exactly 3 responses.");

                if (node.correctResponseIndex < 0 || node.correctResponseIndex > 2)
                    entry.errors.Add($"Interrogation node {node.name} has an invalid correctResponseIndex.");

                foreach (var evidenceId in node.evidenceRequiredIds ?? new List<string>())
                {
                    if (!string.IsNullOrWhiteSpace(evidenceId)
                        && !TryResolveCaseEvidence(caseData, caseEvidenceReferences, evidenceId, out _)
                        && !evidenceLookup.ContainsKey(evidenceId))
                        entry.errors.Add($"Interrogation node {node.name} references unknown evidenceId '{evidenceId}'.");
                }

                if (!string.IsNullOrWhiteSpace(node.nextNodeIdOnCorrect) && !interrogationLookup.ContainsKey(node.nextNodeIdOnCorrect))
                    entry.errors.Add($"Interrogation node {node.name} has unknown nextNodeIdOnCorrect '{node.nextNodeIdOnCorrect}'.");
                if (!string.IsNullOrWhiteSpace(node.nextNodeIdOnWrong) && !interrogationLookup.ContainsKey(node.nextNodeIdOnWrong))
                    entry.errors.Add($"Interrogation node {node.name} has unknown nextNodeIdOnWrong '{node.nextNodeIdOnWrong}'.");
                if (!string.IsNullOrWhiteSpace(node.outcomeIdOnCorrect) && !outcomeIds.Contains(node.outcomeIdOnCorrect))
                    entry.errors.Add($"Interrogation node {node.name} has unknown outcomeIdOnCorrect '{node.outcomeIdOnCorrect}'.");
                if (!string.IsNullOrWhiteSpace(node.outcomeIdOnWrong) && !outcomeIds.Contains(node.outcomeIdOnWrong))
                    entry.errors.Add($"Interrogation node {node.name} has unknown outcomeIdOnWrong '{node.outcomeIdOnWrong}'.");
                if (!string.IsNullOrWhiteSpace(node.locationContextId) && !resolvedLocationIds.Contains(node.locationContextId))
                    entry.errors.Add($"Interrogation node {node.name} has unknown locationContextId '{node.locationContextId}'.");

                foreach (var grantedEvidenceId in node.grantedEvidenceIds ?? new List<string>())
                {
                    if (!string.IsNullOrWhiteSpace(grantedEvidenceId)
                        && !TryResolveCaseEvidence(caseData, caseEvidenceReferences, grantedEvidenceId, out _)
                        && !evidenceLookup.ContainsKey(grantedEvidenceId))
                        entry.errors.Add($"Interrogation node {node.name} grants unknown evidenceId '{grantedEvidenceId}'.");
                }
            }

            DeduplicateMessages(entry.errors);
            DeduplicateMessages(entry.warnings);
            return entry;
        }

        static void ValidateConditionReferences(
            CaseData caseData,
            CaseProgressConditionData condition,
            string ownerLabel,
            List<string> errors,
            Dictionary<string, EvidenceData> caseEvidenceReferences,
            Dictionary<string, EvidenceData> evidenceLookup,
            Dictionary<string, SuspectData> suspectLookup,
            HashSet<string> resolvedLocationIds,
            HashSet<string> declaredNodeIds,
            HashSet<string> outcomeIds)
        {
            if (condition == null || condition.IsEmpty)
                return;

            foreach (var evidenceId in condition.requiredEvidenceIds ?? new List<string>())
            {
                if (!string.IsNullOrWhiteSpace(evidenceId)
                    && !TryResolveCaseEvidence(caseData, caseEvidenceReferences, evidenceId, out _)
                    && !evidenceLookup.ContainsKey(evidenceId))
                    errors.Add($"{ownerLabel} references unknown requiredEvidenceId '{evidenceId}'.");
            }

            foreach (var locationId in condition.requiredVisitedLocationIds ?? new List<string>())
            {
                if (!string.IsNullOrWhiteSpace(locationId) && !resolvedLocationIds.Contains(locationId))
                    errors.Add($"{ownerLabel} references unknown requiredVisitedLocationId '{locationId}'.");
            }

            foreach (var locationId in condition.requiredCompletedLocationIds ?? new List<string>())
            {
                if (!string.IsNullOrWhiteSpace(locationId) && !resolvedLocationIds.Contains(locationId))
                    errors.Add($"{ownerLabel} references unknown requiredCompletedLocationId '{locationId}'.");
            }

            foreach (var nodeId in condition.requiredCompletedInterrogationNodeIds ?? new List<string>())
            {
                if (!string.IsNullOrWhiteSpace(nodeId) && !declaredNodeIds.Contains(nodeId))
                    errors.Add($"{ownerLabel} references unknown requiredCompletedInterrogationNodeId '{nodeId}'.");
            }

            foreach (var outcomeId in condition.requiredInterrogationOutcomeIds ?? new List<string>())
            {
                if (!string.IsNullOrWhiteSpace(outcomeId) && !outcomeIds.Contains(outcomeId))
                    errors.Add($"{ownerLabel} references unknown requiredInterrogationOutcomeId '{outcomeId}'.");
            }

            foreach (var suspectId in condition.requiredSuspectIds ?? new List<string>())
            {
                if (!string.IsNullOrWhiteSpace(suspectId) && !suspectLookup.ContainsKey(suspectId))
                    errors.Add($"{ownerLabel} references unknown requiredSuspectId '{suspectId}'.");
            }
        }

        static Dictionary<string, T> BuildUniqueLookup<T>(
            IEnumerable<T> assets,
            Func<T, string> getId,
            List<string> globalWarnings,
            string label) where T : UnityEngine.Object
        {
            var lookup = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
            var duplicateIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
                    duplicateIds.Add(id);
            }

            foreach (var duplicateId in duplicateIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase))
                globalWarnings.Add($"{label} ID '{duplicateId}' is duplicated across assets.");

            return lookup;
        }

        static Dictionary<string, EvidenceData> BuildEvidenceLookup(
            IEnumerable<CaseData> cases,
            List<string> globalWarnings)
        {
            var lookup = new Dictionary<string, EvidenceData>(StringComparer.OrdinalIgnoreCase);
            var owningCaseIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var duplicateIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var caseData in cases ?? Enumerable.Empty<CaseData>())
            {
                if (caseData?.evidence == null)
                    continue;

                foreach (var evidence in caseData.evidence)
                {
                    if (evidence == null)
                        continue;

                    string runtimeEvidenceId = GetRuntimeEvidenceId(caseData, evidence.evidenceId);
                    if (string.IsNullOrWhiteSpace(runtimeEvidenceId))
                        continue;

                    if (lookup.TryAdd(runtimeEvidenceId, evidence))
                    {
                        owningCaseIds[runtimeEvidenceId] = caseData.caseId ?? string.Empty;
                        continue;
                    }

                    if (!string.Equals(owningCaseIds[runtimeEvidenceId], caseData.caseId, StringComparison.OrdinalIgnoreCase))
                        duplicateIds.Add(runtimeEvidenceId);
                }
            }

            foreach (var duplicateId in duplicateIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase))
                globalWarnings.Add($"Evidence runtime ID '{duplicateId}' is duplicated across assets.");

            return lookup;
        }

        static void AddCaseEvidenceReference(
            Dictionary<string, EvidenceData> lookup,
            string evidenceId,
            EvidenceData evidence)
        {
            if (lookup == null || evidence == null || string.IsNullOrWhiteSpace(evidenceId))
                return;

            lookup.TryAdd(evidenceId, evidence);
        }

        static bool TryResolveCaseEvidence(
            CaseData caseData,
            Dictionary<string, EvidenceData> caseEvidenceReferences,
            string evidenceId,
            out EvidenceData evidence)
        {
            evidence = null;
            if (caseEvidenceReferences == null || string.IsNullOrWhiteSpace(evidenceId))
                return false;

            if (caseEvidenceReferences.TryGetValue(evidenceId, out evidence))
                return true;

            string runtimeEvidenceId = GetRuntimeEvidenceId(caseData, evidenceId);
            return !string.IsNullOrWhiteSpace(runtimeEvidenceId)
                && caseEvidenceReferences.TryGetValue(runtimeEvidenceId, out evidence);
        }

        static string GetRuntimeEvidenceId(CaseData caseData, string evidenceId)
        {
            if (string.IsNullOrWhiteSpace(evidenceId))
                return string.Empty;

            if (evidenceId.IndexOf("_E", StringComparison.OrdinalIgnoreCase) > 0)
                return evidenceId;

            string caseCode = NormalizeCaseCode(caseData?.caseId);
            return string.IsNullOrWhiteSpace(caseCode)
                ? evidenceId
                : $"{caseCode}_{evidenceId}";
        }

        static string NormalizeCaseCode(string caseId)
        {
            if (string.IsNullOrWhiteSpace(caseId))
                return string.Empty;

            if (caseId.StartsWith("Case_", StringComparison.OrdinalIgnoreCase))
                return $"C{caseId[5..]}";

            if (caseId.Length == 4 && (caseId[0] == 'C' || caseId[0] == 'c'))
                return caseId.ToUpperInvariant();

            return caseId;
        }

        static bool HasIntentionalNoHotspotProgression(CaseLocationData location, CaseData caseData)
        {
            if (location == null)
                return false;

            if (location.autoCompleteOnEnter || !string.IsNullOrWhiteSpace(location.completionOutcomeId) || location.autoUnlocksSolve)
                return true;

            foreach (var presence in location.presentSuspects ?? new List<LocationSuspectPresenceData>())
            {
                if (presence != null
                    && (!string.IsNullOrWhiteSpace(presence.interrogationEntryNodeId)
                        || !string.IsNullOrWhiteSpace(presence.departureOutcomeId)))
                {
                    return true;
                }
            }

            foreach (var node in caseData?.interrogationNodes ?? new List<InterrogationNode>())
            {
                if (node != null && string.Equals(node.locationContextId, location.locationId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        static void DeduplicateMessages(List<string> messages)
        {
            if (messages == null || messages.Count < 2)
                return;

            var orderedDistinct = messages.Distinct(StringComparer.Ordinal).ToList();
            messages.Clear();
            messages.AddRange(orderedDistinct);
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
