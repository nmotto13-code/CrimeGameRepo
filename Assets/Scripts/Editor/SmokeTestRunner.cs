#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using CasebookGame.Core;
using CasebookGame.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CasebookGame.Editor
{
    public static class SmokeTestRunner
    {
        const string ScenePath = "Assets/Scenes/CaseScene.unity";

        [MenuItem("Tools/SmokeTest/Run")]
        public static void RunFromMenu()
        {
            var report = RunInternal();
            LogReport(report);
        }

        public static void RunBatchMode()
        {
            var report = RunInternal();
            LogReport(report);
            EditorApplication.Exit(report.Passed ? 0 : 1);
        }

        static SmokeTestReport RunInternal()
        {
            var report = new SmokeTestReport();
            int builtCaseCount = SceneBuilder.BuildSceneForAutomation();
            if (builtCaseCount <= 0)
            {
                report.Failures.Add("SceneBuilder did not build any cases into the scene.");
                return report;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var allCases = LoadCases(report);
            if (allCases.Count == 0)
                return report;

            var caseLoader = Object.FindFirstObjectByType<CaseLoader>(FindObjectsInactive.Include);
            var discoverySystem = Object.FindFirstObjectByType<EvidenceDiscoverySystem>(FindObjectsInactive.Include);
            var evaluator = Object.FindFirstObjectByType<ContradictionEvaluator>(FindObjectsInactive.Include);
            var resultsController = Object.FindFirstObjectByType<ResultsController>(FindObjectsInactive.Include);
            var gameManager = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);

            if (caseLoader == null) report.Failures.Add("CaseLoader is missing from the built scene.");
            if (discoverySystem == null) report.Failures.Add("EvidenceDiscoverySystem is missing from the built scene.");
            if (evaluator == null) report.Failures.Add("ContradictionEvaluator is missing from the built scene.");
            if (resultsController == null) report.Failures.Add("ResultsController is missing from the built scene.");
            if (gameManager == null) report.Failures.Add("GameManager is missing from the built scene.");
            if (report.Failures.Count > 0)
                return report;

            var sceneBackground = GetField<Image>(caseLoader, "sceneBackground");
            var claimsListParent = GetField<Transform>(caseLoader, "claimsListParent");
            var hotspotParent = GetField<Transform>(caseLoader, "hotspotParent");
            var evidenceTabParent = GetField<Transform>(discoverySystem, "evidenceTabParent");
            var resultPanel = GetField<GameObject>(resultsController, "resultPanel");

            if (sceneBackground == null) report.Failures.Add("CaseLoader.sceneBackground is not wired.");
            if (claimsListParent == null) report.Failures.Add("CaseLoader.claimsListParent is not wired.");
            if (hotspotParent == null) report.Failures.Add("CaseLoader.hotspotParent is not wired.");
            if (evidenceTabParent == null) report.Failures.Add("EvidenceDiscoverySystem.evidenceTabParent is not wired.");
            if (resultPanel == null) report.Failures.Add("ResultsController.resultPanel is not wired.");
            if (report.Failures.Count > 0)
                return report;

            if (gameManager.availableCases == null || gameManager.availableCases.Length != allCases.Count)
                report.Failures.Add($"GameManager roster mismatch. Expected {allCases.Count} cases, found {gameManager.availableCases?.Length ?? 0}.");

            foreach (var caseData in allCases)
            {
                ValidateCaseData(caseData, report);
                ExerciseCaseScene(caseData, caseLoader, discoverySystem, evaluator, resultsController,
                    sceneBackground, claimsListParent, hotspotParent, evidenceTabParent, resultPanel, report);
            }

            if (!report.Failures.Any())
                report.Passes.Add($"Validated {allCases.Count} case assets and scene loads.");

            return report;
        }

        static List<CaseData> LoadCases(SmokeTestReport report)
        {
            var cases = AssetDatabase.FindAssets("t:CaseData", new[] { "Assets/Resources/Cases" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<CaseData>)
                .OrderBy(c => c.caseId)
                .ToList();

            if (cases.Count == 0)
                report.Failures.Add("No CaseData assets were found under Resources/Cases.");

            return cases;
        }

        static void ValidateCaseData(CaseData caseData, SmokeTestReport report)
        {
            if (caseData == null)
            {
                report.Failures.Add("Encountered a null CaseData asset in Resources/Cases.");
                return;
            }

            if (string.IsNullOrWhiteSpace(caseData.caseId))
                report.Failures.Add($"{caseData.name}: caseId is blank.");
            if (string.IsNullOrWhiteSpace(caseData.title))
                report.Failures.Add($"{caseData.caseId}: title is blank.");
            if (caseData.sceneBackground == null)
                report.Failures.Add($"{caseData.caseId}: sceneBackground is missing.");
            if (caseData.hotspots == null || caseData.hotspots.Count == 0)
                report.Failures.Add($"{caseData.caseId}: hotspots list is empty.");
            if (caseData.evidence == null || caseData.evidence.Count == 0)
                report.Failures.Add($"{caseData.caseId}: evidence list is empty.");
            if (caseData.claims == null || caseData.claims.Count == 0)
                report.Failures.Add($"{caseData.caseId}: claims list is empty.");

            var evidenceIds = new HashSet<string>();
            foreach (var evidence in caseData.evidence)
            {
                if (evidence == null)
                {
                    report.Failures.Add($"{caseData.caseId}: evidence list contains a null entry.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(evidence.evidenceId))
                    report.Failures.Add($"{caseData.caseId}: an evidence asset has a blank evidenceId.");
                evidenceIds.Add(evidence.evidenceId);
            }

            var claimIds = new HashSet<string>();
            foreach (var claim in caseData.claims)
            {
                if (claim == null)
                {
                    report.Failures.Add($"{caseData.caseId}: claims list contains a null entry.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(claim.claimId))
                    report.Failures.Add($"{caseData.caseId}: a claim asset has a blank claimId.");
                claimIds.Add(claim.claimId);
            }

            if (!string.IsNullOrWhiteSpace(caseData.contradictoryClaimId) && !claimIds.Contains(caseData.contradictoryClaimId))
                report.Failures.Add($"{caseData.caseId}: contradictoryClaimId '{caseData.contradictoryClaimId}' does not match a claim in the case.");

            if (!string.IsNullOrWhiteSpace(caseData.primaryEvidenceIdA) && !evidenceIds.Contains(caseData.primaryEvidenceIdA))
                report.Failures.Add($"{caseData.caseId}: primaryEvidenceIdA '{caseData.primaryEvidenceIdA}' does not match case evidence.");

            if (!string.IsNullOrWhiteSpace(caseData.primaryEvidenceIdB) && !evidenceIds.Contains(caseData.primaryEvidenceIdB))
                report.Failures.Add($"{caseData.caseId}: primaryEvidenceIdB '{caseData.primaryEvidenceIdB}' does not match case evidence.");

            foreach (var hotspot in caseData.hotspots)
            {
                if (!evidenceIds.Contains(hotspot.evidenceId))
                    report.Failures.Add($"{caseData.caseId}: hotspot '{hotspot.hotspotId}' points at missing evidence '{hotspot.evidenceId}'.");
            }
        }

        static void ExerciseCaseScene(
            CaseData caseData,
            CaseLoader caseLoader,
            EvidenceDiscoverySystem discoverySystem,
            ContradictionEvaluator evaluator,
            ResultsController resultsController,
            Image sceneBackground,
            Transform claimsListParent,
            Transform hotspotParent,
            Transform evidenceTabParent,
            GameObject resultPanel,
            SmokeTestReport report)
        {
            ClearChildrenImmediate(claimsListParent);
            ClearChildrenImmediate(hotspotParent);
            ClearChildrenImmediate(evidenceTabParent);
            resultPanel.SetActive(false);

            foreach (var evidence in caseData.evidence.Where(e => e != null))
                evidence.ResetRuntimeState();

            InvokeCaseLoader(caseLoader, "SetupBackground", caseData);
            InvokeCaseLoader(caseLoader, "SetupBrief", caseData);
            InvokeCaseLoader(caseLoader, "SetupClaims", caseData);
            InvokeCaseLoader(caseLoader, "SetupHotspots", caseData);
            evaluator.SetCase(caseData);
            discoverySystem.StartInvestigation(caseData);

            if (sceneBackground.sprite != caseData.sceneBackground)
                report.Failures.Add($"{caseData.caseId}: background sprite was not applied to the scene.");

            if (hotspotParent.childCount != caseData.hotspots.Count || hotspotParent.childCount <= 0)
                report.Failures.Add($"{caseData.caseId}: expected {caseData.hotspots.Count} hotspots, found {hotspotParent.childCount}.");

            if (claimsListParent.childCount != caseData.claims.Count || claimsListParent.childCount <= 0)
                report.Failures.Add($"{caseData.caseId}: expected {caseData.claims.Count} claim cards, found {claimsListParent.childCount}.");

            if (caseData.evidence.Count > 0)
            {
                discoverySystem.RegisterEvidenceFound(caseData.evidence[0]);
                if (evidenceTabParent.childCount <= 0)
                    report.Failures.Add($"{caseData.caseId}: evidence tab card was not created after discovery.");
            }

            var showResult = typeof(ResultsController).GetMethod("ShowResult", BindingFlags.Instance | BindingFlags.NonPublic);
            showResult?.Invoke(resultsController, new object[] { false, "Smoke test" });
            if (!resultPanel.activeSelf)
                report.Failures.Add($"{caseData.caseId}: results UI did not become reachable when ResultsController was invoked.");

            resultPanel.SetActive(false);
            report.Passes.Add($"{caseData.caseId}: scene load checks passed.");
        }

        static T GetField<T>(object target, string fieldName) where T : Object
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return field?.GetValue(target) as T;
        }

        static void InvokeCaseLoader(CaseLoader caseLoader, string methodName, CaseData caseData)
        {
            var method = typeof(CaseLoader).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(caseLoader, new object[] { caseData });
        }

        static void ClearChildrenImmediate(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }

        static void LogReport(SmokeTestReport report)
        {
            var builder = new StringBuilder();
            builder.AppendLine("[SmokeTest] PASS/FAIL Summary");

            foreach (var pass in report.Passes)
                builder.AppendLine($"PASS  {pass}");

            foreach (var failure in report.Failures)
                builder.AppendLine($"FAIL  {failure}");

            if (report.Passed)
                Debug.Log(builder.ToString());
            else
                Debug.LogError(builder.ToString());
        }

        sealed class SmokeTestReport
        {
            public readonly List<string> Passes = new();
            public readonly List<string> Failures = new();
            public bool Passed => Failures.Count == 0;
        }
    }
}
#endif
