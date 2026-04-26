#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

namespace CasebookGame.Editor
{
    public static class SharedSpriteGenerator
    {
        const string SHARED_PATH = "Assets/Sprites/Evidence/shared";

        [MenuItem("Casebook/Generate Missing Shared Sprites")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath,
                "../" + SHARED_PATH.Replace("Assets/", "")));

            GenerateReceipt();
            GenerateCCTV();
            GenerateEvidenceBag();

            AssetDatabase.Refresh();
            Debug.Log("[SharedSpriteGenerator] Done — receipt, cctv_still, evidence_bag created.");
        }

        // ── Thermal receipt — cream paper, ruled lines ─────────────────
        static void GenerateReceipt()
        {
            int w = 400, h = 700;
            var tex = new Texture2D(w, h);
            var cream = new Color(0.98f, 0.97f, 0.90f);
            Fill(tex, cream);

            // Subtle horizontal rules every 28px
            var rule = new Color(0.85f, 0.84f, 0.76f);
            for (int y = 20; y < h - 20; y += 28)
                DrawHLine(tex, y, 24, w - 24, rule);

            // Header dark band
            var header = new Color(0.22f, 0.22f, 0.22f);
            for (int y = h - 60; y < h - 10; y++)
                DrawHLine(tex, y, 10, w - 10, header);

            // Torn-edge bottom
            for (int x = 0; x < w; x++)
            {
                int tear = 6 + (int)(Mathf.PerlinNoise(x * 0.15f, 0) * 10f);
                for (int y = 0; y < tear; y++)
                    tex.SetPixel(x, y, new Color(0, 0, 0, 0));
            }

            tex.Apply();
            Save(tex, SHARED_PATH + "/generic_receipt.png");
            Object.DestroyImmediate(tex);
        }

        // ── CCTV still — dark, scanlines, timestamp bar ────────────────
        static void GenerateCCTV()
        {
            int w = 640, h = 480;
            var tex = new Texture2D(w, h);
            var bg = new Color(0.12f, 0.12f, 0.14f);
            Fill(tex, bg);

            // Scan lines (lighter every other line)
            for (int y = 0; y < h; y += 2)
                DrawHLine(tex, y, 0, w, new Color(0.18f, 0.18f, 0.20f));

            // Subtle vignette corners
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                float dx = (x - w * 0.5f) / (w * 0.5f);
                float dy = (y - h * 0.5f) / (h * 0.5f);
                float vignette = Mathf.Clamp01(1f - (dx * dx + dy * dy) * 0.7f);
                var c = tex.GetPixel(x, y);
                tex.SetPixel(x, y, c * vignette);
            }

            // Timestamp bar at bottom
            var tsBar = new Color(0.05f, 0.05f, 0.05f, 0.90f);
            for (int y = 0; y < 28; y++)
                DrawHLine(tex, y, 0, w, tsBar);

            // Blinking REC dot (red dot top-left)
            DrawFilledRect(tex, 12, h - 22, 10, 10, new Color(0.9f, 0.1f, 0.1f));

            tex.Apply();
            Save(tex, SHARED_PATH + "/generic_cctv_still.png");
            Object.DestroyImmediate(tex);
        }

        // ── Evidence bag — clear plastic with label ────────────────────
        static void GenerateEvidenceBag()
        {
            int w = 512, h = 640;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Fill(tex, new Color(0, 0, 0, 0)); // transparent

            // Bag body — pale blue-grey semi-transparent
            var bagColor = new Color(0.78f, 0.85f, 0.90f, 0.55f);
            DrawFilledRect(tex, 20, 60, w - 40, h - 120, bagColor);

            // Bag border
            var border = new Color(0.50f, 0.62f, 0.72f, 0.90f);
            DrawBorder(tex, 20, 60, w - 40, h - 120, 3, border);

            // Zip seal at top — dark stripe
            var zip = new Color(0.30f, 0.40f, 0.52f, 1.0f);
            DrawFilledRect(tex, 20, h - 80, w - 40, 28, zip);

            // White label area
            DrawFilledRect(tex, 60, 100, w - 120, 120, new Color(1f, 1f, 1f, 0.88f));
            DrawBorder(tex, 60, 100, w - 120, 120, 2, new Color(0.6f, 0.6f, 0.6f));

            tex.Apply();
            Save(tex, SHARED_PATH + "/generic_evidence_bag.png");
            Object.DestroyImmediate(tex);
        }

        // ── Pixel helpers ──────────────────────────────────────────────

        static void Fill(Texture2D t, Color c)
        {
            for (int x = 0; x < t.width; x++)
            for (int y = 0; y < t.height; y++)
                t.SetPixel(x, y, c);
        }

        static void DrawHLine(Texture2D t, int y, int x0, int x1, Color c)
        {
            if (y < 0 || y >= t.height) return;
            for (int x = x0; x < x1 && x < t.width; x++)
                t.SetPixel(x, y, c);
        }

        static void DrawFilledRect(Texture2D t, int x, int y, int w, int h, Color c)
        {
            for (int px = x; px < x + w && px < t.width; px++)
            for (int py = y; py < y + h && py < t.height; py++)
                t.SetPixel(px, py, c);
        }

        static void DrawBorder(Texture2D t, int x, int y, int w, int h, int thick, Color c)
        {
            for (int i = 0; i < thick; i++)
            {
                DrawHLine(t, y + i,         x, x + w, c);
                DrawHLine(t, y + h - 1 - i, x, x + w, c);
                for (int py = y; py < y + h; py++)
                {
                    if (x + i < t.width)           t.SetPixel(x + i, py, c);
                    if (x + w - 1 - i < t.width)   t.SetPixel(x + w - 1 - i, py, c);
                }
            }
        }

        static void Save(Texture2D tex, string assetPath)
        {
            var bytes = tex.EncodeToPNG();
            var full  = Path.Combine(Application.dataPath,
                            assetPath.Replace("Assets/", ""));
            File.WriteAllBytes(full, bytes);
        }
    }
}
#endif
