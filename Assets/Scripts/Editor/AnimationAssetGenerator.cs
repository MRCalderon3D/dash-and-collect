using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DashAndCollect.Editor
{
    /// <summary>
    /// Generates animation sprite sheets (pixel-by-pixel), AnimationClips,
    /// AnimatorControllers, and wires them to prefabs for Dash & Collect.
    ///
    /// Player car animations follow Art Bible §5.3:
    ///   idle  — exhaust pixel pulse (4 frames, 8fps, loop)
    ///   run   — speed-line cycle behind car (6 frames, 12fps, loop)
    ///   jump  — boost lift: car squashes then rises (2 frames, 8fps, no loop)
    ///   death — spin-out tumble (4 frames, 8fps, no loop)
    ///
    /// Coin animation follows Art Bible §7.2:
    ///   spin  — rotation illusion (6 frames, 10fps, loop)
    ///
    /// Menu: Tools → Dash & Collect → Generate Animations
    /// </summary>
    public static class AnimationAssetGenerator
    {
        // ── Art Bible §12 palette (matches SpriteAssetGenerator) ───────────
        static readonly Color32 PlayerWhite      = Hex("FAFAFA");
        static readonly Color32 PlayerGrey       = Hex("BDBDBD");
        static readonly Color32 PlayerDark       = Hex("616161");
        static readonly Color32 PlayerAccent     = Hex("4FC3F7");
        static readonly Color32 PlayerWindshield = Hex("90CAF9");
        static readonly Color32 SurgeRed         = Hex("EF5350");
        static readonly Color32 CoinYellow       = Hex("FFEE58");
        static readonly Color32 CoinYellowDark   = Hex("F9A825");
        static readonly Color32 CoinHighlight    = Hex("FFFDE7");
        static readonly Color32 ExhaustLight     = Hex("B0BEC5");
        static readonly Color32 ExhaustDark      = Hex("78909C");
        static readonly Color32 SpeedLine        = Hex("E0E0E0");
        static readonly Color32 SpeedLineDim     = Hex("9E9E9E");
        static readonly Color32 Clear            = new Color32(0, 0, 0, 0);

        const string SpritesRoot = "Assets/Art/Sprites";
        const string AnimRoot    = "Assets/Art/Animations";

        [MenuItem("Tools/Dash & Collect/Generate Animations")]
        public static void GenerateAll()
        {
            EnsureDirs();

            // ── Sprite Sheets ──────────────────────────────────────────────
            GeneratePlayerIdleSheet();
            GeneratePlayerRunSheet();
            GeneratePlayerJumpSheet();
            GeneratePlayerDeathSheet();
            GenerateCoinSpinSheet();

            AssetDatabase.Refresh();

            // ── Import Settings & Slicing ──────────────────────────────────
            SliceSheet($"{SpritesRoot}/Player/player-default-idle-sheet.png",  4, 16, 32, "player-idle");
            SliceSheet($"{SpritesRoot}/Player/player-default-run-sheet.png",   6, 16, 32, "player-run");
            SliceSheet($"{SpritesRoot}/Player/player-default-jump-sheet.png",  2, 16, 32, "player-jump");
            SliceSheet($"{SpritesRoot}/Player/player-default-death-sheet.png", 4, 16, 32, "player-death");
            SliceSheet($"{SpritesRoot}/Collectibles/coin-gold-spin-sheet.png", 6, 16, 16, "coin-spin");

            AssetDatabase.Refresh();

            // ── Animation Clips ────────────────────────────────────────────
            var idleClip  = CreateClip($"{SpritesRoot}/Player/player-default-idle-sheet.png",  "player-idle",  4,  8, true,  $"{AnimRoot}/Player/PlayerIdle.anim");
            var runClip   = CreateClip($"{SpritesRoot}/Player/player-default-run-sheet.png",   "player-run",   6, 12, true,  $"{AnimRoot}/Player/PlayerRun.anim");
            var jumpClip  = CreateClip($"{SpritesRoot}/Player/player-default-jump-sheet.png",  "player-jump",  2,  8, false, $"{AnimRoot}/Player/PlayerJump.anim");
            var deathClip = CreateClip($"{SpritesRoot}/Player/player-default-death-sheet.png", "player-death", 4,  8, false, $"{AnimRoot}/Player/PlayerDeath.anim");
            var spinClip  = CreateClip($"{SpritesRoot}/Collectibles/coin-gold-spin-sheet.png", "coin-spin",    6, 10, true,  $"{AnimRoot}/Collectibles/CoinSpin.anim");

            // ── Animator Controllers ───────────────────────────────────────
            CreatePlayerAnimator(idleClip, runClip, jumpClip, deathClip);
            CreateCoinAnimator(spinClip);

            // ── Wire to Prefabs ────────────────────────────────────────────
            WirePrefabs();

            Debug.Log("[AnimationAssetGenerator] All animation sheets, clips, animators, and prefab assignments complete.");
        }

        // ════════════════════════════════════════════════════════════════════
        // SPRITE SHEET GENERATORS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Player idle — 4 frames, 16x32 each = 64x32 sheet.
        /// Subtle exhaust pixel puff cycling at rear of car (Art Bible §5.3).
        /// </summary>
        static void GeneratePlayerIdleSheet()
        {
            var tex = new Texture2D(64, 32, TextureFormat.RGBA32, false);
            FillClear(tex);

            for (int f = 0; f < 4; f++)
            {
                int ox = f * 16;
                DrawCarBase(tex, ox);

                // Exhaust puff cycling — small pixels below rear bumper
                // Frame 0: 1 pixel, Frame 1: 2 pixels, Frame 2: 3 pixels, Frame 3: 2 pixels
                int puffSize = f == 0 ? 1 : f == 2 ? 3 : 2;
                Color32 puffCol = f % 2 == 0 ? ExhaustLight : ExhaustDark;
                for (int i = 0; i < puffSize; i++)
                    SetPx(tex, ox + 7 + i, 0, puffCol);
                if (puffSize >= 2)
                    SetPx(tex, ox + 8, 1, ExhaustDark); // rising puff
            }

            SavePNG(tex, "Player/player-default-idle-sheet.png");
        }

        /// <summary>
        /// Player run — 6 frames, 16x32 each = 96x32 sheet.
        /// Speed lines stream behind car, cycling position (Art Bible §5.3).
        /// </summary>
        static void GeneratePlayerRunSheet()
        {
            var tex = new Texture2D(96, 32, TextureFormat.RGBA32, false);
            FillClear(tex);

            for (int f = 0; f < 6; f++)
            {
                int ox = f * 16;
                DrawCarBase(tex, ox);

                // Speed lines behind car — 2-3 horizontal lines at varying Y
                // Lines shift down by 1 pixel each frame to create scroll effect
                int baseY = f % 3;
                for (int line = 0; line < 3; line++)
                {
                    int ly = 1 + baseY + line * 3;
                    if (ly >= 8) continue;
                    Color32 c = line == 0 ? SpeedLine : SpeedLineDim;
                    int len = 4 + (f + line) % 3; // varying length
                    int sx = ox + 6 - len / 2;
                    for (int x = sx; x < sx + len && x < ox + 16; x++)
                        if (x >= ox) SetPx(tex, x, ly, c);
                }

                // Slight body jitter on alternating frames — 1px lateral shift
                // Already drawn as base, overlay accent stripe shift on odd frames
                if (f % 2 == 1)
                {
                    SetPx(tex, ox + 2, 8, PlayerAccent);
                    SetPx(tex, ox + 13, 24, PlayerAccent);
                }
            }

            SavePNG(tex, "Player/player-default-run-sheet.png");
        }

        /// <summary>
        /// Player jump (boost) — 2 frames, 16x32 each = 32x32 sheet.
        /// Frame 0: car squashes (1px shorter). Frame 1: car stretches (1px taller).
        /// </summary>
        static void GeneratePlayerJumpSheet()
        {
            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            FillClear(tex);

            // Frame 0: squash — car body compressed, wheels wider feel
            int ox = 0;
            DrawCarBase(tex, ox);
            // Squash: fill bottom row to widen visual
            FillRect(tex, ox + 2, 0, 12, 1, PlayerGrey);
            // Slight darker overlay on top to visually shorten
            FillRect(tex, ox + 4, 31, 8, 1, PlayerGrey);

            // Frame 1: stretch — car elongated, boost glow at rear
            ox = 16;
            DrawCarBase(tex, ox);
            // Boost glow beneath
            SetPx(tex, ox + 6, 0, PlayerAccent);
            SetPx(tex, ox + 7, 0, PlayerAccent);
            SetPx(tex, ox + 8, 0, PlayerAccent);
            SetPx(tex, ox + 9, 0, PlayerAccent);
            SetPx(tex, ox + 7, 1, CoinHighlight);
            SetPx(tex, ox + 8, 1, CoinHighlight);

            SavePNG(tex, "Player/player-default-jump-sheet.png");
        }

        /// <summary>
        /// Player death — 4 frames, 16x32 each = 64x32 sheet.
        /// Spin-out: car rotates in steps (Art Bible §5.3 — freeze-frame + flash).
        /// Frame 0: normal. Frame 1: rotated ~45°. Frame 2: rotated ~90° (side view).
        /// Frame 3: white flash overlay.
        /// </summary>
        static void GeneratePlayerDeathSheet()
        {
            var tex = new Texture2D(64, 32, TextureFormat.RGBA32, false);
            FillClear(tex);

            // Frame 0: normal car (freeze frame)
            DrawCarBase(tex, 0);

            // Frame 1: angled — shift pixels to imply rotation
            int ox = 16;
            // Simplified angled car: narrower and offset
            FillRect(tex, ox + 3, 1, 10, 28, PlayerWhite);
            FillRect(tex, ox + 4, 3, 8, 2, PlayerGrey); // rear
            FillRect(tex, ox + 4, 8, 8, 2, PlayerAccent); // stripe
            FillRect(tex, ox + 5, 15, 6, 2, PlayerWindshield);
            FillRect(tex, ox + 4, 24, 8, 2, PlayerAccent); // front stripe
            // Side contour
            for (int y = 3; y < 28; y++)
            {
                SetPx(tex, ox + 3, y, PlayerGrey);
                SetPx(tex, ox + 12, y, PlayerGrey);
            }

            // Frame 2: sideways — car rotated 90° (wider, shorter)
            ox = 32;
            // Car seen from side: 32 wide compressed to 16, height reduced
            FillRect(tex, ox + 1, 8, 14, 16, PlayerWhite);
            FillRect(tex, ox + 2, 10, 12, 2, PlayerAccent); // stripe
            FillRect(tex, ox + 4, 15, 8, 2, PlayerWindshield);
            FillRect(tex, ox + 2, 20, 12, 2, PlayerAccent); // stripe
            for (int x = 2; x < 14; x++)
            {
                SetPx(tex, ox + x, 8, PlayerGrey);
                SetPx(tex, ox + x, 23, PlayerGrey);
            }

            // Frame 3: white flash overlay (Art Bible §8.5)
            ox = 48;
            DrawCarBase(tex, ox);
            // Overlay with semi-white flash
            Color32 flash = new Color32(255, 255, 255, 180);
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 16; x++)
                {
                    Color32 existing = tex.GetPixel(ox + x, y);
                    if (existing.a > 0)
                        SetPx(tex, ox + x, y, flash);
                }

            SavePNG(tex, "Player/player-default-death-sheet.png");
        }

        /// <summary>
        /// Coin spin — 6 frames, 16x16 each = 96x16 sheet.
        /// Rotation illusion: circle flattens, flips, expands (Art Bible §7.2).
        /// </summary>
        static void GenerateCoinSpinSheet()
        {
            var tex = new Texture2D(96, 16, TextureFormat.RGBA32, false);
            FillClear(tex);

            // Widths per frame to simulate rotation: full → narrow → edge → narrow → full → wide
            int[] halfWidths = { 6, 4, 1, 4, 6, 5 };

            for (int f = 0; f < 6; f++)
            {
                int ox = f * 16;
                int cx = ox + 8;
                int cy = 8;
                int hw = halfWidths[f];

                if (hw <= 1)
                {
                    // Edge-on: thin vertical line
                    for (int y = cy - 5; y <= cy + 5; y++)
                    {
                        SetPx(tex, cx, y, CoinYellowDark);
                        SetPx(tex, cx - 1, y, CoinYellow);
                    }
                }
                else
                {
                    // Ellipse — taller than wide to simulate perspective
                    int hh = 6; // half-height stays constant
                    DrawEllipseFilled(tex, cx, cy, hw, hh, CoinYellow);
                    // Inner ring
                    if (hw > 2)
                        DrawEllipseFilled(tex, cx, cy, hw - 2, hh - 2, CoinYellowDark);
                    // Centre highlight
                    if (hw > 3)
                    {
                        SetPx(tex, cx, cy, CoinHighlight);
                        SetPx(tex, cx - 1, cy, CoinHighlight);
                    }
                    // Star/cross detail
                    SetPx(tex, cx, cy + 2, CoinHighlight);
                    SetPx(tex, cx, cy - 2, CoinHighlight);
                }
            }

            SavePNG(tex, "Collectibles/coin-gold-spin-sheet.png");
        }

        // ════════════════════════════════════════════════════════════════════
        // CAR BASE DRAWING HELPER
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Draws the standard player car at offset ox (matches SpriteAssetGenerator).
        /// </summary>
        static void DrawCarBase(Texture2D tex, int ox)
        {
            // Rear bumper (rows 0-1)
            FillRect(tex, ox + 3, 0, 10, 2, PlayerGrey);
            // Tail lights
            FillRect(tex, ox + 2, 2, 12, 2, PlayerWhite);
            SetPx(tex, ox + 2, 2, SurgeRed); SetPx(tex, ox + 3, 2, SurgeRed);
            SetPx(tex, ox + 12, 2, SurgeRed); SetPx(tex, ox + 13, 2, SurgeRed);
            SetPx(tex, ox + 2, 3, SurgeRed); SetPx(tex, ox + 13, 3, SurgeRed);
            // Rear body (4-7)
            FillRect(tex, ox + 1, 4, 14, 4, PlayerWhite);
            // Rear accent stripe (8-9)
            FillRect(tex, ox + 1, 8, 14, 2, PlayerAccent);
            // Mid body (10-14)
            FillRect(tex, ox + 1, 10, 14, 5, PlayerWhite);
            // Windshield (15-18)
            FillRect(tex, ox + 1, 15, 14, 4, PlayerWhite);
            FillRect(tex, ox + 3, 16, 10, 2, PlayerWindshield);
            // Hood (19-23)
            FillRect(tex, ox + 1, 19, 14, 5, PlayerWhite);
            // Front accent stripe (24-25)
            FillRect(tex, ox + 1, 24, 14, 2, PlayerAccent);
            // Front body / nose (26-29)
            FillRect(tex, ox + 2, 26, 12, 3, PlayerWhite);
            FillRect(tex, ox + 3, 29, 10, 2, PlayerGrey);
            // Front bumper (30-31)
            FillRect(tex, ox + 4, 30, 8, 2, PlayerGrey);
            // Side edges
            for (int y = 4; y < 30; y++)
            {
                SetPx(tex, ox + 1, y, PlayerGrey);
                SetPx(tex, ox + 14, y, PlayerGrey);
            }
            // Fender curves
            SetPx(tex, ox + 0, 6, PlayerGrey); SetPx(tex, ox + 0, 7, PlayerGrey);
            SetPx(tex, ox + 15, 6, PlayerGrey); SetPx(tex, ox + 15, 7, PlayerGrey);
            SetPx(tex, ox + 0, 22, PlayerGrey); SetPx(tex, ox + 0, 23, PlayerGrey);
            SetPx(tex, ox + 15, 22, PlayerGrey); SetPx(tex, ox + 15, 23, PlayerGrey);
            // Headlights
            SetPx(tex, ox + 3, 29, CoinYellow); SetPx(tex, ox + 4, 29, CoinYellow);
            SetPx(tex, ox + 11, 29, CoinYellow); SetPx(tex, ox + 12, 29, CoinYellow);
        }

        // ════════════════════════════════════════════════════════════════════
        // IMPORT SETTINGS & SLICING
        // ════════════════════════════════════════════════════════════════════

        static void SliceSheet(string path, int frameCount, int frameW, int frameH, string namePrefix)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[AnimationAssetGenerator] No importer at {path}");
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

            // Determine pivot based on entity type
            bool isPlayer = namePrefix.StartsWith("player");
            int alignment = isPlayer
                ? (int)SpriteAlignment.BottomCenter   // car feet align with road
                : (int)SpriteAlignment.Center;         // collectibles centred
            Vector2 pivot = isPlayer
                ? new Vector2(0.5f, 0f)
                : new Vector2(0.5f, 0.5f);

            var slices = new SpriteMetaData[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                slices[i] = new SpriteMetaData
                {
                    name = $"{namePrefix}-{i:D2}",
                    rect = new Rect(i * frameW, 0, frameW, frameH),
                    alignment = alignment,
                    pivot = pivot,
                    border = Vector4.zero,
                };
            }
            importer.spritesheet = slices;

            // Android ETC2 override
            var androidOverride = importer.GetPlatformTextureSettings("Android");
            androidOverride.overridden = true;
            androidOverride.format = TextureImporterFormat.ETC2_RGBA8;
            androidOverride.compressionQuality = 50;
            androidOverride.maxTextureSize = 2048;
            importer.SetPlatformTextureSettings(androidOverride);

            importer.SaveAndReimport();
        }

        // ════════════════════════════════════════════════════════════════════
        // ANIMATION CLIPS
        // ════════════════════════════════════════════════════════════════════

        static AnimationClip CreateClip(string sheetPath, string namePrefix, int frameCount,
                                         int fps, bool loop, string clipPath)
        {
            // Gather sliced sprites
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(sheetPath);
            var sprites = new List<Sprite>();
            for (int i = 0; i < frameCount; i++)
            {
                string spriteName = $"{namePrefix}-{i:D2}";
                foreach (var obj in allAssets)
                {
                    if (obj is Sprite s && s.name == spriteName)
                    {
                        sprites.Add(s);
                        break;
                    }
                }
            }

            if (sprites.Count != frameCount)
            {
                Debug.LogWarning($"[AnimationAssetGenerator] Expected {frameCount} sprites for {namePrefix}, found {sprites.Count}");
                return null;
            }

            // Create clip
            var clip = new AnimationClip();
            clip.frameRate = fps;
            clip.name = Path.GetFileNameWithoutExtension(clipPath);

            // Build sprite keyframes
            var keyframes = new ObjectReferenceKeyframe[frameCount];
            float frameDuration = 1f / fps;
            for (int i = 0; i < frameCount; i++)
            {
                keyframes[i] = new ObjectReferenceKeyframe
                {
                    time = i * frameDuration,
                    value = sprites[i],
                };
            }

            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

            // Loop settings
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            // Ensure directory exists
            string dir = Path.GetDirectoryName(clipPath);
            if (!Directory.Exists(Path.GetFullPath(dir)))
                Directory.CreateDirectory(Path.GetFullPath(dir));

            // Save or replace
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(clip, existing);
                Object.DestroyImmediate(clip);
                return existing;
            }

            AssetDatabase.CreateAsset(clip, clipPath);
            return clip;
        }

        // ════════════════════════════════════════════════════════════════════
        // ANIMATOR CONTROLLERS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Player AnimatorController with states and transitions per the task spec:
        ///   Parameters: isRunning (bool), isJumping (bool), isDead (trigger)
        ///   idle (8fps, loop) → run (isRunning=true)
        ///   run (12fps, loop) → jump (isJumping=true)
        ///   jump (8fps) → run (isJumping=false)
        ///   any → death (isDead trigger)
        /// </summary>
        static void CreatePlayerAnimator(AnimationClip idle, AnimationClip run,
                                          AnimationClip jump, AnimationClip death)
        {
            string path = $"{AnimRoot}/Player/PlayerAnimator.controller";
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(Path.GetFullPath(dir)))
                Directory.CreateDirectory(Path.GetFullPath(dir));

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            // Clear existing layers/parameters to rebuild cleanly
            while (controller.layers.Length > 1)
                controller.RemoveLayer(1);

            // Parameters
            ClearParameters(controller);
            controller.AddParameter("isRunning", AnimatorControllerParameterType.Bool);
            controller.AddParameter("isJumping", AnimatorControllerParameterType.Bool);
            controller.AddParameter("isDead", AnimatorControllerParameterType.Trigger);

            // Get base layer state machine
            var sm = controller.layers[0].stateMachine;
            ClearStates(sm);

            // States
            var idleState  = sm.AddState("Idle",  new Vector3(200, 0, 0));
            var runState   = sm.AddState("Run",   new Vector3(450, 0, 0));
            var jumpState  = sm.AddState("Jump",  new Vector3(450, -100, 0));
            var deathState = sm.AddState("Death", new Vector3(200, -200, 0));

            idleState.motion  = idle;
            runState.motion   = run;
            jumpState.motion  = jump;
            deathState.motion = death;

            // Speed parameters for clip playback (all clips are authored at their target fps)
            idleState.speed  = 1f;
            runState.speed   = 1f;
            jumpState.speed  = 1f;
            deathState.speed = 1f;

            sm.defaultState = idleState;

            // Transitions (all with no exit time for snappy feel)
            // idle → run
            var t1 = idleState.AddTransition(runState);
            t1.AddCondition(AnimatorConditionMode.If, 0, "isRunning");
            t1.hasExitTime = false;
            t1.duration = 0f;

            // run → idle
            var t2 = runState.AddTransition(idleState);
            t2.AddCondition(AnimatorConditionMode.IfNot, 0, "isRunning");
            t2.hasExitTime = false;
            t2.duration = 0f;

            // run → jump
            var t3 = runState.AddTransition(jumpState);
            t3.AddCondition(AnimatorConditionMode.If, 0, "isJumping");
            t3.hasExitTime = false;
            t3.duration = 0f;

            // jump → run (when isJumping goes false)
            var t4 = jumpState.AddTransition(runState);
            t4.AddCondition(AnimatorConditionMode.IfNot, 0, "isJumping");
            t4.hasExitTime = false;
            t4.duration = 0f;

            // any → death
            var tDeath = sm.AddAnyStateTransition(deathState);
            tDeath.AddCondition(AnimatorConditionMode.If, 0, "isDead");
            tDeath.hasExitTime = false;
            tDeath.duration = 0f;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Coin AnimatorController: single spin state (10fps, loop).
        /// </summary>
        static void CreateCoinAnimator(AnimationClip spin)
        {
            string path = $"{AnimRoot}/Collectibles/CoinAnimator.controller";
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(Path.GetFullPath(dir)))
                Directory.CreateDirectory(Path.GetFullPath(dir));

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            while (controller.layers.Length > 1)
                controller.RemoveLayer(1);

            var sm = controller.layers[0].stateMachine;
            ClearStates(sm);

            var spinState = sm.AddState("Spin", new Vector3(200, 0, 0));
            spinState.motion = spin;
            spinState.speed = 1f;
            sm.defaultState = spinState;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        // ════════════════════════════════════════════════════════════════════
        // PREFAB WIRING
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Assigns AnimatorControllers to Player and coin-bearing prefabs.
        /// Adds Animator component if missing.
        /// </summary>
        static void WirePrefabs()
        {
            var playerController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                $"{AnimRoot}/Player/PlayerAnimator.controller");
            var coinController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                $"{AnimRoot}/Collectibles/CoinAnimator.controller");

            // Player prefab
            WireAnimator("Assets/Prefabs/Player/Player.prefab", playerController);

            // Chunk prefabs — wire coin animator to any child with a Collectible component
            // where Type == Coin (3). For now we wire to all collectible children.
            string[] chunkPaths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Chunks" });
            foreach (string guid in chunkPaths)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (coinController == null) continue;

                var prefab = PrefabUtility.LoadPrefabContents(path);
                bool changed = false;

                // Find all SpriteRenderers whose sibling MonoBehaviour has a Type field == 3 (Coin)
                var renderers = prefab.GetComponentsInChildren<SpriteRenderer>(true);
                foreach (var sr in renderers)
                {
                    var behaviours = sr.GetComponents<MonoBehaviour>();
                    foreach (var mb in behaviours)
                    {
                        if (mb == null) continue;
                        var so = new SerializedObject(mb);
                        var typeProp = so.FindProperty("Type");
                        if (typeProp == null || typeProp.intValue != 3) continue; // 3 == Coin

                        var animator = sr.GetComponent<Animator>();
                        if (animator == null)
                            animator = sr.gameObject.AddComponent<Animator>();
                        animator.runtimeAnimatorController = coinController;
                        changed = true;
                    }
                }

                if (changed)
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }

        static void WireAnimator(string prefabPath, RuntimeAnimatorController controller)
        {
            if (controller == null) return;

            var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[AnimationAssetGenerator] Prefab not found: {prefabPath}");
                return;
            }

            var animator = prefab.GetComponent<Animator>();
            if (animator == null)
                animator = prefab.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            PrefabUtility.UnloadPrefabContents(prefab);
        }

        // ════════════════════════════════════════════════════════════════════
        // UTILITY HELPERS
        // ════════════════════════════════════════════════════════════════════

        static void ClearParameters(AnimatorController controller)
        {
            // Remove all existing parameters
            while (controller.parameters.Length > 0)
                controller.RemoveParameter(0);
        }

        static void ClearStates(AnimatorStateMachine sm)
        {
            // Remove all existing states
            var states = sm.states;
            foreach (var s in states)
                sm.RemoveState(s.state);

            // Remove any-state transitions
            var anyTransitions = sm.anyStateTransitions;
            foreach (var t in anyTransitions)
                sm.RemoveAnyStateTransition(t);
        }

        static void EnsureDirs()
        {
            string[] dirs =
            {
                $"{SpritesRoot}/Player",
                $"{SpritesRoot}/Collectibles",
                $"{AnimRoot}/Player",
                $"{AnimRoot}/Collectibles",
            };
            foreach (string d in dirs)
            {
                string full = Path.GetFullPath(d);
                if (!Directory.Exists(full)) Directory.CreateDirectory(full);
            }
        }

        static void FillClear(Texture2D tex)
        {
            var px = new Color32[tex.width * tex.height];
            for (int i = 0; i < px.Length; i++) px[i] = Clear;
            tex.SetPixels32(px);
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

        static void DrawEllipseFilled(Texture2D tex, int cx, int cy, int rx, int ry, Color32 col)
        {
            for (int y = cy - ry; y <= cy + ry; y++)
                for (int x = cx - rx; x <= cx + rx; x++)
                {
                    float dx = (float)(x - cx) / rx;
                    float dy = (float)(y - cy) / ry;
                    if (dx * dx + dy * dy <= 1f)
                        SetPx(tex, x, y, col);
                }
        }

        static Color32 Hex(string hex)
        {
            ColorUtility.TryParseHtmlString($"#{hex}", out Color c);
            return c;
        }

        static void SavePNG(Texture2D tex, string relPath)
        {
            string fullPath = Path.GetFullPath($"{SpritesRoot}/{relPath}");
            File.WriteAllBytes(fullPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

    }
}
