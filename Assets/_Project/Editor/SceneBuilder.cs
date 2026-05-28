using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using MathGame.Core;
using MathGame.UI;

namespace MathGame.Editor
{
    /// <summary>
    /// Builds the full game scene programmatically.
    /// Menu: Tools → MathGame → Build Game Scene
    /// </summary>
    public static class SceneBuilder
    {
        private const string SCENE_PATH     = "Assets/_Project/Scenes/GameScene.unity";
        private const string TILE_PREFAB    = "Assets/_Project/Prefabs/NumberTile.prefab";
        private const string BACKSP_PREFAB  = "Assets/_Project/Prefabs/BackspaceButton.prefab";

        // Reference resolution (landscape)
        private static readonly Vector2 RefResolution = new(1920f, 1080f);

        [MenuItem("Tools/MathGame/Build Game Scene")]
        public static void BuildGameScene()
        {
            EnsureFolders();

            // Create fresh empty scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── EventSystem ──────────────────────────────────────────────────
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();

            // ── GameManager ──────────────────────────────────────────────────
            var gmGO = new GameObject("GameManager");
            var gsm  = gmGO.AddComponent<GameStateMachine>();
            var gm   = gmGO.AddComponent<GameManager>();

            // ── Canvas ───────────────────────────────────────────────────────
            var canvasGO = new GameObject("Canvas");
            var canvas   = canvasGO.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = RefResolution;
            scaler.matchWidthOrHeight  = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();
            var hud = canvasGO.AddComponent<GameHUD>();

            // ── NumberTile prefab ────────────────────────────────────────────
            var tilePrefabAsset = CreateNumberTilePrefab();

            // ── TopBar ───────────────────────────────────────────────────────
            var topBar = MakePanel("TopBar", canvasGO.transform,
                new Color(0.08f, 0.08f, 0.15f, 0.95f));
            AnchorTopStrip(topBar, 0f, 110f);

            var topHL = topBar.AddComponent<HorizontalLayoutGroup>();
            topHL.padding               = new RectOffset(20, 20, 8, 8);
            topHL.spacing               = 0;
            topHL.childForceExpandWidth  = true;
            topHL.childForceExpandHeight = true;

            var localCard  = CreatePlayerCard("PlayerCard_Local",  topBar.transform);
            var remoteCard = CreatePlayerCard("PlayerCard_Remote", topBar.transform);

            // ── TimerBar ─────────────────────────────────────────────────────
            var timerBarGO    = MakePanel("TimerBar", canvasGO.transform, Color.clear);
            var timerBarComp  = timerBarGO.AddComponent<TimerBar>();
            AnchorTopStrip(timerBarGO, 110f, 126f);

            var timerBG = timerBarGO.AddComponent<Image>();
            timerBG.color = new Color(0.1f, 0.1f, 0.1f, 1f);

            var timerFillGO = MakePanel("Fill", timerBarGO.transform, new Color(0.2f, 0.8f, 0.3f));
            var timerFill   = timerFillGO.AddComponent<Image>();
            timerFill.type        = Image.Type.Filled;
            timerFill.fillMethod  = Image.FillMethod.Horizontal;
            timerFill.fillOrigin  = 0;
            timerFill.fillAmount  = 1f;
            StretchFull(timerFillGO.GetComponent<RectTransform>());

            var timerLabelGO = MakeTMPText("TimeLabel", timerBarGO.transform, "01:00", 22);
            CenterAnchor(timerLabelGO.GetComponent<RectTransform>(), new Vector2(150f, 20f));

            SetPrivate(timerBarComp, "_fillImage",  timerFill);
            SetPrivate(timerBarComp, "_timeLabel",  timerLabelGO.GetComponent<TextMeshProUGUI>());

            // ── CenterArea ───────────────────────────────────────────────────
            var centerGO = MakePanel("CenterArea", canvasGO.transform, Color.clear);
            var centerRT = centerGO.GetComponent<RectTransform>();
            centerRT.anchorMin = new Vector2(0f, 0.18f);
            centerRT.anchorMax = new Vector2(1f, 1f);
            centerRT.offsetMin = new Vector2(0f,   0f);
            centerRT.offsetMax = new Vector2(0f, -126f);

            var centerVL = centerGO.AddComponent<VerticalLayoutGroup>();
            centerVL.childAlignment         = TextAnchor.MiddleCenter;
            centerVL.childForceExpandWidth  = true;
            centerVL.childForceExpandHeight = false;
            centerVL.spacing                = 24f;
            centerVL.padding                = new RectOffset(60, 60, 20, 20);

            // ── QuestionPanel ────────────────────────────────────────────────
            var qpGO   = MakePanel("QuestionPanel", centerGO.transform, new Color(0.05f, 0.05f, 0.12f, 0.8f));
            var qpComp = qpGO.AddComponent<QuestionPanel>();
            var qpLE   = qpGO.AddComponent<LayoutElement>();
            qpLE.minHeight      = 360f;
            qpLE.preferredHeight = 400f;

            var qpVL = qpGO.AddComponent<VerticalLayoutGroup>();
            qpVL.childAlignment         = TextAnchor.MiddleCenter;
            qpVL.childForceExpandWidth  = true;
            qpVL.childForceExpandHeight = false;
            qpVL.spacing                = 24f;
            qpVL.padding                = new RectOffset(30, 30, 20, 20);

            // Expression text
            var exprGO   = MakeTMPText("ExpressionText", qpGO.transform, "__ + __ = 10", 72);
            var exprLE   = exprGO.AddComponent<LayoutElement>();
            exprLE.minHeight = 90f;
            var exprText = exprGO.GetComponent<TextMeshProUGUI>();
            exprText.alignment = TextAlignmentOptions.Center;
            exprText.color     = Color.white;

            // Blank slots container
            var blanksGO = MakePanel("BlankSlots", qpGO.transform, Color.clear);
            var blanksHL = blanksGO.AddComponent<HorizontalLayoutGroup>();
            blanksHL.childAlignment          = TextAnchor.MiddleCenter;
            blanksHL.childForceExpandWidth   = false;
            blanksHL.childForceExpandHeight  = true;
            blanksHL.spacing                 = 16f;
            var blanksLE = blanksGO.AddComponent<LayoutElement>();
            blanksLE.minHeight = 90f;

            var slot0 = CreateBlankSlot("BlankSlot_0", blanksGO.transform);
            var slot1 = CreateBlankSlot("BlankSlot_1", blanksGO.transform);
            var slot2 = CreateBlankSlot("BlankSlot_2", blanksGO.transform);

            // Question counter
            var counterGO   = MakeTMPText("QuestionCounter", qpGO.transform, "Câu 1/10", 30);
            var counterLE   = counterGO.AddComponent<LayoutElement>();
            counterLE.minHeight = 40f;
            var counterText = counterGO.GetComponent<TextMeshProUGUI>();
            counterText.alignment = TextAlignmentOptions.Center;
            counterText.color     = new Color(0.7f, 0.7f, 0.7f);

            // Wire QuestionPanel private fields
            SetPrivate(qpComp, "_expressionText",      exprText);
            SetPrivate(qpComp, "_questionCounterText", counterText);
            SetPrivateArray(qpComp, "_blankSlots",
                new Object[] { slot0, slot1, slot2 });

            // ── NumberTilePool ───────────────────────────────────────────────
            var poolGO   = MakePanel("NumberTilePool", canvasGO.transform, new Color(0.06f, 0.06f, 0.13f, 0.95f));
            var poolComp = poolGO.AddComponent<NumberTilePool>();
            var poolRT   = poolGO.GetComponent<RectTransform>();
            poolRT.anchorMin = new Vector2(0f, 0f);
            poolRT.anchorMax = new Vector2(1f, 0.18f);
            poolRT.offsetMin = Vector2.zero;
            poolRT.offsetMax = Vector2.zero;

            var poolHL = poolGO.AddComponent<HorizontalLayoutGroup>();
            poolHL.childAlignment         = TextAnchor.MiddleCenter;
            poolHL.childForceExpandWidth  = false;
            poolHL.childForceExpandHeight = true;
            poolHL.spacing                = 10f;
            poolHL.padding                = new RectOffset(20, 20, 10, 10);

            // Tile container
            var tileContainerGO = MakePanel("TileContainer", poolGO.transform, Color.clear);
            var tileContainerHL = tileContainerGO.AddComponent<HorizontalLayoutGroup>();
            tileContainerHL.childAlignment         = TextAnchor.MiddleCenter;
            tileContainerHL.childForceExpandHeight = true;
            tileContainerHL.childForceExpandWidth  = false;
            tileContainerHL.spacing                = 10f;
            var tileContainerLE = tileContainerGO.AddComponent<LayoutElement>();
            tileContainerLE.flexibleWidth = 1f;

            // Backspace button
            var backspGO  = CreateStyledButton("BackspaceButton", poolGO.transform, "⌫",
                new Color(0.6f, 0.15f, 0.15f));
            var backspLE  = backspGO.AddComponent<LayoutElement>();
            backspLE.minWidth       = 100f;
            backspLE.preferredWidth = 110f;

            // Wire NumberTilePool private fields
            SetPrivate(poolComp, "_tilePrefab",       tilePrefabAsset.GetComponent<NumberTile>());
            SetPrivate(poolComp, "_tileContainer",    tileContainerGO.transform);
            SetPrivate(poolComp, "_backspaceButton",  backspGO.GetComponent<Button>());

            // ── Countdown Overlay ────────────────────────────────────────────
            var cdOverlay = MakePanel("CountdownOverlay", canvasGO.transform, new Color(0, 0, 0, 0.75f));
            cdOverlay.AddComponent<Image>().color = new Color(0, 0, 0, 0.75f);
            StretchFull(cdOverlay.GetComponent<RectTransform>());

            var cdText = MakeTMPText("CountdownText", cdOverlay.transform, "3", 200);
            CenterAnchor(cdText.GetComponent<RectTransform>(), new Vector2(300f, 250f));
            cdText.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
            cdText.GetComponent<TextMeshProUGUI>().color     = Color.white;
            cdOverlay.SetActive(false);

            // ── Result Panel ─────────────────────────────────────────────────
            var resultPanel = MakePanel("ResultPanel", canvasGO.transform,
                new Color(0.04f, 0.04f, 0.1f, 0.97f));
            resultPanel.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.1f, 0.97f);
            StretchFull(resultPanel.GetComponent<RectTransform>());

