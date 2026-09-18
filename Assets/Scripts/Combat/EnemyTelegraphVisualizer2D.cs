using System.Collections;
using UnityEngine;

namespace Combat
{
    [DisallowMultipleComponent]
    public class EnemyTelegraphVisualizer2D : MonoBehaviour
    {
        [Header("--- Telegraph Colors ---")]
        [SerializeField] private Color telegraphFillColor = new Color(1f, 0.55f, 0.1f, 0.28f); // Полупрозрачный оранжевый
        [SerializeField] private Color telegraphBorderColor = new Color(1f, 0.85f, 0.2f, 0.95f); // Яркий контур
        [SerializeField] private Color activeStrikeFillColor = new Color(1f, 0.15f, 0.15f, 0.85f); // Ярко-красный активный удар
        [SerializeField] private Color activeStrikeBorderColor = new Color(1f, 0.9f, 0.9f, 1f);

        [Header("--- Alert Cue ---")]
        [SerializeField] private Color alertColor = new Color(1f, 0.25f, 0.2f, 1f);

        private GameObject _hitboxObj;
        private SpriteRenderer _fillRenderer;
        private LineRenderer _borderRenderer;
        private GameObject _alertObj;
        private TextMesh _alertText;

        private static Sprite _boxSprite;

        private void Awake()
        {
            EnsureSprite();
            CreateHitboxObjects();
            CreateAlertObjects();
        }

        private static void EnsureSprite()
        {
            if (_boxSprite != null) return;
            var tex = new Texture2D(4, 4);
            var cols = new Color[16];
            for (int i = 0; i < 16; i++) cols[i] = Color.white;
            tex.SetPixels(cols);
            tex.Apply();
            _boxSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
        }

        private void CreateHitboxObjects()
        {
            if (_hitboxObj != null) return;

            _hitboxObj = new GameObject("Enemy_Hitbox_Visual");
            _hitboxObj.transform.SetParent(transform, false);

            _fillRenderer = _hitboxObj.AddComponent<SpriteRenderer>();
            _fillRenderer.sprite = _boxSprite;
            _fillRenderer.sortingOrder = 45;

            _borderRenderer = _hitboxObj.AddComponent<LineRenderer>();
            _borderRenderer.positionCount = 5;
            _borderRenderer.useWorldSpace = false;
            _borderRenderer.loop = true;
            _borderRenderer.startWidth = 0.045f;
            _borderRenderer.endWidth = 0.045f;
            _borderRenderer.sortingOrder = 46;

            var mat = new Material(Shader.Find("Sprites/Default"));
            _borderRenderer.material = mat;

            Vector3[] pts = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(-0.5f, -0.5f, 0f)
            };
            _borderRenderer.SetPositions(pts);

            _hitboxObj.SetActive(false);
        }

        private void CreateAlertObjects()
        {
            if (_alertObj != null) return;

            _alertObj = new GameObject("Enemy_Alert_Indicator");
            _alertObj.transform.SetParent(transform, false);
            _alertObj.transform.localPosition = new Vector3(0f, 0.95f, 0f);

            _alertText = _alertObj.AddComponent<TextMesh>();
            _alertText.text = "!";
            _alertText.fontSize = 42;
            _alertText.characterSize = 0.09f;
            _alertText.alignment = TextAlignment.Center;
            _alertText.anchor = TextAnchor.MiddleCenter;
            _alertText.color = alertColor;
            _alertText.fontStyle = FontStyle.Bold;

            var mr = _alertObj.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 60;

            _alertObj.SetActive(false);
        }

