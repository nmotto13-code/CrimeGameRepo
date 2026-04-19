using System.Collections.Generic;
using UnityEngine;
using CasebookGame.Data;

namespace CasebookGame.UI
{
    /// <summary>
    /// Manages 3 board slots. Evidence cards are dragged here to pin them.
    /// Timeline Snap tool reads pinned TIME-tagged evidence from here.
    /// </summary>
    public class BoardController : MonoBehaviour
    {
        public static BoardController Instance { get; private set; }

        [SerializeField] List<BoardSlotUI> slots;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void ClearBoard()
        {
            foreach (var slot in slots) slot.Clear();
        }

        public List<EvidenceData> GetPinnedEvidence()
        {
            var list = new List<EvidenceData>();
            foreach (var slot in slots)
                if (slot.PinnedEvidence != null) list.Add(slot.PinnedEvidence);
            return list;
        }

        public List<EvidenceData> GetTimeTaggedPinned()
        {
            var list = new List<EvidenceData>();
            foreach (var e in GetPinnedEvidence())
                if (e.HasTag(EvidenceTag.TIME)) list.Add(e);
            return list;
        }
    }
}
