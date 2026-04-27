using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CasebookGame.UI
{
    public class ConfirmDialog : MonoBehaviour
    {
        [SerializeField] TMP_Text messageText;
        [SerializeField] Button   confirmBtn;
        [SerializeField] Button   cancelBtn;

        public bool IsShowing { get; private set; }

        Action _onConfirm;
        Action _onCancel;

        void Awake()
        {
            confirmBtn.onClick.AddListener(OnConfirm);
            cancelBtn.onClick.AddListener(OnCancel);
            gameObject.SetActive(false);
        }

        public void Show(string message, Action onConfirm, Action onCancel = null)
        {
            messageText.text = message;
            _onConfirm       = onConfirm;
            _onCancel        = onCancel;
            IsShowing        = true;
            gameObject.SetActive(true);
        }

        public void Dismiss()
        {
            OnCancel();
        }

        void OnConfirm()
        {
            IsShowing = false;
            gameObject.SetActive(false);
            var cb = _onConfirm;
            _onConfirm = null;
            _onCancel  = null;
            cb?.Invoke();
        }

        void OnCancel()
        {
            IsShowing = false;
            gameObject.SetActive(false);
            var cb = _onCancel;
            _onConfirm = null;
            _onCancel  = null;
            cb?.Invoke();
        }
    }
}
