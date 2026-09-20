using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Combat.UI;
using Combat.Stances;
using Combat.Tactician;
using Combat.Player;

namespace Combat
{
    public enum CombatState
    {
        Idle,
        Startup,
        Active,
        Recovery
    }

    public enum WheelControlMode
    {
        ScreenAbsolute, // Правая полусфера колеса бьет вправо на экране, левая — влево (рекомендуется)
        FacingRelative   // Правая полусфера колеса бьет в сторону взгляда (вперед), левая — за спину (назад)
    }

    [Serializable]
    public struct AttackIntent
    {
        public AttackHeight height;
        public StrikeDirection strikeDir; // Forward vs Backward
        public float horizontalSign;       // +1f (Right) or -1f (Left) in world X
        public bool isCharged;             // Заряженная усиленная атака

        public AttackIntent(AttackHeight h, StrikeDirection dir, float sign, bool charged = false)
        {
            height = h;
            strikeDir = dir;
            horizontalSign = sign;
            isCharged = charged;
        }
    }

    [Serializable]
    public class ComboStepEvent : UnityEvent<int, bool> { }

    [DisallowMultipleComponent]
    public class PlayerCombatController2D : MonoBehaviour
    {
        [Header("--- 3 Базовых удара по высоте (High, Mid, Low) ---")]
        [Tooltip("Верхний удар: рубящий / апперкот в голову и воздух (High + Mid)")]
        [SerializeField] private AttackConfig attackHigh = new AttackConfig(
            "Верхний рубящий",
            CombatZone.Mid | CombatZone.High,
            new Vector2(0.9f, 0.45f),
            new Vector2(1.3f, 1.4f),
            0.08f, 0.18f, 0.22f,
            25f,
            new Vector2(4.0f, 7.0f), // Подбрасывание вверх
            new Color(1f, 0.4f, 0.1f, 0.8f) // Оранжево-красный (Mid+High)
        );

        [Tooltip("Средний удар: прямой выпад / колющий тычок в корпус (Mid)")]
        [SerializeField] private AttackConfig attackMid = new AttackConfig(
            "Средний выпад",
            CombatZone.Mid,
            new Vector2(1.2f, 0.0f),
            new Vector2(1.4f, 0.8f),
            0.06f, 0.15f, 0.18f,
            20f,
            new Vector2(6.5f, 1.5f),
            new Color(1f, 0.85f, 0.15f, 0.8f) // Желтый (Mid)
        );

        [Tooltip("Нижний удар: подсечка по ногам / нижняя атака (Low)")]
        [SerializeField] private AttackConfig attackLow = new AttackConfig(
            "Нижняя подсечка",
            CombatZone.Low,
            new Vector2(1.0f, -0.45f),
            new Vector2(1.5f, 0.6f),
            0.07f, 0.16f, 0.20f,
            18f,
            new Vector2(5.5f, 0.5f),
            new Color(0.15f, 0.85f, 1f, 0.8f) // Голубой (Low)
        );

        [Header("--- Combo System Settings ---")]
        [Tooltip("Максимальное количество ударов в одной цепочке комбо")]
        [SerializeField] private int maxComboSteps = 3;

        [Tooltip("Время бездействия (в секундах), после которого комбо сбрасывается")]
        [SerializeField] private float comboResetTime = 1.2f;

        [Tooltip("Множитель времени замаха со 2-го шага комбо (меньше 1 = быстрее)")]
        [Range(0.3f, 1f)]
        [SerializeField] private float comboStartupMultiplier = 0.65f;

        [Tooltip("Множитель урона для завершающего удара комбо (Finisher)")]
        [Range(1f, 4f)]
        [SerializeField] private float finisherDamageMultiplier = 1.85f;

        [Tooltip("Множитель силы отталкивания для завершающего удара комбо")]
        [Range(1f, 4f)]
        [SerializeField] private float finisherKnockbackMultiplier = 2.2f;

        [Tooltip("Сила импульса выпада вперед/в сторону удара при комбо")]
        [SerializeField] private float comboLungeForce = 3.5f;

        [Tooltip("Комбо засчитывается только при повторе одинакового удара (любое совмещение сбрасывает серию)")]
        [SerializeField] private bool requireSameAttackForCombo = true;

        [Tooltip("Ограничивать серию комбо только средними ударами (Вперед и Назад)")]
        [SerializeField] private bool limitCombosToMidStrikesOnly = false;

        [Header("--- Empowered / Charged Strike Settings ---")]
        [Tooltip("Множитель урона для заряженной усиленной атаки")]
        [Range(1.5f, 4f)]
        [SerializeField] private float empoweredDamageMultiplier = 2.0f;

