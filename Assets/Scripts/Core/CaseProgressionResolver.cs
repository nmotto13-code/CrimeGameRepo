using System;
using System.Collections.Generic;
using System.Linq;
using CasebookGame.Data;

namespace CasebookGame.Core
{
    public static class CaseProgressionResolver
    {
        public static bool IsLocationUnlocked(CaseLocationData location, CaseData caseData, EvidenceDiscoverySystem discovery, GameManager gameManager)
        {
            if (location == null)
                return false;

            if (location.unlockCondition != null && !location.unlockCondition.IsEmpty)
                return IsConditionSatisfied(location.unlockCondition, caseData, discovery, gameManager);

            foreach (var evidenceId in location.unlockEvidenceIds ?? Enumerable.Empty<string>())
            {
                if (!IsEvidenceRequirementMet(evidenceId, caseData, discovery))
                    return false;
            }

            foreach (var tag in location.unlockTags ?? Enumerable.Empty<EvidenceTag>())
            {
                if (discovery == null || !discovery.HasFoundTag(tag))
                    return false;
            }

            return true;
        }

        public static bool IsConditionSatisfied(CaseProgressConditionData condition, CaseData caseData, EvidenceDiscoverySystem discovery, GameManager gameManager)
        {
            if (condition == null || condition.IsEmpty)
                return true;

            bool requireAll = condition.matchMode != ConditionMatchMode.Any;
            bool anyMatched = false;

            bool Evaluate(bool result)
            {
                if (requireAll && !result)
                    return false;

                if (!requireAll && result)
                    return true;

                anyMatched |= result;
                return true;
            }

            foreach (var evidenceId in condition.requiredEvidenceIds ?? Enumerable.Empty<string>())
            {
                if (!Evaluate(IsEvidenceRequirementMet(evidenceId, caseData, discovery)))
                    return false;
            }

            foreach (var tag in condition.requiredTags ?? Enumerable.Empty<EvidenceTag>())
            {
                if (!Evaluate(discovery != null && discovery.HasFoundTag(tag)))
                    return false;
            }

            foreach (var locationId in condition.requiredVisitedLocationIds ?? Enumerable.Empty<string>())
            {
                if (!Evaluate(gameManager != null && gameManager.HasVisitedLocation(locationId)))
                    return false;
            }

            foreach (var locationId in condition.requiredCompletedLocationIds ?? Enumerable.Empty<string>())
            {
                if (!Evaluate(gameManager != null && gameManager.HasCompletedLocation(locationId)))
                    return false;
            }

            foreach (var nodeId in condition.requiredCompletedInterrogationNodeIds ?? Enumerable.Empty<string>())
            {
                if (!Evaluate(gameManager != null && gameManager.HasCompletedInterrogationNode(nodeId)))
                    return false;
            }

            foreach (var outcomeId in condition.requiredInterrogationOutcomeIds ?? Enumerable.Empty<string>())
            {
                if (!Evaluate(gameManager != null && gameManager.HasInterrogationOutcome(outcomeId)))
                    return false;
            }

            foreach (var suspectId in condition.requiredSuspectIds ?? Enumerable.Empty<string>())
            {
                if (!Evaluate(IsSuspectRequirementMet(suspectId, caseData, gameManager)))
                    return false;
            }

            return requireAll || anyMatched;
        }

        public static bool IsEvidenceRequirementMet(string evidenceId, CaseData currentCase, EvidenceDiscoverySystem discovery)
        {
            if (string.IsNullOrWhiteSpace(evidenceId))
                return true;

            if (currentCase?.evidence != null)
            {
                foreach (var evidence in currentCase.evidence)
                {
                    if (evidence != null && string.Equals(evidence.evidenceId, evidenceId, StringComparison.OrdinalIgnoreCase))
                        return discovery != null && discovery.HasFoundEvidence(evidenceId);
                }
            }

            string relatedCaseId = ExtractCaseId(evidenceId);
            return !string.IsNullOrWhiteSpace(relatedCaseId) && PlayerProfile.HasSolvedCase(relatedCaseId);
        }

        public static string DescribeLegacyUnlockRequirement(CaseLocationData location, CaseData caseData, EvidenceDiscoverySystem discovery)
        {
            if (location == null)
                return string.Empty;

            var requirements = new List<string>();
            foreach (var evidenceId in location.unlockEvidenceIds ?? Enumerable.Empty<string>())
            {
                if (IsEvidenceRequirementMet(evidenceId, caseData, discovery))
                    continue;

                requirements.Add(ResolveEvidenceLabel(evidenceId, caseData));
            }

            foreach (var tag in location.unlockTags ?? Enumerable.Empty<EvidenceTag>())
            {
                if (discovery != null && discovery.HasFoundTag(tag))
                    continue;

                requirements.Add(HumanizeIdentifier(tag.ToString()));
            }

            return requirements.Count == 0
                ? string.Empty
                : $"Need: {string.Join(", ", requirements)}";
        }

