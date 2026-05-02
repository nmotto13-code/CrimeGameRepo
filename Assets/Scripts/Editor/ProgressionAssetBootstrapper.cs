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
            caseData.interrogationMode = matrixCase.interrogationUsage?.mode ?? string.Empty;
            caseData.isInterrogationForwardCase = matrixCase.interrogationUsage != null && matrixCase.interrogationUsage.isMilestoneForwardCase;
            caseData.interrogationFocusSummary = matrixCase.interrogationUsage?.focus ?? string.Empty;

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

                bool isPrimary = sourceLocation.visitOrder == 0;
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
                    isRequiredForSolve = sourceLocation.isRequiredForSolve
                };

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
            public PrecinctInterrogationUsage interrogationUsage;
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
            public bool isRequiredForSolve = true;
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
