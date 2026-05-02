using UnityEngine;

namespace CasebookGame.Data
{
    [CreateAssetMenu(fileName = "CityLocation", menuName = "Casebook/City Location Data")]
    public class CityLocationData : ScriptableObject
    {
        public string locationId;
        public string districtId;
        public string displayName = "Location";
        public Vector2 mapPosition = new Vector2(0.5f, 0.5f);
        public Sprite nodeIcon;
        public Sprite defaultBackground;
    }
}
