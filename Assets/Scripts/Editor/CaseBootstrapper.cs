#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using CasebookGame.Data;
using UnityEditor;
using UnityEngine;

namespace CasebookGame.Editor
{
    public static class CaseBootstrapper
    {
        const string SpriteFolder      = "Assets/Sprites/Evidence";
        const string EvidenceFolder    = "Assets/ScriptableObjects/Cases/Evidence";
        const string ClaimFolder       = "Assets/ScriptableObjects/Cases/Claims";
        const string SuspectFolder     = "Assets/ScriptableObjects/Cases/Suspects";
        const string IntNodeFolder     = "Assets/ScriptableObjects/Cases/InterrogationNodes";
        const string CaseFolder        = "Assets/Resources/Cases";
        const string DeptFolder        = "Assets/Resources/Departments";
        const string CasesJsonPath     = "Docs/content/cases_C011_C030.json";
        const string SuspectsJsonPath  = "Docs/content/suspects_C011_C030.json";

        static readonly Vector2[] DefaultPositions =
        {
            new Vector2(0.25f, 0.65f), new Vector2(0.70f, 0.70f),
            new Vector2(0.50f, 0.35f), new Vector2(0.20f, 0.30f),
            new Vector2(0.75f, 0.40f), new Vector2(0.45f, 0.75f),
            new Vector2(0.60f, 0.25f),
        };

