using System.Linq;
using System.Collections.Generic;
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
        int currentLocationIndex = 0;
        int progressRevision = 0;
        bool caseReadyForSolve;

        readonly HashSet<string> visitedLocationIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> completedLocationIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> completedInterrogationNodeIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> earnedInterrogationOutcomeIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> forceUnlockedLocationIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> forceLockedLocationIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> revealedSuspectIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> hiddenSuspectIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, int> lastVisitedProgressRevision = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);

        public event System.Action CaseStateChanged;

        public CaseData CurrentCase =>
            availableCases != null && availableCases.Length > 0
                ? availableCases[currentCaseIndex]
                : null;

        public int CurrentLocationIndex => currentLocationIndex;
        public CaseLocationData CurrentLocation => CurrentCase?.GetResolvedLocation(currentLocationIndex);

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

        public void LoadCurrentCase()
        {
            ResetCaseProgressState();
            currentLocationIndex = CurrentCase?.GetStartingLocationIndex() ?? 0;
            MarkLocationVisited(CurrentLocation?.locationId);
            CaseLoader.Instance?.LoadCase(CurrentCase);
            TryAutoCompleteCurrentLocation();
            BroadcastCaseStateChanged();
        }

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

        public void LoadCaseById(string caseId)
        {
            int index = IndexOfCase(caseId);
            if (index >= 0)
                LoadCaseByIndex(index);
        }

        public void SetCurrentLocationIndex(int index)
        {
            var caseData = CurrentCase;
            if (caseData == null)
                return;

            int clampedIndex = Mathf.Clamp(index, 0, caseData.GetResolvedLocationCount() - 1);
            if (!CanVisitLocationIndex(clampedIndex))
                return;

            currentLocationIndex = clampedIndex;
            MarkLocationVisited(CurrentLocation?.locationId);
            CaseLoader.Instance?.RefreshActiveLocation();
            TryAutoCompleteCurrentLocation();
            BroadcastCaseStateChanged();
        }

        public bool HasVisitedLocation(string locationId) =>
            !string.IsNullOrWhiteSpace(locationId) && visitedLocationIds.Contains(locationId);

        public bool HasCompletedLocation(string locationId) =>
            !string.IsNullOrWhiteSpace(locationId) && completedLocationIds.Contains(locationId);

        public bool HasCompletedInterrogationNode(string nodeId) =>
            !string.IsNullOrWhiteSpace(nodeId) && completedInterrogationNodeIds.Contains(nodeId);

        public bool HasInterrogationOutcome(string outcomeId) =>
            !string.IsNullOrWhiteSpace(outcomeId) && earnedInterrogationOutcomeIds.Contains(outcomeId);

        public bool IsSuspectKnown(string suspectId)
        {
            if (string.IsNullOrWhiteSpace(suspectId))
                return false;

            if (hiddenSuspectIds.Contains(suspectId))
                return false;

            if (revealedSuspectIds.Contains(suspectId))
                return true;

            return CurrentCase?.involvedSuspects != null
                && CurrentCase.involvedSuspects.Any(suspect =>
                    suspect != null && string.Equals(suspect.suspectId, suspectId, System.StringComparison.OrdinalIgnoreCase));
        }

        public IReadOnlyList<LocationSuspectPresenceData> GetCurrentLocationSuspectPresence()
        {
            var output = new List<LocationSuspectPresenceData>();
            var caseData = CurrentCase;
            var location = CurrentLocation;
            if (caseData == null || location?.presentSuspects == null)
                return output;

            foreach (var presence in location.presentSuspects)
            {
                if (presence != null && IsSuspectPresenceVisible(presence, caseData))
                    output.Add(presence);
            }

            return output;
        }

        public string GetSuspectDisplayName(string suspectId)
        {
            if (string.IsNullOrWhiteSpace(suspectId))
                return "Unknown Suspect";

            var suspect = CurrentCase?.involvedSuspects?.FirstOrDefault(candidate =>
                candidate != null && string.Equals(candidate.suspectId, suspectId, System.StringComparison.OrdinalIgnoreCase));
            if (suspect != null && !string.IsNullOrWhiteSpace(suspect.displayName))
                return suspect.displayName;

            var summary = CurrentCase?.suspectSummaries?.FirstOrDefault(candidate =>
                candidate != null && string.Equals(candidate.suspectId, suspectId, System.StringComparison.OrdinalIgnoreCase));
            if (summary != null && !string.IsNullOrWhiteSpace(summary.displayName))
                return summary.displayName;

            return suspectId;
        }

        public bool CanVisitLocationIndex(int index)
        {
            var caseData = CurrentCase;
            if (caseData == null)
                return false;

            var location = caseData.GetResolvedLocation(index);
            if (location == null)
                return false;

            string locationId = location.locationId;
            if (forceLockedLocationIds.Contains(locationId))
                return false;

            bool forceUnlocked = forceUnlockedLocationIds.Contains(locationId);
            bool unlocked = forceUnlocked
                || CaseProgressionResolver.IsLocationUnlocked(location, caseData, EvidenceDiscoverySystem.Instance, this);
            if (!unlocked)
                return false;

            if (!forceUnlocked
                && caseData.visitFlowMode == CaseVisitFlowMode.SequenceGraph
                && !IsLocationReachableInGraph(caseData, location))
                return false;

            if (HasVisitedLocation(locationId))
            {
                if (currentLocationIndex == index)
                    return true;

                if (!caseData.allowMapRevisit && location.revisitRule == LocationRevisitRule.Always)
                    return false;

                switch (location.revisitRule)
                {
                    case LocationRevisitRule.Never:
                        return false;
                    case LocationRevisitRule.AfterNewProgress:
                        return !lastVisitedProgressRevision.TryGetValue(locationId, out int revision)
                            || progressRevision > revision;
                    case LocationRevisitRule.Always:
                    default:
                        return true;
                }
            }

            return true;
        }

        public void MarkLocationCompleted(string locationId = null)
        {
            var caseData = CurrentCase;
            string resolvedLocationId = string.IsNullOrWhiteSpace(locationId) ? CurrentLocation?.locationId : locationId;
            if (caseData == null || string.IsNullOrWhiteSpace(resolvedLocationId))
                return;

            if (!completedLocationIds.Add(resolvedLocationId))
                return;

            NotifyCaseProgressAdvanced();
            int locationIndex = caseData.IndexOfResolvedLocation(resolvedLocationId);
            var location = locationIndex >= 0 ? caseData.GetResolvedLocation(locationIndex) : null;
            if (location == null)
            {
                BroadcastCaseStateChanged();
                return;
            }

            if (location.autoUnlocksSolve)
                caseReadyForSolve = true;

            bool outcomeApplied = false;
            if (!string.IsNullOrWhiteSpace(location.completionOutcomeId))
                outcomeApplied = ApplyInterrogationOutcome(location.completionOutcomeId);

            if (!outcomeApplied)
                BroadcastCaseStateChanged();
        }

        public void RegisterInterrogationNodeCompleted(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
                return;

            if (completedInterrogationNodeIds.Add(nodeId))
            {
                NotifyCaseProgressAdvanced();
                BroadcastCaseStateChanged();
            }
        }

        public bool ApplyInterrogationOutcome(string outcomeId)
        {
            var caseData = CurrentCase;
            if (caseData == null || string.IsNullOrWhiteSpace(outcomeId))
                return false;

            if (earnedInterrogationOutcomeIds.Contains(outcomeId))
                return true;

            var outcome = caseData.GetInterrogationOutcome(outcomeId);
            if (outcome == null)
                return false;

            earnedInterrogationOutcomeIds.Add(outcomeId);
            NotifyCaseProgressAdvanced();

            foreach (var locationId in outcome.unlockLocationIds ?? Enumerable.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(locationId))
                {
                    forceUnlockedLocationIds.Add(locationId);
                    forceLockedLocationIds.Remove(locationId);
                }
            }

            foreach (var locationId in outcome.lockLocationIds ?? Enumerable.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(locationId))
                    forceLockedLocationIds.Add(locationId);
            }

            foreach (var suspectId in outcome.revealSuspectIds ?? Enumerable.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(suspectId))
                {
                    revealedSuspectIds.Add(suspectId);
                    hiddenSuspectIds.Remove(suspectId);
                }
            }

            foreach (var suspectId in outcome.hideSuspectIds ?? Enumerable.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(suspectId))
                    hiddenSuspectIds.Add(suspectId);
            }

            foreach (var evidenceId in outcome.grantEvidenceIds ?? Enumerable.Empty<string>())
                EvidenceDiscoverySystem.Instance?.GrantEvidenceById(evidenceId);

            foreach (var tag in outcome.grantTags ?? Enumerable.Empty<EvidenceTag>())
                EvidenceDiscoverySystem.Instance?.GrantTag(tag);

            if (outcome.markCaseReadyForSolve)
                caseReadyForSolve = true;

            bool redirected = false;
            if (!string.IsNullOrWhiteSpace(outcome.redirectToLocationId))
            {
                int targetIndex = caseData.IndexOfResolvedLocation(outcome.redirectToLocationId);
                if (targetIndex >= 0 && CanVisitLocationIndex(targetIndex))
                {
                    redirected = true;
                    SetCurrentLocationIndex(targetIndex);
                }
            }

            if (!redirected)
                BroadcastCaseStateChanged();

            return true;
        }

        public void NotifyCaseProgressAdvanced() => progressRevision++;

        public void BroadcastCaseStateChanged()
        {
            InterrogationFlowController.Instance?.RefreshAvailability();
            CaseStateChanged?.Invoke();
        }

        public bool IsCaseReadyForSolve()
        {
            var caseData = CurrentCase;
            if (caseData == null)
                return true;

            switch (caseData.locationReadyForSolveMode)
            {
                case CaseSolveGateMode.RequireRequiredVisits:
                    for (int index = 0; index < caseData.GetResolvedLocationCount(); index++)
                    {
                        var location = caseData.GetResolvedLocation(index);
                        if (location != null && location.isRequiredForSolve && !HasCompletedLocation(location.locationId))
                            return false;
                    }
                    return true;
                case CaseSolveGateMode.RequireInterrogationOutcome:
                    return caseReadyForSolve;
                case CaseSolveGateMode.LegacyContradictionOnly:
                default:
                    return true;
            }
        }

        public string GetLocationVisitStatusText(int index)
        {
            var caseData = CurrentCase;
            if (caseData == null)
                return "No case loaded";

            var location = caseData.GetResolvedLocation(index);
            if (location == null)
                return "Location unavailable";

            if (CurrentLocationIndex == index)
                return HasCompletedLocation(location.locationId) ? "Current visit cleared" : "Current visit";

            if (HasCompletedLocation(location.locationId))
                return CanVisitLocationIndex(index) ? "Revisit available" : GetLocationLockReason(index);

            if (CanVisitLocationIndex(index))
                return HasVisitedLocation(location.locationId) ? "Revisit available" : "Open lead";

            return GetLocationLockReason(index);
        }

        public string GetLocationLockReason(int index)
        {
            var caseData = CurrentCase;
            if (caseData == null)
                return "No case loaded";

            var location = caseData.GetResolvedLocation(index);
            if (location == null)
                return "Location unavailable";

            string locationId = location.locationId;
            if (forceLockedLocationIds.Contains(locationId))
                return "Locked by an unresolved interrogation beat.";

            bool forceUnlocked = forceUnlockedLocationIds.Contains(locationId);
            if (!forceUnlocked
                && location.unlockCondition != null
                && !location.unlockCondition.IsEmpty
                && !CaseProgressionResolver.IsConditionSatisfied(location.unlockCondition, caseData, EvidenceDiscoverySystem.Instance, this))
            {
                return CaseProgressionResolver.DescribeCondition(location.unlockCondition, caseData);
            }

            if (!forceUnlocked)
            {
                string legacyRequirement = CaseProgressionResolver.DescribeLegacyUnlockRequirement(location, caseData, EvidenceDiscoverySystem.Instance);
                if (!string.IsNullOrWhiteSpace(legacyRequirement))
                    return legacyRequirement;
            }

            if (!forceUnlocked
                && caseData.visitFlowMode == CaseVisitFlowMode.SequenceGraph
                && !IsLocationReachableInGraph(caseData, location))
            {
                return "Follow the current lead chain first.";
            }

            if (HasVisitedLocation(locationId))
            {
                if (!caseData.allowMapRevisit && location.revisitRule == LocationRevisitRule.Always)
                    return "Route revisits are disabled for this case.";

                return location.revisitRule switch
                {
                    LocationRevisitRule.Never => "This visit closes once you move on.",
                    LocationRevisitRule.AfterNewProgress => !lastVisitedProgressRevision.TryGetValue(locationId, out int revision)
                        || progressRevision > revision
                            ? "Revisit available"
                            : "Revisit after new evidence or interrogation progress.",
                    _ => "Revisit available"
                };
            }

            return "Open lead";
        }

        public string GetSolveGateStatusText()
        {
            var caseData = CurrentCase;
            if (caseData == null)
                return "No case loaded.";

            if (IsCaseReadyForSolve())
                return "Case ready for final contradiction. Open SOLVE when your board is set.";

            return caseData.locationReadyForSolveMode switch
            {
                CaseSolveGateMode.RequireRequiredVisits => BuildRequiredVisitGateText(caseData),
                CaseSolveGateMode.RequireInterrogationOutcome => BuildInterrogationGateText(caseData),
                _ => "Contradiction solve is available."
            };
        }

        public string GetSceneHintText()
        {
            var caseData = CurrentCase;
            var location = CurrentLocation;
            if (caseData == null || location == null)
                return "Load a case to begin.";

            var leadPresence = GetCurrentLocationSuspectPresence().FirstOrDefault(presence =>
                presence != null
                && !string.IsNullOrWhiteSpace(presence.interrogationEntryNodeId)
                && InterrogationFlowController.Instance != null
                && InterrogationFlowController.Instance.IsEntryNodeAvailable(presence.interrogationEntryNodeId));
            if (leadPresence != null)
                return $"{GetSuspectDisplayName(leadPresence.suspectId)} is ready for questioning. Tap INTERROGATE.";

            if (!IsCaseReadyForSolve())
            {
                for (int index = 0; index < caseData.GetResolvedLocationCount(); index++)
                {
                    if (index == CurrentLocationIndex)
                        continue;

                    if (CanVisitLocationIndex(index))
                        return $"New lead open: {caseData.GetResolvedLocation(index).displayName}.";
                }
            }

            if (location.hotspots != null && location.hotspots.Count > 0 && !HasCompletedLocation(location.locationId))
                return $"Search {location.displayName} for more clues.";

            return GetSolveGateStatusText();
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

        void ResetCaseProgressState()
        {
            progressRevision = 0;
            caseReadyForSolve = false;
            visitedLocationIds.Clear();
            completedLocationIds.Clear();
            completedInterrogationNodeIds.Clear();
            earnedInterrogationOutcomeIds.Clear();
            forceUnlockedLocationIds.Clear();
            forceLockedLocationIds.Clear();
            revealedSuspectIds.Clear();
            hiddenSuspectIds.Clear();
            lastVisitedProgressRevision.Clear();
        }

        void MarkLocationVisited(string locationId)
        {
            if (string.IsNullOrWhiteSpace(locationId))
                return;

            visitedLocationIds.Add(locationId);
            lastVisitedProgressRevision[locationId] = progressRevision;
        }

        void TryAutoCompleteCurrentLocation()
        {
            var location = CurrentLocation;
            if (location == null || !location.autoCompleteOnEnter)
                return;

            MarkLocationCompleted(location.locationId);
        }

        string BuildRequiredVisitGateText(CaseData caseData)
        {
            var missingLocations = new List<string>();
            for (int index = 0; index < caseData.GetResolvedLocationCount(); index++)
            {
                var location = caseData.GetResolvedLocation(index);
                if (location != null && location.isRequiredForSolve && !HasCompletedLocation(location.locationId))
                    missingLocations.Add(location.displayName);
            }

            return missingLocations.Count == 0
                ? "Finish the active visit chain before solving."
                : $"Finish required visits: {string.Join(", ", missingLocations)}.";
        }

        string BuildInterrogationGateText(CaseData caseData)
        {
            var leadPresence = GetCurrentLocationSuspectPresence().FirstOrDefault(presence =>
                presence != null
                && !string.IsNullOrWhiteSpace(presence.interrogationEntryNodeId)
                && InterrogationFlowController.Instance != null
                && InterrogationFlowController.Instance.IsEntryNodeAvailable(presence.interrogationEntryNodeId));
            if (leadPresence != null)
                return $"Press the current suspect lead: {GetSuspectDisplayName(leadPresence.suspectId)}.";

            for (int index = 0; index < caseData.GetResolvedLocationCount(); index++)
            {
                if (CanVisitLocationIndex(index) && !HasVisitedLocation(caseData.GetResolvedLocation(index).locationId))
                    return $"Advance to {caseData.GetResolvedLocation(index).displayName} before closing the case.";
            }

            return "Work the remaining interrogation and visit beats before closing the file.";
        }

        bool IsLocationReachableInGraph(CaseData caseData, CaseLocationData targetLocation)
        {
            if (caseData == null || targetLocation == null)
                return false;

            int startingIndex = caseData.GetStartingLocationIndex();
            var startingLocation = caseData.GetResolvedLocation(startingIndex);
            if (startingLocation != null
                && string.Equals(startingLocation.locationId, targetLocation.locationId, System.StringComparison.OrdinalIgnoreCase))
                return true;

            bool hasInboundEdge = false;
            for (int index = 0; index < caseData.GetResolvedLocationCount(); index++)
            {
                var sourceLocation = caseData.GetResolvedLocation(index);
                if (sourceLocation?.nextLocationIds == null)
                    continue;

                foreach (var nextLocationId in sourceLocation.nextLocationIds)
                {
                    if (!string.Equals(nextLocationId, targetLocation.locationId, System.StringComparison.OrdinalIgnoreCase))
                        continue;

                    hasInboundEdge = true;
                    if (HasVisitedLocation(sourceLocation.locationId) || HasCompletedLocation(sourceLocation.locationId))
                        return true;
                }
            }

            return !hasInboundEdge;
        }

        bool IsSuspectPresenceVisible(LocationSuspectPresenceData presence, CaseData caseData)
        {
            if (presence == null || string.IsNullOrWhiteSpace(presence.suspectId))
                return false;

            if (hiddenSuspectIds.Contains(presence.suspectId))
                return false;

            if (revealedSuspectIds.Contains(presence.suspectId))
                return true;

            bool conditionSatisfied = CaseProgressionResolver.IsConditionSatisfied(
                presence.availabilityCondition,
                caseData,
                EvidenceDiscoverySystem.Instance,
                this);
            if (!conditionSatisfied)
                return false;

            return presence.isVisibleOnEntry
                || (presence.availabilityCondition != null && !presence.availabilityCondition.IsEmpty);
        }
    }
}
