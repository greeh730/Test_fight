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

        private void Awake()
        {
            CreateHitboxObjects();
            CreateAlertObjects();
        }

        private void CreateHitboxObjects()
        {
            if (_hitboxObj != null) return;

            _hitboxObj = new GameObject("Enemy_Hitbox_Visual");
            _hitboxObj.transform.SetParent(transform, false);

            _fillRenderer = _hitboxObj.AddComponent<SpriteRenderer>();
            _fillRenderer.sprite = Combat.Common.CombatSprites.WhiteBox;
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
            Combat.Common.CombatFloatingText.ShowCounterAttack(position);
        }

        /// <summary>
        /// Создает парящий текст "ВРАГ ПОВЕРЖЕН!" в мировом пространстве
        /// </summary>
        public void ShowDefeatPopup(Vector3 position)
        {
            Combat.Common.CombatFloatingText.ShowDefeat(position);
        }

        /// <summary>
        /// Создает парящий текст "НЕТ СТАМИНЫ!" при исчерпании шкалы выносливости
        /// </summary>
        public void ShowNoStaminaPopup(Vector3 position)
        {
            Combat.Common.CombatFloatingText.ShowNoStamina(position);
        }

        /// <summary>
        /// Универсальный метод для создания парящего текста статуса над персонажем
        /// </summary>
        public void SpawnCustomPopup(Vector3 position, string text, Color color, float duration = 1.15f)
        {
            Combat.Common.CombatFloatingText.Spawn(position, text, color, duration);
        }
    }
}