            var resultVL = resultPanel.AddComponent<VerticalLayoutGroup>();
            resultVL.childAlignment         = TextAnchor.MiddleCenter;
            resultVL.childForceExpandWidth  = true;
            resultVL.childForceExpandHeight = false;
            resultVL.spacing                = 40f;
            resultVL.padding                = new RectOffset(80, 80, 60, 60);

            var resultTitleGO = MakeTMPText("ResultTitle", resultPanel.transform, "BẠN THẮNG!", 90);
            resultTitleGO.GetComponent<TextMeshProUGUI>().color     = new Color(1f, 0.85f, 0.1f);
            resultTitleGO.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
            var titleLE = resultTitleGO.AddComponent<LayoutElement>();
            titleLE.minHeight = 110f;

            var resultScoreGO = MakeTMPText("ResultScore", resultPanel.transform, "0  −  0", 60);
            resultScoreGO.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
            var scoreLE = resultScoreGO.AddComponent<LayoutElement>();
            scoreLE.minHeight = 80f;

            var rematchBtnGO = CreateStyledButton("RematchButton", resultPanel.transform,
                "Chơi lại", new Color(0.1f, 0.5f, 0.9f));
            var rematchLE = rematchBtnGO.AddComponent<LayoutElement>();
            rematchLE.minHeight      = 80f;
            rematchLE.preferredWidth = 400f;

