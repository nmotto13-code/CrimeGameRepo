#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using CasebookGame.Data;

namespace CasebookGame.Editor
{
    /// <summary>
    /// Menu: Casebook/Generate Starter Cases
    /// Creates or updates all 10 starter CaseData ScriptableObjects.
    /// </summary>
    public static class CaseGeneratorEditor
    {
        const string CASES_PATH = "Assets/ScriptableObjects/Cases";
        const string EVIDENCE_PATH = "Assets/ScriptableObjects/Cases/Evidence";
        const string CLAIMS_PATH = "Assets/ScriptableObjects/Cases/Claims";
        const string RESOURCES_PATH = "Assets/Resources/Cases";

        [MenuItem("Casebook/Generate Starter Cases")]
        public static void GenerateAll()
        {
            EnsureFolders();
            BuildCase001();
            BuildCase002();
            BuildCase003();
            BuildCase004();
            BuildCase005();
            BuildCase006();
            BuildCase007();
            BuildCase008();
            BuildCase009();
            BuildCase010();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CaseGenerator] 10 starter cases created/updated.");
        }

        [MenuItem("Casebook/Play Case 001")]
        public static void PlayCase001()
        {
            EditorApplication.EnterPlaymode();
        }

        // ── Helpers ────────────────────────────────────────────────────

        static void EnsureFolders()
        {
            foreach (var path in new[] { CASES_PATH, EVIDENCE_PATH, CLAIMS_PATH, RESOURCES_PATH })
                if (!AssetDatabase.IsValidFolder(path))
                {
                    var parts = path.Split('/');
                    string parent = string.Join("/", parts, 0, parts.Length - 1);
                    AssetDatabase.CreateFolder(parent, parts[parts.Length - 1]);
                }
        }

        static CaseData GetOrCreate(string caseId)
        {
            string resPath = $"{RESOURCES_PATH}/{caseId}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<CaseData>(resPath);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<CaseData>();
            AssetDatabase.CreateAsset(asset, resPath);
            return asset;
        }

        static EvidenceData MakeEvidence(string caseId, string eid, string name, string desc,
            EvidenceTag[] tags, EvidenceTag[] enhanceTags = null)
        {
            string path = $"{EVIDENCE_PATH}/{caseId}_{eid}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EvidenceData>(path);
            if (existing == null)
            {
                existing = ScriptableObject.CreateInstance<EvidenceData>();
                AssetDatabase.CreateAsset(existing, path);
            }
            existing.evidenceId = eid;
            existing.displayName = name;
            existing.descriptionText = desc;
            existing.tags = new List<EvidenceTag>(tags);
            existing.tagsUnlockedOnEnhance = enhanceTags != null
                ? new List<EvidenceTag>(enhanceTags) : new List<EvidenceTag>();
            EditorUtility.SetDirty(existing);
            return existing;
        }

        static ClaimData MakeClaim(string caseId, string cid, string speaker, string text,
            EvidenceTag[] tags, bool isRedHerring = false)
        {
            string path = $"{CLAIMS_PATH}/{caseId}_{cid}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ClaimData>(path);
            if (existing == null)
            {
                existing = ScriptableObject.CreateInstance<ClaimData>();
                AssetDatabase.CreateAsset(existing, path);
            }
            existing.claimId = cid;
            existing.speakerName = speaker;
            existing.claimText = text;
            existing.referencedTags = new List<EvidenceTag>(tags);
            existing.isRedHerring = isRedHerring;
            EditorUtility.SetDirty(existing);
            return existing;
        }

        static HotspotData MakeHotspot(string hid, float nx, float ny, string eid, string label)
            => new HotspotData { hotspotId = hid, normalizedPosition = new Vector2(nx, ny), evidenceId = eid, hotspotLabel = label };

