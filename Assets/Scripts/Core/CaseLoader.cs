using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CasebookGame.Data;
using CasebookGame.UI;

namespace CasebookGame.Core
{
    public class CaseLoader : MonoBehaviour
    {
        public static CaseLoader Instance { get; private set; }

        [Header("Background")]
        [SerializeField] Image sceneBackground;

        [Header("Brief")]
        [SerializeField] TMP_Text caseTitleText;
        [SerializeField] TMP_Text briefText;

        [Header("Evidence List")]
        [SerializeField] Transform evidenceListParent;
        [SerializeField] GameObject evidenceCardPrefab;

        [Header("Claims List")]
        [SerializeField] Transform claimsListParent;
        [SerializeField] GameObject claimCardPrefab;

        [Header("Hotspot Layer")]
        [SerializeField] Transform hotspotParent;
        [SerializeField] RectTransform hotspotLayerRect;
        [SerializeField] GameObject hotspotPrefab;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void LoadCase(CaseData caseData)
        {
            if (caseData == null) return;

            foreach (var e in caseData.evidence)
                e.ResetRuntimeState();

            SetupBackground(caseData);
            SetupBrief(caseData);
            SetupEvidence(caseData);
            SetupClaims(caseData);
            SetupHotspots(caseData);
            EvidenceDiscoverySystem.Instance?.StartInvestigation(caseData);

            ContradictionEvaluator.Instance?.SetCase(caseData);
            BoardController.Instance?.ClearBoard();
            ScoringSystem.Instance?.StartCase(caseData);  // must be last — timer starts here

            // Force immediate layout rebuild so claim cards appear on first rendered frame.
            Canvas.ForceUpdateCanvases();
            if (claimsListParent is RectTransform cRT)
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(cRT);
        }

        void SetupBackground(CaseData d)
        {
            if (!sceneBackground) return;
            if (d.sceneBackground)
            {
                sceneBackground.sprite = d.sceneBackground;
                sceneBackground.color  = Color.white;
                // Hide the "no image" placeholder text when a real sprite is present
                var placeholder = sceneBackground.transform.Find("NoImagePlaceholder");
                if (placeholder) placeholder.gameObject.SetActive(false);
            }
        }

        void SetupBrief(CaseData d)
        {
            if (caseTitleText) caseTitleText.text = d.title;
            if (briefText) briefText.text = d.briefText;
        }

        void SetupEvidence(CaseData d)
        {
            if (!evidenceListParent || !evidenceCardPrefab) return;
            ClearChildren(evidenceListParent);
            foreach (var evidence in d.evidence)
            {
                var card = Instantiate(evidenceCardPrefab, evidenceListParent);
                card.GetComponent<EvidenceCardUI>()?.Initialize(evidence);
            }
        }

        void SetupClaims(CaseData d)
        {
            if (!claimsListParent || !claimCardPrefab) return;
            ClearChildren(claimsListParent);
            foreach (var claim in d.claims)
            {
                var card = Instantiate(claimCardPrefab, claimsListParent);
                card.GetComponent<ClaimCardUI>()?.Initialize(claim);
            }
        }

        void SetupHotspots(CaseData d)
        {
            if (!hotspotParent || !hotspotPrefab) return;
            ClearChildren(hotspotParent);

            foreach (var hotspot in d.hotspots)
            {
                var go = Instantiate(hotspotPrefab, hotspotParent);
                var rt = go.GetComponent<RectTransform>();

                // Use anchors so position is correct even when the parent panel is inactive
                // (inactive RectTransforms report rect.width/height = 0, breaking pixel math).
                rt.anchorMin = hotspot.normalizedPosition;
                rt.anchorMax = hotspot.normalizedPosition;
                rt.pivot     = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;

                go.GetComponent<HotspotController>()?.Initialize(hotspot);
            }
        }

        static void ClearChildren(Transform parent)
        {
            foreach (Transform child in parent)
                Destroy(child.gameObject);
        }
    }
}
