using UnityEngine;

namespace CasebookGame.Data
{
    [System.Serializable]
    public class ToolConfig
    {
        [Header("Cross-Check")]
        public int crossCheckCharges = 2;

        [Header("Enhance")]
        public float enhanceCooldownSeconds = 12f;

        [Header("Timeline Snap")]
        public int timelineSnapCharges = 1;
    }
}
