using System.Collections.Generic;
using UnityEngine;

namespace CasebookGame.Data
{
    [CreateAssetMenu(fileName = "New Case", menuName = "Casebook/Case Data")]
    public class CaseData : ScriptableObject
    {
        [Header("Case Info")]
        public string caseId;
        public string title;
        [TextArea(3, 8)] public string briefText;
        public Sprite sceneBackground;

        [Header("Hotspots")]
        public List<HotspotData> hotspots = new List<HotspotData>();

        [Header("Evidence (4–7 items)")]
        public List<EvidenceData> evidence = new List<EvidenceData>();

        [Header("Claims (3–5 items)")]
        public List<ClaimData> claims = new List<ClaimData>();

        [Header("Solution")]
        public string contradictoryClaimId;
        [TextArea(3, 8)] public string explanationText;
        [Tooltip("First evidence referenced in explanation")]
        public string primaryEvidenceIdA;
        [Tooltip("Second evidence referenced in explanation")]
        public string primaryEvidenceIdB;

        [Header("Tool Overrides")]
        public ToolConfig toolConfig = new ToolConfig();
    }
}
