using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace DashAndCollect.Editor
{
    /// <summary>
    /// Generates a ground tileset sprite sheet, slices it into tiles, creates Tile assets,
    /// a RuleTile for auto-tiling, and a Tile Palette for the Dash & Collect road surface.
    ///
    /// Art Bible §4.2/§4.4/§12 define the road as a top-down highway. The four tile variants
    /// map to a horizontal road cross-section:
    ///   0 — Left Edge:  sand shoulder → white edge line → road surface
    ///   1 — Center:     road surface with dashed lane marking
    ///   2 — Right Edge:  road surface → white edge line → sand shoulder
    ///   3 — Fill:        plain road surface (between lane lines)
    ///
    /// Menu: Tools → Dash & Collect → Generate Ground Tileset
    /// </summary>
    public static class GroundTilesetGenerator
    {
        // ── Art Bible §12 palette ──────────────────────────────────────────
        static readonly Color32 RoadDark       = Hex("3E3E3E");
        static readonly Color32 RoadMid        = Hex("4A4A4A");
        static readonly Color32 RoadLight      = Hex("555555");
        static readonly Color32 LaneLine       = Hex("E0E0E0");
        static readonly Color32 LaneLineDim    = Hex("BDBDBD");
        static readonly Color32 EdgeLine       = Hex("F5F5F5");
        static readonly Color32 Sand           = Hex("FFCC80");
        static readonly Color32 SandDark       = Hex("FFB74D");
        static readonly Color32 SandLight      = Hex("FFE0B2");
        static readonly Color32 SandGravel     = Hex("D7CCC8");
        static readonly Color32 Clear          = new Color32(0, 0, 0, 0);

        const int TileSize = 16;
        const int TileCount = 4;
        const int SheetWidth = TileSize * TileCount; // 64
        const int SheetHeight = TileSize;             // 16

        const string SpritesRoot   = "Assets/Art/Sprites";
        const string TilesetPath   = "Assets/Art/Sprites/Environment/ground-tileset.png";
        const string TilesDir      = "Assets/Art/Tiles/Ground";
        const string PaletteDir    = "Assets/Art/Palettes";
        const string RuleTilePath  = "Assets/Art/Tiles/Ground/ground-road-rule.asset";

        [MenuItem("Tools/Dash & Collect/Generate Ground Tileset")]
        public static void Generate()
        {
            EnsureDirectories();

            GenerateSpriteSheet();
            AssetDatabase.Refresh();
            ConfigureImportSettings();
            AssetDatabase.Refresh();

            CreateTileAssets();
            CreateRuleTile();
            CreateTilePalette();

            Debug.Log("[GroundTilesetGenerator] Tileset, tiles, rule tile, and palette created.");
        }

        // ════════════════════════════════════════════════════════════════════
        // SPRITE SHEET GENERATION
        // ════════════════════════════════════════════════════════════════════

        static void GenerateSpriteSheet()
        {
            var tex = new Texture2D(SheetWidth, SheetHeight, TextureFormat.RGBA32, false);
            FillRect(tex, 0, 0, SheetWidth, SheetHeight, Clear);

            DrawLeftEdge(tex, 0);
            DrawCenter(tex, TileSize);
            DrawRightEdge(tex, TileSize * 2);
            DrawFill(tex, TileSize * 3);

            byte[] png = tex.EncodeToPNG();
            string fullPath = Path.GetFullPath(TilesetPath);
            string dir = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(fullPath, png);
            Object.DestroyImmediate(tex);
        }

        /// <summary>
        /// Tile 0 — Left edge: sand shoulder (left) → edge line → road surface (right).
        /// Tiles vertically for endless scrolling road.
        /// </summary>
        static void DrawLeftEdge(Texture2D tex, int ox)
        {
            // Sand shoulder: columns 0-4
            for (int y = 0; y < TileSize; y++)
            {
                for (int x = 0; x < 5; x++)
                {
                    Color32 c = (x + y) % 3 == 0 ? SandDark : Sand;
                    if (x == 0 && y % 4 == 0) c = SandLight; // subtle highlight
                    SetPx(tex, ox + x, y, c);
                }
            }

            // Gravel transition: column 5
            for (int y = 0; y < TileSize; y++)
                SetPx(tex, ox + 5, y, y % 2 == 0 ? SandGravel : SandDark);

            // Edge line: columns 6-7 (solid white)
            for (int y = 0; y < TileSize; y++)
            {
                SetPx(tex, ox + 6, y, EdgeLine);
                SetPx(tex, ox + 7, y, LaneLineDim);
            }

            // Road surface: columns 8-15
            for (int y = 0; y < TileSize; y++)
                for (int x = 8; x < TileSize; x++)
                    SetPx(tex, ox + x, y, RoadDark);

            // Surface grit
            SetPx(tex, ox + 10, 3, RoadMid);
            SetPx(tex, ox + 13, 7, RoadMid);
            SetPx(tex, ox + 9, 12, RoadLight);
            SetPx(tex, ox + 14, 1, RoadMid);
        }

        /// <summary>
        /// Tile 1 — Center: road surface with dashed lane marking at columns 7-8.
        /// Dash pattern: rows 8-15 filled (on), rows 0-7 empty (off) — tiles vertically.
        /// </summary>
        static void DrawCenter(Texture2D tex, int ox)
        {
            // Full road surface
            for (int y = 0; y < TileSize; y++)
                for (int x = 0; x < TileSize; x++)
                    SetPx(tex, ox + x, y, RoadDark);

            // Dashed lane marking: 2px wide, short dash at top (rows 12-15), long gap (rows 0-11).
            // 1:3 ratio makes dashes distinct and easier to track visually at speed.
            for (int y = 12; y < TileSize; y++)
            {
                SetPx(tex, ox + 7, y, LaneLine);
                SetPx(tex, ox + 8, y, LaneLine);
            }

            // Surface grit
            SetPx(tex, ox + 2, 5, RoadMid);
            SetPx(tex, ox + 11, 2, RoadMid);
            SetPx(tex, ox + 4, 10, RoadLight);
            SetPx(tex, ox + 13, 14, RoadMid);
            SetPx(tex, ox + 6, 1, RoadMid);
            SetPx(tex, ox + 14, 9, RoadLight);
        }

        /// <summary>
        /// Tile 2 — Right edge: road surface (left) → edge line → sand shoulder (right).
        /// Mirror of left edge.
        /// </summary>
        static void DrawRightEdge(Texture2D tex, int ox)
        {
            // Road surface: columns 0-7
            for (int y = 0; y < TileSize; y++)
                for (int x = 0; x < 8; x++)
                    SetPx(tex, ox + x, y, RoadDark);

            // Edge line: columns 8-9
            for (int y = 0; y < TileSize; y++)
            {
                SetPx(tex, ox + 8, y, LaneLineDim);
                SetPx(tex, ox + 9, y, EdgeLine);
            }

            // Gravel transition: column 10
            for (int y = 0; y < TileSize; y++)
                SetPx(tex, ox + 10, y, y % 2 == 0 ? SandGravel : SandDark);

            // Sand shoulder: columns 11-15
            for (int y = 0; y < TileSize; y++)
            {
                for (int x = 11; x < TileSize; x++)
                {
                    Color32 c = (x + y) % 3 == 0 ? SandDark : Sand;
                    if (x == 15 && y % 4 == 0) c = SandLight;
                    SetPx(tex, ox + x, y, c);
                }
            }

            // Surface grit
            SetPx(tex, ox + 2, 4, RoadMid);
            SetPx(tex, ox + 5, 11, RoadMid);
            SetPx(tex, ox + 1, 8, RoadLight);
            SetPx(tex, ox + 6, 14, RoadMid);
        }

        /// <summary>
        /// Tile 3 — Fill: plain road surface, no lane markings.
        /// Used for inner road area between lane-marking tiles.
        /// </summary>
        static void DrawFill(Texture2D tex, int ox)
        {
            for (int y = 0; y < TileSize; y++)
                for (int x = 0; x < TileSize; x++)
                    SetPx(tex, ox + x, y, RoadDark);

            // Sparse surface grit for texture
            SetPx(tex, ox + 3, 4, RoadMid);
            SetPx(tex, ox + 11, 9, RoadMid);
            SetPx(tex, ox + 7, 2, RoadLight);
            SetPx(tex, ox + 14, 12, RoadMid);
            SetPx(tex, ox + 1, 14, RoadMid);
            SetPx(tex, ox + 9, 6, RoadLight);
            SetPx(tex, ox + 5, 13, RoadMid);
            SetPx(tex, ox + 12, 0, RoadMid);
        }

        // ════════════════════════════════════════════════════════════════════
        // IMPORT SETTINGS & SLICING
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// SPRITE-PIPELINE.md §4 import settings + Sprite Mode: Multiple with 16x16 grid slice.
        /// </summary>
        static void ConfigureImportSettings()
        {
            var importer = AssetImporter.GetAtPath(TilesetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[GroundTilesetGenerator] Could not find importer at {TilesetPath}");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;

            // Slice into 4 tiles (16x16 each) — left to right
            var spriteSheet = new SpriteMetaData[TileCount];
            string[] names = { "ground-left-edge", "ground-center", "ground-right-edge", "ground-fill" };
            // Pivots: bottom-left (0,0) per SPRITE-PIPELINE.md §3.3 for ground tiles
            for (int i = 0; i < TileCount; i++)
            {
                spriteSheet[i] = new SpriteMetaData
                {
                    name = names[i],
                    rect = new Rect(i * TileSize, 0, TileSize, TileSize),
                    alignment = (int)SpriteAlignment.BottomLeft,
                    pivot = new Vector2(0f, 0f),
                    border = Vector4.zero,
                };
            }
            importer.spritesheet = spriteSheet;

            // Android platform override: ETC2
            var androidOverride = importer.GetPlatformTextureSettings("Android");
            androidOverride.overridden = true;
            androidOverride.format = TextureImporterFormat.ETC2_RGBA8;
            androidOverride.compressionQuality = 50;
            androidOverride.maxTextureSize = 2048;
            importer.SetPlatformTextureSettings(androidOverride);

            importer.SaveAndReimport();
        }

        // ════════════════════════════════════════════════════════════════════
        // TILE ASSETS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates individual Tile assets from the sliced sprite sheet.
        /// Left-edge and right-edge tiles get BoxCollider2D type (road boundary).
        /// </summary>
        static void CreateTileAssets()
        {
            string[] sliceNames = { "ground-left-edge", "ground-center", "ground-right-edge", "ground-fill" };
            // Load all sub-sprites from the sheet
            var allSprites = AssetDatabase.LoadAllAssetsAtPath(TilesetPath);

            foreach (string name in sliceNames)
            {
                Sprite sprite = null;
                foreach (var obj in allSprites)
                {
                    if (obj is Sprite s && s.name == name)
                    {
                        sprite = s;
                        break;
                    }
                }

                if (sprite == null)
                {
                    Debug.LogWarning($"[GroundTilesetGenerator] Sprite '{name}' not found in sheet.");
                    continue;
                }

                string tilePath = $"{TilesDir}/{name}.asset";
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    AssetDatabase.CreateAsset(tile, tilePath);
                }

                tile.sprite = sprite;
                tile.color = Color.white;
                tile.name = name;

                // Collision: BoxCollider on edge tiles (road boundary for player containment)
                bool isEdge = name.Contains("left-edge") || name.Contains("right-edge");
                tile.colliderType = isEdge ? Tile.ColliderType.Grid : Tile.ColliderType.None;

                EditorUtility.SetDirty(tile);
            }

            AssetDatabase.SaveAssets();
        }

        // ════════════════════════════════════════════════════════════════════
        // RULE TILE
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates a RuleTile with horizontal strip adjacency rules.
        /// Painting a horizontal strip of tiles auto-selects edge/center/fill.
        ///
        /// Adjacency rules (checking left/right neighbors only):
        ///   No left, has right  → left-edge   (road boundary — has collider)
        ///   Has left, has right → center       (lane marking column)
        ///   Has left, no right  → right-edge   (road boundary — has collider)
        ///   Anything else       → fill         (default — plain road)
        /// </summary>
        static void CreateRuleTile()
        {
            // Load sliced sprites
            var allSprites = AssetDatabase.LoadAllAssetsAtPath(TilesetPath);
            Sprite leftEdge = null, center = null, rightEdge = null, fill = null;

            foreach (var obj in allSprites)
            {
                if (!(obj is Sprite s)) continue;
                switch (s.name)
                {
                    case "ground-left-edge":  leftEdge = s; break;
                    case "ground-center":     center = s; break;
                    case "ground-right-edge": rightEdge = s; break;
                    case "ground-fill":       fill = s; break;
                }
            }

            if (leftEdge == null || center == null || rightEdge == null || fill == null)
            {
                Debug.LogError("[GroundTilesetGenerator] Cannot create RuleTile — missing sliced sprites.");
                return;
            }

            var ruleTile = AssetDatabase.LoadAssetAtPath<RuleTile>(RuleTilePath);
            if (ruleTile == null)
            {
                ruleTile = ScriptableObject.CreateInstance<RuleTile>();
                AssetDatabase.CreateAsset(ruleTile, RuleTilePath);
            }

            // Default sprite (fill — used when no rules match, e.g. isolated single tile)
            ruleTile.m_DefaultSprite = fill;
            ruleTile.m_DefaultColliderType = Tile.ColliderType.None;

            ruleTile.m_TilingRules = new System.Collections.Generic.List<RuleTile.TilingRule>();

            // Neighbor constants from RuleTile.TilingRuleOutput.Neighbor
            const int This    = 1; // Neighbor.This — tile of same type present
            const int NotThis = 2; // Neighbor.NotThis — empty or different tile

            // Shared neighbor positions: only check left (-1,0,0) and right (1,0,0).
            // Vertical neighbors are irrelevant for a horizontal road strip.
            var leftPos  = new Vector3Int(-1, 0, 0);
            var rightPos = new Vector3Int( 1, 0, 0);

            // Rule 1: Left edge — nothing to the left, tile to the right
            var ruleLeft = new RuleTile.TilingRule
            {
                m_Sprites = new Sprite[] { leftEdge },
                m_ColliderType = Tile.ColliderType.Grid,
                m_NeighborPositions = new System.Collections.Generic.List<Vector3Int> { leftPos, rightPos },
                m_Neighbors = new System.Collections.Generic.List<int> { NotThis, This },
            };
            ruleTile.m_TilingRules.Add(ruleLeft);

            // Rule 2: Right edge — tile to the left, nothing to the right
            var ruleRight = new RuleTile.TilingRule
            {
                m_Sprites = new Sprite[] { rightEdge },
                m_ColliderType = Tile.ColliderType.Grid,
                m_NeighborPositions = new System.Collections.Generic.List<Vector3Int> { leftPos, rightPos },
                m_Neighbors = new System.Collections.Generic.List<int> { This, NotThis },
            };
            ruleTile.m_TilingRules.Add(ruleRight);

            // Rule 3: Center — tiles on both sides (lane marking column)
            var ruleCenter = new RuleTile.TilingRule
            {
                m_Sprites = new Sprite[] { center },
                m_ColliderType = Tile.ColliderType.None,
                m_NeighborPositions = new System.Collections.Generic.List<Vector3Int> { leftPos, rightPos },
                m_Neighbors = new System.Collections.Generic.List<int> { This, This },
            };
            ruleTile.m_TilingRules.Add(ruleCenter);

            // Default (fill) handled by m_DefaultSprite — covers isolated tiles and
            // any position where center rule is not the desired output (road between lanes).

            EditorUtility.SetDirty(ruleTile);
            AssetDatabase.SaveAssets();
        }

        // ════════════════════════════════════════════════════════════════════
        // TILE PALETTE
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates a Tile Palette prefab (Grid + Tilemap) and places tile assets into it.
        /// The palette appears in Window → 2D → Tile Palette as "Ground Road".
        /// </summary>
        static void CreateTilePalette()
        {
            string palettePath = $"{PaletteDir}/Ground Road.prefab";

            if (AssetDatabase.LoadAssetAtPath<GameObject>(palettePath) != null)
            {
                Debug.Log("[GroundTilesetGenerator] Tile Palette already exists, skipping creation.");
                return;
            }

            // Create Grid root
            var gridGO = new GameObject("Ground Road");
            var grid = gridGO.AddComponent<Grid>();
            grid.cellSize = new Vector3(1f, 1f, 0f);
            grid.cellLayout = GridLayout.CellLayout.Rectangle;

            // Create Tilemap child
            var layerGO = new GameObject("Layer1");
            layerGO.transform.SetParent(gridGO.transform);
            var tilemap = layerGO.AddComponent<Tilemap>();
            layerGO.AddComponent<TilemapRenderer>();

            // Place the 4 tile variants in a row for easy painting
            string[] tileNames = { "ground-left-edge", "ground-center", "ground-right-edge", "ground-fill" };
            for (int i = 0; i < tileNames.Length; i++)
            {
                string tp = $"{TilesDir}/{tileNames[i]}.asset";
                var tile = AssetDatabase.LoadAssetAtPath<TileBase>(tp);
                if (tile != null)
                    tilemap.SetTile(new Vector3Int(i, 0, 0), tile);
            }

            // Also place the rule tile for convenience
            var ruleTile = AssetDatabase.LoadAssetAtPath<TileBase>(RuleTilePath);
            if (ruleTile != null)
                tilemap.SetTile(new Vector3Int(0, -1, 0), ruleTile);

            // Save as prefab
            PrefabUtility.SaveAsPrefabAsset(gridGO, palettePath);
            Object.DestroyImmediate(gridGO);
        }

        // ════════════════════════════════════════════════════════════════════
        // HELPERS
        // ════════════════════════════════════════════════════════════════════

        static void EnsureDirectories()
        {
            string[] dirs =
            {
                $"{SpritesRoot}/Environment",
                TilesDir,
                PaletteDir,
            };
            foreach (string d in dirs)
            {
                string full = Path.GetFullPath(d);
                if (!Directory.Exists(full)) Directory.CreateDirectory(full);
            }
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

        static Color32 Hex(string hex)
        {
            ColorUtility.TryParseHtmlString($"#{hex}", out Color color);
            return color;
        }
    }
}
