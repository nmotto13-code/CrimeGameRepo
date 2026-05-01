using System.Collections.Generic;
using UnityEngine;

namespace CasebookGame.Data
{
    [CreateAssetMenu(fileName = "New Evidence", menuName = "Casebook/Evidence Data")]
    public class EvidenceData : ScriptableObject
    {
        [Header("Schema")]
        [Min(0)] public int schemaVersion = CaseSchemaVersions.Current;

        [Header("Identity")]
        public string evidenceId;
        public string displayName;
        [TextArea(2, 5)] public string descriptionText;

        [Header("Visuals")]
        public Sprite imageSprite;
        [Tooltip("Marks this sprite as a temporary placeholder so validators warn instead of erroring.")]
        public bool usesPlaceholderSprite;
        public EvidenceDisplayMode displayMode;

        [Header("Tags")]
        public List<EvidenceTag> tags = new List<EvidenceTag>();

        [Header("Enhance Tool")]
        public Sprite enhanceOverlayMaskSprite;
        public List<EvidenceTag> tagsUnlockedOnEnhance = new List<EvidenceTag>();

        // Runtime state reset on each case load.
        [System.NonSerialized] public bool isEnhanced;
        [System.NonSerialized] public List<EvidenceTag> runtimeTags = new List<EvidenceTag>();

        public void ResetRuntimeState()
        {
            isEnhanced = false;
            runtimeTags = new List<EvidenceTag>(tags);
        }

        public void ApplyEnhance()
        {
            if (isEnhanced)
                return;

            isEnhanced = true;
            foreach (var tag in tagsUnlockedOnEnhance)
            {
                if (!runtimeTags.Contains(tag))
                    runtimeTags.Add(tag);
            }
        }

        public bool HasTag(EvidenceTag tag) => runtimeTags.Contains(tag);
    }
}
