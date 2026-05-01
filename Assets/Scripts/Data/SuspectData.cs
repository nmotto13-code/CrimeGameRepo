using System.Collections.Generic;
using UnityEngine;

namespace CasebookGame.Data
{
    [CreateAssetMenu(fileName = "New Suspect", menuName = "Casebook/Suspect Data")]
    public class SuspectData : ScriptableObject
    {
        [Header("Schema")]
        [Min(0)] public int schemaVersion = CaseSchemaVersions.Current;

        [Header("Identity")]
        public string suspectId;
        public string displayName;
        public Sprite portraitSprite;

        [Header("Profile")]
        [TextArea(3, 8)] public string bio;
        public List<string> traits = new List<string>();
        public List<SuspectData> knownAssociates = new List<SuspectData>();
        public List<string> linkedCaseIds = new List<string>();
        [Range(0f, 100f)] public float credibilityScore = 50f;
        [TextArea(2, 6)] public string notes;
    }
}
