using System;
using UnityEngine;
using Combat;
using Fencing;

namespace Combat.Player
{
    using AttackDirection = Fencing.AttackDirection;
    /// <summary>
    /// Контроллер спрайтовой анимации героя (Evo) для Abyss of Emotions.
    /// Управляет состояниями героя:
    /// - Idle (покой на земле)
    /// - Walk (ходьба по земле)
    /// - Run (быстрый бег с зажатым Shift)
    /// - Jump (прыжок и полет в воздухе)
    /// - Damage (получение урона / оглушение)
    /// - Attack_High (удар вверх / Launcher)
    /// - Attack_Mid (выпад и пронзающий удар вперед)
    /// - Attack_Low (подсечка по ногам вниз)
    /// - Attack_Combo1 (базовая серия ударов)
    /// Поддерживает прямое управление от новой фехтовальной системы (FencingPlayer).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlayerAnimationController2D : MonoBehaviour
    {
        [Header("--- Animator Settings ---")]
        [SerializeField] private Animator animator;
        [SerializeField] private RuntimeAnimatorController animatorControllerAsset;

        [Header("--- Reaction Durations ---")]
        [Tooltip("Длительность проигрывания анимации урона при получении удара")]
        [SerializeField] private float damageDuration = 0.35f;

        [Header("--- References ---")]
        [SerializeField] private PlayerController2D playerController;
        [SerializeField] private PlayerHealth2D playerHealth;

        [Header("--- Visual Elements ---")]
        [Tooltip("Маркер лица")]
        [SerializeField] private Transform faceTransform;

        // Внутреннее состояние аниматора
        private SpriteRenderer _sr;
        private string _currentState = "";
        private string _currentAttackState = "Attack_Mid";
        private float _damageTimer = 0f;
        private float _attackTimer = 0f;

        public string CurrentState => _currentState;
        public string CurrentAttackState => _currentAttackState;
        public bool IsAttacking => _attackTimer > 0f;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();

            if (animator == null)
            {
                animator = GetComponent<Animator>();
                if (animator == null) animator = gameObject.AddComponent<Animator>();
            }

            if (animatorControllerAsset == null)
            {
#if UNITY_EDITOR
                animatorControllerAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Sprites/Evo/Evo_AnimatorController.controller");
                if (animatorControllerAsset == null)
                {
                    animatorControllerAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Sprites/Other/Player_AnimatorController.controller");
                }
#endif
            }

            if (animatorControllerAsset != null && (animator.runtimeAnimatorController == null || animator.runtimeAnimatorController != animatorControllerAsset))
            {
                animator.runtimeAnimatorController = animatorControllerAsset;
            }

            if (playerController == null) playerController = GetComponent<PlayerController2D>();
            if (playerHealth == null) playerHealth = GetComponent<PlayerHealth2D>();
        }

        private void OnEnable()
        {
            if (playerHealth != null)
            {
                playerHealth.onDamaged.AddListener(HandleDamageTaken);
                playerHealth.onDeath.AddListener(HandleDeath);
                playerHealth.onRespawn.AddListener(HandleRespawn);
            }
        }

        private void OnDisable()
        {
            if (playerHealth != null)
            {
                playerHealth.onDamaged.RemoveListener(HandleDamageTaken);
                playerHealth.onDeath.RemoveListener(HandleDeath);
                playerHealth.onRespawn.RemoveListener(HandleRespawn);
            }
        }