        // ── Entry point ─────────────────────────────────────────────────
        [MenuItem("Casebook/Import/Bootstrap Cases C011-C030")]
        public static void Run()
        {
            EnsureFolder(EvidenceFolder);
            EnsureFolder(ClaimFolder);
            EnsureFolder(SuspectFolder);
            EnsureFolder(IntNodeFolder);
            EnsureFolder(CaseFolder);
            EnsureFolder(DeptFolder);

            string casesPath    = AbsPath(CasesJsonPath);
            string suspectsPath = AbsPath(SuspectsJsonPath);

            if (!File.Exists(casesPath))    { Debug.LogError($"[Bootstrap] Missing: {casesPath}");    return; }
            if (!File.Exists(suspectsPath)) { Debug.LogError($"[Bootstrap] Missing: {suspectsPath}"); return; }

            var root     = JsonUtility.FromJson<JsonRoot>(File.ReadAllText(casesPath));
            // JsonUtility cannot parse a root array — wrap it
            var suspWrap = JsonUtility.FromJson<SuspectsWrapper>(
                "{\"suspects\":" + File.ReadAllText(suspectsPath) + "}");

            int created = 0;
            int updated = 0;

            // ── 1. Create SuspectData assets (first pass — no associates yet) ──
            foreach (var js in suspWrap.suspects)
            {
                string path = $"{SuspectFolder}/{js.suspectId}.asset";
                var sd = AssetDatabase.LoadAssetAtPath<SuspectData>(path);
                if (sd == null)
                {
                    sd = ScriptableObject.CreateInstance<SuspectData>();
                    sd.suspectId        = js.suspectId;
                    sd.displayName      = js.displayName;
                    sd.bio              = js.bio;
                    sd.traits           = new List<string>(js.traits ?? new string[0]);
                    sd.credibilityScore = js.credibilityScore * 100f; // JSON 0-1 → Unity 0-100
                    sd.notes            = js.notes;
                    // linkedCaseIds: convert "C011" → "Case_011"
                    sd.linkedCaseIds = new List<string>();
                    foreach (var cid in (js.linkedCaseIds ?? new string[0]))
                        sd.linkedCaseIds.Add(ToUnityId(cid));
                    AssetDatabase.CreateAsset(sd, path);
                    created++;
                }
            }
            AssetDatabase.SaveAssets();

            // ── 2. Patch knownAssociates (second pass — all assets now exist) ──
            foreach (var js in suspWrap.suspects)
            {
                string path = $"{SuspectFolder}/{js.suspectId}.asset";
                var sd = AssetDatabase.LoadAssetAtPath<SuspectData>(path);
                if (sd == null) continue;
                sd.knownAssociates = new List<SuspectData>();
                foreach (var assocId in (js.knownAssociates ?? new string[0]))
                {
                    var assoc = AssetDatabase.LoadAssetAtPath<SuspectData>(
                        $"{SuspectFolder}/{assocId}.asset");
                    if (assoc != null) sd.knownAssociates.Add(assoc);
                    else Debug.LogWarning($"[Bootstrap] Associate not found: {assocId} for {js.suspectId}");
                }
                EditorUtility.SetDirty(sd);
                updated++;
            }

            // ── 3. Create Evidence, Claims, InterrogationNodes, CaseData ──
            foreach (var dept in root.departments)
            {
                var deptCaseIds = new List<string>();

                foreach (var jc in dept.cases)
                {
                    string unityId = ToUnityId(jc.caseId);
                    deptCaseIds.Add(unityId);

                    // Evidence
                    var evidenceList = new List<EvidenceData>();
                    foreach (var je in jc.evidence)
                    {
                        string path = $"{EvidenceFolder}/{je.evidenceId}.asset";
                        var ev = AssetDatabase.LoadAssetAtPath<EvidenceData>(path);
                        if (ev == null)
                        {
                            ev = ScriptableObject.CreateInstance<EvidenceData>();
                            ev.evidenceId      = je.evidenceId;
                            ev.displayName     = je.displayName;
                            ev.descriptionText = je.description;
                            string spritePath  = $"{SpriteFolder}/{je.evidenceId}.png";
                            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                            ev.imageSprite           = sprite;
                            ev.usesPlaceholderSprite = (sprite == null);
                            ev.displayMode           = EvidenceDisplayMode.Default;
                            AssetDatabase.CreateAsset(ev, path);
                            created++;
                        }
                        evidenceList.Add(ev);
                    }

                    // Claims
                    var claimList = new List<ClaimData>();
                    foreach (var jcl in jc.claims)
                    {
                        string path = $"{ClaimFolder}/{jcl.claimId}.asset";
                        var cl = AssetDatabase.LoadAssetAtPath<ClaimData>(path);
                        if (cl == null)
                        {
                            cl = ScriptableObject.CreateInstance<ClaimData>();
                            cl.claimId     = jcl.claimId;
                            cl.speakerName = jcl.suspectName;
                            cl.claimText   = jcl.claimText;
                            AssetDatabase.CreateAsset(cl, path);
                            created++;
                        }
                        claimList.Add(cl);
                    }

                    // InterrogationNodes
                    var nodeList = new List<InterrogationNode>();
                    if (jc.interrogationNodes != null)
                    {
                        foreach (var jn in jc.interrogationNodes)
                        {
                            string path = $"{IntNodeFolder}/{jn.nodeId}.asset";
                            var node = AssetDatabase.LoadAssetAtPath<InterrogationNode>(path);
                            if (node == null)
                            {
                                node = ScriptableObject.CreateInstance<InterrogationNode>();
                                node.nodeId               = jn.nodeId;
                                node.promptText           = jn.promptText;
                                node.responses            = new List<string>(jn.responses ?? new string[0]);
                                node.correctResponseIndex = jn.correctResponseIndex;
                                node.evidenceRequiredIds  = new List<string>(
                                    jn.evidenceRequiredIds ?? new string[0]);
                                node.unlockConditionTags = new List<EvidenceTag>();
                                foreach (var tagStr in (jn.unlockConditionTags ?? new string[0]))
                                {
                                    if (Enum.TryParse<EvidenceTag>(tagStr, out var tag))
                                        node.unlockConditionTags.Add(tag);
                                    else
                                        Debug.LogWarning($"[Bootstrap] Unknown EvidenceTag: {tagStr} on {jn.nodeId}");
                                }
                                AssetDatabase.CreateAsset(node, path);
                                created++;
                            }
                            nodeList.Add(node);
                        }
                    }

                    // CaseData — create if missing, always patch wiring fields
                    string casePath = $"{CaseFolder}/{unityId}.asset";
                    var cd = AssetDatabase.LoadAssetAtPath<CaseData>(casePath);
                    bool caseWasNew = (cd == null);
                    if (caseWasNew)
                    {
                        cd = ScriptableObject.CreateInstance<CaseData>();
                        cd.caseId    = unityId;
                        cd.title     = jc.title;
                        cd.briefText = jc.brief;
                        cd.evidence  = evidenceList;
                        cd.claims    = claimList;
                        cd.contradictoryClaimId = jc.contradictionClaimId;
                        cd.explanationText      = jc.explanation;
                        cd.primaryEvidenceIdA   = jc.primaryEvidenceIdA;
                        cd.primaryEvidenceIdB   = jc.primaryEvidenceIdB;
                        cd.basePoints           = jc.basePoints > 0 ? jc.basePoints : 500;
                        cd.timeLimitSeconds     = jc.timeLimitSeconds;
                        int   tc_cc = jc.toolConfig != null ? jc.toolConfig.crossCheckCharges    : 2;
                        float tc_ec = jc.toolConfig != null ? jc.toolConfig.enhanceCooldownSeconds : 12f;
                        cd.toolConfig = new ToolConfig
                        {
                            crossCheckCharges      = tc_cc,
                            enhanceCooldownSeconds = tc_ec,
                            timelineSnapCharges    = 1,
                        };
                        for (int i = 0; i < jc.hotspots.Length; i++)
                        {
                            var jh = jc.hotspots[i];
                            cd.hotspots.Add(new HotspotData
                            {
                                hotspotId          = jh.hotspotId,
                                normalizedPosition = i < DefaultPositions.Length
                                    ? DefaultPositions[i] : new Vector2(0.5f, 0.5f),
                                radius       = 0.06f,
                                evidenceId   = jh.linkedEvidenceId,
                                hotspotLabel = jh.label,
                            });
                        }
                    }

                    // Always wire suspects and interrogation nodes (patch operation)
                    cd.involvedSuspects = new List<SuspectData>();
                    foreach (var sid in (jc.involvedSuspects ?? new string[0]))
                    {
                        var sd = AssetDatabase.LoadAssetAtPath<SuspectData>(
                            $"{SuspectFolder}/{sid}.asset");
                        if (sd != null) cd.involvedSuspects.Add(sd);
                        else Debug.LogWarning($"[Bootstrap] Suspect not found: {sid} for case {unityId}");
                    }
                    cd.interrogationNodes = nodeList;

                    if (caseWasNew) { AssetDatabase.CreateAsset(cd, casePath); created++; }
                    else            { EditorUtility.SetDirty(cd); updated++; }
                }

                // DepartmentData
                DepartmentId deptEnum = dept.departmentId == "FRAUD"
                    ? DepartmentId.Fraud : DepartmentId.MissingPersons;
                string deptFile = $"{DeptFolder}/{dept.displayName.Replace(" ", "_")}.asset";
                var da = AssetDatabase.LoadAssetAtPath<DepartmentData>(deptFile);
                if (da == null)
                {
                    da = ScriptableObject.CreateInstance<DepartmentData>();
                    da.departmentId       = deptEnum;
                    da.displayName        = dept.displayName;
                    da.requiredRank       = 1;
                    da.requiredStarsCount = 0;
                    da.caseIds            = deptCaseIds;
                    AssetDatabase.CreateAsset(da, deptFile);
                    created++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string msg = $"Created {created} assets, patched {updated}.";
            Debug.Log($"[CaseBootstrapper] Done. {msg}");
            EditorUtility.DisplayDialog("Bootstrap Complete", msg +
                "\n\nC011–C030 evidence, claims, suspects, interrogation nodes, " +
                "and cases are now in the project.", "OK");
        }

        // ── Helpers ──────────────────────────────────────────────────────
        static string ToUnityId(string jsonId)
        {
            // "C011" → "Case_011"  |  "C021" → "Case_021"
            string num = jsonId.TrimStart('C');
            return int.TryParse(num, out int n) ? $"Case_{n:D3}" : jsonId;
        }

        static string AbsPath(string relative) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", relative));

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string[] parts  = path.Split('/');
            string   parent = string.Join("/", parts, 0, parts.Length - 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, parts[parts.Length - 1]);
        }

        // ── JSON data classes ─────────────────────────────────────────────
        [Serializable] class JsonRoot       { public JsonDepartment[] departments; }
        [Serializable] class JsonDepartment { public string departmentId; public string displayName; public JsonCase[] cases; }
        [Serializable] class JsonCase
        {
            public string caseId, title, brief;
            public string contradictionClaimId, explanation;
            public string primaryEvidenceIdA, primaryEvidenceIdB;
            public int    basePoints;
            public float  timeLimitSeconds;
            public JsonHotspot[]         hotspots;
            public JsonEvidence[]        evidence;
            public JsonClaim[]           claims;
            public JsonIntNode[]         interrogationNodes;
            public string[]              involvedSuspects;
            public JsonToolConfig        toolConfig;
        }
        [Serializable] class JsonHotspot  { public string hotspotId, label, linkedEvidenceId; }
        [Serializable] class JsonEvidence { public string evidenceId, displayName, description; }
        [Serializable] class JsonClaim    { public string claimId, suspectName, claimText; }
        [Serializable] class JsonToolConfig { public int crossCheckCharges; public float enhanceCooldownSeconds; }
        [Serializable] class JsonIntNode
        {
            public string   nodeId, promptText;
            public string[] responses;
            public int      correctResponseIndex;
            public string[] unlockConditionTags;
            public string[] evidenceRequiredIds;
        }

        [Serializable] class SuspectsWrapper { public JsonSuspect[] suspects; }
        [Serializable] class JsonSuspect
        {
            public string   suspectId, displayName, bio, notes;
            public string[] traits;
            public string[] knownAssociates;
            public string[] linkedCaseIds;
            public float    credibilityScore;
        }
    }
}
#endif
