// Assets/Scripts/Fencing/AttackOrb.cs
using UnityEngine;

namespace Fencing
{
    /// <summary>
    /// Шарик направления удара («AttackOrb»).
    /// Реализует кастомную физику движения «на нитке с весом» без Rigidbody2D.
    /// Управляется зажатием ЛКМ (Input.GetMouseButton(0) / Mouse.current.leftButton).
    /// </summary>
    public class AttackOrb : MonoBehaviour
    {
        [Header("Физика нитки и оружия")]
        [Tooltip("Базовая скорость следования за курсором")]
        [SerializeField] private float followSpeed = 8.0f;

        [Tooltip("Вес/масса оружия. Больше вес = медленнее реакция, дольше инерция")]
        [Range(0.1f, 5.0f)]
        [SerializeField] private float weight = 1.0f;

        [Tooltip("Сопротивление движению / вязкое трение")]
        [Range(0.5f, 20.0f)]
        [SerializeField] private float drag = 4.0f;

        [Tooltip("Скорость плавного возврата в центр при отпускании ЛКМ")]
        [SerializeField] private float returnSpeed = 6.0f;

        [Tooltip("Возвращать ли шарик в центр при отпускании ЛКМ")]
        [SerializeField] private bool resetOnRelease = true;

        [Tooltip("Отображать ли визуальный спрайт шарика на игроке в мире (отключено, используется UI в левом нижнем углу)")]
        [SerializeField] private bool showWorldOrb = false;

        [Header("Ссылки")]
        [Tooltip("Ссылка на родительское колесо фехтования")]
        [SerializeField] private FencingWheel wheel;

        [Tooltip("Ссылка на UI колеса фехтования")]
        [SerializeField] private FencingWheelUI wheelUI;

        [Tooltip("Множитель чувствительности мыши при затяжке (1.0 = 1:1 по смещению)")]
        [SerializeField] private float mouseSensitivity = 1.0f;

        [Header("Состояние")]
        [Tooltip("Локальная позиция шарика")]
        [SerializeField] private Vector2 localPosition = Vector2.zero;

        [Tooltip("Вектор импульса/скорости шарика")]
        [SerializeField] private Vector2 velocity = Vector2.zero;

        [Tooltip("Удерживается ли шарик курсором в данный момент")]
        [SerializeField] private bool isHeld = false;

        private bool wasHeld = false;
        private Vector2 dragStartMouseScreen = Vector2.zero;

        #region Свойства
        public Vector2 Velocity { get => velocity; set => velocity = value; }
        public Vector2 LocalPosition => localPosition;
        public bool IsHeld => isHeld;

        public float FollowSpeed { get => followSpeed; set => followSpeed = value; }
        public float Weight { get => weight; set => weight = Mathf.Max(0.1f, value); }
        public float Drag { get => drag; set => drag = Mathf.Max(0.1f, value); }
        public float ReturnSpeed { get => returnSpeed; set => returnSpeed = value; }
        public bool ResetOnRelease { get => resetOnRelease; set => resetOnRelease = value; }
        public float MouseSensitivity { get => mouseSensitivity; set => mouseSensitivity = Mathf.Max(0.1f, value); }
        public FencingWheel Wheel { get => wheel; set => wheel = value; }
        #endregion

        private Camera mainCamera;

        private void Awake()
        {
            if (wheel == null)
            {
                wheel = GetComponentInParent<FencingWheel>();
            }

            if (wheelUI == null)
            {
#if UNITY_6000_0_OR_NEWER
                wheelUI = FindAnyObjectByType<FencingWheelUI>();
#else
                wheelUI = FindObjectOfType<FencingWheelUI>();
#endif
            }

            mainCamera = Camera.main;
            localPosition = transform.localPosition;

            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                Destroy(sr);
            }
        }

