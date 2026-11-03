// Assets/Scripts/FocusManager.cs
using System;
using System.Collections;
using System.Collections.Generic;
using Fencing;
using UnityEngine;

/// <summary>
/// Менеджер ресурса шкалы фокуса (Focus).
/// Синглтон на сцене. Отвечает за накопление ресурса (при парировании и ударах),
/// расход фокуса, активацию замедления времени (timeScale), зума камеры и виньетки при ПКМ по игроку.
/// </summary>
public class FocusManager : MonoBehaviour
{
    private static FocusManager instance;

    public static FocusManager Instance
    {
        get
        {
            if (instance == null)
            {
#if UNITY_6000_0_OR_NEWER
                instance = FindAnyObjectByType<FocusManager>();
#else
                instance = FindObjectOfType<FocusManager>();
#endif
                if (instance == null)
                {
                    GameObject go = new GameObject("FocusManager");
                    instance = go.AddComponent<FocusManager>();
                }
            }
            return instance;
        }
        private set => instance = value;
    }

    [Header("Параметры ресурса фокуса")]
    [Tooltip("Максимальный объем шкалы фокуса")]
    public float maxFocus = 100f;

    [Tooltip("Порог шкалы фокуса для активации режима (по умолчанию 100)")]
    public float activationThreshold = 100f;

    [Tooltip("Текущее значение шкалы фокуса (0..maxFocus)")]
    public float focusPercent = 100f;         // 0..maxFocus

    [Tooltip("Активен ли режим фокуса в данный момент")]
    public bool isFocusActive = false;
    public bool IsFocusActive => isFocusActive;

    [Header("Параметры состояния фокуса")]
    [Tooltip("Замедление времени в режиме фокуса (0.1–0.3)")]
    [Range(0.05f, 1f)]
    public float timeScaleInFocus = 0.2f;         // [0.1–0.3]

    [Tooltip("Множитель зума камеры в фокусе (1.3 = +30% приближение)")]
    public float cameraZoomMultiplier = 1.3f;      // +30%

    [Tooltip("Длительность перехода в секундах реального времени")]
    public float transitionDuration = 0.2f;

    [Tooltip("Скорость расхода фокуса (единиц в секунду реального времени)")]
    public float focusDrainRate = 20f;             // ед/сек реального времени

    [Tooltip("Количество фокуса, накапливаемое при успешном ударе мечом")]
    public float focusGainOnHit = 15f;

    [Tooltip("Количество фокуса, накапливаемое при успешном парировании")]
    public float focusGainOnParry = 35f;

    [Header("UI Виньетка")]
    [Tooltip("CanvasGroup полноэкранной виньетки")]
    [SerializeField] private CanvasGroup vignetteCanvasGroup;

    [Header("Круг замаха (CircleCharge)")]
    [Tooltip("Префаб круга замаха для спавна при входе в фокус")]
    [SerializeField] private GameObject circleChargePrefab;

    private CircleCharge activeCircleCharge;
    private StrikeTrajectory activeStrikeTrajectory;

    [Header("Слабые точки (WeakPointSpawner)")]
    [Tooltip("Спавнер слабых точек на врагах в радиусе")]
    [SerializeField] private WeakPointSpawner weakPointSpawner;

    [Header("Параметры замаха (Tension)")]
    [Tooltip("Дистанция натяжения замаха мышью в world units")]
    public float maxStretchDistance = 2.5f;

    [Tooltip("Скорость возврата натяжения к центру")]
    public float tensionReturnSpeed = 8f;

    [Tooltip("Порог максимального замаха (0..1), при котором разрешен удар по ЛКМ")]
    [Range(0.5f, 1f)]
    public float maxTensionThreshold = 0.85f; // порог максимального замаха

    [Tooltip("Минимальное натяжение для выпуска удара (устарело, см. maxTensionThreshold)")]
    public float minTensionToStrike = 0.85f;  // для обратной совместимости

    [Header("Параметры удара вдоль линии (Strike)")]
    private Vector2 lockedStrikeDir;          // направление, зафиксированное в момент ЛКМ
    private Vector3 lockedStrikeOrigin;       // точка старта удара (край игрока)
    private bool strikeExecuting = false;
    private float strikeTimer = 0f;

    [Tooltip("Длительность анимации удара в секундах реального времени")]
    public float strikeDuration = 0.2f;       // длительность анимации удара

    [Tooltip("Дальность удара в world units")]
    public float strikeRange = 6f;            // дальность удара в world units

    [Tooltip("Ширина (толщина) зоны поражения удара")]
    public float strikeWidth = 1.2f;

    [Tooltip("Урон нелетального удара по слабым точкам или телу врага")]
    public float normalStrikeDamage = 35f;

    [Tooltip("Штраф фокуса при промахе")]
    public float missPenalty = 25f;

    [Tooltip("Оставаться в фокусе после нелетального (non-lethal) удара, если шкала не кончилась")]
    public bool continueFocusAfterHit = false;

    [Header("Ссылка на контроллер игрока")]
    [SerializeField] private Combat.Player.PlayerFocusController playerFocusController;

    [Header("Стойки и блокировки")]
    [Tooltip("Ссылка на колесо фехтования для отслеживания текущей боевой стойки")]
    [SerializeField] private FencingWheel fencingWheel;

    private Collider2D playerCollider;
    private float focusActiveTimer = 0f;

