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
        public bool isRequiredForSolve = true;
    }
}
