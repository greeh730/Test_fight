using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Combat;
using Combat.Player;
using Combat.UI;
using Combat.Tactician;
using Combat.Tutorial;
using Combat.Common;
using Combat.Cameras;

namespace Combat.Editor
{
    public static class TutorialSceneBuilder
    {
        private static Font s_Font;
        private static DefaultControls.Resources s_Resources;

        [MenuItem("Tools/Build Scenes/Build Tutorial Scene")]
        public static void BuildTutorialScene()
        {
            InitResources();

            // 1. Создаем новую пустую 2D сцену
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 2. Спрайт и материал для геометрии
            Sprite squareSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/v2/Square.png");
            if (squareSprite == null && CombatSprites.WhiteBox != null)
            {
                squareSprite = CombatSprites.WhiteBox;
            }
            Material spriteMat = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");

            // Цветовая палитра коридора обучения
            Color groundBaseColor = new Color(0.10f, 0.13f, 0.18f, 1f);
            Color groundGlowColor = new Color(0.20f, 0.85f, 1.0f, 1f);
            Color platformBodyColor = new Color(0.16f, 0.22f, 0.30f, 1f);
            Color platformGlowColor = new Color(1.0f, 0.70f, 0.20f, 1f);
            Color wallColor = new Color(0.08f, 0.10f, 0.14f, 1f);
            Color archPillarColor = new Color(0.14f, 0.18f, 0.25f, 1f);

            // 3. Создаем окружение (Geometry Root)
            var envRoot = new GameObject("Environment");

            // А. Пол коридора (-5 .. 38м, толщина 3м)
            CreateBlock(envRoot.transform, "Ground_Base", new Vector3(16.5f, -1.5f, 0f), new Vector2(43f, 3.0f), groundBaseColor, squareSprite, spriteMat, hasCollider: true);
            CreateBlock(envRoot.transform, "Ground_Glow", new Vector3(16.5f, -0.05f, 0f), new Vector2(43f, 0.10f), groundGlowColor, squareSprite, spriteMat, hasCollider: false);

            // Б. Левая и правая стены
            CreateBlock(envRoot.transform, "Wall_Left", new Vector3(-5f, 5f, 0f), new Vector2(1.0f, 12f), wallColor, squareSprite, spriteMat, hasCollider: true);
            CreateBlock(envRoot.transform, "Wall_Left_Glow", new Vector3(-4.48f, 5f, 0f), new Vector2(0.06f, 12f), groundGlowColor, squareSprite, spriteMat, hasCollider: false);

            CreateBlock(envRoot.transform, "Wall_Right", new Vector3(38f, 5f, 0f), new Vector2(1.0f, 12f), wallColor, squareSprite, spriteMat, hasCollider: true);
            CreateBlock(envRoot.transform, "Wall_Right_Glow", new Vector3(37.48f, 5f, 0f), new Vector2(0.06f, 12f), groundGlowColor, squareSprite, spriteMat, hasCollider: false);

            // В. Препятствие для Этапа 1 (Передвижение и прыжок): ступень и платформа
            CreateBlock(envRoot.transform, "Obstacle_Step", new Vector3(6.5f, 0.45f, 0f), new Vector2(2.0f, 0.9f), platformBodyColor, squareSprite, spriteMat, hasCollider: true);
            CreateBlock(envRoot.transform, "Obstacle_Step_Glow", new Vector3(6.5f, 0.92f, 0f), new Vector2(2.0f, 0.06f), platformGlowColor, squareSprite, spriteMat, hasCollider: false);

            CreateBlock(envRoot.transform, "Obstacle_Platform", new Vector3(9.2f, 1.15f, 0f), new Vector2(3.4f, 0.5f), platformBodyColor, squareSprite, spriteMat, hasCollider: true);
            CreateBlock(envRoot.transform, "Obstacle_Platform_Glow", new Vector3(9.2f, 1.42f, 0f), new Vector2(3.4f, 0.06f), platformGlowColor, squareSprite, spriteMat, hasCollider: false);

            // Г. Декоративные арки / пилоны вдоль коридора
            float[] pillarXs = new float[] { -2f, 5f, 13f, 20f, 29f, 36f };
            foreach (float px in pillarXs)
            {
                CreateBlock(envRoot.transform, $"Arch_Pillar_{px}", new Vector3(px, 3.5f, 0f), new Vector2(0.4f, 7.0f), archPillarColor, squareSprite, spriteMat, hasCollider: false);
            }

            // 4. Освещение
            var globalLightObj = new GameObject("Global Light 2D", typeof(UnityEngine.Rendering.Universal.Light2D));
            var globalLight = globalLightObj.GetComponent<UnityEngine.Rendering.Universal.Light2D>();
            globalLight.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Global;
            globalLight.color = new Color(0.85f, 0.92f, 1.0f);
            globalLight.intensity = 0.95f;

            // 5. Библиотека связок
            var libraryObj = new GameObject("CombatSequenceLibrary", typeof(CombatSequenceLibrary));

            // 6. Игрок (Player)
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Player.prefab");
            GameObject playerObj;
            if (playerPrefab != null)
            {
                playerObj = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                playerObj.transform.position = new Vector3(0.5f, 0.55f, 0f);
            }
            else
            {
                playerObj = new GameObject("Player", typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(BoxCollider2D),
                    typeof(PlayerController2D), typeof(PlayerCombatController2D), typeof(PlayerBlockAndParry2D),
                    typeof(PlayerHealth2D), typeof(PlayerStamina2D), typeof(TacticianCombatController2D));
                playerObj.transform.position = new Vector3(0.5f, 0.55f, 0f);
            }
            playerObj.tag = "Player";

            // 7. Тренировочный манекен (CombatDummy)
            var dummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/CombatDummy.prefab");
            GameObject dummyObj = null;
            if (dummyPrefab != null)
            {
                dummyObj = (GameObject)PrefabUtility.InstantiatePrefab(dummyPrefab);
                dummyObj.transform.position = new Vector3(16.5f, 0.55f, 0f);
            }

            // 8. Тренировочный враг-скелет (Enemy_Fighter)
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Enemy_Fighter.prefab");
            GameObject enemyObj = null;
            if (enemyPrefab != null)
            {
                enemyObj = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab);
                enemyObj.transform.position = new Vector3(22.0f, 0.55f, 0f);
                enemyObj.SetActive(false);
            }

