using UnityEngine;

namespace CasebookGame.Data
{
    [CreateAssetMenu(fileName = "District", menuName = "Casebook/District Data")]
    public class DistrictData : ScriptableObject
    {
        public string districtId;
        public string displayName = "District";
        public Sprite mapIcon;
        public Color accentColor = new Color(0.72f, 0.54f, 0.20f, 1f);
        [Min(0)] public int sortOrder;
        [Min(1)] public int requiredRank = 1;
        [Min(0)] public int requiredStarsCount = 0;
    }
}
