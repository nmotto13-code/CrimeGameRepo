using System.Collections.Generic;
using UnityEngine;

namespace CasebookGame.Data
{
    [CreateAssetMenu(fileName = "New Interrogation Node", menuName = "Casebook/Interrogation Node")]
    public class InterrogationNode : ScriptableObject
    {
        [Header("Schema")]
        [Min(0)] public int schemaVersion = CaseSchemaVersions.Current;

        [Header("Identity")]
        public string nodeId;
        public string suspectId;

        [Header("Prompt")]
        [TextArea(3, 8)] public string promptText;
        public List<string> responses = new List<string> { string.Empty, string.Empty, string.Empty };
        [Range(0, 2)] public int correctResponseIndex;

        [Header("Unlocking")]
        public List<EvidenceTag> unlockConditionTags = new List<EvidenceTag>();
        public List<string> evidenceRequiredIds = new List<string>();

        [Header("Branching")]
        public string nextNodeIdOnCorrect;
        public string nextNodeIdOnWrong;

        [Header("Rewards")]
        public List<string> grantedEvidenceIds = new List<string>();
        public List<EvidenceTag> grantedTags = new List<EvidenceTag>();
    }
}
