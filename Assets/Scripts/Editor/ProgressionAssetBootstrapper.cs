#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using CasebookGame.Data;
using UnityEditor;
using UnityEngine;

namespace CasebookGame.Editor
{
    public static class ProgressionAssetBootstrapper
    {
        const string DepartmentsFolder = "Assets/Resources/Departments";
        const string DistrictsFolder = "Assets/Resources/Districts";
        const string CityLocationsFolder = "Assets/Resources/CityLocations";
        const string CasesFolder = "Assets/Resources/Cases";
        const string SuspectsFolder = "Assets/ScriptableObjects/Cases/Suspects";
        const string ContentMatrixPath = "Docs/content/precinct_map_case_matrix_C001_C030.json";
        const string PresentationManifestPath = "scripts/image-gen/presentation-prompts.json";

        [MenuItem("Casebook/Progression/Ensure Default Department Asset")]
        public static void EnsureDefaultDepartmentAssetMenu() => EnsureDefaultDepartmentAssets();

        [MenuItem("Casebook/Progression/Ensure World Map Assets")]
        public static void EnsureWorldMapAssetsMenu() => EnsureWorldMapAssets();

        public static void EnsureDefaultDepartmentAssets()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder(DepartmentsFolder);
            EnsureWorldMapAssets();
        }

        public static void EnsureWorldMapAssets()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder(DepartmentsFolder);
            EnsureFolder(DistrictsFolder);
            EnsureFolder(CityLocationsFolder);

