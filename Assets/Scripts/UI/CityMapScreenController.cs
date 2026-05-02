using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CasebookGame.Core;
using CasebookGame.Data;

namespace CasebookGame.UI
{
    public class CityMapScreenController : BaseScreen
    {
        [SerializeField] RectTransform mapRoot;
        [SerializeField] Image mapBackgroundImage;
        [SerializeField] Transform legendParent;
        [SerializeField] TMP_Text summaryText;
        [SerializeField] TMP_Text emptyStateText;
        [SerializeField] TMP_Text selectionDistrictText;
        [SerializeField] TMP_Text selectionLocationText;
        [SerializeField] TMP_Text selectionCaseText;
        [SerializeField] TMP_Text selectionBodyText;
        [SerializeField] TMP_Text selectionStatusText;
        [SerializeField] Image selectionIconImage;
        [SerializeField] Button launchCaseButton;
        [SerializeField] Button closeBtn;
        [SerializeField] Button departmentDeskBtn;

        readonly Color unlockedNodeTint = new Color(1f, 1f, 1f, 0.98f);
        readonly Color lockedNodeTint = new Color(0.55f, 0.57f, 0.64f, 0.86f);
        readonly Color selectedOutlineColor = new Color(0.95f, 0.74f, 0.22f, 1f);
        readonly Color defaultOutlineColor = new Color(0.14f, 0.16f, 0.20f, 0.88f);
        readonly Color lockedOutlineColor = new Color(0.32f, 0.22f, 0.24f, 0.88f);
        readonly Color legendTextColor = new Color(0.80f, 0.82f, 0.88f, 1f);

        readonly List<MapNodeInfo> nodeInfos = new();
        MapNodeInfo selectedNode;

        public override ScreenId ScreenId => ScreenId.CityMap;

        protected override void Awake()
        {
            base.Awake();

            closeBtn?.onClick.AddListener(() => NavigationManager.Instance?.Pop(TransitionType.SlideRight));
            departmentDeskBtn?.onClick.AddListener(() =>
            {
                NavigationManager.Instance?.Pop(TransitionType.SlideRight);
                NavigationManager.Instance?.Push(ScreenId.CaseSelect, TransitionType.SlideLeft);
            });
            launchCaseButton?.onClick.AddListener(LaunchSelectedCase);
        }

        public override void OnScreenEnter()
        {
            gameObject.SetActive(true);
            Populate();
        }