            // 9. Камера
            var camObj = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(CameraFollow2D));
            camObj.tag = "MainCamera";
            var cam = camObj.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5.2f;
            cam.backgroundColor = new Color(0.03f, 0.05f, 0.08f, 1f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            camObj.transform.position = new Vector3(0.5f, 2.0f, -10f);

            var follow = camObj.GetComponent<CameraFollow2D>();
            if (follow != null)
            {
                var followSo = new SerializedObject(follow);
                followSo.FindProperty("target").objectReferenceValue = playerObj.transform;
                followSo.ApplyModifiedProperties();
            }

            // 10. UI Canvas
            var canvasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/CombatCanvas.prefab");
            GameObject canvasObj;
            if (canvasPrefab != null)
            {
                canvasObj = (GameObject)PrefabUtility.InstantiatePrefab(canvasPrefab);
            }
            else
            {
                canvasObj = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                var c = canvasObj.GetComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            // Добавляем плавный Fade In переход
            if (canvasObj.GetComponent<ScreenFadeTransition2D>() == null)
            {
                canvasObj.AddComponent<ScreenFadeTransition2D>();
            }

            // EventSystem
            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            }

            // 11. Создаем обучающий UI (Prompt Panel, Skip Button, Completion Modal)
            CreateTutorialUIElements(canvasObj.transform, 
                out GameObject promptPanel,
                out Text stageBadgeText,
                out Text promptTitleText,
                out Text promptDescText,
                out Text promptObjectiveText,
                out Image checkmarkImg,
                out Button skipBtn,
                out GameObject completionModal,
                out Button continueToArenaBtn,
                out Button returnToMenuBtn);

            // 12. Создаем контроллер обучения [TutorialController]
            var tutorialCtrlObj = new GameObject("[TutorialController]", typeof(TutorialController2D));
            var tutorialController = tutorialCtrlObj.GetComponent<TutorialController2D>();

            var ctrlSo = new SerializedObject(tutorialController);
            ctrlSo.FindProperty("playerMovement").objectReferenceValue = playerObj.GetComponent<PlayerController2D>();
            ctrlSo.FindProperty("playerCombat").objectReferenceValue = playerObj.GetComponent<PlayerCombatController2D>();
            ctrlSo.FindProperty("playerBlockParry").objectReferenceValue = playerObj.GetComponent<PlayerBlockAndParry2D>();
            ctrlSo.FindProperty("playerTactician").objectReferenceValue = playerObj.GetComponent<TacticianCombatController2D>();
            if (dummyObj != null) ctrlSo.FindProperty("trainingDummy").objectReferenceValue = dummyObj.GetComponent<CombatDummy2D>();
            if (enemyObj != null) ctrlSo.FindProperty("tutorialEnemy").objectReferenceValue = enemyObj.GetComponent<EnemyAIController2D>();

            ctrlSo.FindProperty("promptPanel").objectReferenceValue = promptPanel;
            ctrlSo.FindProperty("stageBadgeText").objectReferenceValue = stageBadgeText;
            ctrlSo.FindProperty("promptTitleText").objectReferenceValue = promptTitleText;
            ctrlSo.FindProperty("promptDescriptionText").objectReferenceValue = promptDescText;
            ctrlSo.FindProperty("promptObjectiveText").objectReferenceValue = promptObjectiveText;
            ctrlSo.FindProperty("objectiveCheckmark").objectReferenceValue = checkmarkImg;
            ctrlSo.FindProperty("skipButton").objectReferenceValue = skipBtn;

            ctrlSo.FindProperty("completionModal").objectReferenceValue = completionModal;
            ctrlSo.FindProperty("continueToArenaButton").objectReferenceValue = continueToArenaBtn;
            ctrlSo.FindProperty("returnToMenuButton").objectReferenceValue = returnToMenuBtn;

            ctrlSo.ApplyModifiedProperties();

            // 13. Сохраняем сцену
            string scenePath = "Assets/Scenes/TutorialScene.unity";
            EditorSceneManager.SaveScene(newScene, scenePath);

            // 14. Обновляем Build Settings
            UpdateBuildSettings(scenePath);

            Debug.Log($"<color=#00FF88>[TutorialSceneBuilder] Сцена обучения успешно создана и сохранена: {scenePath}!</color>");
        }

