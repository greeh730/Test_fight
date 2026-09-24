using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Combat.UI;

namespace Combat.Editor
{
    public static class MainMenuSceneBuilder
    {
        private static Font s_Font;
        private static DefaultControls.Resources s_Resources;

        [MenuItem("Combat/Build Main Menu Scene")]
        public static void BuildMainMenuScene()
        {
            InitResources();

            // 1. Создаем новую пустую сцену
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 2. Main Camera
            var camObj = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            var cam = camObj.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 1f);
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.transform.position = new Vector3(0f, 0f, -10f);

            // 3. EventSystem
            var eventSystemObj = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            eventSystemObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            eventSystemObj.AddComponent<StandaloneInputModule>();
#endif

            // 4. Canvas
            var canvasObj = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // 5. MainMenuController
            var controller = canvasObj.AddComponent<MainMenuController>();

            // 6. UI Hierarchy
            // А. Задний фон (Background)
            var bgObj = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgObj.transform.SetParent(canvasObj.transform, false);
            var bgRt = bgObj.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;
            var bgImg = bgObj.GetComponent<Image>();
            bgImg.color = new Color(0.04f, 0.06f, 0.09f, 1f);

            // Декоративные диагональные полосы и акценты
            CreateDecorativeElements(canvasObj.transform);

            // Б. Заголовок (Title & Logo)
            CreateTitle(canvasObj.transform);

            // В. Контейнер кнопок (Buttons)
            var buttonsObj = CreateMenuButtons(canvasObj.transform, out Button startBtn, out Button controlsBtn, out Button quitBtn);

            // Г. Нижняя подсказка управления (Controls Quick Strip)
            CreateBottomControlsStrip(canvasObj.transform);

            // Д. Галочка отключения обучения в углу (Tutorial Toggle)
            var tutorialToggle = CreateTutorialToggle(canvasObj.transform);

            // Е. Модальное окно управления (Controls Modal Panel с вкладками приёмов и базы)
            var controlsPanelObj = CreateControlsModal(canvasObj.transform, 
                out Button closeControlsBtn, 
                out Button movesTabBtn, 
                out Button basicsTabBtn, 
                out GameObject movesContent, 
                out GameObject basicsContent);

