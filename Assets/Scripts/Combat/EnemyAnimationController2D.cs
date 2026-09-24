using System;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Контроллер спрайтовой анимации врага (Вова) для Abyss of Emotions.
    /// Синхронизирует все анимации врага с его действиями и поведением ИИ:
    /// - Idle (покой на земле)
    /// - Walk (преследование / перемещение с динамической скоростью под бег)
    /// - Jump (нахождение в воздухе / прыжки между платформами)
    /// - Mid_attack (прямой средний удар)
    /// - High_attack (удар вверх / Anti-Air)
    /// - Low_Attack (нижняя подсечка по ногам)
    /// - Hit (реакция на получение урона)
    /// - Stun (оглушение после парирования / истощения стамины)
    /// 
    /// Скорость фазы замаха (Windup) точно синхронизирована с реальной длительностью
    /// телеграфа (telegraphDuration), а активная фаза удара — с длительностью удара и восстановления.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public class EnemyAnimationController2D : MonoBehaviour
    {
        [Header("--- Animator Settings ---")]
        [SerializeField] private Animator animator;
        [SerializeField] private RuntimeAnimatorController animatorControllerAsset;

        [Header("--- References ---")]
        [SerializeField] private EnemyAIController2D enemyAI;
        [SerializeField] private Rigidbody2D rb;
        [SerializeField] private SpriteRenderer sr;

        // Временные метки кадров удара внутри "сырых" анимационных клипов:
        // Mid_attack.anim: длина 0.500s, кадр удара 3 (0048) на 0.250s
        private const float MidStrikeClipTime = 0.250f;
        private const float MidClipLength = 0.500f;

        // High_attack.anim: длина 0.5833s, кадр удара 4 (0018) на 0.3333s
        private const float HighStrikeClipTime = 0.3333f;
        private const float HighClipLength = 0.5833f;

        // Low_Attack.anim: длина 0.625s, кадр удара 3 (0026) на 0.375s
        private const float LowStrikeClipTime = 0.375f;
        private const float LowClipLength = 0.625f;

        // Внутреннее состояние
        private string _currentState = "";
        private bool _isAttacking = false;
        private bool _isHitReaction = false;
        private float _hitReactionTimer = 0f;

        private float _currentAttackStrikeTime = 0.25f;
        private float _currentAttackClipLength = 0.50f;
        private string _currentAttackClipName = "Mid_attack";

        public bool IsAttacking => _isAttacking;
        public string CurrentAnimationState => _currentState;

        private void OnValidate()
        {
            EnsureComponents();
        }

        private void Reset()
        {
            EnsureComponents();
        }

        public void EnsureComponents()
        {
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            if (rb == null) rb = GetComponent<Rigidbody2D>();
            if (enemyAI == null) enemyAI = GetComponent<EnemyAIController2D>();

            if (animator == null)
            {
                animator = GetComponent<Animator>();
                if (animator == null) animator = gameObject.AddComponent<Animator>();
            }

            if (animatorControllerAsset == null)
            {
#if UNITY_EDITOR
                animatorControllerAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/sprites/Vova/Enemy_AnimatorController.controller");
#endif
            }

            if (animator != null && animatorControllerAsset != null && (animator.runtimeAnimatorController == null || animator.runtimeAnimatorController != animatorControllerAsset))
            {
                animator.runtimeAnimatorController = animatorControllerAsset;
            }

            // Отключаем старый маркер лица Face, так как новый детальный спрайт персонажа уже содержит лицо
            var face = transform.Find("Face");
            if (face != null)
            {
                var faceSr = face.GetComponent<SpriteRenderer>();
                if (faceSr != null) faceSr.enabled = false;
                face.gameObject.SetActive(false);
            }

            // Сбрасываем цвет на чисто белый, чтобы детальные спрайты не красились в старый темно-красный цвет кубика
            if (sr != null)
            {
                sr.color = Color.white;
            }
        }

        private void Awake()
        {
            EnsureComponents();
        }

        private void Start()
        {
            if (enemyAI == null) enemyAI = GetComponent<EnemyAIController2D>();
            if (rb == null) rb = GetComponent<Rigidbody2D>();

            PlayState("Idle", 1f, force: true);
        }

        private void Update()
        {
            if (animator == null || enemyAI == null) return;

            // 1. Таймер реакции на получение урона (Hit)
            if (_isHitReaction)
            {
                _hitReactionTimer -= Time.deltaTime;
                if (_hitReactionTimer <= 0f)
                {
                    _isHitReaction = false;
                }
                else
                {
                    return; // Не перебиваем анимацию получения урона обычной ходьбой
                }
            }

            // 2. Во время атаки анимация управляется напрямую методами StartAttackAnimation / TriggerActiveStrike
            if (_isAttacking)
            {
                return;
            }

            // 3. Состояния передвижения и реакции ИИ
            if (enemyAI.CurrentState == EnemyState.Dead)
            {
                // Повержен
                if (_currentState != "Hit")
                {
                    PlayState("Hit", 0f);
                }
                return;
            }

            if (enemyAI.CurrentState == EnemyState.Stunned)
            {
                PlayState("Stun", 1.0f);
                return;
            }

            // В воздухе (прыжок на платформу, падение или отталкивание)
            if (!enemyAI.IsGrounded)
            {
                PlayState("Jump", 1.0f);
                return;
            }

            // На земле: движение (бег/ходьба) или покой (Idle)
            float speedX = rb != null ? Mathf.Abs(rb.linearVelocity.x) : 0f;
            if (speedX > 0.18f && enemyAI.CurrentState == EnemyState.Chasing)
            {
                // Скорость воспроизведения анимации шагов пропорциональна скорости движения бота
                float walkSpeed = Mathf.Clamp(speedX / 3.4f, 0.75f, 1.45f);
                PlayState("Walk", walkSpeed);
            }
            else
            {
                PlayState("Idle", 1.0f);
            }
        }

        /// <summary>
        /// Запускает анимацию замаха атаки с точной синхронизацией скорости под реальную длительность телеграфа.
        /// </summary>
        public void StartAttackAnimation(CombatZone zone, float telegraphDuration, float activeStrikeDuration, float recoveryDuration)
        {
            EnsureComponents();
            _isHitReaction = false;
            _isAttacking = true;

            switch (zone)
            {
                case CombatZone.High:
                    _currentAttackClipName = "High_attack";
                    _currentAttackStrikeTime = HighStrikeClipTime;
                    _currentAttackClipLength = HighClipLength;
                    break;

                case CombatZone.Low:
                    _currentAttackClipName = "Low_Attack";
                    _currentAttackStrikeTime = LowStrikeClipTime;
                    _currentAttackClipLength = LowClipLength;
                    break;

                case CombatZone.Mid:
                default:
                    _currentAttackClipName = "Mid_attack";
                    _currentAttackStrikeTime = MidStrikeClipTime;
                    _currentAttackClipLength = MidClipLength;
                    break;
            }

            if (animator == null) return;

            // Вычисляем скорость воспроизведения замаха:
            // Анимация должна дойти от кадра 0 до кадра удара ровно за telegraphDuration!
            float safeTelegraph = Mathf.Max(0.08f, telegraphDuration);
            float windupSpeed = _currentAttackStrikeTime / safeTelegraph;

            animator.speed = windupSpeed;
            animator.Play(_currentAttackClipName, 0, 0f);
            _currentState = _currentAttackClipName;
        }

        /// <summary>
        /// Переключает анимацию в активную фазу удара и последующего восстановления.
        /// Скорость воспроизведения удара и восстановления синхронизирована с активным окном удара.
        /// </summary>
        public void TriggerActiveStrike(CombatZone zone, float activeStrikeDuration, float recoveryDuration)
        {
            EnsureComponents();
            if (!_isAttacking || animator == null) return;

            float remainingClipTime = Mathf.Max(0.05f, _currentAttackClipLength - _currentAttackStrikeTime);
            float realRemainingDuration = Mathf.Max(0.08f, activeStrikeDuration + recoveryDuration);

            // Скорость воспроизведения второй половины клипа (удар + возврат оружия):
            float strikeSpeed = remainingClipTime / realRemainingDuration;

            animator.speed = strikeSpeed;
        }

        /// <summary>
        /// Завершает атаку после окончания восстановления.
        /// </summary>
        public void EndAttack()
        {
            _isAttacking = false;
        }

        /// <summary>
        /// Прерывает атаку (например, при парировании, сбивании с ног или получении урона).
        /// </summary>
        public void CancelAttack()
        {
            _isAttacking = false;
        }

        /// <summary>
        /// Воспроизводит анимацию получения урона.
        /// </summary>
        public void PlayHit(float duration = 0.25f)
        {
            EnsureComponents();
            _isAttacking = false;
            _isHitReaction = true;
            _hitReactionTimer = duration;

            if (animator == null) return;
            animator.speed = 1.35f;
            animator.Play("Hit", 0, 0f);
            _currentState = "Hit";
        }

        /// <summary>
        /// Воспроизводит анимацию оглушения.
        /// </summary>
        public void PlayStun()
        {
            EnsureComponents();
            _isAttacking = false;
            _isHitReaction = false;

            PlayState("Stun", 1.0f, force: true);
        }

        /// <summary>
        /// Воспроизводит анимацию гибели / поражения.
        /// </summary>
        public void PlayDeath()
        {
            EnsureComponents();
            _isAttacking = false;
            _isHitReaction = false;

            if (animator == null) return;
            animator.speed = 0.9f;
            animator.Play("Hit", 0, 0.4f);
            _currentState = "Dead";
        }

        /// <summary>
        /// Возвращает врага в состояние покоя Idle.
        /// </summary>
        public void PlayIdle()
        {
            EnsureComponents();
            _isAttacking = false;
            _isHitReaction = false;
            PlayState("Idle", 1.0f, force: true);
        }

        private void PlayState(string newState, float speed = 1.0f, bool force = false)
        {
            EnsureComponents();
            if (animator == null) return;

            if (_currentState == newState && !force)
            {
                animator.speed = speed;
                return;
            }

            _currentState = newState;
            animator.speed = speed;
            animator.Play(newState, 0, 0f);
        }
    }
}
