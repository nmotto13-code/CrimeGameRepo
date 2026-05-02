using System.Collections.Generic;
using UnityEngine;

namespace CasebookGame.Data
{
    [System.Serializable]
    public class CaseSuspectSummary
    {
        public string suspectId;
        public string displayName;
        public string relevance;
    }

    [CreateAssetMenu(fileName = "New Case", menuName = "Casebook/Case Data")]
    public class CaseData : ScriptableObject
    {
        [Header("Schema")]
        [Min(0)] public int schemaVersion = CaseSchemaVersions.Current;

        [Header("Case Info")]
        public string caseId;
        public string title;
        [TextArea(3, 8)] public string briefText;
        public Sprite sceneBackground;

        [Header("World Placement")]
        public DepartmentId departmentId = DepartmentId.Patrol;
        public string districtId;
        public string cityLocationId;
        public string caseArcId;
        [TextArea(2, 5)] public string arcBeatSummary;

        [Header("Hotspots")]
        public List<HotspotData> hotspots = new List<HotspotData>();

        [Header("Case Visits")]
        public List<CaseLocationData> caseLocations = new List<CaseLocationData>();

        [Header("Evidence (4-7 items)")]
        public List<EvidenceData> evidence = new List<EvidenceData>();

        [Header("Claims (3-5 items)")]
        public List<ClaimData> claims = new List<ClaimData>();

        [Header("Suspects")]
        public List<SuspectData> involvedSuspects = new List<SuspectData>();
        public List<CaseSuspectSummary> suspectSummaries = new List<CaseSuspectSummary>();

        [Header("Interrogation")]
        public List<InterrogationNode> interrogationNodes = new List<InterrogationNode>();
        public string interrogationMode;
        public bool isInterrogationForwardCase;
        [TextArea(2, 5)] public string interrogationFocusSummary;

        [Header("Solution")]
        public string contradictoryClaimId;
        [TextArea(3, 8)] public string explanationText;
        [Tooltip("First evidence referenced in explanation")]
        public string primaryEvidenceIdA;
        [Tooltip("Second evidence referenced in explanation")]
        public string primaryEvidenceIdB;

        [Header("Scoring")]
        public int basePoints = 500;
        public float timeLimitSeconds = 0f;  // 0 = no limit, elapsed tracked silently

        [Header("Star Mastery")]
        public ThirdStarRequirementType thirdStarRequirement = ThirdStarRequirementType.AllEvidenceDiscovered;
        [Min(0f)] public float thirdStarParSeconds = 0f;

        [Header("Tool Overrides")]
        public ToolConfig toolConfig = new ToolConfig();

        public float GetThirdStarParSeconds() =>
            thirdStarParSeconds > 0f ? thirdStarParSeconds : timeLimitSeconds;

        public int GetResolvedLocationCount() =>
            caseLocations != null && caseLocations.Count > 0 ? caseLocations.Count : 1;

        public bool HasSuspectPresentation() =>
            (involvedSuspects != null && involvedSuspects.Count > 0)
            || (suspectSummaries != null && suspectSummaries.Count > 0);

        public bool HasInterrogationPresentation() =>
            (interrogationNodes != null && interrogationNodes.Count > 0)
            || isInterrogationForwardCase
            || !string.IsNullOrWhiteSpace(interrogationMode);

        public CaseLocationData GetResolvedLocation(int index = 0)
        {
            if (caseLocations != null && caseLocations.Count > 0)
            {
                int clampedIndex = Mathf.Clamp(index, 0, caseLocations.Count - 1);
                return caseLocations[clampedIndex];
            }

            return new CaseLocationData
            {
                locationId = string.IsNullOrWhiteSpace(cityLocationId) ? caseId : cityLocationId,
                displayName = string.IsNullOrWhiteSpace(title) ? caseId : title,
                sceneBackground = sceneBackground,
                hotspots = CloneHotspots(hotspots),
                entryText = briefText,
                visitOrder = 0,
                isRequiredForSolve = true
            };
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
    }
}
