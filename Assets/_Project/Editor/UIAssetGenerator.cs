using System.IO;
using UnityEditor;
using UnityEngine;

namespace DashAndCollect.Editor
{
    /// <summary>
    /// Generates all UI visual assets for Dash & Collect following:
    ///   - ART-BIBLE.md §9 (UI Style Guide) for colours, spacing, radii, states
    ///   - SPRITE-PIPELINE.md §7 (UI Asset Pipeline) for naming, folders, import, 9-slice, atlas
    ///
    /// Menu: Tools -> Generate UI Assets
    /// </summary>
    public static class UIAssetGenerator
    {
        // ── ART-BIBLE §9.1 — UI Palette ──────────────────────────────────
        static readonly Color32 PanelBg        = HexA("1A1A1A", 0xD9); // 85% opacity
        static readonly Color32 PanelBgOpaque  = Hex("1A1A1A");
        static readonly Color32 PanelBorder    = Hex("E0E0E0");
        static readonly Color32 BtnNormal      = Hex("3E3E3E");
        static readonly Color32 BtnHover       = Hex("5A5A5A");
        static readonly Color32 BtnPressed     = Hex("2A2A2A");
        static readonly Color32 BtnDisabledBorder = Hex("757575");
        static readonly Color32 TextPrimary    = Hex("FAFAFA");
        static readonly Color32 TextSecondary  = Hex("BDBDBD");
        static readonly Color32 TextDisabled   = Hex("757575");

        // ── ART-BIBLE §9.1 / §12 — Accent colours ────────────────────────
        static readonly Color32 AccentDash     = Hex("4FC3F7");
        static readonly Color32 AccentShield   = Hex("FFD54F");
        static readonly Color32 AccentSurge    = Hex("EF5350");
        static readonly Color32 AccentCoin     = Hex("FFEE58");
        static readonly Color32 AccentConfirm  = Hex("66BB6A");

        // ── Derived colours ───────────────────────────────────────────────
        static readonly Color32 BarTrackBg     = Hex("212121");
        static readonly Color32 BarFillGreen   = AccentConfirm;
        static readonly Color32 OverlayBlack50 = HexA("000000", 0x80); // 50% opacity

        static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        const string UIRoot = "Assets/_Project/Art/UI";

        // ════════════════════════════════════════════════════════════════════
        // ENTRY POINT
        // ════════════════════════════════════════════════════════════════════

        [MenuItem("Tools/Generate UI Assets")]
        public static void GenerateAll()
        {
            EnsureDirectories();

            int count = 0;

            // 1. Buttons (4 states each)
            count += GenerateButtonSet("play",     64, 24);
            count += GenerateButtonSet("retry",    64, 24);
            count += GenerateButtonSet("mainmenu", 80, 24);
            count += GenerateButtonSet("generic",  64, 24);

            // 2. Panels (9-slice ready)
            count += GenerateDialogPanel();
            count += GenerateHudPanel();
            count += GenerateTooltipPanel();

            // 3. Icons (16x16 pixel art)
            count += GenerateIconCoin();
            count += GenerateIconHeart();
            count += GenerateIconStar();
            count += GenerateIconGear();
            count += GenerateIconSoundOn();
            count += GenerateIconSoundOff();
            count += GenerateIconPause();

            // 4. Progress bars
            count += GenerateBarBackground();
            count += GenerateBarFill();
            count += GenerateBarBorder();

            // 5. Screen backgrounds
            count += GenerateMenuBackground();
            count += GenerateGameOverOverlay();

            AssetDatabase.Refresh();
            ApplyAllImportSettings();

            Debug.Log($"[UIAssetGenerator] Generated {count} UI assets.");
        }

        // ════════════════════════════════════════════════════════════════════
        // 1. BUTTONS — ART-BIBLE §9.4 (4px radius) / §9.5 (states)
        // ════════════════════════════════════════════════════════════════════

        static int GenerateButtonSet(string element, int w, int h)
        {
            GenerateButton(element, w, h, BtnNormal,  PanelBorder,  "normal");
            GenerateButton(element, w, h, BtnHover,   TextPrimary,  "hover");
            GenerateButton(element, w, h, BtnPressed,  TextSecondary, "pressed");
            GenerateButtonDisabled(element, w, h);
            return 4;
        }