            resultPanel.SetActive(false);

            // ── Wire GameHUD ─────────────────────────────────────────────────
            hud.questionPanel  = qpComp;
            hud.numberTilePool = poolComp;
            hud.timerBar       = timerBarComp;
            hud.playerCards    = new[]
            {
                localCard.GetComponent<PlayerInfoCard>(),
                remoteCard.GetComponent<PlayerInfoCard>()
            };

            SetPrivate(hud, "_countdownOverlay", (Object)cdOverlay);
            SetPrivate(hud, "_countdownText",    cdText.GetComponent<TextMeshProUGUI>());
            SetPrivate(hud, "_resultPanel",      (Object)resultPanel);
            SetPrivate(hud, "_resultTitleText",  resultTitleGO.GetComponent<TextMeshProUGUI>());
            SetPrivate(hud, "_resultScoreText",  resultScoreGO.GetComponent<TextMeshProUGUI>());
            SetPrivate(hud, "_rematchButton",    rematchBtnGO.GetComponent<Button>());

            // ── Wire GameManager ─────────────────────────────────────────────
            gm.stateMachine = gsm;
            gm.hud          = hud;

            // ── Save scene ───────────────────────────────────────────────────
            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            AssetDatabase.Refresh();

            Debug.Log($"[SceneBuilder] GameScene saved → {SCENE_PATH}");
            EditorUtility.DisplayDialog("MathGame",
                $"Scene built successfully!\n\n{SCENE_PATH}\n\nPress Play to test.", "OK");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Prefab creators
        // ═══════════════════════════════════════════════════════════════════════

