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

    [Serializable]
    public class ComboStepEvent : UnityEvent<int, bool> { }

    [DisallowMultipleComponent]
    public class PlayerCombatController2D : MonoBehaviour
    {
        [Header("--- Combo System Settings ---")]
        [Tooltip("Максимальное количество ударов в одной цепочке комбо")]
        [SerializeField] private int maxComboSteps = 3;

        [Tooltip("Время бездействия (в секундах), после которого комбо сбрасывается (> 10 сек)")]
        [SerializeField] private float comboResetTime = 10.5f;

        [Tooltip("Множитель времени замаха со 2-го шага комбо (меньше 1 = быстрее)")]
        [Range(0.3f, 1f)]
        [SerializeField] private float comboStartupMultiplier = 0.65f;

        [Tooltip("Множитель урона для завершающего удара комбо (Finisher)")]
        [Range(1f, 4f)]
        [SerializeField] private float finisherDamageMultiplier = 1.85f;

        [Tooltip("Множитель силы отталкивания для завершающего удара комбо")]
        [Range(1f, 4f)]
        [SerializeField] private float finisherKnockbackMultiplier = 2.2f;

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
        [SerializeField] private DirectionSequenceRecognizer sequenceRecognizer;

        [Header("--- Visualizer & Gizmos ---")]
        [SerializeField] private HitboxVisualizer2D visualizer;

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
        public event System.Action<ComboSequenceDefinition> onSequenceExecuted;
        public event System.Action onAttackHit;

        // Runtime State
        public CombatState CurrentState { get; private set; } = CombatState.Idle;
        public AttackConfig CurrentAttack { get; private set; }
        public float FacingDirection { get; private set; } = 1f;
        public int CurrentComboStep { get; private set; } = 1;
        public bool IsFinisher => CurrentComboStep >= maxComboSteps;
        public bool CanCancelIntoCombo => _canCancelIntoCombo;
        public float AttackDamageMultiplier { get; set; } = 1.0f;

        private Coroutine _attackRoutine;
        private Coroutine _hitstopRoutine;
        private readonly HashSet<Collider2D> _hitTargetsInCurrentSwing = new HashSet<Collider2D>();
        private readonly HashSet<IHurtboxTarget2D> _hitReceiversInCurrentSwing = new HashSet<IHurtboxTarget2D>();
        private readonly List<Collider2D> _overlapResults = new List<Collider2D>(16);
        private ContactFilter2D _contactFilter;

        private ComboSequenceDefinition _bufferedSequence;
        private bool _bufferedIsStale;
        private float _bufferedAttackTime = -10f;
        private float _lastAttackStartTime = -10f;
        private float _lastAttackFinishTime = -10f;
        private bool _canCancelIntoCombo;
        private bool _hasHitTargetInCurrentAttack;
        private bool _lastWasFinisher;
        private Rigidbody2D _rb;
        private SpriteRenderer _sr;
        private PlayerStamina2D _stamina;
        private PlayerBlockAndParry2D _blockParry;
        private Player.PlayerController2D _playerController;
        private Coroutine _airStallRoutine;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _sr = GetComponent<SpriteRenderer>();
            _stamina = GetComponent<PlayerStamina2D>();
            _blockParry = GetComponent<PlayerBlockAndParry2D>();
            _playerController = GetComponent<Player.PlayerController2D>();

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

            EnsureSequenceRecognizer();

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

        public void SetSequenceRecognizer(DirectionSequenceRecognizer recognizer)
        {
            if (sequenceRecognizer != null)
            {
                sequenceRecognizer.OnSequenceMatched -= OnSequenceMatched;
            }

            sequenceRecognizer = recognizer;

            if (sequenceRecognizer != null && isActiveAndEnabled)
            {
                sequenceRecognizer.OnSequenceMatched -= OnSequenceMatched;
                sequenceRecognizer.OnSequenceMatched += OnSequenceMatched;
            }
        }

        private void EnsureSequenceRecognizer()
        {
            if (vectorWheel == null)
            {
                vectorWheel = FindAnyObjectByType<VectorWheelController>();
            }

            if (sequenceRecognizer == null && vectorWheel != null)
            {
                sequenceRecognizer = vectorWheel.SequenceRecognizer;
            }
            if (sequenceRecognizer == null)
            {
                sequenceRecognizer = FindAnyObjectByType<DirectionSequenceRecognizer>();
            }

            if (sequenceRecognizer != null && isActiveAndEnabled)
            {
                sequenceRecognizer.OnSequenceMatched -= OnSequenceMatched;
                sequenceRecognizer.OnSequenceMatched += OnSequenceMatched;
            }
        }

        private void Start()
        {
            EnsureSequenceRecognizer();

            if (vectorWheel != null)
            {
                vectorWheel.SetStance(currentStance);
            }
        }

        private void OnEnable()
        {
            if (vectorWheel == null)
            {
                vectorWheel = FindAnyObjectByType<VectorWheelController>();
            }

            if (vectorWheel != null)
            {
                vectorWheel.onSwipeCompleted.RemoveListener(OnWheelSwipeCompleted);
                vectorWheel.onSwipeCompleted.AddListener(OnWheelSwipeCompleted);
            }

            EnsureSequenceRecognizer();
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

            if (sequenceRecognizer != null)
            {
                sequenceRecognizer.OnSequenceMatched -= OnSequenceMatched;
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

        private float _hitstunTimer = 0f;
        public bool IsHitstunned => _hitstunTimer > 0f;

        public void ApplyHitstun(float duration)
        {
            _hitstunTimer = Mathf.Max(_hitstunTimer, duration);
            CancelAttack();
            ClearSequenceBuffer();
        }

        private void UpdateComboTimers()
        {
            if (_hitstunTimer > 0f)
            {
                _hitstunTimer -= Time.deltaTime;
            }

            // Сброс комбо при долгом бездействии в Idle
            float idleTimeReference = _lastAttackFinishTime > 0f ? _lastAttackFinishTime : _lastAttackStartTime;
            if (CurrentComboStep > 1 && CurrentState == CombatState.Idle && Time.time - idleTimeReference > comboResetTime)
            {
                ResetCombo();
            }

            // Устаревание буфера связок
            if (_bufferedSequence != null && Time.time - _bufferedAttackTime > inputBufferDuration)
            {
                ClearSequenceBuffer();
            }
        }

        private void CheckInputBuffer()
        {
            if (_bufferedSequence != null && CanExecuteAttackNow())
            {
                var seq = _bufferedSequence;
                bool stale = _bufferedIsStale;
                ClearSequenceBuffer();
                ExecuteSequenceAttack(seq, stale);
            }
        }

        public bool CanExecuteAttackNow()
        {
            if (IsHitstunned) return false;
            if (_blockParry == null) _blockParry = GetComponent<PlayerBlockAndParry2D>();
            if (_blockParry != null && (_blockParry.IsBlocking || _blockParry.IsParrying || _blockParry.IsParryStaggered)) return false;
            if (CurrentState == CombatState.Idle) return true;
            if (_canCancelIntoCombo) return true;
            return false;
        }

        public void ClearBuffer()
        {
            ClearSequenceBuffer();
        }

        private void ClearSequenceBuffer()
        {
            _bufferedSequence = null;
            _bufferedIsStale = false;
            _bufferedAttackTime = -10f;
        }

        public void ResetCombo()
        {
            CurrentComboStep = 1;
            _lastWasFinisher = false;
            onComboStepChanged?.Invoke(CurrentComboStep, false);
            if (sequenceRecognizer != null)
            {
                sequenceRecognizer.ResetHistory();
            }
        }

        private void OnSequenceMatched(ComboSequenceDefinition seq, bool isStale)
        {
            if (seq == null) return;
            if (currentStance == CombatStance.Tactician) return; // В стойке тактика связки воина не активируются

            // Проверка стамины игрока: при истощении связки продолжать нельзя!
            if (_stamina != null && (_stamina.IsExhausted || _stamina.CurrentStamina <= 0f))
            {
                Debug.LogWarning($"<color=orange>[СТАМИНА НА НУЛЕ]</color> Недостаточно выносливости для проведения связки: <b>{seq.SequenceName}</b>!");
                return;
            }

            TryExecuteSequenceOrBuffer(seq, isStale);
        }

        public bool TryExecuteSequenceOrBuffer(ComboSequenceDefinition seq, bool isStale)
        {
            if (CanExecuteAttackNow())
            {
                ClearSequenceBuffer();
                return ExecuteSequenceAttack(seq, isStale);
            }

            _bufferedSequence = seq;
            _bufferedIsStale = isStale;
            _bufferedAttackTime = Time.time;
            return false;
        }

        private void OnWheelSwipeCompleted(Direction8 dir, Vector2 vector, float distance)
        {
            if (dir == Direction8.None) return;

            // В стойке Тактика свайп активирует способность арена-контроля
            if (currentStance == CombatStance.Tactician)
            {
                if (tacticianController == null)
                {
                    tacticianController = GetComponent<TacticianCombatController2D>() ?? gameObject.AddComponent<TacticianCombatController2D>();
                }

                if (tacticianController != null && !tacticianController.IsAbilityUnlocked(dir))
                {
                    Debug.LogWarning($"<color=orange>[ТАКТИК]</color> Способность {TacticianCombatController2D.GetAbilityName(dir)} заблокирована! Соберите Кристалл Победы для открытия.");
                    return;
                }

                if (_stamina != null && (_stamina.IsExhausted || !_stamina.CanAfford(18f)))
                {
                    Debug.LogWarning("<color=orange>[СТАМИНА НА НУЛЕ]</color> Недостаточно выносливости для способности Тактика!");
                    return;
                }

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

            // В Боевой Стойке (Normal): одиночный свайп (всего одна стрелка) НЕ выполняет атаку.
            // Персонаж не наносит ударов и не совершает выпадов, если не введена полная моушн-комбинация (2+ стрелок).
        }

        public bool ExecuteSequenceAttack(ComboSequenceDefinition seq, bool isStale, bool isComboChain = false)
        {
            if (!CanExecuteAttackNow() || seq == null) return false;

            if (_stamina != null)
            {
                _stamina.ConsumeForAction(seq.SequenceId, seq.StaminaCost);
            }

            // Направление удара определяется направлением связки (абсолютно на экране)
            float strikeSign = 0f;
            if (seq.RequiredDirections != null && seq.RequiredDirections.Count > 0)
            {
                for (int i = seq.RequiredDirections.Count - 1; i >= 0; i--)
                {
                    var d = seq.RequiredDirections[i];
                    if (d == Direction8.Right || d == Direction8.UpRight || d == Direction8.DownRight)
                    {
                        strikeSign = 1f;
                        break;
                    }
                    if (d == Direction8.Left || d == Direction8.UpLeft || d == Direction8.DownLeft)
                    {
                        strikeSign = -1f;
                        break;
                    }
                }
            }
            if (Mathf.Abs(strikeSign) < 0.01f)
            {
                strikeSign = FacingDirection;
            }

            if (Mathf.Abs(strikeSign) > 0.01f)
            {
                SetFacingDirection(strikeSign);
            }

            bool isChaining = isComboChain || (_attackRoutine != null) || (CurrentState != CombatState.Idle);

            if (_attackRoutine != null)
            {
                StopCoroutine(_attackRoutine);
                _attackRoutine = null;
                if (visualizer != null) visualizer.HideHitbox();
            }

            if (isChaining)
            {
                CurrentComboStep = (CurrentComboStep >= maxComboSteps) ? 1 : CurrentComboStep + 1;
            }
            else
            {
                float idleTimeRef = _lastAttackFinishTime > 0f ? _lastAttackFinishTime : _lastAttackStartTime;
                bool withinTime = (Time.time - idleTimeRef <= comboResetTime) && !_lastWasFinisher && (idleTimeRef > 0f);
                CurrentComboStep = withinTime ? ((CurrentComboStep >= maxComboSteps) ? 1 : CurrentComboStep + 1) : 1;
            }

            _lastAttackStartTime = Time.time;
            onSequenceExecuted?.Invoke(seq);
            _attackRoutine = StartCoroutine(SequenceAttackRoutine(seq, isStale, strikeSign));
            return true;
        }

        private IEnumerator SequenceAttackRoutine(ComboSequenceDefinition seq, bool isStale, float horizontalSign)
        {
            var baseAttack = seq.AttackData;
            CurrentAttack = baseAttack;
            _hitTargetsInCurrentSwing.Clear();
            _hitReceiversInCurrentSwing.Clear();
            _canCancelIntoCombo = false;
            _hasHitTargetInCurrentAttack = false;

            int thisAttackStep = CurrentComboStep;
            bool isFinisher = thisAttackStep >= maxComboSteps;

            if (vectorWheel != null)
            {
                vectorWheel.SetAttackPlaqueWithSequence(seq.SequenceName, seq.GlyphPattern, thisAttackStep, isStale);
            }
            onComboStepChanged?.Invoke(thisAttackStep, isFinisher);

            float speedMult = _stamina != null ? _stamina.ActionSpeedMultiplier : 1.0f;

            // 1. ФАЗА ЗАМАХА (STARTUP) — удары в комбо ускоряются
            CurrentState = CombatState.Startup;
            float startup = (thisAttackStep > 1 ? baseAttack.startupTime * comboStartupMultiplier : baseAttack.startupTime) / speedMult;
            yield return new WaitForSeconds(startup);

            // 2. АКТИВНАЯ ФАЗА (ACTIVE)
            CurrentState = CombatState.Active;
            float activeTimer = baseAttack.activeTime / speedMult;

            // Звук взмаха/удара клинком
            if (Combat.Audio.SoundManager.Instance != null)
            {
                Combat.Audio.SoundManager.Instance.PlayPlayerAttack();
            }

            // Выпад в направлении удара
            ApplySequenceLunge(horizontalSign, seq.LungeForce);

            float dmgMult = isFinisher ? finisherDamageMultiplier : (isStale ? 0.7f : 1.0f);
            float kbMult = isFinisher ? finisherKnockbackMultiplier : 1.0f;
            Vector2 boxSize = baseAttack.hitboxSize;
            Color boxColor = isStale ? new Color(0.85f, 0.45f, 0.45f, 0.85f) : (isFinisher ? new Color(1f, 0.2f, 0.2f, 0.95f) : baseAttack.hitboxColor);

            var effectiveAttack = new AttackConfig(
                seq.SequenceName + (isStale ? " [ПРИВЫКАНИЕ]" : (isFinisher ? " [ФИНИШЕР!]" : "")),
                baseAttack.targetedZones,
                baseAttack.hitboxOffset,
                boxSize,
                baseAttack.startupTime,
                baseAttack.activeTime,
                baseAttack.recoveryTime,
                baseAttack.damage * dmgMult * AttackDamageMultiplier,
                baseAttack.knockbackForce * kbMult,
                boxColor,
                launcher: seq.IsLauncher,
                stale: isStale,
                lunge: seq.LungeForce
            )
            {
                attacker = gameObject
            };

            while (activeTimer > 0f)
            {
                Vector2 boxCenter = GetHitboxCenter(effectiveAttack, horizontalSign);

                if (visualizer != null)
                {
                    visualizer.ShowHitbox(boxCenter, boxSize, boxColor, isFinisher, false);
                }

                CheckHitboxOverlapSequence(effectiveAttack, horizontalSign, boxCenter, boxSize, isFinisher, seq.IsLauncher, isStale);

                // Feature 2: Hit-Confirm Cancel!
                // При успешном попадании по врагу игрок может мгновенно прервать остаток активной фазы в забуферизованный приём
                if (_hasHitTargetInCurrentAttack && _bufferedSequence != null)
                {
                    if (visualizer != null) visualizer.HideHitbox();
                    _lastAttackFinishTime = Time.time;
                    _lastWasFinisher = isFinisher;
                    var nextSeq = _bufferedSequence;
                    bool nextStale = _bufferedIsStale;
                    ClearSequenceBuffer();
                    _attackRoutine = null;
                    ExecuteSequenceAttack(nextSeq, nextStale, isComboChain: true);
                    yield break;
                }

                activeTimer -= Time.deltaTime;
                yield return null;
            }

            if (visualizer != null)
            {
                visualizer.HideHitbox();
            }

            // 3. ФАЗА ВОССТАНОВЛЕНИЯ (RECOVERY)
            CurrentState = CombatState.Recovery;
            float recovery = baseAttack.recoveryTime / speedMult;
            float cancelOpenTime = recovery * recoveryCancelThreshold;
            float recoveryTimer = 0f;

            while (recoveryTimer < recovery)
            {
                recoveryTimer += Time.deltaTime;

                if (recoveryTimer >= cancelOpenTime || _hasHitTargetInCurrentAttack)
                {
                    _canCancelIntoCombo = true;

                    if (_bufferedSequence != null)
                    {
                        _lastAttackFinishTime = Time.time;
                        _lastWasFinisher = isFinisher;
                        var nextSeq = _bufferedSequence;
                        bool nextStale = _bufferedIsStale;
                        ClearSequenceBuffer();
                        _attackRoutine = null;
                        ExecuteSequenceAttack(nextSeq, nextStale, isComboChain: true);
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

        private void ApplySequenceLunge(float horizontalSign, float force)
        {
            if (_rb == null) _rb = GetComponent<Rigidbody2D>();
            if (_rb != null && Mathf.Abs(force) > 0.05f)
            {
                float speedMult = _stamina != null ? _stamina.ActionSpeedMultiplier : 1.0f;
                float multiplier = IsFinisher ? 1.4f : (CurrentComboStep > 1 ? 1.15f : 1.0f);
                _rb.linearVelocity = new Vector2(horizontalSign * force * multiplier * speedMult, _rb.linearVelocity.y);
            }
        }

        private void CheckHitboxOverlapSequence(AttackConfig effectiveAttack, float horizontalSign, Vector2 center, Vector2 size, bool isFinisher, bool isLauncher, bool isStale)
        {
            _overlapResults.Clear();
            int count = Physics2D.OverlapBox(center, size, 0f, _contactFilter, _overlapResults);

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
                        if (receiver != null && _hitReceiversInCurrentSwing.Contains(receiver)) continue;

                        _hitTargetsInCurrentSwing.Add(col);
                        if (receiver != null) _hitReceiversInCurrentSwing.Add(receiver);

                        OnTargetHitSuccess(isFinisher, false);

                        Vector2 knockbackDir = new Vector2(horizontalSign, isLauncher ? 3.5f : 1f).normalized;
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

                        OnTargetHitSuccess(isFinisher, false);

                        Vector2 knockbackDir = new Vector2(horizontalSign, isLauncher ? 3.5f : 1f).normalized;
                        target.TakeHit(effectiveAttack, effectiveAttack.targetedZones, center, knockbackDir);
                    }
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

        private void OnTargetHitSuccess(bool isFinisher = false, bool isCharged = false)
        {
            if (!_hasHitTargetInCurrentAttack)
            {
                _hasHitTargetInCurrentAttack = true;
                _canCancelIntoCombo = true;
                float hitstop = isCharged ? 0.10f : (isFinisher ? hitstopDuration * 1.8f : hitstopDuration);
                TriggerHitstop(hitstop);
                onAttackHit?.Invoke();

                // Air Stall: если игрок наносит удар в воздухе, притормаживаем его падение для джаггл-комбо
                if (_playerController != null && !_playerController.IsGrounded)
                {
                    ApplyPlayerAirStall(0.24f);
                }
            }
        }

        public void ApplyPlayerAirStall(float duration = 0.24f)
        {
            if (_airStallRoutine != null) StopCoroutine(_airStallRoutine);
            _airStallRoutine = StartCoroutine(AirStallRoutine(duration));
        }

        private IEnumerator AirStallRoutine(float duration)
        {
            if (_rb == null) _rb = GetComponent<Rigidbody2D>();
            if (_rb == null) yield break;

            float origGravity = _rb.gravityScale;
            _rb.gravityScale = 0f;
            // Сглаживаем и приподнимаем вертикальную скорость
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, Mathf.Max(0.4f, _rb.linearVelocity.y * 0.1f));

            float el = 0f;
            while (el < duration)
            {
                el += Time.deltaTime;
                if (_rb != null && _rb.linearVelocity.y < 0f)
                {
                    _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 0f);
                }
                yield return null;
            }

            if (_rb != null)
            {
                _rb.gravityScale = origGravity;
            }
            _airStallRoutine = null;
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
            if (CurrentAttack != null)
            {
                DrawAttackGizmo(CurrentAttack, FacingDirection);
            }
        }

        private void DrawAttackGizmo(AttackConfig attack, float sign)
        {
            if (attack == null) return;
            Vector2 pos = (Vector2)transform.position + new Vector2(attack.hitboxOffset.x * sign, attack.hitboxOffset.y);

            Gizmos.color = attack.hitboxColor;
            Gizmos.DrawWireCube(new Vector3(pos.x, pos.y, 0f), new Vector3(attack.hitboxSize.x, attack.hitboxSize.y, 0.1f));

            Gizmos.color = new Color(attack.hitboxColor.r, attack.hitboxColor.g, attack.hitboxColor.b, 0.18f);
            Gizmos.DrawCube(new Vector3(pos.x, pos.y, 0f), new Vector3(attack.hitboxSize.x, attack.hitboxSize.y, 0.05f));
        }
    }
}