        private float GetAttackClipLength(string stateName)
        {
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                foreach (var clip in animator.runtimeAnimatorController.animationClips)
                {
                    if (clip.name == stateName || clip.name == "Evo_" + stateName)
                    {
                        return clip.length;
                    }
                }
            }
            return 0.785f;
        }

        private void Start()
        {
            PlayState("Idle");
        }

        private void Update()
        {
            UpdateAnimationState();
        }

        private void UpdateAnimationState()
        {
            // 0. СМЕРТЬ ИГРОКА
            if (playerHealth != null && playerHealth.IsDead)
            {
                PlayState("Damage");
                if (animator != null) animator.speed = 0f;
                return;
            }

            // 1. ПОЛУЧЕНИЕ УРОНА (HURT / HITSTUN)
            if (_damageTimer > 0f)
            {
                _damageTimer -= Time.deltaTime;
                _attackTimer = 0f;
                PlayState("Damage");
                if (animator != null) animator.speed = 1.0f;
                return;
            }

            // 2. ВЫПОЛНЕНИЕ АТАКИ (ФЕХТОВАЛЬНЫЙ ВЫПАД)
            if (_attackTimer > 0f)
            {
                _attackTimer -= Time.deltaTime;
                if (_currentState != _currentAttackState)
                {
                    PlayState(_currentAttackState);
                    if (animator != null) animator.speed = 1.35f;
                }
                return;
            }

            // 3. ПРОВЕРКА ВОЗДУХА / ПРЫЖКА
            if (playerController != null && !playerController.IsGrounded)
            {
                if (_currentState != "Jump")
                {
                    PlayState("Jump");
                }
                if (animator != null) animator.speed = 1.0f;
                return;
            }

            // 4. ДВИЖЕНИЕ ИЛИ ПОКОЙ НА ЗЕМЛЕ (RUN / WALK / IDLE)
            bool isMoving = playerController != null && (playerController.HasMoveInput || Mathf.Abs(playerController.Velocity.x) > 0.2f);
            if (isMoving)
            {
                if (playerController != null && playerController.IsSprinting)
                {
                    PlayState("Run");
                    float speedX = Mathf.Abs(playerController.Velocity.x);
                    if (animator != null) animator.speed = Mathf.Clamp(speedX / 7.5f, 0.8f, 1.8f);
                }
                else
                {
                    PlayState("Walk");
                    float speedX = playerController != null ? Mathf.Abs(playerController.Velocity.x) : 5f;
                    if (animator != null) animator.speed = Mathf.Clamp(speedX / 4.8f, 0.8f, 1.6f);
                }
            }
            else
            {
                PlayState("Idle");
                if (animator != null) animator.speed = 1.0f;
            }
        }

        /// <summary>
        /// Воспроизводит анимацию удара согласно фехтовальному направлению.
        /// </summary>
        public void PlayAttackAnimation(AttackDirection direction)
        {
            string animState = "Attack_Mid";

            switch (direction)
            {
                case AttackDirection.Up:
                case AttackDirection.UpRight:
                case AttackDirection.UpLeft:
                    animState = "Attack_High";
                    break;

                case AttackDirection.Down:
                case AttackDirection.DownRight:
                case AttackDirection.DownLeft:
                    animState = "Attack_Low";
                    break;

                case AttackDirection.Right:
                case AttackDirection.Left:
                case AttackDirection.Center:
                default:
                    animState = "Attack_Mid";
                    break;
            }

            _currentAttackState = animState;
            float clipLen = GetAttackClipLength(animState);
            _attackTimer = Mathf.Clamp(clipLen * 0.45f, 0.18f, 0.45f);

            PlayState(animState);
            if (animator != null)
            {
                animator.speed = 1.35f;
                animator.Play(animState, 0, 0f);
            }
        }

        private void HandleDamageTaken()
        {
            _damageTimer = damageDuration;
            _attackTimer = 0f;
            PlayState("Damage");
        }

        private void HandleDeath()
        {
            _attackTimer = 0f;
            PlayState("Damage");
            if (animator != null) animator.speed = 0f;
        }

        private void HandleRespawn()
        {
            _damageTimer = 0f;
            _attackTimer = 0f;
            PlayState("Idle");
        }

        private void PlayState(string newState)
        {
            if (_currentState == newState) return;

            _currentState = newState;
            if (animator != null)
            {
                animator.speed = 1f;
                animator.Play(newState, 0, 0f);
            }
        }
    }
}
