using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using CasebookGame.Data;
using CasebookGame.UI;

namespace CasebookGame.Core
{
    [RequireComponent(typeof(RectTransform))]
    public class HotspotController : MonoBehaviour, IPointerClickHandler
    {
        HotspotData  hotspotData;
        EvidenceData evidenceData;
        bool         isDiscovered = false;

        [Header("Visuals")]
        [SerializeField] GameObject pulseRing;       // hidden — hotspots are invisible until found
        [SerializeField] GameObject discoveredBadge; // shown after discovery
        [SerializeField] Image      pulseImage;
        [SerializeField] float      pulseSpeed = 1.5f;

        Coroutine pulseRoutine;

        public bool IsDiscovered => isDiscovered;

        public void Initialize(HotspotData data)
        {
            hotspotData  = data;
            evidenceData = FindEvidenceById(data.evidenceId);
            SetDiscovered(false);
        }

        void OnEnable()
        {
            // Panel was inactive during Initialize — pulse restarts when it activates.
            if (!isDiscovered && pulseRoutine == null && pulseImage != null)
                pulseRoutine = StartCoroutine(PulseLoop());
        }

        void OnDisable()
        {
            if (pulseRoutine != null) { StopCoroutine(pulseRoutine); pulseRoutine = null; }
        }

        void SetDiscovered(bool discovered)
        {
            isDiscovered = discovered;
            if (pulseRing)       pulseRing.SetActive(false);        // always hidden — glass mechanic
            if (discoveredBadge) discoveredBadge.SetActive(discovered);

            if (pulseRoutine != null) { StopCoroutine(pulseRoutine); pulseRoutine = null; }
        }

        // Called by MagnifyingGlass when the glass hovers long enough.
        public void TriggerDiscovery()
        {
            if (isDiscovered) return;

            if (evidenceData == null && hotspotData != null)
                evidenceData = FindEvidenceById(hotspotData.evidenceId);

            if (evidenceData == null)
            {
                Debug.LogWarning($"[Hotspot] No evidence for id '{hotspotData?.evidenceId}'. " +
                                 "Run Casebook → Generate Starter Cases.");
                return;
            }

            SetDiscovered(true);
            EvidenceDiscoverySystem.Instance?.RegisterEvidenceFound(evidenceData);
            EvidenceDetailPanel.Instance?.Show(evidenceData);
        }

        // IPointerClickHandler — tap fallback (handy in the editor; glass is the primary mechanic).
        public void OnPointerClick(PointerEventData eventData)
        {
            TriggerDiscovery();
        }

        IEnumerator PulseLoop()
        {
            if (!pulseImage) yield break;
            float t = 0f;
            while (true)
            {
                t += Time.deltaTime * pulseSpeed;
                var c = pulseImage.color;
                c.a = Mathf.Lerp(0.3f, 0.9f, Mathf.PingPong(t, 1f));
                pulseImage.color = c;
                yield return null;
            }
        }

        EvidenceData FindEvidenceById(string id)
        {
            var gm = GameManager.Instance;
            if (gm?.CurrentCase == null) return null;
            return gm.CurrentCase.evidence.Find(e => e.evidenceId == id);
        }
    }
}