        static void GenerateButton(string element, int w, int h,
            Color32 fill, Color32 border, string state)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            FillClear(tex);

            // Fill with rounded rect (4px corner radius = 1px corner cut)
            DrawRoundedRect(tex, 0, 0, w, h, 1, fill);

            // 1px border with same corner cut
            DrawRoundedBorder(tex, 0, 0, w, h, 1, border);

            SaveUI(tex, $"Buttons/ui-btn-{element}-{state}.png");
        }

        static void GenerateButtonDisabled(string element, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            FillClear(tex);

            // 40% opacity fill — ART-BIBLE §9.5
            Color32 disabledFill = HexA("3E3E3E", 0x66); // 40%
            DrawRoundedRect(tex, 0, 0, w, h, 1, disabledFill);
            DrawRoundedBorder(tex, 0, 0, w, h, 1, BtnDisabledBorder);

            SaveUI(tex, $"Buttons/ui-btn-{element}-disabled.png");
        }

        // ════════════════════════════════════════════════════════════════════
        // 2. PANELS — ART-BIBLE §9.4 (8px radius) / §9.6 (panel style)
        // ════════════════════════════════════════════════════════════════════

        static int GenerateDialogPanel()
        {
            // 96x96, dark semi-transparent, 1px slate border, 8px radius (2px corner cut)
            var tex = new Texture2D(96, 96, TextureFormat.RGBA32, false);
            FillClear(tex);
            DrawRoundedRect(tex, 0, 0, 96, 96, 2, PanelBg);
            DrawRoundedBorder(tex, 0, 0, 96, 96, 2, PanelBorder);
            SaveUI(tex, "Panels/ui-panel-dialog-normal.png");
            return 1;
        }

        static int GenerateHudPanel()
        {
            // 128x32, dark semi-transparent, no border radius (pixel-sharp)
            var tex = new Texture2D(128, 32, TextureFormat.RGBA32, false);
            FillClear(tex);
            FillRect(tex, 0, 0, 128, 32, PanelBg);
            // 1px border, sharp corners
            DrawSharpBorder(tex, 0, 0, 128, 32, PanelBorder);
            SaveUI(tex, "Panels/ui-panel-hud-normal.png");
            return 1;
        }

        static int GenerateTooltipPanel()
        {
            // 64x32, dark with 1px border, 4px radius (1px corner cut)
            var tex = new Texture2D(64, 32, TextureFormat.RGBA32, false);
            FillClear(tex);
            DrawRoundedRect(tex, 0, 0, 64, 32, 1, PanelBg);
            DrawRoundedBorder(tex, 0, 0, 64, 32, 1, PanelBorder);
            SaveUI(tex, "Panels/ui-panel-tooltip-normal.png");
            return 1;
        }

        // ════════════════════════════════════════════════════════════════════
        // 3. ICONS — 16x16 white silhouettes on transparent (ART-BIBLE §9.7)
        // ════════════════════════════════════════════════════════════════════

        static int GenerateIconCoin()
        {
            // Yellow coin matching gameplay coin style (Art Bible §7.2)
            var tex = Create16();
            Color32 coinOuter = AccentCoin;
            Color32 coinInner = Hex("F9A825");
            Color32 coinHi    = Hex("FFFDE7");

            DrawCircleFilled(tex, 7, 7, 6, coinOuter);
            DrawCircleFilled(tex, 7, 7, 4, coinInner);
            DrawCircleFilled(tex, 7, 7, 2, coinHi);
            // Star/cross highlight in centre
            SetPx(tex, 7, 9, coinHi); SetPx(tex, 7, 5, coinHi);
            SetPx(tex, 5, 7, coinHi); SetPx(tex, 9, 7, coinHi);

            SaveUI(tex, "Icons/ui-icon-coin-normal.png");
            return 1;
        }

