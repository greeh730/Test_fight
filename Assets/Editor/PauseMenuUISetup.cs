using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Combat.UI;

namespace Combat.Editor
{
    public static class PauseMenuUISetup
    {
        private static Font s_Font;
        private static DefaultControls.Resources s_Resources;

        [MenuItem("Combat/Setup Complete Pause Menu UI")]
        public static void SetupUI()
        {
            InitResources();

            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[PauseMenuUISetup] Canvas not found in active scene!");
                return;
            }

            // Удаляем старую панель паузы, если уже была создана
            var oldPanel = canvas.transform.Find("PausePanel");
            if (oldPanel != null)
            {
                Object.DestroyImmediate(oldPanel.gameObject);
            }

            // Создаем новую панель паузы
            var panelObj = CreatePausePanel(canvas.transform);

            // Навешиваем и связываем PauseMenuController
            var controller = canvas.GetComponent<PauseMenuController>();
            if (controller == null)
            {
                controller = canvas.gameObject.AddComponent<PauseMenuController>();
            }

            WireComponentReferences(controller, panelObj, canvas.GetComponent<CombatSettingsUI>());

            // По умолчанию панель скрыта
            panelObj.SetActive(false);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("<color=#4EE2EC>[PauseMenuUISetup] Complete Pause Menu UI successfully created and wired!</color>");
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

        private static GameObject CreatePausePanel(Transform parent)
        {
            // Полноэкранный темный оверлей
            var panelObj = new GameObject("PausePanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            panelObj.transform.SetParent(parent, false);

            var panelRt = panelObj.GetComponent<RectTransform>();
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;

            var panelImg = panelObj.GetComponent<Image>();
            panelImg.color = new Color(0.02f, 0.03f, 0.05f, 0.78f);

            var cg = panelObj.GetComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;

            // Центральное окно паузы
            var winObj = new GameObject("PauseWindow", typeof(RectTransform), typeof(Image));
            winObj.transform.SetParent(panelObj.transform, false);

            var winRt = winObj.GetComponent<RectTransform>();
            winRt.anchorMin = new Vector2(0.5f, 0.5f);
            winRt.anchorMax = new Vector2(0.5f, 0.5f);
            winRt.pivot = new Vector2(0.5f, 0.5f);
            winRt.anchoredPosition = Vector2.zero;
            winRt.sizeDelta = new Vector2(380f, 490f);

            var winImg = winObj.GetComponent<Image>();
            winImg.sprite = s_Resources.background;
            winImg.type = Image.Type.Sliced;
            winImg.color = new Color(0.07f, 0.09f, 0.13f, 0.98f);

            // 1. Заголовок
            CreateHeader(winObj.transform);

            // 2. Список кнопок
            CreateButtons(winObj.transform);

            // 3. Подвал с подсказкой и обратной связью
            CreateFooter(winObj.transform);

            return panelObj;
        }

        private static void CreateHeader(Transform parent)
        {
            var headerObj = new GameObject("Header", typeof(RectTransform));
            headerObj.transform.SetParent(parent, false);

            var rt = headerObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -15f);
            rt.sizeDelta = new Vector2(0f, 65f);

            var titleTextObj = CreateText("TitleText", headerObj.transform, "//  П А У З А", 20, FontStyle.Bold, new Color(0.2f, 0.95f, 1f, 1f));
            var titleRt = titleTextObj.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0.35f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 0.5f);
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;

            // Акцентная неоновая полоса
            var lineObj = new GameObject("AccentLine", typeof(RectTransform), typeof(Image));
            lineObj.transform.SetParent(headerObj.transform, false);
            var lineRt = lineObj.GetComponent<RectTransform>();
            lineRt.anchorMin = new Vector2(0.2f, 0f);
            lineRt.anchorMax = new Vector2(0.8f, 0f);
            lineRt.pivot = new Vector2(0.5f, 0.5f);
            lineRt.sizeDelta = new Vector2(0f, 2f);

            var lineImg = lineObj.GetComponent<Image>();
            lineImg.color = new Color(0.2f, 0.95f, 1f, 0.65f);
        }

        private static void CreateButtons(Transform parent)
        {
            var btnContainer = new GameObject("ButtonsContainer", typeof(RectTransform));
            btnContainer.transform.SetParent(parent, false);

            var crt = btnContainer.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.08f, 0.15f);
            crt.anchorMax = new Vector2(0.92f, 0.84f);
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;

            float yPos = 0f;
            float btnHeight = 42f;
            float btnGap = 10f;

            // 1. Продолжить (акцентная кнопка)
            CreateMenuButton("ResumeButton", btnContainer.transform, "ПРОДОЛЖИТЬ", new Color(0.85f, 0.15f, 0.25f, 1f), ref yPos, btnHeight, btnGap, isPrimary: true);

            // 2. Быстрое сохранение
            CreateMenuButton("QuickSaveButton", btnContainer.transform, "БЫСТРОЕ СОХРАНЕНИЕ", new Color(0.14f, 0.18f, 0.25f, 1f), ref yPos, btnHeight, btnGap, isPrimary: false);

