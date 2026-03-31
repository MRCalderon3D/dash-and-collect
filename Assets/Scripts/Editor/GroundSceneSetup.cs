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
    /// Creates a "Ground" Grid root with a child Tilemap painted as a 3-lane road:
    ///   left-edge | fill | center | fill | right-edge
    /// Tiles are painted tall enough (3x camera height) for seamless vertical wrapping.
    /// Attaches GroundScroller wired to GameManager.
    ///
    /// Menu: Tools → Dash & Collect → Setup Ground
    /// </summary>
    public static class GroundSceneSetup
    {
        const string TilesDir     = "Assets/Art/Tiles/Ground";
        const string RuleTilePath = "Assets/Art/Tiles/Ground/ground-road-rule.asset";

        // Road layout: 5 columns wide
        // Col 0: left-edge    (sand → road boundary)
        // Col 1: fill         (plain road)
        // Col 2: center       (road + lane marking)
        // Col 3: fill         (plain road)
        // Col 4: right-edge   (road boundary → sand)
        const int RoadWidthTiles = 5;

        [MenuItem("Tools/Dash & Collect/Setup Ground")]
        public static void Setup()
        {
            var gameManager = Object.FindFirstObjectByType<GameManager>();
            if (gameManager == null)
            {
                Debug.LogError("[GroundSceneSetup] No GameManager found in the active scene. Open Game.unity first.");
                return;
            }

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

            // Camera info for sizing.
            var cam = Camera.main;
            float camOrtho = cam != null ? cam.orthographicSize : 5f;
            float camX     = cam != null ? cam.transform.position.x : 0f;
            float camY     = cam != null ? cam.transform.position.y : 0f;

            // Tilemap height: 3x camera height so wrapping is never visible.
            // Each tile is 1 world unit (16px / 16 PPU). Round up to whole tiles.
            int tileRows = Mathf.CeilToInt(camOrtho * 2f * 3f);
            if (tileRows < 30) tileRows = 30; // minimum safety

            // Create Grid root
            var gridGO = new GameObject("Ground");
            Undo.RegisterCreatedObjectUndo(gridGO, "Setup Ground");
            var grid = gridGO.AddComponent<Grid>();
            grid.cellSize = new Vector3(1f, 1f, 0f);
            grid.cellLayout = GridLayout.CellLayout.Rectangle;

            // Position the grid so the road is centred on the camera X.
            // Tile (0,0) is at Grid origin, so offset so the 5-tile-wide road is centred.
            float roadHalfWidth = RoadWidthTiles * 0.5f;
            gridGO.transform.position = new Vector3(camX - roadHalfWidth, camY - tileRows * 0.5f, 0f);

            // Create Tilemap child
            var tmGO = new GameObject("RoadTilemap");
            tmGO.transform.SetParent(gridGO.transform);
            tmGO.transform.localPosition = Vector3.zero;

            var tilemap  = tmGO.AddComponent<Tilemap>();
            var renderer = tmGO.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = -5; // behind gameplay (0), in front of BG_Near (-10)

            // Paint the road: 5 columns x tileRows rows
            TileBase[] columnTiles = { leftEdge, fill, center, fill, rightEdge };

            for (int y = 0; y < tileRows; y++)
            {
                for (int x = 0; x < RoadWidthTiles; x++)
                {
                    tilemap.SetTile(new Vector3Int(x, y, 0), columnTiles[x]);
                }
            }

            // Add GroundScroller for endless scrolling
            var scroller = gridGO.AddComponent<GroundScroller>();
            var so = new SerializedObject(scroller);
            so.FindProperty("_gameManager").objectReferenceValue = gameManager;
            so.FindProperty("_tilemapHeight").floatValue = tileRows; // 1 tile = 1 world unit
            so.ApplyModifiedPropertiesWithoutUndo();

            // Mark scene dirty
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            Debug.Log($"[GroundSceneSetup] Road tilemap painted ({RoadWidthTiles}x{tileRows} tiles). Save the scene to persist.");
        }
    }
}
