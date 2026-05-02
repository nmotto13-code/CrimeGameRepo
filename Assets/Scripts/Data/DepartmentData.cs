using System.Collections.Generic;
using UnityEngine;

namespace CasebookGame.Data
{
    [CreateAssetMenu(fileName = "Department", menuName = "Casebook/Department Data")]
    public class DepartmentData : ScriptableObject
    {
        [Header("Identity")]
        public DepartmentId departmentId = DepartmentId.Patrol;
        public string displayName = "Patrol";

        [Header("Unlocks")]
        [Min(1)] public int requiredRank = 1;
        [Min(0)] public int requiredStarsCount = 0;

        [Header("Presentation")]
        [TextArea(2, 5)] public string summaryText;
        [TextArea(1, 3)] public string unlockBlurb;
        public string arcLabel;
        public Sprite mapIcon;
        public Color themeColor = new Color(0.72f, 0.54f, 0.20f, 1f);

        [Header("Roster")]
        public List<string> caseIds = new List<string>();

        public bool ContainsCase(string caseId) =>
            !string.IsNullOrWhiteSpace(caseId) && caseIds != null && caseIds.Contains(caseId);
    }
}
