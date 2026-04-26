#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using CasebookGame.Data;

namespace CasebookGame.Editor
{
    /// <summary>
    /// Auto-assigns shared generic sprites to all evidence assets that use
    /// [UNITY UI] or [REUSE] types. AI-GENERATE items are left null so the
    /// artist knows they still need a unique image.
    /// Run: Casebook → Wire Evidence Sprites
    /// </summary>
    public static class EvidenceSpriteWirer
    {
        // Sprite asset paths
        const string DOC      = "Assets/Sprites/Evidence/shared/generic_document.png";
        const string TERMINAL = "Assets/Sprites/Evidence/shared/generic_terminal.png";
        const string KEYCARD  = "Assets/Sprites/Evidence/shared/generic_keycard.png";
        const string CCTV     = "Assets/Sprites/Evidence/shared/generic_cctv_still.png";
        const string RECEIPT  = "Assets/Sprites/Evidence/shared/generic_receipt.png";
        const string BAG      = "Assets/Sprites/Evidence/shared/generic_evidence_bag.png";
        const string AI       = null; // Needs unique AI-generated image — skip

        // Map: "C00X_E00Y" → sprite path (null = AI-generate, leave as-is)
        static readonly Dictionary<string, string> Map = new()
        {
            // Case 002 — Locked Room Receipt
            { "C002_E001", TERMINAL },  // Vault Access Log
            { "C002_E002", AI       },  // CCTV Still (unique)
            { "C002_E003", AI       },  // Biometric Keypad (unique)
            { "C002_E004", DOC      },  // Vault Door Seal
            { "C002_E005", KEYCARD  },  // Lena's Keycard

            // Case 003 — Coffee Shop Alibi
            { "C003_E001", RECEIPT  },  // Café Receipt
            { "C003_E002", TERMINAL },  // Gallery Alarm Log
            { "C003_E003", AI       },  // Transit Map (unique)
            { "C003_E004", CCTV     },  // Gallery CCTV
            { "C003_E005", DOC      },  // Credit Card Statement

            // Case 004 — Parking Garage Ticket
            { "C004_E001", RECEIPT  },  // Parking Ticket
            { "C004_E002", CCTV     },  // Medical Lobby Cam
            { "C004_E003", TERMINAL },  // Plate Reader Log
            { "C004_E004", DOC      },  // Appointment Record
            { "C004_E005", RECEIPT  },  // Fuel Station Receipt

            // Case 005 — Stairwell Footprints
            { "C005_E001", DOC      },  // Weather Report
            { "C005_E002", AI       },  // Forensic Footprint Photos (unique)
            { "C005_E003", DOC      },  // Staircase Note
            { "C005_E004", DOC      },  // Witness Timestamp
            { "C005_E005", AI       },  // Suspect's Boots (unique)

            // Case 006 — Delivery Window
            { "C006_E001", AI       },  // Delivery Photo (unique)
            { "C006_E002", DOC      },  // Courier Log
            { "C006_E003", CCTV     },  // Gate Camera
            { "C006_E004", RECEIPT  },  // Previous Stop Receipt
            { "C006_E005", DOC      },  // Weather Station Report

            // Case 007 — Neighbor Testimony
            { "C007_E001", AI       },  // Smart Speaker (unique)
            { "C007_E002", DOC      },  // Greta's Statement
            { "C007_E003", TERMINAL },  // Building Wi-Fi Log
            { "C007_E004", TERMINAL },  // Elevator Log
            { "C007_E005", AI       },  // Victim's Phone (unique)

            // Case 008 — Train Platform
            { "C008_E001", RECEIPT  },  // Train Ticket
            { "C008_E002", TERMINAL },  // Warehouse Motion Sensor
            { "C008_E003", CCTV     },  // Platform Camera
            { "C008_E004", AI       },  // Warehouse Location Map (unique)
            { "C008_E005", DOC      },  // Conductor Log

            // Case 009 — Gym Keycard
            { "C009_E001", TERMINAL },  // Class Booking System
            { "C009_E002", TERMINAL },  // Keycard Entry Log
            { "C009_E003", CCTV     },  // Studio Camera
            { "C009_E004", AI       },  // Victim's Locker (unique)
            { "C009_E005", DOC      },  // Witness Statement

            // Case 010 — Gallery Opening
            { "C010_E001", DOC      },  // Guest Sign-In Sheet
            { "C010_E002", RECEIPT  },  // Valet Ticket
            { "C010_E003", CCTV     },  // Photo Evidence
            { "C010_E004", DOC      },  // Rival's Statement
            { "C010_E005", DOC      },  // Warehouse Report
            { "C010_E006", DOC      },  // Gallery Sommelier Statement
        };

        [MenuItem("Casebook/Wire Evidence Sprites")]
        public static void WireAll()
        {
            int wired = 0, skipped = 0;

            var guids = AssetDatabase.FindAssets("t:EvidenceData",
                new[] { "Assets/ScriptableObjects/Cases/Evidence" });

            foreach (var guid in guids)
            {
                var path     = AssetDatabase.GUIDToAssetPath(guid);
                var evidence = AssetDatabase.LoadAssetAtPath<EvidenceData>(path);
                if (evidence == null) continue;

                // Derive key from filename e.g. "C001_E002"
                var key = System.IO.Path.GetFileNameWithoutExtension(path);

                if (!Map.TryGetValue(key, out var spritePath))
                {
                    Debug.Log($"[EvidenceWirer] No mapping for {key} — skipped");
                    skipped++;
                    continue;
                }

                if (spritePath == null)
                {
                    // AI-generate — skip, leave null
                    skipped++;
                    continue;
                }

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (sprite == null)
                {
                    Debug.LogWarning($"[EvidenceWirer] Sprite not found at {spritePath} — run 'Generate Missing Shared Sprites' first");
                    skipped++;
                    continue;
                }

                var so = new SerializedObject(evidence);
                var prop = so.FindProperty("imageSprite");
                if (prop.objectReferenceValue != null)
                {
                    // Already has a sprite — don't overwrite
                    skipped++;
                    continue;
                }

                prop.objectReferenceValue = sprite;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(evidence);
                wired++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Evidence Sprite Wirer",
                $"Done.\n\nWired:   {wired}\nSkipped: {skipped} (already set, AI-generate, or no mapping)",
                "OK");

            Debug.Log($"[EvidenceWirer] Wired {wired}, skipped {skipped}");
        }
    }
}
#endif