        void Populate()
        {
            if (mapRoot == null)
                return;

            nodeInfos.Clear();
            selectedNode = null;

            ClearDynamicMapChildren();
            ClearChildren(legendParent);

            if (mapBackgroundImage != null)
            {
                mapBackgroundImage.gameObject.SetActive(true);
                mapBackgroundImage.preserveAspect = true;
            }

            var cases = GameManager.Instance?.availableCases ?? Array.Empty<CaseData>();
            var departments = ProgressionRules.GetDepartments(cases);
            var districtAssets = Resources.LoadAll<DistrictData>("Districts")
                .Where(asset => asset != null && !string.IsNullOrWhiteSpace(asset.districtId))
                .ToDictionary(asset => asset.districtId, asset => asset, StringComparer.OrdinalIgnoreCase);
            var locationAssets = Resources.LoadAll<CityLocationData>("CityLocations")
                .Where(asset => asset != null && !string.IsNullOrWhiteSpace(asset.locationId))
                .ToDictionary(asset => asset.locationId, asset => asset, StringComparer.OrdinalIgnoreCase);

            int rank = PlayerProfile.GetRank();
            int stars = PlayerProfile.GetTotalStars(cases);
            var dailyCase = PlayerProfile.GetDailyCaseInfo(cases, departments);

            for (int i = 0; i < cases.Length; i++)
            {
                var caseData = cases[i];
                if (caseData == null)
                    continue;

                var department = ProgressionRules.GetDepartmentForCase(caseData.caseId, departments);
                var departmentLock = ProgressionRules.GetDepartmentLockStatus(department, rank, stars);
                var district = ResolveDistrict(caseData, districtAssets);
                var location = ResolveLocation(caseData, locationAssets);
                nodeInfos.Add(new MapNodeInfo
                {
                    Case = caseData,
                    CaseIndex = i,
                    Department = department,
                    District = district,
                    Location = location,
                    IsUnlocked = departmentLock.IsUnlocked,
                    LockReason = BuildLockReason(departmentLock),
                    IsDailyCase = string.Equals(dailyCase.CaseId, caseData.caseId, StringComparison.OrdinalIgnoreCase),
                    Stars = PlayerProfile.GetCaseStars(caseData.caseId)
                });
            }

            if (emptyStateText != null)
                emptyStateText.gameObject.SetActive(nodeInfos.Count == 0);

            if (nodeInfos.Count == 0)
            {
                UpdateSelectionPanel(null);
                return;
            }

            foreach (var districtGroup in nodeInfos
                .Where(info => info.Location != null)
                .GroupBy(info => info.District?.districtId ?? info.Case.districtId)
                .OrderBy(group => group.First().District?.sortOrder ?? int.MaxValue))
            {
                CreateDistrictMarker(districtGroup.ToList());
                CreateLegendRow(districtGroup.ToList());
            }

            foreach (var nodeInfo in nodeInfos.OrderBy(info => info.Location?.mapPosition.y ?? 0f))
                CreateNode(nodeInfo);

            selectedNode = nodeInfos
                .FirstOrDefault(info => info.IsDailyCase && info.IsUnlocked)
                ?? nodeInfos.FirstOrDefault(info => info.IsUnlocked)
                ?? nodeInfos[0];

            SyncNodeSelectionVisuals();
            UpdateSelectionPanel(selectedNode);

            if (summaryText != null)
            {
                int unlockedCount = nodeInfos.Count(info => info.IsUnlocked);
                int districtCount = nodeInfos
                    .Select(info => info.District?.districtId ?? info.Case.districtId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();
                summaryText.text = $"Rank {rank} | {stars} stars | {unlockedCount}/{nodeInfos.Count} locations open across {districtCount} districts";
            }
        }

        void CreateNode(MapNodeInfo nodeInfo)
        {
            var buttonGo = new GameObject($"Node_{nodeInfo.Case.caseId}");
            buttonGo.transform.SetParent(mapRoot, false);

            var rt = buttonGo.AddComponent<RectTransform>();
            rt.anchorMin = nodeInfo.Location?.mapPosition ?? new Vector2(0.5f, 0.5f);
            rt.anchorMax = rt.anchorMin;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(94f, 94f);
            rt.anchoredPosition = Vector2.zero;

            var background = buttonGo.AddComponent<Image>();
            background.color = nodeInfo.IsUnlocked ? new Color(0.12f, 0.15f, 0.22f, 0.92f) : new Color(0.12f, 0.10f, 0.12f, 0.92f);

            var button = buttonGo.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() =>
            {
                selectedNode = nodeInfo;
                SyncNodeSelectionVisuals();
                UpdateSelectionPanel(nodeInfo);
            });

            var iconFrame = new GameObject("IconFrame");
            iconFrame.transform.SetParent(buttonGo.transform, false);
            var iconFrameRT = iconFrame.AddComponent<RectTransform>();
            iconFrameRT.anchorMin = new Vector2(0.12f, 0.22f);
            iconFrameRT.anchorMax = new Vector2(0.88f, 0.88f);
            iconFrameRT.offsetMin = Vector2.zero;
            iconFrameRT.offsetMax = Vector2.zero;
            var iconFrameImage = iconFrame.AddComponent<Image>();
            iconFrameImage.color = defaultOutlineColor;

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(iconFrame.transform, false);
            var iconRT = iconGo.AddComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0.12f, 0.12f);
            iconRT.anchorMax = new Vector2(0.88f, 0.88f);
            iconRT.offsetMin = Vector2.zero;
            iconRT.offsetMax = Vector2.zero;
            var iconImage = iconGo.AddComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.color = nodeInfo.IsUnlocked ? unlockedNodeTint : lockedNodeTint;
            iconImage.sprite = nodeInfo.Location?.nodeIcon;

