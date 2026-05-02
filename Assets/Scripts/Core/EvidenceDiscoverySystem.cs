using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CasebookGame.Data;
using CasebookGame.UI;

namespace CasebookGame.Core
{
    public class EvidenceDiscoverySystem : MonoBehaviour
    {
        public static EvidenceDiscoverySystem Instance { get; private set; }

        [Header("Counter Bar")]
        [SerializeField] TMP_Text   foundCounterText;
        [SerializeField] GameObject analyseButton;
        [SerializeField] GameObject investigationOverlay;
        [SerializeField] TMP_Text   investigationHintText;

        [Header("Evidence Tab")]
        [SerializeField] Transform  evidenceTabParent;
        [SerializeField] GameObject evidenceTabCardPrefab;

        int totalEvidence;
        int foundCount;

        readonly List<HotspotController> activeHotspots = new();
        readonly HashSet<string> foundEvidenceIds = new();
        readonly HashSet<EvidenceTag> grantedTags = new();
        readonly List<EvidenceData> foundEvidence = new();

        public IReadOnlyList<HotspotController> ActiveHotspots => activeHotspots;
        public IReadOnlyList<EvidenceData> FoundEvidence => foundEvidence;

        public System.Action<EvidenceData> OnEvidenceFound;
        public System.Action               OnAllEvidenceFound;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            var btn = analyseButton?.GetComponent<Button>();
            if (btn) btn.onClick.AddListener(OnAnalyseTapped);
        }

        void OnAnalyseTapped() => ContradictionEvaluator.Instance?.BeginSubmit();

        public void StartInvestigation(CaseData caseData)
        {
            totalEvidence = caseData.evidence.Count;
            foundCount    = 0;
            foundEvidenceIds.Clear();
            foundEvidence.Clear();
            grantedTags.Clear();

            // Clear previous evidence tab cards
            if (evidenceTabParent)
                foreach (Transform child in evidenceTabParent)
                    Destroy(child.gameObject);

            RefreshCounter();
            if (investigationOverlay) investigationOverlay.SetActive(true);
            if (analyseButton)        analyseButton.SetActive(false);
            if (investigationHintText)
                investigationHintText.text = $"Find all {totalEvidence} clues";

            activeHotspots.Clear();
            var hotspots = FindObjectsByType<HotspotController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var h in hotspots)
                activeHotspots.Add(h);
        }

        public void RegisterEvidenceFound(EvidenceData evidence)
        {
            if (evidence == null)
                return;

            if (!string.IsNullOrWhiteSpace(evidence.evidenceId) && foundEvidenceIds.Contains(evidence.evidenceId))
                return;

            if (!string.IsNullOrWhiteSpace(evidence.evidenceId))
                foundEvidenceIds.Add(evidence.evidenceId);
            foundEvidence.Add(evidence);

            foundCount++;
            RefreshCounter();

            // Add to Evidence tab list
            if (evidenceTabParent && evidenceTabCardPrefab)
            {
                var tabCard = Instantiate(evidenceTabCardPrefab, evidenceTabParent);
                tabCard.GetComponent<EvidenceCardUI>()?.Initialize(evidence);
            }

            OnEvidenceFound?.Invoke(evidence);

            if (foundCount >= 1 && analyseButton)
                analyseButton.SetActive(true);

            if (foundCount >= totalEvidence)
            {
                if (investigationHintText)
                    investigationHintText.text = "All clues found — tap ANALYSE";
                OnAllEvidenceFound?.Invoke();
            }
        }

        void RefreshCounter()
        {
            if (foundCounterText)
                foundCounterText.text = $"FOUND  {foundCount} / {totalEvidence}";
        }

        public bool IsAllFound  => foundCount >= totalEvidence;
        public int  FoundCount  => foundCount;
        public int  TotalCount  => totalEvidence;

        public bool HasFoundEvidence(string evidenceId) =>
            !string.IsNullOrWhiteSpace(evidenceId) && foundEvidenceIds.Contains(evidenceId);

        public bool HasFoundTag(EvidenceTag tag)
        {
            if (grantedTags.Contains(tag))
                return true;

            foreach (var evidence in foundEvidence)
            {
                if (evidence != null && evidence.HasTag(tag))
                    return true;
            }

            return false;
        }

        public void GrantEvidenceById(string evidenceId)
        {
            if (string.IsNullOrWhiteSpace(evidenceId))
                return;

            var currentCase = GameManager.Instance?.CurrentCase;
            if (currentCase?.evidence == null)
                return;

            foreach (var evidence in currentCase.evidence)
            {
                if (evidence != null && evidence.evidenceId == evidenceId)
                {
                    RegisterEvidenceFound(evidence);
                    return;
                }
            }
        }

        public void GrantTag(EvidenceTag tag) => grantedTags.Add(tag);
    }
}