        static int GenerateIconHeart()
        {
            // Red heart — white silhouette for tinting
            var tex = Create16();
            Color32 w = TextPrimary;

            // Heart shape (pixel art, 16x16)
            //   Row 12-13: two bumps
            SetPx(tex, 3, 12, w); SetPx(tex, 4, 13, w); SetPx(tex, 5, 13, w);
            SetPx(tex, 6, 12, w);
            SetPx(tex, 9, 12, w); SetPx(tex, 10, 13, w); SetPx(tex, 11, 13, w);
            SetPx(tex, 12, 12, w);
            //   Row 11: wide top
            for (int x = 2; x <= 13; x++) SetPx(tex, x, 11, w);
            //   Row 10: full width
            for (int x = 2; x <= 13; x++) SetPx(tex, x, 10, w);
            //   Row 9
            for (int x = 2; x <= 13; x++) SetPx(tex, x, 9, w);
            //   Row 8
            for (int x = 3; x <= 12; x++) SetPx(tex, x, 8, w);
            //   Row 7
            for (int x = 3; x <= 12; x++) SetPx(tex, x, 7, w);
            //   Row 6
            for (int x = 4; x <= 11; x++) SetPx(tex, x, 6, w);
            //   Row 5
            for (int x = 5; x <= 10; x++) SetPx(tex, x, 5, w);
            //   Row 4
            for (int x = 6; x <= 9; x++) SetPx(tex, x, 4, w);
            //   Row 3: bottom point
            SetPx(tex, 7, 3, w); SetPx(tex, 8, 3, w);

            SaveUI(tex, "Icons/ui-icon-heart-normal.png");
            return 1;
        }

        static int GenerateIconStar()
        {
            // Yellow star — white silhouette for tinting
            var tex = Create16();
            Color32 w = TextPrimary;

            // 5-point star, pixel art on 16x16
            // Centre column (top spike)
            SetPx(tex, 7, 14, w); SetPx(tex, 8, 14, w);
            SetPx(tex, 7, 13, w); SetPx(tex, 8, 13, w);
            SetPx(tex, 6, 12, w); SetPx(tex, 7, 12, w); SetPx(tex, 8, 12, w); SetPx(tex, 9, 12, w);
            // Wide bar (arms)
            for (int x = 1; x <= 14; x++) SetPx(tex, x, 10, w);
            for (int x = 2; x <= 13; x++) SetPx(tex, x, 11, w);
            for (int x = 3; x <= 12; x++) SetPx(tex, x, 9, w);
            // Lower body
            for (int x = 4; x <= 11; x++) SetPx(tex, x, 8, w);
            for (int x = 4; x <= 11; x++) SetPx(tex, x, 7, w);
            // Lower legs
            for (int x = 3; x <= 5; x++) SetPx(tex, x, 6, w);
            for (int x = 10; x <= 12; x++) SetPx(tex, x, 6, w);
            for (int x = 2; x <= 4; x++) SetPx(tex, x, 5, w);
            for (int x = 11; x <= 13; x++) SetPx(tex, x, 5, w);
            SetPx(tex, 2, 4, w); SetPx(tex, 3, 4, w);
            SetPx(tex, 12, 4, w); SetPx(tex, 13, 4, w);
            // Centre body fill
            for (int x = 5; x <= 10; x++) SetPx(tex, x, 6, w);
            for (int x = 6; x <= 9; x++) SetPx(tex, x, 5, w);
            // Bottom point
            SetPx(tex, 7, 4, w); SetPx(tex, 8, 4, w);
            SetPx(tex, 7, 3, w); SetPx(tex, 8, 3, w);

            SaveUI(tex, "Icons/ui-icon-star-normal.png");
            return 1;
        }

        static int GenerateIconGear()
        {
            // Settings gear — white silhouette
            var tex = Create16();
            Color32 w = TextPrimary;

            // Gear body: circle with teeth
            DrawCircleFilled(tex, 7, 7, 4, w);
            // Inner hole (clear)
            DrawCircleFilled(tex, 7, 7, 2, Clear);

            // 8 teeth around the perimeter (N, NE, E, SE, S, SW, W, NW)
            int[][] teeth =
            {
                new[] { 7, 13 }, new[] { 7, 12 },  // N
                new[] { 7, 1 },  new[] { 7, 2 },   // S
                new[] { 13, 7 }, new[] { 12, 7 },   // E
                new[] { 1, 7 },  new[] { 2, 7 },    // W
                new[] { 11, 11 }, new[] { 10, 10 }, // NE
                new[] { 3, 11 },  new[] { 4, 10 },  // NW
                new[] { 11, 3 },  new[] { 10, 4 },  // SE
                new[] { 3, 3 },   new[] { 4, 4 },   // SW
            };
            foreach (var t in teeth)
                SetPx(tex, t[0], t[1], w);

            SaveUI(tex, "Icons/ui-icon-gear-normal.png");
            return 1;
        }