            // 3. Быстрая загрузка
            CreateMenuButton("QuickLoadButton", btnContainer.transform, "БЫСТРАЯ ЗАГРУЗКА", new Color(0.14f, 0.18f, 0.25f, 1f), ref yPos, btnHeight, btnGap, isPrimary: false);

            // 4. Настройки
            CreateMenuButton("SettingsButton", btnContainer.transform, "НАСТРОЙКИ", new Color(0.14f, 0.18f, 0.25f, 1f), ref yPos, btnHeight, btnGap, isPrimary: false);

            // 5. В главное меню
            CreateMenuButton("MainMenuButton", btnContainer.transform, "В ГЛАВНОЕ МЕНЮ", new Color(0.18f, 0.12f, 0.14f, 1f), ref yPos, btnHeight, btnGap, isPrimary: false);
        }

        private static void CreateMenuButton(string name, Transform parent, string label, Color bgColor, ref float yPos, float height, float gap, bool isPrimary)
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
            colors.highlightedColor = isPrimary ? new Color(1f, 0.25f, 0.35f, 1f) : new Color(0.22f, 0.28f, 0.38f, 1f);
            colors.pressedColor = isPrimary ? new Color(0.65f, 0.1f, 0.18f, 1f) : new Color(0.1f, 0.13f, 0.18f, 1f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;

            var text = btnObj.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = label;
                text.font = s_Font;
                text.fontSize = isPrimary ? 13 : 12;
                text.fontStyle = isPrimary ? FontStyle.Bold : FontStyle.Normal;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.white;
            }

            yPos -= (height + gap);
        }

        private static void CreateFooter(Transform parent)
        {
            var footerObj = new GameObject("Footer", typeof(RectTransform));
            footerObj.transform.SetParent(parent, false);

            var rt = footerObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0.18f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Текст обратной связи (заглушка сохранения / загрузки)
            var fbTextObj = CreateText("FeedbackText", footerObj.transform, string.Empty, 11, FontStyle.Italic, new Color(1f, 0.85f, 0.2f, 1f));
            var fbrt = fbTextObj.GetComponent<RectTransform>();
            fbrt.anchorMin = new Vector2(0f, 0.5f);
            fbrt.anchorMax = new Vector2(1f, 1f);
            fbrt.offsetMin = Vector2.zero;
            fbrt.offsetMax = Vector2.zero;

            // Подсказка горячей клавиши
            var hintTextObj = CreateText("HintText", footerObj.transform, "[ ESC — вернуться в игру ]", 10, FontStyle.Normal, new Color(0.45f, 0.52f, 0.62f, 1f));
            var hrt = hintTextObj.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0f, 0f);
            hrt.anchorMax = new Vector2(1f, 0.5f);
            hrt.offsetMin = Vector2.zero;
            hrt.offsetMax = Vector2.zero;
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

        private static void WireComponentReferences(PauseMenuController controller, GameObject panelObj, CombatSettingsUI settingsUI)
        {
            var so = new SerializedObject(controller);

            so.FindProperty("pausePanel").objectReferenceValue = panelObj;
            so.FindProperty("panelCanvasGroup").objectReferenceValue = panelObj.GetComponent<CanvasGroup>();

            var pKeyProp = so.FindProperty("pauseKey");
            if (pKeyProp != null)
            {
                pKeyProp.intValue = (int)KeyCode.Escape;
            }

            var btnContainer = panelObj.transform.Find("PauseWindow/ButtonsContainer");
            if (btnContainer != null)
            {
                var resBtn = btnContainer.Find("ResumeButton");
                if (resBtn != null) so.FindProperty("resumeButton").objectReferenceValue = resBtn.GetComponent<Button>();

                var saveBtn = btnContainer.Find("QuickSaveButton");
                if (saveBtn != null) so.FindProperty("quickSaveButton").objectReferenceValue = saveBtn.GetComponent<Button>();

                var loadBtn = btnContainer.Find("QuickLoadButton");
                if (loadBtn != null) so.FindProperty("quickLoadButton").objectReferenceValue = loadBtn.GetComponent<Button>();

                var setBtn = btnContainer.Find("SettingsButton");
                if (setBtn != null) so.FindProperty("settingsButton").objectReferenceValue = setBtn.GetComponent<Button>();

                var mainBtn = btnContainer.Find("MainMenuButton");
                if (mainBtn != null) so.FindProperty("mainMenuButton").objectReferenceValue = mainBtn.GetComponent<Button>();
            }

            var fbText = panelObj.transform.Find("PauseWindow/Footer/FeedbackText");
            if (fbText != null)
            {
                so.FindProperty("feedbackText").objectReferenceValue = fbText.GetComponent<Text>();
            }

            if (settingsUI != null)
            {
                so.FindProperty("settingsUI").objectReferenceValue = settingsUI;
            }

            so.ApplyModifiedProperties();
        }
    }
}