            var label = CreateText(buttonGo.transform, "Label", BuildNodeLabel(nodeInfo), 16, FontStyles.Bold, Color.white);
            var labelRT = label.rectTransform;
            labelRT.anchorMin = new Vector2(0f, 0f);
            labelRT.anchorMax = new Vector2(1f, 0.22f);
            labelRT.offsetMin = new Vector2(4f, 0f);
            labelRT.offsetMax = new Vector2(-4f, -2f);
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 10;
            label.fontSizeMax = 16;

            if (nodeInfo.Stars > 0)
            {
                var stars = CreateText(buttonGo.transform, "Stars", BuildStars(nodeInfo.Stars), 18, FontStyles.Bold, Color.white);
                var starsRT = stars.rectTransform;
                starsRT.anchorMin = new Vector2(0.05f, 0.78f);
                starsRT.anchorMax = new Vector2(0.95f, 1f);
                starsRT.offsetMin = Vector2.zero;
                starsRT.offsetMax = new Vector2(0f, 4f);
                stars.alignment = TextAlignmentOptions.TopRight;
            }

            if (!nodeInfo.IsUnlocked)
            {
                var lockText = CreateText(buttonGo.transform, "Locked", "LOCKED", 14, FontStyles.Bold, new Color(0.95f, 0.72f, 0.72f));
                var lockRT = lockText.rectTransform;
                lockRT.anchorMin = new Vector2(0f, 0.78f);
                lockRT.anchorMax = new Vector2(1f, 1f);
                lockRT.offsetMin = new Vector2(4f, 0f);
                lockRT.offsetMax = new Vector2(-4f, 4f);
                lockText.alignment = TextAlignmentOptions.TopLeft;
            }
            else if (nodeInfo.IsDailyCase)
            {
                var dailyText = CreateText(buttonGo.transform, "Daily", "DAILY", 14, FontStyles.Bold, selectedOutlineColor);
                var dailyRT = dailyText.rectTransform;
                dailyRT.anchorMin = new Vector2(0f, 0.78f);
                dailyRT.anchorMax = new Vector2(1f, 1f);
                dailyRT.offsetMin = new Vector2(4f, 0f);
                dailyRT.offsetMax = new Vector2(-4f, 4f);
                dailyText.alignment = TextAlignmentOptions.TopLeft;
            }

            nodeInfo.Button = button;
            nodeInfo.IconFrame = iconFrameImage;
            nodeInfo.IconImage = iconImage;
        }

        void CreateDistrictMarker(IReadOnlyList<MapNodeInfo> districtNodes)
        {
            var first = districtNodes.FirstOrDefault();
            if (first?.District == null || first.Location == null)
                return;

            Vector2 centroid = new Vector2(
                districtNodes.Average(node => node.Location.mapPosition.x),
                districtNodes.Average(node => node.Location.mapPosition.y));
            centroid.y = Mathf.Clamp01(centroid.y + 0.08f);

            var markerGo = new GameObject($"District_{first.District.districtId}");
            markerGo.transform.SetParent(mapRoot, false);

            var rt = markerGo.AddComponent<RectTransform>();
            rt.anchorMin = centroid;
            rt.anchorMax = centroid;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(188f, 54f);
            rt.anchoredPosition = Vector2.zero;

            var background = markerGo.AddComponent<Image>();
            background.color = new Color(first.District.accentColor.r, first.District.accentColor.g, first.District.accentColor.b, 0.86f);

            if (first.District.mapIcon != null)
            {
                var iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(markerGo.transform, false);
                var iconRT = iconGo.AddComponent<RectTransform>();
                iconRT.anchorMin = new Vector2(0.05f, 0.14f);
                iconRT.anchorMax = new Vector2(0.28f, 0.86f);
                iconRT.offsetMin = Vector2.zero;
                iconRT.offsetMax = Vector2.zero;
                var iconImage = iconGo.AddComponent<Image>();
                iconImage.sprite = first.District.mapIcon;
                iconImage.preserveAspect = true;
            }

            var label = CreateText(markerGo.transform, "Label", first.District.displayName, 18, FontStyles.Bold, Color.white);
            var labelRT = label.rectTransform;
            labelRT.anchorMin = new Vector2(0.26f, 0f);
            labelRT.anchorMax = new Vector2(0.96f, 1f);
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.enableAutoSizing = true;
            label.fontSizeMin = 12;
            label.fontSizeMax = 18;
        }

