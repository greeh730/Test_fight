// Assets/Scripts/Fencing/FencingWheel.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fencing
{
    /// <summary>
    /// Главный контроллер механизма «Колесо фехтования».
    /// Отвечает за геометрию колеса (радиусы, 6 секторов атак, 2 мертвые зоны),
    /// ограничение перемещения шарика и генерацию события удара OnAttack.
    /// </summary>
    public class FencingWheel : MonoBehaviour
    {
        [Header("=== НАСТРОЙКИ В ИНСПЕКТОРЕ (РАЗМЕР, СКОРОСТЬ, ВЕС) ===")]
        [Tooltip("Если включено: параметры берутся прямо из этого Инспектора и НЕ перезаписываются PlayerPrefs / CombatSettingsManager при запуске игры!")]
        [SerializeField] private bool useInspectorSettings = true;

        [Tooltip("Размер UI-колеса на экране (радиус в экранных пикселях). Рекомендуется: 80 - 140")]
        [Range(40f, 250f)]
        [SerializeField] private float uiWheelRadius = 90f;

        [Tooltip("Скорость следования шарика за курсором мыши (больше = быстрее реакция)")]
        [Range(2f, 50f)]
        [SerializeField] private float orbFollowSpeed = 20f;

        [Tooltip("Вес / инерция шарика (меньше = резкий и отзывчивый, больше = тяжелый и инертный)")]
        [Range(0.05f, 3.0f)]
        [SerializeField] private float orbWeight = 0.2f;

        [Tooltip("Сопротивление движению / вязкое трение шарика")]
        [Range(0.5f, 20.0f)]
        [SerializeField] private float orbDrag = 4.0f;

        [Tooltip("Скорость плавного возврата шарика в центр при отпускании ЛКМ")]
        [Range(1.0f, 20.0f)]
        [SerializeField] private float orbReturnSpeed = 6.0f;

        [Tooltip("Радиус центральной свободной зоны (сброс флага удара, пока шарик внутри — удар не наносится)")]
        [Range(0.2f, 1.8f)]
        [SerializeField] private float inspectorFreeZoneRadius = 0.95f;

        [Tooltip("Размер иконок стрелок заклинаний на лепестках колеса (в пикселях)")]
        [Range(20f, 80f)]
        [SerializeField] private float arrowSize = 48f;

        [Header("Геометрия колеса")]
        [Tooltip("Внешний радиус колеса фехтования")]
        [SerializeField] private float wheelRadius = 2.0f;

        [Tooltip("Радиус центральной свободной зоны (сброс флага атаки)")]
        [SerializeField] private float freeZoneRadius = 0.95f;

        [Tooltip("Ширина каждого из 6 атакующих лепестков в градусах")]
        [Range(10f, 120f)]
        [SerializeField] private float attackSectorAngle = 60.0f;

        [Tooltip("Угол каждой мертвой зоны (верх и низ) в градусах — отключено (0)")]
        [Range(0f, 120f)]
        [SerializeField] private float deadZoneAngle = 0.0f;

        [Tooltip("Включить или отключить мертвые зоны — отключено")]
        [SerializeField] private bool enableDeadZone = false;

        [Tooltip("Центральные углы 6 атакующих секторов")]
        [SerializeField] private float[] attackAngles = new float[6];

        [Header("Секторы")]
        [Tooltip("Список 6 атакующих лепестков")]
        [SerializeField] private List<AttackSector> attackSectors = new List<AttackSector>();

        [Tooltip("Список мертвых зон (отключены)")]
        [SerializeField] private List<AttackSector> deadSectors = new List<AttackSector>();

        [Header("Ссылки")]
        [Tooltip("Ссылка на шарик удара (AttackOrb)")]
        [SerializeField] private AttackOrb orb;

        [Tooltip("Ссылка на UI колеса фехтования")]
        [SerializeField] private FencingWheelUI wheelUI;

        [Header("Срабатывание атаки")]
        [Tooltip("Порог срабатывания атаки (0 = сразу при выходе из свободной зоны, 1 = у внешней стенки)")]
        [Range(0f, 1f)]
        [SerializeField] private float attackTriggerThreshold = 0.0f;

        [Tooltip("Радиус хитбокса шарика, учитываемый при касании границы лепестка")]
        [Range(0f, 0.5f)]
        [SerializeField] private float orbHitboxRadius = 0.15f;

        [Tooltip("Учитывать ли радиус шарика (атака срабатывает, как только край шарика коснулся границы)")]
        [SerializeField] private bool includeOrbRadiusInTrigger = true;

        [Header("Состояние")]
        [Tooltip("Флаг предотвращения спама ударами. Сбрасывается при возврате в свободную зону.")]
        [SerializeField] private bool hasAttacked = false;

        [Tooltip("Флаг открытой панели настроек. Блокирует активацию шарика.")]
        [SerializeField] private bool isPanelOpen = false;

        [Header("Боевая стойка")]
        [Tooltip("Текущая стойка игрока: Фехтование (Красное колесо) или Магия (Синее колесо)")]
        [SerializeField] private StanceType currentStance = StanceType.Fencing;

        [Tooltip("Клавиша переключения стойки (по умолчанию Ctrl)")]
        [SerializeField] private KeyCode stanceToggleKey = KeyCode.LeftControl;
        public KeyCode StanceToggleKey { get => stanceToggleKey; set => stanceToggleKey = value; }

        /// <summary>
        /// Событие переключения боевой стойки (Фехтование / Магия).
        /// </summary>
        public event Action<StanceType> OnStanceChanged;

        public StanceType CurrentStance => currentStance;

        public void SetStance(StanceType newStance)
        {
            if (currentStance == newStance) return;
            currentStance = newStance;
            OnStanceChanged?.Invoke(currentStance);
        }

        public void ToggleStance()
        {
            SetStance(currentStance == StanceType.Fencing ? StanceType.Magic : StanceType.Fencing);
        }

        public bool ReadStanceToggleInput()
        {
            if (Combat.Dialogue.DialogueManager.Instance != null && Combat.Dialogue.DialogueManager.Instance.IsDialogueActive) return false;
            if (Combat.Tutorial.PlayerCapabilityController.Instance != null && !Combat.Tutorial.PlayerCapabilityController.Instance.CanToggleStance) return false;

#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                if (UnityEngine.InputSystem.Keyboard.current.leftCtrlKey.wasPressedThisFrame ||
                    UnityEngine.InputSystem.Keyboard.current.rightCtrlKey.wasPressedThisFrame)
                {
                    return true;
                }
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            try
            {
                if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl) || Input.GetKeyDown(stanceToggleKey))
                {
                    return true;
                }
            }
            catch {}
