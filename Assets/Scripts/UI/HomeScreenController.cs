using UnityEngine;
using UnityEngine.UI;
using CasebookGame.Core;

namespace CasebookGame.UI
{
    public class HomeScreenController : MonoBehaviour
    {
        [SerializeField] GameObject homePanel;
        [SerializeField] GameObject caseSelectPanel;
        [SerializeField] GameObject accountPanel;

        [SerializeField] Button selectCaseBtn;
        [SerializeField] Button viewProfileBtn;

        void Start()
        {
            selectCaseBtn.onClick.AddListener(OpenCaseSelect);
            viewProfileBtn.onClick.AddListener(OpenAccount);
        }

        void OpenCaseSelect()
        {
            caseSelectPanel.SetActive(true);
            GetComponent<CaseSelectController>()?.Populate();
        }

        void OpenAccount()
        {
            accountPanel.SetActive(true);
            GetComponentInChildren<AccountScreenController>(true)?.Refresh();
        }

        public void EnterGame()
        {
            homePanel.SetActive(false);
        }
    }
}