        void CreateLegendRow(IReadOnlyList<MapNodeInfo> districtNodes)
        {
            if (legendParent == null || districtNodes.Count == 0)
                return;

            var first = districtNodes[0];
            string districtName = first.District != null ? first.District.displayName : first.Case.districtId;
            int unlockedCount = districtNodes.Count(node => node.IsUnlocked);

            var rowGo = new GameObject($"Legend_{districtName}");
            rowGo.transform.SetParent(legendParent, false);
            rowGo.AddComponent<LayoutElement>().preferredHeight = 34;

            var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 10;
            rowLayout.childControlHeight = true;
            rowLayout.childControlWidth = false;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childForceExpandWidth = false;

            if (first.District?.mapIcon != null)
            {
                var iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(rowGo.transform, false);
                iconGo.AddComponent<LayoutElement>().preferredWidth = 26;
                var iconImage = iconGo.AddComponent<Image>();
                iconImage.sprite = first.District.mapIcon;
                iconImage.preserveAspect = true;
            }

            var text = rowGo.AddComponent<TextMeshProUGUI>();
            text.fontSize = 18;
            text.color = legendTextColor;
            text.text = $"{districtName}  •  {unlockedCount}/{districtNodes.Count} open";
            text.alignment = TextAlignmentOptions.MidlineLeft;
        }

        void UpdateSelectionPanel(MapNodeInfo nodeInfo)
        {
            bool hasSelection = nodeInfo != null;
            if (selectionDistrictText != null)
                selectionDistrictText.text = hasSelection
                    ? $"{(nodeInfo.District != null ? nodeInfo.District.displayName : nodeInfo.Case.districtId)} • {(nodeInfo.Department != null ? nodeInfo.Department.displayName : "Unassigned")}"
                    : "No location selected";

            if (selectionLocationText != null)
                selectionLocationText.text = hasSelection
                    ? nodeInfo.Location?.displayName ?? nodeInfo.Case.GetResolvedLocation(0)?.displayName ?? nodeInfo.Case.title
                    : "Select a location";

            if (selectionCaseText != null)
                selectionCaseText.text = hasSelection ? nodeInfo.Case.title : string.Empty;

            if (selectionBodyText != null)
                selectionBodyText.text = hasSelection ? BuildSelectionBody(nodeInfo) : "Tap a city node to inspect the case file and launch path.";

            if (selectionStatusText != null)
                selectionStatusText.text = hasSelection
                    ? (nodeInfo.IsUnlocked ? BuildOpenStatus(nodeInfo) : nodeInfo.LockReason)
                    : string.Empty;

            if (selectionIconImage != null)
            {
                selectionIconImage.sprite = hasSelection ? nodeInfo.Location?.nodeIcon : null;
                selectionIconImage.color = hasSelection && nodeInfo.Location?.nodeIcon != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            }

            if (launchCaseButton != null)
                launchCaseButton.interactable = hasSelection && nodeInfo.IsUnlocked;
        }

        string BuildSelectionBody(MapNodeInfo nodeInfo)
        {
            if (!string.IsNullOrWhiteSpace(nodeInfo.Case.interrogationFocusSummary))
                return nodeInfo.Case.interrogationFocusSummary;

            if (!string.IsNullOrWhiteSpace(nodeInfo.Case.arcBeatSummary))
                return nodeInfo.Case.arcBeatSummary;

            return nodeInfo.Case.GetResolvedLocation(0)?.entryText ?? nodeInfo.Case.briefText;
        }

