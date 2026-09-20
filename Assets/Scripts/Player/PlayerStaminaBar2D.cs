using System.Collections;
using UnityEngine;
using Combat.Common;

namespace Combat.Player
{
    /// <summary>
    /// Визуальная шкала выносливости игрока.
    /// Отображает текущий уровень стамины, ghost-шлейф расхода,
    /// а при истощении мигает красным с надписью процента восстановления.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerStamina2D))]
    public class PlayerStaminaBar2D : MonoBehaviour
    {
        [Header("--- Position & Dimensions ---")]
        [Tooltip("Смещение полоски относительно игрока")]
        [SerializeField] private Vector3 barOffset = new Vector3(0f, 1.35f, 0f);
        [SerializeField] private float barWidth = 1.35f;
        [SerializeField] private float barHeight = 0.12f;

        [Header("--- Colors ---")]
        [SerializeField] private Color normalStaminaColor = new Color(0.2f, 0.9f, 1f, 1f);       // Кибер-бирюзовый
        [SerializeField] private Color lowStaminaColor = new Color(1f, 0.65f, 0.15f, 1f);        // Предупреждающий оранжевый
        [SerializeField] private Color exhaustedStaminaColor = new Color(0.95f, 0.2f, 0.2f, 1f);  // Красный истощения
        [SerializeField] private Color ghostBarColor = new Color(1f, 1f, 1f, 0.75f);
        [SerializeField] private Color backgroundColor = new Color(0.08f, 0.1f, 0.14f, 0.92f);
        [SerializeField] private Color borderColor = new Color(0.25f, 0.35f, 0.45f, 0.9f);

        private PlayerStamina2D _stamina;
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
            _stamina = GetComponent<PlayerStamina2D>();
            BuildBarObjects();
        }

        private void Start()
        {
            if (_stamina != null)
            {
                _maxStamina = _stamina.MaxStamina;
                _targetStamina = _stamina.CurrentStamina;
                _currentDisplayedStamina = _targetStamina;
                _ghostStamina = _targetStamina;
            }
        }

        private void BuildBarObjects()
        {
            if (_isInitialized && _rootObj != null) return;

            _rootObj = new GameObject("Player_StaminaBar_Visual");
            _rootObj.transform.SetParent(transform, false);
            _rootObj.transform.localPosition = barOffset;

            // 1. Фон
            var bgObj = new GameObject("Bar_BG");
            bgObj.transform.SetParent(_rootObj.transform, false);
            _bgRenderer = bgObj.AddComponent<SpriteRenderer>();
            _bgRenderer.sprite = CombatSprites.WhiteBox;
            _bgRenderer.color = backgroundColor;
            _bgRenderer.sortingOrder = 48;
            bgObj.transform.localScale = new Vector3(barWidth + 0.06f, barHeight + 0.05f, 1f);

            // 2. Ghost-бар
            var ghostObj = new GameObject("Bar_Ghost");
            ghostObj.transform.SetParent(_rootObj.transform, false);
            _ghostRenderer = ghostObj.AddComponent<SpriteRenderer>();
            _ghostRenderer.sprite = CombatSprites.WhiteBox;
            _ghostRenderer.color = ghostBarColor;
            _ghostRenderer.sortingOrder = 49;

            // 3. Заполнение (Fill)
            var fillObj = new GameObject("Bar_Fill");
            fillObj.transform.SetParent(_rootObj.transform, false);
            _fillRenderer = fillObj.AddComponent<SpriteRenderer>();
            _fillRenderer.sprite = CombatSprites.WhiteBox;
            _fillRenderer.color = normalStaminaColor;
            _fillRenderer.sortingOrder = 50;

            // 4. Контур
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

            // 5. Текст статуса
            var labelObj = new GameObject("Bar_Label");
            labelObj.transform.SetParent(_rootObj.transform, false);
            labelObj.transform.localPosition = new Vector3(0f, (barHeight * 0.5f) + 0.11f, 0f);

            _labelText = labelObj.AddComponent<TextMesh>();
            _labelText.text = "ВЫНОСЛИВОСТЬ";
            _labelText.fontSize = 24;
            _labelText.characterSize = 0.044f;
            _labelText.alignment = TextAlignment.Center;
            _labelText.anchor = TextAnchor.MiddleCenter;
            _labelText.fontStyle = FontStyle.Bold;
            _labelText.color = new Color(0.9f, 0.95f, 1f, 0.9f);

            var mr = labelObj.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 52;

            _isInitialized = true;
            UpdateVisualFill(1f, 1f);
        }

