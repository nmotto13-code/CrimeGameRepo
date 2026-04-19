using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CasebookGame.Data;
using CasebookGame.UI;
using CasebookGame.Tools;

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

            ToolsController.Instance?.InitializeTools(caseData.toolConfig);
            ContradictionEvaluator.Instance?.SetCase(caseData);
            BoardController.Instance?.ClearBoard();
        }

        void SetupBackground(CaseData d)
        {
            if (sceneBackground && d.sceneBackground)
                sceneBackground.sprite = d.sceneBackground;
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

            var layerRect = hotspotLayerRect ? hotspotLayerRect
                          : hotspotParent.GetComponent<RectTransform>();
            if (!layerRect) return;

            foreach (var hotspot in d.hotspots)
            {
                var go = Instantiate(hotspotPrefab, hotspotParent);
                var rt = go.GetComponent<RectTransform>();
                float x = hotspot.normalizedPosition.x * layerRect.rect.width - layerRect.rect.width * 0.5f;
                float y = hotspot.normalizedPosition.y * layerRect.rect.height - layerRect.rect.height * 0.5f;
                rt.anchoredPosition = new Vector2(x, y);
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
