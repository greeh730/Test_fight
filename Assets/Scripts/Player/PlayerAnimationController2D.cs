using System;
using UnityEngine;
using Combat;
using Combat.UI;
using Combat.Stances;

namespace Combat.Player
{
    /// <summary>
    /// Контроллер спрайтовой анимации игрока для Abyss of Emotions.
    /// Управляет переключением состояний:
    /// - Idle (покой на земле)
    /// - Jump (прыжок и падение в воздухе)
    /// - Block (защитная стойка / прицеливание парирования на ПКМ)
    /// - Attack Wind-Up (замах при удержании направления на стрелке с фиксацией на кадре 10)
    /// - Attack Strike (активная фаза удара)
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlayerAnimationController2D : MonoBehaviour
    {
        [Header("--- Animator Settings ---")]
        [SerializeField] private Animator animator;
        [SerializeField] private RuntimeAnimatorController animatorControllerAsset;

        [Header("--- Attack Wind-Up Hold Settings ---")]
        [Tooltip("Кадр, на котором замирает анимация замаха при удержании стрелки (2-я картинка 0012 = кадр 2 при samples=12)")]
        [SerializeField] private int attackWindupHoldFrame = 2;

        [Tooltip("Частота кадров (samples) анимации атаки")]
        [SerializeField] private float attackFrameRate = 12f;

        [Tooltip("Скорость проигрывания до кадра замаха")]
        [SerializeField] private float windupSpeed = 2.5f;

        [Header("--- References ---")]
        [SerializeField] private PlayerController2D playerController;
        [SerializeField] private PlayerCombatController2D playerCombat;
        [SerializeField] private PlayerBlockAndParry2D playerBlockParry;
        [SerializeField] private VectorWheelController vectorWheel;

        [Header("--- Visual Elements ---")]
        [Tooltip("Старый белый маркер лица (отключается, так как спрайт уже содержит лицо)")]
        [SerializeField] private Transform faceTransform;

        // Внутреннее состояние аниматора
        private SpriteRenderer _sr;
        private string _currentState = "";
        private float _currentWindupTime = 0f;
        private bool _isHoldingWindup = false;
        private float _holdTargetTime = 0.8333f;
        private float _attackClipLength = 0.9167f;

        public bool IsHoldingWindup => _isHoldingWindup;

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
                animatorControllerAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Sprites/Player_AnimatorController.controller");
#endif
            }

            if (animatorControllerAsset != null && animator.runtimeAnimatorController == null)
            {
                animator.runtimeAnimatorController = animatorControllerAsset;
            }

            if (playerController == null) playerController = GetComponent<PlayerController2D>();
            if (playerCombat == null) playerCombat = GetComponent<PlayerCombatController2D>();
            if (playerBlockParry == null) playerBlockParry = GetComponent<PlayerBlockAndParry2D>();
            if (vectorWheel == null) vectorWheel = FindAnyObjectByType<VectorWheelController>();

            if (faceTransform == null) faceTransform = transform.Find("Face");
            if (faceTransform != null)
            {
                // Отключаем старый спрайт-квадрат лица, так как теперь есть детальный спрайт персонажа
                var faceSr = faceTransform.GetComponent<SpriteRenderer>();
                if (faceSr != null) faceSr.enabled = false;
            }