        /// <summary>
        /// Отображает полупрозрачный предупреждающий хитбокс замаха
        /// </summary>
        public void ShowTelegraph(Vector2 center, Vector2 size, float pulseFactor = 1f)
        {
            if (_hitboxObj == null) CreateHitboxObjects();
            _hitboxObj.SetActive(true);

            _hitboxObj.transform.position = new Vector3(center.x, center.y, 0f);
            _hitboxObj.transform.localScale = new Vector3(size.x, size.y, 1f);

            Color fill = telegraphFillColor;
            fill.a = Mathf.Lerp(0.2f, 0.45f, pulseFactor);
            _fillRenderer.color = fill;

            Color border = telegraphBorderColor;
            border.a = Mathf.Lerp(0.6f, 1f, pulseFactor);
            _borderRenderer.startColor = border;
            _borderRenderer.endColor = border;

            if (_alertObj != null)
            {
                _alertObj.SetActive(true);
                float scale = Mathf.Lerp(1.0f, 1.35f, pulseFactor);
                _alertObj.transform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        /// <summary>
        /// Отображает активный хитбокс удара
        /// </summary>
        public void ShowActiveStrike(Vector2 center, Vector2 size)
        {
            if (_hitboxObj == null) CreateHitboxObjects();
            _hitboxObj.SetActive(true);

            _hitboxObj.transform.position = new Vector3(center.x, center.y, 0f);
            _hitboxObj.transform.localScale = new Vector3(size.x, size.y, 1f);

            _fillRenderer.color = activeStrikeFillColor;
            _borderRenderer.startColor = activeStrikeBorderColor;
            _borderRenderer.endColor = activeStrikeBorderColor;

            if (_alertObj != null) _alertObj.SetActive(false);
        }

        /// <summary>
        /// Скрывает хитбокс и индикатор предупреждения
        /// </summary>
        public void HideHitbox()
        {
            if (_hitboxObj != null) _hitboxObj.SetActive(false);
            if (_alertObj != null) _alertObj.SetActive(false);
        }

        /// <summary>
        /// Создает парящий текст "КОНТРАТАКА!" в мировом пространстве
        /// </summary>
        public void ShowCounterAttackPopup(Vector3 position)
        {
            StartCoroutine(SpawnCounterTextRoutine(position));
        }

        private IEnumerator SpawnCounterTextRoutine(Vector3 spawnPos)
        {
            var go = new GameObject("CounterText_Popup");
            go.transform.position = spawnPos + new Vector3(0f, 1.2f, 0f);

            var tm = go.AddComponent<TextMesh>();
            tm.text = "КОНТРАТАКА!";
            tm.fontSize = 44;
            tm.characterSize = 0.085f;
            tm.alignment = TextAlignment.Center;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.fontStyle = FontStyle.Bold;
            tm.color = new Color(1f, 0.9f, 0.2f, 1f); // Золотисто-желтый

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 70;

            float duration = 0.95f;
            float elapsed = 0f;
            Vector3 startPos = go.transform.position;
            Vector3 endPos = startPos + new Vector3(0f, 1.1f, 0f);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;

                if (go != null)
                {
                    go.transform.position = Vector3.Lerp(startPos, endPos, t);
                    // Fade out
                    Color c = tm.color;
                    c.a = Mathf.Clamp01(1f - (t * t));
                    tm.color = c;
                }
                yield return null;
            }

            if (go != null) Destroy(go);
        }

        /// <summary>
        /// Создает парящий текст "ВРАГ ПОВЕРЖЕН!" в мировом пространстве
        /// </summary>
        public void ShowDefeatPopup(Vector3 position)
        {
            StartCoroutine(SpawnDefeatTextRoutine(position));
        }

        private IEnumerator SpawnDefeatTextRoutine(Vector3 spawnPos)
        {
            var go = new GameObject("DefeatText_Popup");
            go.transform.position = spawnPos + new Vector3(0f, 1.3f, 0f);

            var tm = go.AddComponent<TextMesh>();
            tm.text = "ВРАГ ПОВЕРЖЕН!";
            tm.fontSize = 46;
            tm.characterSize = 0.088f;
            tm.alignment = TextAlignment.Center;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.fontStyle = FontStyle.Bold;
            tm.color = new Color(0.3f, 1f, 0.45f, 1f); // Ярко-зеленый/триумфальный

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 75;

            float duration = 1.2f;
            float elapsed = 0f;
            Vector3 startPos = go.transform.position;
            Vector3 endPos = startPos + new Vector3(0f, 1.2f, 0f);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;

                if (go != null)
                {
                    go.transform.position = Vector3.Lerp(startPos, endPos, t);
                    Color c = tm.color;
                    c.a = Mathf.Clamp01(1f - (t * t));
                    tm.color = c;
                }
                yield return null;
            }

            if (go != null) Destroy(go);
        }

        /// <summary>
        /// Создает парящий текст "НЕТ СТАМИНЫ!" при исчерпании шкалы выносливости
        /// </summary>
        public void ShowNoStaminaPopup(Vector3 position)
        {
            StartCoroutine(SpawnNoStaminaTextRoutine(position));
        }

        private IEnumerator SpawnNoStaminaTextRoutine(Vector3 spawnPos)
        {
            var go = new GameObject("NoStaminaText_Popup");
            go.transform.position = spawnPos + new Vector3(0f, 1.4f, 0f);

            var tm = go.AddComponent<TextMesh>();
            tm.text = "НЕТ СТАМИНЫ!";
            tm.fontSize = 46;
            tm.characterSize = 0.088f;
            tm.alignment = TextAlignment.Center;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.fontStyle = FontStyle.Bold;
            tm.color = new Color(1f, 0.45f, 0.1f, 1f); // Яркий янтарно-оранжевый

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 76;

            float duration = 1.15f;
            float elapsed = 0f;
            Vector3 startPos = go.transform.position;
            Vector3 endPos = startPos + new Vector3(0f, 1.1f, 0f);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;

                if (go != null)
                {
                    go.transform.position = Vector3.Lerp(startPos, endPos, t);
                    Color c = tm.color;
                    c.a = Mathf.Clamp01(1f - (t * t));
                    tm.color = c;
                }
                yield return null;
            }

            if (go != null) Destroy(go);
        }
    }
}
