using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Combat.Tutorial;

namespace Combat.Editor
{
    public static class TutorialObjectiveUISetup
    {
        private static Font s_Font;

        [MenuItem("Combat/Setup Tutorial Objective HUD")]
        public static GameObject SetupObjectiveHUD()
        {
            InitFont();

            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[TutorialObjectiveUISetup] Canvas not found in active scene!");
                return null;
            }

            // Удаляем старый виджет, если был
            var oldHUD = canvas.transform.Find("TutorialObjectiveHUD");
            if (oldHUD != null)
            {
                Object.DestroyImmediate(oldHUD.gameObject);
            }

            // Создаем корневой объект
            var hudObj = new GameObject("TutorialObjectiveHUD", typeof(RectTransform), typeof(CanvasGroup), typeof(TutorialObjectiveHUD));
            hudObj.transform.SetParent(canvas.transform, false);

            var rootRt = hudObj.GetComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(1f, 1f);
            rootRt.anchorMax = new Vector2(1f, 1f);
            rootRt.pivot = new Vector2(1f, 1f);
            rootRt.anchoredPosition = new Vector2(-25f, -25f);
            rootRt.sizeDelta = new Vector2(420f, 130f);

            var cg = hudObj.GetComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;

            // Панель ObjectivePanel
            var panelObj = new GameObject("ObjectivePanel", typeof(RectTransform), typeof(Image), typeof(Outline));
            panelObj.transform.SetParent(hudObj.transform, false);

            var panelRt = panelObj.GetComponent<RectTransform>();
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;

            var panelImg = panelObj.GetComponent<Image>();
            panelImg.color = new Color(0.03f, 0.04f, 0.07f, 0.92f);

            var outline = panelObj.GetComponent<Outline>();
            outline.effectColor = new Color(0.2f, 0.65f, 1f, 0.65f);
            outline.effectDistance = new Vector2(2f, -2f);

            // 1. Заголовок
            var headerObj = new GameObject("HeaderText", typeof(RectTransform), typeof(Text));
            headerObj.transform.SetParent(panelObj.transform, false);

            var headerRt = headerObj.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0f, 1f);
            headerRt.anchoredPosition = new Vector2(16f, -12f);
            headerRt.sizeDelta = new Vector2(-32f, 22f);

            var headerText = headerObj.GetComponent<Text>();
            headerText.font = s_Font;
            headerText.fontSize = 14;
            headerText.fontStyle = FontStyle.Bold;
            headerText.color = new Color(0.35f, 0.85f, 1f, 1f);
            headerText.alignment = TextAnchor.UpperLeft;
            headerText.text = "ЗАДАНИЕ";

            // 2. Инструкция
            var instrObj = new GameObject("InstructionText", typeof(RectTransform), typeof(Text));
            instrObj.transform.SetParent(panelObj.transform, false);

            var instrRt = instrObj.GetComponent<RectTransform>();
            instrRt.anchorMin = new Vector2(0f, 0f);
            instrRt.anchorMax = new Vector2(1f, 1f);
            instrRt.offsetMin = new Vector2(16f, 36f);
            instrRt.offsetMax = new Vector2(-16f, -34f);

            var instrText = instrObj.GetComponent<Text>();
            instrText.font = s_Font;
            instrText.fontSize = 15;
            instrText.lineSpacing = 1.15f;
            instrText.color = new Color(0.96f, 0.96f, 0.96f, 1f);
            instrText.alignment = TextAnchor.UpperLeft;
            instrText.text = "Зажмите ЛКМ и проведите мышкой по направлениям, чтобы ударить";

            // 3. Прогресс / Счётчик
            var progObj = new GameObject("ProgressText", typeof(RectTransform), typeof(Text));
            progObj.transform.SetParent(panelObj.transform, false);

            var progRt = progObj.GetComponent<RectTransform>();
            progRt.anchorMin = new Vector2(0f, 0f);
            progRt.anchorMax = new Vector2(1f, 0f);
            progRt.pivot = new Vector2(0f, 0f);
            progRt.anchoredPosition = new Vector2(16f, 10f);
            progRt.sizeDelta = new Vector2(-32f, 24f);

            var progText = progObj.GetComponent<Text>();
            progText.font = s_Font;
            progText.fontSize = 16;
            progText.fontStyle = FontStyle.Bold;
            progText.color = new Color(1f, 0.85f, 0.3f, 1f);
            progText.alignment = TextAnchor.MiddleLeft;
            progText.text = "Удар по манекену 0/3";

            // Привязка компонента TutorialObjectiveHUD
            var hud = hudObj.GetComponent<TutorialObjectiveHUD>();
            var serialized = new SerializedObject(hud);

            serialized.FindProperty("canvasGroup").objectReferenceValue = cg;
            serialized.FindProperty("panelRect").objectReferenceValue = panelRt;
            serialized.FindProperty("backgroundImage").objectReferenceValue = panelImg;
            serialized.FindProperty("borderOutline").objectReferenceValue = outline;
            serialized.FindProperty("headerText").objectReferenceValue = headerText;
            serialized.FindProperty("instructionText").objectReferenceValue = instrText;
            serialized.FindProperty("progressText").objectReferenceValue = progText;

            serialized.ApplyModifiedProperties();

            // Сохранение префаба
            string prefabPath = "Assets/Prefabs/UI/TutorialObjectiveHUD.prefab";
            PrefabUtility.SaveAsPrefabAsset(hudObj, prefabPath);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("<color=#00FFAA>[TutorialObjectiveUISetup] Tutorial Objective HUD successfully created and wired!</color>");

            return hudObj;
        }

        private static void InitFont()
        {
            s_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (s_Font == null) s_Font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }
}
