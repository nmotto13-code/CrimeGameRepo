using UnityEngine;

namespace CasebookGame.Data
{
    [System.Serializable]
    public class HotspotData
    {
        public string hotspotId;
        [Tooltip("0–1 normalized position on background image")]
        public Vector2 normalizedPosition;
        [Tooltip("Tap radius in normalized space (0–1)")]
        public float radius = 0.06f;
        public string evidenceId;
        public string hotspotLabel;
    }
}