        string BuildOpenStatus(MapNodeInfo nodeInfo)
        {
            string stars = nodeInfo.Stars > 0 ? $"{nodeInfo.Stars}/3 stars" : "Uncleared";
            string interrogation = nodeInfo.Case.HasInterrogationPresentation()
                ? "Suspect pressure available"
                : "Contradiction file ready";
            return $"{stars} • {interrogation}";
        }

        void LaunchSelectedCase()
        {
            if (selectedNode == null || !selectedNode.IsUnlocked)
                return;

            GameManager.Instance?.LoadCaseByIndex(selectedNode.CaseIndex);
            GameScreenController.Instance?.ResetEntryState();
            NavigationManager.Instance?.Push(ScreenId.Game, TransitionType.FadeUp);
        }

        void SyncNodeSelectionVisuals()
        {
            foreach (var nodeInfo in nodeInfos)
            {
                if (nodeInfo.IconFrame == null)
                    continue;

                bool isSelected = ReferenceEquals(nodeInfo, selectedNode);
                nodeInfo.IconFrame.color = isSelected
                    ? selectedOutlineColor
                    : nodeInfo.IsUnlocked
                        ? defaultOutlineColor
                        : lockedOutlineColor;
            }
        }

        string BuildLockReason(DepartmentLockStatus lockStatus)
        {
            if (lockStatus.IsUnlocked || lockStatus.Department == null)
                return "Ready for launch";

            string blurb = string.IsNullOrWhiteSpace(lockStatus.Department.unlockBlurb)
                ? lockStatus.RequirementText
                : lockStatus.Department.unlockBlurb;
            return $"{lockStatus.RequirementText} • {blurb}";
        }

        static DistrictData ResolveDistrict(CaseData caseData, IReadOnlyDictionary<string, DistrictData> districtAssets)
        {
            if (caseData != null
                && !string.IsNullOrWhiteSpace(caseData.districtId)
                && districtAssets.TryGetValue(caseData.districtId, out var district))
            {
                return district;
            }

            return null;
        }

        static CityLocationData ResolveLocation(CaseData caseData, IReadOnlyDictionary<string, CityLocationData> locationAssets)
        {
            if (caseData != null
                && !string.IsNullOrWhiteSpace(caseData.cityLocationId)
                && locationAssets.TryGetValue(caseData.cityLocationId, out var location))
            {
                return location;
            }

            return null;
        }

        static string BuildNodeLabel(MapNodeInfo nodeInfo)
        {
            string title = nodeInfo.Location?.displayName ?? nodeInfo.Case.title;
            return title.Length > 12 ? title[..12].ToUpperInvariant() : title.ToUpperInvariant();
        }

        static string BuildStars(int stars)
        {
            const string filled = "<color=#F2C94C>★</color>";
            const string empty = "<color=#555A69>★</color>";
            int clamped = Mathf.Clamp(stars, 0, 3);
            return string.Concat(Enumerable.Repeat(filled, clamped))
                + string.Concat(Enumerable.Repeat(empty, 3 - clamped));
        }

        void ClearDynamicMapChildren()
        {
            if (mapRoot == null)
                return;

            for (int i = mapRoot.childCount - 1; i >= 0; i--)
            {
                var child = mapRoot.GetChild(i);
                if (mapBackgroundImage != null && child == mapBackgroundImage.transform)
                    continue;

                Destroy(child.gameObject);
            }
        }

        static TMP_Text CreateText(Transform parent, string name, string value, float size, FontStyles style, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        static void ClearChildren(Transform parent)
        {
            if (parent == null)
                return;

            for (int i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }

        sealed class MapNodeInfo
        {
            public CaseData Case;
            public int CaseIndex;
            public DepartmentData Department;
            public DistrictData District;
            public CityLocationData Location;
            public bool IsUnlocked;
            public string LockReason;
            public bool IsDailyCase;
            public int Stars;
            public Button Button;
            public Image IconFrame;
            public Image IconImage;
        }
    }
}