        private void Update()
        {
            if (!_isInitialized || _stamina == null) return;

            _maxStamina = _stamina.MaxStamina;
            float target = _stamina.CurrentStamina;

            if (target < _targetStamina)
            {
                _ghostCatchupTimer = 0.25f;
            }
            else if (target > _ghostStamina)
            {
                _ghostStamina = target;
            }

            _targetStamina = target;

            // Плавное движение
            _currentDisplayedStamina = Mathf.MoveTowards(_currentDisplayedStamina, _targetStamina, Time.deltaTime * _maxStamina * 4f);

            if (_ghostCatchupTimer > 0f)
            {
                _ghostCatchupTimer -= Time.deltaTime;
            }
            else
            {
                _ghostStamina = Mathf.MoveTowards(_ghostStamina, _targetStamina, Time.deltaTime * _maxStamina * 1.6f);
            }

            float currentRatio = Mathf.Clamp01(_currentDisplayedStamina / _maxStamina);
            float ghostRatio = Mathf.Clamp01(_ghostStamina / _maxStamina);

            UpdateVisualFill(currentRatio, ghostRatio);
        }

        private void LateUpdate()
        {
            if (!_isInitialized || _rootObj == null) return;

            // Компенсируем поворот персонажа, чтобы шкала не переворачивалась
            Vector3 lossy = transform.lossyScale;
            float signX = Mathf.Sign(lossy.x != 0f ? lossy.x : 1f);
            float signY = Mathf.Sign(lossy.y != 0f ? lossy.y : 1f);

            _rootObj.transform.localScale = new Vector3(signX * 1f, signY * 1f, 1f);
            _rootObj.transform.localPosition = barOffset;
        }

        private void UpdateVisualFill(float currentRatio, float ghostRatio)
        {
            if (_fillRenderer == null || _ghostRenderer == null) return;

            float leftEdge = -barWidth * 0.5f;

            float fillW = barWidth * currentRatio;
            _fillRenderer.transform.localPosition = new Vector3(leftEdge + (fillW * 0.5f), 0f, 0f);
            _fillRenderer.transform.localScale = new Vector3(Mathf.Max(0.001f, fillW), barHeight, 1f);

            float ghostW = barWidth * ghostRatio;
            _ghostRenderer.transform.localPosition = new Vector3(leftEdge + (ghostW * 0.5f), 0f, 0f);
            _ghostRenderer.transform.localScale = new Vector3(Mathf.Max(0.001f, ghostW), barHeight, 1f);

            // Обработка состояния истощения
            if (_stamina != null && _stamina.IsExhausted)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 8f);
                _fillRenderer.color = Color.Lerp(exhaustedStaminaColor, lowStaminaColor, pulse);

                int pct = Mathf.RoundToInt(currentRatio * 100f);
                if (_labelText != null)
                {
                    _labelText.text = $"[ИСТОЩЕНИЕ {pct}%]";
                    _labelText.color = new Color(1f, 0.35f, 0.25f, 1f);
                }
            }
            else
            {
                Color c = Color.Lerp(lowStaminaColor, normalStaminaColor, currentRatio);
                _fillRenderer.color = c;

                if (_labelText != null)
                {
                    _labelText.text = "ВЫНОСЛИВОСТЬ";
                    _labelText.color = new Color(0.9f, 0.95f, 1f, 0.9f);
                }
            }
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
