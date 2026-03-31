using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DashAndCollect.Editor
{
    /// <summary>
    /// Adds parallax background layers to the active scene using the generated
    /// coastal highway sprites (Art Bible §4.3, §12.2).
    ///
    /// Creates a "Background" root with 3 children (far, mid, near), each with
    /// SpriteRenderer + ParallaxBackground. Wires GameManager reference.
    ///
    /// Menu: Tools → Dash & Collect → Setup Background
    /// </summary>
    public static class BackgroundSceneSetup
    {
        const string SpritesRoot = "Assets/Art/Sprites/Background";

        struct LayerDef
        {
            public string Name;
            public string SpriteFile;
            public int    SortingOrder;
            public float  ParallaxFactor;
        }

        static readonly LayerDef[] Layers =
        {
            new LayerDef { Name = "BG_Far",  SpriteFile = "bg-coastal-far.png",  SortingOrder = -30, ParallaxFactor = 0.1f },
            new LayerDef { Name = "BG_Mid",  SpriteFile = "bg-coastal-mid.png",  SortingOrder = -20, ParallaxFactor = 0.4f },
            new LayerDef { Name = "BG_Near", SpriteFile = "bg-coastal-near.png", SortingOrder = -10, ParallaxFactor = 0.7f },
        };

        [MenuItem("Tools/Dash & Collect/Setup Background")]
        public static void Setup()
        {
            // Find GameManager in the scene (needed for parallax speed reference).
            var gameManager = Object.FindFirstObjectByType<GameManager>();
            if (gameManager == null)
            {
                Debug.LogError("[BackgroundSceneSetup] No GameManager found in the active scene. Open Game.unity first.");
                return;
            }

            // Remove existing background root if re-running.
            var existing = GameObject.Find("Background");
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            // Create root.
            var root = new GameObject("Background");
            Undo.RegisterCreatedObjectUndo(root, "Setup Background");
            root.transform.position = Vector3.zero;

            // Camera info for positioning.
            var cam = Camera.main;
            float camX = cam != null ? cam.transform.position.x : 0f;
            float camY = cam != null ? cam.transform.position.y : 0f;

            foreach (var layer in Layers)
            {
                string spritePath = $"{SpritesRoot}/{layer.SpriteFile}";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (sprite == null)
                {
                    Debug.LogWarning($"[BackgroundSceneSetup] Sprite not found: {spritePath}. Run 'Tools → Dash & Collect → Generate Sprites' first.");
                    continue;
                }

                var go = new GameObject(layer.Name);
                go.transform.SetParent(root.transform);

                // Position: centred on camera X, bottom-aligned with camera bottom.
                // Sprites use bottom-left pivot, so offset X by half sprite width.
                float spriteW = sprite.bounds.size.x;
                float spriteH = sprite.bounds.size.y;
                float camBottom = cam != null ? camY - cam.orthographicSize : -5f;
                go.transform.position = new Vector3(camX - spriteW / 2f, camBottom, 0f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = layer.SortingOrder;
                sr.color = Color.white;

                var parallax = go.AddComponent<ParallaxBackground>();
                parallax.Initialize(gameManager);

                // Set serialized parallax factor via SerializedObject.
                var so = new SerializedObject(parallax);
                so.FindProperty("_parallaxFactor").floatValue = layer.ParallaxFactor;
                so.FindProperty("_gameManager").objectReferenceValue = gameManager;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // Mark scene dirty so the user can save.
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            Debug.Log("[BackgroundSceneSetup] Background layers added. Save the scene to persist.");
        }
    }
}