            if (TryImportAuthoredWorld())
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return;
            }

            EnsureFallbackWorldMapAssets();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static bool TryImportAuthoredWorld()
        {
            var matrix = LoadJsonAsset<PrecinctMatrixRoot>(ContentMatrixPath);
            var manifest = LoadJsonAsset<PresentationManifestRoot>(PresentationManifestPath);
            if (matrix == null || manifest == null || matrix.cases == null || matrix.cases.Length == 0)
                return false;

            var caseAssets = AssetDatabase.FindAssets("t:CaseData", new[] { CasesFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<CaseData>)
                .Where(asset => asset != null)
                .ToDictionary(asset => asset.caseId, asset => asset, StringComparer.OrdinalIgnoreCase);

            var matrixCases = matrix.cases
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.assetCaseId))
                .ToDictionary(entry => entry.assetCaseId, entry => entry, StringComparer.OrdinalIgnoreCase);

            var manifestLocations = (manifest.cityLocations ?? Array.Empty<PresentationCityLocationEntry>())
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.locationId))
                .ToDictionary(entry => entry.locationId, entry => entry, StringComparer.OrdinalIgnoreCase);

            var manifestDistricts = (manifest.districtMarkers ?? Array.Empty<PresentationDistrictEntry>())
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.districtId))
                .ToDictionary(entry => entry.districtId, entry => entry, StringComparer.OrdinalIgnoreCase);

            var locationIconPaths = BuildLocationIconLookup(manifest.locationNodeIcons ?? Array.Empty<PresentationNodeIconEntry>());
            var arcLookup = (matrix.caseArcs ?? Array.Empty<PrecinctCaseArcEntry>())
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.caseArcId))
                .ToDictionary(entry => entry.caseArcId, entry => entry, StringComparer.OrdinalIgnoreCase);

            var departmentEntries = matrix.departments ?? Array.Empty<PrecinctDepartmentEntry>();
            foreach (var departmentEntry in departmentEntries)
                UpsertDepartmentAsset(departmentEntry, manifest.departmentIcons, arcLookup, matrixCases);

            foreach (var districtEntry in matrix.districts ?? Array.Empty<PrecinctDistrictEntry>())
                UpsertDistrictAsset(districtEntry, manifestDistricts, matrixCases, departmentEntries);

            foreach (var locationEntry in manifest.cityLocations ?? Array.Empty<PresentationCityLocationEntry>())
                UpsertCityLocationAsset(locationEntry, locationIconPaths);

            foreach (var pair in caseAssets)
            {
                if (!matrixCases.TryGetValue(pair.Key, out var matrixCase))
                    continue;

                ApplyCaseMetadata(pair.Value, matrixCase, manifestLocations);
                MergeInvolvedSuspects(pair.Value, matrixCase);
                ApplyPilotVisitFlowOverrides(pair.Value);
                EditorUtility.SetDirty(pair.Value);
            }

            return true;
        }

        static void UpsertDepartmentAsset(
            PrecinctDepartmentEntry departmentEntry,
            PresentationDepartmentEntry[] manifestDepartmentIcons,
            IReadOnlyDictionary<string, PrecinctCaseArcEntry> arcLookup,
            IReadOnlyDictionary<string, PrecinctCaseEntry> matrixCases)
        {
            if (departmentEntry == null || string.IsNullOrWhiteSpace(departmentEntry.departmentAssetName))
                return;

            string assetPath = $"{DepartmentsFolder}/{departmentEntry.departmentAssetName}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<DepartmentData>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<DepartmentData>();
                AssetDatabase.CreateAsset(asset, assetPath);
            }

            var unlocks = GetDepartmentUnlockConfig(departmentEntry.departmentId);
            var iconEntry = manifestDepartmentIcons?.FirstOrDefault(entry =>
                entry != null && string.Equals(entry.departmentId, departmentEntry.departmentId, StringComparison.OrdinalIgnoreCase));

            asset.departmentId = ParseDepartmentId(departmentEntry.departmentId);
            asset.displayName = asset.departmentId == DepartmentId.Patrol
                ? "Patrol (Training)"
                : departmentEntry.displayName;
            asset.requiredRank = unlocks.requiredRank;
            asset.requiredStarsCount = unlocks.requiredStars;
            asset.summaryText = unlocks.summaryText;
            asset.unlockBlurb = unlocks.unlockBlurb;
            asset.arcLabel = ResolveDepartmentArcLabel(departmentEntry, arcLookup, matrixCases);
            asset.themeColor = ParseColorOrDefault(iconEntry?.color, asset.themeColor);
            asset.mapIcon = LoadSprite(iconEntry?.output);
            asset.caseIds = (departmentEntry.caseIds ?? Array.Empty<string>())
                .Select(NormalizeCaseId)
                .Where(caseId => !string.IsNullOrWhiteSpace(caseId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            EditorUtility.SetDirty(asset);
        }

        static void UpsertDistrictAsset(
            PrecinctDistrictEntry districtEntry,
            IReadOnlyDictionary<string, PresentationDistrictEntry> manifestDistricts,
            IReadOnlyDictionary<string, PrecinctCaseEntry> matrixCases,
            PrecinctDepartmentEntry[] departmentEntries)
        {
            if (districtEntry == null || string.IsNullOrWhiteSpace(districtEntry.districtId))
                return;

            string assetPath = $"{DistrictsFolder}/{districtEntry.districtId}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<DistrictData>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<DistrictData>();
                AssetDatabase.CreateAsset(asset, assetPath);
            }

            manifestDistricts.TryGetValue(districtEntry.districtId, out var markerEntry);
            ResolveDistrictUnlocks(districtEntry.districtId, matrixCases, departmentEntries, out int requiredRank, out int requiredStars);

            asset.districtId = districtEntry.districtId;
            asset.displayName = districtEntry.displayName;
            asset.sortOrder = districtEntry.sortOrder;
            asset.accentColor = ParseColorOrDefault(markerEntry?.color, asset.accentColor);
            asset.requiredRank = requiredRank;
            asset.requiredStarsCount = requiredStars;
            asset.mapIcon = LoadSprite(markerEntry?.output);

            EditorUtility.SetDirty(asset);
        }

        static void UpsertCityLocationAsset(
            PresentationCityLocationEntry locationEntry,
            IReadOnlyDictionary<string, string> locationIconPaths)
        {
            if (locationEntry == null || string.IsNullOrWhiteSpace(locationEntry.locationId))
                return;

            string assetPath = $"{CityLocationsFolder}/{locationEntry.locationId}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<CityLocationData>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<CityLocationData>();
                AssetDatabase.CreateAsset(asset, assetPath);
            }

            asset.locationId = locationEntry.locationId;
            asset.districtId = locationEntry.districtId;
            asset.displayName = locationEntry.displayName;
            asset.mapPosition = new Vector2(locationEntry.mapPosition.x, locationEntry.mapPosition.y);
            asset.nodeIcon = LoadSprite(locationIconPaths.TryGetValue(locationEntry.locationId, out var iconPath) ? iconPath : null);
            asset.defaultBackground = LoadSprite(locationEntry.defaultBackgroundPath);

            EditorUtility.SetDirty(asset);
        }

        static void ApplyCaseMetadata(
            CaseData caseData,
            PrecinctCaseEntry matrixCase,
            IReadOnlyDictionary<string, PresentationCityLocationEntry> manifestLocations)
        {
            caseData.departmentId = ParseDepartmentId(matrixCase.departmentId);
            caseData.districtId = matrixCase.districtId;
            caseData.cityLocationId = matrixCase.cityLocationId;
            caseData.caseArcId = matrixCase.caseArcId;
            caseData.arcBeatSummary = matrixCase.arcBeatSummary;
            caseData.visitFlowMode = ParseEnumOrDefault(matrixCase.visitFlowMode, CaseVisitFlowMode.LegacyFallback);
            caseData.startingLocationId = ResolveStartingLocationId(matrixCase);
            caseData.allowMapRevisit = matrixCase.allowMapRevisit;
            caseData.locationReadyForSolveMode = ParseEnumOrDefault(matrixCase.locationReadyForSolveMode, CaseSolveGateMode.LegacyContradictionOnly);
            caseData.interrogationMode = matrixCase.interrogationUsage?.mode ?? string.Empty;
            caseData.isInterrogationForwardCase = matrixCase.interrogationUsage != null && matrixCase.interrogationUsage.isMilestoneForwardCase;
            caseData.interrogationFocusSummary = matrixCase.interrogationUsage?.focus ?? string.Empty;
            caseData.interrogationOutcomes = BuildInterrogationOutcomes(matrixCase.interrogationOutcomes, caseData);

            caseData.suspectSummaries = (matrixCase.suspectRelevance ?? Array.Empty<PrecinctSuspectEntry>())
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.displayName))
                .Select(entry => new CaseSuspectSummary
                {
                    suspectId = entry.suspectId ?? string.Empty,
                    displayName = entry.displayName,
                    relevance = entry.relevance ?? string.Empty
                })
                .ToList();

            caseData.caseLocations = BuildCaseLocations(caseData, matrixCase, manifestLocations);
        }

        static List<CaseLocationData> BuildCaseLocations(
            CaseData caseData,
            PrecinctCaseEntry matrixCase,
            IReadOnlyDictionary<string, PresentationCityLocationEntry> manifestLocations)
        {
            var existingPrimary = caseData.caseLocations != null && caseData.caseLocations.Count > 0
                ? caseData.caseLocations[0]
                : null;

            var output = new List<CaseLocationData>();
            var sourceLocations = matrixCase.caseLocations ?? Array.Empty<PrecinctCaseLocationEntry>();
            if (sourceLocations.Length == 0)
            {
                output.Add(existingPrimary ?? caseData.GetResolvedLocation(0));
                return output;
            }

            foreach (var sourceLocation in sourceLocations.OrderBy(entry => entry.visitOrder))
            {
                if (sourceLocation == null)
                    continue;

                bool isPrimary = output.Count == 0;
                var location = new CaseLocationData
                {
                    locationId = sourceLocation.locationId,
                    displayName = sourceLocation.displayName,
                    sceneBackground = ResolveLocationBackground(caseData, existingPrimary, sourceLocation, manifestLocations, isPrimary),
                    hotspots = isPrimary
                        ? CloneHotspots(existingPrimary?.hotspots?.Count > 0 ? existingPrimary.hotspots : caseData.hotspots)
                        : new List<HotspotData>(),
                    entryText = sourceLocation.entryText,
                    visitOrder = sourceLocation.visitOrder,
                    unlockEvidenceIds = NormalizeEvidenceIdsForCase(sourceLocation.unlockEvidenceIds, caseData),
                    unlockTags = ParseEvidenceTags(sourceLocation.unlockTags),
                    unlockCondition = BuildCondition(sourceLocation.unlockCondition, caseData),
                    nextLocationIds = NormalizeStringList(sourceLocation.nextLocationIds),
                    revisitRule = ParseEnumOrDefault(sourceLocation.revisitRule, LocationRevisitRule.Always),
                    presentSuspects = BuildLocationSuspectPresence(sourceLocation.presentSuspects, caseData),
                    autoCompleteOnEnter = sourceLocation.autoCompleteOnEnter,
                    completionOutcomeId = sourceLocation.completionOutcomeId ?? string.Empty,
                    autoUnlocksSolve = sourceLocation.autoUnlocksSolve,
                    isRequiredForSolve = sourceLocation.isRequiredForSolve
                };

                if (!location.autoCompleteOnEnter
                    && (location.hotspots == null || location.hotspots.Count == 0)
                    && HasRouteOnlyProgressionHook(sourceLocation))
                {
                    location.autoCompleteOnEnter = true;
                }

                output.Add(location);
            }

            return output;
        }

        static Sprite ResolveLocationBackground(
            CaseData caseData,
            CaseLocationData existingPrimary,
            PrecinctCaseLocationEntry sourceLocation,
            IReadOnlyDictionary<string, PresentationCityLocationEntry> manifestLocations,
            bool isPrimary)
        {
            if (isPrimary)
                return existingPrimary?.sceneBackground ?? caseData.sceneBackground;

            if (manifestLocations.TryGetValue(sourceLocation.locationId, out var manifestLocation))
                return LoadSprite(manifestLocation.defaultBackgroundPath) ?? caseData.sceneBackground;

            return caseData.sceneBackground;
        }

        static void MergeInvolvedSuspects(CaseData caseData, PrecinctCaseEntry matrixCase)
        {
            caseData.involvedSuspects ??= new List<SuspectData>();
            var existingIds = new HashSet<string>(
                caseData.involvedSuspects.Where(asset => asset != null && !string.IsNullOrWhiteSpace(asset.suspectId))
                    .Select(asset => asset.suspectId),
                StringComparer.OrdinalIgnoreCase);

            foreach (var suspectEntry in matrixCase.suspectRelevance ?? Array.Empty<PrecinctSuspectEntry>())
            {
                if (suspectEntry == null || string.IsNullOrWhiteSpace(suspectEntry.suspectId) || existingIds.Contains(suspectEntry.suspectId))
                    continue;

                string suspectPath = $"{SuspectsFolder}/{suspectEntry.suspectId}.asset";
                var suspectAsset = AssetDatabase.LoadAssetAtPath<SuspectData>(suspectPath);
                if (suspectAsset == null)
                    continue;

                caseData.involvedSuspects.Add(suspectAsset);
                existingIds.Add(suspectEntry.suspectId);
            }
        }

        static void ApplyPilotVisitFlowOverrides(CaseData caseData)
        {
            if (caseData == null || string.IsNullOrWhiteSpace(caseData.caseId))
                return;

            switch (caseData.caseId)
            {
                case "Case_020":
                    ApplyCase020PilotOverrides(caseData);
                    break;
                case "Case_030":
                    ApplyCase030PilotOverrides(caseData);
                    break;
            }
        }

        static void ApplyCase020PilotOverrides(CaseData caseData)
        {
            if (caseData.caseLocations == null || caseData.caseLocations.Count < 2)
                return;

            var vault = caseData.caseLocations[0];
            var elevator = caseData.caseLocations[1];
            if (vault == null || elevator == null)
                return;

            caseData.schemaVersion = CaseSchemaVersions.Current;
            caseData.visitFlowMode = CaseVisitFlowMode.SequenceGraph;
            caseData.startingLocationId = vault.locationId;
            caseData.allowMapRevisit = true;
            caseData.locationReadyForSolveMode = CaseSolveGateMode.RequireInterrogationOutcome;
            caseData.interrogationMode = "SuspectVisitRouting";
            caseData.isInterrogationForwardCase = true;
            caseData.interrogationFocusSummary = "Pressure Mira in the vault, then follow the exposed freight route to the annex handoff.";
            caseData.interrogationOutcomes = new List<InterrogationOutcomeData>
            {
                CreateOutcome(
                    "C020_OUTCOME_UNLOCK_ELEVATOR",
                    "Freight route exposed",
                    "Mira's answer opens the freight elevator lead and pushes the team to the annex handoff point.",
                    unlockLocationIds: new[] { elevator.locationId },
                    revealSuspectIds: new[] { "S013" },
                    redirectToLocationId: elevator.locationId),
                CreateOutcome(
                    "C020_OUTCOME_READY_FOR_SOLVE",
                    "Internal leak confirmed",
                    "The bureau-side transfer story is pinned down. The contradiction board is ready for final closure.",
                    revealSuspectIds: new[] { "S011", "S013" },
                    markCaseReadyForSolve: true)
            };

            ConfigurePilotLocation(
                vault,
                revisitRule: LocationRevisitRule.AfterNewProgress,
                nextLocationIds: new[] { elevator.locationId },
                presentSuspects: new[]
                {
                    CreatePresence("S011", "Holding the vault story together for now", "C020_INT001")
                });

            ConfigurePilotLocation(
                elevator,
                revisitRule: LocationRevisitRule.Always,
                unlockCondition: BuildPilotCondition(requiredOutcomeIds: new[] { "C020_OUTCOME_UNLOCK_ELEVATOR" }),
                presentSuspects: new[]
                {
                    CreatePresence("S013", "Waiting at the transfer route the first answer exposes", "C020_INT002")
                });

            UpdateInterrogationNode(caseData, "C020_INT001", "S011", vault.locationId, "C020_OUTCOME_UNLOCK_ELEVATOR");
            UpdateInterrogationNode(caseData, "C020_INT002", "S013", elevator.locationId, "C020_OUTCOME_READY_FOR_SOLVE");
        }

        static void ApplyCase030PilotOverrides(CaseData caseData)
        {
            if (caseData.caseLocations == null || caseData.caseLocations.Count < 3)
                return;

            var archiveFloor = caseData.caseLocations[0];
            var corridor = caseData.caseLocations[1];
            var transferTube = caseData.caseLocations[2];
            if (archiveFloor == null || corridor == null || transferTube == null)
                return;

            caseData.schemaVersion = CaseSchemaVersions.Current;
            caseData.visitFlowMode = CaseVisitFlowMode.SequenceGraph;
            caseData.startingLocationId = archiveFloor.locationId;
            caseData.allowMapRevisit = true;
            caseData.locationReadyForSolveMode = CaseSolveGateMode.RequireInterrogationOutcome;
            caseData.interrogationMode = "SuspectVisitRouting";
            caseData.isInterrogationForwardCase = true;
            caseData.interrogationFocusSummary = "Trace the leak from Vera's desk trail to the corridor mirror line, then corner the transfer-tube explanation.";
            caseData.interrogationOutcomes = new List<InterrogationOutcomeData>
            {
                CreateOutcome(
                    "C030_OUTCOME_UNLOCK_CORRIDOR",
                    "Corridor lead opened",
                    "The first archive pressure point exposes the corridor mirror line as the next stop.",
                    unlockLocationIds: new[] { corridor.locationId },
                    revealSuspectIds: new[] { "S030" },
                    redirectToLocationId: corridor.locationId),
                CreateOutcome(
                    "C030_OUTCOME_UNLOCK_TRANSFER",
                    "Transfer rack exposed",
                    "The corridor answer points directly to the transfer-tube rack and the operational leak path.",
                    unlockLocationIds: new[] { transferTube.locationId },
                    revealSuspectIds: new[] { "S029", "S030" },
                    redirectToLocationId: transferTube.locationId),
                CreateOutcome(
                    "C030_OUTCOME_READY_FOR_SOLVE",
                    "Desk conspiracy locked",
                    "The transfer explanation completes the macro route. The case is ready for final contradiction.",
                    revealSuspectIds: new[] { "S029", "S030" },
                    markCaseReadyForSolve: true)
            };

            ConfigurePilotLocation(
                archiveFloor,
                revisitRule: LocationRevisitRule.AfterNewProgress,
                nextLocationIds: new[] { corridor.locationId },
                presentSuspects: new[]
                {
                    CreatePresence("S029", "Still controlling the narrative from the records floor", "C030_INT001")
                });

            ConfigurePilotLocation(
                corridor,
                revisitRule: LocationRevisitRule.AfterNewProgress,
                unlockCondition: BuildPilotCondition(requiredOutcomeIds: new[] { "C030_OUTCOME_UNLOCK_CORRIDOR" }),
                nextLocationIds: new[] { transferTube.locationId },
                presentSuspects: new[]
                {
                    CreatePresence("S030", "Hovering near the mirrored blind spot the first answer uncovers", "C030_INT002")
                });

            ConfigurePilotLocation(
                transferTube,
                revisitRule: LocationRevisitRule.Always,
                unlockCondition: BuildPilotCondition(requiredOutcomeIds: new[] { "C030_OUTCOME_UNLOCK_TRANSFER" }),
                presentSuspects: new[]
                {
                    CreatePresence("S029", "Back on the route where the transfer story falls apart", "C030_INT003")
                });

            UpdateInterrogationNode(caseData, "C030_INT001", "S029", archiveFloor.locationId, "C030_OUTCOME_UNLOCK_CORRIDOR");
            UpdateInterrogationNode(caseData, "C030_INT002", "S030", corridor.locationId, "C030_OUTCOME_UNLOCK_TRANSFER");
            UpdateInterrogationNode(caseData, "C030_INT003", "S029", transferTube.locationId, "C030_OUTCOME_READY_FOR_SOLVE");
        }

        static void ConfigurePilotLocation(
            CaseLocationData location,
            LocationRevisitRule revisitRule,
            string[] nextLocationIds = null,
            CaseProgressConditionData unlockCondition = null,
            LocationSuspectPresenceData[] presentSuspects = null)
        {
            if (location == null)
                return;

            location.unlockEvidenceIds = new List<string>();
            location.unlockTags = new List<EvidenceTag>();
            location.unlockCondition = unlockCondition ?? new CaseProgressConditionData();
            location.nextLocationIds = nextLocationIds != null ? new List<string>(nextLocationIds) : new List<string>();
            location.revisitRule = revisitRule;
            location.presentSuspects = presentSuspects != null ? new List<LocationSuspectPresenceData>(presentSuspects) : new List<LocationSuspectPresenceData>();
            location.completionOutcomeId = string.Empty;
            location.autoUnlocksSolve = false;
            location.isRequiredForSolve = true;
        }

        static CaseProgressConditionData BuildPilotCondition(
            string[] requiredOutcomeIds = null,
            string[] requiredVisitedLocationIds = null,
            string[] requiredCompletedNodeIds = null)
        {
            return new CaseProgressConditionData
            {
                requiredInterrogationOutcomeIds = NormalizeStringList(requiredOutcomeIds),
                requiredVisitedLocationIds = NormalizeStringList(requiredVisitedLocationIds),
                requiredCompletedInterrogationNodeIds = NormalizeStringList(requiredCompletedNodeIds),
                matchMode = ConditionMatchMode.All
            };
        }

        static LocationSuspectPresenceData CreatePresence(string suspectId, string presenceLabel, string interrogationEntryNodeId)
        {
            return new LocationSuspectPresenceData
            {
                suspectId = suspectId,
                presenceLabel = presenceLabel,
                isVisibleOnEntry = true,
                availabilityCondition = new CaseProgressConditionData(),
                interrogationEntryNodeId = interrogationEntryNodeId,
                departureOutcomeId = string.Empty,
                notes = string.Empty
            };
        }

        static InterrogationOutcomeData CreateOutcome(
            string outcomeId,
            string displayLabel,
            string summaryText,
            string[] unlockLocationIds = null,
            string[] revealSuspectIds = null,
            string redirectToLocationId = "",
            bool markCaseReadyForSolve = false)
        {
            return new InterrogationOutcomeData
            {
                outcomeId = outcomeId,
                displayLabel = displayLabel,
                summaryText = summaryText,
                unlockLocationIds = NormalizeStringList(unlockLocationIds),
                lockLocationIds = new List<string>(),
                revealSuspectIds = NormalizeStringList(revealSuspectIds),
                hideSuspectIds = new List<string>(),
                grantEvidenceIds = new List<string>(),
                grantTags = new List<EvidenceTag>(),
                markCaseReadyForSolve = markCaseReadyForSolve,
                redirectToLocationId = redirectToLocationId ?? string.Empty
            };
        }

        static void UpdateInterrogationNode(
            CaseData caseData,
            string nodeId,
            string suspectId,
            string locationContextId,
            string outcomeIdOnCorrect)
        {
            var node = caseData?.interrogationNodes?.FirstOrDefault(candidate =>
                candidate != null && string.Equals(candidate.nodeId, nodeId, StringComparison.OrdinalIgnoreCase));
            if (node == null)
                return;

            node.schemaVersion = CaseSchemaVersions.Current;
            node.suspectId = suspectId ?? string.Empty;
            node.locationContextId = locationContextId ?? string.Empty;
            node.nextNodeIdOnCorrect = string.Empty;
            node.nextNodeIdOnWrong = string.Empty;
            node.outcomeIdOnCorrect = outcomeIdOnCorrect ?? string.Empty;
            node.outcomeIdOnWrong = string.Empty;
            EditorUtility.SetDirty(node);
        }

        static void ResolveDistrictUnlocks(
            string districtId,
            IReadOnlyDictionary<string, PrecinctCaseEntry> matrixCases,
            PrecinctDepartmentEntry[] departmentEntries,
            out int requiredRank,
            out int requiredStars)
        {
            requiredRank = 1;
            requiredStars = 0;

            var candidateCases = matrixCases.Values
                .Where(entry => entry != null && string.Equals(entry.districtId, districtId, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (candidateCases.Count == 0)
                return;

            var unlocks = candidateCases
                .Select(entry => GetDepartmentUnlockConfig(entry.departmentId))
                .OrderBy(config => config.requiredRank)
                .ThenBy(config => config.requiredStars)
                .FirstOrDefault();

            requiredRank = unlocks.requiredRank;
            requiredStars = unlocks.requiredStars;
        }

        static string ResolveDepartmentArcLabel(
            PrecinctDepartmentEntry departmentEntry,
            IReadOnlyDictionary<string, PrecinctCaseArcEntry> arcLookup,
            IReadOnlyDictionary<string, PrecinctCaseEntry> matrixCases)
        {
            var firstCaseId = departmentEntry.caseIds?.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(firstCaseId))
                return string.Empty;

            string normalizedCaseId = NormalizeCaseId(firstCaseId);
            if (!matrixCases.TryGetValue(normalizedCaseId, out var caseEntry) || string.IsNullOrWhiteSpace(caseEntry.caseArcId))
                return string.Empty;

            return arcLookup.TryGetValue(caseEntry.caseArcId, out var arcEntry)
                ? arcEntry.displayName
                : caseEntry.caseArcId;
        }

        static (int requiredRank, int requiredStars, string summaryText, string unlockBlurb) GetDepartmentUnlockConfig(string departmentId)
        {
            return departmentId switch
            {
                "Fraud" => (4, 12,
                    "Financial contradictions, paper-trail pressure, and the first full conspiracy desk.",
                    "Promotion to Fraud requires Rank 4 and 12 total stars from patrol fieldwork."),
                "MissingPersons" => (8, 24,
                    "Person-of-interest cases, movement tracing, and the capstone leak investigation.",
                    "Missing Persons opens at Rank 8 with 24 stars once the city trusts your broader casework."),
                _ => (1, 0,
                    "Foundational patrol files that teach the city, the departments, and the contradiction engine.",
                    "Patrol is your starting board. Clear cases cleanly to earn promotions.")
            };
        }

        static DepartmentId ParseDepartmentId(string departmentId)
        {
            if (Enum.TryParse(departmentId, true, out DepartmentId parsed))
                return parsed;

            return DepartmentId.Patrol;
        }

        static Dictionary<string, string> BuildLocationIconLookup(IEnumerable<PresentationNodeIconEntry> nodeEntries)
        {
            var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var nodeEntry in nodeEntries)
            {
                if (nodeEntry == null || string.IsNullOrWhiteSpace(nodeEntry.output) || nodeEntry.locationIds == null)
                    continue;

                foreach (var locationId in nodeEntry.locationIds)
                {
                    if (!string.IsNullOrWhiteSpace(locationId))
                        lookup[locationId] = nodeEntry.output;
                }
            }

            return lookup;
        }

        static T LoadJsonAsset<T>(string path) where T : class
        {
            if (!System.IO.File.Exists(path))
                return null;

            string json = System.IO.File.ReadAllText(path);
            return string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<T>(json);
        }

        static Sprite LoadSprite(string assetPath) =>
            string.IsNullOrWhiteSpace(assetPath) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);

        static Color ParseColorOrDefault(string htmlColor, Color fallback) =>
            !string.IsNullOrWhiteSpace(htmlColor) && ColorUtility.TryParseHtmlString(htmlColor, out var parsed)
                ? parsed
                : fallback;

        static string NormalizeCaseId(string caseId)
        {
            if (string.IsNullOrWhiteSpace(caseId))
                return string.Empty;

            if (caseId.StartsWith("Case_", StringComparison.OrdinalIgnoreCase))
                return caseId;

            if (caseId.Length == 4 && (caseId[0] == 'C' || caseId[0] == 'c'))
                return $"Case_{caseId[1..]}";

            return caseId;
        }

        static string ResolveStartingLocationId(PrecinctCaseEntry matrixCase)
        {
            if (!string.IsNullOrWhiteSpace(matrixCase?.startingLocationId))
                return matrixCase.startingLocationId;

            return matrixCase?.caseLocations?
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.locationId))
                .OrderBy(entry => entry.visitOrder)
                .Select(entry => entry.locationId)
                .FirstOrDefault()
                ?? string.Empty;
        }

        static List<string> NormalizeEvidenceIdsForCase(string[] evidenceIds, CaseData caseData)
        {
            var output = new List<string>();
            foreach (var evidenceId in evidenceIds ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(evidenceId))
                    continue;

                output.Add(NormalizeEvidenceIdForCase(evidenceId, caseData));
            }

            return output;
        }

        static string NormalizeEvidenceIdForCase(string evidenceId, CaseData caseData)
        {
            if (caseData?.evidence == null || string.IsNullOrWhiteSpace(evidenceId))
                return evidenceId;

            if (caseData.evidence.Any(evidence => evidence != null && string.Equals(evidence.evidenceId, evidenceId, StringComparison.OrdinalIgnoreCase)))
                return evidenceId;

            int separatorIndex = evidenceId.IndexOf("_E", StringComparison.OrdinalIgnoreCase);
            if (separatorIndex > 0)
            {
                string suffix = evidenceId[(separatorIndex + 1)..];
                var localMatch = caseData.evidence.FirstOrDefault(evidence =>
                    evidence != null && string.Equals(evidence.evidenceId, suffix, StringComparison.OrdinalIgnoreCase));
                if (localMatch != null)
                    return localMatch.evidenceId;
            }

            return evidenceId;
        }

        static List<EvidenceTag> ParseEvidenceTags(string[] tagValues)
        {
            var tags = new List<EvidenceTag>();
            foreach (var tagValue in tagValues ?? Array.Empty<string>())
            {
                if (Enum.TryParse(tagValue, true, out EvidenceTag tag))
                    tags.Add(tag);
            }

            return tags;
        }

        static CaseProgressConditionData BuildCondition(PrecinctCaseProgressConditionEntry entry, CaseData caseData)
        {
            if (entry == null)
                return new CaseProgressConditionData();

            return new CaseProgressConditionData
            {
                requiredEvidenceIds = NormalizeEvidenceIdsForCase(entry.requiredEvidenceIds, caseData),
                requiredTags = ParseEvidenceTags(entry.requiredTags),
                requiredVisitedLocationIds = NormalizeStringList(entry.requiredVisitedLocationIds),
                requiredCompletedLocationIds = NormalizeStringList(entry.requiredCompletedLocationIds),
                requiredCompletedInterrogationNodeIds = NormalizeStringList(entry.requiredCompletedInterrogationNodeIds),
                requiredInterrogationOutcomeIds = NormalizeStringList(entry.requiredInterrogationOutcomeIds),
                requiredSuspectIds = NormalizeStringList(entry.requiredSuspectIds),
                matchMode = ParseEnumOrDefault(entry.matchMode, ConditionMatchMode.All)
            };
        }

        static List<LocationSuspectPresenceData> BuildLocationSuspectPresence(PrecinctLocationSuspectPresenceEntry[] entries, CaseData caseData)
        {
            var output = new List<LocationSuspectPresenceData>();
            foreach (var entry in entries ?? Array.Empty<PrecinctLocationSuspectPresenceEntry>())
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.suspectId))
                    continue;

                output.Add(new LocationSuspectPresenceData
                {
                    suspectId = entry.suspectId,
                    presenceLabel = entry.presenceLabel ?? string.Empty,
                    isVisibleOnEntry = entry.isVisibleOnEntry,
                    availabilityCondition = BuildCondition(entry.availabilityCondition, caseData),
                    interrogationEntryNodeId = entry.interrogationEntryNodeId ?? string.Empty,
                    departureOutcomeId = entry.departureOutcomeId ?? string.Empty,
                    notes = entry.notes ?? string.Empty
                });
            }

            return output;
        }

        static bool HasRouteOnlyProgressionHook(PrecinctCaseLocationEntry entry)
        {
            if (entry == null)
                return false;

            if (!string.IsNullOrWhiteSpace(entry.completionOutcomeId) || entry.autoUnlocksSolve)
                return true;

            foreach (var presence in entry.presentSuspects ?? Array.Empty<PrecinctLocationSuspectPresenceEntry>())
            {
                if (presence != null
                    && (!string.IsNullOrWhiteSpace(presence.interrogationEntryNodeId)
                        || !string.IsNullOrWhiteSpace(presence.departureOutcomeId)))
                {
                    return true;
                }
            }

            return false;
        }

        static List<InterrogationOutcomeData> BuildInterrogationOutcomes(PrecinctInterrogationOutcomeEntry[] entries, CaseData caseData)
        {
            var output = new List<InterrogationOutcomeData>();
            foreach (var entry in entries ?? Array.Empty<PrecinctInterrogationOutcomeEntry>())
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.outcomeId))
                    continue;

                output.Add(new InterrogationOutcomeData
                {
                    outcomeId = entry.outcomeId,
                    displayLabel = entry.displayLabel ?? string.Empty,
                    summaryText = entry.summaryText ?? string.Empty,
                    unlockLocationIds = NormalizeStringList(entry.unlockLocationIds),
                    lockLocationIds = NormalizeStringList(entry.lockLocationIds),
                    revealSuspectIds = NormalizeStringList(entry.revealSuspectIds),
                    hideSuspectIds = NormalizeStringList(entry.hideSuspectIds),
                    grantEvidenceIds = NormalizeEvidenceIdsForCase(entry.grantEvidenceIds, caseData),
                    grantTags = ParseEvidenceTags(entry.grantTags),
                    markCaseReadyForSolve = entry.markCaseReadyForSolve,
                    redirectToLocationId = entry.redirectToLocationId ?? string.Empty
                });
            }

            return output;
        }

        static List<string> NormalizeStringList(string[] values)
        {
            var output = new List<string>();
            foreach (var value in values ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(value))
                    output.Add(value);
            }

            return output;
        }

        static TEnum ParseEnumOrDefault<TEnum>(string value, TEnum fallback) where TEnum : struct
        {
            if (!string.IsNullOrWhiteSpace(value) && Enum.TryParse(value, true, out TEnum parsed))
                return parsed;

            return fallback;
        }

        static List<HotspotData> CloneHotspots(List<HotspotData> source)
        {
            var clone = new List<HotspotData>();
            if (source == null)
                return clone;

            foreach (var hotspot in source)
            {
                if (hotspot == null)
                    continue;

                clone.Add(new HotspotData
                {
                    hotspotId = hotspot.hotspotId,
                    normalizedPosition = hotspot.normalizedPosition,
                    radius = hotspot.radius,
                    evidenceId = hotspot.evidenceId,
                    hotspotLabel = hotspot.hotspotLabel
                });
            }

            return clone;
        }

        static void EnsureFallbackWorldMapAssets()
        {
            var existingDepartments = AssetDatabase.FindAssets("t:DepartmentData", new[] { DepartmentsFolder });
            if (existingDepartments.Length == 0)
            {
                var cases = AssetDatabase.FindAssets("t:CaseData", new[] { CasesFolder })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<CaseData>)
                    .Where(asset => asset != null)
                    .OrderBy(asset => asset.caseId)
                    .ToArray();

                var department = ScriptableObject.CreateInstance<DepartmentData>();
                department.departmentId = DepartmentId.Patrol;
                department.displayName = "Patrol (Training)";
                department.requiredRank = 1;
                department.requiredStarsCount = 0;
                department.summaryText = "Fallback department asset generated from available cases.";
                department.caseIds = cases.Select(asset => asset.caseId).ToList();
                AssetDatabase.CreateAsset(department, $"{DepartmentsFolder}/Patrol_Training.asset");
                EditorUtility.SetDirty(department);
            }

            var departments = AssetDatabase.FindAssets("t:DepartmentData", new[] { DepartmentsFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<DepartmentData>)
                .Where(asset => asset != null)
                .OrderBy(asset => asset.requiredRank)
                .ThenBy(asset => asset.displayName)
                .ToList();

            var casesForFallback = AssetDatabase.FindAssets("t:CaseData", new[] { CasesFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<CaseData>)
                .Where(asset => asset != null)
                .OrderBy(asset => asset.caseId)
                .ToList();

            foreach (var caseData in casesForFallback)
            {
                var department = departments.FirstOrDefault(candidate => candidate.ContainsCase(caseData.caseId)) ?? departments.FirstOrDefault();
                if (department == null)
                    continue;

                caseData.departmentId = department.departmentId;
                caseData.districtId = string.IsNullOrWhiteSpace(caseData.districtId)
                    ? $"district_{department.departmentId}".ToLowerInvariant()
                    : caseData.districtId;
                caseData.cityLocationId = string.IsNullOrWhiteSpace(caseData.cityLocationId)
                    ? $"location_{caseData.caseId}".ToLowerInvariant()
                    : caseData.cityLocationId;
                caseData.caseLocations ??= new List<CaseLocationData>();
                if (caseData.caseLocations.Count == 0)
                {
                    caseData.caseLocations.Add(new CaseLocationData
                    {
                        locationId = caseData.cityLocationId,
                        displayName = caseData.title,
                        sceneBackground = caseData.sceneBackground,
                        hotspots = CloneHotspots(caseData.hotspots),
                        entryText = caseData.briefText,
                        visitOrder = 0,
                        isRequiredForSolve = true
                    });
                }

                EditorUtility.SetDirty(caseData);
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parts = path.Split('/');
            string parent = string.Join("/", parts, 0, parts.Length - 1);
            AssetDatabase.CreateFolder(parent, parts[^1]);
        }

        [Serializable]
        sealed class PrecinctMatrixRoot
        {
            public PrecinctDepartmentEntry[] departments;
            public PrecinctDistrictEntry[] districts;
            public PrecinctCaseArcEntry[] caseArcs;
            public PrecinctCaseEntry[] cases;
        }

        [Serializable]
        sealed class PrecinctDepartmentEntry
        {
            public string departmentId;
            public string displayName;
            public string departmentAssetName;
            public string[] caseIds;
        }

        [Serializable]
        sealed class PrecinctDistrictEntry
        {
            public string districtId;
            public string displayName;
            public int sortOrder;
            public string themeSummary;
        }

        [Serializable]
        sealed class PrecinctCaseArcEntry
        {
            public string caseArcId;
            public string displayName;
            public string[] caseIds;
            public string arcSummary;
        }

        [Serializable]
        sealed class PrecinctCaseEntry
        {
            public string caseId;
            public string assetCaseId;
            public string departmentId;
            public string districtId;
            public string cityLocationId;
            public string caseArcId;
            public string arcBeatSummary;
            public string visitFlowMode;
            public string startingLocationId;
            public bool allowMapRevisit = true;
            public string locationReadyForSolveMode;
            public PrecinctInterrogationUsage interrogationUsage;
            public PrecinctInterrogationOutcomeEntry[] interrogationOutcomes;
            public PrecinctSuspectEntry[] suspectRelevance;
            public PrecinctCaseLocationEntry[] caseLocations;
        }

        [Serializable]
        sealed class PrecinctInterrogationUsage
        {
            public string mode;
            public bool isMilestoneForwardCase;
            public string focus;
        }

        [Serializable]
        sealed class PrecinctSuspectEntry
        {
            public string suspectId;
            public string displayName;
            public string relevance;
        }

        [Serializable]
        sealed class PrecinctCaseLocationEntry
        {
            public string locationId;
            public string displayName;
            public string entryText;
            public int visitOrder;
            public string[] unlockEvidenceIds;
            public string[] unlockTags;
            public PrecinctCaseProgressConditionEntry unlockCondition;
            public string[] nextLocationIds;
            public string revisitRule = "Always";
            public PrecinctLocationSuspectPresenceEntry[] presentSuspects;
            public bool autoCompleteOnEnter = false;
            public string completionOutcomeId;
            public bool autoUnlocksSolve = false;
            public bool isRequiredForSolve = true;
        }

        [Serializable]
        sealed class PrecinctCaseProgressConditionEntry
        {
            public string[] requiredEvidenceIds;
            public string[] requiredTags;
            public string[] requiredVisitedLocationIds;
            public string[] requiredCompletedLocationIds;
            public string[] requiredCompletedInterrogationNodeIds;
            public string[] requiredInterrogationOutcomeIds;
            public string[] requiredSuspectIds;
            public string matchMode = "All";
        }

        [Serializable]
        sealed class PrecinctLocationSuspectPresenceEntry
        {
            public string suspectId;
            public string presenceLabel;
            public bool isVisibleOnEntry = true;
            public PrecinctCaseProgressConditionEntry availabilityCondition;
            public string interrogationEntryNodeId;
            public string departureOutcomeId;
            public string notes;
        }

        [Serializable]
        sealed class PrecinctInterrogationOutcomeEntry
        {
            public string outcomeId;
            public string displayLabel;
            public string summaryText;
            public string[] unlockLocationIds;
            public string[] lockLocationIds;
            public string[] revealSuspectIds;
            public string[] hideSuspectIds;
            public string[] grantEvidenceIds;
            public string[] grantTags;
            public bool markCaseReadyForSolve = false;
            public string redirectToLocationId;
        }

        [Serializable]
        sealed class PresentationManifestRoot
        {
            public PresentationDepartmentEntry[] departmentIcons;
            public PresentationDistrictEntry[] districtMarkers;
            public PresentationNodeIconEntry[] locationNodeIcons;
            public PresentationCityLocationEntry[] cityLocations;
        }

        [Serializable]
        sealed class PresentationDepartmentEntry
        {
            public string departmentId;
            public string color;
            public string output;
        }

        [Serializable]
        sealed class PresentationDistrictEntry
        {
            public string districtId;
            public string color;
            public string output;
        }

        [Serializable]
        sealed class PresentationNodeIconEntry
        {
            public string output;
            public string[] locationIds;
        }

        [Serializable]
        sealed class PresentationCityLocationEntry
        {
            public string locationId;
            public string districtId;
            public string displayName;
            public string launchCaseId;
            public string nodeArchetypeId;
            public PresentationPoint mapPosition;
            public string defaultBackgroundPath;
        }

        [Serializable]
        sealed class PresentationPoint
        {
            public float x;
            public float y;
        }
    }
}
#endif