            // Ж. Fade Overlay (для плавного перехода в игру)
            var fadeOverlayObj = new GameObject("FadeOverlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            fadeOverlayObj.transform.SetParent(canvasObj.transform, false);
            var fadeRt = fadeOverlayObj.GetComponent<RectTransform>();
            fadeRt.anchorMin = Vector2.zero;
            fadeRt.anchorMax = Vector2.one;
            fadeRt.sizeDelta = Vector2.zero;
            var fadeImg = fadeOverlayObj.GetComponent<Image>();
            fadeImg.color = Color.black;
            var fadeCg = fadeOverlayObj.GetComponent<CanvasGroup>();
            fadeCg.alpha = 1f;
            fadeCg.blocksRaycasts = true;

            // 7. Настраиваем связи контроллера
            var so = new SerializedObject(controller);
            so.FindProperty("gameSceneName").stringValue = "SampleScene";
            so.FindProperty("tutorialSceneName").stringValue = "TutorialScene";
            so.FindProperty("disableTutorialToggle").objectReferenceValue = tutorialToggle;
            so.FindProperty("startButton").objectReferenceValue = startBtn;
            so.FindProperty("controlsButton").objectReferenceValue = controlsBtn;
            so.FindProperty("quitButton").objectReferenceValue = quitBtn;
            so.FindProperty("controlsPanel").objectReferenceValue = controlsPanelObj;
            so.FindProperty("closeControlsButton").objectReferenceValue = closeControlsBtn;
            so.FindProperty("movesTabButton").objectReferenceValue = movesTabBtn;
            so.FindProperty("basicsTabButton").objectReferenceValue = basicsTabBtn;
            so.FindProperty("movesTabContent").objectReferenceValue = movesContent;
            so.FindProperty("basicsTabContent").objectReferenceValue = basicsContent;
            so.FindProperty("fadeOverlay").objectReferenceValue = fadeCg;
            so.ApplyModifiedProperties();

            // 8. Сохраняем сцену
            string scenePath = "Assets/Scenes/MainMenu.unity";
            EditorSceneManager.SaveScene(newScene, scenePath);

            // 9. Обновляем Build Settings (MainMenu = 0, SampleScene = 1)
            UpdateBuildSettings(scenePath);

            Debug.Log($"<color=#00FF88>[MainMenuSceneBuilder] Главное меню успешно создано и сохранено: {scenePath}!</color>");
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

        private static void CreateDecorativeElements(Transform parent)
        {
            // Акцентная неоновая горизонтальная черта
            var lineObj = new GameObject("AccentLineTop", typeof(RectTransform), typeof(Image));
            lineObj.transform.SetParent(parent, false);
            var rt = lineObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.98f);
            rt.anchorMax = new Vector2(1f, 0.985f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = lineObj.GetComponent<Image>();
            img.color = new Color(0.9f, 0.15f, 0.25f, 0.75f); // Crimson neon
        }

        private static void CreateTitle(Transform parent)
        {
            var titleRoot = new GameObject("TitleContainer", typeof(RectTransform));
            titleRoot.transform.SetParent(parent, false);

            var rt = titleRoot.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.12f, 0.65f);
            rt.anchorMax = new Vector2(0.55f, 0.88f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Название игры: VECTOR FIGHT
            var mainTitle = CreateText("MainTitle", titleRoot.transform, "VECTOR FIGHT", 62, FontStyle.Bold, new Color(0.98f, 0.18f, 0.28f, 1f));
            var mrt = mainTitle.GetComponent<RectTransform>();
            mrt.anchorMin = new Vector2(0f, 0.45f);
            mrt.anchorMax = new Vector2(1f, 1f);
            mrt.offsetMin = Vector2.zero;
            mrt.offsetMax = Vector2.zero;
            var mtxt = mainTitle.GetComponent<Text>();
            mtxt.alignment = TextAnchor.MiddleLeft;

            // Подзаголовок
            var subtitle = CreateText("Subtitle", titleRoot.transform, "// 2D TACTICAL MOTION COMBAT SYSTEM", 18, FontStyle.Bold, new Color(0.1f, 0.85f, 1f, 0.9f));
            var srt = subtitle.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0f, 0.15f);
            srt.anchorMax = new Vector2(1f, 0.45f);
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;
            var stxt = subtitle.GetComponent<Text>();
            stxt.alignment = TextAnchor.MiddleLeft;

            // Бейдж версии
            var badge = CreateText("Badge", titleRoot.transform, "PRE-ALPHA BUILD v0.9 • 8-WAY SECTOR VECTOR ENGINE", 12, FontStyle.Normal, new Color(0.45f, 0.52f, 0.65f, 0.8f));
            var brt = badge.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(1f, 0.20f);
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            var btxt = badge.GetComponent<Text>();
            btxt.alignment = TextAnchor.MiddleLeft;
        }

        private static GameObject CreateMenuButtons(Transform parent, out Button startBtn, out Button controlsBtn, out Button quitBtn)
        {
            var containerObj = new GameObject("ButtonsContainer", typeof(RectTransform));
            containerObj.transform.SetParent(parent, false);

            var crt = containerObj.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.12f, 0.28f);
            crt.anchorMax = new Vector2(0.38f, 0.62f);
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;

            float yPos = 0f;
            float btnHeight = 56f;
            float gap = 14f;

            // 1. СТАРТ (Главная кнопка)
            startBtn = CreateButton("StartButton", containerObj.transform, "▶  НАЧАТЬ ИГРУ", new Color(0.85f, 0.15f, 0.25f, 1f), ref yPos, btnHeight, gap, isPrimary: true);

            // 2. УПРАВЛЕНИЕ
            controlsBtn = CreateButton("ControlsButton", containerObj.transform, "⚔  УПРАВЛЕНИЕ", new Color(0.12f, 0.16f, 0.22f, 1f), ref yPos, btnHeight, gap, isPrimary: false);