        [Tooltip("Множитель силы отталкивания для заряженной усиленной атаки")]
        [Range(1.5f, 5f)]
        [SerializeField] private float empoweredKnockbackMultiplier = 2.8f;

        [Tooltip("Множитель размера хитбокса заряженной атаки")]
        [Range(1f, 2f)]
        [SerializeField] private float empoweredHitboxScale = 1.25f;

        [Tooltip("Сила выпада вперед при заряженной атаке")]
        [SerializeField] private float empoweredLungeForce = 6.0f;

        [Tooltip("Скорость плавного подшага вперед во время удержания заряда (1-2 сек)")]
        [SerializeField] private float chargeCrawlSpeed = 1.2f;

        [Tooltip("Цвет хитбокса заряженной атаки")]
        [SerializeField] private Color empoweredHitboxColor = new Color(1f, 0.65f, 0.05f, 0.9f);

        [Header("--- Input Buffer & Cancel Windows ---")]
        [Tooltip("Длительность окна буферизации ввода (в секундах)")]
        [SerializeField] private float inputBufferDuration = 0.55f;

        [Tooltip("Доля времени фазы Recovery, после которой разрешена отмена следующим ударом")]
        [Range(0f, 1f)]
        [SerializeField] private float recoveryCancelThreshold = 0.3f;

        [Tooltip("Длительность тактильной микро-паузы (Hitstop) при попадании")]
        [Range(0f, 0.15f)]
        [SerializeField] private float hitstopDuration = 0.045f;

        [Header("--- Target Layer Mask ---")]
        [Tooltip("Слои, на которых ищутся враги и мишени")]
        [SerializeField] private LayerMask targetLayers = ~0;

        [Header("--- Input & Wheel Binding ---")]
        [SerializeField] private VectorWheelController vectorWheel;
        [Tooltip("Режим интерпретации направлений колеса")]
        [SerializeField] private WheelControlMode wheelControlMode = WheelControlMode.ScreenAbsolute;

        [Header("--- Visualizer & Gizmos ---")]
        [SerializeField] private HitboxVisualizer2D visualizer;
        [SerializeField] private bool showAllHitboxesInEditorGizmos = true;
        [Tooltip("Отображать зеркальные хитбоксы ударов Назад в Scene Gizmos")]
        [SerializeField] private bool showBackwardHitboxesInGizmos = true;

        [Header("--- Face / Visual Orientation ---")]
        [Tooltip("Ссылка на дочерний объект Face (если null, ищется автоматически)")]
        [SerializeField] private Transform faceTransform;
        private Vector3 _faceBaseLocalPos = new Vector3(0.16f, 0.14f, 0f);
        private Vector3 _faceBaseLocalScale = new Vector3(0.45f, 0.26f, 1f);

        [Header("--- Stance System Settings ---")]
        [Tooltip("Текущая боевая стойка игрока (Normal / Tactician)")]
        [SerializeField] private CombatStance currentStance = CombatStance.Normal;
        [SerializeField] private TacticianCombatController2D tacticianController;
        public CombatStance CurrentStance => currentStance;

        [Serializable]
        public class StanceChangedEvent : UnityEvent<CombatStance> { }
        public StanceChangedEvent onStanceChanged;

        [Header("--- Combo Events ---")]
        public ComboStepEvent onComboStepChanged;

        // Runtime State
        public CombatState CurrentState { get; private set; } = CombatState.Idle;
        public AttackConfig CurrentAttack { get; private set; }
        public AttackIntent CurrentIntent { get; private set; }
        public float FacingDirection { get; private set; } = 1f;
        public int CurrentComboStep { get; private set; } = 1;
        public bool IsFinisher => CurrentComboStep >= maxComboSteps;
        public bool CanCancelIntoCombo => _canCancelIntoCombo;

        private Coroutine _attackRoutine;
        private Coroutine _hitstopRoutine;
        private readonly HashSet<Collider2D> _hitTargetsInCurrentSwing = new HashSet<Collider2D>();
        private readonly HashSet<IHurtboxTarget2D> _hitReceiversInCurrentSwing = new HashSet<IHurtboxTarget2D>();
        private readonly List<Collider2D> _overlapResults = new List<Collider2D>(16);
        private ContactFilter2D _contactFilter;

        private AttackIntent? _bufferedIntent;
        private float _bufferedAttackTime = -10f;
        private float _lastAttackStartTime = -10f;
        private float _lastAttackFinishTime = -10f;
        private bool _canCancelIntoCombo;
        private bool _hasHitTargetInCurrentAttack;
        private bool _lastWasFinisher;
        private AttackIntent? _lastComboIntent;
        private Rigidbody2D _rb;
        private SpriteRenderer _sr;
        private PlayerStamina2D _stamina;

