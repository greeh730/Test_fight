using System;
using UnityEngine;
using Combat;

namespace Combat.Tutorial
{
    /// <summary>
    /// Специальный контроллер скелета Вовы для режима обучения.
    /// - До диалога стоит мирно в центре комнаты.
    /// - После диалога нападает на игрока.
    /// - Бессмертен к обычным ударам мечом (здоровье не падает ниже 10 HP).
    /// - Добивается только через режим фокуса (летальный выпад).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyAIController2D))]
    public class TutorialVovaController : MonoBehaviour
    {
        public static TutorialVovaController Instance { get; private set; }

        public event Action OnVovaDefeated;

        [Header("--- State ---")]
        [SerializeField] private bool isCombatActive = false;
        [SerializeField] private bool isDefeated = false;

        private EnemyAIController2D _ai;
        private Rigidbody2D _rb;

        public bool IsCombatActive => isCombatActive;
        public bool IsDefeated => isDefeated;
        public EnemyAIController2D AI => _ai;
        public bool IsStunned => _ai != null && _ai.CurrentState == EnemyState.Stunned;

        private void Awake()
        {
            Instance = this;
            _ai = GetComponent<EnemyAIController2D>();
            _rb = GetComponent<Rigidbody2D>();

            if (_ai != null)
            {
                _ai.CanDie = false; // Бессмертен от обычных атак
            }
        }

        private void Start()
        {
            SetPassiveMode();
        }

        private void OnEnable()
        {
            EnemyAIController2D.OnAnyEnemyDied += HandleAnyEnemyDied;
        }

        private void OnDisable()
        {
            EnemyAIController2D.OnAnyEnemyDied -= HandleAnyEnemyDied;
        }

        /// <summary>
        /// Мирный режим: Вова стоит на месте и не агрится на игрока до окончания диалога
        /// </summary>
        public void SetPassiveMode()
        {
            isCombatActive = false;
            if (_ai != null)
            {
                _ai.enabled = false;
            }
            if (_rb != null)
            {
                _rb.linearVelocity = Vector2.zero;
            }
        }

        /// <summary>
        /// Активирует боевой режим: Вова нападает на игрока
        /// </summary>
        public void ActivateCombat()
        {
            if (isDefeated) return;

            isCombatActive = true;
            if (_ai != null)
            {
                _ai.enabled = true;
                _ai.CanDie = false;
                _ai.StaminaBreakStunDuration = 8.0f; // 8 секунд на подготовку и выполнение добивания
            }

            Debug.Log("<color=#00FFAA>[VOVA]</color> Вова активирован и вступил в бой с игроком!");
        }

        private void Update()
        {
            if (isDefeated || _ai == null) return;

            // Если бой еще не начался, держим Вову на месте
            if (!isCombatActive)
            {
                if (_rb != null) _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                return;
            }

            // Защита от гибели от обычных ударов мечом:
            // Если здоровье опустилось до 15 HP — принудительно оглушаем Вову для фокусного добивания!
            if (_ai.CurrentHealth <= 15.5f && !_ai.IsDead)
            {
                if (_ai.CurrentState != EnemyState.Stunned)
                {
                    _ai.DrainStamina(999f);
                    _ai.TriggerStaminaBreak();
                }
                _ai.SendMessage("TakeDirectDamage", -10f, SendMessageOptions.DontRequireReceiver);
            }
        }

        /// <summary>
        /// Вызывается при получении летального удара в режиме фокуса
        /// </summary>
        public void ExecuteFocusFinisher()
        {
            if (isDefeated) return;

            isDefeated = true;
            isCombatActive = false;

            if (_ai != null)
            {
                _ai.CanDie = true;
                _ai.Die(Vector2.right, true);
            }

            OnVovaDefeated?.Invoke();
            Debug.Log("<color=#FFD700><b>[VOVA FINISHED]</b></color> Вова добит сфокусированным ударом!");
        }

        private void HandleAnyEnemyDied(EnemyAIController2D deadEnemy)
        {
            if (deadEnemy == _ai && !isDefeated)
            {
                isDefeated = true;
                isCombatActive = false;
                OnVovaDefeated?.Invoke();
            }
        }
    }
}
