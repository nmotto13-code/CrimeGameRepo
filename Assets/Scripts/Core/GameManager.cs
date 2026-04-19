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
    }
}
