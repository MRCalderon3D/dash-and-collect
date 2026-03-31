using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DashAndCollect.Editor
{
    /// <summary>
    /// Generates final pixel art sprites for Dash & Collect following the Art Bible palette
    /// and SPRITE-PIPELINE.md specifications. Replaces M2/M3 placeholders and creates missing
    /// assets for M4.
    ///
    /// Menu: Tools → Dash & Collect → Generate Sprites
    /// </summary>
    public static class SpriteAssetGenerator
    {
        // ── Art Bible §12 — Reserved Gameplay Colours ──────────────────────
        static readonly Color32 DashBlue       = Hex("4FC3F7");
        static readonly Color32 DashBlueDark   = Hex("0288D1");
        static readonly Color32 ShieldGold     = Hex("FFD54F");
        static readonly Color32 ShieldGoldDark = Hex("F9A825");
        static readonly Color32 SurgeRed       = Hex("EF5350");
        static readonly Color32 SurgeRedDark   = Hex("C62828");
        static readonly Color32 CoinYellow     = Hex("FFEE58");
        static readonly Color32 CoinYellowDark = Hex("F9A825");
        static readonly Color32 CoinHighlight  = Hex("FFFDE7");

        // ── Art Bible §12.1 — Player & Road ────────────────────────────────
        static readonly Color32 PlayerWhite    = Hex("FAFAFA");
        static readonly Color32 PlayerGrey     = Hex("BDBDBD");
        static readonly Color32 PlayerDark     = Hex("616161");
        static readonly Color32 PlayerAccent   = Hex("4FC3F7"); // Blue accent stripe
        static readonly Color32 PlayerWindshield = Hex("90CAF9");

        static readonly Color32 RoadDark       = Hex("3E3E3E");
        static readonly Color32 RoadMid        = Hex("4A4A4A");
        static readonly Color32 LaneLine       = Hex("E0E0E0");

        // ── Art Bible §12.2 — Coastal Highway Palette ──────────────────────
        static readonly Color32 SkyTop         = Hex("FF8A65");
        static readonly Color32 SkyBottom      = Hex("4DD0E1");
        static readonly Color32 PalmTrunk      = Hex("8D6E63");
        static readonly Color32 PalmFoliage    = Hex("66BB6A");
        static readonly Color32 Sand           = Hex("FFCC80");
        static readonly Color32 OceanFar       = Hex("26C6DA");

        // ── Art Bible §6.1 — Traffic car colours (Coastal biome) ───────────
        static readonly Color32 TrafficPurple     = Hex("7E57C2");
        static readonly Color32 TrafficPurpleDark = Hex("512DA8");
        static readonly Color32 TrafficWindshield = Hex("B39DDB");

        static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        const string SpritesRoot = "Assets/Art/Sprites";

        [MenuItem("Tools/Dash & Collect/Generate Sprites")]
        public static void GenerateAll()
        {
            EnsureDirectories();

            int count = 0;
            count += GeneratePlayer();
            count += GenerateHazard();
            count += GenerateCoin();
            count += GenerateCollectibleDash();
            count += GenerateCollectibleShield();
            count += GenerateCollectibleSurge();
            count += GenerateGroundTile();
            count += GenerateBackgroundFar();
            count += GenerateBackgroundMid();
            count += GenerateBackgroundNear();

            AssetDatabase.Refresh();

            ApplyAllImportSettings();
            UpdatePrefabReferences();

            Debug.Log($"[SpriteAssetGenerator] Generated {count} sprites. Prefab references updated.");
        }

        // ════════════════════════════════════════════════════════════════════
        // SPRITE GENERATORS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Player car — 16x32 top-down retro sports car (Art Bible §5).
        /// White body, blue accent stripe, visible windshield.
        /// </summary>
        static int GeneratePlayer()
        {
            var tex = new Texture2D(16, 32, TextureFormat.RGBA32, false);
            FillClear(tex);

            // Car body outline (rounded rectangle, bottom-up)
            // Row 0-1: rear bumper
            FillRect(tex, 3, 0, 10, 2, PlayerGrey);
            // Row 2-3: tail lights (red accents at edges)
            FillRect(tex, 2, 2, 12, 2, PlayerWhite);
            SetPx(tex, 2, 2, SurgeRed); SetPx(tex, 3, 2, SurgeRed);
            SetPx(tex, 12, 2, SurgeRed); SetPx(tex, 13, 2, SurgeRed);
            SetPx(tex, 2, 3, SurgeRed); SetPx(tex, 13, 3, SurgeRed);
            // Row 4-7: rear body
            FillRect(tex, 1, 4, 14, 4, PlayerWhite);
            FillRect(tex, 2, 4, 12, 4, PlayerWhite); // ensure fill
            // Row 8-9: accent stripe (blue, runs across width)
            FillRect(tex, 1, 8, 14, 2, PlayerAccent);
            // Row 10-14: mid body
            FillRect(tex, 1, 10, 14, 5, PlayerWhite);
            // Row 15-18: windshield area
            FillRect(tex, 1, 15, 14, 4, PlayerWhite);
            FillRect(tex, 3, 16, 10, 2, PlayerWindshield);
            // Row 19-23: hood
            FillRect(tex, 1, 19, 14, 5, PlayerWhite);
            // Row 24-25: front accent stripe
            FillRect(tex, 1, 24, 14, 2, PlayerAccent);
            // Row 26-29: front body / nose
            FillRect(tex, 2, 26, 12, 3, PlayerWhite);
            FillRect(tex, 3, 29, 10, 2, PlayerGrey);
            // Row 30-31: front bumper
            FillRect(tex, 4, 30, 8, 2, PlayerGrey);

            // Side edges — darker to show body contour
            for (int y = 4; y < 30; y++)
            {
                SetPx(tex, 1, y, PlayerGrey);
                SetPx(tex, 14, y, PlayerGrey);
            }
            // Fender curves
            SetPx(tex, 0, 6, PlayerGrey); SetPx(tex, 0, 7, PlayerGrey);
            SetPx(tex, 15, 6, PlayerGrey); SetPx(tex, 15, 7, PlayerGrey);
            SetPx(tex, 0, 22, PlayerGrey); SetPx(tex, 0, 23, PlayerGrey);
            SetPx(tex, 15, 22, PlayerGrey); SetPx(tex, 15, 23, PlayerGrey);

            // Headlights
            SetPx(tex, 3, 29, CoinYellow); SetPx(tex, 4, 29, CoinYellow);
            SetPx(tex, 11, 29, CoinYellow); SetPx(tex, 12, 29, CoinYellow);

            SaveSprite(tex, "Player/player-default-idle.png");
            return 1;
        }

        /// <summary>
        /// Hazard — 16x32 traffic car (Art Bible §6.1).
        /// Purple sedan, boxy silhouette, top-down.
        /// </summary>
        static int GenerateHazard()
        {
            var tex = new Texture2D(16, 32, TextureFormat.RGBA32, false);
            FillClear(tex);

            // Boxy sedan shape — wider and stubbier than player
            // Rear bumper
            FillRect(tex, 3, 0, 10, 2, TrafficPurpleDark);
            // Rear body
            FillRect(tex, 2, 2, 12, 6, TrafficPurple);
            // Rear windshield
            FillRect(tex, 3, 7, 10, 2, TrafficWindshield);
            // Roof
            FillRect(tex, 2, 9, 12, 8, TrafficPurple);
            // Front windshield
            FillRect(tex, 3, 17, 10, 3, TrafficWindshield);
            // Hood
            FillRect(tex, 2, 20, 12, 7, TrafficPurple);
            // Front bumper
            FillRect(tex, 3, 27, 10, 3, TrafficPurpleDark);
            // Front cap
            FillRect(tex, 4, 30, 8, 2, TrafficPurpleDark);

            // Side contour
            for (int y = 2; y < 28; y++)
            {
                SetPx(tex, 2, y, TrafficPurpleDark);
                SetPx(tex, 13, y, TrafficPurpleDark);
            }

            // Tail lights (amber)
            SetPx(tex, 3, 1, ShieldGold); SetPx(tex, 4, 1, ShieldGold);
            SetPx(tex, 11, 1, ShieldGold); SetPx(tex, 12, 1, ShieldGold);

            // Headlights (white)
            SetPx(tex, 4, 29, PlayerWhite); SetPx(tex, 5, 29, PlayerWhite);
            SetPx(tex, 10, 29, PlayerWhite); SetPx(tex, 11, 29, PlayerWhite);

            SaveSprite(tex, "Obstacles/hazard-block-idle.png");
            return 1;
        }

        /// <summary>
        /// Coin — 16x16 yellow circle with inner highlight (Art Bible §7.2).
        /// </summary>
        static int GenerateCoin()
        {
            var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            FillClear(tex);
            DrawCircleFilled(tex, 8, 8, 6, CoinYellow);
            DrawCircleFilled(tex, 8, 8, 4, CoinYellowDark);
            DrawCircleFilled(tex, 8, 8, 2, CoinHighlight);
            // Star detail in centre
            SetPx(tex, 8, 10, CoinHighlight);
            SetPx(tex, 8, 6, CoinHighlight);
            SetPx(tex, 6, 8, CoinHighlight);
            SetPx(tex, 10, 8, CoinHighlight);
            SaveSprite(tex, "Collectibles/coin-default-idle.png");
            return 1;
        }

        /// <summary>
        /// Dash collectible — 16x16 blue chevron (Art Bible §7.2).
        /// Forward-pointing arrow, implies speed/movement.
        /// </summary>
        static int GenerateCollectibleDash()
        {
            var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            FillClear(tex);

            // Chevron pointing up (forward on road)
            // Outer chevron — dark blue
            int[] chevronX = { 7, 8 };
            for (int row = 0; row < 7; row++)
            {
                int left = 7 - row;
                int right = 8 + row;
                if (left >= 0 && left < 16 && right < 16)
                {
                    SetPx(tex, left, 4 + row, DashBlueDark);
                    SetPx(tex, left + 1, 4 + row, DashBlue);
                    SetPx(tex, right - 1, 4 + row, DashBlue);
                    SetPx(tex, right, 4 + row, DashBlueDark);
                }
            }
            // Inner fill
            for (int row = 0; row < 6; row++)
            {
                int left = 7 - row + 1;
                int right = 8 + row - 1;
                for (int x = left; x <= right; x++)
                    SetPx(tex, x, 5 + row, DashBlue);
            }
            // Bright tip
            SetPx(tex, 7, 12, CoinHighlight);
            SetPx(tex, 8, 12, CoinHighlight);
            SetPx(tex, 7, 13, DashBlue);
            SetPx(tex, 8, 13, DashBlue);

            SaveSprite(tex, "Collectibles/collectible-dash-idle.png");
            return 1;
        }

        /// <summary>
        /// Shield collectible — 16x16 gold hexagon (Art Bible §7.2).
        /// Defensive shape, protective.
        /// </summary>
        static int GenerateCollectibleShield()
        {
            var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            FillClear(tex);

            // Hexagon / shield shape
            // Build row by row — wider at middle
            int[] halfWidths = { 3, 5, 6, 7, 7, 7, 7, 6, 5, 4, 3, 2 };
            for (int i = 0; i < halfWidths.Length; i++)
            {
                int y = 2 + i;
                int hw = halfWidths[i];
                int left = 8 - hw;
                int right = 7 + hw;
                for (int x = left; x <= right; x++)
                {
                    bool edge = (x == left || x == right);
                    SetPx(tex, x, y, edge ? ShieldGoldDark : ShieldGold);
                }
            }
            // Top and bottom edge darker
            FillRect(tex, 5, 2, 6, 1, ShieldGoldDark);
            FillRect(tex, 6, 13, 4, 1, ShieldGoldDark);
            // Centre highlight
            SetPx(tex, 7, 7, CoinHighlight);
            SetPx(tex, 8, 7, CoinHighlight);
            SetPx(tex, 7, 8, CoinHighlight);
            SetPx(tex, 8, 8, CoinHighlight);

            SaveSprite(tex, "Collectibles/collectible-shield-idle.png");
            return 1;
        }

        /// <summary>
        /// Surge collectible — 16x16 red lightning bolt (Art Bible §7.2).
        /// Spiky, energetic shape.
        /// </summary>
        static int GenerateCollectibleSurge()
        {
            var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            FillClear(tex);

            // Lightning bolt shape
            // Top spike
            SetPx(tex, 9, 14, SurgeRed); SetPx(tex, 10, 14, SurgeRed);
            SetPx(tex, 8, 13, SurgeRed); SetPx(tex, 9, 13, SurgeRed); SetPx(tex, 10, 13, SurgeRed);
            SetPx(tex, 7, 12, SurgeRed); SetPx(tex, 8, 12, SurgeRed); SetPx(tex, 9, 12, SurgeRed);
            // Upper diagonal
            SetPx(tex, 7, 11, SurgeRed); SetPx(tex, 8, 11, SurgeRed);
            SetPx(tex, 6, 10, SurgeRed); SetPx(tex, 7, 10, SurgeRed);
            SetPx(tex, 5, 9, SurgeRed); SetPx(tex, 6, 9, SurgeRed);
            // Horizontal bar
            FillRect(tex, 5, 8, 7, 2, SurgeRed);
            FillRect(tex, 6, 8, 5, 2, SurgeRedDark); // darker inner
            // Lower diagonal
            SetPx(tex, 9, 7, SurgeRed); SetPx(tex, 10, 7, SurgeRed);
            SetPx(tex, 8, 6, SurgeRed); SetPx(tex, 9, 6, SurgeRed);
            SetPx(tex, 7, 5, SurgeRed); SetPx(tex, 8, 5, SurgeRed);
            // Bottom spike
            SetPx(tex, 5, 4, SurgeRed); SetPx(tex, 6, 4, SurgeRed); SetPx(tex, 7, 4, SurgeRed);
            SetPx(tex, 5, 3, SurgeRed); SetPx(tex, 6, 3, SurgeRed);
            SetPx(tex, 5, 2, SurgeRed);

            // Highlight on bolt centre
            SetPx(tex, 7, 9, CoinHighlight);
            SetPx(tex, 8, 8, CoinHighlight);

            SaveSprite(tex, "Collectibles/collectible-surge-idle.png");
            return 1;
        }

        /// <summary>
        /// Ground tile — 16x16 road surface with lane marking (Art Bible §4.2).
        /// Tileable horizontally. Represents one tile of the 3-lane road.
        /// </summary>
        static int GenerateGroundTile()
        {
            var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);

            // Fill with dark road surface
            FillRect(tex, 0, 0, 16, 16, RoadDark);

            // Subtle texture variation — slightly lighter pixels scattered
            SetPx(tex, 3, 4, RoadMid); SetPx(tex, 11, 9, RoadMid);
            SetPx(tex, 7, 2, RoadMid); SetPx(tex, 14, 12, RoadMid);
            SetPx(tex, 1, 14, RoadMid); SetPx(tex, 9, 6, RoadMid);

            // Dashed lane marking — 2px wide, centred at x=7-8
            // Dashes: 4px on, 4px off pattern (top half on, bottom half off for tiling)
            for (int y = 8; y < 16; y++)
            {
                SetPx(tex, 7, y, LaneLine);
                SetPx(tex, 8, y, LaneLine);
            }

            SaveSprite(tex, "Environment/ground-coastal-idle.png");
            return 1;
        }

        /// <summary>
        /// Background far — 320x180 TOP-DOWN terrain base (Art Bible §4.4, §12.2).
        /// Farthest layer: the ground surface seen from above.
        /// Layout (left to right): beach sand | road shoulder | ROAD (centre) | road shoulder | beach sand.
        /// Tiles vertically for endless scrolling.
        /// </summary>
        static int GenerateBackgroundFar()
        {
            var tex = new Texture2D(320, 180, TextureFormat.RGBA32, false);

            // Road is ~96px wide in the centre (3 lanes × 16px × 2 = 96 at 16PPU scale).
            // Shoulders are ~20px each. Rest is beach/terrain.
            int roadLeft  = 112;  // 320/2 - 48
            int roadRight = 208;  // 320/2 + 48
            int shoulderW = 20;

            for (int y = 0; y < 180; y++)
            {
                for (int x = 0; x < 320; x++)
                {
                    if (x >= roadLeft && x < roadRight)
                    {
                        // Road surface
                        SetPx(tex, x, y, RoadDark);
                    }
                    else if (x >= roadLeft - shoulderW && x < roadLeft)
                    {
                        // Left shoulder — gravel/sand transition
                        float t = (float)(x - (roadLeft - shoulderW)) / shoulderW;
                        SetPx(tex, x, y, LerpColor(Sand, Hex("D7CCC8"), t));
                    }
                    else if (x >= roadRight && x < roadRight + shoulderW)
                    {
                        // Right shoulder
                        float t = (float)(x - roadRight) / shoulderW;
                        SetPx(tex, x, y, LerpColor(Hex("D7CCC8"), Sand, t));
                    }
                    else
                    {
                        // Beach sand terrain
                        // Subtle variation using a simple hash
                        bool variant = ((x * 7 + y * 13) % 5) == 0;
                        SetPx(tex, x, y, variant ? Hex("FFE0B2") : Sand);
                    }
                }
            }

            // Road lane markings — dashed white lines (top-down view)
            // Two lane dividers at 1/3 and 2/3 across the road
            int lane1 = roadLeft + 32;
            int lane2 = roadLeft + 64;
            for (int y = 0; y < 180; y++)
            {
                // Dashed pattern: 12px on, 8px off
                bool dashOn = (y % 20) < 12;
                if (dashOn)
                {
                    SetPx(tex, lane1, y, LaneLine); SetPx(tex, lane1 + 1, y, LaneLine);
                    SetPx(tex, lane2, y, LaneLine); SetPx(tex, lane2 + 1, y, LaneLine);
                }
            }

            // Road edge lines (solid white)
            for (int y = 0; y < 180; y++)
            {
                SetPx(tex, roadLeft, y, LaneLine);  SetPx(tex, roadLeft + 1, y, LaneLine);
                SetPx(tex, roadRight - 1, y, LaneLine); SetPx(tex, roadRight - 2, y, LaneLine);
            }

            // Sparse road surface grit
            for (int i = 0; i < 40; i++)
            {
                int gx = roadLeft + 4 + (i * 37) % 88;
                int gy = (i * 53) % 180;
                SetPx(tex, gx, gy, Hex("4A4A4A"));
            }

            SaveSprite(tex, "Background/bg-coastal-far.png");
            return 1;
        }

        /// <summary>
        /// Background mid — 320x180 TOP-DOWN roadside objects (Art Bible §4.3).
        /// Mostly transparent. Palm tree canopies (green circles from above),
        /// small shadows, and terrain detail on the sand areas.
        /// </summary>
        static int GenerateBackgroundMid()
        {
            var tex = new Texture2D(320, 180, TextureFormat.RGBA32, false);
            FillClear(tex);

            // Palm tree canopies — seen from above as round green blobs with trunk dot
            // Only on the sand areas (left of road and right of road)
            int[][] treeCentres =
            {
                // Left side trees (x < ~92)
                new[] { 30, 30 }, new[] { 60, 90 }, new[] { 20, 140 }, new[] { 75, 50 }, new[] { 50, 170 },
                // Right side trees (x > ~228)
                new[] { 245, 25 }, new[] { 270, 80 }, new[] { 290, 140 }, new[] { 250, 110 }, new[] { 280, 50 },
            };

            foreach (int[] tc in treeCentres)
            {
                int cx = tc[0], cy = tc[1];

                // Shadow (offset down-right, darker)
                DrawCircleFilled(tex, cx + 3, cy - 3, 10, Hex("00000030")); // transparent shadow

                // Canopy — layered circles (top-down palm = irregular round mass)
                Color32 foliageDark = Hex("388E3C");
                Color32 foliageLight = PalmFoliage;
                DrawCircleFilled(tex, cx, cy, 11, foliageDark);
                DrawCircleFilled(tex, cx, cy, 8, foliageLight);
                DrawCircleFilled(tex, cx - 3, cy + 2, 5, foliageLight);
                DrawCircleFilled(tex, cx + 4, cy - 1, 5, foliageLight);

                // Trunk visible through canopy centre (brown dot)
                DrawCircleFilled(tex, cx, cy, 2, PalmTrunk);
            }

            // Beach detail — scattered small shells / rocks (tiny 2px dots on sand areas)
            int[][] debris =
            {
                new[] { 15, 60 }, new[] { 80, 20 }, new[] { 45, 110 }, new[] { 10, 155 },
                new[] { 235, 45 }, new[] { 295, 100 }, new[] { 260, 160 }, new[] { 305, 30 },
            };
            foreach (int[] d in debris)
            {
                SetPx(tex, d[0], d[1], Hex("BCAAA4"));
                SetPx(tex, d[0] + 1, d[1], Hex("D7CCC8"));
            }

            SaveSprite(tex, "Background/bg-coastal-mid.png");
            return 1;
        }

        /// <summary>
        /// Background near — 320x180 TOP-DOWN road shoulder details (Art Bible §4.3).
        /// Mostly transparent. Guardrail lines running vertically alongside the road,
        /// small road furniture from above. Must not obscure gameplay lanes.
        /// </summary>
        static int GenerateBackgroundNear()
        {
            var tex = new Texture2D(320, 180, TextureFormat.RGBA32, false);
            FillClear(tex);

            // Guardrails — thin vertical lines running alongside the road edges.
            // Road spans roughly x=112..208 in the far layer. Guardrails sit just inside the shoulders.
            int guardLeft = 95;
            int guardRight = 225;

            // Continuous rail line
            for (int y = 0; y < 180; y++)
            {
                SetPx(tex, guardLeft, y, PlayerGrey);
                SetPx(tex, guardLeft + 1, y, PlayerGrey);
                SetPx(tex, guardRight, y, PlayerGrey);
                SetPx(tex, guardRight + 1, y, PlayerGrey);
            }

            // Rail posts (wider marks every 24px)
            for (int y = 0; y < 180; y += 24)
            {
                FillRect(tex, guardLeft - 1, y, 4, 3, PlayerWhite);
                FillRect(tex, guardRight - 1, y, 4, 3, PlayerWhite);
            }

            // Road shoulder rumble strips (small dashes along inner shoulder)
            int rumbleLeft = 105;
            int rumbleRight = 215;
            for (int y = 0; y < 180; y += 6)
            {
                if ((y / 6) % 2 == 0)
                {
                    SetPx(tex, rumbleLeft, y, Hex("FFE082"));
                    SetPx(tex, rumbleLeft, y + 1, Hex("FFE082"));
                    SetPx(tex, rumbleRight, y, Hex("FFE082"));
                    SetPx(tex, rumbleRight, y + 1, Hex("FFE082"));
                }
            }

            SaveSprite(tex, "Background/bg-coastal-near.png");
            return 1;
        }

        // ════════════════════════════════════════════════════════════════════
        // IMPORT SETTINGS
        // ════════════════════════════════════════════════════════════════════

        struct SpriteSpec
        {
            public string Path;
            public int PPU;
            public SpriteAlignment Pivot;
            public Vector2 CustomPivot;
        }

        static void ApplyAllImportSettings()
        {
            var specs = new[]
            {
                new SpriteSpec
                {
                    Path = $"{SpritesRoot}/Player/player-default-idle.png",
                    PPU = 16, Pivot = SpriteAlignment.BottomCenter, CustomPivot = new Vector2(0.5f, 0f)
                },
                new SpriteSpec
                {
                    Path = $"{SpritesRoot}/Obstacles/hazard-block-idle.png",
                    PPU = 16, Pivot = SpriteAlignment.BottomCenter, CustomPivot = new Vector2(0.5f, 0f)
                },
                new SpriteSpec
                {
                    Path = $"{SpritesRoot}/Collectibles/coin-default-idle.png",
                    PPU = 16, Pivot = SpriteAlignment.Center, CustomPivot = new Vector2(0.5f, 0.5f)
                },
                new SpriteSpec
                {
                    Path = $"{SpritesRoot}/Collectibles/collectible-dash-idle.png",
                    PPU = 16, Pivot = SpriteAlignment.Center, CustomPivot = new Vector2(0.5f, 0.5f)
                },
                new SpriteSpec
                {
                    Path = $"{SpritesRoot}/Collectibles/collectible-shield-idle.png",
                    PPU = 16, Pivot = SpriteAlignment.Center, CustomPivot = new Vector2(0.5f, 0.5f)
                },
                new SpriteSpec
                {
                    Path = $"{SpritesRoot}/Collectibles/collectible-surge-idle.png",
                    PPU = 16, Pivot = SpriteAlignment.Center, CustomPivot = new Vector2(0.5f, 0.5f)
                },
                new SpriteSpec
                {
                    Path = $"{SpritesRoot}/Environment/ground-coastal-idle.png",
                    PPU = 16, Pivot = SpriteAlignment.BottomLeft, CustomPivot = new Vector2(0f, 0f)
                },
                new SpriteSpec
                {
                    Path = $"{SpritesRoot}/Background/bg-coastal-far.png",
                    PPU = 16, Pivot = SpriteAlignment.BottomLeft, CustomPivot = new Vector2(0f, 0f)
                },
                new SpriteSpec
                {
                    Path = $"{SpritesRoot}/Background/bg-coastal-mid.png",
                    PPU = 16, Pivot = SpriteAlignment.BottomLeft, CustomPivot = new Vector2(0f, 0f)
                },
                new SpriteSpec
                {
                    Path = $"{SpritesRoot}/Background/bg-coastal-near.png",
                    PPU = 16, Pivot = SpriteAlignment.BottomLeft, CustomPivot = new Vector2(0f, 0f)
                },
            };

            foreach (var spec in specs)
                ApplyImportSettings(spec);
        }

        /// <summary>
        /// Apply SPRITE-PIPELINE.md §4 import settings:
        /// 16 PPU, Point filter, no compression, mips off, Single sprite mode.
        /// </summary>
        static void ApplyImportSettings(SpriteSpec spec)
        {
            var importer = AssetImporter.GetAtPath(spec.Path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[SpriteAssetGenerator] Importer not found for {spec.Path}");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = spec.PPU;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;

            // Pivot
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)spec.Pivot;
            settings.spritePivot = spec.CustomPivot;
            importer.SetTextureSettings(settings);

            // Android platform override: ETC2 (SPRITE-PIPELINE.md §4.2)
            var androidOverride = importer.GetPlatformTextureSettings("Android");
            androidOverride.overridden = true;
            androidOverride.format = TextureImporterFormat.ETC2_RGBA8;
            androidOverride.compressionQuality = 50;
            androidOverride.maxTextureSize = 2048;
            importer.SetPlatformTextureSettings(androidOverride);

            importer.SaveAndReimport();
        }

        // ════════════════════════════════════════════════════════════════════
        // PREFAB REFERENCE UPDATES
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Finds all prefabs referencing old placeholder sprites (Player.png, Hazard.png, Coin.png)
        /// and re-points their SpriteRenderers to the new art-bible-named sprites.
        /// Also resets SpriteRenderer.color to white (placeholders used tint colours).
        /// </summary>
        static void UpdatePrefabReferences()
        {
            var oldPlayerSprite  = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesRoot}/Player/Player.png");
            var oldHazardSprite  = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesRoot}/Obstacles/Hazard.png");
            var oldCoinSprite    = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesRoot}/Collectibles/Coin.png");

            var newPlayerSprite  = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesRoot}/Player/player-default-idle.png");
            var newHazardSprite  = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesRoot}/Obstacles/hazard-block-idle.png");
            var newCoinSprite    = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesRoot}/Collectibles/coin-default-idle.png");

            // New collectible sprites for type-specific replacements
            var newDashSprite    = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesRoot}/Collectibles/collectible-dash-idle.png");
            var newShieldSprite  = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesRoot}/Collectibles/collectible-shield-idle.png");
            var newSurgeSprite   = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesRoot}/Collectibles/collectible-surge-idle.png");

            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" });
            int updated = 0;

            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = PrefabUtility.LoadPrefabContents(path);
                bool changed = false;

                var renderers = prefab.GetComponentsInChildren<SpriteRenderer>(true);
                foreach (var sr in renderers)
                {
                    if (sr.sprite == null) continue;

                    // Player sprite swap
                    if (oldPlayerSprite != null && sr.sprite == oldPlayerSprite && newPlayerSprite != null)
                    {
                        sr.sprite = newPlayerSprite;
                        sr.color = Color.white;
                        changed = true;
                    }
                    // Hazard sprite swap
                    else if (oldHazardSprite != null && sr.sprite == oldHazardSprite && newHazardSprite != null)
                    {
                        sr.sprite = newHazardSprite;
                        sr.color = Color.white;
                        changed = true;
                    }
                    // Coin/Collectible sprite swap — check for Collectible component to assign typed sprite
                    else if (oldCoinSprite != null && sr.sprite == oldCoinSprite)
                    {
                        // Find the Collectible MonoBehaviour on this GameObject to read Type
                        Sprite replacement = newCoinSprite; // default to coin
                        var behaviours = sr.GetComponents<MonoBehaviour>();
                        foreach (var mb in behaviours)
                        {
                            if (mb == null) continue;
                            var so = new SerializedObject(mb);
                            var typeProp = so.FindProperty("Type");
                            if (typeProp == null) continue;
                            // CollectibleType enum: 0=Dash, 1=Shield, 2=Surge, 3=Coin
                            switch (typeProp.intValue)
                            {
                                case 0: replacement = newDashSprite ?? newCoinSprite; break;
                                case 1: replacement = newShieldSprite ?? newCoinSprite; break;
                                case 2: replacement = newSurgeSprite ?? newCoinSprite; break;
                                case 3: replacement = newCoinSprite; break;
                            }
                            break;
                        }
                        sr.sprite = replacement;
                        sr.color = Color.white;
                        changed = true;
                    }
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                    updated++;
                }
                PrefabUtility.UnloadPrefabContents(prefab);
            }

            Debug.Log($"[SpriteAssetGenerator] Updated {updated} prefab(s) with new sprite references.");
        }

        // ════════════════════════════════════════════════════════════════════
        // DRAWING HELPERS
        // ════════════════════════════════════════════════════════════════════

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

        // ════════════════════════════════════════════════════════════════════
        // FILE I/O
        // ════════════════════════════════════════════════════════════════════

        static void EnsureDirectories()
        {
            string[] dirs =
            {
                $"{SpritesRoot}/Player",
                $"{SpritesRoot}/Obstacles",
                $"{SpritesRoot}/Collectibles",
                $"{SpritesRoot}/Environment",
                $"{SpritesRoot}/Background",
            };
            foreach (string dir in dirs)
            {
                string full = Path.GetFullPath(dir);
                if (!Directory.Exists(full))
                    Directory.CreateDirectory(full);
            }
        }

        static void SaveSprite(Texture2D tex, string relativePath)
        {
            string assetPath = $"{SpritesRoot}/{relativePath}";
            string fullPath = Path.GetFullPath(assetPath);
            byte[] png = tex.EncodeToPNG();
            File.WriteAllBytes(fullPath, png);
            Object.DestroyImmediate(tex);
        }
    }
}
