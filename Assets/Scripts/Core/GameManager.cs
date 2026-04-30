using System.Linq;
using UnityEngine;
using CasebookGame.Data;

namespace CasebookGame.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Case Roster")]
        public CaseData[] availableCases;
        public int currentCaseIndex = 0;

        public CaseData CurrentCase =>
            availableCases != null && availableCases.Length > 0
                ? availableCases[currentCaseIndex]
                : null;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            if (availableCases == null || availableCases.Length == 0)
                availableCases = Resources.LoadAll<CaseData>("Cases");

            SortAvailableCases();
            LoadCurrentCase();
        }

        public void LoadCurrentCase() => CaseLoader.Instance?.LoadCase(CurrentCase);

        public void NextCase()
        {
            currentCaseIndex = (currentCaseIndex + 1) % availableCases.Length;
            LoadCurrentCase();
        }

        public void RetryCase() => LoadCurrentCase();

        public void LoadCaseByIndex(int index)
        {
            if (index < 0 || index >= availableCases.Length) return;
            currentCaseIndex = index;
            LoadCurrentCase();
        }

        public int IndexOfCase(string caseId)
        {
            if (availableCases == null || string.IsNullOrWhiteSpace(caseId))
                return -1;

            for (int i = 0; i < availableCases.Length; i++)
            {
                if (availableCases[i] != null && availableCases[i].caseId == caseId)
                    return i;
            }

            return -1;
        }

        void SortAvailableCases()
        {
            if (availableCases == null)
                return;

            availableCases = availableCases
                .Where(c => c != null)
                .OrderBy(c => c.caseId)
                .ToArray();

            currentCaseIndex = Mathf.Clamp(currentCaseIndex, 0, Mathf.Max(0, availableCases.Length - 1));
        }
    }
}
