using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Combat.Settings;
using Combat.UI;

namespace Combat.Editor
{
    public static class SettingsUISetup
    {
        private static Font s_Font;
        private static DefaultControls.Resources s_Resources;

        [MenuItem("Combat/Setup Complete Settings UI")]
        public static void SetupUI()
        {
            InitResources();

            // 1. Убеждаемся в наличии CombatSettingsManager
            EnsureSettingsManager();

            // 2. Ищем Canvas
            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[SettingsUISetup] Canvas not found in scene!");
                return;
            }

            // Удаляем старую панель настроек и кнопку, если существовали
            var oldPanel = canvas.transform.Find("SettingsPanel");
            if (oldPanel != null) Object.DestroyImmediate(oldPanel.gameObject);

            var oldGear = canvas.transform.Find("GearButton");
            if (oldGear != null) Object.DestroyImmediate(oldGear.gameObject);

            // 3. Создаем кнопку-шестеренку [⚙]
            var gearBtnObj = CreateGearButton(canvas.transform);

            // 4. Создаем панель настроек
            var panelObj = CreateSettingsPanel(canvas.transform);

            // 5. Навешиваем CombatSettingsUI на Canvas и привязываем ссылки
            var uiComp = canvas.GetComponent<CombatSettingsUI>();
            if (uiComp == null) uiComp = canvas.gameObject.AddComponent<CombatSettingsUI>();
            WireComponentReferences(uiComp, panelObj, gearBtnObj);

            // По умолчанию скрываем панель
            panelObj.SetActive(false);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("<color=#4EE2EC>[SettingsUISetup] Complete Combat Settings UI successfully created and wired!</color>");
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

        private static void EnsureSettingsManager()
        {
            var mgr = Object.FindAnyObjectByType<CombatSettingsManager>();
            if (mgr == null)
            {
                var go = new GameObject("[CombatSettingsManager]");
                mgr = go.AddComponent<CombatSettingsManager>();
                Undo.RegisterCreatedObjectUndo(go, "Create CombatSettingsManager");
            }
        }

        private static GameObject CreateGearButton(Transform parent)
        {
            var btnObj = DefaultControls.CreateButton(s_Resources);
            btnObj.name = "GearButton";
            btnObj.transform.SetParent(parent, false);

            var rt = btnObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-25f, -25f);
            rt.sizeDelta = new Vector2(44f, 44f);

            var img = btnObj.GetComponent<Image>();
            img.color = new Color(0.08f, 0.11f, 0.16f, 0.9f);

            var text = btnObj.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = "⚙";
                text.font = s_Font;
                text.fontSize = 24;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = new Color(0.9f, 0.95f, 1f, 1f);
            }