        static int GenerateIconSoundOn()
        {
            // Speaker with sound waves — white silhouette
            var tex = Create16();
            Color32 w = TextPrimary;

            // Speaker body (left side)
            FillRect(tex, 2, 5, 3, 6, w);  // Rectangle body
            // Cone (triangle to the right)
            SetPx(tex, 5, 4, w); SetPx(tex, 5, 11, w);
            FillRect(tex, 5, 5, 1, 6, w);
            SetPx(tex, 6, 3, w); SetPx(tex, 6, 12, w);
            FillRect(tex, 6, 4, 1, 8, w);
            SetPx(tex, 7, 3, w); SetPx(tex, 7, 12, w);
            FillRect(tex, 7, 3, 1, 10, w);

            // Sound waves (arcs to the right)
            // Wave 1 (small)
            SetPx(tex, 9, 6, w); SetPx(tex, 9, 7, w); SetPx(tex, 9, 8, w); SetPx(tex, 9, 9, w);
            // Wave 2 (medium)
            SetPx(tex, 11, 5, w); SetPx(tex, 11, 6, w); SetPx(tex, 11, 7, w);
            SetPx(tex, 11, 8, w); SetPx(tex, 11, 9, w); SetPx(tex, 11, 10, w);
            // Wave 3 (large)
            SetPx(tex, 13, 4, w); SetPx(tex, 13, 5, w); SetPx(tex, 13, 6, w);
            SetPx(tex, 13, 7, w); SetPx(tex, 13, 8, w); SetPx(tex, 13, 9, w);
            SetPx(tex, 13, 10, w); SetPx(tex, 13, 11, w);

            SaveUI(tex, "Icons/ui-icon-soundon-normal.png");
            return 1;
        }

        static int GenerateIconSoundOff()
        {
            // Speaker without waves + X mark — white silhouette
            var tex = Create16();
            Color32 w = TextPrimary;

            // Speaker body (same as sound on, no waves)
            FillRect(tex, 2, 5, 3, 6, w);
            SetPx(tex, 5, 4, w); SetPx(tex, 5, 11, w);
            FillRect(tex, 5, 5, 1, 6, w);
            SetPx(tex, 6, 3, w); SetPx(tex, 6, 12, w);
            FillRect(tex, 6, 4, 1, 8, w);
            SetPx(tex, 7, 3, w); SetPx(tex, 7, 12, w);
            FillRect(tex, 7, 3, 1, 10, w);

            // X mark (muted)
            SetPx(tex, 10, 5, w); SetPx(tex, 11, 6, w); SetPx(tex, 12, 7, w);
            SetPx(tex, 13, 8, w); SetPx(tex, 14, 9, w);
            SetPx(tex, 14, 5, w); SetPx(tex, 13, 6, w); // already 12,7
            SetPx(tex, 11, 8, w); SetPx(tex, 10, 9, w);

            SaveUI(tex, "Icons/ui-icon-soundoff-normal.png");
            return 1;
        }

        static int GenerateIconPause()
        {
            // Two vertical bars — white silhouette
            var tex = Create16();
            Color32 w = TextPrimary;

            // Left bar (3px wide)
            FillRect(tex, 4, 3, 3, 10, w);
            // Right bar (3px wide)
            FillRect(tex, 9, 3, 3, 10, w);

            SaveUI(tex, "Icons/ui-icon-pause-normal.png");
            return 1;
        }

        // ════════════════════════════════════════════════════════════════════
        // 4. PROGRESS BARS — SPRITE-PIPELINE §7.4
        // ════════════════════════════════════════════════════════════════════

        static int GenerateBarBackground()
        {
            // 128x8 dark rounded rectangle (4px radius = 1px corner cut)
            var tex = new Texture2D(128, 8, TextureFormat.RGBA32, false);
            FillClear(tex);
            DrawRoundedRect(tex, 0, 0, 128, 8, 1, BarTrackBg);
            SaveUI(tex, "Bars/ui-bar-track-normal.png");
            return 1;
        }