        static Sprite PlaceholderSprite(Color c)
        {
            var tex = new Texture2D(2, 2);
            tex.SetPixels(new[] { c, c, c, c });
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);
        }

        // ══════════════════════════════════════════════════════════════
        // CASE 001 — "The Missing Watch" (time contradiction)
        // ══════════════════════════════════════════════════════════════
        static void BuildCase001()
        {
            var c = GetOrCreate("Case_001");
            c.caseId = "Case_001";
            c.title = "The Missing Watch";
            c.briefText = "Victor Hale was found dead in his study at 11:45 PM. His antique pocket watch is missing. " +
                          "His assistant claims he last saw Victor alive at midnight — yet the coroner sets death no later than 11 PM.";

            c.evidence = new List<EvidenceData>
            {
                MakeEvidence("C001","E001","Coroner Report",
                    "Time of death estimated between 10:30 PM and 11:00 PM based on body temperature.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.PHYSICAL }),
                MakeEvidence("C001","E002","Stopped Wall Clock",
                    "The study wall clock stopped at 10:47 PM. Face shows impact cracks.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.OBJECT }),
                MakeEvidence("C001","E003","Security Log",
                    "Front door keypad log: last entry at 10:32 PM (Victor Hale's code).",
                    new[]{ EvidenceTag.TIME, EvidenceTag.ACCESS, EvidenceTag.DIGITAL }),
                MakeEvidence("C001","E004","Assistant's Visitor Badge",
                    "Badge swipe shows assistant left the building at 10:15 PM.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.ACCESS, EvidenceTag.ALIBI }),
                MakeEvidence("C001","E005","Watch Box (Empty)",
                    "Victor's monogrammed watch box found open on the desk. Watch absent.",
                    new[]{ EvidenceTag.OBJECT, EvidenceTag.PHYSICAL }),
            };

            c.claims = new List<ClaimData>
            {
                MakeClaim("C001","CL001","Marcus Vane (Assistant)",
                    "I last saw Mr. Hale at midnight. He was fine — I handed him his evening tea.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.ALIBI }),
                MakeClaim("C001","CL002","Eva Mercer (Housekeeper)",
                    "I heard footsteps upstairs around 10:30. I thought it was Mr. Hale going to bed early.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.PHYSICAL }, isRedHerring: true),
                MakeClaim("C001","CL003","Detective Frost",
                    "The clock stopped when it was knocked off the mantle — probably during a struggle.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.OBJECT }, isRedHerring: true),
            };

            c.contradictoryClaimId = "CL001";
            c.primaryEvidenceIdA = "E001";
            c.primaryEvidenceIdB = "E004";
            c.explanationText = "Marcus claims he saw Victor at midnight. The coroner places death before 11 PM " +
                                "and Marcus's own badge shows he left the building at 10:15 PM — " +
                                "he couldn't have been there at midnight.";

            c.hotspots = new List<HotspotData>
            {
                MakeHotspot("H001", 0.3f, 0.6f, "E002", "Wall Clock"),
                MakeHotspot("H002", 0.7f, 0.4f, "E005", "Watch Box"),
            };

            c.toolConfig = new ToolConfig();
            EditorUtility.SetDirty(c);
        }

        // ══════════════════════════════════════════════════════════════
        // CASE 002 — "Locked Room Receipt" (access contradiction)
        // ══════════════════════════════════════════════════════════════
        static void BuildCase002()
        {
            var c = GetOrCreate("Case_002");
            c.caseId = "Case_002";
            c.title = "Locked Room Receipt";
            c.briefText = "A sealed vault at Aldmore Bank was breached. The vault requires two simultaneous key-holders. " +
                          "Branch manager Lena Park claims she never left her office — but the vault log says otherwise.";

            c.evidence = new List<EvidenceData>
            {
                MakeEvidence("C002","E001","Vault Access Log",
                    "Log entry: Vault opened 2:17 PM. Key A: Lena Park. Key B: Desmond Ault.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.ACCESS, EvidenceTag.DIGITAL }),
                MakeEvidence("C002","E002","Office CCTV Still",
                    "Still from office cam: Lena Park visible at her desk at 2:00 PM, 2:10 PM, 2:30 PM. No footage between 2:10 and 2:30.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.DIGITAL }),
                MakeEvidence("C002","E003","Biometric Keypad Log",
                    "Lena Park's thumbprint authenticated at vault keypad at 2:16 PM.",
                    new[]{ EvidenceTag.ACCESS, EvidenceTag.DIGITAL, EvidenceTag.PERSON }),
                MakeEvidence("C002","E004","Vault Door Seal",
                    "Re-lock time stamp: 2:22 PM. Vault was open for ~5 minutes.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.ACCESS }),
                MakeEvidence("C002","E005","Lena's Keycard",
                    "Keycard reader outside the vault corridor: Lena's card scanned at 2:14 PM.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.ACCESS }),
            };

            c.claims = new List<ClaimData>
            {
                MakeClaim("C002","CL001","Lena Park (Branch Manager)",
                    "I was at my desk the entire afternoon. I never went near the vault.",
                    new[]{ EvidenceTag.ACCESS, EvidenceTag.ALIBI, EvidenceTag.TIME }),
                MakeClaim("C002","CL002","Desmond Ault (Security Chief)",
                    "The vault requires simultaneous authentication. No single person can open it alone.",
                    new[]{ EvidenceTag.ACCESS }, isRedHerring: true),
                MakeClaim("C002","CL003","Iris Tan (Teller)",
                    "I saw Lena heading toward the back corridor around 2:15.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.PERSON }, isRedHerring: true),
            };

            c.contradictoryClaimId = "CL001";
            c.primaryEvidenceIdA = "E001";
            c.primaryEvidenceIdB = "E003";
            c.explanationText = "Lena claims she never left her desk, but her biometric thumbprint authenticated at the vault keypad at 2:16 PM " +
                                "and her keycard scanned the vault corridor door at 2:14 PM. She was physically present at the vault.";

            c.hotspots = new List<HotspotData>
            {
                MakeHotspot("H001", 0.5f, 0.5f, "E001", "Vault Door"),
                MakeHotspot("H002", 0.2f, 0.7f, "E003", "Biometric Pad"),
            };

            c.toolConfig = new ToolConfig();
            EditorUtility.SetDirty(c);
        }

        // ══════════════════════════════════════════════════════════════
        // CASE 003 — "Coffee Shop Alibi" (location/time contradiction)
        // ══════════════════════════════════════════════════════════════
        static void BuildCase003()
        {
            var c = GetOrCreate("Case_003");
            c.caseId = "Case_003";
            c.title = "Coffee Shop Alibi";
            c.briefText = "Art appraiser Theo Grant claims he was sipping espresso at Café Lumière when the Harwick Gallery was robbed at 3:10 PM. " +
                          "The café is 40 minutes across town. His receipt tells a different story.";

            c.evidence = new List<EvidenceData>
            {
                MakeEvidence("C003","E001","Café Receipt",
                    "Café Lumière register printout: Theo Grant, 1× espresso, 3:05 PM.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.LOCATION, EvidenceTag.DIGITAL }),
                MakeEvidence("C003","E002","Gallery Alarm Log",
                    "Harwick Gallery motion sensor triggered: 3:08 PM.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.DIGITAL }),
                MakeEvidence("C003","E003","Transit Map",
                    "Café Lumière to Harwick Gallery: 38 minutes by car under optimal conditions.",
                    new[]{ EvidenceTag.LOCATION, EvidenceTag.TIME }),
                MakeEvidence("C003","E004","Gallery CCTV Timestamp",
                    "Blurred figure enters gallery side door at 3:07 PM. Same build as Theo Grant.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.PERSON, EvidenceTag.DIGITAL }),
                MakeEvidence("C003","E005","Credit Card Statement",
                    "Theo's card charged at Café Lumière at 3:05 PM — but card was tapped, not swiped.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.DIGITAL }),
            };

            c.claims = new List<ClaimData>
            {
                MakeClaim("C003","CL001","Theo Grant (Art Appraiser)",
                    "I was at Café Lumière from 3 PM to 3:45 PM. The barista knows me — she'll confirm.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.LOCATION, EvidenceTag.ALIBI }),
                MakeClaim("C003","CL002","Barista Mei (Café Lumière)",
                    "Theo is a regular. I remember making his espresso that afternoon.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.ALIBI }, isRedHerring: true),
                MakeClaim("C003","CL003","Detective Osei",
                    "The gallery side door was forced with a standard pick. No fingerprints.",
                    new[]{ EvidenceTag.PHYSICAL, EvidenceTag.OBJECT }, isRedHerring: true),
            };

            c.contradictoryClaimId = "CL001";
            c.primaryEvidenceIdA = "E001";
            c.primaryEvidenceIdB = "E003";
            c.explanationText = "Theo's receipt shows he paid at the café at 3:05 PM, yet the gallery alarm triggered at 3:08 PM — " +
                                "only 3 minutes later. The café is a minimum 38 minutes away. " +
                                "He could not have been at both locations. His alibi is physically impossible.";

            c.hotspots = new List<HotspotData>
            {
                MakeHotspot("H001", 0.4f, 0.6f, "E001", "Receipt on Table"),
                MakeHotspot("H002", 0.7f, 0.3f, "E002", "Alarm Panel"),
            };

            c.toolConfig = new ToolConfig();
            EditorUtility.SetDirty(c);
        }

        // ══════════════════════════════════════════════════════════════
        // CASE 004 — "Parking Garage Ticket" (vehicle/time)
        // ══════════════════════════════════════════════════════════════
        static void BuildCase004()
        {
            var c = GetOrCreate("Case_004");
            c.caseId = "Case_004";
            c.title = "Parking Garage Ticket";
            c.briefText = "Sasha Novak claims she drove her sedan to Riverside Medical at 1 PM for a checkup. " +
                          "The parking garage ticket and plate reader disagree with the medical center's lobby cam.";

            c.evidence = new List<EvidenceData>
            {
                MakeEvidence("C004","E001","Parking Ticket",
                    "Riverside Medical Garage. Entry: 12:48 PM. Exit: 1:55 PM. Vehicle: Sasha's plate.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.VEHICLE }),
                MakeEvidence("C004","E002","Medical Lobby Cam",
                    "Lobby camera: Sasha Novak NOT seen entering between 12:30 PM and 2:30 PM.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.DIGITAL, EvidenceTag.PERSON }),
                MakeEvidence("C004","E003","Plate Reader Log",
                    "ANPR camera at garage exit: Sasha's plate exiting at 1:55 PM.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.VEHICLE, EvidenceTag.DIGITAL }),
                MakeEvidence("C004","E004","Appointment Record",
                    "Riverside Medical: Sasha's appointment scheduled 1:30 PM. Marked 'No-Show'.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.DIGITAL }),
                MakeEvidence("C004","E005","Fuel Station Receipt",
                    "Gas station 12 km east: Sasha's card charged at 1:22 PM.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.VEHICLE }),
            };

            c.claims = new List<ClaimData>
            {
                MakeClaim("C004","CL001","Sasha Novak",
                    "I drove to Riverside Medical. I sat in the waiting room for over an hour.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.LOCATION, EvidenceTag.ALIBI }),
                MakeClaim("C004","CL002","Dr. Rajan Patel",
                    "Sasha is a patient of mine. She may have left if the wait was long.",
                    new[]{ EvidenceTag.ALIBI }, isRedHerring: true),
                MakeClaim("C004","CL003","Garage Attendant",
                    "I remember her car — a dark blue sedan. Parked on Level 2.",
                    new[]{ EvidenceTag.VEHICLE }, isRedHerring: true),
            };

            c.contradictoryClaimId = "CL001";
            c.primaryEvidenceIdA = "E005";
            c.primaryEvidenceIdB = "E001";
            c.explanationText = "Sasha's card was charged at a fuel station 12 km east at 1:22 PM, " +
                                "yet her car's plate shows it in the Riverside garage from 12:48–1:55 PM. " +
                                "Her car cannot be in two places simultaneously. Someone else drove her vehicle " +
                                "— or she planted the ticket.";

            c.hotspots = new List<HotspotData>{ MakeHotspot("H001", 0.5f, 0.55f, "E001", "Parking Ticket") };
            c.toolConfig = new ToolConfig();
            EditorUtility.SetDirty(c);
        }

        // ══════════════════════════════════════════════════════════════
        // CASE 005 — "Stairwell Footprints" (weather/physical)
        // ══════════════════════════════════════════════════════════════
        static void BuildCase005()
        {
            var c = GetOrCreate("Case_005");
            c.caseId = "Case_005";
            c.title = "Stairwell Footprints";
            c.briefText = "A witness claims they chased a suspect down an outdoor staircase during heavy rain. " +
                          "The forensics team found crisp, undisturbed dust prints — but it rained for 3 hours that evening.";

            c.evidence = new List<EvidenceData>
            {
                MakeEvidence("C005","E001","Weather Report",
                    "City meteorological station: heavy rain 6:00 PM – 9:15 PM.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.WEATHER }),
                MakeEvidence("C005","E002","Forensic Photos",
                    "Staircase landing: dry dust footprints, undisturbed. Prints identified as size 10 boot.",
                    new[]{ EvidenceTag.PHYSICAL, EvidenceTag.OBJECT }),
                MakeEvidence("C005","E003","Staircase Surface Note",
                    "Staircase is uncovered — fully exposed to weather. Concrete surface.",
                    new[]{ EvidenceTag.LOCATION, EvidenceTag.WEATHER }),
                MakeEvidence("C005","E004","Witness Statement Timestamp",
                    "Emergency call made by witness: 7:34 PM (during the rain window).",
                    new[]{ EvidenceTag.TIME }),
                MakeEvidence("C005","E005","Suspect's Boots",
                    "Boots recovered: size 10, treads match staircase prints. But boots are dry.",
                    new[]{ EvidenceTag.PHYSICAL, EvidenceTag.OBJECT }),
            };

            c.claims = new List<ClaimData>
            {
                MakeClaim("C005","CL001","Witness (Petra Vance)",
                    "I saw him flee down the back staircase at about 7:30 PM. It was pouring. I chased him.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.WEATHER, EvidenceTag.PHYSICAL }),
                MakeClaim("C005","CL002","Detective Harlow",
                    "The boot prints are a match — the suspect was definitely on those stairs.",
                    new[]{ EvidenceTag.PHYSICAL }, isRedHerring: true),
                MakeClaim("C005","CL003","Suspect (Dale Marsh)",
                    "I was home all evening. I don't even own those boots.",
                    new[]{ EvidenceTag.ALIBI }, isRedHerring: true),
            };

            c.contradictoryClaimId = "CL001";
            c.primaryEvidenceIdA = "E001";
            c.primaryEvidenceIdB = "E002";
            c.explanationText = "Petra claims the chase happened at 7:30 PM in pouring rain. The staircase is uncovered — " +
                                "any prints made at 7:30 PM would have been washed away by the rain that lasted until 9:15 PM. " +
                                "The dry, crisp dust prints prove the footsteps happened AFTER the rain stopped, not during it.";

            c.hotspots = new List<HotspotData>{ MakeHotspot("H001", 0.45f, 0.6f, "E002", "Footprints") };
            c.toolConfig = new ToolConfig();
            EditorUtility.SetDirty(c);
        }

        // ══════════════════════════════════════════════════════════════
        // CASE 006 — "Delivery Window" (time; Enhance reveals key tag)
        // ══════════════════════════════════════════════════════════════
        static void BuildCase006()
        {
            var c = GetOrCreate("Case_006");
            c.caseId = "Case_006";
            c.title = "Delivery Window";
            c.briefText = "A courier claims he delivered a package to the Orin estate between 2 and 4 PM. " +
                          "The estate owner says no one arrived. Enhance the delivery photo for the hidden timestamp.";

            var photoEvidence = MakeEvidence("C006","E001","Delivery Photo",
                "Courier's phone photo: front gate of Orin estate. Overcast sky. Time metadata stripped.",
                new[]{ EvidenceTag.LOCATION, EvidenceTag.PHYSICAL },
                new[]{ EvidenceTag.TIME }); // Enhance reveals TIME tag + hidden timestamp text
            photoEvidence.enhanceOverlayMaskSprite = null; // Assign a real sprite in Inspector

            c.evidence = new List<EvidenceData>
            {
                photoEvidence,
                MakeEvidence("C006","E002","Courier Dispatch Log",
                    "Dispatch office record: route assigned 1:30 PM. 14 stops. Orin estate is stop #11.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.DIGITAL }),
                MakeEvidence("C006","E003","Gate Camera",
                    "Orin estate gate cam: no vehicle or person detected between 1 PM and 5 PM.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.DIGITAL, EvidenceTag.ACCESS }),
                MakeEvidence("C006","E004","Previous Stop Receipt",
                    "Stop #10 delivery: signed 4:48 PM — after the claimed delivery window.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.DIGITAL }),
                MakeEvidence("C006","E005","Weather Station",
                    "Cloud cover report: overcast sky as in photo matches 5:00 PM – 6:00 PM window.",
                    new[]{ EvidenceTag.WEATHER, EvidenceTag.TIME }),
            };

            c.claims = new List<ClaimData>
            {
                MakeClaim("C006","CL001","Courier (Felix Dunn)",
                    "I delivered the package to the Orin estate at 3:15 PM, within the window. I have a photo.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.LOCATION }),
                MakeClaim("C006","CL002","Estate Owner (Rosaline Orin)",
                    "No one came. My gate would have logged it. The package never arrived.",
                    new[]{ EvidenceTag.ACCESS, EvidenceTag.TIME }, isRedHerring: true),
                MakeClaim("C006","CL003","Dispatch Manager",
                    "Our drivers have freedom to reorder their route — stop 11 could be done any time.",
                    new[]{ EvidenceTag.TIME }, isRedHerring: true),
            };

            c.contradictoryClaimId = "CL001";
            c.primaryEvidenceIdA = "E004";
            c.primaryEvidenceIdB = "E001";
            c.explanationText = "Felix claims he arrived at 3:15 PM, but his previous stop (#10) was signed at 4:48 PM. " +
                                "He hadn't even reached the previous stop yet at 3:15 PM. " +
                                "Enhancing the delivery photo reveals the actual timestamp — well after his claimed window.";

            c.hotspots = new List<HotspotData>{ MakeHotspot("H001", 0.5f, 0.4f, "E001", "Delivery Photo") };
            c.toolConfig = new ToolConfig { crossCheckCharges = 2, enhanceCooldownSeconds = 12f, timelineSnapCharges = 1 };
            EditorUtility.SetDirty(c);
        }

        // ══════════════════════════════════════════════════════════════
        // CASE 007 — "Neighbor Testimony" (statement vs digital)
        // ══════════════════════════════════════════════════════════════
        static void BuildCase007()
        {
            var c = GetOrCreate("Case_007");
            c.caseId = "Case_007";
            c.title = "Neighbor Testimony";
            c.briefText = "Neighbor Greta Lowe says she heard loud music from apartment 4B at 10 PM — proving the resident was home. " +
                          "But the resident's smart speaker app log shows something different.";

            c.evidence = new List<EvidenceData>
            {
                MakeEvidence("C007","E001","Smart Speaker Log",
                    "Resident's smart speaker: last audio playback ended at 8:42 PM. No activity after.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.DIGITAL }),
                MakeEvidence("C007","E002","Greta Lowe Statement",
                    "Written statement: 'I distinctly heard bass-heavy music from 4B at 10 PM. Lasted 20 minutes.'",
                    new[]{ EvidenceTag.TIME, EvidenceTag.ALIBI }),
                MakeEvidence("C007","E003","Building Wifi Log",
                    "Resident's phone connected to home wifi: last ping 8:55 PM. Disconnected afterward.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.DIGITAL }),
                MakeEvidence("C007","E004","Elevator Log",
                    "Elevator RFID: Resident's fob accessed lobby level at 9:03 PM (departing).",
                    new[]{ EvidenceTag.TIME, EvidenceTag.ACCESS }),
                MakeEvidence("C007","E005","Victim's Phone",
                    "Crime scene: victim's phone shows missed call from resident at 9:17 PM.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.DIGITAL, EvidenceTag.PERSON }),
            };

            c.claims = new List<ClaimData>
            {
                MakeClaim("C007","CL001","Greta Lowe (Neighbor)",
                    "The music from 4B was loud at exactly 10 PM — they were definitely home.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.ALIBI }),
                MakeClaim("C007","CL002","Resident (Omar Fitch)",
                    "I went for a walk around 9. The speaker might have played on a timer.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.ALIBI }, isRedHerring: true),
                MakeClaim("C007","CL003","Building Super",
                    "The building's old — sound carries. She might have heard another apartment.",
                    new[]{ EvidenceTag.ALIBI }, isRedHerring: true),
            };

            c.contradictoryClaimId = "CL001";
            c.primaryEvidenceIdA = "E001";
            c.primaryEvidenceIdB = "E002";
            c.explanationText = "Greta claims she heard music from 4B at 10 PM. But the smart speaker log shows audio stopped at 8:42 PM " +
                                "and the wifi log shows the resident's phone disconnected from home at 8:55 PM. " +
                                "The speaker was silent at 10 PM — Greta's testimony is impossible.";

            c.hotspots = new List<HotspotData>{ MakeHotspot("H001", 0.5f, 0.5f, "E001", "Smart Speaker") };
            c.toolConfig = new ToolConfig();
            EditorUtility.SetDirty(c);
        }

        // ══════════════════════════════════════════════════════════════
        // CASE 008 — "Train Platform" (Timeline Snap useful)
        // ══════════════════════════════════════════════════════════════
        static void BuildCase008()
        {
            var c = GetOrCreate("Case_008");
            c.caseId = "Case_008";
            c.title = "Train Platform";
            c.briefText = "Suspect Carl Duval says he boarded the 6:30 PM express to Alton and couldn't have been near the warehouse. " +
                          "Two TIME-tagged pieces of evidence tell a different story — use Timeline Snap on the board.";

            c.evidence = new List<EvidenceData>
            {
                MakeEvidence("C008","E001","Train Ticket",
                    "Ticket: Carl Duval. 6:30 PM Alton Express. Purchased digitally at 6:29 PM.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.DIGITAL }),
                MakeEvidence("C008","E002","Warehouse Sensor",
                    "Motion sensor at warehouse loading dock: triggered 6:34 PM.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.DIGITAL }),
                MakeEvidence("C008","E003","Train Platform Camera",
                    "Platform cam: Train departs 6:31 PM. Carl Duval NOT visible boarding.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.PERSON, EvidenceTag.DIGITAL }),
                MakeEvidence("C008","E004","Warehouse Location",
                    "Warehouse is 18 minutes from Central Station by car.",
                    new[]{ EvidenceTag.LOCATION, EvidenceTag.TIME }),
                MakeEvidence("C008","E005","Conductor Log",
                    "Conductor: passenger manifest for 6:30 train. Carl Duval's seat: empty.",
                    new[]{ EvidenceTag.ALIBI, EvidenceTag.DIGITAL }),
            };

            c.claims = new List<ClaimData>
            {
                MakeClaim("C008","CL001","Carl Duval",
                    "I bought my ticket and boarded the 6:30 express. Check the manifest.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.ALIBI }),
                MakeClaim("C008","CL002","Station Staff",
                    "The 6:30 was on time. Several passengers boarded last-minute.",
                    new[]{ EvidenceTag.TIME }, isRedHerring: true),
                MakeClaim("C008","CL003","Carl's colleague",
                    "Carl called me from the train at 6:40 PM. He sounded like he was moving.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.ALIBI }, isRedHerring: true),
            };

            c.contradictoryClaimId = "CL001";
            c.primaryEvidenceIdA = "E001";
            c.primaryEvidenceIdB = "E002";
            c.explanationText = "Carl claims he boarded the 6:30 PM train. The train departed at 6:31 PM, " +
                                "but the warehouse sensor triggered at 6:34 PM — 3 minutes after the train left, " +
                                "and 18 minutes from the station. Carl cannot be on a moving train and triggering a warehouse sensor simultaneously.";

            c.hotspots = new List<HotspotData>{ MakeHotspot("H001", 0.4f, 0.45f, "E001", "Train Ticket") };
            c.toolConfig = new ToolConfig();
            EditorUtility.SetDirty(c);
        }

        // ══════════════════════════════════════════════════════════════
        // CASE 009 — "Gym Keycard" (access logs)
        // ══════════════════════════════════════════════════════════════
        static void BuildCase009()
        {
            var c = GetOrCreate("Case_009");
            c.caseId = "Case_009";
            c.title = "Gym Keycard";
            c.briefText = "Fitness instructor Nadia Clare claims she was teaching a 7 PM spin class when a client's locker was raided. " +
                          "The gym's keycard system and class booking log disagree.";

            c.evidence = new List<EvidenceData>
            {
                MakeEvidence("C009","E001","Class Booking System",
                    "7 PM Spin class: booked but marked 'Cancelled by Instructor' at 6:52 PM.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.DIGITAL }),
                MakeEvidence("C009","E002","Keycard Entry Log",
                    "Nadia's keycard: entered back staff corridor at 7:04 PM — near locker room.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.ACCESS }),
                MakeEvidence("C009","E003","Studio Cam",
                    "Spin studio camera: empty at 7:00 PM, 7:05 PM, 7:10 PM. No class in session.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.DIGITAL }),
                MakeEvidence("C009","E004","Victim's Locker",
                    "Locker 114: lock forced. Victim's wallet missing. Locker near staff corridor.",
                    new[]{ EvidenceTag.ACCESS, EvidenceTag.PHYSICAL }),
                MakeEvidence("C009","E005","Witness (Class Member)",
                    "Class attendee arrived at 7 PM: 'The studio was dark. I assumed it was cancelled.'",
                    new[]{ EvidenceTag.TIME, EvidenceTag.ALIBI }),
            };

            c.claims = new List<ClaimData>
            {
                MakeClaim("C009","CL001","Nadia Clare (Instructor)",
                    "I taught my spin class from 7 to 8 PM as usual. Ask anyone who attended.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.ALIBI, EvidenceTag.PERSON }),
                MakeClaim("C009","CL002","Gym Manager",
                    "Nadia has been with us three years. She's never had a complaint.",
                    new[]{ EvidenceTag.PERSON }, isRedHerring: true),
                MakeClaim("C009","CL003","Another Trainer",
                    "I heard music from the spin studio around 7. Could have been Nadia.",
                    new[]{ EvidenceTag.ALIBI }, isRedHerring: true),
            };

            c.contradictoryClaimId = "CL001";
            c.primaryEvidenceIdA = "E001";
            c.primaryEvidenceIdB = "E003";
            c.explanationText = "Nadia claims she taught a class at 7 PM, but her own booking system shows she cancelled it at 6:52 PM. " +
                                "Studio cameras show the room empty at 7 PM. She was not teaching — her keycard places her in the staff corridor near the locker room.";

            c.hotspots = new List<HotspotData>{ MakeHotspot("H001", 0.5f, 0.5f, "E002", "Keycard Panel") };
            c.toolConfig = new ToolConfig();
            EditorUtility.SetDirty(c);
        }

        // ══════════════════════════════════════════════════════════════
        // CASE 010 — "Gallery Opening" (multi-evidence contradiction)
        // ══════════════════════════════════════════════════════════════
        static void BuildCase010()
        {
            var c = GetOrCreate("Case_010");
            c.caseId = "Case_010";
            c.title = "Gallery Opening";
            c.briefText = "Art dealer Simon Rowe says he spent the full evening at the Aldene Gallery opening. " +
                          "Three separate pieces of evidence each chip away at this alibi — converging on one impossible claim.";

            c.evidence = new List<EvidenceData>
            {
                MakeEvidence("C010","E001","Guest Sign-In Sheet",
                    "Gallery opening sign-in: Simon Rowe, arrival 7:05 PM. No departure time logged.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.PERSON }),
                MakeEvidence("C010","E002","Valet Parking Ticket",
                    "Valet log: Simon's Jaguar retrieved at 8:22 PM. Gallery closing was 11 PM.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.VEHICLE }),
                MakeEvidence("C010","E003","Photo Evidence",
                    "Press photographer's images timestamped: Simon in background at 7:10 PM — not seen in any shots after 8 PM.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.PERSON, EvidenceTag.DIGITAL }),
                MakeEvidence("C010","E004","Rival's Statement",
                    "Gallery rival Cora Webb: 'Simon approached me around 10 PM to discuss the Harlow piece.'",
                    new[]{ EvidenceTag.TIME, EvidenceTag.ALIBI }),
                MakeEvidence("C010","E005","Warehouse Break-in Report",
                    "Warehouse storing the Harlow piece broken into at 9:15 PM. 2.4 km from gallery.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.LOCATION }),
                MakeEvidence("C010","E006","Gallery Sommelier",
                    "Sommelier filled glasses for the main hall guests continuously 8–11 PM. No recollection of Simon.",
                    new[]{ EvidenceTag.ALIBI, EvidenceTag.TIME }),
            };

            c.claims = new List<ClaimData>
            {
                MakeClaim("C010","CL001","Simon Rowe (Art Dealer)",
                    "I was at the gallery all night — arrived at 7 and stayed until the champagne was gone.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.ALIBI, EvidenceTag.PERSON }),
                MakeClaim("C010","CL002","Cora Webb (Gallery Rival)",
                    "Simon talked to me around 10 PM. He seemed distracted.",
                    new[]{ EvidenceTag.TIME, EvidenceTag.ALIBI }, isRedHerring: true),
                MakeClaim("C010","CL003","Event Photographer",
                    "I don't photograph everyone — Simon could have been there without appearing in my shots.",
                    new[]{ EvidenceTag.PERSON }, isRedHerring: true),
                MakeClaim("C010","CL004","Detective Lam",
                    "The Harlow piece was the primary target. Someone with knowledge of its location planned this.",
                    new[]{ EvidenceTag.OBJECT }, isRedHerring: true),
            };

            c.contradictoryClaimId = "CL001";
            c.primaryEvidenceIdA = "E002";
            c.primaryEvidenceIdB = "E005";
            c.explanationText = "Simon claims he stayed all evening. His valet ticket shows his car left at 8:22 PM — nearly 3 hours before closing. " +
                                "Press photos confirm he vanished from sight after 8 PM. The warehouse break-in occurred at 9:15 PM, " +
                                "giving Simon time to drive there after retrieving his car. He was not at the gallery all night.";

            c.hotspots = new List<HotspotData>
            {
                MakeHotspot("H001", 0.3f, 0.5f, "E001", "Sign-In Desk"),
                MakeHotspot("H002", 0.7f, 0.6f, "E002", "Valet Ticket"),
            };

            c.toolConfig = new ToolConfig();
            EditorUtility.SetDirty(c);
        }
    }
}
#endif
