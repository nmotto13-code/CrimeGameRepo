using System;
using System.Collections.Generic;
using UnityEngine;

namespace CasebookGame.Data
{
    [Serializable]
    public class InterrogationOutcomeData
    {
        public string outcomeId;
        public string displayLabel;
        [TextArea(2, 5)] public string summaryText;
        public List<string> unlockLocationIds = new List<string>();
        public List<string> lockLocationIds = new List<string>();
        public List<string> revealSuspectIds = new List<string>();
        public List<string> hideSuspectIds = new List<string>();
        public List<string> grantEvidenceIds = new List<string>();
        public List<EvidenceTag> grantTags = new List<EvidenceTag>();
        public bool markCaseReadyForSolve;
        public string redirectToLocationId;
    }
}