        static int GenerateBarFill()
        {
            // 128x8 green fill, 9-slice for variable width (4px radius = 1px corner cut)
            var tex = new Texture2D(128, 8, TextureFormat.RGBA32, false);
            FillClear(tex);
            DrawRoundedRect(tex, 0, 0, 128, 8, 1, BarFillGreen);
            SaveUI(tex, "Bars/ui-bar-fill-normal.png");
            return 1;
        }

        static int GenerateBarBorder()
        {
            // 128x8 slate border only, transparent fill, 9-slice
            var tex = new Texture2D(128, 8, TextureFormat.RGBA32, false);
            FillClear(tex);
            DrawRoundedBorder(tex, 0, 0, 128, 8, 1, PanelBorder);
            SaveUI(tex, "Bars/ui-bar-border-normal.png");
            return 1;
        }

        // ════════════════════════════════════════════════════════════════════
        // 5. SCREEN BACKGROUNDS — ART-BIBLE §9.6
        // ════════════════════════════════════════════════════════════════════

        static int GenerateMenuBackground()
        {
            // 320x180 dark gradient — slightly different from game background
            var tex = new Texture2D(320, 180, TextureFormat.RGBA32, false);
            Color32 top    = Hex("0D0D1A"); // very dark navy
            Color32 bottom = Hex("1A1A2E"); // slightly lighter dark navy

            for (int y = 0; y < 180; y++)
            {
                float t = y / 179f;
                Color32 row = LerpColor(bottom, top, t);
                for (int x = 0; x < 320; x++)
                    SetPx(tex, x, y, row);
            }

            // Subtle pixel noise for texture (every 7th pixel slightly lighter)
            for (int y = 0; y < 180; y++)
                for (int x = 0; x < 320; x++)
                    if (((x * 7 + y * 13) % 11) == 0)
                        SetPx(tex, x, y, Hex("1F1F33"));

            SaveUI(tex, "Backgrounds/ui-bg-menu-normal.png");
            return 1;
        }

        static int GenerateGameOverOverlay()
        {
            // 320x180 solid black at 50% opacity — ART-BIBLE §9.1 Overlay dim
            var tex = new Texture2D(320, 180, TextureFormat.RGBA32, false);
            var pixels = new Color32[320 * 180];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = OverlayBlack50;
            tex.SetPixels32(pixels);
            SaveUI(tex, "Backgrounds/ui-bg-gameover-normal.png");
            return 1;
        }

        // ════════════════════════════════════════════════════════════════════
        // IMPORT SETTINGS — SPRITE-PIPELINE §7.3, §7.4
        // ════════════════════════════════════════════════════════════════════

        struct UISpec
        {
            public string Path;
            public Vector4 Border; // L, B, R, T for 9-slice (0 = no 9-slice)
        }

        static void ApplyAllImportSettings()
        {
            // ── Buttons: 4px border (1px corner cut fits within) ──
            string[] btnElements = { "play", "retry", "mainmenu", "generic" };
            string[] btnStates = { "normal", "hover", "pressed", "disabled" };
            foreach (string elem in btnElements)
            {
                int w = elem == "mainmenu" ? 80 : 64;
                foreach (string state in btnStates)
                {
                    ApplyUIImport(new UISpec
                    {
                        Path = $"{UIRoot}/Buttons/ui-btn-{elem}-{state}.png",
                        Border = new Vector4(4, 4, 4, 4)
                    });
                }
            }

            // ── Panels: 8px border for dialog/tooltip corners ──
            ApplyUIImport(new UISpec
            {
                Path = $"{UIRoot}/Panels/ui-panel-dialog-normal.png",
                Border = new Vector4(8, 8, 8, 8)
            });
            ApplyUIImport(new UISpec
            {
                Path = $"{UIRoot}/Panels/ui-panel-hud-normal.png",
                Border = new Vector4(8, 8, 8, 8)
            });
            ApplyUIImport(new UISpec
            {
                Path = $"{UIRoot}/Panels/ui-panel-tooltip-normal.png",
                Border = new Vector4(4, 4, 4, 4)
            });

            // ── Icons: no 9-slice ──
            string[] icons =
            {
                "coin", "heart", "star", "gear", "soundon", "soundoff", "pause"
            };
            foreach (string icon in icons)
            {
                ApplyUIImport(new UISpec
                {
                    Path = $"{UIRoot}/Icons/ui-icon-{icon}-normal.png",
                    Border = Vector4.zero
                });
            }

            // ── Bars: 9-slice for horizontal stretch ──
            ApplyUIImport(new UISpec
            {
                Path = $"{UIRoot}/Bars/ui-bar-track-normal.png",
                Border = new Vector4(4, 2, 4, 2)
            });
            ApplyUIImport(new UISpec
            {
                Path = $"{UIRoot}/Bars/ui-bar-fill-normal.png",
                Border = new Vector4(4, 2, 4, 2)
            });
            ApplyUIImport(new UISpec
            {
                Path = $"{UIRoot}/Bars/ui-bar-border-normal.png",
                Border = new Vector4(4, 2, 4, 2)
            });

            // ── Backgrounds: no 9-slice ──
            ApplyUIImport(new UISpec
            {
                Path = $"{UIRoot}/Backgrounds/ui-bg-menu-normal.png",
                Border = Vector4.zero
            });
            ApplyUIImport(new UISpec
            {
                Path = $"{UIRoot}/Backgrounds/ui-bg-gameover-normal.png",
                Border = Vector4.zero
            });
        }