#endif
            return false;
        }

        public static bool ReadCtrlInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                if (UnityEngine.InputSystem.Keyboard.current.leftCtrlKey.wasPressedThisFrame ||
                    UnityEngine.InputSystem.Keyboard.current.rightCtrlKey.wasPressedThisFrame)
                {
                    return true;
                }
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            try
            {
                if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl))
                {
                    return true;
                }
            }
            catch {}
#endif
            return false;
        }

        /// <summary>
        /// Событие срабатывания удара при достижении внешней границы лепестка (только в стойке фехтования).
        /// Вызывается 1 раз до возвращения шарика в зеленую свободную зону.
        /// </summary>
        public event Action<AttackDirection> OnAttack;

        /// <summary>
        /// Событие действия в стойке магии (для механики заклинаний).
        /// </summary>
        public event Action<AttackDirection> OnMagicAction;

        [Header("Блокировка магии (Lockout)")]
        [Tooltip("Заблокирован ли ввод магии (штраф при ошибке ввода заклинания)")]
        [SerializeField] private bool isMagicLocked = false;
        private AttackDirection lastMagicDirection = AttackDirection.None;

        public bool IsMagicLocked
        {
            get => isMagicLocked;
            set => isMagicLocked = value;
        }

        /// <summary>
        /// Событие изменения геометрических параметров колеса.
        /// </summary>
        public event Action OnGeometryChanged;

        public GameObject SettingsPanel { get; set; }

        #region Свойства
        public float WheelRadius
        {
            get => wheelRadius;
            set
            {
                wheelRadius = Mathf.Max(0.5f, value);
                freeZoneRadius = Mathf.Clamp(freeZoneRadius, 0.05f, wheelRadius * 0.9f);
                OnGeometryChanged?.Invoke();
            }
        }

        public float FreeZoneRadius
        {
            get => freeZoneRadius;
            set
            {
                freeZoneRadius = Mathf.Clamp(value, 0.05f, wheelRadius * 0.9f);
                OnGeometryChanged?.Invoke();
            }
        }

        public float AttackSectorAngle
        {
            get => attackSectorAngle;
            set
            {
                attackSectorAngle = Mathf.Clamp(value, 10f, 120f);
                RebuildSectors();
                OnGeometryChanged?.Invoke();
            }
        }

        public float DeadZoneAngle
        {
            get => deadZoneAngle;
            set
            {
                deadZoneAngle = Mathf.Clamp(value, 0f, 120f);
                RebuildSectors();
                OnGeometryChanged?.Invoke();
            }
        }

        public bool EnableDeadZone
        {
            get => enableDeadZone;
            set
            {
                enableDeadZone = value;
                RebuildSectors();
                OnGeometryChanged?.Invoke();
            }
        }

        public float AttackTriggerThreshold
        {
            get => attackTriggerThreshold;
            set { attackTriggerThreshold = Mathf.Clamp01(value); OnGeometryChanged?.Invoke(); }
        }

        public float OrbHitboxRadius
        {
            get => orbHitboxRadius;
            set { orbHitboxRadius = Mathf.Clamp(value, 0f, 0.5f); OnGeometryChanged?.Invoke(); }
        }

        public bool IncludeOrbRadiusInTrigger
        {
            get => includeOrbRadiusInTrigger;
            set { includeOrbRadiusInTrigger = value; OnGeometryChanged?.Invoke(); }
        }

        public bool UseInspectorSettings { get => useInspectorSettings; set => useInspectorSettings = value; }
        public float UIWheelRadius { get => uiWheelRadius; set { uiWheelRadius = value; SyncInspectorSettings(); } }
        public float OrbFollowSpeed { get => orbFollowSpeed; set { orbFollowSpeed = value; SyncInspectorSettings(); } }
        public float OrbWeight { get => orbWeight; set { orbWeight = value; SyncInspectorSettings(); } }
        public float OrbDrag { get => orbDrag; set { orbDrag = value; SyncInspectorSettings(); } }
        public float OrbReturnSpeed { get => orbReturnSpeed; set { orbReturnSpeed = value; SyncInspectorSettings(); } }
        public float InspectorFreeZoneRadius { get => inspectorFreeZoneRadius; set { inspectorFreeZoneRadius = value; freeZoneRadius = value; SyncInspectorSettings(); } }
        public float ArrowSize { get => arrowSize; set { arrowSize = value; SyncInspectorSettings(); } }
        public FencingWheelUI WheelUI { get => wheelUI; set => wheelUI = value; }

        public float[] AttackAngles => attackAngles;
        public bool HasAttacked => hasAttacked;
        public bool IsPanelOpen { get => isPanelOpen; set => isPanelOpen = value; }
        public AttackOrb Orb { get => orb; set => orb = value; }
        public IReadOnlyList<AttackSector> AttackSectors => attackSectors;
        public IReadOnlyList<AttackSector> DeadSectors => deadSectors;
        #endregion

        /// <summary>
        /// Синхронизирует параметры колеса, шарика и UI напрямую из Инспектора.
        /// Гарантирует, что настройки в Инспекторе применяются и не перезаписываются PlayerPrefs.
        /// </summary>
        public void SyncInspectorSettings()
        {
            if (!useInspectorSettings) return;

            freeZoneRadius = Mathf.Clamp(Mathf.Max(inspectorFreeZoneRadius, 0.95f), 0.05f, wheelRadius * 0.9f);
            enableDeadZone = false;
            deadZoneAngle = 0f;
            attackSectorAngle = 60f;

            if (orb == null) orb = GetComponentInChildren<AttackOrb>();
            if (orb != null)
            {
                orb.FollowSpeed = orbFollowSpeed;
                orb.Weight = orbWeight;
                orb.Drag = orbDrag;
                orb.ReturnSpeed = orbReturnSpeed;
            }

            if (wheelUI == null)
            {
#if UNITY_6000_0_OR_NEWER
                wheelUI = UnityEngine.Object.FindAnyObjectByType<FencingWheelUI>();
#else
                wheelUI = UnityEngine.Object.FindObjectOfType<FencingWheelUI>();
#endif
            }

            if (wheelUI != null)
            {
                wheelUI.UIRadius = uiWheelRadius;
                wheelUI.ArrowIconSize = arrowSize;
            }

            RebuildSectors();
            OnGeometryChanged?.Invoke();
        }

        /// <summary>
        /// Возвращает дистанцию срабатывания удара.
        /// Триггерная зона воспроизводства атак — весь лепесток атаки (сразу за пределами свободной зоны).
        /// </summary>
        public float GetAttackTriggerDistance()
        {
            return freeZoneRadius;
        }

        public void AlignAllPetalsByRight()
        {
            deadZoneAngle = Mathf.Max(0f, 180f - (3f * attackSectorAngle));
            RebuildSectors();
            OnGeometryChanged?.Invoke();
        }

        public void FitRemainingPetalsByRight()
        {
            RebuildSectors();
            OnGeometryChanged?.Invoke();
        }

        private void Reset()
        {
            RebuildSectors();
            if (orb == null) orb = GetComponentInChildren<AttackOrb>();
        }

        private void OnValidate()
        {
            wheelRadius = Mathf.Max(0.5f, wheelRadius);
            inspectorFreeZoneRadius = Mathf.Clamp(inspectorFreeZoneRadius, 0.05f, wheelRadius * 0.9f);
            freeZoneRadius = inspectorFreeZoneRadius;
            enableDeadZone = false;
            deadZoneAngle = 0f;
            attackSectorAngle = 60f;

            RebuildSectors();

            if (useInspectorSettings)
            {
                if (orb == null) orb = GetComponentInChildren<AttackOrb>();
                if (orb != null)
                {
                    orb.FollowSpeed = orbFollowSpeed;
                    orb.Weight = orbWeight;
                    orb.Drag = orbDrag;
                    orb.ReturnSpeed = orbReturnSpeed;
                }

                if (wheelUI == null)
                {
#if UNITY_6000_0_OR_NEWER
                    wheelUI = UnityEngine.Object.FindAnyObjectByType<FencingWheelUI>();
#else
                    wheelUI = UnityEngine.Object.FindObjectOfType<FencingWheelUI>();
#endif
                }

                if (wheelUI != null)
                {
                    wheelUI.UIRadius = uiWheelRadius;
                    wheelUI.ArrowIconSize = arrowSize;
                }
            }
        }

        private void Awake()
        {
            if (inspectorFreeZoneRadius < 0.95f) inspectorFreeZoneRadius = 0.95f;
            if (freeZoneRadius < 0.95f) freeZoneRadius = 0.95f;

            if (orb == null)
            {
                orb = GetComponentInChildren<AttackOrb>();
            }

            if (useInspectorSettings)
            {
                SyncInspectorSettings();
            }
            else if (attackSectors == null || attackSectors.Count != 6)
            {
                RebuildSectors();
            }
        }

        private void Start()
        {
            if (useInspectorSettings)
            {
                SyncInspectorSettings();
            }
        }

        private void OnEnable()
        {
            Combat.Settings.CombatSettingsManager.OnSettingsChanged += HandleSettingsChanged;
            if (useInspectorSettings)
            {
                SyncInspectorSettings();
            }
            else if (Combat.Settings.CombatSettingsManager.Instance != null)
            {
                ApplySettings(Combat.Settings.CombatSettingsManager.Instance.CurrentSettings);
            }
        }

        private void OnDisable()
        {
            Combat.Settings.CombatSettingsManager.OnSettingsChanged -= HandleSettingsChanged;
        }

        private void HandleSettingsChanged(Combat.Settings.WheelSettingsData data)
        {
            if (useInspectorSettings)
            {
                SyncInspectorSettings();
                return;
            }
            ApplySettings(data);
        }

        public void ApplySettings(Combat.Settings.WheelSettingsData data)
        {
            if (useInspectorSettings)
            {
                SyncInspectorSettings();
                return;
            }

            if (data == null) return;

            if (orb == null) orb = GetComponentInChildren<AttackOrb>();
            if (orb != null)
            {
                orb.Weight = data.orbWeight;
                orb.FollowSpeed = data.orbFollowSpeed;
            }

            freeZoneRadius = Mathf.Clamp(data.freeZoneRadius, 0.05f, wheelRadius * 0.9f);
            enableDeadZone = data.enableDeadZone;

            if (!enableDeadZone)
            {
                deadZoneAngle = 0f;
                attackSectorAngle = 60f;
            }
            else
            {
                deadZoneAngle = Mathf.Clamp(data.deadZoneAngle, 0f, 90f);
                attackSectorAngle = Mathf.Clamp((180f - deadZoneAngle) / 3f, 15f, 60f);
            }

            RebuildSectors();
            OnGeometryChanged?.Invoke();
        }

        private void Update()
        {
            // Переключение стойки по нажатию Ctrl
            if (ReadStanceToggleInput())
            {
                ToggleStance();
            }

            if (orb == null) return;

            Vector2 orbPos = orb.LocalPosition;
            float currentDist = orbPos.magnitude;
            float triggerDist = GetAttackTriggerDistance();

            // 1. Возврат в зеленую свободную зону сбрасывает флаг атаки
            if (currentDist <= freeZoneRadius)
            {
                hasAttacked = false;
                lastMagicDirection = AttackDirection.None;
            }
            // 2. Как только шарик касается лепестка атаки (выходит за пределы свободной зоны)
            else
            {
                AttackDirection dir = GetAttackDirection(orbPos);
                if (dir != AttackDirection.None)
                {
                    // Удар мечом возможен ТОЛЬКО в стойке фехтования (Fencing)
                    if (currentStance == StanceType.Fencing)
                    {
                        if (Combat.Dialogue.DialogueManager.Instance != null && Combat.Dialogue.DialogueManager.Instance.IsDialogueActive) return;
                        if (Combat.Tutorial.PlayerCapabilityController.Instance != null && !Combat.Tutorial.PlayerCapabilityController.Instance.CanSwordAttack) return;

                        if (!hasAttacked)
                        {
                            hasAttacked = true;
                            OnAttack?.Invoke(dir);
                        }
                    }
                    else if (currentStance == StanceType.Magic)
                    {
                        if (Combat.Dialogue.DialogueManager.Instance != null && Combat.Dialogue.DialogueManager.Instance.IsDialogueActive) return;
                        if (Combat.Tutorial.PlayerCapabilityController.Instance != null && !Combat.Tutorial.PlayerCapabilityController.Instance.CanCastMagic) return;

                        // Если колесо магии заблокировано после ошибки, ввод не регистрируется
                        if (isMagicLocked) return;

                        if (!hasAttacked || dir != lastMagicDirection)
                        {
                            hasAttacked = true;
                            lastMagicDirection = dir;
                            OnMagicAction?.Invoke(dir);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Определяет направление атаки по локальным координатам шарика.
        /// Возвращает None, если шарик в свободной зоне, в мертвой зоне или вне лепестка.
        /// </summary>
        public AttackDirection GetAttackDirection(Vector2 localPos)
        {
            float dist = localPos.magnitude;
            if (dist <= freeZoneRadius || dist < 0.05f)
            {
                return AttackDirection.None;
            }

            float angle = Mathf.Atan2(localPos.y, localPos.x) * Mathf.Rad2Deg;

            // Проверка попадания в один из 6 атакующих секторов
            for (int i = 0; i < attackSectors.Count; i++)
            {
                if (attackSectors[i].ContainsAngle(angle))
                {
                    return attackSectors[i].direction;
                }
            }

            return AttackDirection.None;
        }

        /// <summary>
        /// Определяет направление прицеливания даже внутри лепестка до касания стенки.
        /// </summary>
        public AttackDirection GetAimDirection(Vector2 localPos)
        {
            if (localPos.magnitude < 0.05f) return AttackDirection.None;
            float angle = Mathf.Atan2(localPos.y, localPos.x) * Mathf.Rad2Deg;

            for (int i = 0; i < attackSectors.Count; i++)
            {
                if (attackSectors[i].ContainsAngle(angle)) return attackSectors[i].direction;
            }

            return AttackDirection.None;
        }

        /// <summary>
        /// Ограничивает локальную позицию шарика:
        /// 1) Внутри свободной зоны (dist <= freeZoneRadius) мертвых зон нет.
        /// 2) За пределами свободной зоны шарик не может проникнуть в мертвые зоны (плавно скользит вдоль границы).
        /// 3) Не может выйти за пределы внешнего круга wheelRadius.
        /// </summary>
        public Vector2 ClampPosition(Vector2 localPos)
        {
            if (localPos == Vector2.zero) return Vector2.zero;

            float dist = localPos.magnitude;
            if (dist <= 0.0001f) return Vector2.zero;

            // 1. Ограничение внешним радиусом колеса
            float clampedDist = Mathf.Min(dist, wheelRadius);
            Vector2 dir = localPos / dist;
            Vector2 pos = dir * clampedDist;

            // 2. Внутри зеленой свободной зоны мертвых зон нет
            if (clampedDist <= freeZoneRadius)
            {
                return pos;
            }

            // 3. За пределами свободной зоны проверяем попадание в клинья мертвых зон (верх и низ)
            if (!enableDeadZone || deadZoneAngle <= 0.1f || deadSectors == null || deadSectors.Count == 0)
            {
                return pos;
            }

            float angle = Mathf.Atan2(pos.y, pos.x) * Mathf.Rad2Deg;

            for (int i = 0; i < deadSectors.Count; i++)
            {
                AttackSector dead = deadSectors[i];
                if (dead.angleWidth <= 0.01f) continue;

                if (dead.ContainsAngle(angle))
                {
                    // Точка внутри мертвой зоны: вычисляем проекцию на ближайшую границу сектора
                    float delta = Mathf.DeltaAngle(dead.angleCenter, angle);
                    float halfWidth = dead.angleWidth * 0.5f;

                    float boundaryAngle = (delta >= 0f) ? (dead.angleCenter + halfWidth) : (dead.angleCenter - halfWidth);
                    float rad = boundaryAngle * Mathf.Deg2Rad;
                    Vector2 boundaryDir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                    Vector2 sidePoint = boundaryDir * clampedDist;

                    // Плавно скользим вдоль границы к ближайшему сектору атаки, не утягивая шарик внутрь
                    return sidePoint;
                }
            }

            return pos;
        }

        /// <summary>
        /// Проверяет, находится ли точка внутри мертвой зоны.
        /// Возвращает false, если мертвые зоны отключены.
        /// </summary>
        public bool IsInDeadZone(Vector2 localPos)
        {
            if (!enableDeadZone || deadZoneAngle <= 0.01f || deadSectors == null || deadSectors.Count == 0) return false;
            if (localPos.magnitude <= freeZoneRadius) return false;
            float angle = Mathf.Atan2(localPos.y, localPos.x) * Mathf.Rad2Deg;

            for (int i = 0; i < deadSectors.Count; i++)
            {
                if (deadSectors[i].ContainsAngle(angle)) return true;
            }
            return false;
        }

        /// <summary>
        /// Пересчитывает 6 атакующих секторов (и мертвые зоны, если они включены).
        /// </summary>
        [ContextMenu("Пересчитать секторы колеса")]
        public void RebuildSectors()
        {
            float effectiveDeadAngle = 0f;
            if (enableDeadZone)
            {
                float maxDeadAngle = Mathf.Max(0f, 180f - (3f * attackSectorAngle));
                effectiveDeadAngle = Mathf.Clamp(deadZoneAngle, 0f, maxDeadAngle);
            }
            else
            {
                attackSectorAngle = 60f;
                deadZoneAngle = 0f;
            }

            deadSectors = new List<AttackSector>();
            if (enableDeadZone && effectiveDeadAngle > 0.01f)
            {
                deadSectors.Add(new AttackSector(AttackDirection.None, 90f, effectiveDeadAngle, SectorType.Dead));
                deadSectors.Add(new AttackSector(AttackDirection.None, -90f, effectiveDeadAngle, SectorType.Dead));
            }

            if (attackAngles == null || attackAngles.Length != 6)
            {
                attackAngles = new float[6];
            }

            float w = attackSectorAngle;
            // 3 сектора справа (UpRight, Right, DownRight) и 3 слева (DownLeft, Left, UpLeft)
            attackAngles[0] = w;          // UpRight
            attackAngles[1] = 0f;         // Right
            attackAngles[2] = -w;         // DownRight
            attackAngles[3] = -180f + w;  // DownLeft
            attackAngles[4] = 180f;       // Left
            attackAngles[5] = 180f - w;   // UpLeft

            attackSectors = new List<AttackSector>
            {
                new AttackSector(AttackDirection.UpRight,   attackAngles[0], attackSectorAngle, SectorType.Attack),
                new AttackSector(AttackDirection.Right,     attackAngles[1], attackSectorAngle, SectorType.Attack),
                new AttackSector(AttackDirection.DownRight, attackAngles[2], attackSectorAngle, SectorType.Attack),
                new AttackSector(AttackDirection.DownLeft,  attackAngles[3], attackSectorAngle, SectorType.Attack),
                new AttackSector(AttackDirection.Left,      attackAngles[4], attackSectorAngle, SectorType.Attack),
                new AttackSector(AttackDirection.UpLeft,    attackAngles[5], attackSectorAngle, SectorType.Attack)
            };
        }

        public float GetSectorCenterAngle(AttackDirection direction)
        {
            if (attackSectors != null)
            {
                for (int i = 0; i < attackSectors.Count; i++)
                {
                    if (attackSectors[i].direction == direction)
                    {
                        return attackSectors[i].angleCenter;
                    }
                }
            }
            return direction.ToAngle();
        }

        public Vector2 GetDirectionVector(AttackDirection direction)
        {
            float angle = GetSectorCenterAngle(direction) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        #region Gizmos
        private void OnDrawGizmosSelected()
        {
            Vector3 center = transform.position;

            // 1. Внешний круг колеса (белый)
            Gizmos.color = Color.white;
            DrawWireCircle(center, wheelRadius, 64);

            // 2. Свободная зона (зеленый)
            Gizmos.color = Color.green;
            DrawWireCircle(center, freeZoneRadius, 32);

            // 3. Мертвые зоны (красно-серый) - только если включены
            if (enableDeadZone && deadZoneAngle > 0.01f && deadSectors != null)
            {
                Gizmos.color = new Color(0.9f, 0.25f, 0.25f, 0.85f);
                foreach (var dead in deadSectors)
                {
                    if (dead.angleWidth > 0.01f)
                    {
                        DrawSectorBoundaryGizmo(center, dead.angleCenter, dead.angleWidth, wheelRadius);
                    }
                }
            }

            // 4. Границы 6 атакующих лепестков (красный)
            Gizmos.color = new Color(1f, 0.4f, 0.4f, 0.9f);
            if (attackSectors != null)
            {
                foreach (var sec in attackSectors)
                {
                    DrawSectorBoundaryGizmo(center, sec.angleCenter, sec.angleWidth, wheelRadius);
                }
            }

            // 5. Позиция шарика (желтый круг) и вектор импульса
            if (orb != null)
            {
                Vector3 orbWorldPos = transform.TransformPoint(orb.LocalPosition);
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(orbWorldPos, 0.12f * (wheelRadius / 2f));

                Vector3 velWorld = transform.TransformDirection(orb.Velocity);
                if (velWorld.sqrMagnitude > 0.01f)
                {
                    Gizmos.DrawLine(orbWorldPos, orbWorldPos + velWorld * 0.15f);
                }
            }
        }

        private void DrawWireCircle(Vector3 center, float radius, int segments)
        {
            float step = 360f / segments;
            Vector3 prev = center + new Vector3(Mathf.Cos(0) * radius, Mathf.Sin(0) * radius, 0);

            for (int i = 1; i <= segments; i++)
            {
                float rad = i * step * Mathf.Deg2Rad;
                Vector3 next = center + new Vector3(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius, 0);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        private void DrawSectorBoundaryGizmo(Vector3 center, float centerAngle, float width, float radius)
        {
            float halfWidth = width * 0.5f;
            float leftAngle = (centerAngle - halfWidth) * Mathf.Deg2Rad;
            float rightAngle = (centerAngle + halfWidth) * Mathf.Deg2Rad;

            Vector3 leftDir = new Vector3(Mathf.Cos(leftAngle), Mathf.Sin(leftAngle), 0);
            Vector3 rightDir = new Vector3(Mathf.Cos(rightAngle), Mathf.Sin(rightAngle), 0);

            Gizmos.DrawLine(center + leftDir * freeZoneRadius, center + leftDir * radius);
            Gizmos.DrawLine(center + rightDir * freeZoneRadius, center + rightDir * radius);
        }
        #endregion
    }
}
