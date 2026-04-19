using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace CasebookGame.UI
{
    /// <summary>
    /// Manages swipe-left/right and tap-to-switch navigation between
    /// Brief / Evidence / Claims / Board tabs.
    /// </summary>
    public class TabController : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public static TabController Instance { get; private set; }

        [Header("Panels (Brief=0, Evidence=1, Claims=2, Board=3)")]
        [SerializeField] List<GameObject> tabPanels;

        [Header("Tab Buttons")]
        [SerializeField] List<Button> tabButtons;
        [SerializeField] Color activeTabColor = Color.white;
        [SerializeField] Color inactiveTabColor = new Color(0.6f, 0.6f, 0.6f);

        [Header("Swipe Config")]
        [SerializeField] float swipeThreshold = 50f;

        int currentTab = 0;
        Vector2 dragStart;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            for (int i = 0; i < tabButtons.Count; i++)
            {
                int idx = i;
                tabButtons[i].onClick.AddListener(() => SwitchToTab(idx));
            }
            SwitchToTab(0);
        }

        public void SwitchToTab(int index)
        {
            if (index < 0 || index >= tabPanels.Count) return;
            currentTab = index;

            for (int i = 0; i < tabPanels.Count; i++)
                tabPanels[i].SetActive(i == currentTab);

            for (int i = 0; i < tabButtons.Count; i++)
            {
                var img = tabButtons[i].GetComponent<Image>();
                if (img) img.color = i == currentTab ? activeTabColor : inactiveTabColor;

                var lbl = tabButtons[i].GetComponentInChildren<TMP_Text>();
                if (lbl) lbl.fontStyle = i == currentTab ? FontStyles.Bold : FontStyles.Normal;
            }
        }

        public void OnBeginDrag(PointerEventData e) => dragStart = e.position;

        public void OnDrag(PointerEventData e) { }

        public void OnEndDrag(PointerEventData e)
        {
            float delta = e.position.x - dragStart.x;
            if (Mathf.Abs(delta) < swipeThreshold) return;

            if (delta < 0) SwitchToTab(currentTab + 1);
            else SwitchToTab(currentTab - 1);
        }

        public int CurrentTab => currentTab;
    }
}
