using UnityEngine;
using UnityEngine.EventSystems;
using CasebookGame.Data;
using CasebookGame.UI;

namespace CasebookGame.Core
{
    [RequireComponent(typeof(RectTransform))]
    public class HotspotController : MonoBehaviour, IPointerClickHandler
    {
        HotspotData hotspotData;
        EvidenceData evidenceData;

        [Header("Visual")]
        [SerializeField] GameObject pulseRing;

        public void Initialize(HotspotData data)
        {
            hotspotData = data;
            evidenceData = FindEvidenceById(data.evidenceId);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (evidenceData == null) return;
            EvidenceDetailPanel.Instance?.Show(evidenceData);
        }

        EvidenceData FindEvidenceById(string id)
        {
            var gm = GameManager.Instance;
            if (gm?.CurrentCase == null) return null;
            return gm.CurrentCase.evidence.Find(e => e.evidenceId == id);
        }

        void OnEnable()
        {
            if (pulseRing) pulseRing.SetActive(true);
        }
    }
}
