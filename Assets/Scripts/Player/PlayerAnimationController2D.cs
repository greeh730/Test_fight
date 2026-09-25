using System;
using UnityEngine;
using Combat;
using Combat.UI;
using Combat.Stances;

namespace Combat.Player
{
    /// <summary>
    /// Контроллер спрайтовой анимации героя (Evo) для Abyss of Emotions.
    /// Управляет 11 состояниями героя:
    /// - Idle (покой на земле)
    /// - Walk (ходьба по земле)
    /// - Run (быстрый бег с зажатым Shift)
    /// - Jump (прыжок и полет в воздухе)
    /// - Block (защитная стойка ПКМ)
    /// - Parry (отбив удара клинком)
    /// - Damage (получение урона / оглушение)
    /// - Attack_High (подбрасывающий удар вверх / Launcher)
    /// - Attack_Mid (выпад и пронзающий удар вперед)
    /// - Attack_Low (подсечка по ногам вниз)
    /// - Attack_Combo1 (базовая серия нейтральных ударов)
    /// Включает поддержку интерактивного замаха (Wind-Up) при удержании направления на Vector Wheel.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlayerAnimationController2D : MonoBehaviour
    {
        [Header("--- Animator Settings ---")]
        [SerializeField] private Animator animator;
        [SerializeField] private RuntimeAnimatorController animatorControllerAsset;

        [Header("--- Attack Wind-Up Hold Settings ---")]
        [Tooltip("Кадр, на котором замирает анимация замаха при удержании стрелки (кадр 2 при samples=14)")]
        [SerializeField] private int attackWindupHoldFrame = 2;

        [Tooltip("Частота кадров (samples) анимации атаки")]
        [SerializeField] private float attackFrameRate = 14f;

        [Tooltip("Скорость проигрывания до кадра замаха")]
        [SerializeField] private float windupSpeed = 3.5f;

        [Header("--- Reaction Durations ---")]
        [Tooltip("Длительность проигрывания анимации урона при получении удара")]
        [SerializeField] private float damageDuration = 0.35f;

        [Tooltip("Длительность проигрывания анимации парирования")]
        [SerializeField] private float parryDuration = 0.35f;

        [Header("--- References ---")]
        [SerializeField] private PlayerController2D playerController;
        [SerializeField] private PlayerCombatController2D playerCombat;
        [SerializeField] private PlayerBlockAndParry2D playerBlockParry;
        [SerializeField] private PlayerHealth2D playerHealth;
        [SerializeField] private VectorWheelController vectorWheel;

        [Header("--- Visual Elements ---")]
        [Tooltip("Старый маркер лица (отключается, так как спрайт уже содержит детали лица)")]
        [SerializeField] private Transform faceTransform;

        // Внутреннее состояние аниматора
        private SpriteRenderer _sr;
        private string _currentState = "";
        private string _currentAttackState = "Attack_Combo1";
        private float _currentWindupTime = 0f;
        private bool _isHoldingWindup = false;
        private float _holdTargetTime = 0.143f;
        private float _damageTimer = 0f;
        private float _parryTimer = 0f;
        private bool _awaitingNewDirectionAfterStrike = false;
        private Direction8 _struckDirection = Direction8.None;

        public bool IsHoldingWindup => _isHoldingWindup;
        public string CurrentState => _currentState;
        public string CurrentAttackState => _currentAttackState;

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
            if (playerCombat == null) playerCombat = GetComponent<PlayerCombatController2D>();
            if (playerBlockParry == null) playerBlockParry = GetComponent<PlayerBlockAndParry2D>();
            if (playerHealth == null) playerHealth = GetComponent<PlayerHealth2D>();
            if (vectorWheel == null) vectorWheel = FindAnyObjectByType<VectorWheelController>();

            if (faceTransform == null) faceTransform = transform.Find("Face");
            if (faceTransform != null)
            {
                var faceSr = faceTransform.GetComponent<SpriteRenderer>();
                if (faceSr != null) faceSr.enabled = false;
            }

            CalculateClipTimings();
        }

