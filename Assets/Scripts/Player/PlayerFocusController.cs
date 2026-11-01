// Assets/Scripts/Player/PlayerFocusController.cs
using UnityEngine;

namespace Combat.Player
{
    /// <summary>
    /// Контроллер и параметры режима фокуса (Focus Mode) на объекте игрока.
    /// Позволяет настраивать шкалу фокуса, порог активации, параметры удара,
    /// замедление времени, силу натяжения замаха и боевые эффекты прямо в Инспекторе Unity.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Combat/Player/Player Focus Controller")]
    public class PlayerFocusController : MonoBehaviour
    {
        [Header("--- 1. Шкала фокуса (Resource & Threshold) ---")]
        [Tooltip("Максимальный объем шкалы фокуса")]
        [Min(10f)]
        [SerializeField] private float maxFocus = 100f;

        [Tooltip("Порог шкалы для активации режима фокуса (по умолчанию 100 — требуется полный заряд)")]
        [Min(1f)]
        [SerializeField] private float activationThreshold = 100f;

        [Tooltip("Скорость расхода фокуса (единиц в секунду реального времени при замедлении)")]
        [Min(0f)]
        [SerializeField] private float focusDrainRate = 20f;

        [Tooltip("Начальное заполнение шкалы фокуса при старте игры")]
        [Min(0f)]
        [SerializeField] private float startingFocus = 100f;

        [Tooltip("Количество фокуса, накапливаемое при успешном ударе мечом")]
        [Min(0f)]
        [SerializeField] private float focusGainOnHit = 15f;

        [Tooltip("Количество фокуса, накапливаемое при успешном парировании")]
        [Min(0f)]
        [SerializeField] private float focusGainOnParry = 35f;

        [Header("--- 2. Замедление времени и камера (Slow Motion & Camera) ---")]
        [Tooltip("Степень замедления времени в фокусе (0.05 = 20x замедление, 0.2 = 5x, 1.0 = без замедления)")]
        [Range(0.05f, 1.0f)]
        [SerializeField] private float timeScaleInFocus = 0.2f;

        [Tooltip("Множитель приближения камеры (зума) в режиме фокуса (1.0 = без зума, 1.3 = +30%)")]
        [Range(1.0f, 2.5f)]
        [SerializeField] private float cameraZoomMultiplier = 1.3f;

        [Tooltip("Длительность плавного перехода времени и камеры (в секундах)")]
        [Range(0.05f, 1.0f)]
        [SerializeField] private float transitionDuration = 0.2f;

        [Header("--- 3. Сила натяжения при замахе (Tension / Windup) ---")]
        [Tooltip("Дистанция натяжения замаха мышью в мировых координатах (меньше = резче замах, больше = требуется более широкий жест мыши)")]
        [Range(0.5f, 6.0f)]
        [SerializeField] private float maxStretchDistance = 2.5f;

        [Tooltip("Порог натяжения замаха (0.5..1.0), при котором разрешен удар по ЛКМ")]
        [Range(0.5f, 1.0f)]
        [SerializeField] private float maxTensionThreshold = 0.85f;

        [Tooltip("Скорость возврата натяжения к центру при приближении курсора к персонажу")]
        [Range(1f, 30f)]
        [SerializeField] private float tensionReturnSpeed = 8f;

        [Header("--- 4. Размер и свойства удара (Strike Dimensions & Damage) ---")]
        [Tooltip("Дальность (длина) выпада/разреза в мировых координатах")]
        [Range(2.0f, 25.0f)]
        [SerializeField] private float strikeRange = 6.0f;

        [Tooltip("Ширина (толщина) зоны поражения удара")]
        [Range(0.2f, 4.0f)]
        [SerializeField] private float strikeWidth = 1.2f;

        [Tooltip("Длительность анимации пролета разреза (в секундах реального времени)")]
        [Range(0.05f, 0.6f)]
        [SerializeField] private float strikeDuration = 0.2f;

        [Tooltip("Урон нелетального удара по слабым точкам или телу врага")]
        [Min(0f)]
        [SerializeField] private float normalStrikeDamage = 35f;

        [Tooltip("Штраф к шкале фокуса при промахе")]
        [Min(0f)]
        [SerializeField] private float missPenalty = 25f;

