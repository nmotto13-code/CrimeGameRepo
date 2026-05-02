using System;
using System.Collections.Generic;

namespace CasebookGame.Data
{
    [Serializable]
    public class CaseProgressConditionData
    {
        public List<string> requiredEvidenceIds = new List<string>();
        public List<EvidenceTag> requiredTags = new List<EvidenceTag>();
        public List<string> requiredVisitedLocationIds = new List<string>();
        public List<string> requiredCompletedLocationIds = new List<string>();
        public List<string> requiredCompletedInterrogationNodeIds = new List<string>();
        public List<string> requiredInterrogationOutcomeIds = new List<string>();
        public List<string> requiredSuspectIds = new List<string>();
        public ConditionMatchMode matchMode = ConditionMatchMode.All;

        public bool IsEmpty =>
            (requiredEvidenceIds == null || requiredEvidenceIds.Count == 0)
            && (requiredTags == null || requiredTags.Count == 0)
            && (requiredVisitedLocationIds == null || requiredVisitedLocationIds.Count == 0)
            && (requiredCompletedLocationIds == null || requiredCompletedLocationIds.Count == 0)
            && (requiredCompletedInterrogationNodeIds == null || requiredCompletedInterrogationNodeIds.Count == 0)
            && (requiredInterrogationOutcomeIds == null || requiredInterrogationOutcomeIds.Count == 0)
            && (requiredSuspectIds == null || requiredSuspectIds.Count == 0);
    }
}