        public static string DescribeCondition(CaseProgressConditionData condition, CaseData caseData)
        {
            if (condition == null || condition.IsEmpty)
                return string.Empty;

            var requirements = new List<string>();
            requirements.AddRange((condition.requiredEvidenceIds ?? Enumerable.Empty<string>())
                .Where(evidenceId => !string.IsNullOrWhiteSpace(evidenceId))
                .Select(evidenceId => ResolveEvidenceLabel(evidenceId, caseData)));
            requirements.AddRange((condition.requiredTags ?? Enumerable.Empty<EvidenceTag>())
                .Select(tag => HumanizeIdentifier(tag.ToString())));
            requirements.AddRange((condition.requiredVisitedLocationIds ?? Enumerable.Empty<string>())
                .Where(locationId => !string.IsNullOrWhiteSpace(locationId))
                .Select(locationId => ResolveLocationLabel(locationId, caseData, "Visit")));
            requirements.AddRange((condition.requiredCompletedLocationIds ?? Enumerable.Empty<string>())
                .Where(locationId => !string.IsNullOrWhiteSpace(locationId))
                .Select(locationId => $"Clear {ResolveLocationLabel(locationId, caseData, "visit")}"));
            requirements.AddRange((condition.requiredCompletedInterrogationNodeIds ?? Enumerable.Empty<string>())
                .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
                .Select(nodeId => ResolveNodeLabel(nodeId, caseData)));
            requirements.AddRange((condition.requiredInterrogationOutcomeIds ?? Enumerable.Empty<string>())
                .Where(outcomeId => !string.IsNullOrWhiteSpace(outcomeId))
                .Select(outcomeId => ResolveOutcomeLabel(outcomeId, caseData)));
            requirements.AddRange((condition.requiredSuspectIds ?? Enumerable.Empty<string>())
                .Where(suspectId => !string.IsNullOrWhiteSpace(suspectId))
                .Select(suspectId => ResolveSuspectLabel(suspectId, caseData)));

            if (requirements.Count == 0)
                return string.Empty;

            string joiner = condition.matchMode == ConditionMatchMode.Any ? " or " : ", ";
            return $"Need: {string.Join(joiner, requirements)}";
        }

        public static string ExtractCaseId(string evidenceId)
        {
            int separatorIndex = evidenceId.IndexOf("_E", StringComparison.OrdinalIgnoreCase);
            return separatorIndex > 0 ? evidenceId[..separatorIndex] : string.Empty;
        }

        static string ResolveEvidenceLabel(string evidenceId, CaseData caseData)
        {
            if (string.IsNullOrWhiteSpace(evidenceId))
                return "Evidence";

            var evidence = caseData?.evidence?.FirstOrDefault(candidate =>
                candidate != null && string.Equals(candidate.evidenceId, evidenceId, StringComparison.OrdinalIgnoreCase));
            if (evidence != null && !string.IsNullOrWhiteSpace(evidence.displayName))
                return evidence.displayName;

            string relatedCaseId = ExtractCaseId(evidenceId);
            return !string.IsNullOrWhiteSpace(relatedCaseId)
                ? $"Solve {relatedCaseId}"
                : evidenceId;
        }

        static string ResolveLocationLabel(string locationId, CaseData caseData, string fallbackPrefix)
        {
            var location = caseData?.caseLocations?.FirstOrDefault(candidate =>
                candidate != null && string.Equals(candidate.locationId, locationId, StringComparison.OrdinalIgnoreCase));
            if (location != null && !string.IsNullOrWhiteSpace(location.displayName))
                return location.displayName;

            return $"{fallbackPrefix} {HumanizeIdentifier(locationId)}";
        }

        static string ResolveOutcomeLabel(string outcomeId, CaseData caseData)
        {
            var outcome = caseData?.GetInterrogationOutcome(outcomeId);
            if (outcome != null && !string.IsNullOrWhiteSpace(outcome.displayLabel))
                return outcome.displayLabel;

            return HumanizeIdentifier(outcomeId);
        }

        static string ResolveNodeLabel(string nodeId, CaseData caseData)
        {
            var node = caseData?.interrogationNodes?.FirstOrDefault(candidate =>
                candidate != null && string.Equals(candidate.nodeId, nodeId, StringComparison.OrdinalIgnoreCase));
            if (node != null && !string.IsNullOrWhiteSpace(node.promptText))
                return $"Question: {Truncate(node.promptText, 42)}";

            return HumanizeIdentifier(nodeId);
        }

        static string ResolveSuspectLabel(string suspectId, CaseData caseData)
        {
            var suspect = caseData?.involvedSuspects?.FirstOrDefault(candidate =>
                candidate != null && string.Equals(candidate.suspectId, suspectId, StringComparison.OrdinalIgnoreCase));
            if (suspect != null && !string.IsNullOrWhiteSpace(suspect.displayName))
                return suspect.displayName;

            var summary = caseData?.suspectSummaries?.FirstOrDefault(candidate =>
                candidate != null && string.Equals(candidate.suspectId, suspectId, StringComparison.OrdinalIgnoreCase));
            if (summary != null && !string.IsNullOrWhiteSpace(summary.displayName))
                return summary.displayName;

            return HumanizeIdentifier(suspectId);
        }

        static string HumanizeIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value.Replace("_", " ").Trim();
        }

        static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
                return value;

            return value[..maxLength] + "...";
        }

        static bool IsSuspectRequirementMet(string suspectId, CaseData caseData, GameManager gameManager)
        {
            if (string.IsNullOrWhiteSpace(suspectId))
                return true;

            if (gameManager != null && gameManager.IsSuspectKnown(suspectId))
                return true;

            return caseData?.involvedSuspects != null
                && caseData.involvedSuspects.Any(suspect =>
                    suspect != null && string.Equals(suspect.suspectId, suspectId, StringComparison.OrdinalIgnoreCase));
        }
    }
}
