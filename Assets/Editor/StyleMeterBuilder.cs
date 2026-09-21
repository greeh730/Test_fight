using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Combat.Style;
using Combat.UI;

namespace Combat.Editor
{
    public static class StyleMeterBuilder
    {
        [MenuItem("Tools/Combat/Setup Style Meter")]
        public static string SetupStyleMeter()
        {
            // 1. [StyleManager] GameObject
            var styleMgrGo = GameObject.Find("[StyleManager]");
            if (styleMgrGo == null)
            {
                styleMgrGo = new GameObject("[StyleManager]");
                styleMgrGo.AddComponent<StyleManager>();
            }
            else if (styleMgrGo.GetComponent<StyleManager>() == null)
            {
                styleMgrGo.AddComponent<StyleManager>();
            }

            // 2. Setup StyleMeter in Canvas
            var canvas = GameObject.Find("Canvas");
            if (canvas == null) return "Canvas not found";

            var squareSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/v2/Square.png");
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                var allFonts = Resources.FindObjectsOfTypeAll<Font>();
                foreach (var f in allFonts)
                {
                    if (f.name.Contains("Bold") || f.name.Contains("Legacy")) { font = f; break; }
                }
            }

            // Clean up existing if present
            var existing = canvas.transform.Find("StyleMeter");
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var rootGo = new GameObject("StyleMeter");
            rootGo.transform.SetParent(canvas.transform, false);

            var rootRt = rootGo.AddComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(1f, 1f);
            rootRt.anchorMax = new Vector2(1f, 1f);
            rootRt.pivot = new Vector2(1f, 1f);
            rootRt.anchoredPosition = new Vector2(-75f, -18f);
            rootRt.sizeDelta = new Vector2(195f, 70f);

            var bgImg = rootGo.AddComponent<Image>();
            bgImg.sprite = squareSprite;
            bgImg.color = new Color(0.07f, 0.09f, 0.13f, 0.88f);

            var outline = rootGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.22f, 0.32f, 0.44f, 0.65f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var meterUI = rootGo.AddComponent<StyleMeterUI>();
            var meterSo = new SerializedObject(meterUI);

            // Rank Badge (Container for Big Letter)
            var badgeGo = new GameObject("RankBadge");
            badgeGo.transform.SetParent(rootGo.transform, false);
            var badgeRt = badgeGo.AddComponent<RectTransform>();
            badgeRt.anchorMin = new Vector2(0f, 0.5f);
            badgeRt.anchorMax = new Vector2(0f, 0.5f);
            badgeRt.pivot = new Vector2(0.5f, 0.5f);
            badgeRt.anchoredPosition = new Vector2(35f, 0f);
            badgeRt.sizeDelta = new Vector2(58f, 58f);

            var badgeBg = badgeGo.AddComponent<Image>();
            badgeBg.sprite = squareSprite;
            badgeBg.color = new Color(0.12f, 0.15f, 0.20f, 0.95f);

            var letterGo = new GameObject("RankLetterText");
            letterGo.transform.SetParent(badgeGo.transform, false);
            var letterRt = letterGo.AddComponent<RectTransform>();
            letterRt.anchorMin = Vector2.zero;
            letterRt.anchorMax = Vector2.one;
            letterRt.sizeDelta = Vector2.zero;
            var letterText = letterGo.AddComponent<Text>();
            letterText.font = font;
            letterText.fontSize = 44;
            letterText.fontStyle = FontStyle.Bold;
            letterText.alignment = TextAnchor.MiddleCenter;
            letterText.text = "D";
            letterText.color = new Color(0.72f, 0.76f, 0.82f);
            var letterOutline = letterGo.AddComponent<Outline>();
            letterOutline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            letterOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // Rank Title Text
            var titleGo = new GameObject("RankTitleText");
            titleGo.transform.SetParent(rootGo.transform, false);
            var titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(0f, 1f);
            titleRt.pivot = new Vector2(0f, 1f);
            titleRt.anchoredPosition = new Vector2(72f, -12f);
            titleRt.sizeDelta = new Vector2(115f, 18f);
            var titleText = titleGo.AddComponent<Text>();
            titleText.font = font;
            titleText.fontSize = 13;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleLeft;
            titleText.text = "DULL";
            titleText.color = new Color(0.72f, 0.76f, 0.82f);