        public AttackConfig AttackHigh => attackHigh;
        public AttackConfig AttackMid => attackMid;
        public AttackConfig AttackLow => attackLow;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _sr = GetComponent<SpriteRenderer>();
            _stamina = GetComponent<PlayerStamina2D>();

            _contactFilter = new ContactFilter2D();
            _contactFilter.SetLayerMask(targetLayers);
            _contactFilter.useTriggers = true;

            if (visualizer == null)
            {
                visualizer = GetComponent<HitboxVisualizer2D>();
                if (visualizer == null) visualizer = gameObject.AddComponent<HitboxVisualizer2D>();
            }

            if (vectorWheel == null)
            {
                vectorWheel = FindAnyObjectByType<VectorWheelController>();
            }

            if (faceTransform == null)
            {
                faceTransform = transform.Find("Face");
            }
            if (faceTransform != null)
            {
                _faceBaseLocalPos = faceTransform.localPosition;
                _faceBaseLocalScale = faceTransform.localScale;
            }

            if (tacticianController == null)
            {
                tacticianController = GetComponent<TacticianCombatController2D>();
                if (tacticianController == null) tacticianController = gameObject.AddComponent<TacticianCombatController2D>();
            }
        }

        private void Start()
        {
            if (vectorWheel != null)
            {
                vectorWheel.SetStance(currentStance);
            }
        }

        private void OnEnable()
        {
            if (vectorWheel != null)
            {
                vectorWheel.onSwipeCompleted.AddListener(OnWheelSwipeCompleted);
            }
        }

        public void CancelAttack()
        {
            if (_attackRoutine != null)
            {
                StopCoroutine(_attackRoutine);
                _attackRoutine = null;
            }
            CurrentState = CombatState.Idle;
            if (visualizer != null) visualizer.HideHitbox();
        }

        private void OnDisable()
        {
            CancelAttack();
            if (vectorWheel != null)
            {
                vectorWheel.onSwipeCompleted.RemoveListener(OnWheelSwipeCompleted);
            }
            Time.timeScale = 1f;
        }

        private void Update()
        {
            if (Time.timeScale <= 0.0001f || Combat.UI.PauseMenuController.IsGamePaused)
            {
                return;
            }

            CheckStanceToggle();
            UpdateFacingDirection();
            UpdateChargeCrawl();
            UpdateComboTimers();
            CheckInputBuffer();
        }

        private void CheckStanceToggle()
        {
            if (IsLeftCtrlDown())
            {
                ToggleStance();
            }
        }

        private static bool IsLeftCtrlDown()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                return UnityEngine.InputSystem.Keyboard.current.leftCtrlKey.wasPressedThisFrame;
            }