        private static GameObject CreateNumberTilePrefab()
        {
            var go  = new GameObject("NumberTile");
            var bg  = go.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.45f, 0.85f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;

            // Label child
            var labelGO   = MakeTMPText("Label", go.transform, "0", 44);
            var labelText = labelGO.GetComponent<TextMeshProUGUI>();
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.color     = Color.white;
            StretchFull(labelGO.GetComponent<RectTransform>());

            // Set preferred size
            var le = go.AddComponent<LayoutElement>();
            le.minWidth      = 90f;
            le.preferredWidth = 100f;
            le.minHeight     = 70f;

            var tile = go.AddComponent<NumberTile>();
            SetPrivate(tile, "_label",      labelText);
            SetPrivate(tile, "_background", bg);

            // Save prefab
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, TILE_PREFAB);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // PlayerInfoCard
        // ═══════════════════════════════════════════════════════════════════════

        private static GameObject CreatePlayerCard(string name, Transform parent)
        {
            var go = MakePanel(name, parent, Color.clear);
            var card = go.AddComponent<PlayerInfoCard>();

            var vl = go.AddComponent<VerticalLayoutGroup>();
            vl.childAlignment         = TextAnchor.MiddleCenter;
            vl.childForceExpandWidth  = true;
            vl.childForceExpandHeight = false;
            vl.spacing                = 4f;
            vl.padding                = new RectOffset(10, 10, 6, 6);

            var nameGO  = MakeTMPText("NameText",  go.transform, "Player", 26);
            var scoreGO = MakeTMPText("ScoreText", go.transform, "0",      44);
            var rankGO  = MakeTMPText("RankText",  go.transform, "Bronze", 20);

            nameGO.GetComponent<TextMeshProUGUI>().alignment  = TextAlignmentOptions.Center;
            scoreGO.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
            rankGO.GetComponent<TextMeshProUGUI>().alignment  = TextAlignmentOptions.Center;
            rankGO.GetComponent<TextMeshProUGUI>().color      = new Color(0.85f, 0.65f, 0.2f);

            var badgeGO = MakePanel("RankBadge", go.transform, new Color(0.8f, 0.5f, 0.2f));
            var badge   = badgeGO.AddComponent<Image>();
            badge.color = new Color(0.8f, 0.5f, 0.2f);
            var badgeLE = badgeGO.AddComponent<LayoutElement>();
            badgeLE.minHeight = 8f;

            SetPrivate(card, "_nameText",  nameGO.GetComponent<TextMeshProUGUI>());
            SetPrivate(card, "_scoreText", scoreGO.GetComponent<TextMeshProUGUI>());
            SetPrivate(card, "_rankText",  rankGO.GetComponent<TextMeshProUGUI>());
            SetPrivate(card, "_rankBadge", badge);

            return go;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // BlankSlot
        // ═══════════════════════════════════════════════════════════════════════

        private static BlankSlot CreateBlankSlot(string name, Transform parent)
        {
            var go  = MakePanel(name, parent, new Color(1f, 1f, 1f, 0.15f));
            var bg  = go.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.15f);

            var le = go.AddComponent<LayoutElement>();
            le.minWidth      = 110f;
            le.preferredWidth = 120f;
            le.minHeight     = 80f;

            var valGO   = MakeTMPText("ValueText", go.transform, "?", 48);
            var valText = valGO.GetComponent<TextMeshProUGUI>();
            valText.alignment = TextAlignmentOptions.Center;
            valText.color     = new Color(1f, 0.9f, 0.3f);
            StretchFull(valGO.GetComponent<RectTransform>());

            var slot = go.AddComponent<BlankSlot>();
            SetPrivate(slot, "_background",  bg);
            SetPrivate(slot, "_valueText",   valText);

            return slot;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Buttons
        // ═══════════════════════════════════════════════════════════════════════

        private static GameObject CreateStyledButton(string name, Transform parent,
            string label, Color bgColor)
        {
            var go  = MakePanel(name, parent, bgColor);
            var bg  = go.AddComponent<Image>();
            bg.color = bgColor;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;

            var lblGO   = MakeTMPText("Label", go.transform, label, 38);
            var lblText = lblGO.GetComponent<TextMeshProUGUI>();
            lblText.alignment = TextAlignmentOptions.Center;
            lblText.color     = Color.white;
            StretchFull(lblGO.GetComponent<RectTransform>());

            return go;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Layout helpers
        // ═══════════════════════════════════════════════════════════════════════

        private static GameObject MakePanel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go;
        }

        private static GameObject MakeTMPText(string name, Transform parent,
            string content, int fontSize)
        {
            var go   = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var tmp  = go.AddComponent<TextMeshProUGUI>();
            tmp.text     = content;
            tmp.fontSize = fontSize;
            tmp.color    = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            return go;
        }

        // Full top strip: from topOffset to bottomOffset (px from top)
        private static void AnchorTopStrip(GameObject go, float topPx, float bottomPx)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, -bottomPx);
            rt.offsetMax = new Vector2(0f, -topPx);
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void CenterAnchor(RectTransform rt, Vector2 size)
        {
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = size;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // SerializedObject helpers for private [SerializeField] fields
        // ═══════════════════════════════════════════════════════════════════════

        private static void SetPrivate(Component comp, string fieldName, Object value)
        {
            var so   = new SerializedObject(comp);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[SceneBuilder] Field '{fieldName}' not found on {comp.GetType().Name}");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }

        private static void SetPrivateArray(Component comp, string fieldName, Object[] values)
        {
            var so   = new SerializedObject(comp);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[SceneBuilder] Array field '{fieldName}' not found on {comp.GetType().Name}");
                return;
            }
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedProperties();
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project"))
                AssetDatabase.CreateFolder("Assets", "_Project");

            if (!AssetDatabase.IsValidFolder("Assets/_Project/Scenes"))
                AssetDatabase.CreateFolder("Assets/_Project", "Scenes");

            if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs"))
                AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");

            if (!AssetDatabase.IsValidFolder("Assets/_Project/Editor"))
                AssetDatabase.CreateFolder("Assets/_Project", "Editor");
        }
    }
}
