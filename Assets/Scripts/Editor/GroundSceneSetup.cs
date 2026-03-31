using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace DashAndCollect.Editor
{
    /// <summary>
    /// Paints a scrolling road tilemap into the active scene using the generated
    /// ground tileset (Art Bible §4.2, §4.4).
    ///
    /// Reads LaneConfig to align tile columns with gameplay lane positions.
    /// Layout (7 cols): left-edge | lane1 | marking | lane2 | marking | lane3 | right-edge
    /// Lane fill tiles are centred on LaneConfig.lanePositions {-2, 0, +2}.
    ///
    /// Menu: Tools → Dash & Collect → Setup Ground
    /// </summary>
    public static class GroundSceneSetup
    {
        const string TilesDir = "Assets/Art/Tiles/Ground";

        [MenuItem("Tools/Dash & Collect/Setup Ground")]
        public static void Setup()
        {
            var gameManager = Object.FindFirstObjectByType<GameManager>();
            if (gameManager == null)
            {
                Debug.LogError("[GroundSceneSetup] No GameManager found. Open Game.unity first.");
                return;
            }

            // Load LaneConfig to get exact lane X positions.
            var laneConfigs = AssetDatabase.FindAssets("t:LaneConfig");
            LaneConfig laneConfig = null;
            if (laneConfigs.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(laneConfigs[0]);
                laneConfig = AssetDatabase.LoadAssetAtPath<LaneConfig>(path);
            }
            float[] lanes = laneConfig != null ? laneConfig.lanePositions : new[] { -2f, 0f, 2f };

            // Load tile assets
            var leftEdge  = AssetDatabase.LoadAssetAtPath<TileBase>($"{TilesDir}/ground-left-edge.asset");
            var center    = AssetDatabase.LoadAssetAtPath<TileBase>($"{TilesDir}/ground-center.asset");
            var rightEdge = AssetDatabase.LoadAssetAtPath<TileBase>($"{TilesDir}/ground-right-edge.asset");
            var fill      = AssetDatabase.LoadAssetAtPath<TileBase>($"{TilesDir}/ground-fill.asset");

            if (leftEdge == null || center == null || rightEdge == null || fill == null)
            {
                Debug.LogError("[GroundSceneSetup] Tile assets not found. Run 'Tools → Dash & Collect → Generate Ground Tileset' first.");
                return;
            }

            // Remove existing ground root if re-running.
            var existing = GameObject.Find("Ground");
            if (existing != null)
                Undo.DestroyObjectImmediate(existing);

            // ── Layout calculation ──────────────────────────────────────────
            // Each tile = 1 world unit (16px / 16 PPU).
            // Tile at column C with grid at X=G has left edge at G+C, right at G+C+1, centre at G+C+0.5.
            //
            // 7-column layout:
            //   Col 0: left-edge
            //   Col 1: fill (lane 1)      — centre must equal lanes[0]
            //   Col 2: center (marking)   — between lane 1 and 2
            //   Col 3: fill (lane 2)      — centre must equal lanes[1]
            //   Col 4: center (marking)   — between lane 2 and 3
            //   Col 5: fill (lane 3)      — centre must equal lanes[2]
            //   Col 6: right-edge
            //
            // Tile sprites use BottomLeft pivot (0,0). Unity Tilemap places the sprite
            // pivot at the cell center (anchor 0.5,0.5), so each sprite shifts +0.5
            // from the cell's left edge.
            //
            // Sprite centre for cell C = G + C + 0.5 (cell centre) + 0.5 (pivot shift) = G + C + 1.0
            // Lane 1 at col 1: G + 1 + 1.0 = G + 2.0  →  G = lanes[0] - 2.0
            // Lane 2 at col 3: G + 3 + 1.0 = G + 4.0 = lanes[0] + 2.0  ✓ (0 when lanes[0]=-2)
            // Lane 3 at col 5: G + 5 + 1.0 = G + 6.0 = lanes[0] + 4.0  ✓ (+2 when lanes[0]=-2)

            const int RoadWidthTiles = 7;
            float gridX = lanes[0] - 2.0f; // = -4.0 for default lanes

            // Camera info for height.
            var cam = Camera.main;
            float camY = cam != null ? cam.transform.position.y : 0f;
            float camOrtho = cam != null ? cam.orthographicSize : 5f;

            int tileRows = Mathf.CeilToInt(camOrtho * 2f * 3f);
            if (tileRows < 30) tileRows = 30;

            // Create Grid root
            var gridGO = new GameObject("Ground");
            Undo.RegisterCreatedObjectUndo(gridGO, "Setup Ground");
            var grid = gridGO.AddComponent<Grid>();
            grid.cellSize = new Vector3(1f, 1f, 0f);
            grid.cellLayout = GridLayout.CellLayout.Rectangle;
            gridGO.transform.position = new Vector3(gridX, camY - tileRows * 0.5f, 0f);

            // Create Tilemap child
            var tmGO = new GameObject("RoadTilemap");
            tmGO.transform.SetParent(gridGO.transform);
            tmGO.transform.localPosition = Vector3.zero;

            var tilemap  = tmGO.AddComponent<Tilemap>();
            var renderer = tmGO.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = -5;

            // Paint: 7 columns × tileRows
            TileBase[] columnTiles = { leftEdge, fill, center, fill, center, fill, rightEdge };

            for (int y = 0; y < tileRows; y++)
                for (int x = 0; x < RoadWidthTiles; x++)
                    tilemap.SetTile(new Vector3Int(x, y, 0), columnTiles[x]);

            // Log alignment for debugging
            Debug.Log($"[GroundSceneSetup] Grid X={gridX:F1}. " +
                      $"Lane centres: {gridX + 2.0f:F1}, {gridX + 4.0f:F1}, {gridX + 6.0f:F1} " +
                      $"(expected: {lanes[0]}, {lanes[1]}, {lanes[2]})");

            // Add GroundScroller
            var scroller = gridGO.AddComponent<GroundScroller>();
            var so = new SerializedObject(scroller);
            so.FindProperty("_gameManager").objectReferenceValue = gameManager;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[GroundSceneSetup] Road tilemap painted ({RoadWidthTiles}x{tileRows}). Save the scene to persist.");
        }
    }
}