#endif
            try { return Input.GetKeyDown(KeyCode.LeftControl); } catch { return false; }
        }

        public void ToggleStance()
        {
            currentStance = (currentStance == CombatStance.Normal) ? CombatStance.Tactician : CombatStance.Normal;
            if (vectorWheel != null)
            {
                vectorWheel.SetStance(currentStance);
            }
            onStanceChanged?.Invoke(currentStance);

            string stanceText = (currentStance == CombatStance.Tactician)
                ? "<color=#00E5FF><b>[СТОЙКА ТАКТИКА]</b></color>"
                : "<color=#FF4444><b>[БОЕВАЯ СТОЙКА]</b></color>";
            SpawnStancePopup(stanceText, (currentStance == CombatStance.Tactician) ? new Color(0f, 0.95f, 1f) : new Color(1f, 0.25f, 0.25f));
            Debug.Log($"<color=cyan>[STANCE TOGGLE]</color> Смена боевой стойки: <b>{currentStance}</b>");
        }

        public void RestoreStance(CombatStance stance, float facing)
        {
            currentStance = stance;
            if (vectorWheel != null)
            {
                vectorWheel.SetStance(currentStance);
            }
            onStanceChanged?.Invoke(currentStance);

            if (Mathf.Abs(facing) > 0.01f)
            {
                FacingDirection = Mathf.Sign(facing);
                Vector3 s = transform.localScale;
                s.x = Mathf.Abs(s.x) * FacingDirection;
                transform.localScale = s;
            }
        }

        private void SpawnStancePopup(string text, Color color)
        {
            StartCoroutine(SpawnStancePopupRoutine(text, color));
        }

        private IEnumerator SpawnStancePopupRoutine(string text, Color color)
        {
            var go = new GameObject("Stance_Popup");
            go.transform.position = transform.position + new Vector3(0f, 1.4f, 0f);

            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 46;
            tm.characterSize = 0.088f;
            tm.alignment = TextAlignment.Center;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.fontStyle = FontStyle.Bold;
            tm.color = color;

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 90;

            float dur = 1.15f;
            float el = 0f;
            Vector3 startPos = go.transform.position;
            Vector3 endPos = startPos + new Vector3(0f, 1.1f, 0f);

            while (el < dur)
            {
                el += Time.deltaTime;
                float t = el / dur;
                if (go != null)
                {
                    go.transform.position = Vector3.Lerp(startPos, endPos, t);
                    Color c = tm.color;
                    c.a = Mathf.Clamp01(1f - t);
                    tm.color = c;
                }
                yield return null;
            }

            if (go != null) Destroy(go);
        }

        private void UpdateChargeCrawl()
        {
            if (vectorWheel == null || !vectorWheel.IsDragging) return;
            if (vectorWheel.DirectionHoldTimer < 0.2f) return;
            if (CurrentState != CombatState.Idle) return;

            var intent = MapDirection(vectorWheel.CurrentDirection);
            if (!intent.HasValue) return;

            float sign = intent.Value.horizontalSign;
            if (Mathf.Abs(sign) > 0.01f)
            {
                SetFacingDirection(sign);
                if (_rb != null)
                {
                    // Медленное продвижение вперед во время зажатия (подкрадывание/подшаг)
                    _rb.linearVelocity = new Vector2(sign * chargeCrawlSpeed, _rb.linearVelocity.y);
                }
            }
        }

        private void UpdateFacingDirection()
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();

            if (_sr != null)
            {
                FacingDirection = _sr.flipX ? -1f : 1f;
                return;
            }

            if (transform.localScale.x < -0.01f)
            {
                FacingDirection = -1f;
            }
            else if (transform.localScale.x > 0.01f)
            {
                FacingDirection = 1f;
            }
            else if (_rb != null && Mathf.Abs(_rb.linearVelocity.x) > 0.1f)
            {
                FacingDirection = Mathf.Sign(_rb.linearVelocity.x);
            }
        }

        public void SetFacingDirection(float dir)
        {
            if (Mathf.Abs(dir) < 0.01f) return;
            FacingDirection = Mathf.Sign(dir);

            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            if (_sr != null)
            {
                _sr.flipX = FacingDirection < 0f;
            }

            if (faceTransform == null) faceTransform = transform.Find("Face");
            if (faceTransform != null)
            {
                float absX = Mathf.Abs(_faceBaseLocalPos.x > 0.001f ? _faceBaseLocalPos.x : 0.16f);
                float absScaleX = Mathf.Abs(_faceBaseLocalScale.x > 0.001f ? _faceBaseLocalScale.x : 0.45f);

                faceTransform.localPosition = new Vector3(absX * FacingDirection, _faceBaseLocalPos.y, _faceBaseLocalPos.z);
                faceTransform.localScale = new Vector3(absScaleX * FacingDirection, _faceBaseLocalScale.y, _faceBaseLocalScale.z);
            }
        }

        private void UpdateComboTimers()
        {
            // Сброс комбо при долгом бездействии в Idle
            float idleTimeReference = _lastAttackFinishTime > 0f ? _lastAttackFinishTime : _lastAttackStartTime;
            if (CurrentComboStep > 1 && CurrentState == CombatState.Idle && Time.time - idleTimeReference > comboResetTime)
            {
                ResetCombo();
            }

            // Устаревание буфера ввода
            if (_bufferedIntent.HasValue && Time.time - _bufferedAttackTime > inputBufferDuration)
            {
                _bufferedIntent = null;
            }
        }

        private void CheckInputBuffer()
        {
            if (!_bufferedIntent.HasValue) return;

            if (CanExecuteAttackNow())
            {
                var intent = _bufferedIntent.Value;
                ClearBuffer();
                ExecuteAttack(intent);
            }
        }

        public bool CanExecuteAttackNow()
        {
            if (CurrentState == CombatState.Idle) return true;
            if (_canCancelIntoCombo) return true;
            return false;
        }

        private void ClearBuffer()
        {
            _bufferedIntent = null;
            _bufferedAttackTime = -10f;
        }

        public void ResetCombo()
        {
            CurrentComboStep = 1;
            _lastWasFinisher = false;
            _lastComboIntent = null;
            onComboStepChanged?.Invoke(CurrentComboStep, false);
        }

        private void OnWheelSwipeCompleted(Direction8 dir, Vector2 vector, float distance)
        {
            if (dir == Direction8.None) return;

            if (currentStance == CombatStance.Tactician)
            {
                if (_stamina != null)
                {
                    _stamina.ConsumeForAction($"Tactician_{dir}", 18f);
                }
                if (tacticianController != null)
                {
                    tacticianController.ExecuteAbility(dir);
                }
                return;
            }

            bool isCharged = vectorWheel != null && vectorWheel.ConsumeCharge();
            AttackIntent? mapped = MapDirection(dir, isCharged);
            if (!mapped.HasValue) return;

            TryAttackOrBuffer(mapped.Value);
        }

        public AttackIntent? MapDirection(Direction8 dir, bool isCharged = false)
        {
            if (dir == Direction8.None) return null;

            // 1. Высота атаки (High, Mid, Low)
            AttackHeight height = dir switch
            {
                Direction8.Up or Direction8.UpRight or Direction8.UpLeft => AttackHeight.High,
                Direction8.Right or Direction8.Left => AttackHeight.Mid,
                Direction8.Down or Direction8.DownRight or Direction8.DownLeft => AttackHeight.Low,
                _ => AttackHeight.Mid
            };

            // 2. Определение стороны удара (+1 вправо, -1 влево на экране)
            float sign;
            StrikeDirection strikeDir;

            if (wheelControlMode == WheelControlMode.ScreenAbsolute)
            {
                if (dir == Direction8.Right || dir == Direction8.UpRight || dir == Direction8.DownRight)
                {
                    sign = 1f; // бьет вправо на экране -> ВПЕРЕД
                    strikeDir = StrikeDirection.Forward;
                }
                else if (dir == Direction8.Left || dir == Direction8.UpLeft || dir == Direction8.DownLeft)
                {
                    sign = -1f; // бьет влево на экране -> НАЗАД
                    strikeDir = StrikeDirection.Backward;
                }
                else
                {
                    sign = FacingDirection; // для чистых Up/Down бьем в сторону взгляда
                    strikeDir = StrikeDirection.Forward;
                }
            }
            else // FacingRelative
            {
                if (dir == Direction8.Right || dir == Direction8.UpRight || dir == Direction8.DownRight)
                {
                    strikeDir = StrikeDirection.Forward;
                    sign = FacingDirection;
                }
                else if (dir == Direction8.Left || dir == Direction8.UpLeft || dir == Direction8.DownLeft)
                {
                    strikeDir = StrikeDirection.Backward;
                    sign = -FacingDirection;
                }
                else
                {
                    strikeDir = StrikeDirection.Forward;
                    sign = FacingDirection;
                }
            }

            return new AttackIntent(height, strikeDir, sign, isCharged);
        }

        public bool TryAttackOrBuffer(AttackIntent intent)
        {
            if (CanExecuteAttackNow())
            {
                ClearBuffer();
                return ExecuteAttack(intent);
            }

            _bufferedIntent = intent;
            _bufferedAttackTime = Time.time;
            return false;
        }

        public bool TryAttackOrBuffer(AttackDirection dir)
        {
            var intent = ConvertLegacyDirection(dir);
            return TryAttackOrBuffer(intent);
        }

        public bool ExecuteAttack(AttackDirection dir)
        {
            return ExecuteAttack(ConvertLegacyDirection(dir));
        }

        public bool ExecuteAttack(AttackHeight height, StrikeDirection strikeDir = StrikeDirection.Forward)
        {
            float sign = (strikeDir == StrikeDirection.Forward) ? FacingDirection : -FacingDirection;
            return ExecuteAttack(new AttackIntent(height, strikeDir, sign));
        }

        private AttackIntent ConvertLegacyDirection(AttackDirection dir)
        {
            return dir switch
            {
                AttackDirection.Right => new AttackIntent(AttackHeight.Mid, StrikeDirection.Forward, FacingDirection),
                AttackDirection.Up => new AttackIntent(AttackHeight.High, StrikeDirection.Forward, FacingDirection),
                AttackDirection.Down => new AttackIntent(AttackHeight.Low, StrikeDirection.Forward, FacingDirection),
                AttackDirection.Left => new AttackIntent(AttackHeight.Mid, StrikeDirection.Backward, -FacingDirection),
                _ => new AttackIntent(AttackHeight.Mid, StrikeDirection.Forward, FacingDirection)
            };
        }

        public AttackConfig GetAttackForHeight(AttackHeight height)
        {
            return height switch
            {
                AttackHeight.High => attackHigh,
                AttackHeight.Mid => attackMid,
                AttackHeight.Low => attackLow,
                _ => attackMid
            };
        }

        private bool IsComboCompatible(AttackIntent lastIntent, AttackIntent nextIntent)
        {
            if (!requireSameAttackForCombo) return true;

            // 1. Высота удара должна строго совпадать (например, Mid != High)
            if (lastIntent.height != nextIntent.height) return false;

            // 2. Горизонтальное направление должно строго совпадать (Вправо != Влево)
            if (Mathf.Sign(lastIntent.horizontalSign) != Mathf.Sign(nextIntent.horizontalSign)) return false;

            // 3. Относительное направление должно совпадать (Forward != Backward)
            if (lastIntent.strikeDir != nextIntent.strikeDir) return false;

            // 4. Ограничение комбо только средними ударами (если включено в инспекторе)
            if (limitCombosToMidStrikesOnly && nextIntent.height != AttackHeight.Mid) return false;

            return true;
        }

        private void PrepareNextComboStep(AttackIntent newIntent, bool isChaining)
        {
            if (newIntent.isCharged)
            {
                CurrentComboStep = 1;
                _lastComboIntent = newIntent;
                _lastAttackStartTime = Time.time;
                return;
            }

            if (isChaining)
            {
                if (_lastComboIntent.HasValue && IsComboCompatible(_lastComboIntent.Value, newIntent))
                {
                    CurrentComboStep = (CurrentComboStep >= maxComboSteps) ? 1 : CurrentComboStep + 1;
                }
                else
                {
                    CurrentComboStep = 1;
                }
            }
            else
            {
                float idleTimeReference = _lastAttackFinishTime > 0f ? _lastAttackFinishTime : _lastAttackStartTime;
                bool withinTime = (Time.time - idleTimeReference <= comboResetTime) && !_lastWasFinisher && (idleTimeReference > 0f);
                if (withinTime && _lastComboIntent.HasValue && IsComboCompatible(_lastComboIntent.Value, newIntent))
                {
                    CurrentComboStep = (CurrentComboStep >= maxComboSteps) ? 1 : CurrentComboStep + 1;
                }
                else
                {
                    CurrentComboStep = 1;
                }
            }

            _lastComboIntent = newIntent;
            _lastAttackStartTime = Time.time;
        }

        public bool ExecuteAttack(AttackIntent intent, bool isComboChain = false)
        {
            if (!CanExecuteAttackNow()) return false;

            AttackConfig attack = GetAttackForHeight(intent.height);
            if (attack == null) return false;

            if (_stamina != null)
            {
                string actionId = $"{intent.height}_{intent.strikeDir}_{(intent.isCharged ? "Charged" : "Normal")}";
                float cost = intent.isCharged ? 20f : 12f;
                _stamina.ConsumeForAction(actionId, cost);
            }

            // Поворачиваем персонажа и отзеркаливаем лицо в сторону удара (влево / вправо)
            if (Mathf.Abs(intent.horizontalSign) > 0.01f)
            {
                SetFacingDirection(intent.horizontalSign);
            }

            bool isChaining = isComboChain || (_attackRoutine != null) || (CurrentState != CombatState.Idle);

            if (_attackRoutine != null)
            {
                StopCoroutine(_attackRoutine);
                _attackRoutine = null;
                if (visualizer != null) visualizer.HideHitbox();
            }

            PrepareNextComboStep(intent, isChaining);

            _attackRoutine = StartCoroutine(AttackSequenceRoutine(attack, intent));
            return true;
        }

        private IEnumerator AttackSequenceRoutine(AttackConfig attack, AttackIntent intent)
        {
            CurrentAttack = attack;
            CurrentIntent = intent;
            _hitTargetsInCurrentSwing.Clear();
            _hitReceiversInCurrentSwing.Clear();
            _canCancelIntoCombo = false;
            _hasHitTargetInCurrentAttack = false;

            int thisAttackStep = CurrentComboStep;
            bool isFinisher = thisAttackStep >= maxComboSteps && !intent.isCharged;
            bool isCharged = intent.isCharged;

            string dirLabel = (intent.strikeDir == StrikeDirection.Forward) ? "ВПЕРЕД" : "НАЗАД";
            string arrow = intent.horizontalSign > 0 ? "▶" : "◀";
            if (intent.height == AttackHeight.High) arrow = intent.horizontalSign > 0 ? "↗" : "↖";
            if (intent.height == AttackHeight.Low) arrow = intent.horizontalSign > 0 ? "↘" : "↙";

            string displayName = $"{attack.attackName} {dirLabel} {arrow}";

            if (vectorWheel != null)
            {
                vectorWheel.SetAttackPlaqueWithCombo(displayName, thisAttackStep, isFinisher, isCharged);
            }
            onComboStepChanged?.Invoke(thisAttackStep, isFinisher);

            float speedMult = _stamina != null ? _stamina.ActionSpeedMultiplier : 1.0f;

            // 1. ФАЗА ЗАМАХА (STARTUP) — удары в комбо ускоряются, заряженный слегка акцентирован
            CurrentState = CombatState.Startup;
            float startup = (isCharged ? attack.startupTime * 1.1f : (thisAttackStep > 1 ? attack.startupTime * comboStartupMultiplier : attack.startupTime)) / speedMult;
            yield return new WaitForSeconds(startup);

            // 2. АКТИВНАЯ ФАЗА (ACTIVE)
            CurrentState = CombatState.Active;
            float activeTimer = attack.activeTime / speedMult;

            // Выпад в направлении удара (усиленный выпад при заряженном ударе)
            ApplyComboLunge(intent.horizontalSign, isCharged);

            Vector2 boxSize = isCharged ? attack.hitboxSize * empoweredHitboxScale : attack.hitboxSize;
            Color boxColor = isCharged ? empoweredHitboxColor : attack.hitboxColor;

            while (activeTimer > 0f)
            {
                Vector2 boxCenter = GetHitboxCenter(attack, intent.horizontalSign);

                if (visualizer != null)
                {
                    visualizer.ShowHitbox(boxCenter, boxSize, boxColor, isFinisher, isCharged);
                }

                CheckHitboxOverlap(attack, intent, boxCenter, boxSize, isFinisher, isCharged);

                activeTimer -= Time.deltaTime;
                yield return null;
            }

            if (visualizer != null)
            {
                visualizer.HideHitbox();
            }

            // 3. ФАЗА ВОССТАНОВЛЕНИЯ (RECOVERY)
            CurrentState = CombatState.Recovery;
            float recovery = attack.recoveryTime / speedMult;
            float cancelOpenTime = recovery * recoveryCancelThreshold;
            float recoveryTimer = 0f;

            while (recoveryTimer < recovery)
            {
                recoveryTimer += Time.deltaTime;

                if (recoveryTimer >= cancelOpenTime || _hasHitTargetInCurrentAttack)
                {
                    _canCancelIntoCombo = true;

                    if (_bufferedIntent.HasValue)
                    {
                        _lastAttackFinishTime = Time.time;
                        _lastWasFinisher = isFinisher;
                        var nextIntent = _bufferedIntent.Value;
                        ClearBuffer();
                        _attackRoutine = null;
                        ExecuteAttack(nextIntent, isComboChain: true);
                        yield break;
                    }
                }

                yield return null;
            }

            _lastAttackFinishTime = Time.time;
            _lastWasFinisher = isFinisher;
            _canCancelIntoCombo = false;
            CurrentState = CombatState.Idle;
            CurrentAttack = null;
            _attackRoutine = null;
        }

        private void ApplyComboLunge(float horizontalSign, bool isCharged = false)
        {
            if (_rb == null) _rb = GetComponent<Rigidbody2D>();
            if (_rb != null)
            {
                float speedMult = _stamina != null ? _stamina.ActionSpeedMultiplier : 1.0f;
                if (isCharged)
                {
                    _rb.linearVelocity = new Vector2(horizontalSign * empoweredLungeForce * speedMult, _rb.linearVelocity.y);
                }
                else if (comboLungeForce > 0.05f)
                {
                    float multiplier = IsFinisher ? 1.5f : (CurrentComboStep > 1 ? 1.15f : 0.85f);
                    _rb.linearVelocity = new Vector2(horizontalSign * comboLungeForce * multiplier * speedMult, _rb.linearVelocity.y);
                }
            }
        }

        public void TriggerHitstop(float customDuration = -1f)
        {
            float duration = customDuration > 0.001f ? customDuration : hitstopDuration;
            if (duration <= 0.005f) return;
            if (_hitstopRoutine != null) StopCoroutine(_hitstopRoutine);
            _hitstopRoutine = StartCoroutine(HitstopRoutine(duration));
        }

        private IEnumerator HitstopRoutine(float duration)
        {
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = 1.0f;
            _hitstopRoutine = null;
        }

        private void CheckHitboxOverlap(AttackConfig attack, AttackIntent intent, Vector2 center, Vector2 size, bool isFinisher, bool isCharged = false)
        {
            _overlapResults.Clear();
            int count = Physics2D.OverlapBox(center, size, 0f, _contactFilter, _overlapResults);

            string statusSuffix = isCharged ? " [УСИЛЕННАЯ АТАКА!]" : (isFinisher ? " [ФИНИШЕР!]" : "");
            float dmgMult = isCharged ? empoweredDamageMultiplier : (isFinisher ? finisherDamageMultiplier : 1f);
            float kbMult = isCharged ? empoweredKnockbackMultiplier : (isFinisher ? finisherKnockbackMultiplier : 1f);
            Color effectiveColor = isCharged ? empoweredHitboxColor : attack.hitboxColor;

            AttackConfig effectiveAttack = (isFinisher || isCharged)
                ? new AttackConfig(
                    attack.attackName + statusSuffix,
                    attack.targetedZones,
                    attack.hitboxOffset,
                    size,
                    attack.startupTime,
                    attack.activeTime,
                    attack.recoveryTime,
                    attack.damage * dmgMult,
                    attack.knockbackForce * kbMult,
                    effectiveColor
                )
                : attack;

            for (int i = 0; i < count; i++)
            {
                var col = _overlapResults[i];
                if (col == null || col.gameObject == gameObject) continue;
                if (_hitTargetsInCurrentSwing.Contains(col)) continue;

                var hurtbox = col.GetComponent<CombatHurtbox2D>();
                if (hurtbox != null)
                {
                    if (effectiveAttack.targetedZones.Overlaps(hurtbox.BodyZone))
                    {
                        var receiver = hurtbox.GetTargetReceiver();
                        if (receiver != null && _hitReceiversInCurrentSwing.Contains(receiver))
                        {
                            continue;
                        }

                        _hitTargetsInCurrentSwing.Add(col);
                        if (receiver != null) _hitReceiversInCurrentSwing.Add(receiver);

                        OnTargetHitSuccess(isFinisher, isCharged);

                        Vector2 knockbackDir = new Vector2(intent.horizontalSign, 1f).normalized;
                        hurtbox.ReceiveHit(effectiveAttack, center, knockbackDir);
                    }
                }
                else
                {
                    var target = col.GetComponent<IHurtboxTarget2D>() ?? col.GetComponentInParent<IHurtboxTarget2D>();
                    if (target != null)
                    {
                        if (_hitReceiversInCurrentSwing.Contains(target)) continue;

                        _hitTargetsInCurrentSwing.Add(col);
                        _hitReceiversInCurrentSwing.Add(target);

                        OnTargetHitSuccess(isFinisher, isCharged);

                        Vector2 knockbackDir = new Vector2(intent.horizontalSign, 1f).normalized;
                        target.TakeHit(effectiveAttack, effectiveAttack.targetedZones, center, knockbackDir);
                    }
                }
            }
        }

        private void OnTargetHitSuccess(bool isFinisher = false, bool isCharged = false)
        {
            if (!_hasHitTargetInCurrentAttack)
            {
                _hasHitTargetInCurrentAttack = true;
                _canCancelIntoCombo = true;
                float hitstop = isCharged ? 0.10f : (isFinisher ? hitstopDuration * 1.8f : hitstopDuration);
                TriggerHitstop(hitstop);

                if (isCharged && visualizer != null)
                {
                    visualizer.ShowEmpoweredPopup(transform.position + new Vector3(FacingDirection * 1.1f, 0.6f, 0f));
                }
            }
        }

        public Vector2 GetHitboxCenter(AttackConfig attack, float horizontalSign)
        {
            Vector2 playerPos = transform.position;
            return new Vector2(
                playerPos.x + attack.hitboxOffset.x * horizontalSign,
                playerPos.y + attack.hitboxOffset.y
            );
        }

        public Vector2 GetHitboxCenter(AttackConfig attack)
        {
            return GetHitboxCenter(attack, FacingDirection);
        }

        private void OnDrawGizmosSelected()
        {
            float forwardSign = Application.isPlaying ? FacingDirection : (transform.localScale.x < 0 ? -1f : 1f);

            if (showAllHitboxesInEditorGizmos)
            {
                DrawAttackGizmo(attackHigh, forwardSign, false);
                DrawAttackGizmo(attackMid, forwardSign, false);
                DrawAttackGizmo(attackLow, forwardSign, false);

                if (showBackwardHitboxesInGizmos)
                {
                    DrawAttackGizmo(attackHigh, -forwardSign, true);
                    DrawAttackGizmo(attackMid, -forwardSign, true);
                    DrawAttackGizmo(attackLow, -forwardSign, true);
                }
            }
            else if (CurrentAttack != null)
            {
                DrawAttackGizmo(CurrentAttack, CurrentIntent.horizontalSign, CurrentIntent.strikeDir == StrikeDirection.Backward);
            }
        }

        private void DrawAttackGizmo(AttackConfig attack, float sign, bool isBackward)
        {
            if (attack == null) return;
            Vector2 pos = (Vector2)transform.position + new Vector2(attack.hitboxOffset.x * sign, attack.hitboxOffset.y);

            Color c = attack.hitboxColor;
            if (isBackward)
            {
                c = new Color(c.r, c.g, c.b, c.a * 0.45f);
            }

            Gizmos.color = c;
            Gizmos.DrawWireCube(new Vector3(pos.x, pos.y, 0f), new Vector3(attack.hitboxSize.x, attack.hitboxSize.y, 0.1f));

            Gizmos.color = new Color(c.r, c.g, c.b, isBackward ? 0.08f : 0.18f);
            Gizmos.DrawCube(new Vector3(pos.x, pos.y, 0f), new Vector3(attack.hitboxSize.x, attack.hitboxSize.y, 0.05f));
        }
    }
}
