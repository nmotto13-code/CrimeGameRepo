using System;
using System.Collections.Generic;
using UnityEngine;

namespace CasebookGame.Data
{
    [Serializable]
    public class CaseLocationData
    {
        public string locationId;
        public string displayName;
        public Sprite sceneBackground;
        public List<HotspotData> hotspots = new List<HotspotData>();
        [TextArea(2, 5)] public string entryText;
        [Min(0)] public int visitOrder;
        public List<string> unlockEvidenceIds = new List<string>();
        public List<EvidenceTag> unlockTags = new List<EvidenceTag>();
        public CaseProgressConditionData unlockCondition = new CaseProgressConditionData();
        public List<string> nextLocationIds = new List<string>();
        public LocationRevisitRule revisitRule = LocationRevisitRule.Always;
        public List<LocationSuspectPresenceData> presentSuspects = new List<LocationSuspectPresenceData>();
        public bool autoCompleteOnEnter;
        public string completionOutcomeId;
        public bool autoUnlocksSolve;
        public bool isRequiredForSolve = true;
    }
}
