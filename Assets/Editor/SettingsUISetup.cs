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
            winRt.sizeDelta = new Vector2(680f, 540f);

            var winImg = winObj.GetComponent<Image>();
            winImg.sprite = s_Resources.background;
            winImg.type = Image.Type.Sliced;
            winImg.color = new Color(0.08f, 0.10f, 0.14f, 0.98f);

            // Заголовок (Header)
            CreateHeader(winObj.transform);

            // Контент (2 колонки: Controls и Visuals)
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
            var titleTextObj = CreateText("TitleText", headerObj.transform, "⚙  НАСТРОЙКИ БОЕВОЙ СИСТЕМЫ", 15, FontStyle.Bold, Color.white);
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
            bodyRt.offsetMin = new Vector2(25f, 60f);
            bodyRt.offsetMax = new Vector2(-25f, -56f);

            // Левая колонка: Controls
            var colLeftObj = new GameObject("ColControls", typeof(RectTransform));
            colLeftObj.transform.SetParent(bodyObj.transform, false);
            var leftRt = colLeftObj.GetComponent<RectTransform>();
            leftRt.anchorMin = new Vector2(0f, 0f);
            leftRt.anchorMax = new Vector2(0.485f, 1f);
            leftRt.offsetMin = Vector2.zero;
            leftRt.offsetMax = Vector2.zero;

            PopulateControlsColumn(colLeftObj.transform);

            // Разделительная линия по центру
            var dividerObj = new GameObject("Divider", typeof(RectTransform), typeof(Image));
            dividerObj.transform.SetParent(bodyObj.transform, false);
            var divRt = dividerObj.GetComponent<RectTransform>();
            divRt.anchorMin = new Vector2(0.5f, 0.05f);
            divRt.anchorMax = new Vector2(0.5f, 0.95f);
            divRt.sizeDelta = new Vector2(1f, 0f);
            dividerObj.GetComponent<Image>().color = new Color(0.2f, 0.25f, 0.35f, 0.5f);

            // Правая колонка: Visuals
            var colRightObj = new GameObject("ColVisuals", typeof(RectTransform));
            colRightObj.transform.SetParent(bodyObj.transform, false);
            var rightRt = colRightObj.GetComponent<RectTransform>();
            rightRt.anchorMin = new Vector2(0.515f, 0f);
            rightRt.anchorMax = new Vector2(1f, 1f);
            rightRt.offsetMin = Vector2.zero;
            rightRt.offsetMax = Vector2.zero;

            PopulateVisualsColumn(colRightObj.transform);
        }

        private static void PopulateControlsColumn(Transform parent)
        {
            float yPos = -5f;
            const float itemGap = 42f;

            // Заголовок секции
            var secTitle = CreateText("SecTitleControls", parent, "— УПРАВЛЕНИЕ И ЖЕСТЫ —", 13, FontStyle.Bold, new Color(1f, 0.2f, 0.3f, 1f));
            PositionItem(secTitle.GetComponent<RectTransform>(), yPos, 22f);
            yPos -= 30f;

            // 1. Чувствительность мыши (Slider: 0.25 - 2.5)
            CreateSliderItem("Sensitivity", parent, "Чувствительность", 0.25f, 2.5f, 1.0f, "1.00x", ref yPos, itemGap);

            // 2. Мертвая зона (Slider: 10 - 60)
            CreateSliderItem("Deadzone", parent, "Мертвая зона", 10f, 60f, 25f, "25 px", ref yPos, itemGap);

            // 3. Режим активации (Dropdown: Hold, Toggle)
            CreateDropdownItem("ActivationMode", parent, "Режим активации", new List<string> { "Удержание (Hold)", "Переключение (Toggle)" }, ref yPos, itemGap + 6f);

            // 4. Инверсия X (Toggle)
            CreateToggleItem("InvertX", parent, "Инвертировать ось X", false, ref yPos, 32f);

            // 5. Инверсия Y (Toggle)
            CreateToggleItem("InvertY", parent, "Инвертировать ось Y", false, ref yPos, 32f);

            // 6. Быстрый каст у края (Toggle)
            CreateToggleItem("QuickCast", parent, "Быстрый удар у края (Quick-Cast)", false, ref yPos, 32f);

            // 7. Стик геймпада (Toggle)
            CreateToggleItem("Gamepad", parent, "Правый стик геймпада", true, ref yPos, 32f);
        }

        private static void PopulateVisualsColumn(Transform parent)
        {
            float yPos = -5f;
            const float itemGap = 42f;

            // Заголовок секции
            var secTitle = CreateText("SecTitleVisuals", parent, "— ВИЗУАЛ И ИНТЕРФЕЙС —", 13, FontStyle.Bold, new Color(0.3f, 0.85f, 1f, 1f));
            PositionItem(secTitle.GetComponent<RectTransform>(), yPos, 22f);
            yPos -= 30f;

            // 1. Расположение колеса (Dropdown)
            CreateDropdownItem("Placement", parent, "Расположение на экране", new List<string> { "Справа внизу", "По центру экрана", "Слева внизу" }, ref yPos, itemGap + 6f);

            // 2. Цветовая тема (Dropdown)
            CreateDropdownItem("ColorTheme", parent, "Цветовая схема", new List<string> {
                "Багровый неон (Crimson)",
                "Кибер-циан (Cyber Cyan)",
                "Золото самурая (Gold)",
                "Изумрудный (Emerald)",
                "Контрастный желтый (Yellow)"
            }, ref yPos, itemGap + 6f);

            // 3. Масштаб колеса (Slider: 0.75 - 1.4)
            CreateSliderItem("HudScale", parent, "Масштаб колеса", 0.75f, 1.4f, 1.0f, "100%", ref yPos, itemGap);

            // 4. Прозрачность в покое (Slider: 0.0 - 1.0)
            CreateSliderItem("IdleOpacity", parent, "Прозрачность в покое", 0.0f, 1.0f, 0.45f, "45%", ref yPos, itemGap);

            // 5. Показывать плашку атак (Toggle)
            CreateToggleItem("ShowPlaque", parent, "Показывать плашку атак", true, ref yPos, 32f);
        }

        private static void CreateSliderItem(string name, Transform parent, string labelText, float min, float max, float defVal, string defDisplay, ref float yPos, float gap)
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
            if (fillImg != null) fillImg.color = new Color(0.95f, 0.15f, 0.25f, 1f);

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
            {
                // При клике на кнопку в футере тоже закрываем
                footerCloseBtn.GetComponent<Button>().onClick.AddListener(ui.CloseSettings);
            }

            var resetBtn = panelObj.transform.Find("SettingsWindow/Footer/ResetButton");
            if (resetBtn != null)
                so.FindProperty("resetButton").objectReferenceValue = resetBtn.GetComponent<Button>();

            // 2. Control Settings UI
            var leftCol = panelObj.transform.Find("SettingsWindow/Body/ColControls");
            if (leftCol != null)
            {
                var sensSlider = leftCol.Find("SensitivityGroup/SensitivitySlider");
                if (sensSlider != null) so.FindProperty("sensitivitySlider").objectReferenceValue = sensSlider.GetComponent<Slider>();

                var sensVal = leftCol.Find("SensitivityGroup/SensitivityValueText");
                if (sensVal != null) so.FindProperty("sensitivityValueText").objectReferenceValue = sensVal.GetComponent<Text>();

                var deadSlider = leftCol.Find("DeadzoneGroup/DeadzoneSlider");
                if (deadSlider != null) so.FindProperty("deadzoneSlider").objectReferenceValue = deadSlider.GetComponent<Slider>();

                var deadVal = leftCol.Find("DeadzoneGroup/DeadzoneValueText");
                if (deadVal != null) so.FindProperty("deadzoneValueText").objectReferenceValue = deadVal.GetComponent<Text>();

                var actDd = leftCol.Find("ActivationModeGroup/ActivationModeDropdown");
                if (actDd != null) so.FindProperty("activationModeDropdown").objectReferenceValue = actDd.GetComponent<Dropdown>();

                var invX = leftCol.Find("InvertXToggle");
                if (invX != null) so.FindProperty("invertXToggle").objectReferenceValue = invX.GetComponent<Toggle>();

                var invY = leftCol.Find("InvertYToggle");
                if (invY != null) so.FindProperty("invertYToggle").objectReferenceValue = invY.GetComponent<Toggle>();

                var qc = leftCol.Find("QuickCastToggle");
                if (qc != null) so.FindProperty("quickCastToggle").objectReferenceValue = qc.GetComponent<Toggle>();

                var gp = leftCol.Find("GamepadToggle");
                if (gp != null) so.FindProperty("gamepadToggle").objectReferenceValue = gp.GetComponent<Toggle>();
            }

            // 3. Visual & HUD UI
            var rightCol = panelObj.transform.Find("SettingsWindow/Body/ColVisuals");
            if (rightCol != null)
            {
                var plcDd = rightCol.Find("PlacementGroup/PlacementDropdown");
                if (plcDd != null) so.FindProperty("placementDropdown").objectReferenceValue = plcDd.GetComponent<Dropdown>();

                var thmDd = rightCol.Find("ColorThemeGroup/ColorThemeDropdown");
                if (thmDd != null) so.FindProperty("colorThemeDropdown").objectReferenceValue = thmDd.GetComponent<Dropdown>();

                var scaleSlider = rightCol.Find("HudScaleGroup/HudScaleSlider");
                if (scaleSlider != null) so.FindProperty("hudScaleSlider").objectReferenceValue = scaleSlider.GetComponent<Slider>();

                var scaleVal = rightCol.Find("HudScaleGroup/HudScaleValueText");
                if (scaleVal != null) so.FindProperty("hudScaleValueText").objectReferenceValue = scaleVal.GetComponent<Text>();

                var opSlider = rightCol.Find("IdleOpacityGroup/IdleOpacitySlider");
                if (opSlider != null) so.FindProperty("idleOpacitySlider").objectReferenceValue = opSlider.GetComponent<Slider>();

                var opVal = rightCol.Find("IdleOpacityGroup/IdleOpacityValueText");
                if (opVal != null) so.FindProperty("idleOpacityValueText").objectReferenceValue = opVal.GetComponent<Text>();

                var plqToggle = rightCol.Find("ShowPlaqueToggle");
                if (plqToggle != null) so.FindProperty("showPlaqueToggle").objectReferenceValue = plqToggle.GetComponent<Toggle>();
            }

            so.ApplyModifiedProperties();
        }
    }
}
