using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Combat.Dialogue;

namespace Combat.Editor
{
    public static class DialogueUISetup
    {
        private static Font s_Font;

        [MenuItem("Combat/Setup Complete Dialogue UI")]
        public static GameObject SetupDialogueUI()
        {
            InitFont();

            // Ищем существующий Canvas или создаем отдельный для диалогов
            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            bool createdNewCanvas = false;

            if (canvas == null)
            {
                var canvasObj = new GameObject("DialogueCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObj.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 120;

                var scaler = canvasObj.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                createdNewCanvas = true;
            }

            // Удаляем старый UI диалогов, если уже был
            var oldUI = canvas.transform.Find("DialogueUI");
            if (oldUI != null)
            {
                Object.DestroyImmediate(oldUI.gameObject);
            }

            // Создаем корневой объект DialogueUI
            var dialogueUIObj = new GameObject("DialogueUI", typeof(RectTransform), typeof(CanvasGroup), typeof(DialogueUI));
            dialogueUIObj.transform.SetParent(canvas.transform, false);

            var rootRt = dialogueUIObj.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var cg = dialogueUIObj.GetComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;

            // 1. Окно диалога (снизу по центру)
            var winObj = new GameObject("DialogueWindow", typeof(RectTransform), typeof(Image), typeof(Outline));
            winObj.transform.SetParent(dialogueUIObj.transform, false);

            var winRt = winObj.GetComponent<RectTransform>();
            winRt.anchorMin = new Vector2(0.5f, 0f);
            winRt.anchorMax = new Vector2(0.5f, 0f);
            winRt.pivot = new Vector2(0.5f, 0f);
            winRt.anchoredPosition = new Vector2(0f, 35f);
            winRt.sizeDelta = new Vector2(920f, 175f);

            var winImg = winObj.GetComponent<Image>();
            winImg.color = new Color(0.04f, 0.05f, 0.08f, 0.94f);

            var winOutline = winObj.GetComponent<Outline>();
            winOutline.effectColor = new Color(0.25f, 0.55f, 0.85f, 0.55f);
            winOutline.effectDistance = new Vector2(2f, -2f);

            // 2. Имя говорящего
            var nameBgObj = new GameObject("SpeakerNamePlate", typeof(RectTransform), typeof(Image));
            nameBgObj.transform.SetParent(winObj.transform, false);

            var nameBgRt = nameBgObj.GetComponent<RectTransform>();
            nameBgRt.anchorMin = new Vector2(0f, 1f);
            nameBgRt.anchorMax = new Vector2(0f, 1f);
            nameBgRt.pivot = new Vector2(0f, 0f);
            nameBgRt.anchoredPosition = new Vector2(20f, -8f);
            nameBgRt.sizeDelta = new Vector2(230f, 36f);

            var nameBgImg = nameBgObj.GetComponent<Image>();
            nameBgImg.color = new Color(0.08f, 0.12f, 0.18f, 0.98f);

            var nameTextObj = new GameObject("SpeakerNameText", typeof(RectTransform), typeof(Text));
            nameTextObj.transform.SetParent(nameBgObj.transform, false);

            var nameTextRt = nameTextObj.GetComponent<RectTransform>();
            nameTextRt.anchorMin = Vector2.zero;
            nameTextRt.anchorMax = Vector2.one;
            nameTextRt.offsetMin = new Vector2(12f, 0f);
            nameTextRt.offsetMax = new Vector2(-12f, 0f);

            var speakerNameText = nameTextObj.GetComponent<Text>();
            speakerNameText.font = s_Font;
            speakerNameText.fontSize = 20;
            speakerNameText.fontStyle = FontStyle.Bold;
            speakerNameText.color = new Color(1f, 0.82f, 0.35f, 1f);
            speakerNameText.alignment = TextAnchor.MiddleLeft;
            speakerNameText.text = "Незнакомец";

            // 3. Текст реплики
            var textObj = new GameObject("DialogueText", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(winObj.transform, false);

            var textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(30f, 25f);
            textRt.offsetMax = new Vector2(-175f, -38f); // запас справа для портрета

            var dialogueText = textObj.GetComponent<Text>();
            dialogueText.font = s_Font;
            dialogueText.fontSize = 19;
            dialogueText.lineSpacing = 1.25f;
            dialogueText.color = new Color(0.95f, 0.95f, 0.97f, 1f);
            dialogueText.alignment = TextAnchor.UpperLeft;
            dialogueText.text = "Текст диалога...";

            // 4. Подсказка перехода (ContinuePrompt)
            var promptObj = new GameObject("ContinuePrompt", typeof(RectTransform), typeof(Text));
            promptObj.transform.SetParent(winObj.transform, false);

            var promptRt = promptObj.GetComponent<RectTransform>();
            promptRt.anchorMin = new Vector2(1f, 0f);
            promptRt.anchorMax = new Vector2(1f, 0f);
            promptRt.pivot = new Vector2(1f, 0f);
            promptRt.anchoredPosition = new Vector2(-180f, 12f);
            promptRt.sizeDelta = new Vector2(260f, 28f);

            var promptText = promptObj.GetComponent<Text>();
            promptText.font = s_Font;
            promptText.fontSize = 15;
            promptText.fontStyle = FontStyle.Italic;
            promptText.color = new Color(0.4f, 0.8f, 1.0f, 0.9f);
            promptText.alignment = TextAnchor.MiddleRight;
            promptText.text = "ЛКМ / Пробел ▶";

            // 5. Контейнер портрета (справа внутри окна)
            var portraitFrameObj = new GameObject("PortraitContainer", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            portraitFrameObj.transform.SetParent(winObj.transform, false);

            var portFrameRt = portraitFrameObj.GetComponent<RectTransform>();
            portFrameRt.anchorMin = new Vector2(1f, 0.5f);
            portFrameRt.anchorMax = new Vector2(1f, 0.5f);
            portFrameRt.pivot = new Vector2(1f, 0.5f);
            portFrameRt.anchoredPosition = new Vector2(-15f, 0f);
            portFrameRt.sizeDelta = new Vector2(140f, 140f);

            var portFrameImg = portraitFrameObj.GetComponent<Image>();
            portFrameImg.color = new Color(0.06f, 0.08f, 0.12f, 1f);

            // Внутреннее изображение портрета
            var portraitImgObj = new GameObject("PortraitImage", typeof(RectTransform), typeof(Image));
            portraitImgObj.transform.SetParent(portraitFrameObj.transform, false);

            var portImgRt = portraitImgObj.GetComponent<RectTransform>();
            portImgRt.anchorMin = new Vector2(0.5f, 0.5f);
            portImgRt.anchorMax = new Vector2(0.5f, 0.5f);
            portImgRt.pivot = new Vector2(0.5f, 0.5f);
            portImgRt.sizeDelta = new Vector2(140f, 140f);
            portImgRt.anchoredPosition = Vector2.zero;

            var portraitImg = portraitImgObj.GetComponent<Image>();
            portraitImg.preserveAspect = true;

            // Привязка компонента DialogueUI
            var dialogueUI = dialogueUIObj.GetComponent<DialogueUI>();
            var uiSerialized = new SerializedObject(dialogueUI);

            uiSerialized.FindProperty("mainCanvasGroup").objectReferenceValue = cg;
            uiSerialized.FindProperty("windowRect").objectReferenceValue = winRt;
            uiSerialized.FindProperty("backgroundImage").objectReferenceValue = winImg;
            uiSerialized.FindProperty("speakerNameText").objectReferenceValue = speakerNameText;
            uiSerialized.FindProperty("dialogueText").objectReferenceValue = dialogueText;
            uiSerialized.FindProperty("continuePrompt").objectReferenceValue = promptObj;
            uiSerialized.FindProperty("continuePromptText").objectReferenceValue = promptText;
            uiSerialized.FindProperty("portraitContainer").objectReferenceValue = portraitFrameObj;
            uiSerialized.FindProperty("portraitFrameRt").objectReferenceValue = portFrameRt;
            uiSerialized.FindProperty("portraitImage").objectReferenceValue = portraitImg;
            uiSerialized.FindProperty("portraitImageRt").objectReferenceValue = portImgRt;

            uiSerialized.ApplyModifiedProperties();

            // Создаем или находим DialogueManager
            var mgr = Object.FindAnyObjectByType<DialogueManager>();
            if (mgr == null)
            {
                var mgrObj = new GameObject("[DialogueManager]", typeof(DialogueManager));
                mgr = mgrObj.GetComponent<DialogueManager>();
            }

            var mgrSerialized = new SerializedObject(mgr);
            mgrSerialized.FindProperty("dialogueUI").objectReferenceValue = dialogueUI;
            mgrSerialized.ApplyModifiedProperties();

            // Сохраняем префаб
            string prefabPath = "Assets/Prefabs/UI/DialogueCanvas.prefab";
            if (createdNewCanvas)
            {
                PrefabUtility.SaveAsPrefabAssetAndConnect(canvas.gameObject, prefabPath, InteractionMode.AutomatedAction);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("<color=#00FFAA>[DialogueUISetup] Dialogue UI successfully created and wired!</color>");

            return dialogueUIObj;
        }

        private static void InitFont()
        {
            s_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (s_Font == null) s_Font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }
}