        static void ApplyUIImport(UISpec spec)
        {
            var importer = AssetImporter.GetAtPath(spec.Path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[UIAssetGenerator] Importer not found for {spec.Path}");
                return;
            }

            // SPRITE-PIPELINE §7.3 — standard UI import settings
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            settings.spriteMeshType = SpriteMeshType.FullRect;

            // 9-slice borders (SPRITE-PIPELINE §7.4)
            if (spec.Border != Vector4.zero)
                settings.spriteBorder = spec.Border;

            importer.SetTextureSettings(settings);

            // Android platform override: ETC2 (SPRITE-PIPELINE §7.5)
            var androidOverride = importer.GetPlatformTextureSettings("Android");
            androidOverride.overridden = true;
            androidOverride.format = TextureImporterFormat.ETC2_RGBA8;
            androidOverride.compressionQuality = 50;
            androidOverride.maxTextureSize = 2048;
            importer.SetPlatformTextureSettings(androidOverride);

            importer.SaveAndReimport();
        }

        // ════════════════════════════════════════════════════════════════════
        // DRAWING HELPERS
        // ════════════════════════════════════════════════════════════════════

        static Texture2D Create16()
        {
            var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            FillClear(tex);
            return tex;
        }

        static void FillClear(Texture2D tex)
        {
            var pixels = new Color32[tex.width * tex.height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Clear;
            tex.SetPixels32(pixels);
        }

        static void FillRect(Texture2D tex, int x, int y, int w, int h, Color32 col)
        {
            for (int py = y; py < y + h && py < tex.height; py++)
                for (int px = x; px < x + w && px < tex.width; px++)
                    tex.SetPixel(px, py, col);
        }

        static void SetPx(Texture2D tex, int x, int y, Color32 col)
        {
            if (x >= 0 && x < tex.width && y >= 0 && y < tex.height)
                tex.SetPixel(x, y, col);
        }

        /// <summary>
        /// Filled rounded rectangle with pixel corner cuts.
        /// cornerCut = 1 means 1 pixel removed from each corner (4px radius).
        /// cornerCut = 2 means 2 pixels removed (8px radius).
        /// </summary>
        static void DrawRoundedRect(Texture2D tex, int rx, int ry, int rw, int rh,
            int cornerCut, Color32 col)
        {
            for (int y = ry; y < ry + rh; y++)
            {
                for (int x = rx; x < rx + rw; x++)
                {
                    if (IsCornerCut(x - rx, y - ry, rw, rh, cornerCut))
                        continue;
                    SetPx(tex, x, y, col);
                }
            }
        }

        /// <summary>
        /// 1px border for a rounded rectangle.
        /// </summary>
        static void DrawRoundedBorder(Texture2D tex, int rx, int ry, int rw, int rh,
            int cornerCut, Color32 col)
        {
            for (int y = ry; y < ry + rh; y++)
            {
                for (int x = rx; x < rx + rw; x++)
                {
                    if (IsCornerCut(x - rx, y - ry, rw, rh, cornerCut))
                        continue;

                    bool isBorder = (x == rx || x == rx + rw - 1 ||
                                     y == ry || y == ry + rh - 1);
                    // Also draw the pixel just inside the corner cut as border
                    if (!isBorder)
                    {
                        // Check if any adjacent pixel IS a corner cut — if so, this is an inner border pixel
                        bool adjCut = false;
                        if (x > rx && IsCornerCut(x - rx - 1, y - ry, rw, rh, cornerCut)) adjCut = true;
                        if (x < rx + rw - 1 && IsCornerCut(x - rx + 1, y - ry, rw, rh, cornerCut)) adjCut = true;
                        if (y > ry && IsCornerCut(x - rx, y - ry - 1, rw, rh, cornerCut)) adjCut = true;
                        if (y < ry + rh - 1 && IsCornerCut(x - rx, y - ry + 1, rw, rh, cornerCut)) adjCut = true;
                        if (adjCut) isBorder = true;
                    }

                    if (isBorder)
                        SetPx(tex, x, y, col);
                }
            }
        }

        /// <summary>
        /// Sharp (no rounding) 1px border.
        /// </summary>
        static void DrawSharpBorder(Texture2D tex, int rx, int ry, int rw, int rh, Color32 col)
        {
            for (int x = rx; x < rx + rw; x++)
            {
                SetPx(tex, x, ry, col);
                SetPx(tex, x, ry + rh - 1, col);
            }
            for (int y = ry; y < ry + rh; y++)
            {
                SetPx(tex, rx, y, col);
                SetPx(tex, rx + rw - 1, y, col);
            }
        }

        /// <summary>
        /// Returns true if the local coordinate (lx, ly) within a rect of size (w, h)
        /// falls in a corner-cut region.
        /// cornerCut=1: removes 1 pixel from each corner.
        /// cornerCut=2: removes pixels where dx+dy &lt; 2 for each corner (triangle cut).
        /// </summary>
        static bool IsCornerCut(int lx, int ly, int w, int h, int cornerCut)
        {
            if (cornerCut <= 0) return false;

            // Distance from each corner
            int dLeft = lx;
            int dRight = w - 1 - lx;
            int dBottom = ly;
            int dTop = h - 1 - ly;

            // Bottom-left
            if (dLeft + dBottom < cornerCut) return true;
            // Bottom-right
            if (dRight + dBottom < cornerCut) return true;
            // Top-left
            if (dLeft + dTop < cornerCut) return true;
            // Top-right
            if (dRight + dTop < cornerCut) return true;

            return false;
        }

        static void DrawCircleFilled(Texture2D tex, int cx, int cy, int radius, Color32 col)
        {
            for (int y = cy - radius; y <= cy + radius; y++)
                for (int x = cx - radius; x <= cx + radius; x++)
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius)
                        SetPx(tex, x, y, col);
        }