    public Vector2 LockedStrikeDir => lockedStrikeDir;
    public Vector3 LockedStrikeOrigin => lockedStrikeOrigin;
    public bool IsStrikeExecuting => strikeExecuting;
    public float StrikeTimer => strikeTimer;
    public Collider2D PlayerCollider => playerCollider;
    public CircleCharge ActiveCircleCharge => activeCircleCharge;
    public StrikeTrajectory ActiveStrikeTrajectory => activeStrikeTrajectory;
    public FencingWheel FencingWheelReference { get => fencingWheel; set => fencingWheel = value; }

    /// <summary>
    /// Проверяет, активна ли стойка магии (в стойке магии вход в режим фокуса на ПКМ запрещен).
    /// </summary>
    public bool IsMagicStanceActive()
    {
        if (fencingWheel == null)
        {
#if UNITY_6000_0_OR_NEWER
            fencingWheel = FindAnyObjectByType<FencingWheel>();
#else
            fencingWheel = FindObjectOfType<FencingWheel>();
#endif
        }

        if (fencingWheel != null)
        {
            return fencingWheel.CurrentStance == StanceType.Magic;
        }

        return false;
    }

    /// <summary>
    /// Событие изменения шкалы фокуса для UI и других систем.
    /// Передает актуальный процент (0..100).
    /// </summary>
    public event Action<float> OnFocusChanged;

    private Coroutine vignetteRoutine;

    /// <summary>
    /// Применяет параметры фокуса из инспектора игрока (PlayerFocusController).
    /// </summary>
    public void ApplyPlayerConfig(Combat.Player.PlayerFocusController config)
    {
        if (config == null) return;
        playerFocusController = config;

        maxFocus = config.MaxFocus;
        activationThreshold = config.ActivationThreshold;
        focusDrainRate = config.FocusDrainRate;
        focusGainOnHit = config.FocusGainOnHit;
        focusGainOnParry = config.FocusGainOnParry;

        timeScaleInFocus = config.TimeScaleInFocus;
        cameraZoomMultiplier = config.CameraZoomMultiplier;
        transitionDuration = config.TransitionDuration;

        maxStretchDistance = config.MaxStretchDistance;
        maxTensionThreshold = config.MaxTensionThreshold;
        tensionReturnSpeed = config.TensionReturnSpeed;

        strikeRange = config.StrikeRange;
        strikeWidth = config.StrikeWidth;
        strikeDuration = config.StrikeDuration;
        normalStrikeDamage = config.NormalStrikeDamage;
        missPenalty = config.MissPenalty;
        continueFocusAfterHit = config.ContinueFocusAfterHit;

        playerHoverRadius = config.PlayerHoverRadius;

        if (focusPercent > maxFocus)
        {
            focusPercent = maxFocus;
            OnFocusChanged?.Invoke(focusPercent);
        }
    }

    /// <summary>
    /// Автоматически находит PlayerFocusController на игроке в сцене и синхронизирует настройки.
    /// </summary>
    public void EnsurePlayerConfig()
    {
        if (playerFocusController != null)
        {
            ApplyPlayerConfig(playerFocusController);
            return;
        }

        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            var pfc = playerObj.GetComponent<Combat.Player.PlayerFocusController>();
            if (pfc != null)
            {
                ApplyPlayerConfig(pfc);
            }
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        EnsurePlayerConfig();
        EnsureVignetteReference();
        EnsurePlayerCollider();
    }

    private void Start()
    {
        EnsurePlayerConfig();
        EnsurePlayerCollider();
    }

    private void OnDestroy()
    {
        if (isFocusActive)
        {
            // Восстановление нормального течения времени при уничтожении
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }

        DespawnCircleCharge();

        if (instance == this)
        {
            instance = null;
        }
    }

    private void OnDisable()
    {
        if (isFocusActive)
        {
            ExitFocus();
        }
        DespawnCircleCharge();
    }

    private Coroutine cameraZoomRoutine;
    private float defaultCameraSize = 5f;
    private bool defaultCameraSizeCached = false;

    private void SetCameraZoom(float multiplier, float duration)
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        if (!defaultCameraSizeCached)
        {
            defaultCameraSize = cam.orthographicSize;
            defaultCameraSizeCached = true;
        }

        float targetSize = defaultCameraSize / Mathf.Max(0.1f, multiplier);

        if (duration <= 0.001f)
        {
            if (cameraZoomRoutine != null) StopCoroutine(cameraZoomRoutine);
            cam.orthographicSize = targetSize;
            return;
        }

