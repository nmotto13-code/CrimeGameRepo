using System.Collections.Generic;
using UnityEngine;

namespace CasebookGame.Data
{
    [CreateAssetMenu(fileName = "Department", menuName = "Casebook/Department Data")]
    public class DepartmentData : ScriptableObject
    {
        public DepartmentId departmentId = DepartmentId.Patrol;
        public string displayName = "Patrol";
        [Min(1)] public int requiredRank = 1;
        [Min(0)] public int requiredStarsCount = 0;
        public List<string> caseIds = new List<string>();

        public bool ContainsCase(string caseId) =>
            !string.IsNullOrWhiteSpace(caseId) && caseIds != null && caseIds.Contains(caseId);
    }
}
