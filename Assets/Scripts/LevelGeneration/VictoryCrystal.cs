using System.Collections;
using UnityEngine;
using Combat.Player;

namespace LevelGeneration
{
    /// <summary>
    /// Компонент кристалла победы в финальной секции уровня.
    /// При касании игроком воспроизводит эффект триумфа и запускает
    /// процедурную перегенерацию уровня со спавном на старте.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public class VictoryCrystal : MonoBehaviour
    {
        [Header("--- Настройки срабатывания ---")]
        [Tooltip("Задержка перед перегенерацией уровня для ощущения триумфа (сек)")]
        [SerializeField] private float transitionDelay = 0.25f;

        [Header("--- Визуальная анимация (парение) ---")]
        [SerializeField] private bool enableHoverAnimation = true;
        [SerializeField] private float hoverSpeed = 3.0f;
        [SerializeField] private float hoverHeight = 0.15f;
        [SerializeField] private float pulseScale = 0.06f;

        [Header("--- Цвета эффекта ---")]
        [SerializeField] private Color activeColor = new Color(0.30f, 0.90f, 1.0f, 0.95f);
        [SerializeField] private Color triggerColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);

        private Vector3 _initialLocalPos;
        private Vector3 _initialLocalScale;
        private SpriteRenderer _sr;
        private Collider2D _col;
        private bool _isTriggered = false;
        private float _timeOffset;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _col = GetComponent<Collider2D>();
            if (_col != null)
            {
                _col.isTrigger = true;
            }

            _initialLocalPos = transform.localPosition;
            _initialLocalScale = transform.localScale;
            _timeOffset = Random.Range(0f, 10f);
        }

        private void Update()
        {
            if (_isTriggered || !enableHoverAnimation) return;

            // Плавное парение вверх-вниз
            float offsetY = Mathf.Sin((Time.time + _timeOffset) * hoverSpeed) * hoverHeight;
            transform.localPosition = _initialLocalPos + new Vector3(0f, offsetY, 0f);

            // Легкая пульсация дыхания кристалла
            float scaleMod = 1.0f + Mathf.Sin((Time.time + _timeOffset) * (hoverSpeed * 1.5f)) * pulseScale;
            transform.localScale = new Vector3(_initialLocalScale.x * scaleMod, _initialLocalScale.y * scaleMod, _initialLocalScale.z);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_isTriggered) return;

            // Проверяем, коснулся ли игрок
            bool isPlayer = other.CompareTag("Player") ||
                            other.GetComponentInParent<PlayerController2D>() != null ||
                            other.gameObject.name.IndexOf("Player", System.StringComparison.OrdinalIgnoreCase) >= 0;

            if (isPlayer)
            {
                if (Application.isPlaying && transitionDelay > 0f)
                {
                    StartCoroutine(TriggerVictoryRoutine());
                }
                else
                {
                    ExecuteVictoryRegeneration();
                }
            }
        }

        private IEnumerator TriggerVictoryRoutine()
        {
            _isTriggered = true;

            Debug.Log("<color=#00FFAA><b>[VICTORY]</b></color> Игрок коснулся Кристалла Победы! Уровень пройден, перегенерация со спавна...");

            // Вспышка и расширение кристалла при касании
            if (_sr != null)
            {
                _sr.color = triggerColor;
            }

            float elapsed = 0f;
            Vector3 startScale = transform.localScale;
            Vector3 popScale = _initialLocalScale * 1.45f;

            while (elapsed < transitionDelay)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / transitionDelay);
                transform.localScale = Vector3.Lerp(startScale, popScale, t);
                yield return null;
            }

            ExecuteVictoryRegeneration();
        }

        public void ExecuteVictoryRegeneration()
        {
            _isTriggered = true;

            // Перегенерация уровня через LevelSequenceGenerator
            var generator = LevelSequenceGenerator.Instance;
            if (generator == null)
            {
                generator = Object.FindObjectOfType<LevelSequenceGenerator>();
            }

            if (generator != null)
            {
                generator.GenerateLevel();
            }
            else
            {
                Debug.LogWarning("[VictoryCrystal] LevelSequenceGenerator не найден на сцене!");
            }
        }
    }
}