        private static void InitResources()
        {
            s_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (s_Font == null) s_Font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            s_Resources = new DefaultControls.Resources
            {
                standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
                knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
                checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
                dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
                mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd")
            };
        }

        private static GameObject CreateBlock(Transform parent, string name, Vector3 pos, Vector2 size, Color color, Sprite sprite, Material mat, bool hasCollider, int sortingOrder = 4)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.material = mat;
            sr.color = color;
            sr.sortingOrder = sortingOrder;

            if (hasCollider)
            {
                var col = go.AddComponent<BoxCollider2D>();
                col.size = Vector2.one;
            }

            return go;
        }

        private static void CreateTutorialUIElements(
            Transform parent,
            out GameObject promptPanel,
            out Text stageBadgeText,
            out Text promptTitleText,
            out Text promptDescText,
            out Text promptObjectiveText,
            out Image checkmarkImg,
            out Button skipBtn,
            out GameObject completionModal,
            out Button continueToArenaBtn,
            out Button returnToMenuBtn)
        {
            // 1. Панель подсказки (Prompt Box) в верхнем левом углу
            promptPanel = new GameObject("TutorialPromptPanel", typeof(RectTransform), typeof(Image));
            promptPanel.transform.SetParent(parent, false);

            var prt = promptPanel.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.04f, 0.65f);
            prt.anchorMax = new Vector2(0.48f, 0.95f);
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;

            var pimg = promptPanel.GetComponent<Image>();
            pimg.color = new Color(0.05f, 0.08f, 0.12f, 0.92f);

            // А. Бейдж этапа
            var badgeObj = CreateText("StageBadge", promptPanel.transform, "ЭТАП 1 / 5  •  ОСНОВЫ ДВИЖЕНИЯ", 12, FontStyle.Bold, new Color(0.2f, 0.95f, 1f, 1f));
            var brt = badgeObj.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.04f, 0.85f);
            brt.anchorMax = new Vector2(0.96f, 0.96f);
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            stageBadgeText = badgeObj.GetComponent<Text>();
            stageBadgeText.alignment = TextAnchor.MiddleLeft;

            // Б. Заголовок подсказки
            var titleObj = CreateText("PromptTitle", promptPanel.transform, "ПЕРЕДВИЖЕНИЕ И ПРЫЖОК", 17, FontStyle.Bold, Color.white);
            var trt = titleObj.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.04f, 0.70f);
            trt.anchorMax = new Vector2(0.96f, 0.84f);
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            promptTitleText = titleObj.GetComponent<Text>();
            promptTitleText.alignment = TextAnchor.MiddleLeft;

            // В. Описание
            var descObj = CreateText("PromptDesc", promptPanel.transform, "• Клавиши [ WASD ] — перемещение влево/вправо.\n• Клавиша [ ПРОБЕЛ ] — прыжок.", 13, FontStyle.Normal, new Color(0.85f, 0.88f, 0.94f, 1f));
            var drt = descObj.GetComponent<RectTransform>();
            drt.anchorMin = new Vector2(0.04f, 0.28f);
            drt.anchorMax = new Vector2(0.96f, 0.68f);
            drt.offsetMin = Vector2.zero;
            drt.offsetMax = Vector2.zero;
            promptDescText = descObj.GetComponent<Text>();
            promptDescText.alignment = TextAnchor.UpperLeft;
            promptDescText.lineSpacing = 1.15f;

            // Г. Контейнер цели (Objective Strip)
            var objContainer = new GameObject("ObjectiveContainer", typeof(RectTransform), typeof(Image));
            objContainer.transform.SetParent(promptPanel.transform, false);
            var ocrt = objContainer.GetComponent<RectTransform>();
            ocrt.anchorMin = new Vector2(0.04f, 0.05f);
            ocrt.anchorMax = new Vector2(0.96f, 0.24f);
            ocrt.offsetMin = Vector2.zero;
            ocrt.offsetMax = Vector2.zero;
            objContainer.GetComponent<Image>().color = new Color(0.08f, 0.12f, 0.18f, 0.85f);

            var checkObj = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            checkObj.transform.SetParent(objContainer.transform, false);
            var chrt = checkObj.GetComponent<RectTransform>();
            chrt.anchorMin = new Vector2(0.02f, 0.15f);
            chrt.anchorMax = new Vector2(0.08f, 0.85f);
            chrt.offsetMin = Vector2.zero;
            chrt.offsetMax = Vector2.zero;
            checkmarkImg = checkObj.GetComponent<Image>();
            checkmarkImg.color = new Color(0.3f, 0.95f, 0.5f, 1f);

            var objTextObj = CreateText("ObjectiveText", objContainer.transform, "Пройдите вперед и перепрыгните через препятствие.", 12, FontStyle.Bold, new Color(1f, 0.92f, 0.4f, 1f));
            var otrt = objTextObj.GetComponent<RectTransform>();
            otrt.anchorMin = new Vector2(0.10f, 0f);
            otrt.anchorMax = new Vector2(0.98f, 1f);
            otrt.offsetMin = Vector2.zero;
            otrt.offsetMax = Vector2.zero;
            promptObjectiveText = objTextObj.GetComponent<Text>();
            promptObjectiveText.alignment = TextAnchor.MiddleLeft;

            // 2. Кнопка "Пропустить обучение" (Skip Button) в верхнем правом углу
            var skipBtnObj = DefaultControls.CreateButton(s_Resources);
            skipBtnObj.name = "SkipTutorialButton";
            skipBtnObj.transform.SetParent(parent, false);
            var srt = skipBtnObj.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0.78f, 0.90f);
            srt.anchorMax = new Vector2(0.96f, 0.96f);
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;
            skipBtnObj.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.22f, 0.85f);
            skipBtn = skipBtnObj.GetComponent<Button>();
            var stxt = skipBtnObj.GetComponentInChildren<Text>();
            if (stxt != null)
            {
                stxt.text = "✕  ПРОПУСТИТЬ ОБУЧЕНИЕ";
                stxt.font = s_Font;
                stxt.fontSize = 12;
                stxt.fontStyle = FontStyle.Bold;
                stxt.color = new Color(0.75f, 0.82f, 0.92f, 0.9f);
            }

            // 3. Модальное окно завершения обучения (Completion Modal)
            completionModal = new GameObject("CompletionModal", typeof(RectTransform), typeof(Image));
            completionModal.transform.SetParent(parent, false);
            var cmrt = completionModal.GetComponent<RectTransform>();
            cmrt.anchorMin = Vector2.zero;
            cmrt.anchorMax = Vector2.one;
            cmrt.offsetMin = Vector2.zero;
            cmrt.offsetMax = Vector2.zero;
            completionModal.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.05f, 0.92f);

            var winObj = new GameObject("WinCard", typeof(RectTransform), typeof(Image));
            winObj.transform.SetParent(completionModal.transform, false);
            var wrt = winObj.GetComponent<RectTransform>();
            wrt.anchorMin = new Vector2(0.5f, 0.5f);
            wrt.anchorMax = new Vector2(0.5f, 0.5f);
            wrt.pivot = new Vector2(0.5f, 0.5f);
            wrt.sizeDelta = new Vector2(680f, 440f);
            winObj.GetComponent<Image>().color = new Color(0.07f, 0.09f, 0.14f, 0.98f);

            var winTitle = CreateText("WinTitle", winObj.transform, "🏆  ОБУЧЕНИЕ ЗАВЕРШЕНО!", 24, FontStyle.Bold, new Color(0.2f, 0.95f, 1f, 1f));
            var wtrt = winTitle.GetComponent<RectTransform>();
            wtrt.anchorMin = new Vector2(0.05f, 0.82f);
            wtrt.anchorMax = new Vector2(0.95f, 0.95f);
            wtrt.offsetMin = Vector2.zero;
            wtrt.offsetMax = Vector2.zero;

            string summary = 
                "Вы успешно освоили все базовые техники Abyss of emotions:\n\n" +
                "  <color=#00FF88>✓</color> Передвижение и преодоление препятствий\n" +
                "  <color=#00FF88>✓</color> Вычерчивание атак клинком внутри векторного колеса\n" +
                "  <color=#00FF88>✓</color> Смену стойки на Тактика и применение магии арены\n" +
                "  <color=#00FF88>✓</color> Спасительный рывок уклонения (Shift) с неуязвимостью\n" +
                "  <color=#00FF88>✓</color> Направленное парирование (ПКМ) и контроль выносливости\n\n" +
                "Теперь вы готовы выйти на главную арену и сразиться с противниками!";

            var winDesc = CreateText("WinSummary", winObj.transform, summary, 14, FontStyle.Normal, new Color(0.88f, 0.92f, 0.98f, 1f));
            var wdrt = winDesc.GetComponent<RectTransform>();
            wdrt.anchorMin = new Vector2(0.08f, 0.28f);
            wdrt.anchorMax = new Vector2(0.92f, 0.80f);
            wdrt.offsetMin = Vector2.zero;
            wdrt.offsetMax = Vector2.zero;
            var wtxt = winDesc.GetComponent<Text>();
            wtxt.alignment = TextAnchor.UpperLeft;
            wtxt.lineSpacing = 1.15f;

            // Кнопка: В БОЙ (НА АРЕНУ)
            var arenaBtnObj = DefaultControls.CreateButton(s_Resources);
            arenaBtnObj.name = "ContinueToArenaButton";
            arenaBtnObj.transform.SetParent(winObj.transform, false);
            var abrt = arenaBtnObj.GetComponent<RectTransform>();
            abrt.anchorMin = new Vector2(0.10f, 0.08f);
            abrt.anchorMax = new Vector2(0.52f, 0.22f);
            abrt.offsetMin = Vector2.zero;
            abrt.offsetMax = Vector2.zero;
            arenaBtnObj.GetComponent<Image>().color = new Color(0.85f, 0.15f, 0.25f, 1f);
            continueToArenaBtn = arenaBtnObj.GetComponent<Button>();
            var atxt = arenaBtnObj.GetComponentInChildren<Text>();
            if (atxt != null)
            {
                atxt.text = "▶  В БОЙ (НА АРЕНУ)";
                atxt.font = s_Font;
                atxt.fontSize = 15;
                atxt.fontStyle = FontStyle.Bold;
                atxt.color = Color.white;
            }

            // Кнопка: В ГЛАВНОЕ МЕНЮ
            var menuBtnObj = DefaultControls.CreateButton(s_Resources);
            menuBtnObj.name = "ReturnToMenuButton";
            menuBtnObj.transform.SetParent(winObj.transform, false);
            var mbrt = menuBtnObj.GetComponent<RectTransform>();
            mbrt.anchorMin = new Vector2(0.56f, 0.08f);
            mbrt.anchorMax = new Vector2(0.90f, 0.22f);
            mbrt.offsetMin = Vector2.zero;
            mbrt.offsetMax = Vector2.zero;
            menuBtnObj.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.22f, 1f);
            returnToMenuBtn = menuBtnObj.GetComponent<Button>();
            var mtxt = menuBtnObj.GetComponentInChildren<Text>();
            if (mtxt != null)
            {
                mtxt.text = "✕  В ГЛАВНОЕ МЕНЮ";
                mtxt.font = s_Font;
                mtxt.fontSize = 14;
                mtxt.fontStyle = FontStyle.Bold;
                mtxt.color = new Color(0.85f, 0.90f, 0.98f, 1f);
            }
        }

        private static GameObject CreateText(string name, Transform parent, string content, int size, FontStyle style, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.text = content;
            text.font = s_Font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;

            return go;
        }

        private static void UpdateBuildSettings(string tutorialScenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>();
            string mainMenuPath = "Assets/Scenes/MainMenu.unity";
            scenes.Add(new EditorBuildSettingsScene(mainMenuPath, true));
            scenes.Add(new EditorBuildSettingsScene(tutorialScenePath, true));
            string sampleScenePath = "Assets/Scenes/SampleScene.unity";
            scenes.Add(new EditorBuildSettingsScene(sampleScenePath, true));

            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"<color=#4EE2EC>[TutorialSceneBuilder] Build Settings обновлены: 0: {mainMenuPath}, 1: {tutorialScenePath}, 2: {sampleScenePath}</color>");
        }
    }
}
