using System.Collections;
using UnityEngine;

namespace Combat
{
    [DisallowMultipleComponent]
    public class EnemyStaminaBar2D : MonoBehaviour
    {
        [Header("--- Position & Dimensions ---")]
        [Tooltip("Локальное смещение полоски стамины относительно врага")]
        [SerializeField] private Vector3 barOffset = new Vector3(0f, 1.25f, 0f);
        [SerializeField] private float barWidth = 1.35f;
        [SerializeField] private float barHeight = 0.13f;

        [Header("--- Colors ---")]
        [SerializeField] private Color fullStaminaColor = new Color(1f, 0.88f, 0.2f, 1f);   // Золотисто-желтый
        [SerializeField] private Color midStaminaColor = new Color(1f, 0.58f, 0.1f, 1f);   // Янтарно-оранжевый
        [SerializeField] private Color lowStaminaColor = new Color(0.95f, 0.18f, 0.15f, 1f); // Насыщенный красный
        [SerializeField] private Color ghostBarColor = new Color(1f, 0.95f, 0.8f, 0.8f);    // Бело-золотой шлейф
        [SerializeField] private Color backgroundColor = new Color(0.12f, 0.12f, 0.15f, 0.9f);
        [SerializeField] private Color borderColor = new Color(0.35f, 0.35f, 0.4f, 0.9f);

        private GameObject _rootObj;
        private SpriteRenderer _bgRenderer;
        private SpriteRenderer _ghostRenderer;
        private SpriteRenderer _fillRenderer;
        private LineRenderer _borderRenderer;
        private TextMesh _labelText;

        private float _maxStamina = 100f;
        private float _targetStamina = 100f;
        private float _currentDisplayedStamina = 100f;
        private float _ghostStamina = 100f;
        private float _ghostCatchupTimer;
        private bool _isInitialized;

        private void Awake()
        {
            BuildBarObjects();
        }

        private void BuildBarObjects()
        {
            if (_isInitialized && _rootObj != null) return;

            // Корневой объект шкалы
            _rootObj = new GameObject("Enemy_StaminaBar_Visual");
            _rootObj.transform.SetParent(transform, false);
            _rootObj.transform.localPosition = barOffset;

            // 1. Фон (Background)
            var bgObj = new GameObject("Bar_BG");
            bgObj.transform.SetParent(_rootObj.transform, false);
            _bgRenderer = bgObj.AddComponent<SpriteRenderer>();
            _bgRenderer.sprite = Combat.Common.CombatSprites.WhiteBox;
            _bgRenderer.color = backgroundColor;
            _bgRenderer.sortingOrder = 48;
            bgObj.transform.localScale = new Vector3(barWidth + 0.06f, barHeight + 0.05f, 1f);

            // 2. Ghost-бар (шлейф урона)
            var ghostObj = new GameObject("Bar_Ghost");
            ghostObj.transform.SetParent(_rootObj.transform, false);
            _ghostRenderer = ghostObj.AddComponent<SpriteRenderer>();
            _ghostRenderer.sprite = Combat.Common.CombatSprites.WhiteBox;
            _ghostRenderer.color = ghostBarColor;
            _ghostRenderer.sortingOrder = 49;

            // 3. Основная полоска заполнения (Fill)
            var fillObj = new GameObject("Bar_Fill");
            fillObj.transform.SetParent(_rootObj.transform, false);
            _fillRenderer = fillObj.AddComponent<SpriteRenderer>();
            _fillRenderer.sprite = Combat.Common.CombatSprites.WhiteBox;
            _fillRenderer.color = fullStaminaColor;
            _fillRenderer.sortingOrder = 50;

            // 4. Контур / Рамка (Border)
            var borderObj = new GameObject("Bar_Border");
            borderObj.transform.SetParent(_rootObj.transform, false);
            _borderRenderer = borderObj.AddComponent<LineRenderer>();
            _borderRenderer.positionCount = 5;
            _borderRenderer.useWorldSpace = false;
            _borderRenderer.loop = true;
            _borderRenderer.startWidth = 0.022f;
            _borderRenderer.endWidth = 0.022f;
            _borderRenderer.sortingOrder = 51;
            _borderRenderer.material = new Material(Shader.Find("Sprites/Default"));
            _borderRenderer.startColor = borderColor;
            _borderRenderer.endColor = borderColor;

            float hw = (barWidth + 0.06f) * 0.5f;
            float hh = (barHeight + 0.05f) * 0.5f;
            _borderRenderer.SetPositions(new Vector3[]
            {
                new Vector3(-hw, -hh, 0f),
                new Vector3(hw, -hh, 0f),
                new Vector3(hw, hh, 0f),
                new Vector3(-hw, hh, 0f),
                new Vector3(-hw, -hh, 0f)
            });

            // 5. Текстовая подпись "СТАМИНА"
            var labelObj = new GameObject("Bar_Label");
            labelObj.transform.SetParent(_rootObj.transform, false);
            labelObj.transform.localPosition = new Vector3(0f, (barHeight * 0.5f) + 0.11f, 0f);

            _labelText = labelObj.AddComponent<TextMesh>();
            _labelText.text = "СТАМИНА";
            _labelText.fontSize = 24;
            _labelText.characterSize = 0.046f;
            _labelText.alignment = TextAlignment.Center;
            _labelText.anchor = TextAnchor.MiddleCenter;
            _labelText.fontStyle = FontStyle.Bold;
            _labelText.color = new Color(0.92f, 0.92f, 0.95f, 0.88f);

            var mr = labelObj.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 52;

            _isInitialized = true;
            UpdateVisualFill(1f, 1f);
        }