        private void Update()
        {
            if (wheel == null) return;
            if (mainCamera == null) mainCamera = Camera.main;

            float dt = Time.deltaTime;
            if (dt > 0.05f) dt = 0.05f;

            // Блокировка управления шариком при открытой панели настроек или в режиме фокуса
            if (wheel.IsPanelOpen || (FocusManager.Instance != null && FocusManager.Instance.isFocusActive))
            {
                isHeld = false;
            }
            else
            {
                isHeld = ReadMouseButton0();
            }

            if (isHeld)
            {
                // 1. Позиция мыши в экранных координатах (пиксели экрана)
                Vector2 currentMouseScreen = ReadMousePosition();

                // Если ЛКМ была только что зажата в этом кадре
                if (!wasHeld)
                {
                    // Шарик ВСЕГДА начинает движение строго из центра колеса
                    localPosition = Vector2.zero;
                    velocity = Vector2.zero;
                    transform.localPosition = Vector3.zero;

                    // Фиксируем исходную точку клика в экранных координатах
                    dragStartMouseScreen = currentMouseScreen;
                }

                // 2. Радиус в экранных пикселях, соответствующий полному радиусу колеса
                float dragRadiusPixels = 100f;
                if (wheelUI != null && wheelUI.UIRadius > 20f)
                {
                    dragRadiusPixels = wheelUI.UIRadius;
                }
                else
                {
                    dragRadiusPixels = Mathf.Clamp(Screen.height * 0.12f, 70f, 180f);
                }

                // 3. Смещение мыши от точки начального клика
                // ВАЖНО: Работаем чисто в экранных координатах мыши, без перевода в мировые координаты колеса!
                // Передвижение персонажа по WASD (вправо/влево/прыжки) никак не влияет на экранные координаты мыши.
                Vector2 mouseDelta = (currentMouseScreen - dragStartMouseScreen) * mouseSensitivity;

                // Плавающий якорь (Virtual Floating Drag): если мышь ушла дальше края колеса,
                // плавно подтягиваем начальную точку, чтобы при смене направления шарик отзывался моментально
                if (mouseDelta.magnitude > dragRadiusPixels)
                {
                    Vector2 excess = mouseDelta - mouseDelta.normalized * dragRadiusPixels;
                    dragStartMouseScreen += excess / mouseSensitivity;
                    mouseDelta = (currentMouseScreen - dragStartMouseScreen) * mouseSensitivity;
                }

                // Нормализованное смещение (от 0 до 1 на краю колеса)
                Vector2 normalizedDelta = mouseDelta / dragRadiusPixels;

                // 4. Целевая точка в локальных координатах колеса
                Vector2 targetLocal = normalizedDelta * wheel.WheelRadius;

                // 5. Clamp целевой точки по геометрии колеса и мертвым зонам
                Vector2 clampedTarget = wheel.ClampPosition(targetLocal);

                // 6. Плавное критически демпфированное следование без перелётов и дрожания (SmoothDamp)
                // Чем выше followSpeed, тем быстрее отклик.
                // Чем выше weight, тем больше ощущение приятной инерции оружия.
                float safeWeight = Mathf.Max(0.05f, weight);
                float baseTime = 0.35f / Mathf.Max(1f, followSpeed * 0.12f + 1f);
                float smoothTime = Mathf.Clamp(baseTime * Mathf.Sqrt(safeWeight), 0.015f, 0.35f);
                float maxSpeed = Mathf.Max(25f, wheel.WheelRadius * 30f);

                localPosition = Vector2.SmoothDamp(localPosition, clampedTarget, ref velocity, smoothTime, maxSpeed, dt);

                // Ограничение положения границами
                ApplyBoundaryConstraints();
            }
            else
            {
                // ЛКМ отпущена
                if (resetOnRelease)
                {
                    // Плавный возврат в центр без перелёта и осцилляций
                    float returnSmoothTime = Mathf.Clamp(0.5f / Mathf.Max(1f, returnSpeed), 0.03f, 0.25f);
                    localPosition = Vector2.SmoothDamp(localPosition, Vector2.zero, ref velocity, returnSmoothTime, 40f, dt);
                    if (localPosition.sqrMagnitude < 0.0001f)
                    {
                        localPosition = Vector2.zero;
                        velocity = Vector2.zero;
                    }
                }
                else
                {
                    // Оружие остается на месте, мягко затухая по инерции
                    float damping = Mathf.Max(1f, drag * 5.0f);
                    velocity = Vector2.MoveTowards(velocity, Vector2.zero, damping * dt);
                    localPosition += velocity * dt;
                    ApplyBoundaryConstraints();
                }
            }

            wasHeld = isHeld;

            // Применяем локальную позицию
            transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
        }

        /// <summary>
        /// Ограничивает позицию границами колеса и мертвыми зонами с гашением нормальной скорости.
        /// </summary>
        private void ApplyBoundaryConstraints()
        {
            float maxR = wheel.WheelRadius;
            float mag = localPosition.magnitude;
            if (mag > maxR)
            {
                Vector2 n = localPosition / mag;
                localPosition = n * maxR;
                float normalVel = Vector2.Dot(velocity, n);
                if (normalVel > 0f) velocity -= n * normalVel;
            }

            if (wheel.EnableDeadZone && wheel.DeadZoneAngle > 0.5f)
            {
                Vector2 clamped = wheel.ClampPosition(localPosition);
                if ((clamped - localPosition).sqrMagnitude > 0.001f)
                {
                    Vector2 normal = (localPosition - clamped).normalized;
                    float normalVel = Vector2.Dot(velocity, normal);
                    if (normalVel > 0f) velocity -= normal * normalVel;
                    localPosition = clamped;
                }
            }
        }

        /// <summary>
        /// Универсальное считывание зажатия ЛКМ (Input.GetMouseButton(0) + Input System Mouse.leftButton).
        /// </summary>
        private bool ReadMouseButton0()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                return UnityEngine.InputSystem.Mouse.current.leftButton.isPressed;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            try
            {
                return Input.GetMouseButton(0);
            }
            catch {}
#endif
            return false;
        }

        private Vector2 lastValidMouseScreen = Vector2.zero;

        /// <summary>
        /// Универсальное считывание позиции курсора мыши с кэшированием последнего валидного значения.
        /// </summary>
        private Vector2 ReadMousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                Vector2 pos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                if (pos.sqrMagnitude > 0.001f || lastValidMouseScreen == Vector2.zero)
                {
                    lastValidMouseScreen = pos;
                }
                return lastValidMouseScreen;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            try
            {
                Vector2 pos = Input.mousePosition;
                if (pos.sqrMagnitude > 0.001f || lastValidMouseScreen == Vector2.zero)
                {
                    lastValidMouseScreen = pos;
                }
                return lastValidMouseScreen;
            }
            catch {}
#endif
            return lastValidMouseScreen;
        }

        /// <summary>
        /// Процедурный визуальный кружок для шарика в сцене (по умолчанию отключен, так как индикатор отображается в UI).
        /// </summary>
        private void EnsureVisualSprite()
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (!showWorldOrb)
            {
                if (sr != null) sr.enabled = false;
                return;
            }

            if (sr == null)
            {
                sr = gameObject.AddComponent<SpriteRenderer>();
            }

            if (sr.sprite == null)
            {
                int size = 32;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                float radius = size * 0.45f;
                Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), center);
                        tex.SetPixel(x, y, d <= radius ? Color.white : Color.clear);
                    }
                }
                tex.Apply();
                sr.sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size * 2.5f);
                sr.color = new Color(1f, 0.9f, 0.1f, 1f);
                sr.sortingOrder = 10;
            }
            sr.enabled = true;
        }
    }
}