            // Progress Bar BG
            var barBgGo = new GameObject("RankProgressBar_BG");
            barBgGo.transform.SetParent(rootGo.transform, false);
            var barBgRt = barBgGo.AddComponent<RectTransform>();
            barBgRt.anchorMin = new Vector2(0f, 1f);
            barBgRt.anchorMax = new Vector2(0f, 1f);
            barBgRt.pivot = new Vector2(0f, 1f);
            barBgRt.anchoredPosition = new Vector2(72f, -32f);
            barBgRt.sizeDelta = new Vector2(115f, 6f);
            var barBgImg = barBgGo.AddComponent<Image>();
            barBgImg.sprite = squareSprite;
            barBgImg.color = new Color(0.14f, 0.17f, 0.22f, 1f);

            // Progress Bar Fill
            var barFillGo = new GameObject("RankProgressBar_Fill");
            barFillGo.transform.SetParent(barBgGo.transform, false);
            var barFillRt = barFillGo.AddComponent<RectTransform>();
            barFillRt.anchorMin = Vector2.zero;
            barFillRt.anchorMax = Vector2.one;
            barFillRt.sizeDelta = Vector2.zero;
            var barFillImg = barFillGo.AddComponent<Image>();
            barFillImg.sprite = squareSprite;
            barFillImg.type = Image.Type.Filled;
            barFillImg.fillMethod = Image.FillMethod.Horizontal;
            barFillImg.fillOrigin = 0;
            barFillImg.fillAmount = 0.0f;
            barFillImg.color = new Color(0.72f, 0.76f, 0.82f);

            // Score Text
            var scoreGo = new GameObject("ScoreText");
            scoreGo.transform.SetParent(rootGo.transform, false);
            var scoreRt = scoreGo.AddComponent<RectTransform>();
            scoreRt.anchorMin = new Vector2(0f, 1f);
            scoreRt.anchorMax = new Vector2(0f, 1f);
            scoreRt.pivot = new Vector2(0f, 1f);
            scoreRt.anchoredPosition = new Vector2(72f, -44f);
            scoreRt.sizeDelta = new Vector2(115f, 18f);
            var scoreText = scoreGo.AddComponent<Text>();
            scoreText.font = font;
            scoreText.fontSize = 12;
            scoreText.fontStyle = FontStyle.Bold;
            scoreText.alignment = TextAnchor.MiddleLeft;
            scoreText.text = "STYLE: 0";
            scoreText.color = new Color(0.92f, 0.94f, 0.96f, 0.95f);

            // Popup Bonus Text (underneath the meter)
            var popupGo = new GameObject("PopupBonusText");
            popupGo.transform.SetParent(rootGo.transform, false);
            var popupRt = popupGo.AddComponent<RectTransform>();
            popupRt.anchorMin = new Vector2(0.5f, 0f);
            popupRt.anchorMax = new Vector2(0.5f, 0f);
            popupRt.pivot = new Vector2(0.5f, 1f);
            popupRt.anchoredPosition = new Vector2(0f, -6f);
            popupRt.sizeDelta = new Vector2(195f, 24f);
            var popupText = popupGo.AddComponent<Text>();
            popupText.font = font;
            popupText.fontSize = 15;
            popupText.fontStyle = FontStyle.Bold;
            popupText.alignment = TextAnchor.MiddleCenter;
            popupText.text = "+500 KILL!";
            popupText.color = new Color(1f, 0.88f, 0.2f, 1f);
            var popupOutline = popupGo.AddComponent<Outline>();
            popupOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            popupOutline.effectDistance = new Vector2(1.5f, -1.5f);
            popupGo.SetActive(false);

            // Wire serialized fields on StyleMeterUI
            meterSo.FindProperty("rankLetterText").objectReferenceValue = letterText;
            meterSo.FindProperty("rankTitleText").objectReferenceValue = titleText;
            meterSo.FindProperty("rankFillBar").objectReferenceValue = barFillImg;
            meterSo.FindProperty("scoreText").objectReferenceValue = scoreText;
            meterSo.FindProperty("popupBonusText").objectReferenceValue = popupText;
            meterSo.FindProperty("rankBadgeTransform").objectReferenceValue = badgeRt;
            meterSo.ApplyModifiedProperties();

            // Save Prefabs and Scenes
            string mgrPath = "Assets/Prefabs/Managers/CombatStyleManager.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(styleMgrGo, mgrPath, InteractionMode.AutomatedAction);

            string canvasPath = "Assets/Prefabs/UI/CombatCanvas.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(canvas, canvasPath, InteractionMode.AutomatedAction);

            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();

            return "StyleMeter UI and [StyleManager] created, wired, and saved to prefabs!";
        }
    }
}
