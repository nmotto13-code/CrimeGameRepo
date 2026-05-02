using System;

namespace CasebookGame.Data
{
    [Serializable]
    public class LocationSuspectPresenceData
    {
        public string suspectId;
        public string presenceLabel;
        public bool isVisibleOnEntry = true;
        public CaseProgressConditionData availabilityCondition = new CaseProgressConditionData();
        public string interrogationEntryNodeId;
        public string departureOutcomeId;
        public string notes;
    }
}