            // 3. ВЫХОД
            quitBtn = CreateButton("QuitButton", containerObj.transform, "✕  ВЫХОД", new Color(0.12f, 0.16f, 0.22f, 1f), ref yPos, btnHeight, gap, isPrimary: false);

            return containerObj;
        }

        private static Button CreateButton(string name, Transform parent, string label, Color bgColor, ref float yPos, float height, float gap, bool isPrimary)
        {
            var btnObj = DefaultControls.CreateButton(s_Resources);
            btnObj.name = name;
            btnObj.transform.SetParent(parent, false);

            var rt = btnObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, yPos);
            rt.sizeDelta = new Vector2(0f, height);

            var img = btnObj.GetComponent<Image>();
            img.color = bgColor;

            var btn = btnObj.GetComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = bgColor;
            colors.highlightedColor = isPrimary ? new Color(1f, 0.25f, 0.35f, 1f) : new Color(0.20f, 0.26f, 0.36f, 1f);
            colors.pressedColor = isPrimary ? new Color(0.65f, 0.10f, 0.18f, 1f) : new Color(0.08f, 0.11f, 0.16f, 1f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;

            var text = btnObj.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = label;
                text.font = s_Font;
                text.fontSize = isPrimary ? 16 : 14;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.white;
            }

            yPos -= (height + gap);
            return btn;
        }

        private static void CreateBottomControlsStrip(Transform parent)
        {
            var stripObj = new GameObject("BottomControlsStrip", typeof(RectTransform));
            stripObj.transform.SetParent(parent, false);

            var rt = stripObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.12f, 0.08f);
            rt.anchorMax = new Vector2(0.66f, 0.16f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            string cheatsheet = "[ ЛКМ ] Свайпы атак    •    [ ПКМ ] Парирование    •    [ L-CTRL ] Стойка    •    [ ESC ] Пауза";
            var textObj = CreateText("CheatsheetText", stripObj.transform, cheatsheet, 12, FontStyle.Normal, new Color(0.55f, 0.62f, 0.72f, 0.85f));
            var trt = textObj.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var txt = textObj.GetComponent<Text>();
            txt.alignment = TextAnchor.MiddleLeft;
        }

        private static Toggle CreateTutorialToggle(Transform parent)
        {
            var rootObj = new GameObject("TutorialTogglePanel", typeof(RectTransform), typeof(Image));
            rootObj.transform.SetParent(parent, false);

            var rt = rootObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.68f, 0.08f);
            rt.anchorMax = new Vector2(0.92f, 0.16f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var panelImg = rootObj.GetComponent<Image>();
            panelImg.color = new Color(0.06f, 0.09f, 0.14f, 0.85f);

            var toggleObj = DefaultControls.CreateToggle(s_Resources);
            toggleObj.name = "DisableTutorialToggle";
            toggleObj.transform.SetParent(rootObj.transform, false);

            var trt = toggleObj.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.06f, 0.15f);
            trt.anchorMax = new Vector2(0.96f, 0.85f);
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            var toggle = toggleObj.GetComponent<Toggle>();
            toggle.isOn = PlayerPrefs.GetInt("DisableTutorial", 0) == 1;

            var checkmark = toggle.graphic as Image;
            if (checkmark != null)
            {
                checkmark.color = new Color(0.2f, 0.95f, 1f, 1f); // Neon Cyan
            }

            var label = toggleObj.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = "Отключить обучение";
                label.font = s_Font;
                label.fontSize = 13;
                label.fontStyle = FontStyle.Bold;
                label.color = new Color(0.88f, 0.92f, 0.98f, 1f);
                label.alignment = TextAnchor.MiddleLeft;
            }

            return toggle;
        }

        private static GameObject CreateControlsModal(
            Transform parent, 
            out Button closeBtn, 
            out Button movesTabBtn, 
            out Button basicsTabBtn, 
            out GameObject movesContent, 
            out GameObject basicsContent)
        {
            // Полупрозрачный оверлей
            var modalRoot = new GameObject("ControlsModalPanel", typeof(RectTransform), typeof(Image));
            modalRoot.transform.SetParent(parent, false);

            var mrt = modalRoot.GetComponent<RectTransform>();
            mrt.anchorMin = Vector2.zero;
            mrt.anchorMax = Vector2.one;
            mrt.sizeDelta = Vector2.zero;

            var mimg = modalRoot.GetComponent<Image>();
            mimg.color = new Color(0.02f, 0.03f, 0.05f, 0.90f);

            // Окно управления (расширенный размер под список приёмов)
            var winObj = new GameObject("ControlsWindow", typeof(RectTransform), typeof(Image));
            winObj.transform.SetParent(modalRoot.transform, false);

            var winRt = winObj.GetComponent<RectTransform>();
            winRt.anchorMin = new Vector2(0.5f, 0.5f);
            winRt.anchorMax = new Vector2(0.5f, 0.5f);
            winRt.pivot = new Vector2(0.5f, 0.5f);
            winRt.sizeDelta = new Vector2(980f, 680f);

            var winImg = winObj.GetComponent<Image>();
            winImg.sprite = s_Resources.background;
            winImg.type = Image.Type.Sliced;
            winImg.color = new Color(0.07f, 0.09f, 0.14f, 0.98f);

            // 1. Заголовок окна
            var header = CreateText("Header", winObj.transform, "//  Б О Е В О Е   Р У К О В О Д С Т В О", 20, FontStyle.Bold, new Color(0.2f, 0.95f, 1f, 1f));
            var hrt = header.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0.05f, 0.92f);
            hrt.anchorMax = new Vector2(0.95f, 0.98f);
            hrt.offsetMin = Vector2.zero;
            hrt.offsetMax = Vector2.zero;
            header.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

            // 2. Вкладки (Tabs)
            float tabY = 0f;
            movesTabBtn = CreateButton("MovesTabButton", winObj.transform, "⚔  СПИСОК ПРИЁМОВ (СВЯЗКИ)", new Color(0.85f, 0.15f, 0.25f, 1f), ref tabY, 40f, 0f, isPrimary: true);
            var mtrt = movesTabBtn.GetComponent<RectTransform>();
            mtrt.anchorMin = new Vector2(0.05f, 0.85f);
            mtrt.anchorMax = new Vector2(0.49f, 0.91f);
            mtrt.pivot = new Vector2(0.5f, 0.5f);
            mtrt.anchoredPosition = Vector2.zero;
            mtrt.sizeDelta = Vector2.zero;

            basicsTabBtn = CreateButton("BasicsTabButton", winObj.transform, "⚙  БАЗОВОЕ УПРАВЛЕНИЕ", new Color(0.12f, 0.16f, 0.22f, 1f), ref tabY, 40f, 0f, isPrimary: false);
            var btrt = basicsTabBtn.GetComponent<RectTransform>();
            btrt.anchorMin = new Vector2(0.51f, 0.85f);
            btrt.anchorMax = new Vector2(0.95f, 0.91f);
            btrt.pivot = new Vector2(0.5f, 0.5f);
            btrt.anchoredPosition = Vector2.zero;
            btrt.sizeDelta = Vector2.zero;

            // 3. Контейнер вкладки "Список приемов"
            movesContent = new GameObject("MovesTabContent", typeof(RectTransform));
            movesContent.transform.SetParent(winObj.transform, false);
            var mcRt = movesContent.GetComponent<RectTransform>();
            mcRt.anchorMin = new Vector2(0.05f, 0.11f);
            mcRt.anchorMax = new Vector2(0.95f, 0.83f);
            mcRt.offsetMin = Vector2.zero;
            mcRt.offsetMax = Vector2.zero;

            string movesInfo =
                "<color=#FF3355><b>1. ВОСХОДЯЩИЙ ВИХРЬ ⚡ (СУПЕР-ФИНИШЕР)</b></color>\n" +
                "  • <b>Вправо:</b> <color=#FFD700><b>↙ ↓ ↘ → ↗</b></color>   |   <b>Влево:</b> <color=#FFD700><b>↘ ↓ ↙ ← ↖</b></color>\n" +
                "  • <b>Как сделать:</b> Зажмите ЛКМ и плавно проведите дугу по нижнему полукругу вверх в сторону удара.\n" +
                "  • <b>Параметры:</b> Урон: <b>38</b> | Зоны: Средняя + Верхняя | Выпад: 5.5 | <color=#FFD700><b>Подбрасывает врага в воздух (Launcher)</b></color> при истощении выносливости.\n\n" +

                "<color=#FF6622><b>2. ПРОНЗАЮЩИЙ ШТОРМ 💥 (СИЛОВОЙ ВЫПАД)</b></color>\n" +
                "  • <b>Вправо:</b> <color=#FFD700><b>↘ → ↗</b></color>   |   <b>Влево:</b> <color=#FFD700><b>↙ ← ↖</b></color>\n" +
                "  • <b>Как сделать:</b> Свайп из нижнего сектора вперед и вверх.\n" +
                "  • <b>Параметры:</b> Урон: <b>32</b> | Зона: Средняя | Сила выпада: 6.8 (стремительный дальний рывок вперед).\n\n" +

                "<color=#FFAA00><b>3. ВЕРХНИЕ И ДИАГОНАЛЬНЫЕ АТАКИ</b></color>\n" +
                "  • <b>Верхний рубящий ▲:</b> <color=#FFD700><b>↓ ↑</b></color> — вертикальный свайп снизу-вверх (Урон: 26 | Зоны: Верх + Мид | Выпад: 2.8 | Сбивает прыжки)\n" +
                "  • <b>Диагональный срез ↗:</b> <color=#FFD700><b>↙ ↗</b></color> — диагональный срез снизу-слева вверх-вправо (Урон: 26 | Выпад: 3.2)\n" +
                "  • <b>Диагональный срез ↖:</b> <color=#FFD700><b>↘ ↖</b></color> — диагональный срез снизу-справа вверх-влево (Урон: 26 | Выпад: 3.2)\n\n" +

                "<color=#00E5FF><b>4. ВЫПАДЫ КЛИНКОМ (ПРОВЕРОЧНЫЕ УКОЛЫ)</b></color>\n" +
                "  • <b>Выпад клинком ▶ (вправо):</b> <color=#FFD700><b>← →</b></color> — оттяжка назад с быстрым выбросом вперед (Урон: 22 | Зона: Средняя | Выпад: 4.2)\n" +
                "  • <b>Выпад клинком ◀ (влево):</b> <color=#FFD700><b>→ ←</b></color> — оттяжка вперед с быстрым выбросом назад (Урон: 22 | Зона: Средняя | Выпад: 4.2)\n\n" +

                "<color=#33CCFF><b>5. НИЖНИЕ ПОДСЕЧКИ И СРЕЗЫ</b></color>\n" +
                "  • <b>Нижняя подсечка ▼:</b> <color=#FFD700><b>↑ ↓</b></color> — вертикальный срез сверху-вниз по ногам (Урон: 20 | Зона: Нижняя | Выпад: 3.5)\n" +
                "  • <b>Низкий срез ↘ (вправо):</b> <color=#FFD700><b>↖ ↘</b></color> — диагональный срез сверху-вниз вправо (Урон: 20 | Зона: Нижняя | Выпад: 3.5)\n" +
                "  • <b>Низкий срез ↙ (влево):</b> <color=#FFD700><b>↗ ↙</b></color> — диагональный срез сверху-вниз влево (Урон: 20 | Зона: Нижняя | Выпад: 3.5)";

            var movesTextObj = CreateText("MovesText", movesContent.transform, movesInfo, 12, FontStyle.Normal, new Color(0.90f, 0.92f, 0.96f, 0.95f));
            var mtRt = movesTextObj.GetComponent<RectTransform>();
            mtRt.anchorMin = Vector2.zero;
            mtRt.anchorMax = Vector2.one;
            mtRt.offsetMin = Vector2.zero;
            mtRt.offsetMax = Vector2.zero;
            var mtxt = movesTextObj.GetComponent<Text>();
            mtxt.alignment = TextAnchor.UpperLeft;
            mtxt.lineSpacing = 1.15f;

            // 4. Контейнер вкладки "Базовое управление"
            basicsContent = new GameObject("BasicsTabContent", typeof(RectTransform));
            basicsContent.transform.SetParent(winObj.transform, false);
            var bcRt = basicsContent.GetComponent<RectTransform>();
            bcRt.anchorMin = new Vector2(0.05f, 0.11f);
            bcRt.anchorMax = new Vector2(0.95f, 0.83f);
            bcRt.offsetMin = Vector2.zero;
            bcRt.offsetMax = Vector2.zero;

            string basicsInfo =
                "<color=#00E5FF><b>[ ЛКМ (Зажатие и вычерчивание жеста) ]</b></color>\n" +
                "Вычерчивание траектории внутри Векторного Колеса. Одиночные свайпы не атакуют вхолостую — удары наносятся строго через комбинации и моушн-связки из 8 направлений (см. вкладку «Список приёмов»).\n\n" +

                "<color=#00E5FF><b>[ ПКМ (Зажатие и выбор сектора) ]</b></color>\n" +
                "Направленное отражение и парирование атак врага. Выберите сектор направления, откуда летит вражеский удар, и отпустите ПКМ в момент контакта. Успешное парирование оглушает противника и истощает его шкалу выносливости.\n\n" +

                "<color=#00E5FF><b>[ Левый Ctrl ] — Смена боевой стойки:</b></color>\n" +
                "  • <color=#FF4444><b>БОЕВАЯ СТОЙКА (КЛИНОК):</b></color> выполнение рубящих связок, проверочных выпадов и финишеров клинком.\n" +
                "  • <color=#00E5FF><b>СТОЙКА ТАКТИКА (МАГИЯ КРИСТАЛЛА):</b></color> свайпы колеса активируют способности контроля арены (Кристалл телепорта, Барьер, Грави-воронка, Ударная волна и др.).\n\n" +

                "<color=#00E5FF><b>[ ESC ]</b></color>\n" +
                "Меню паузы: продолжить бой, быстрое сохранение, быстрая загрузка, настройки параметров и выход в главное меню.\n\n" +

                "<color=#00E5FF><b>[ R ]</b></color>\n" +
                "Мгновенный перезапуск арены при гибели персонажа.";

            var basicsTextObj = CreateText("BasicsText", basicsContent.transform, basicsInfo, 13, FontStyle.Normal, new Color(0.90f, 0.92f, 0.96f, 0.95f));
            var btRt = basicsTextObj.GetComponent<RectTransform>();
            btRt.anchorMin = Vector2.zero;
            btRt.anchorMax = Vector2.one;
            btRt.offsetMin = Vector2.zero;
            btRt.offsetMax = Vector2.zero;
            var btxt = basicsTextObj.GetComponent<Text>();
            btxt.alignment = TextAnchor.UpperLeft;
            btxt.lineSpacing = 1.25f;

            basicsContent.SetActive(false); // По умолчанию открыта вкладка приемов

            // 5. Кнопка Закрыть
            float closeY = 0f;
            closeBtn = CreateButton("CloseButton", winObj.transform, "ЗАКРЫТЬ [ ESC ]", new Color(0.85f, 0.15f, 0.25f, 1f), ref closeY, 40f, 0f, isPrimary: true);
            var crt = closeBtn.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.35f, 0.03f);
            crt.anchorMax = new Vector2(0.65f, 0.09f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = Vector2.zero;

            modalRoot.SetActive(false);
            return modalRoot;
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

        private static void UpdateBuildSettings(string mainMenuScenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>();
            scenes.Add(new EditorBuildSettingsScene(mainMenuScenePath, true));

            string tutorialScenePath = "Assets/Scenes/TutorialScene.unity";
            scenes.Add(new EditorBuildSettingsScene(tutorialScenePath, true));

            string sampleScenePath = "Assets/Scenes/SampleScene.unity";
            scenes.Add(new EditorBuildSettingsScene(sampleScenePath, true));

            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"<color=#4EE2EC>[MainMenuSceneBuilder] Build Settings обновлены: 0: {mainMenuScenePath}, 1: {tutorialScenePath}, 2: {sampleScenePath}</color>");
        }
    }
}
