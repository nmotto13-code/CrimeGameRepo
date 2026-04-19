using System.Collections.Generic;
using UnityEngine;

namespace CasebookGame.Data
{
    [CreateAssetMenu(fileName = "New Claim", menuName = "Casebook/Claim Data")]
    public class ClaimData : ScriptableObject
    {
        [Header("Identity")]
        public string claimId;
        public string speakerName;
        [TextArea(2, 5)] public string claimText;

        [Header("Cross-Check")]
        public List<EvidenceTag> referencedTags = new List<EvidenceTag>();

        [Header("Design")]
        public bool isRedHerring;
    }
}