        [Tooltip("Оставаться в режиме фокуса после успешного нелетального удара (если ресурс ещё остался)")]
        [SerializeField] private bool continueFocusAfterHit = false;

        [Header("--- 5. Управление и зона наведения (Input & Detection) ---")]
        [Tooltip("Радиус вокруг игрока для удобного наведения курсора ПКМ")]
        [Range(0.5f, 5.0f)]
        [SerializeField] private float playerHoverRadius = 1.8f;

        #region Public Properties
        public float MaxFocus { get => maxFocus; set => maxFocus = Mathf.Max(1f, value); }
        public float ActivationThreshold { get => activationThreshold; set => activationThreshold = Mathf.Max(0f, value); }
        public float FocusDrainRate { get => focusDrainRate; set => focusDrainRate = Mathf.Max(0f, value); }
        public float StartingFocus { get => startingFocus; set => startingFocus = value; }
        public float FocusGainOnHit { get => focusGainOnHit; set => focusGainOnHit = Mathf.Max(0f, value); }
        public float FocusGainOnParry { get => focusGainOnParry; set => focusGainOnParry = Mathf.Max(0f, value); }
        public float TimeScaleInFocus { get => timeScaleInFocus; set => timeScaleInFocus = Mathf.Clamp(value, 0.01f, 1f); }
        public float CameraZoomMultiplier { get => cameraZoomMultiplier; set => cameraZoomMultiplier = Mathf.Max(0.5f, value); }
        public float TransitionDuration { get => transitionDuration; set => transitionDuration = Mathf.Max(0.01f, value); }
        public float MaxStretchDistance { get => maxStretchDistance; set => maxStretchDistance = Mathf.Max(0.1f, value); }
        public float MaxTensionThreshold { get => maxTensionThreshold; set => maxTensionThreshold = Mathf.Clamp01(value); }
        public float TensionReturnSpeed { get => tensionReturnSpeed; set => tensionReturnSpeed = Mathf.Max(0.1f, value); }
        public float StrikeRange { get => strikeRange; set => strikeRange = Mathf.Max(0.5f, value); }
        public float StrikeWidth { get => strikeWidth; set => strikeWidth = Mathf.Max(0.1f, value); }
        public float StrikeDuration { get => strikeDuration; set => strikeDuration = Mathf.Max(0.01f, value); }
        public float NormalStrikeDamage { get => normalStrikeDamage; set => normalStrikeDamage = Mathf.Max(0f, value); }
        public float MissPenalty { get => missPenalty; set => missPenalty = Mathf.Max(0f, value); }
        public bool ContinueFocusAfterHit { get => continueFocusAfterHit; set => continueFocusAfterHit = value; }
        public float PlayerHoverRadius { get => playerHoverRadius; set => playerHoverRadius = Mathf.Max(0.1f, value); }
        #endregion

        private void Awake()
        {
            ApplyToManager();
        }

        private void Start()
        {
            ApplyToManager();
            if (FocusManager.Instance != null && startingFocus > 0f)
            {
                FocusManager.Instance.SetFocus(Mathf.Min(startingFocus, maxFocus));
            }
        }

        private void OnValidate()
        {
            if (activationThreshold > maxFocus)
            {
                activationThreshold = maxFocus;
            }
            if (startingFocus > maxFocus)
            {
                startingFocus = maxFocus;
            }

            if (Application.isPlaying && FocusManager.Instance != null)
            {
                ApplyToManager();
            }
        }

        /// <summary>
        /// Передает актуальные параметры в синглтон FocusManager.
        /// </summary>
        [ContextMenu("Применить настройки в FocusManager")]
        public void ApplyToManager()
        {
            var manager = FocusManager.Instance;
            if (manager != null)
            {
                manager.ApplyPlayerConfig(this);
            }
        }

        [ContextMenu("Заполнить шкалу фокуса (100%)")]
        public void FillFocusContextMenu()
        {
            if (FocusManager.Instance != null)
            {
                FocusManager.Instance.RestoreFocusToFull();
            }
        }

        [ContextMenu("Сбросить шкалу фокуса (0%)")]
        public void EmptyFocusContextMenu()
        {
            if (FocusManager.Instance != null)
            {
                FocusManager.Instance.SetFocus(0f);
            }
        }
    }
}