            return btnObj;
        }

        private static GameObject CreateSettingsPanel(Transform parent)
        {
            // Корневой оверлей (затемнение)
            var panelObj = new GameObject("SettingsPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            panelObj.transform.SetParent(parent, false);

            var panelRt = panelObj.GetComponent<RectTransform>();
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;

            var panelImg = panelObj.GetComponent<Image>();
            panelImg.color = new Color(0.03f, 0.04f, 0.06f, 0.72f);

            // Центральное окно настроек
            var winObj = new GameObject("SettingsWindow", typeof(RectTransform), typeof(Image));
            winObj.transform.SetParent(panelObj.transform, false);

            var winRt = winObj.GetComponent<RectTransform>();
            winRt.anchorMin = new Vector2(0.5f, 0.5f);
            winRt.anchorMax = new Vector2(0.5f, 0.5f);
            winRt.pivot = new Vector2(0.5f, 0.5f);
            winRt.anchoredPosition = Vector2.zero;
            winRt.sizeDelta = new Vector2(1040f, 600f);

            var winImg = winObj.GetComponent<Image>();
            winImg.sprite = s_Resources.background;
            winImg.type = Image.Type.Sliced;
            winImg.color = new Color(0.08f, 0.10f, 0.14f, 0.98f);

            // Заголовок (Header)
            CreateHeader(winObj.transform);

            // Контент (3 колонки: Controls, Visuals, Audio)
            CreateBody(winObj.transform);

            // Нижняя панель с кнопками (Footer)
            CreateFooter(winObj.transform);

            return panelObj;
        }

        private static void CreateHeader(Transform parent)
        {
            var headerObj = new GameObject("Header", typeof(RectTransform), typeof(Image));
            headerObj.transform.SetParent(parent, false);

            var rt = headerObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 48f);

            var img = headerObj.GetComponent<Image>();
            img.color = new Color(0.12f, 0.15f, 0.22f, 1f);

            // Заголовок
            var titleTextObj = CreateText("TitleText", headerObj.transform, "⚙  НАСТРОЙКИ БОЕВОЙ СИСТЕМЫ И ЗВУКА", 15, FontStyle.Bold, Color.white);
            var titleRt = titleTextObj.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0f, 0.5f);
            titleRt.offsetMin = new Vector2(20f, 0f);
            titleRt.offsetMax = new Vector2(-60f, 0f);
            titleTextObj.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;

            // Кнопка закрытия [✕]
            var closeBtnObj = DefaultControls.CreateButton(s_Resources);
            closeBtnObj.name = "HeaderCloseButton";
            closeBtnObj.transform.SetParent(headerObj.transform, false);

            var closeRt = closeBtnObj.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1f, 0.5f);
            closeRt.anchorMax = new Vector2(1f, 0.5f);
            closeRt.pivot = new Vector2(1f, 0.5f);
            closeRt.anchoredPosition = new Vector2(-10f, 0f);
            closeRt.sizeDelta = new Vector2(34f, 34f);

            closeBtnObj.GetComponent<Image>().color = new Color(0.25f, 0.1f, 0.12f, 0.85f);
            var btnText = closeBtnObj.GetComponentInChildren<Text>();
            if (btnText != null)
            {
                btnText.text = "✕";
                btnText.font = s_Font;
                btnText.fontSize = 16;
                btnText.color = Color.white;
            }
        }

        private static void CreateBody(Transform parent)
        {
            var bodyObj = new GameObject("Body", typeof(RectTransform));
            bodyObj.transform.SetParent(parent, false);

            var bodyRt = bodyObj.GetComponent<RectTransform>();
            bodyRt.anchorMin = Vector2.zero;
            bodyRt.anchorMax = Vector2.one;
            bodyRt.offsetMin = new Vector2(20f, 60f);
            bodyRt.offsetMax = new Vector2(-20f, -56f);

            // Колонка 1: Колесо фехтования (0 .. 0.40)
            var colFencingObj = new GameObject("ColFencing", typeof(RectTransform));
            colFencingObj.transform.SetParent(bodyObj.transform, false);
            var fencingRt = colFencingObj.GetComponent<RectTransform>();
            fencingRt.anchorMin = new Vector2(0f, 0f);
            fencingRt.anchorMax = new Vector2(0.40f, 1f);
            fencingRt.offsetMin = Vector2.zero;
            fencingRt.offsetMax = Vector2.zero;

            PopulateFencingWheelColumn(colFencingObj.transform);

            // Разделитель 1
            CreateDivider("Divider1", bodyObj.transform, 0.41f);

            // Колонка 2: Аудио (0.42 .. 0.70)
            var colAudioObj = new GameObject("ColAudio", typeof(RectTransform));
            colAudioObj.transform.SetParent(bodyObj.transform, false);
            var audioRt = colAudioObj.GetComponent<RectTransform>();
            audioRt.anchorMin = new Vector2(0.42f, 0f);
            audioRt.anchorMax = new Vector2(0.70f, 1f);
            audioRt.offsetMin = Vector2.zero;
            audioRt.offsetMax = Vector2.zero;

            PopulateAudioColumn(colAudioObj.transform);

            // Разделитель 2
            CreateDivider("Divider2", bodyObj.transform, 0.71f);

            // Колонка 3: Визуал и хитбоксы (0.72 .. 1.0)
            var colCombatObj = new GameObject("ColCombat", typeof(RectTransform));
            colCombatObj.transform.SetParent(bodyObj.transform, false);
            var combatRt = colCombatObj.GetComponent<RectTransform>();
            combatRt.anchorMin = new Vector2(0.72f, 0f);
            combatRt.anchorMax = new Vector2(1f, 1f);
            combatRt.offsetMin = Vector2.zero;
            combatRt.offsetMax = Vector2.zero;

            PopulateCombatColumn(colCombatObj.transform);
        }

        private static void CreateDivider(string name, Transform parent, float anchorX)
        {
            var divObj = new GameObject(name, typeof(RectTransform), typeof(Image));
            divObj.transform.SetParent(parent, false);
            var rt = divObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(anchorX, 0.03f);
            rt.anchorMax = new Vector2(anchorX, 0.97f);
            rt.sizeDelta = new Vector2(1f, 0f);
            divObj.GetComponent<Image>().color = new Color(0.2f, 0.25f, 0.35f, 0.5f);
        }

        // ─────────────────────────────────────────────────────────────
        //  КОЛОНКА: КОЛЕСО ФЕХТОВАНИЯ
        // ─────────────────────────────────────────────────────────────
        private static void PopulateFencingWheelColumn(Transform parent)
        {
            float yPos = -5f;
            const float sliderGap = 40f;
            const float toggleGap = 30f;

            // Заголовок секции
            var secTitle = CreateText("SecTitleFencing", parent,
                "— КОЛЕСО ФЕХТОВАНИЯ —", 13, FontStyle.Bold,
                new Color(1f, 0.75f, 0.15f, 1f));
            PositionItem(secTitle.GetComponent<RectTransform>(), yPos, 22f);
            yPos -= 26f;

            // ── Отображать колесо в UI (Toggle) ──────────────────────
            CreateToggleItem("ShowWheel", parent, "Отображать колесо в UI", false, ref yPos, toggleGap);

            // ── Расположение (две кнопки рядом) ───────────────────────
            var placementGroupObj = new GameObject("PlacementGroup", typeof(RectTransform));
            placementGroupObj.transform.SetParent(parent, false);
            PositionItem(placementGroupObj.GetComponent<RectTransform>(), yPos, 42f);
            yPos -= 46f;

            // Лейбл над кнопками
            var plcLabel = CreateText("Label", placementGroupObj.transform,
                "Расположение", 11, FontStyle.Normal, new Color(0.82f, 0.86f, 0.92f, 1f));
            var plcLrt = plcLabel.GetComponent<RectTransform>();
            plcLrt.anchorMin = new Vector2(0f, 0.7f);
            plcLrt.anchorMax = new Vector2(1f, 1f);
            plcLrt.offsetMin = Vector2.zero;
            plcLrt.offsetMax = Vector2.zero;
            plcLabel.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;

            // Кнопка "Слева внизу"
            var btnLeft = DefaultControls.CreateButton(s_Resources);
            btnLeft.name = "PlacementLeftButton";
            btnLeft.transform.SetParent(placementGroupObj.transform, false);
            var blRt = btnLeft.GetComponent<RectTransform>();
            blRt.anchorMin = new Vector2(0f, 0f);
            blRt.anchorMax = new Vector2(0.48f, 0.65f);
            blRt.offsetMin = Vector2.zero;
            blRt.offsetMax = Vector2.zero;
            var blImg = btnLeft.GetComponent<Image>();
            blImg.color = new Color(0.12f, 0.45f, 0.85f, 1f); // активный по умолчанию
            var blText = btnLeft.GetComponentInChildren<Text>();
            if (blText != null) { blText.text = "◄ Слева внизу"; blText.font = s_Font; blText.fontSize = 9; blText.color = Color.white; }

            // Кнопка "Справа внизу"
            var btnRight = DefaultControls.CreateButton(s_Resources);
            btnRight.name = "PlacementRightButton";
            btnRight.transform.SetParent(placementGroupObj.transform, false);
            var brRt = btnRight.GetComponent<RectTransform>();
            brRt.anchorMin = new Vector2(0.52f, 0f);
            brRt.anchorMax = new Vector2(1f, 0.65f);
            brRt.offsetMin = Vector2.zero;
            brRt.offsetMax = Vector2.zero;
            var brImg = btnRight.GetComponent<Image>();
            brImg.color = new Color(0.14f, 0.18f, 0.25f, 0.9f);
            var brText = btnRight.GetComponentInChildren<Text>();
            if (brText != null) { brText.text = "Справа внизу ►"; brText.font = s_Font; brText.fontSize = 9; brText.color = Color.white; }

            // ── Размер колеса ─────────────────────────────────────────
            CreateSliderItem("WheelSize", parent,
                "Размер колеса",
                50f, 150f, 90f, "90 px",
                ref yPos, sliderGap);

            // ── Шарик: вес и ускорение ───────────────────────────────
            var orbHeader = CreateText("OrbSectionHeader", parent,
                "— Шарик 🟡 —", 11, FontStyle.Italic, new Color(1f, 0.9f, 0.3f, 0.8f));
            PositionItem(orbHeader.GetComponent<RectTransform>(), yPos, 16f);
            yPos -= 18f;

            CreateSliderItem("OrbWeight", parent,
                "Вес шарика",
                0.1f, 5.0f, 1.0f, "1.00",
                ref yPos, sliderGap);

            CreateSliderItem("OrbSpeed", parent,
                "Ускорение шарика",
                2.0f, 24.0f, 8.0f, "8.0",
                ref yPos, sliderGap,
                fillColor: new Color(1f, 0.75f, 0.15f, 1f));

            // ── Свободная зона ────────────────────────────────────────
            var fzHeader = CreateText("FreeZoneSectionHeader", parent,
                "— Свободная зона 🟢 —", 11, FontStyle.Italic, new Color(0.3f, 1f, 0.55f, 0.8f));
            PositionItem(fzHeader.GetComponent<RectTransform>(), yPos, 16f);
            yPos -= 18f;

            CreateSliderItem("FreeZoneRadius", parent,
                "Размер зоны",
                0.2f, 1.5f, 0.8f, "0.80",
                ref yPos, sliderGap);

            // ── Лепестки атак ─────────────────────────────────────────
            var attHeader = CreateText("AttackSectionHeader", parent,
                "— Лепестки атак 🔵 —", 11, FontStyle.Italic, new Color(0.35f, 0.65f, 1f, 0.8f));
            PositionItem(attHeader.GetComponent<RectTransform>(), yPos, 16f);
            yPos -= 18f;

            CreateSliderItem("AttackAngle", parent,
                "Угол всех лепестков",
                15f, 60f, 50f, "50°",
                ref yPos, sliderGap,
                fillColor: new Color(0.2f, 0.55f, 0.95f, 1f));

            // ── Мёртвая зона ─────────────────────────────────────────
            var dzHeader = CreateText("DeadZoneSectionHeader", parent,
                "— Мёртвая зона 🔴 —", 11, FontStyle.Italic, new Color(1f, 0.3f, 0.3f, 0.8f));
            PositionItem(dzHeader.GetComponent<RectTransform>(), yPos, 16f);
            yPos -= 18f;

            CreateToggleItem("EnableDeadZone", parent, "Включить мёртвые зоны", true, ref yPos, toggleGap);

            CreateSliderItem("DeadZoneAngle", parent,
                "Размер мёртвой зоны",
                0f, 90f, 30f, "30°",
                ref yPos, sliderGap,
                fillColor: new Color(0.85f, 0.15f, 0.2f, 1f));
        }

        // ─────────────────────────────────────────────────────────────
        //  КОЛОНКА: ШКАЛА ФОКУСА И ВИЗУАЛ
        // ─────────────────────────────────────────────────────────────
        private static void PopulateCombatColumn(Transform parent)
        {
            float yPos = -5f;
            const float sliderGap = 40f;
            const float toggleGap = 34f;

            // Заголовок секции: Шкала фокуса
            var secTitle = CreateText("SecTitleFocus", parent,
                "— ШКАЛА ФОКУСА —", 13, FontStyle.Bold,
                new Color(0.2f, 0.75f, 1f, 1f));
            PositionItem(secTitle.GetComponent<RectTransform>(), yPos, 22f);
            yPos -= 28f;

            // 1. Размер шкалы фокуса (масштаб 0.5x - 2.5x)
            CreateSliderItem("FocusBarScale", parent, "Размер шкалы", 0.5f, 2.5f, 1.0f, "1.00x", ref yPos, sliderGap, new Color(0.2f, 0.65f, 1f));

            // 2. Позиция X (пиксели от левого края 0 - 600 px)
            CreateSliderItem("FocusBarPosX", parent, "Положение X (от края)", 0f, 600f, 25f, "25 px", ref yPos, sliderGap, new Color(0.2f, 0.65f, 1f));

            // 3. Позиция Y (пиксели от верхнего края 0 - 400 px)
            CreateSliderItem("FocusBarPosY", parent, "Положение Y (сверху)", 0f, 400f, 25f, "25 px", ref yPos, sliderGap, new Color(0.2f, 0.65f, 1f));

            yPos -= 8f;

            // Секция: Стрелки и строка действий над колесом
            var secRuneBar = CreateText("SecTitleRuneBar", parent,
                "— СТРЕЛКИ И СТРОКА МАГИИ —", 12, FontStyle.Bold,
                new Color(0.78f, 0.55f, 1f, 1f));
            PositionItem(secRuneBar.GetComponent<RectTransform>(), yPos, 20f);
            yPos -= 24f;

            CreateSliderItem("ArrowIconSize", parent, "Размер стрелок", 14f, 64f, 48f, "48 px", ref yPos, sliderGap, new Color(0.68f, 0.45f, 0.95f));
            CreateSliderItem("RuneBarWidth", parent, "Ширина строки", 80f, 300f, 240f, "240 px", ref yPos, sliderGap, new Color(0.68f, 0.45f, 0.95f));
            CreateSliderItem("RuneBarHeight", parent, "Высота строки", 16f, 64f, 48f, "48 px", ref yPos, sliderGap, new Color(0.68f, 0.45f, 0.95f));
            CreateSliderItem("RuneBarOffsetY", parent, "Отступ над колесом (Y)", 0f, 80f, 26f, "26 px", ref yPos, sliderGap, new Color(0.68f, 0.45f, 0.95f));
            CreateSliderItem("RuneBarOffsetX", parent, "Смещение по X", -100f, 100f, 0f, "0 px", ref yPos, sliderGap, new Color(0.68f, 0.45f, 0.95f));
            CreateToggleItem("ShowRuneBarOnlyInMagic", parent, "Только в стойке магии", true, ref yPos, toggleGap);

            yPos -= 6f;

            // Секция: Бой и хитбоксы
            var secCombat = CreateText("SecTitleCombat", parent,
                "— ВИЗУАЛ И БОЙ —", 12, FontStyle.Bold,
                new Color(0.85f, 0.88f, 0.95f, 1f));
            PositionItem(secCombat.GetComponent<RectTransform>(), yPos, 20f);
            yPos -= 26f;

            // Показывать хитбоксы
            CreateToggleItem("ShowHitboxes", parent,
                "Показывать хитбоксы", true, ref yPos, toggleGap);

            // Подсказка
            var infoText = CreateText("HitboxInfoText", parent,
                "Видимость коллайдеров\nдля отладки.", 9, FontStyle.Italic,
                new Color(0.55f, 0.62f, 0.72f, 1f));
            var irt = infoText.GetComponent<RectTransform>();
            PositionItem(irt, yPos, 32f);
            infoText.GetComponent<Text>().alignment = TextAnchor.UpperLeft;
        }

        private static void PopulateAudioColumn(Transform parent)
        {
            float yPos = -5f;
            const float itemGap = 42f;

            // Заголовок секции
            var secTitle = CreateText("SecTitleAudio", parent, "— ЗВУК И ГРОМКОСТЬ —", 13, FontStyle.Bold, new Color(0.2f, 0.9f, 0.5f, 1f));
            PositionItem(secTitle.GetComponent<RectTransform>(), yPos, 22f);
            yPos -= 30f;

            // 1. Общая громкость (Master: 0.0 - 1.0)
            CreateSliderItem("MasterVolume", parent, "Общая громкость", 0.0f, 1.0f, 1.0f, "100%", ref yPos, itemGap);

            // 2. Звуки боя и магии (SFX: 0.0 - 1.0)
            CreateSliderItem("SfxVolume", parent, "Звуки боя и магии", 0.0f, 1.0f, 1.0f, "100%", ref yPos, itemGap);

            // 3. Звуки шагов (Footsteps: 0.0 - 1.0)
            CreateSliderItem("FootstepsVolume", parent, "Шаги и перемещение", 0.0f, 1.0f, 0.85f, "85%", ref yPos, itemGap);

            // 4. Отключить звук (Mute toggle)
            CreateToggleItem("MuteAudio", parent, "Отключить весь звук (Mute)", false, ref yPos, 36f);

            // 5. Кнопка "Проверить звук"
            var testBtnObj = DefaultControls.CreateButton(s_Resources);
            testBtnObj.name = "TestAudioButton";
            testBtnObj.transform.SetParent(parent, false);
            PositionItem(testBtnObj.GetComponent<RectTransform>(), yPos, 34f);
            yPos -= itemGap;

            testBtnObj.GetComponent<Image>().color = new Color(0.16f, 0.28f, 0.38f, 0.95f);
            var testBtnText = testBtnObj.GetComponentInChildren<Text>();
            if (testBtnText != null)
            {
                testBtnText.text = "🔊 Проверить звук";
                testBtnText.font = s_Font;
                testBtnText.fontSize = 11;
                testBtnText.fontStyle = FontStyle.Bold;
                testBtnText.color = new Color(0.9f, 0.98f, 1f, 1f);
            }
        }

        private static void CreateSliderItem(string name, Transform parent, string labelText, float min, float max, float defVal, string defDisplay, ref float yPos, float gap, Color? fillColor = null)
        {
            var rootObj = new GameObject(name + "Group", typeof(RectTransform));
            rootObj.transform.SetParent(parent, false);
            PositionItem(rootObj.GetComponent<RectTransform>(), yPos, 38f);
            yPos -= gap;

            // Лейбл слева
            var labelObj = CreateText("Label", rootObj.transform, labelText, 11, FontStyle.Normal, new Color(0.82f, 0.86f, 0.92f, 1f));
            var lrt = labelObj.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0f, 0.5f);
            lrt.anchorMax = new Vector2(0.5f, 1f);
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            labelObj.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;

            // Значение справа
            var valObj = CreateText(name + "ValueText", rootObj.transform, defDisplay, 11, FontStyle.Bold, Color.white);
            var vrt = valObj.GetComponent<RectTransform>();
            vrt.anchorMin = new Vector2(0.5f, 0.5f);
            vrt.anchorMax = new Vector2(1f, 1f);
            vrt.offsetMin = Vector2.zero;
            vrt.offsetMax = Vector2.zero;
            valObj.GetComponent<Text>().alignment = TextAnchor.MiddleRight;

            // Сам слайдер внизу
            var sliderObj = DefaultControls.CreateSlider(s_Resources);
            sliderObj.name = name + "Slider";
            sliderObj.transform.SetParent(rootObj.transform, false);

            var srt = sliderObj.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0f, 0f);
            srt.anchorMax = new Vector2(1f, 0.45f);
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;

            var slider = sliderObj.GetComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = defVal;

            // Стилизация цветов слайдера
            var fillImg = slider.fillRect.GetComponent<Image>();
            if (fillImg != null) fillImg.color = fillColor ?? new Color(0.95f, 0.15f, 0.25f, 1f);

            var handleImg = slider.handleRect.GetComponent<Image>();
            if (handleImg != null) handleImg.color = Color.white;
        }

        private static void CreateDropdownItem(string name, Transform parent, string labelText, List<string> options, ref float yPos, float gap)
        {
            var rootObj = new GameObject(name + "Group", typeof(RectTransform));
            rootObj.transform.SetParent(parent, false);
            PositionItem(rootObj.GetComponent<RectTransform>(), yPos, 44f);
            yPos -= gap;

            // Лейбл вверху
            var labelObj = CreateText("Label", rootObj.transform, labelText, 11, FontStyle.Normal, new Color(0.82f, 0.86f, 0.92f, 1f));
            var lrt = labelObj.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0f, 0.6f);
            lrt.anchorMax = new Vector2(1f, 1f);
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            labelObj.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;

            // Dropdown внизу
            var ddObj = DefaultControls.CreateDropdown(s_Resources);
            ddObj.name = name + "Dropdown";
            ddObj.transform.SetParent(rootObj.transform, false);

            var ddRt = ddObj.GetComponent<RectTransform>();
            ddRt.anchorMin = new Vector2(0f, 0f);
            ddRt.anchorMax = new Vector2(1f, 0.58f);
            ddRt.offsetMin = Vector2.zero;
            ddRt.offsetMax = Vector2.zero;

            var dd = ddObj.GetComponent<Dropdown>();
            dd.ClearOptions();
            dd.AddOptions(options);

            var ddImg = ddObj.GetComponent<Image>();
            if (ddImg != null) ddImg.color = new Color(0.12f, 0.15f, 0.22f, 1f);

            var capText = ddObj.GetComponentInChildren<Text>();
            if (capText != null)
            {
                capText.font = s_Font;
                capText.fontSize = 11;
                capText.color = Color.white;
            }
        }

        private static void CreateToggleItem(string name, Transform parent, string labelText, bool defIsOn, ref float yPos, float gap)
        {
            var toggleObj = DefaultControls.CreateToggle(s_Resources);
            toggleObj.name = name + "Toggle";
            toggleObj.transform.SetParent(parent, false);
            PositionItem(toggleObj.GetComponent<RectTransform>(), yPos, 24f);
            yPos -= gap;

            var toggle = toggleObj.GetComponent<Toggle>();
            toggle.isOn = defIsOn;

            var label = toggleObj.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = labelText;
                label.font = s_Font;
                label.fontSize = 11;
                label.color = new Color(0.85f, 0.89f, 0.95f, 1f);
            }

            var checkImg = toggle.graphic as Image;
            if (checkImg != null)
            {
                checkImg.color = new Color(0.95f, 0.2f, 0.3f, 1f);
            }
        }

        private static void CreateFooter(Transform parent)
        {
            var footerObj = new GameObject("Footer", typeof(RectTransform), typeof(Image));
            footerObj.transform.SetParent(parent, false);

            var rt = footerObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 52f);

            var img = footerObj.GetComponent<Image>();
            img.color = new Color(0.09f, 0.12f, 0.17f, 1f);

            // Кнопка "Сбросить по умолчанию"
            var resetBtnObj = DefaultControls.CreateButton(s_Resources);
            resetBtnObj.name = "ResetButton";
            resetBtnObj.transform.SetParent(footerObj.transform, false);

            var rrt = resetBtnObj.GetComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0f, 0.5f);
            rrt.anchorMax = new Vector2(0f, 0.5f);
            rrt.pivot = new Vector2(0f, 0.5f);
            rrt.anchoredPosition = new Vector2(25f, 0f);
            rrt.sizeDelta = new Vector2(190f, 34f);

            resetBtnObj.GetComponent<Image>().color = new Color(0.18f, 0.22f, 0.3f, 1f);
            var rText = resetBtnObj.GetComponentInChildren<Text>();
            if (rText != null)
            {
                rText.text = "Сброс по умолчанию";
                rText.font = s_Font;
                rText.fontSize = 11;
                rText.color = new Color(0.75f, 0.8f, 0.88f, 1f);
            }

            // Подсказка F1 / ESC
            var hintTextObj = CreateText("HintText", footerObj.transform, "[ F1 / ESC — свернуть ]", 10, FontStyle.Italic, new Color(0.45f, 0.52f, 0.62f, 1f));
            var hrt = hintTextObj.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0.5f, 0.5f);
            hrt.anchorMax = new Vector2(0.5f, 0.5f);
            hrt.pivot = new Vector2(0.5f, 0.5f);
            hrt.anchoredPosition = Vector2.zero;
            hrt.sizeDelta = new Vector2(180f, 30f);

            // Кнопка "Применить и закрыть"
            var closeBtnObj = DefaultControls.CreateButton(s_Resources);
            closeBtnObj.name = "FooterCloseButton";
            closeBtnObj.transform.SetParent(footerObj.transform, false);

            var crt = closeBtnObj.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(1f, 0.5f);
            crt.anchorMax = new Vector2(1f, 0.5f);
            crt.pivot = new Vector2(1f, 0.5f);
            crt.anchoredPosition = new Vector2(-25f, 0f);
            crt.sizeDelta = new Vector2(160f, 34f);

            closeBtnObj.GetComponent<Image>().color = new Color(0.85f, 0.15f, 0.25f, 1f);
            var cText = closeBtnObj.GetComponentInChildren<Text>();
            if (cText != null)
            {
                cText.text = "Сохранить и закрыть";
                cText.font = s_Font;
                cText.fontSize = 11;
                cText.fontStyle = FontStyle.Bold;
                cText.color = Color.white;
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

        private static void PositionItem(RectTransform rt, float yPos, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, yPos);
            rt.sizeDelta = new Vector2(0f, height);
        }

        private static void WireComponentReferences(CombatSettingsUI ui, GameObject panelObj, GameObject gearBtnObj)
        {
            var so = new SerializedObject(ui);

            // 1. Windows & Hotkeys
            so.FindProperty("settingsPanel").objectReferenceValue = panelObj;
            so.FindProperty("panelCanvasGroup").objectReferenceValue = panelObj.GetComponent<CanvasGroup>();
            so.FindProperty("gearOpenButton").objectReferenceValue = gearBtnObj.GetComponent<Button>();

            var headerCloseBtn = panelObj.transform.Find("SettingsWindow/Header/HeaderCloseButton");
            if (headerCloseBtn != null)
                so.FindProperty("closeButton").objectReferenceValue = headerCloseBtn.GetComponent<Button>();

            var footerCloseBtn = panelObj.transform.Find("SettingsWindow/Footer/FooterCloseButton");
            if (footerCloseBtn != null)
                so.FindProperty("footerCloseButton").objectReferenceValue = footerCloseBtn.GetComponent<Button>();

            var resetBtn = panelObj.transform.Find("SettingsWindow/Footer/ResetButton");
            if (resetBtn != null)
                so.FindProperty("resetButton").objectReferenceValue = resetBtn.GetComponent<Button>();

            // 2. Fencing Wheel UI
            var fencingCol = panelObj.transform.Find("SettingsWindow/Body/ColFencing");
            if (fencingCol != null)
            {
                var swToggle = fencingCol.Find("ShowWheelToggle");
                if (swToggle != null) so.FindProperty("showWheelToggle").objectReferenceValue = swToggle.GetComponent<Toggle>();

                var plcLeftBtn = fencingCol.Find("PlacementGroup/PlacementLeftButton");
                if (plcLeftBtn != null)
                {
                    so.FindProperty("placementLeftButton").objectReferenceValue = plcLeftBtn.GetComponent<Button>();
                    so.FindProperty("placementLeftBg").objectReferenceValue = plcLeftBtn.GetComponent<Image>();
                }

                var plcRightBtn = fencingCol.Find("PlacementGroup/PlacementRightButton");
                if (plcRightBtn != null)
                {
                    so.FindProperty("placementRightButton").objectReferenceValue = plcRightBtn.GetComponent<Button>();
                    so.FindProperty("placementRightBg").objectReferenceValue = plcRightBtn.GetComponent<Image>();
                }

                var wsSlider = fencingCol.Find("WheelSizeGroup/WheelSizeSlider");
                if (wsSlider != null) so.FindProperty("wheelSizeSlider").objectReferenceValue = wsSlider.GetComponent<Slider>();
                var wsVal = fencingCol.Find("WheelSizeGroup/WheelSizeValueText");
                if (wsVal != null) so.FindProperty("wheelSizeValueText").objectReferenceValue = wsVal.GetComponent<Text>();

                var owSlider = fencingCol.Find("OrbWeightGroup/OrbWeightSlider");
                if (owSlider != null) so.FindProperty("orbWeightSlider").objectReferenceValue = owSlider.GetComponent<Slider>();
                var owVal = fencingCol.Find("OrbWeightGroup/OrbWeightValueText");
                if (owVal != null) so.FindProperty("orbWeightValueText").objectReferenceValue = owVal.GetComponent<Text>();

                var osSlider = fencingCol.Find("OrbSpeedGroup/OrbSpeedSlider");
                if (osSlider != null) so.FindProperty("orbSpeedSlider").objectReferenceValue = osSlider.GetComponent<Slider>();
                var osVal = fencingCol.Find("OrbSpeedGroup/OrbSpeedValueText");
                if (osVal != null) so.FindProperty("orbSpeedValueText").objectReferenceValue = osVal.GetComponent<Text>();

                var fzSlider = fencingCol.Find("FreeZoneRadiusGroup/FreeZoneRadiusSlider");
                if (fzSlider != null) so.FindProperty("freeZoneRadiusSlider").objectReferenceValue = fzSlider.GetComponent<Slider>();
                var fzVal = fencingCol.Find("FreeZoneRadiusGroup/FreeZoneRadiusValueText");
                if (fzVal != null) so.FindProperty("freeZoneRadiusValueText").objectReferenceValue = fzVal.GetComponent<Text>();

                var aaSlider = fencingCol.Find("AttackAngleGroup/AttackAngleSlider");
                if (aaSlider != null) so.FindProperty("attackAngleSlider").objectReferenceValue = aaSlider.GetComponent<Slider>();
                var aaVal = fencingCol.Find("AttackAngleGroup/AttackAngleValueText");
                if (aaVal != null) so.FindProperty("attackAngleValueText").objectReferenceValue = aaVal.GetComponent<Text>();

                var edzToggle = fencingCol.Find("EnableDeadZoneToggle");
                if (edzToggle != null) so.FindProperty("enableDeadZoneToggle").objectReferenceValue = edzToggle.GetComponent<Toggle>();

                var dzaSlider = fencingCol.Find("DeadZoneAngleGroup/DeadZoneAngleSlider");
                if (dzaSlider != null) so.FindProperty("deadZoneAngleSlider").objectReferenceValue = dzaSlider.GetComponent<Slider>();
                var dzaVal = fencingCol.Find("DeadZoneAngleGroup/DeadZoneAngleValueText");
                if (dzaVal != null) so.FindProperty("deadZoneAngleValueText").objectReferenceValue = dzaVal.GetComponent<Text>();
            }

            // 3. Audio & Volume UI
            var audioCol = panelObj.transform.Find("SettingsWindow/Body/ColAudio");
            if (audioCol != null)
            {
                var masterSlider = audioCol.Find("MasterVolumeGroup/MasterVolumeSlider");
                if (masterSlider != null) so.FindProperty("masterVolumeSlider").objectReferenceValue = masterSlider.GetComponent<Slider>();
                var masterVal = audioCol.Find("MasterVolumeGroup/MasterVolumeValueText");
                if (masterVal != null) so.FindProperty("masterVolumeValueText").objectReferenceValue = masterVal.GetComponent<Text>();

                var sfxSlider = audioCol.Find("SfxVolumeGroup/SfxVolumeSlider");
                if (sfxSlider != null) so.FindProperty("sfxVolumeSlider").objectReferenceValue = sfxSlider.GetComponent<Slider>();
                var sfxVal = audioCol.Find("SfxVolumeGroup/SfxVolumeValueText");
                if (sfxVal != null) so.FindProperty("sfxVolumeValueText").objectReferenceValue = sfxVal.GetComponent<Text>();

                var ftSlider = audioCol.Find("FootstepsVolumeGroup/FootstepsVolumeSlider");
                if (ftSlider != null) so.FindProperty("footstepsVolumeSlider").objectReferenceValue = ftSlider.GetComponent<Slider>();
                var ftVal = audioCol.Find("FootstepsVolumeGroup/FootstepsVolumeValueText");
                if (ftVal != null) so.FindProperty("footstepsVolumeValueText").objectReferenceValue = ftVal.GetComponent<Text>();

                var muteToggle = audioCol.Find("MuteAudioToggle");
                if (muteToggle != null) so.FindProperty("muteAudioToggle").objectReferenceValue = muteToggle.GetComponent<Toggle>();

                var testBtn = audioCol.Find("TestAudioButton");
                if (testBtn != null) so.FindProperty("testAudioButton").objectReferenceValue = testBtn.GetComponent<Button>();
            }

            // 4. Focus Bar & Combat UI
            var combatCol = panelObj.transform.Find("SettingsWindow/Body/ColCombat");
            if (combatCol != null)
            {
                var fScaleSlider = combatCol.Find("FocusBarScaleGroup/FocusBarScaleSlider");
                if (fScaleSlider != null) so.FindProperty("focusBarScaleSlider").objectReferenceValue = fScaleSlider.GetComponent<Slider>();
                var fScaleVal = combatCol.Find("FocusBarScaleGroup/FocusBarScaleValueText");
                if (fScaleVal != null) so.FindProperty("focusBarScaleValueText").objectReferenceValue = fScaleVal.GetComponent<Text>();

                var fPosXSlider = combatCol.Find("FocusBarPosXGroup/FocusBarPosXSlider");
                if (fPosXSlider != null) so.FindProperty("focusBarPosXSlider").objectReferenceValue = fPosXSlider.GetComponent<Slider>();
                var fPosXVal = combatCol.Find("FocusBarPosXGroup/FocusBarPosXValueText");
                if (fPosXVal != null) so.FindProperty("focusBarPosXValueText").objectReferenceValue = fPosXVal.GetComponent<Text>();

                var fPosYSlider = combatCol.Find("FocusBarPosYGroup/FocusBarPosYSlider");
                if (fPosYSlider != null) so.FindProperty("focusBarPosYSlider").objectReferenceValue = fPosYSlider.GetComponent<Slider>();
                var fPosYVal = combatCol.Find("FocusBarPosYGroup/FocusBarPosYValueText");
                if (fPosYVal != null) so.FindProperty("focusBarPosYValueText").objectReferenceValue = fPosYVal.GetComponent<Text>();

                var aisSlider = combatCol.Find("ArrowIconSizeGroup/ArrowIconSizeSlider");
                if (aisSlider != null) so.FindProperty("arrowIconSizeSlider").objectReferenceValue = aisSlider.GetComponent<Slider>();
                var aisVal = combatCol.Find("ArrowIconSizeGroup/ArrowIconSizeValueText");
                if (aisVal != null) so.FindProperty("arrowIconSizeValueText").objectReferenceValue = aisVal.GetComponent<Text>();

                var rbwSlider = combatCol.Find("RuneBarWidthGroup/RuneBarWidthSlider");
                if (rbwSlider != null) so.FindProperty("runeBarWidthSlider").objectReferenceValue = rbwSlider.GetComponent<Slider>();
                var rbwVal = combatCol.Find("RuneBarWidthGroup/RuneBarWidthValueText");
                if (rbwVal != null) so.FindProperty("runeBarWidthValueText").objectReferenceValue = rbwVal.GetComponent<Text>();

                var rbhSlider = combatCol.Find("RuneBarHeightGroup/RuneBarHeightSlider");
                if (rbhSlider != null) so.FindProperty("runeBarHeightSlider").objectReferenceValue = rbhSlider.GetComponent<Slider>();
                var rbhVal = combatCol.Find("RuneBarHeightGroup/RuneBarHeightValueText");
                if (rbhVal != null) so.FindProperty("runeBarHeightValueText").objectReferenceValue = rbhVal.GetComponent<Text>();

                var rbySlider = combatCol.Find("RuneBarOffsetYGroup/RuneBarOffsetYSlider");
                if (rbySlider != null) so.FindProperty("runeBarOffsetYSlider").objectReferenceValue = rbySlider.GetComponent<Slider>();
                var rbyVal = combatCol.Find("RuneBarOffsetYGroup/RuneBarOffsetYValueText");
                if (rbyVal != null) so.FindProperty("runeBarOffsetYValueText").objectReferenceValue = rbyVal.GetComponent<Text>();

                var rbxSlider = combatCol.Find("RuneBarOffsetXGroup/RuneBarOffsetXSlider");
                if (rbxSlider != null) so.FindProperty("runeBarOffsetXSlider").objectReferenceValue = rbxSlider.GetComponent<Slider>();
                var rbxVal = combatCol.Find("RuneBarOffsetXGroup/RuneBarOffsetXValueText");
                if (rbxVal != null) so.FindProperty("runeBarOffsetXValueText").objectReferenceValue = rbxVal.GetComponent<Text>();

                var rbmToggle = combatCol.Find("ShowRuneBarOnlyInMagicToggle");
                if (rbmToggle != null) so.FindProperty("showRuneBarOnlyInMagicToggle").objectReferenceValue = rbmToggle.GetComponent<Toggle>();

                var hitToggle = combatCol.Find("ShowHitboxesToggle");
                if (hitToggle != null) so.FindProperty("showHitboxesToggle").objectReferenceValue = hitToggle.GetComponent<Toggle>();
            }

            so.ApplyModifiedProperties();
        }
    }
}
