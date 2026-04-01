using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DashAndCollect.Editor
{
    /// <summary>
    /// Wires generated UI assets to the scene UI hierarchy for Dash & Collect.
    ///
    /// Finds existing canvases (HUD, DeathScreen) and creates missing GameObjects
    /// (MainMenu canvas, score panel, coin icon, overlay background, result panel,
    /// main menu button, final score label).
    ///
    /// All sprites loaded from Assets/_Project/Art/UI/ per SPRITE-PIPELINE.md §7.
    /// All colours/sizes from ART-BIBLE.md §9 (UI Style Guide).
    ///
    /// Menu: Tools -> Wire UI Assets
    /// </summary>
    public static class UIAssetWirer
    {
        // ── ART-BIBLE §9.1 — UI Palette ──────────────────────────────────
        static readonly Color TextPrimary   = HexColor("FAFAFA");
        static readonly Color TextSecondary  = HexColor("BDBDBD");
        static readonly Color AccentCoin     = HexColor("FFEE58");

        // ── ART-BIBLE §9.2 — Typography sizes ────────────────────────────
        const int FontTitle    = 32;
        const int FontSubtitle = 24;
        const int FontBody     = 16;
        const int FontLabel    = 12;

        // ── Asset paths (SPRITE-PIPELINE §7.1 naming) ─────────────────────
        const string UI = "Assets/_Project/Art/UI";

        const string BtnPlayNormal       = UI + "/Buttons/ui-btn-play-normal.png";
        const string BtnPlayHover        = UI + "/Buttons/ui-btn-play-hover.png";
        const string BtnPlayPressed      = UI + "/Buttons/ui-btn-play-pressed.png";
        const string BtnPlayDisabled     = UI + "/Buttons/ui-btn-play-disabled.png";

        const string BtnRetryNormal      = UI + "/Buttons/ui-btn-retry-normal.png";
        const string BtnRetryHover       = UI + "/Buttons/ui-btn-retry-hover.png";
        const string BtnRetryPressed     = UI + "/Buttons/ui-btn-retry-pressed.png";
        const string BtnRetryDisabled    = UI + "/Buttons/ui-btn-retry-disabled.png";

        const string BtnMainMenuNormal   = UI + "/Buttons/ui-btn-mainmenu-normal.png";
        const string BtnMainMenuHover    = UI + "/Buttons/ui-btn-mainmenu-hover.png";
        const string BtnMainMenuPressed  = UI + "/Buttons/ui-btn-mainmenu-pressed.png";
        const string BtnMainMenuDisabled = UI + "/Buttons/ui-btn-mainmenu-disabled.png";

        const string PanelDialog         = UI + "/Panels/ui-panel-dialog-normal.png";
        const string PanelHud            = UI + "/Panels/ui-panel-hud-normal.png";
        const string PanelTooltip        = UI + "/Panels/ui-panel-tooltip-normal.png";

        const string IconCoin            = UI + "/Icons/ui-icon-coin-normal.png";

        const string BgGameOver          = UI + "/Backgrounds/ui-bg-gameover-normal.png";
        const string BgMenu              = UI + "/Backgrounds/ui-bg-menu-normal.png";

        // ════════════════════════════════════════════════════════════════════
        // ENTRY POINT
        // ════════════════════════════════════════════════════════════════════

        [MenuItem("Tools/Wire UI Assets")]
        public static void WireAll()
        {
            int wired = 0;
            wired += WireMainMenu();
            wired += WireHUD();
            wired += WireGameOver();

            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log($"[UIAssetWirer] Wired {wired} UI elements. Save the scene to persist changes.");
        }

        // ════════════════════════════════════════════════════════════════════
        // 1. MAIN MENU — creates a new Canvas if not found
        // ════════════════════════════════════════════════════════════════════

        static int WireMainMenu()
        {
            int count = 0;

            // Find or create MainMenu canvas
            var menuCanvas = FindCanvasByName("MainMenu");
            if (menuCanvas == null)
            {
                var menuGo = new GameObject("MainMenu");
                Undo.RegisterCreatedObjectUndo(menuGo, "Create MainMenu Canvas");

                menuCanvas = menuGo.AddComponent<Canvas>();
                menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                menuCanvas.sortingOrder = 5;
                menuGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                menuGo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(800, 600);
                menuGo.AddComponent<GraphicRaycaster>();
            }

            var root = menuCanvas.transform;

            // ── Background panel (dialog panel sprite with 9-slice) ──
            var bgPanel = FindOrCreateChild(root, "BackgroundPanel");
            var bgImage = EnsureComponent<Image>(bgPanel);
            bgImage.sprite = LoadSprite(PanelDialog);
            bgImage.type = Image.Type.Sliced;
            bgImage.color = Color.white;
            StretchFill(bgPanel.GetComponent<RectTransform>());
            count++;

            // ── Title text ──
            var titleGo = FindOrCreateChild(root, "TitleText");
            var titleText = EnsureComponent<Text>(titleGo);
            titleText.text = "DASH & COLLECT";
            titleText.fontSize = FontTitle;
            titleText.color = TextPrimary;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.fontStyle = FontStyle.Bold;
            var titleRect = titleGo.GetComponent<RectTransform>();
            SetAnchors(titleRect, new Vector2(0.1f, 0.65f), new Vector2(0.9f, 0.85f));
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            count++;

            // ── High score text ──
            var highScoreGo = FindOrCreateChild(root, "HighScoreText");
            var highScoreText = EnsureComponent<Text>(highScoreGo);
            highScoreText.text = "HIGH SCORE: 0";
            highScoreText.fontSize = FontBody;
            highScoreText.color = TextSecondary;
            highScoreText.alignment = TextAnchor.MiddleCenter;
            var hsRect = highScoreGo.GetComponent<RectTransform>();
            SetAnchors(hsRect, new Vector2(0.2f, 0.5f), new Vector2(0.8f, 0.62f));
            hsRect.offsetMin = Vector2.zero;
            hsRect.offsetMax = Vector2.zero;
            count++;

            // ── Play button ──
            var playBtnGo = FindOrCreateChild(root, "PlayButton");
            var playImage = EnsureComponent<Image>(playBtnGo);
            var playBtn = EnsureComponent<Button>(playBtnGo);
            ConfigureButton(playBtn, playImage,
                BtnPlayNormal, BtnPlayHover, BtnPlayPressed, BtnPlayDisabled);
            playImage.type = Image.Type.Sliced;
            EnsureComponent<UIButtonFeedback>(playBtnGo);
            var playRect = playBtnGo.GetComponent<RectTransform>();
            SetAnchors(playRect, new Vector2(0.3f, 0.28f), new Vector2(0.7f, 0.42f));
            playRect.offsetMin = Vector2.zero;
            playRect.offsetMax = Vector2.zero;

            // Play button label
            var playLabelGo = FindOrCreateChild(playBtnGo.transform, "Label");
            var playLabel = EnsureComponent<Text>(playLabelGo);
            playLabel.text = "PLAY";
            playLabel.fontSize = FontBody;
            playLabel.color = TextPrimary;
            playLabel.alignment = TextAnchor.MiddleCenter;
            playLabel.fontStyle = FontStyle.Bold;
            StretchFill(playLabelGo.GetComponent<RectTransform>());
            count++;

            // ── CanvasGroup for fade transition ──
            var menuCanvasGroup = EnsureComponent<CanvasGroup>(menuCanvas.gameObject);
            count++;

            // ── MainMenuController — wires Play button and GameManager ──
            var menuController = EnsureComponent<MainMenuController>(menuCanvas.gameObject);
            var gameManager = Object.FindFirstObjectByType<GameManager>();
            if (gameManager != null)
            {
                var so = new SerializedObject(menuController);
                SetSerializedRef(so, "_gameManager", gameManager);
                SetSerializedRef(so, "_playButton", playBtn);
                SetSerializedRef(so, "_highScoreLabel", highScoreText);
                SetSerializedRef(so, "_canvasGroup", menuCanvasGroup);
                so.ApplyModifiedProperties();
                count++;
            }
            else
            {
                Debug.LogWarning("[UIAssetWirer] GameManager not found in scene. " +
                                 "Wire MainMenuController._gameManager manually.");
            }

            return count;
        }

        // ════════════════════════════════════════════════════════════════════
        // 2. GAMEPLAY HUD — wires existing HUD canvas
        // ════════════════════════════════════════════════════════════════════

        static int WireHUD()
        {
            int count = 0;

            var hudCanvas = FindCanvasByName("HUD");
            if (hudCanvas == null)
            {
                Debug.LogWarning("[UIAssetWirer] HUD canvas not found in scene. Skipping HUD wiring.");
                return 0;
            }

            var root = hudCanvas.transform;

            // ── Score panel (9-sliced HUD panel) ──
            var scorePanelGo = FindOrCreateChild(root, "ScorePanel");
            var scorePanelImage = EnsureComponent<Image>(scorePanelGo);
            scorePanelImage.sprite = LoadSprite(PanelHud);
            scorePanelImage.type = Image.Type.Sliced;
            scorePanelImage.color = Color.white;
            var spRect = scorePanelGo.GetComponent<RectTransform>();
            // Top-right corner — ART-BIBLE §8.6: score is top-right
            SetAnchors(spRect, new Vector2(0.55f, 0.88f), new Vector2(0.98f, 0.98f));
            spRect.offsetMin = Vector2.zero;
            spRect.offsetMax = Vector2.zero;
            count++;

            // ── Coin icon ──
            var coinIconGo = FindOrCreateChild(scorePanelGo.transform, "CoinIcon");
            var coinImage = EnsureComponent<Image>(coinIconGo);
            coinImage.sprite = LoadSprite(IconCoin);
            coinImage.type = Image.Type.Simple;
            coinImage.preserveAspect = true;
            coinImage.SetNativeSize();
            var ciRect = coinIconGo.GetComponent<RectTransform>();
            ciRect.anchorMin = new Vector2(0.05f, 0.15f);
            ciRect.anchorMax = new Vector2(0.05f, 0.85f);
            ciRect.offsetMin = Vector2.zero;
            ciRect.offsetMax = new Vector2(24, 0);
            ciRect.sizeDelta = new Vector2(24, 24);
            ciRect.anchoredPosition = new Vector2(20, 0);
            count++;

            // ── Coin label ──
            var coinLabelGo = FindOrCreateChild(scorePanelGo.transform, "CoinLabel");
            var coinLabel = EnsureComponent<Text>(coinLabelGo);
            coinLabel.text = "0";
            coinLabel.fontSize = FontBody;
            coinLabel.color = TextPrimary;
            coinLabel.alignment = TextAnchor.MiddleLeft;
            var clRect = coinLabelGo.GetComponent<RectTransform>();
            SetAnchors(clRect, new Vector2(0.18f, 0.1f), new Vector2(0.48f, 0.9f));
            clRect.offsetMin = Vector2.zero;
            clRect.offsetMax = Vector2.zero;
            count++;

            // ── Score label ──
            var scoreLabelGo = FindOrCreateChild(scorePanelGo.transform, "ScoreLabel");
            var scoreLabel = EnsureComponent<Text>(scoreLabelGo);
            scoreLabel.text = "0";
            scoreLabel.fontSize = FontBody;
            scoreLabel.color = TextPrimary;
            scoreLabel.alignment = TextAnchor.MiddleRight;
            var slRect = scoreLabelGo.GetComponent<RectTransform>();
            SetAnchors(slRect, new Vector2(0.52f, 0.1f), new Vector2(0.95f, 0.9f));
            slRect.offsetMin = Vector2.zero;
            slRect.offsetMax = Vector2.zero;
            count++;

            // ── Wire HUDController serialized fields ──
            var hudController = hudCanvas.GetComponent<HUDController>();
            if (hudController != null)
            {
                var so = new SerializedObject(hudController);
                SetSerializedRef(so, "_scoreLabel", scoreLabel);
                SetSerializedRef(so, "_coinLabel", coinLabel);
                SetSerializedRef(so, "_coinIcon", coinIconGo.transform);
                so.ApplyModifiedProperties();
                count++;
            }

            // ── Remove old placeholder Label if present ──
            var oldLabel = root.Find("Label");
            if (oldLabel != null)
            {
                var oldText = oldLabel.GetComponent<Text>();
                if (oldText != null && oldText.text.Contains("placeholder"))
                {
                    Undo.DestroyObjectImmediate(oldLabel.gameObject);
                    count++;
                }
            }

            return count;
        }

        // ════════════════════════════════════════════════════════════════════
        // 3. GAME OVER — wires existing DeathScreen canvas
        // ════════════════════════════════════════════════════════════════════

        static int WireGameOver()
        {
            int count = 0;

            var deathCanvas = FindCanvasByName("DeathScreen");
            if (deathCanvas == null)
            {
                Debug.LogWarning("[UIAssetWirer] DeathScreen canvas not found in scene. Skipping GameOver wiring.");
                return 0;
            }

            // Temporarily activate so we can manipulate children
            bool wasActive = deathCanvas.gameObject.activeSelf;
            deathCanvas.gameObject.SetActive(true);

            var root = deathCanvas.transform;

            // ── Overlay background (full-screen semi-transparent) ──
            var overlayGo = FindOrCreateChild(root, "OverlayBg");
            overlayGo.transform.SetAsFirstSibling(); // behind everything
            var overlayImage = EnsureComponent<Image>(overlayGo);
            overlayImage.sprite = LoadSprite(BgGameOver);
            overlayImage.type = Image.Type.Simple;
            overlayImage.color = Color.white; // opacity baked into the sprite
            overlayImage.raycastTarget = false;
            StretchFill(overlayGo.GetComponent<RectTransform>());
            count++;

            // ── Result panel (dialog panel with 9-slice) ──
            var resultPanelGo = FindOrCreateChild(root, "ResultPanel");
            resultPanelGo.transform.SetSiblingIndex(1); // above overlay
            var resultImage = EnsureComponent<Image>(resultPanelGo);
            resultImage.sprite = LoadSprite(PanelDialog);
            resultImage.type = Image.Type.Sliced;
            resultImage.color = Color.white;
            var rpRect = resultPanelGo.GetComponent<RectTransform>();
            SetAnchors(rpRect, new Vector2(0.1f, 0.15f), new Vector2(0.9f, 0.85f));
            rpRect.offsetMin = Vector2.zero;
            rpRect.offsetMax = Vector2.zero;
            count++;

            // ── "GAME OVER" title ──
            var goTitleGo = FindOrCreateChild(resultPanelGo.transform, "GameOverTitle");
            var goTitle = EnsureComponent<Text>(goTitleGo);
            goTitle.text = "GAME OVER";
            goTitle.fontSize = FontTitle;
            goTitle.color = TextPrimary;
            goTitle.alignment = TextAnchor.MiddleCenter;
            goTitle.fontStyle = FontStyle.Bold;
            var goTitleRect = goTitleGo.GetComponent<RectTransform>();
            SetAnchors(goTitleRect, new Vector2(0.05f, 0.75f), new Vector2(0.95f, 0.95f));
            goTitleRect.offsetMin = Vector2.zero;
            goTitleRect.offsetMax = Vector2.zero;
            count++;

            // ── Final score label (24px, accent colour) ──
            var finalScoreGo = FindOrCreateChild(resultPanelGo.transform, "FinalScoreLabel");
            var finalScoreText = EnsureComponent<Text>(finalScoreGo);
            finalScoreText.text = "0";
            finalScoreText.fontSize = FontSubtitle;
            finalScoreText.color = AccentCoin;
            finalScoreText.alignment = TextAnchor.MiddleCenter;
            finalScoreText.fontStyle = FontStyle.Bold;
            var fsRect = finalScoreGo.GetComponent<RectTransform>();
            SetAnchors(fsRect, new Vector2(0.1f, 0.52f), new Vector2(0.9f, 0.72f));
            fsRect.offsetMin = Vector2.zero;
            fsRect.offsetMax = Vector2.zero;
            count++;

            // ── Retry button ──
            var retryBtnGo = root.Find("RetryButton");
            if (retryBtnGo == null)
                retryBtnGo = FindOrCreateChild(resultPanelGo.transform, "RetryButton").transform;
            else
            {
                // Reparent existing RetryButton under ResultPanel
                Undo.SetTransformParent(retryBtnGo, resultPanelGo.transform, "Reparent RetryButton");
            }

            var retryImage = EnsureComponent<Image>(retryBtnGo.gameObject);
            var retryBtn = EnsureComponent<Button>(retryBtnGo.gameObject);
            ConfigureButton(retryBtn, retryImage,
                BtnRetryNormal, BtnRetryHover, BtnRetryPressed, BtnRetryDisabled);
            retryImage.type = Image.Type.Sliced;
            EnsureComponent<UIButtonFeedback>(retryBtnGo.gameObject);
            var retryRect = retryBtnGo.GetComponent<RectTransform>();
            SetAnchors(retryRect, new Vector2(0.1f, 0.12f), new Vector2(0.45f, 0.3f));
            retryRect.offsetMin = Vector2.zero;
            retryRect.offsetMax = Vector2.zero;

            // Retry button label — find existing Text child or create
            var retryLabelTransform = retryBtnGo.Find("Text") ?? retryBtnGo.Find("Label");
            GameObject retryLabelGo;
            if (retryLabelTransform != null)
                retryLabelGo = retryLabelTransform.gameObject;
            else
                retryLabelGo = FindOrCreateChild(retryBtnGo, "Label");

            var retryLabel = EnsureComponent<Text>(retryLabelGo);
            retryLabel.text = "RETRY";
            retryLabel.fontSize = FontBody;
            retryLabel.color = TextPrimary;
            retryLabel.alignment = TextAnchor.MiddleCenter;
            retryLabel.fontStyle = FontStyle.Bold;
            StretchFill(retryLabelGo.GetComponent<RectTransform>());
            count++;

            // ── Main Menu button ──
            var mainMenuBtnGo = FindOrCreateChild(resultPanelGo.transform, "MainMenuButton");
            var mainMenuImage = EnsureComponent<Image>(mainMenuBtnGo);
            var mainMenuBtn = EnsureComponent<Button>(mainMenuBtnGo);
            ConfigureButton(mainMenuBtn, mainMenuImage,
                BtnMainMenuNormal, BtnMainMenuHover, BtnMainMenuPressed, BtnMainMenuDisabled);
            mainMenuImage.type = Image.Type.Sliced;
            EnsureComponent<UIButtonFeedback>(mainMenuBtnGo);
            var mmRect = mainMenuBtnGo.GetComponent<RectTransform>();
            SetAnchors(mmRect, new Vector2(0.55f, 0.12f), new Vector2(0.9f, 0.3f));
            mmRect.offsetMin = Vector2.zero;
            mmRect.offsetMax = Vector2.zero;

            // Main menu button label
            var mmLabelGo = FindOrCreateChild(mainMenuBtnGo.transform, "Label");
            var mmLabel = EnsureComponent<Text>(mmLabelGo);
            mmLabel.text = "MENU";
            mmLabel.fontSize = FontBody;
            mmLabel.color = TextPrimary;
            mmLabel.alignment = TextAnchor.MiddleCenter;
            mmLabel.fontStyle = FontStyle.Bold;
            StretchFill(mmLabelGo.GetComponent<RectTransform>());
            count++;

            // ── Ensure CanvasGroup for animated fade ──
            var canvasGroup = EnsureComponent<CanvasGroup>(deathCanvas.gameObject);
            count++;

            // ── Wire DeathScreenController serialized fields ──
            var deathController = deathCanvas.GetComponent<DeathScreenController>();
            if (deathController != null)
            {
                var so = new SerializedObject(deathController);
                SetSerializedRef(so, "_finalScoreLabel", finalScoreText);
                SetSerializedRef(so, "_retryButton", retryBtn);
                SetSerializedRef(so, "_mainMenuButton", mainMenuBtn);
                SetSerializedRef(so, "_canvasGroup", canvasGroup);
                SetSerializedRef(so, "_resultPanel", resultPanelGo.transform);
                so.ApplyModifiedProperties();
                count++;
            }

            // ── Remove old placeholder Label if present ──
            var oldLabel = root.Find("Label");
            if (oldLabel != null)
            {
                var oldText = oldLabel.GetComponent<Text>();
                if (oldText != null && oldText.text.Contains("placeholder"))
                {
                    Undo.DestroyObjectImmediate(oldLabel.gameObject);
                    count++;
                }
            }

            // Restore original active state
            deathCanvas.gameObject.SetActive(wasActive);

            return count;
        }

        // ════════════════════════════════════════════════════════════════════
        // BUTTON CONFIGURATION — ART-BIBLE §9.5 (SpriteSwap states)
        // ════════════════════════════════════════════════════════════════════

        static void ConfigureButton(Button btn, Image targetImage,
            string normalPath, string hoverPath, string pressedPath, string disabledPath)
        {
            Undo.RecordObject(btn, "Configure Button SpriteState");
            Undo.RecordObject(targetImage, "Set Button Sprite");

            // Normal sprite on the Image component
            targetImage.sprite = LoadSprite(normalPath);

            // Switch to SpriteSwap transition
            btn.transition = Selectable.Transition.SpriteSwap;
            btn.targetGraphic = targetImage;

            var spriteState = new SpriteState
            {
                highlightedSprite = LoadSprite(hoverPath),
                pressedSprite     = LoadSprite(pressedPath),
                disabledSprite    = LoadSprite(disabledPath),
                selectedSprite    = LoadSprite(hoverPath)
            };
            btn.spriteState = spriteState;
        }

        // ════════════════════════════════════════════════════════════════════
        // SERIALIZED FIELD WIRING
        // ════════════════════════════════════════════════════════════════════

        static void SetSerializedRef(SerializedObject so, string fieldName, Object value)
        {
            var prop = so.FindProperty(fieldName);
            if (prop != null)
            {
                prop.objectReferenceValue = value;
            }
            else
            {
                Debug.LogWarning(
                    $"[UIAssetWirer] Field '{fieldName}' not found on {so.targetObject.GetType().Name}");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // HIERARCHY HELPERS
        // ════════════════════════════════════════════════════════════════════

        static Canvas FindCanvasByName(string name)
        {
            var allCanvas = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in allCanvas)
            {
                if (c.gameObject.name == name)
                    return c;
            }
            return null;
        }

        static GameObject FindOrCreateChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                Undo.RecordObject(existing.gameObject, $"Update {name}");
                return existing.gameObject;
            }

            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            go.transform.SetParent(parent, false);
            return go;
        }

        static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var comp = go.GetComponent<T>();
            if (comp == null)
            {
                comp = Undo.AddComponent<T>(go);
            }
            else
            {
                Undo.RecordObject(comp, $"Update {typeof(T).Name}");
            }
            return comp;
        }

        // ════════════════════════════════════════════════════════════════════
        // RECT TRANSFORM HELPERS
        // ════════════════════════════════════════════════════════════════════

        static void StretchFill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
        }

        // ════════════════════════════════════════════════════════════════════
        // ASSET LOADING
        // ════════════════════════════════════════════════════════════════════

        static Sprite LoadSprite(string assetPath)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
                Debug.LogWarning($"[UIAssetWirer] Sprite not found at {assetPath}. " +
                                 "Run Tools -> Generate UI Assets first.");
            return sprite;
        }

        static Color HexColor(string hex)
        {
            ColorUtility.TryParseHtmlString($"#{hex}", out Color color);
            return color;
        }
    }
}