            CalculateClipTimings();
        }

        private void CalculateClipTimings()
        {
            _holdTargetTime = attackWindupHoldFrame / Mathf.Max(1f, attackFrameRate);

            if (animator != null && animator.runtimeAnimatorController != null)
            {
                foreach (var clip in animator.runtimeAnimatorController.animationClips)
                {
                    if (clip.name == "Attack")
                    {
                        _attackClipLength = clip.length;
                        break;
                    }
                }
            }
        }

        private void Start()
        {
            if (vectorWheel == null)
            {
                vectorWheel = FindAnyObjectByType<VectorWheelController>();
            }

            PlayState("Idle");
        }

        private void Update()
        {
            UpdateAnimationState();
        }

        private void UpdateAnimationState()
        {
            // 1. ПРОВЕРКА ЗАМАХА НА СТРЕЛКЕ (Vector Wheel LMB Drag & Hold)
            bool isAimingOnWheel = false;
            Direction8 aimDir = Direction8.None;

            if (vectorWheel != null && vectorWheel.IsDragging && vectorWheel.ActiveGestureButton == WheelGestureButton.LMB)
            {
                if (vectorWheel.CurrentDirection != Direction8.None)
                {
                    isAimingOnWheel = true;
                    aimDir = vectorWheel.CurrentDirection;
                }
            }

            // Поворот персонажа в сторону прицеливания стрелки
            if (isAimingOnWheel && aimDir != Direction8.None)
            {
                float aimSign = GetHorizontalSign(aimDir);
                if (Mathf.Abs(aimSign) > 0.01f && playerCombat != null)
                {
                    playerCombat.SetFacingDirection(aimSign);
                }
            }

            // Проверка фазы атаки контроллера боя
            bool isCombatActive = playerCombat != null && playerCombat.CurrentState != CombatState.Idle;
            bool isCombatStartup = playerCombat != null && playerCombat.CurrentState == CombatState.Startup;
            bool isCombatStriking = playerCombat != null && (playerCombat.CurrentState == CombatState.Active || playerCombat.CurrentState == CombatState.Recovery);

            // А) Режим удержания замаха (Игрок целится стрелкой ИЛИ идет фаза Startup)
            if (isAimingOnWheel || isCombatStartup)
            {
                HandleAttackWindupHold();
                return;
            }
            else
            {
                _isHoldingWindup = false;
            }

            // Б) Активная фаза удара (Выполняется атака клинком)
            if (isCombatStriking)
            {
                if (_isHoldingWindup)
                {
                    _isHoldingWindup = false;
                    // Подхватываем кадр замаха 2 (картинка 0012) и продолжаем удар вперед!
                    float startNormalized = Mathf.Clamp01(_holdTargetTime / _attackClipLength);
                    animator.Play("Attack", 0, startNormalized);
                }
                else if (_currentState != "Attack")
                {
                    PlayState("Attack");
                }
                animator.speed = 1.35f;
                return;
            }

            _isHoldingWindup = false;

            // 2. ПРОВЕРКА БЛОКА / ПАРИРОВАНИЯ (ПКМ)
            if (playerBlockParry != null && playerBlockParry.IsBlocking)
            {
                PlayState("Block");
                animator.speed = 1.0f;
                return;
            }

            // 3. ПРОВЕРКА ВОЗДУХА / ПРЫЖКА (не зацикливается, удерживает позу парения)
            if (playerController != null && !playerController.IsGrounded)
            {
                if (_currentState != "Jump")
                {
                    PlayState("Jump");
                }
                animator.speed = 1.0f;
                return;
            }

            // 4. ДВИЖЕНИЕ ИЛИ ПОКОЙ НА ЗЕМЛЕ (WALK / IDLE)
            bool isMoving = playerController != null && (playerController.HasMoveInput || Mathf.Abs(playerController.Velocity.x) > 0.2f);
            if (isMoving)
            {
                PlayState("Walk");
                float speedX = playerController != null ? Mathf.Abs(playerController.Velocity.x) : 5f;
                animator.speed = Mathf.Clamp(speedX / 4.8f, 0.8f, 1.6f);
            }
            else
            {
                PlayState("Idle");
                animator.speed = 1.0f;
            }
        }

        /// <summary>
        /// Управление замахом: анимация быстро доходит до кадра 10 и замирает,
        /// пока игрок держит стрелку направления (например, зажал влево).
        /// </summary>
        private void HandleAttackWindupHold()
        {
            if (_currentState != "Attack" || !_isHoldingWindup)
            {
                PlayState("Attack");
                _isHoldingWindup = true;
                _currentWindupTime = 0f;
            }

            if (_currentWindupTime < _holdTargetTime)
            {
                _currentWindupTime += Time.deltaTime * windupSpeed;
                if (_currentWindupTime >= _holdTargetTime)
                {
                    _currentWindupTime = _holdTargetTime;
                }
            }

            float normalizedTime = Mathf.Clamp01(_currentWindupTime / _attackClipLength);
            animator.Play("Attack", 0, normalizedTime);
            animator.speed = 0f; // Замораживаем на кадре 10!
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

        private float GetHorizontalSign(Direction8 dir)
        {
            switch (dir)
            {
                case Direction8.Right:
                case Direction8.UpRight:
                case Direction8.DownRight:
                    return 1f;

                case Direction8.Left:
                case Direction8.UpLeft:
                case Direction8.DownLeft:
                    return -1f;

                default:
                    return 0f;
            }
        }

        public void ForceHoldFrame(int frameIndex)
        {
            attackWindupHoldFrame = frameIndex;
            CalculateClipTimings();
        }
    }
}
