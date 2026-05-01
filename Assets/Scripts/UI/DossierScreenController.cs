using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CasebookGame.Core;
using CasebookGame.Data;

namespace CasebookGame.UI
{
    public class DossierScreenController : BaseScreen
    {
        [SerializeField] Transform listParent;
        [SerializeField] Button closeBtn;
        [SerializeField] TMP_Text emptyStateText;

        readonly Color cardColor = new Color(0.12f, 0.12f, 0.18f, 1f);
        readonly Color headerColor = new Color(0.18f, 0.18f, 0.26f, 1f);
        readonly Color detailColor = new Color(0.10f, 0.10f, 0.16f, 1f);
        readonly Color accentColor = new Color(0.90f, 0.72f, 0.28f, 1f);
        readonly Color portraitFallbackColor = new Color(0.22f, 0.24f, 0.32f, 1f);

        public override ScreenId ScreenId => ScreenId.Dossier;

        protected override void Awake()
        {
            base.Awake();
            closeBtn?.onClick.AddListener(() => NavigationManager.Instance?.Pop(TransitionType.SlideRight));
        }

        public override void OnScreenEnter()
        {
            gameObject.SetActive(true);
            Populate();
        }

        void Populate()
        {
            if (listParent == null)
                return;

            foreach (Transform child in listParent)
                Destroy(child.gameObject);

            var suspects = GameManager.Instance?.CurrentCase?.involvedSuspects
                ?.Where(s => s != null)
                .ToList() ?? new List<SuspectData>();

            if (emptyStateText != null)
                emptyStateText.gameObject.SetActive(suspects.Count == 0);

            if (suspects.Count == 0)
                return;

            foreach (var suspect in suspects)
                CreateSuspectCard(suspect);

            Canvas.ForceUpdateCanvases();
            if (listParent is RectTransform rectTransform)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }

        void CreateSuspectCard(SuspectData suspect)
        {
            var card = new GameObject($"Suspect_{suspect.suspectId}");
            card.transform.SetParent(listParent, false);
            card.AddComponent<RectTransform>();
            card.AddComponent<Image>().color = cardColor;
            card.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var layout = card.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 0;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            var header = new GameObject("Header");
            header.transform.SetParent(card.transform, false);
            header.AddComponent<RectTransform>();
            var headerImage = header.AddComponent<Image>();
            headerImage.color = headerColor;
            var headerButton = header.AddComponent<Button>();
            headerButton.targetGraphic = headerImage;
            header.AddComponent<LayoutElement>().preferredHeight = 220;

            var headerLayout = header.AddComponent<HorizontalLayoutGroup>();
            headerLayout.padding = new RectOffset(18, 18, 18, 18);
            headerLayout.spacing = 16;
            headerLayout.childControlHeight = true;
            headerLayout.childControlWidth = true;
            headerLayout.childForceExpandHeight = true;
            headerLayout.childForceExpandWidth = false;

            var portraitFrame = new GameObject("PortraitFrame");
            portraitFrame.transform.SetParent(header.transform, false);
            portraitFrame.AddComponent<RectTransform>();
            portraitFrame.AddComponent<Image>().color = portraitFallbackColor;
            portraitFrame.AddComponent<LayoutElement>().preferredWidth = 120;

            var portrait = new GameObject("Portrait");
            portrait.transform.SetParent(portraitFrame.transform, false);
            var portraitRT = portrait.AddComponent<RectTransform>();
            portraitRT.anchorMin = Vector2.zero;
            portraitRT.anchorMax = Vector2.one;
            portraitRT.offsetMin = new Vector2(8, 8);
            portraitRT.offsetMax = new Vector2(-8, -8);
            var portraitImage = portrait.AddComponent<Image>();
            portraitImage.preserveAspect = true;
            portraitImage.color = Color.white;
            portraitImage.sprite = suspect.portraitSprite;

            if (suspect.portraitSprite == null)
            {
                var initials = new GameObject("Initials");
                initials.transform.SetParent(portraitFrame.transform, false);
                var initialsText = initials.AddComponent<TextMeshProUGUI>();
                initialsText.text = BuildInitials(suspect.displayName);
                initialsText.fontSize = 34;
                initialsText.fontStyle = FontStyles.Bold;
                initialsText.alignment = TextAlignmentOptions.Center;
                initialsText.color = accentColor;
                var initialsRT = initials.GetComponent<RectTransform>();
                initialsRT.anchorMin = Vector2.zero;
                initialsRT.anchorMax = Vector2.one;
                initialsRT.offsetMin = Vector2.zero;
                initialsRT.offsetMax = Vector2.zero;
            }

            var summary = new GameObject("Summary");
            summary.transform.SetParent(header.transform, false);
            summary.AddComponent<RectTransform>();
            summary.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var summaryLayout = summary.AddComponent<VerticalLayoutGroup>();
            summaryLayout.spacing = 8;
            summaryLayout.childControlHeight = true;
            summaryLayout.childControlWidth = true;
            summaryLayout.childForceExpandHeight = false;
            summaryLayout.childForceExpandWidth = true;

            var nameText = CreateText(summary, "Name", suspect.displayName, 32, FontStyles.Bold, Color.white);
            nameText.gameObject.AddComponent<LayoutElement>().preferredHeight = 46;

            var credibilityText = CreateText(summary, "Credibility",
                $"Credibility {Mathf.RoundToInt(suspect.credibilityScore)}/100", 20, FontStyles.Bold, accentColor);
            credibilityText.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;

            var bioText = CreateText(summary, "Bio", suspect.bio, 22, FontStyles.Normal, new Color(0.84f, 0.84f, 0.90f));
            bioText.textWrappingMode = TextWrappingModes.Normal;
            bioText.overflowMode = TextOverflowModes.Ellipsis;
            bioText.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

            var tapHint = CreateText(summary, "TapHint", "Tap to expand traits and associates", 18,
                FontStyles.Italic, new Color(0.64f, 0.64f, 0.72f));
            tapHint.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;

            var details = new GameObject("Details");
            details.transform.SetParent(card.transform, false);
            details.AddComponent<RectTransform>();
            details.AddComponent<Image>().color = detailColor;
            details.SetActive(false);

            var detailsLayout = details.AddComponent<VerticalLayoutGroup>();
            detailsLayout.padding = new RectOffset(18, 18, 16, 18);
            detailsLayout.spacing = 10;
            detailsLayout.childControlHeight = true;
            detailsLayout.childControlWidth = true;
            detailsLayout.childForceExpandHeight = false;
            detailsLayout.childForceExpandWidth = true;

            CreateText(details, "TraitsLabel", "Traits", 18, FontStyles.Bold, accentColor);
            CreateText(details, "TraitsValue", BuildListText(suspect.traits), 20, FontStyles.Normal, Color.white)
                .textWrappingMode = TextWrappingModes.Normal;

            CreateText(details, "AssociatesLabel", "Known Associates", 18, FontStyles.Bold, accentColor);
            CreateText(details, "AssociatesValue", BuildAssociatesText(suspect.knownAssociates), 20, FontStyles.Normal, Color.white)
                .textWrappingMode = TextWrappingModes.Normal;

            CreateText(details, "LinkedCasesLabel", "Linked Cases", 18, FontStyles.Bold, accentColor);
            CreateText(details, "LinkedCasesValue", BuildListText(suspect.linkedCaseIds), 20, FontStyles.Normal, Color.white)
                .textWrappingMode = TextWrappingModes.Normal;

            CreateText(details, "NotesLabel", "Notes", 18, FontStyles.Bold, accentColor);
            var notesText = CreateText(details, "NotesValue", string.IsNullOrWhiteSpace(suspect.notes) ? "No notes recorded." : suspect.notes,
                20, FontStyles.Normal, Color.white);
            notesText.textWrappingMode = TextWrappingModes.Normal;

            headerButton.onClick.AddListener(() =>
            {
                details.SetActive(!details.activeSelf);
                if (listParent is RectTransform rectTransform)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
            });
        }

        static TextMeshProUGUI CreateText(GameObject parent, string name, string value, float size,
            FontStyles style, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAlignmentOptions.TopLeft;
            return text;
        }

        static string BuildInitials(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "?";

            var parts = value.Split(' ');
            string initials = string.Concat(parts.Where(part => part.Length > 0).Take(2).Select(part => char.ToUpperInvariant(part[0])));
            return string.IsNullOrWhiteSpace(initials) ? "?" : initials;
        }

        static string BuildListText(IEnumerable<string> values)
        {
            var items = values?.Where(value => !string.IsNullOrWhiteSpace(value)).ToList() ?? new List<string>();
            return items.Count == 0 ? "None noted." : string.Join(" | ", items);
        }

        static string BuildAssociatesText(IEnumerable<SuspectData> associates)
        {
            var names = associates?
                .Where(associate => associate != null)
                .Select(associate => string.IsNullOrWhiteSpace(associate.displayName) ? associate.suspectId : associate.displayName);
            return BuildListText(names);
        }
    }
}