        if (cameraZoomRoutine != null) StopCoroutine(cameraZoomRoutine);
        cameraZoomRoutine = StartCoroutine(CameraZoomRoutine(cam, targetSize, duration));
    }

    private IEnumerator CameraZoomRoutine(Camera cam, float targetSize, float duration)
    {
        float startSize = cam.orthographicSize;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            cam.orthographicSize = Mathf.Lerp(startSize, targetSize, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        cam.orthographicSize = targetSize;
        cameraZoomRoutine = null;
    }

    [Header("Зона наведения")]
    [Tooltip("Радиус вокруг центра игрока для удобного наведения курсора")]
    public float playerHoverRadius = 1.8f;

    public static bool? mockRMB = null;
    public static bool? mockRMBDown = null;

    /// <summary>
    /// Универсальное считывание зажатия ПКМ (Input System + Legacy).
    /// </summary>
    public static bool ReadRMB()
    {
        if (mockRMB.HasValue) return mockRMB.Value;

#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Mouse.current != null)
        {
            if (UnityEngine.InputSystem.Mouse.current.rightButton.isPressed) return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
        try
        {
            if (Input.GetMouseButton(1)) return true;
        }
        catch {}
#endif
        return false;
    }

    /// <summary>
    /// Универсальное считывание клика ПКМ в текущем кадре (Input System + Legacy).
    /// </summary>
    public static bool ReadRMBDown()
    {
        if (mockRMBDown.HasValue) return mockRMBDown.Value;

#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Mouse.current != null)
        {
            if (UnityEngine.InputSystem.Mouse.current.rightButton.wasPressedThisFrame) return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
        try
        {
            if (Input.GetMouseButtonDown(1)) return true;
        }
        catch {}
#endif
        return false;
    }

    public static Vector3? mockMousePosition = null;

    /// <summary>
    /// Универсальное считывание позиции курсора мыши (Input System + Legacy).
    /// </summary>
    public static Vector3 ReadMousePosition()
    {
        if (mockMousePosition.HasValue) return mockMousePosition.Value;

#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Mouse.current != null)
        {
            Vector2 pos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            return new Vector3(pos.x, pos.y, 0f);
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
        try
        {
            return Input.mousePosition;
        }
        catch {}
#endif
        return Vector3.zero;
    }

    public static bool? mockLMBDown = null;
    public static bool? mockLMB = null;
    public static bool? mockLMBUp = null;
    public static Vector3? mockMouseWorldPosition = null;

    /// <summary>
    /// Универсальное получение позиции курсора в мировых 2D-координатах.
    /// </summary>
    public static Vector2 GetMouseWorldPosition()
    {
        if (mockMouseWorldPosition.HasValue) return (Vector2)mockMouseWorldPosition.Value;

        var cam = Camera.main;
        if (cam == null) return Vector2.zero;

        Vector3 screenPos = ReadMousePosition();
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
        return new Vector2(world.x, world.y);
    }

    /// <summary>
    /// Универсальное считывание зажатия ЛКМ (Input System + Legacy).
    /// </summary>
    public static bool ReadLMB()
    {
        if (mockLMB.HasValue) return mockLMB.Value;

#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Mouse.current != null)
        {
            if (UnityEngine.InputSystem.Mouse.current.leftButton.isPressed) return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
        try
        {
            if (Input.GetMouseButton(0)) return true;
        }
        catch {}
#endif
        return false;
    }

    /// <summary>
    /// Универсальное считывание нажатия ЛКМ в текущем кадре (Input System + Legacy).
    /// </summary>
    public static bool ReadLMBDown()
    {
        if (mockLMBDown.HasValue) return mockLMBDown.Value;

#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Mouse.current != null)
        {
            if (UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame) return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
        try
        {
            if (Input.GetMouseButtonDown(0)) return true;
        }
        catch {}
#endif
        return false;
    }

    /// <summary>
    /// Универсальное считывание отпускания ЛКМ в текущем кадре (Input System + Legacy).
    /// </summary>
    public static bool ReadLMBUp()
    {
        if (mockLMBUp.HasValue) return mockLMBUp.Value;

#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Mouse.current != null)
        {
            if (UnityEngine.InputSystem.Mouse.current.leftButton.wasReleasedThisFrame) return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
        try
        {
            if (Input.GetMouseButtonUp(0)) return true;
        }
        catch {}
#endif
        return false;
    }

    private void Update()
    {
#if UNITY_EDITOR
        // Быстрое пополнение фокуса клавишей F для мгновенного тестирования
        try
        {
            if (Input.GetKeyDown(KeyCode.F))
            {
                AddFocus(100f);
            }
        }
        catch {}
#endif

        // Если удар в процессе исполнения — обрабатываем вытягивание разреза и проверку попадания
        if (strikeExecuting)
        {
            UpdateStrikeExecution();
            return;
        }

        // Если активна стойка магии — вход в фокус и взаимодействие с ПКМ запрещены
        if (IsMagicStanceActive())
        {
            if (isFocusActive)
            {
                ExitFocus();
            }
            return;
        }

        // Кейс 7: Ввод блокируется, если Time.timeScale был изменён извне (например, другим эффектом)
        if (!isFocusActive)
        {
            if (!Mathf.Approximately(Time.timeScale, 1.0f))
            {
                return;
            }
        }
        else
        {
            if (!Mathf.Approximately(Time.timeScale, timeScaleInFocus))
            {
                return;
            }

            var p = GetPlayer();
            if (p == null)
            {
                ExitFocus();
                return;
            }
        }

        if (isFocusActive)
        {
            focusActiveTimer += Time.unscaledDeltaTime;
        }

        bool rmbHeld = ReadRMB();
        bool rmbDown = ReadRMBDown();
        bool lmbDown = ReadLMBDown();

        // 1. Вход в фокус: если курсор над игроком и ПКМ нажата или удерживается
        if (!isFocusActive)
        {
            if ((rmbDown || rmbHeld) && CanEnterFocus())
            {
                EnterFocus();
            }
        }
        else
        {
            float tension = (activeCircleCharge != null) ? activeCircleCharge.GetTension01() : 0f;

            // 2. Обязательное условие удара: при максимальном замахе во время удержания ПКМ нажать ЛКМ
            if (rmbHeld && lmbDown)
            {
                if (tension >= maxTensionThreshold)
                {
                    Vector2 strikeDir = (activeStrikeTrajectory != null)
                        ? activeStrikeTrajectory.GetStrikeDirection()
                        : GetFallbackStrikeDirection();

                    ExecuteStrike(strikeDir);
                    return;
                }
                else
                {
                    Debug.Log($"<color=#FFAA00>[FOCUS]</color> Нажата ЛКМ, но замах еще не максимальный ({tension:F2} < {maxTensionThreshold:F2}). Натяните курсор сильнее!");
                    var p = GetPlayer();
                    Vector3 spawnPos = (p != null) ? p.transform.position + Vector3.up * 1.5f : transform.position + Vector3.up * 1.5f;
                    Combat.Common.CombatFloatingText.Spawn(spawnPos, "Натяните курсор сильнее!", new Color(1f, 0.85f, 0.2f, 1f), 0.9f);
                }
            }

            // 3. Отпускание ПКМ без нажатия ЛКМ — мягкая отмена фокуса без выполнения удара
            if (!rmbHeld && focusActiveTimer >= 0.05f)
            {
                Debug.Log("<color=#888888>[FOCUS]</color> ПКМ отпущена без удара по ЛКМ — отмена фокуса без удара.");
                ExitFocus();
                return;
            }
        }

        // 3. Расход ресурса фокуса в активном режиме (используется Time.unscaledDeltaTime)
        if (isFocusActive)
        {
            if (!strikeExecuting)
            {
                EnsureWeakPointSpawner();
                if (weakPointSpawner != null)
                {
                    var p = GetPlayer();
                    Vector3 origin = (p != null) ? p.transform.position : transform.position;
                    weakPointSpawner.UpdateActiveWeakPoints(origin);
                }
            }

            focusPercent -= focusDrainRate * Time.unscaledDeltaTime;

            // Кейс 4: Шкала дошла до 0 -> принудительный ExitFocus() без штрафа
            if (focusPercent <= 0f)
            {
                focusPercent = 0f;
                ExitFocus();
            }

            OnFocusChanged?.Invoke(focusPercent);
        }
    }

    /// <summary>
    /// Проверяет, находится ли курсор мыши над коллайдером игрока или в зоне наведения.
    /// </summary>
    public bool IsCursorOverPlayer()
    {
        var cam = Camera.main;
        if (cam == null) return false;

        Vector3 mouseScreen = ReadMousePosition();
        Vector3 mouseWorld = cam.ScreenToWorldPoint(mouseScreen);
        Vector2 mousePos2D = new Vector2(mouseWorld.x, mouseWorld.y);

        // 1. Прямая проверка коллайдеров
        Collider2D hit = Physics2D.OverlapPoint(mousePos2D);
        if (hit != null && IsPlayerCollider(hit))
        {
            return true;
        }

        var hits = Physics2D.OverlapPointAll(mousePos2D);
        foreach (var h in hits)
        {
            if (h != null && IsPlayerCollider(h))
            {
                return true;
            }
        }

        // 2. Проверка по границам коллайдера и радиусу наведения
        var player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            var col = player.GetComponent<Collider2D>();
            if (col != null)
            {
                Bounds b = col.bounds;
                b.Expand(0.8f);
                if (b.Contains(new Vector3(mousePos2D.x, mousePos2D.y, b.center.z)))
                {
                    return true;
                }
            }

            float dist = Vector2.Distance(mousePos2D, (Vector2)player.transform.position);
            if (dist <= playerHoverRadius)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsPlayerCollider(Collider2D col)
    {
        if (col == null) return false;
        if (col.CompareTag("Player")) return true;
        if (col.GetComponentInParent<FencingPlayer>() != null) return true;
        return false;
    }

    /// <summary>
    /// Получение ссылки на Transform игрока в сцене (по тегу Player или FencingPlayer).
    /// </summary>
    public Transform GetPlayer()
    {
        var pObj = GameObject.FindWithTag("Player");
        if (pObj != null) return pObj.transform;

        var fp = FindAnyObjectByType<FencingPlayer>();
        if (fp != null) return fp.transform;

        return transform;
    }

    /// <summary>
    /// Кэширует Collider2D игрока или ищет в дочерних объектах с fallback-логикой.
    /// </summary>
    public void EnsurePlayerCollider()
    {
        if (playerCollider != null) return;

        var pObj = GameObject.FindWithTag("Player");
        if (pObj != null)
        {
            playerCollider = pObj.GetComponentInChildren<Collider2D>();
            if (playerCollider == null)
            {
                playerCollider = pObj.GetComponent<Collider2D>();
            }
        }

        if (playerCollider == null)
        {
            var fp = FindAnyObjectByType<FencingPlayer>();
            if (fp != null)
            {
                playerCollider = fp.GetComponentInChildren<Collider2D>();
                if (playerCollider == null)
                {
                    playerCollider = fp.GetComponent<Collider2D>();
                }
            }
        }

        if (playerCollider == null)
        {
            Debug.LogWarning("<color=#FFAA00>[FOCUS]</color> playerCollider не найден! Убедитесь, что у объекта игрока есть тег 'Player' и Collider2D.");
        }
    }

    /// <summary>
    /// Проверяет, находится ли точка в мировых координатах внутри коллайдера(ов) игрока (слой Player или fallback).
    /// </summary>
    public bool IsInsidePlayerHitbox(Vector2 mouseWorld)
    {
        int playerLayerMask = LayerMask.GetMask("Player");
        if (playerLayerMask != 0)
        {
            Collider2D hit = Physics2D.OverlapPoint(mouseWorld, playerLayerMask);
            if (hit != null) return true;
        }

        EnsurePlayerCollider();
        if (playerCollider != null)
        {
            return playerCollider.OverlapPoint(mouseWorld);
        }

        var pc = GetPlayer();
        Vector3 playerPos = (pc != null) ? pc.transform.position : Vector3.zero;
        return Vector2.Distance(mouseWorld, (Vector2)playerPos) < 0.5f;
    }

    /// <summary>
    /// Возвращает true, если накоплен необходимый ресурс фокуса (>= activationThreshold),
    /// курсор наведен на игрока, и игра не находится во внешней паузе.
    /// </summary>
    public bool CanEnterFocus()
    {
        // Ввод блокируется активным диалогом
        if (Combat.Dialogue.DialogueManager.Instance != null && Combat.Dialogue.DialogueManager.Instance.IsDialogueActive) return false;

        // Проверка ограничений способностей (Обучение)
        if (Combat.Tutorial.PlayerCapabilityController.Instance != null && !Combat.Tutorial.PlayerCapabilityController.Instance.CanUseFocus) return false;

        // В стойке магии вход в режим фокуса запрещен
        if (IsMagicStanceActive()) return false;

        // Кейс 7: Ввод блокируется, если Time.timeScale был изменен извне (например, другим эффектом)
        if (!Mathf.Approximately(Time.timeScale, 1.0f)) return false;

        // Вход разрешен только при достижении порога активации (по дефолту 100)
        if (focusPercent < activationThreshold) return false;
        if (isFocusActive) return false;

        var p = GetPlayer();
        if (p == null) return false;

        return true;
    }

    [ContextMenu("Заполнить фокус на 100%")]
    public void FillFocus100()
    {
        AddFocus(100f);
    }

    /// <summary>
    /// Активация режима фокуса: замедление времени, зум камеры и появление виньетки.
    /// </summary>
    public void EnterFocus()
    {
        if (isFocusActive) return;

        isFocusActive = true;
        focusActiveTimer = 0f;

        // Замедление времени
        Time.timeScale = timeScaleInFocus;
        Time.fixedDeltaTime = 0.02f * Time.timeScale; // ВАЖНО для плавной физики при timeScale < 1

        // Зум камеры (приближение к игроку)
        SetCameraZoom(cameraZoomMultiplier, transitionDuration);

        // Показать виньетку (alpha 0.5 через CanvasGroup плавно)
        SetVignetteAlpha(0.5f, transitionDuration);

        // Спавн круга замаха над игроком
        SpawnCircleCharge();

        // Спавн слабых точек на врагах в радиусе
        SpawnWeakPoints();

        Debug.Log($"<color=#00FFFF>[FOCUS]</color> Вход в фокус! TimeScale: {timeScaleInFocus}, Zoom: {cameraZoomMultiplier}x");
    }

    /// <summary>
    /// Выход из режима фокуса: восстановление нормального времени, камеры, скрытие виньетки
    /// и полная очистка всех объектов (круг замаха, слабые точки, трейлы) и флагов.
    /// </summary>
    public void ExitFocus()
    {
        if (!isFocusActive) return;

        isFocusActive = false;

        strikeExecuting = false;
        strikeTimer = 0f;

        // Кейс 8: Уничтожение круга замаха и обнуление ссылки
        DespawnCircleCharge();

        // Кейс 8: Уничтожение всех слабых точек и очистка коллекций
        ClearWeakPoints();

        // Восстановление нормального течения времени
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        // Возврат камеры к базовому масштабу
        SetCameraZoom(1f, transitionDuration);

        // Скрыть виньетку (alpha -> 0)
        SetVignetteAlpha(0f, transitionDuration);

        Debug.Log("<color=#FFAA00>[FOCUS]</color> Выход из фокуса. Время, камера и объекты полностью очищены.");
    }

    private Vector2 GetFallbackStrikeDirection()
    {
        var player = GetPlayer();
        Vector3 playerPos = player != null ? player.transform.position : transform.position;
        Vector2 mouseWorld = GetMouseWorldPosition();
        Vector2 pullDir = mouseWorld - (Vector2)playerPos;
        if (pullDir.sqrMagnitude > 0.0001f)
        {
            return -pullDir.normalized;
        }
        return Vector2.right;
    }

    /// <summary>
    /// Запуск выполнения удара вдоль белой линии траектории.
    /// </summary>
    private void ExecuteStrike(Vector2 dir)
    {
        strikeExecuting = true;
        strikeTimer = 0f;
        lockedStrikeDir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;

        var player = GetPlayer();
        Vector3 playerPos = player != null ? player.transform.position : transform.position;
        lockedStrikeOrigin = playerPos + (Vector3)(lockedStrikeDir * 0.4f);
        lockedStrikeOrigin.z = -1f;

        // Скрываем круг замаха на время исполнения удара
        if (activeCircleCharge != null)
        {
            activeCircleCharge.SetVisible(false);
        }

        // Фиксируем траекторию для воспроизведения анимации разреза
        if (activeStrikeTrajectory != null)
        {
            activeStrikeTrajectory.LockStrike(lockedStrikeOrigin, lockedStrikeDir, strikeRange);
        }

        Debug.Log($"<color=#FFFFFF>[STRIKE]</color> Удар выпущен! Направление: {lockedStrikeDir}, Длина: {strikeRange}");
    }

    /// <summary>
    /// Покадровое обновление анимации разреза по линии за время strikeDuration.
    /// </summary>
    private void UpdateStrikeExecution()
    {
        strikeTimer += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(strikeTimer / Mathf.Max(0.01f, strikeDuration));

        if (activeStrikeTrajectory != null)
        {
            activeStrikeTrajectory.UpdateStrikeProgress(lockedStrikeOrigin, lockedStrikeDir, strikeRange, progress);
        }

        if (strikeTimer >= strikeDuration)
        {
            PerformStrikeHitCheck();
        }
    }

    /// <summary>
    /// Проверка пересечения отрезка удара со слабыми точками и телами врагов.
    /// </summary>
    private void PerformStrikeHitCheck()
    {
        strikeExecuting = false;

        Vector2 segStart = (Vector2)lockedStrikeOrigin;
        Vector2 segEnd = segStart + lockedStrikeDir * strikeRange;

        WeakPoint bestHit = null;
        bool hitAnyLethal = false;

        for (int i = 0; i < WeakPoint.ActiveWeakPoints.Count; i++)
        {
            var wp = WeakPoint.ActiveWeakPoints[i];
            if (wp == null || !wp.gameObject.activeInHierarchy) continue;

            float r = wp.HitRadiusWorld;
            float dist = DistancePointToSegment(wp.transform.position, segStart, segEnd);

            if (dist <= r + (strikeWidth * 0.5f))
            {
                if (wp.isLethal)
                {
                    bestHit = wp;
                    hitAnyLethal = true;
                    break; // Красная летальная точка имеет наивысший приоритет
                }
                else if (bestHit == null)
                {
                    bestHit = wp;
                }
            }
        }

        if (activeStrikeTrajectory != null)
        {
            activeStrikeTrajectory.PlayResolutionFlash(0.12f, destroyAfter: true);
        }

        if (bestHit != null)
        {
            ResolveHit(bestHit, hitAnyLethal);
        }
        else
        {
            // Проверяем прямое пересечение коллайдера врага линией удара с учетом ширины strikeWidth
            Collider2D hitEnemy = CheckEnemyColliderIntersect(segStart, segEnd);
            if (hitEnemy != null)
            {
                var vovaTarget = hitEnemy.GetComponent<Combat.Tutorial.TutorialVovaController>() ?? hitEnemy.GetComponentInParent<Combat.Tutorial.TutorialVovaController>();
                if (vovaTarget != null)
                {
                    vovaTarget.ExecuteFocusFinisher();
                    RestoreFocusToFull();
                    ApplyKillBuffs();
                    ExitFocus();
                    return;
                }

                hitEnemy.SendMessage("TakeDamage", normalStrikeDamage, SendMessageOptions.DontRequireReceiver);
                var target = hitEnemy.GetComponent<Combat.IHurtboxTarget2D>() ?? hitEnemy.GetComponentInParent<Combat.IHurtboxTarget2D>();
                if (target != null)
                {
                    Combat.AttackConfig bodyAttack = new Combat.AttackConfig
                    {
                        attackName = "Фокусный выпад",
                        damage = normalStrikeDamage,
                        targetedZones = Combat.CombatZone.Mid,
                        knockbackForce = new Vector2(5f, 1.5f),
                        attacker = gameObject
                    };
                    target.TakeHit(bodyAttack, Combat.CombatZone.Mid, hitEnemy.transform.position, lockedStrikeDir);
                }

                if (!continueFocusAfterHit)
                {
                    ExitFocus();
                }
            }
            else
            {
                ResolveMiss();
            }
        }
    }

    private Collider2D CheckEnemyColliderIntersect(Vector2 start, Vector2 end)
    {
        Vector2 dir = end - start;
        float dist = dir.magnitude;
        if (dist > 0.001f) dir /= dist;

        RaycastHit2D[] hits = Physics2D.CircleCastAll(start, strikeWidth * 0.5f, dir, dist);
        foreach (var h in hits)
        {
            if (h.collider == null) continue;
            if (h.collider.CompareTag("Enemy") || h.collider.gameObject.layer == LayerMask.NameToLayer("Enemy") || h.collider.GetComponentInParent<Combat.CombatDummy2D>() != null || h.collider.GetComponentInParent<Combat.Tutorial.TutorialVovaController>() != null)
            {
                return h.collider;
            }
        }
        return null;
    }

    /// <summary>
    /// Расчет расстояния от точки p до отрезка [a, b].
    /// </summary>
    public static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lenSq = ab.sqrMagnitude;
        if (lenSq < 0.0001f) return Vector2.Distance(p, a);

        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
        Vector2 projection = a + t * ab;
        return Vector2.Distance(p, projection);
    }

    /// <summary>
    /// Полное разрешение попадания по слабой точке:
    /// - Если lethal:
    ///   - Нанесение летального урона объекту-владельцу
    ///   - FocusManager.Instance.RestoreFocusToFull() — шкала 100%
    ///   - ApplyKillBuffs() — бафф ярости (+50% скорости бега)
    ///   - ExitFocus()
    /// - Если non-lethal:
    ///   - Нанесение 20 урона
    ///   - ExitFocus()
    /// </summary>
    public void ResolveHit(WeakPoint point, bool isLethal)
    {
        if (point == null) return;

        GameObject enemyObj = point.ownerEnemy;
        if (enemyObj == null)
        {
            Collider2D col = Physics2D.OverlapCircle(point.transform.position, 1.5f);
            if (col != null) enemyObj = col.gameObject;
        }

        var tutorialVova = (enemyObj != null) ? (enemyObj.GetComponent<Combat.Tutorial.TutorialVovaController>() ?? enemyObj.GetComponentInParent<Combat.Tutorial.TutorialVovaController>()) : null;
        if (tutorialVova != null)
        {
            tutorialVova.ExecuteFocusFinisher();
            RestoreFocusToFull();
            ApplyKillBuffs();
            ExitFocus();
            return;
        }

        if (isLethal)
        {
            Debug.Log($"<color=#FF2222>[STRIKE HIT]</color> Летальный удар по {(enemyObj != null ? enemyObj.name : "точке")}!");

            if (enemyObj != null)
            {
                var vova = enemyObj.GetComponent<Combat.Tutorial.TutorialVovaController>() ?? enemyObj.GetComponentInParent<Combat.Tutorial.TutorialVovaController>();
                if (vova != null)
                {
                    vova.ExecuteFocusFinisher();
                }
                else
                {
                    var target = enemyObj.GetComponent<Combat.IHurtboxTarget2D>() ?? enemyObj.GetComponentInParent<Combat.IHurtboxTarget2D>();
                    if (target != null)
                    {
                        Combat.AttackConfig lethalAttack = new Combat.AttackConfig
                        {
                            attackName = "Фокусный смертельный выпад",
                            damage = 9999f,
                            targetedZones = Combat.CombatZone.High | Combat.CombatZone.Mid | Combat.CombatZone.Low,
                            knockbackForce = new Vector2(8f, 2.5f),
                            attacker = gameObject
                        };
                        target.TakeHit(lethalAttack, Combat.CombatZone.Mid, point.transform.position, Vector2.right);
                    }

                    enemyObj.SendMessage("TakeDamage", 9999f, SendMessageOptions.DontRequireReceiver);
                    enemyObj.SendMessage("Die", SendMessageOptions.DontRequireReceiver);
                }
            }

            RestoreFocusToFull();
            ApplyKillBuffs();
            ExitFocus();
        }
        else
        {
            Debug.Log($"<color=#FFFFFF>[STRIKE HIT]</color> Нелетальный удар по {(enemyObj != null ? enemyObj.name : "точке")}!");

            if (enemyObj != null)
            {
                var target = enemyObj.GetComponent<Combat.IHurtboxTarget2D>() ?? enemyObj.GetComponentInParent<Combat.IHurtboxTarget2D>();
                if (target != null)
                {
                    Combat.AttackConfig normalAttack = new Combat.AttackConfig
                    {
                        attackName = "Фокусный выпад",
                        damage = normalStrikeDamage,
                        targetedZones = Combat.CombatZone.Mid,
                        knockbackForce = new Vector2(5f, 1.5f),
                        attacker = gameObject
                    };
                    target.TakeHit(normalAttack, Combat.CombatZone.Mid, point.transform.position, Vector2.right);
                }

                enemyObj.SendMessage("TakeDamage", normalStrikeDamage, SendMessageOptions.DontRequireReceiver);
            }

            if (!continueFocusAfterHit)
            {
                ExitFocus();
            }
        }
    }

    /// <summary>
    /// Разрешение промаха:
    /// - Списание штрафа фокуса (missPenalty)
    /// - ExitFocus()
    /// </summary>
    public void ResolveMiss()
    {
        if (!isFocusActive) return;

        // Штраф фокуса
        focusPercent = Mathf.Clamp(focusPercent - missPenalty, 0f, maxFocus);
        OnFocusChanged?.Invoke(focusPercent);

        Debug.Log($"<color=#888888>[FOCUS]</color> Промах! Списано {missPenalty} фокуса.");

        ExitFocus();
    }

    /// <summary>
    /// Мгновенно восстанавливает шкалу фокуса до максимума.
    /// </summary>
    public void RestoreFocusToFull()
    {
        focusPercent = maxFocus;
        OnFocusChanged?.Invoke(focusPercent);
        Debug.Log($"<color=#00FFAA>[FOCUS]</color> Шкала фокуса мгновенно восстановлена на 100% ({maxFocus})!");
    }

    /// <summary>
    /// Устанавливает точное значение шкалы фокуса.
    /// </summary>
    public void SetFocus(float value)
    {
        float previous = focusPercent;
        focusPercent = Mathf.Clamp(value, 0f, maxFocus);
        if (!Mathf.Approximately(previous, focusPercent))
        {
            OnFocusChanged?.Invoke(focusPercent);
        }
    }

    /// <summary>
    /// Событие летального убийства в фокусе (для подключения систем наград/баффов).
    /// </summary>
    public static event Action OnLethalKill;

    /// <summary>
    /// Активирует событие убийства при летальном попадании.
    /// </summary>
    public void ApplyKillBuffs()
    {
        OnLethalKill?.Invoke();
    }



    private void EnsureWeakPointSpawner()
    {
        if (weakPointSpawner == null)
        {
            weakPointSpawner = GetComponent<WeakPointSpawner>();
            if (weakPointSpawner == null)
            {
#if UNITY_6000_0_OR_NEWER
                weakPointSpawner = FindAnyObjectByType<WeakPointSpawner>();
#else
                weakPointSpawner = FindObjectOfType<WeakPointSpawner>();
#endif
            }
            if (weakPointSpawner == null)
            {
                weakPointSpawner = gameObject.AddComponent<WeakPointSpawner>();
            }
        }
    }

    private void SpawnWeakPoints()
    {
        EnsureWeakPointSpawner();

        Vector3 origin = transform.position;
        var player = GameObject.FindWithTag("Player");
        if (player != null) origin = player.transform.position;

        if (weakPointSpawner != null)
        {
            weakPointSpawner.SpawnWeakPoints(origin);
        }
    }

    private void ClearWeakPoints()
    {
        WeakPoint.ClearAll();
    }

    private void SpawnCircleCharge()
    {
        DespawnCircleCharge();

        Vector3 spawnPos = Vector3.zero;
        var player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            spawnPos = player.transform.position;
        }
        spawnPos.z = -1f; // МЕЖДУ камерой (Z=-10) и игроком (Z=0) — но перед фоном

        GameObject chargeObj = null;
        if (circleChargePrefab != null)
        {
            chargeObj = Instantiate(circleChargePrefab, spawnPos, Quaternion.identity);
        }
        else
        {
#if UNITY_EDITOR
            var prefabAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CircleCharge.prefab");
            if (prefabAsset != null)
            {
                chargeObj = Instantiate(prefabAsset, spawnPos, Quaternion.identity);
            }
#endif
            if (chargeObj == null)
            {
                chargeObj = new GameObject("CircleCharge");
                chargeObj.transform.position = spawnPos;
                chargeObj.AddComponent<CircleCharge>();
            }
        }

        if (chargeObj != null)
        {
            activeCircleCharge = chargeObj.GetComponent<CircleCharge>();
            if (activeCircleCharge != null)
            {
                if (player != null)
                {
                    activeCircleCharge.playerAnchor = player.transform;
                }
                activeCircleCharge.maxStretchDistance = maxStretchDistance;
                activeCircleCharge.returnSpeed = tensionReturnSpeed;
            }
        }

        // Спавн предпросмотра траектории будущего разреза (StrikeTrajectory)
        GameObject trajectoryObj = new GameObject("StrikeTrajectory");
        trajectoryObj.transform.position = spawnPos;
        activeStrikeTrajectory = trajectoryObj.AddComponent<StrikeTrajectory>();
        if (player != null)
        {
            activeStrikeTrajectory.playerAnchor = player.transform;
        }
        activeStrikeTrajectory.lineLength = strikeRange;
        if (activeStrikeTrajectory.LineRend != null)
        {
            activeStrikeTrajectory.LineRend.startWidth = Mathf.Max(0.04f, strikeWidth * 0.08f);
            activeStrikeTrajectory.LineRend.endWidth = Mathf.Max(0.12f, strikeWidth * 0.2f);
        }
        activeStrikeTrajectory.SetCircle(activeCircleCharge);
    }

    private void DespawnCircleCharge()
    {
        if (activeCircleCharge != null)
        {
            if (Application.isPlaying)
            {
                Destroy(activeCircleCharge.gameObject);
            }
            else
            {
                DestroyImmediate(activeCircleCharge.gameObject);
            }
            activeCircleCharge = null;
        }

        if (activeStrikeTrajectory != null)
        {
            if (activeStrikeTrajectory.IsFlashing)
            {
                // Не уничтожаем сразу, если проигрывается вспышка разрешения удара
                activeStrikeTrajectory = null;
            }
            else
            {
                if (activeStrikeTrajectory.LineRend != null)
                {
                    activeStrikeTrajectory.LineRend.enabled = false;
                }
                activeStrikeTrajectory.gameObject.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(activeStrikeTrajectory.gameObject);
                }
                else
                {
                    DestroyImmediate(activeStrikeTrajectory.gameObject);
                }
                activeStrikeTrajectory = null;
            }
        }

#if UNITY_6000_0_OR_NEWER
        var extraCharges = FindObjectsByType<CircleCharge>(FindObjectsInactive.Include);
        var extraTrajectories = FindObjectsByType<StrikeTrajectory>(FindObjectsInactive.Include);
#else
        var extraCharges = FindObjectsOfType<CircleCharge>();
        var extraTrajectories = FindObjectsOfType<StrikeTrajectory>();
#endif
        foreach (var c in extraCharges)
        {
            if (c != null)
            {
                c.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(c.gameObject);
                else DestroyImmediate(c.gameObject);
            }
        }
        foreach (var t in extraTrajectories)
        {
            if (t != null)
            {
                if (t.IsFlashing) continue; // Позволяем корутине вспышки завершиться и уничтожить объект
                if (t.LineRend != null) t.LineRend.enabled = false;
                t.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(t.gameObject);
                else DestroyImmediate(t.gameObject);
            }
        }
    }

    /// <summary>
    /// Прибавляет количество фокуса, клампит до maxFocus и вызывает OnFocusChanged.
    /// </summary>
    public void AddFocus(float amount)
    {
        if (amount <= 0f) return;

        float previous = focusPercent;
        focusPercent = Mathf.Clamp(focusPercent + amount, 0f, maxFocus);

        if (!Mathf.Approximately(previous, focusPercent))
        {
            OnFocusChanged?.Invoke(focusPercent);
        }
    }

    /// <summary>
    /// Вычитает количество фокуса, клампит от 0 до maxFocus и вызывает OnFocusChanged.
    /// </summary>
    public void ConsumeFocus(float amount)
    {
        if (amount <= 0f) return;

        float previous = focusPercent;
        focusPercent = Mathf.Clamp(focusPercent - amount, 0f, maxFocus);

        if (!Mathf.Approximately(previous, focusPercent))
        {
            OnFocusChanged?.Invoke(focusPercent);
        }
    }

    private void EnsureVignetteReference()
    {
        if (vignetteCanvasGroup == null)
        {
            var vigObj = GameObject.Find("FocusVignette");
            if (vigObj != null)
            {
                vignetteCanvasGroup = vigObj.GetComponent<CanvasGroup>();
            }
        }
    }

    private void SetVignetteAlpha(float targetAlpha, float duration)
    {
        EnsureVignetteReference();
        if (vignetteCanvasGroup == null) return;

        if (duration <= 0f)
        {
            if (vignetteRoutine != null) StopCoroutine(vignetteRoutine);
            vignetteRoutine = null;
            vignetteCanvasGroup.alpha = targetAlpha;
            return;
        }

        if (vignetteRoutine != null)
        {
            StopCoroutine(vignetteRoutine);
        }
        vignetteRoutine = StartCoroutine(VignetteFadeRoutine(targetAlpha, duration));
    }

    private IEnumerator VignetteFadeRoutine(float targetAlpha, float duration)
    {
        float startAlpha = vignetteCanvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            vignetteCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        vignetteCanvasGroup.alpha = targetAlpha;
        vignetteRoutine = null;
    }
}