        public void Initialize(float maxStamina)
        {
            _maxStamina = Mathf.Max(1f, maxStamina);
            _targetStamina = _maxStamina;
            _currentDisplayedStamina = _maxStamina;
            _ghostStamina = _maxStamina;
            if (!_isInitialized) BuildBarObjects();
            UpdateVisualFill(1f, 1f);
        }

        public void SetStamina(float current, float max)
        {
            if (!_isInitialized) BuildBarObjects();

            _maxStamina = Mathf.Max(1f, max);
            float prevTarget = _targetStamina;
            _targetStamina = Mathf.Clamp(current, 0f, _maxStamina);

            if (_targetStamina < prevTarget)
            {
                // Получен урон по стамине: запускаем паузу перед тем, как ghost-бар начнет догонять
                _ghostCatchupTimer = 0.25f;
            }
            else if (_targetStamina > _ghostStamina)
            {
                _ghostStamina = _targetStamina;
            }
        }

        public void SetVisible(bool visible)
        {
            if (_rootObj != null)
            {
                _rootObj.SetActive(visible);
            }
        }

        private void Update()
        {
            if (!_isInitialized || _rootObj == null || !_rootObj.activeSelf) return;

            // Плавное следование отображаемой стамины
            _currentDisplayedStamina = Mathf.MoveTowards(_currentDisplayedStamina, _targetStamina, Time.deltaTime * _maxStamina * 4f);

            // Ghost-бар догоняет с небольшой задержкой
            if (_ghostCatchupTimer > 0f)
            {
                _ghostCatchupTimer -= Time.deltaTime;
            }
            else
            {
                _ghostStamina = Mathf.MoveTowards(_ghostStamina, _targetStamina, Time.deltaTime * _maxStamina * 1.5f);
            }

            float currentRatio = Mathf.Clamp01(_currentDisplayedStamina / _maxStamina);
            float ghostRatio = Mathf.Clamp01(_ghostStamina / _maxStamina);

            UpdateVisualFill(currentRatio, ghostRatio);
        }

        private void LateUpdate()
        {
            if (!_isInitialized || _rootObj == null) return;

            // Компенсируем поворот и масштаб родителя, чтобы шкала и текст никогда не зеркалились
            Vector3 lossy = transform.lossyScale;
            float signX = Mathf.Sign(lossy.x != 0f ? lossy.x : 1f);
            float signY = Mathf.Sign(lossy.y != 0f ? lossy.y : 1f);

            _rootObj.transform.localScale = new Vector3(signX * 1f, signY * 1f, 1f);
            _rootObj.transform.localPosition = barOffset;
        }

        private void UpdateVisualFill(float currentRatio, float ghostRatio)
        {
            if (_fillRenderer == null || _ghostRenderer == null) return;

            // Расчет позиции и ширины основного бара (прижато к левому краю)
            float leftEdge = -barWidth * 0.5f;

            float fillW = barWidth * currentRatio;
            _fillRenderer.transform.localPosition = new Vector3(leftEdge + (fillW * 0.5f), 0f, 0f);
            _fillRenderer.transform.localScale = new Vector3(Mathf.Max(0.001f, fillW), barHeight, 1f);

            // Расчет цвета (Золотой -> Оранжевый -> Красный)
            Color fillColor;
            if (currentRatio > 0.5f)
            {
                float t = (currentRatio - 0.5f) * 2f;
                fillColor = Color.Lerp(midStaminaColor, fullStaminaColor, t);
            }
            else
            {
                float t = currentRatio * 2f;
                fillColor = Color.Lerp(lowStaminaColor, midStaminaColor, t);
            }
            _fillRenderer.color = fillColor;

            // Расчет Ghost-бара
            float ghostW = barWidth * ghostRatio;
            _ghostRenderer.transform.localPosition = new Vector3(leftEdge + (ghostW * 0.5f), 0f, 0f);
            _ghostRenderer.transform.localScale = new Vector3(Mathf.Max(0.001f, ghostW), barHeight, 1f);
        }

        private void OnDestroy()
        {
            if (_rootObj != null)
            {
                Destroy(_rootObj);
            }
        }
    }
}