        private void OnEnable()
        {
            if (playerHealth != null)
            {
                playerHealth.onDamaged.AddListener(HandleDamageTaken);
                playerHealth.onDeath.AddListener(HandleDeath);
                playerHealth.onRespawn.AddListener(HandleRespawn);
            }

            if (playerBlockParry != null)
            {
                playerBlockParry.onParryStarted.AddListener(HandleParryTriggered);
                playerBlockParry.onParrySuccess.AddListener(HandleParryTriggered);
            }

            if (playerCombat != null)
            {
                playerCombat.onSequenceExecuted += HandleSequenceExecuted;
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

            if (playerBlockParry != null)
            {
                playerBlockParry.onParryStarted.RemoveListener(HandleParryTriggered);
                playerBlockParry.onParrySuccess.RemoveListener(HandleParryTriggered);
            }

            if (playerCombat != null)
            {
                playerCombat.onSequenceExecuted -= HandleSequenceExecuted;
            }
        }

        private void CalculateClipTimings()
        {
            _holdTargetTime = attackWindupHoldFrame / Mathf.Max(1f, attackFrameRate);
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
            // 0. СМЕРТЬ ИГРОКА
            if (playerHealth != null && playerHealth.IsDead)
            {
                PlayState("Damage");
                if (animator != null) animator.speed = 0f;
                return;
            }

            // 1. ПОЛУЧЕНИЕ УРОНА (HURT / HITSTUN)
            if (_damageTimer > 0f || (playerCombat != null && playerCombat.IsHitstunned))
            {
                if (_damageTimer > 0f) _damageTimer -= Time.deltaTime;
                _isHoldingWindup = false;
                PlayState("Damage");
                if (animator != null) animator.speed = 1.0f;
                return;
            }

            // 2. ПАРИРОВАНИЕ (PARRY)
            if (_parryTimer > 0f || (playerBlockParry != null && playerBlockParry.IsParrying))
            {
                if (_parryTimer > 0f) _parryTimer -= Time.deltaTime;
                _isHoldingWindup = false;
                PlayState("Parry");
                if (animator != null) animator.speed = 1.0f;
                return;
            }

            // 3. ПРОВЕРКА ЗАМАХА И УДАРА (Vector Wheel LMB Drag & Strike)
            bool isDraggingLMB = vectorWheel != null && vectorWheel.IsDragging && vectorWheel.ActiveGestureButton == WheelGestureButton.LMB;
            Direction8 currentWheelDir = isDraggingLMB ? vectorWheel.CurrentDirection : Direction8.None;

            if (!isDraggingLMB)
            {
                // Отпустили ЛКМ — полностью сбрасываем блокировку после удара
                _awaitingNewDirectionAfterStrike = false;
                _struckDirection = Direction8.None;
            }
            else if (_awaitingNewDirectionAfterStrike)
            {
                // Если вернулись в центр колеса или перешли в НОВЫЙ сектор — сбрасываем блокировку для нового замаха
                if (currentWheelDir == Direction8.None)
                {
                    _awaitingNewDirectionAfterStrike = false;
                    _struckDirection = Direction8.None;
                }
                else if (currentWheelDir != _struckDirection)
                {
                    _awaitingNewDirectionAfterStrike = false;
                    _struckDirection = Direction8.None;
                }
            }

            // Замах активен, только если игрок целится И НЕ ожидает нового направления после совершенного удара
            bool isAimingOnWheel = isDraggingLMB && currentWheelDir != Direction8.None && !_awaitingNewDirectionAfterStrike;
            Direction8 aimDir = isAimingOnWheel ? currentWheelDir : Direction8.None;

            // Выбор анимации замаха при прицеливании на стрелке (без разворота персонажа за мышкой)
            if (isAimingOnWheel && aimDir != Direction8.None)
            {
                _currentAttackState = GetAttackStateForDirection(aimDir);
            }

            // Проверка фаз боя контроллера
            bool isCombatStartup = playerCombat != null && playerCombat.CurrentState == CombatState.Startup;
            bool isCombatStriking = playerCombat != null && (playerCombat.CurrentState == CombatState.Active || playerCombat.CurrentState == CombatState.Recovery);

            // Б) Активная фаза удара (Выполняется атака клинком) — высший приоритет над замахом!
            if (isCombatStriking)
            {
                _awaitingNewDirectionAfterStrike = true;
                _struckDirection = currentWheelDir;

                if (_isHoldingWindup)
                {
                    _isHoldingWindup = false;
                    float attackLength = GetAttackClipLength(_currentAttackState);
                    float startNormalized = Mathf.Clamp01(_holdTargetTime / Mathf.Max(0.01f, attackLength));
                    if (animator != null)
                    {
                        animator.Play(_currentAttackState, 0, startNormalized);
                        _currentState = _currentAttackState;
                        animator.speed = 1.35f;
                    }
                }
                else if (_currentState != _currentAttackState)
                {
                    PlayState(_currentAttackState);
                    if (animator != null) animator.speed = 1.35f;
                }
                return;
            }

            // А) Режим удержания замаха (Игрок целится стрелкой ИЛИ идет фаза Startup)
            if (isAimingOnWheel || isCombatStartup)
            {
                HandleAttackWindupHold();
                return;
            }
            else
            {
                if (_isHoldingWindup)
                {
                    _isHoldingWindup = false;
                }
            }

            _isHoldingWindup = false;

            // 4. ПРОВЕРКА БЛОКА (ПКМ)
            if (playerBlockParry != null && playerBlockParry.IsBlocking)
            {
                PlayState("Block");
                if (animator != null) animator.speed = 1.0f;
                return;
            }

            // 5. ПРОВЕРКА ВОЗДУХА / ПРЫЖКА (не зацикливается, удерживает позу падения)
            if (playerController != null && !playerController.IsGrounded)
            {
                if (_currentState != "Jump")
                {
                    PlayState("Jump");
                }
                if (animator != null) animator.speed = 1.0f;
                return;
            }

            // 6. ДВИЖЕНИЕ ИЛИ ПОКОЙ НА ЗЕМЛЕ (RUN / WALK / IDLE)
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

        private void HandleAttackWindupHold()
        {
            if (_currentState != _currentAttackState || !_isHoldingWindup)
            {
                PlayState(_currentAttackState);
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

            float attackLength = GetAttackClipLength(_currentAttackState);
            float normalizedTime = Mathf.Clamp01(_currentWindupTime / Mathf.Max(0.01f, attackLength));
            if (animator != null)
            {
                animator.Play(_currentAttackState, 0, normalizedTime);
                animator.speed = 0f; // Замораживаем на кадре замаха
            }
        }

        private void HandleDamageTaken()
        {
            _damageTimer = damageDuration;
            _isHoldingWindup = false;
            _awaitingNewDirectionAfterStrike = false;
            _struckDirection = Direction8.None;
            PlayState("Damage");
        }

        private void HandleDeath()
        {
            _isHoldingWindup = false;
            _awaitingNewDirectionAfterStrike = false;
            _struckDirection = Direction8.None;
            PlayState("Damage");
            if (animator != null) animator.speed = 0f;
        }

        private void HandleRespawn()
        {
            _damageTimer = 0f;
            _parryTimer = 0f;
            _isHoldingWindup = false;
            _awaitingNewDirectionAfterStrike = false;
            _struckDirection = Direction8.None;
            PlayState("Idle");
        }

        private void HandleParryTriggered()
        {
            _parryTimer = parryDuration;
            _isHoldingWindup = false;
            _awaitingNewDirectionAfterStrike = false;
            _struckDirection = Direction8.None;
            PlayState("Parry");
        }

        private void HandleSequenceExecuted(ComboSequenceDefinition seq)
        {
            if (seq == null) return;

            _awaitingNewDirectionAfterStrike = true;
            _struckDirection = (vectorWheel != null) ? vectorWheel.CurrentDirection : Direction8.None;

            if (seq.IsLauncher || (seq.AttackData != null && seq.AttackData.targetedZones.HasFlag(CombatZone.High)))
            {
                _currentAttackState = "Attack_High";
            }
            else if (seq.AttackData != null && seq.AttackData.targetedZones.HasFlag(CombatZone.Low))
            {
                _currentAttackState = "Attack_Low";
            }
            else if (seq.AttackData != null && seq.AttackData.targetedZones.HasFlag(CombatZone.Mid))
            {
                _currentAttackState = "Attack_Mid";
            }
            else
            {
                _currentAttackState = "Attack_Combo1";
            }
        }

        private string GetAttackStateForDirection(Direction8 dir)
        {
            switch (dir)
            {
                case Direction8.Up:
                case Direction8.UpRight:
                case Direction8.UpLeft:
                    return "Attack_High";

                case Direction8.Down:
                case Direction8.DownRight:
                case Direction8.DownLeft:
                    return "Attack_Low";

                case Direction8.Right:
                case Direction8.Left:
                    return "Attack_Mid";

                default:
                    return "Attack_Combo1";
            }
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