        static Color32 LerpColor(Color32 a, Color32 b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color32(
                (byte)(a.r + (b.r - a.r) * t),
                (byte)(a.g + (b.g - a.g) * t),
                (byte)(a.b + (b.b - a.b) * t),
                (byte)(a.a + (b.a - a.a) * t)
            );
        }

        static Color32 Hex(string hex)
        {
            ColorUtility.TryParseHtmlString($"#{hex}", out Color color);
            return color;
        }

        static Color32 HexA(string hex, byte alpha)
        {
            ColorUtility.TryParseHtmlString($"#{hex}", out Color color);
            Color32 c = color;
            c.a = alpha;
            return c;
        }

        // ════════════════════════════════════════════════════════════════════
        // FILE I/O
        // ════════════════════════════════════════════════════════════════════

        static void EnsureDirectories()
        {
            string[] dirs =
            {
                $"{UIRoot}/Buttons",
                $"{UIRoot}/Panels",
                $"{UIRoot}/Icons",
                $"{UIRoot}/Bars",
                $"{UIRoot}/Backgrounds",
            };
            foreach (string dir in dirs)
            {
                string full = Path.GetFullPath(dir);
                if (!Directory.Exists(full))
                    Directory.CreateDirectory(full);
            }
        }

        static void SaveUI(Texture2D tex, string relativePath)
        {
            string assetPath = $"{UIRoot}/{relativePath}";
            string fullPath = Path.GetFullPath(assetPath);
            byte[] png = tex.EncodeToPNG();
            File.WriteAllBytes(fullPath, png);
            Object.DestroyImmediate(tex);
        }
    }
}
