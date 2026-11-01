using System;
using UnityEngine;
using Combat.Player;
using Combat.Dialogue;

namespace Combat.Tutorial
{
    /// <summary>
    /// Контроллер доступных способностей и ограничений игрока для этапов обучения.
    /// Позволяет точечно разрешать или запрещать:
    /// - Ходьбу / прыжки / дэш
    /// - Удары мечом через колесо фехтования
    /// - Переключение на стойку Архе (Ctrl)
    /// - Каст заклинаний Архе
    /// - Режим фокуса (ПКМ)
    /// - Бессмертие игрока
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerCapabilityController : MonoBehaviour
    {
        private static PlayerCapabilityController instance;
        public static PlayerCapabilityController Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<PlayerCapabilityController>(FindObjectsInactive.Include);
                }
                return instance;
            }
            private set => instance = value;
        }

        public static event Action OnCapabilitiesChanged;

        [Header("--- Movement Restrictions ---")]
        [Tooltip("Разрешено ли горизонтальное передвижение (ходьба A/D)")]
        [SerializeField] private bool allowMovement = true;

        [Tooltip("Разрешены ли прыжки (W/Space)")]
        [SerializeField] private bool allowJump = true;

        [Tooltip("Разрешен ли рывок (Dash / Shift)")]
        [SerializeField] private bool allowDash = true;

        [Header("--- Combat Restrictions ---")]
        [Tooltip("Разрешены ли удары мечом (взмахи через колесо фехтования)")]
        [SerializeField] private bool allowSwordAttacks = true;

        [Tooltip("Разрешено ли переключение стоек по Ctrl (стойка Архе)")]
        [SerializeField] private bool allowStanceToggle = true;

        [Tooltip("Разрешен ли ввод рун и каст заклинаний Архе")]
        [SerializeField] private bool allowMagicCasting = true;

        [Tooltip("Разрешена ли активация режима фокуса (зажатие ПКМ)")]
        [SerializeField] private bool allowFocusMode = true;

        [Header("--- Survival ---")]
        [Tooltip("Режим бессмертия игрока (HP не опускается ниже 1)")]
        [SerializeField] private bool isImmortal = false;

        private PlayerHealth2D _cachedPlayerHealth;

        #region Public Properties
        public bool AllowMovement
        {
            get => allowMovement;
            set { allowMovement = value; NotifyChanged(); }
        }

        public bool AllowJump
        {
            get => allowJump;
            set { allowJump = value; NotifyChanged(); }
        }

        public bool AllowDash
        {
            get => allowDash;
            set { allowDash = value; NotifyChanged(); }
        }

        public bool AllowSwordAttacks
        {
            get => allowSwordAttacks;
            set { allowSwordAttacks = value; NotifyChanged(); }
        }

        public bool AllowStanceToggle
        {
            get => allowStanceToggle;
            set { allowStanceToggle = value; NotifyChanged(); }
        }

        public bool AllowMagicCasting
        {
            get => allowMagicCasting;
            set { allowMagicCasting = value; NotifyChanged(); }
        }

        public bool AllowFocusMode
        {
            get => allowFocusMode;
            set { allowFocusMode = value; NotifyChanged(); }
        }

        public bool IsImmortal
        {
            get => isImmortal;
            set
            {
                isImmortal = value;
                SyncPlayerHealthImmortality();
                NotifyChanged();
            }
        }

        /// <summary>
        /// Проверяет, заблокирован ли ввод активным диалогом
        /// </summary>
        public bool IsDialogueActive => DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive;

        public bool CanMove => allowMovement && !IsDialogueActive;
        public bool CanJump => allowJump && !IsDialogueActive;
        public bool CanDash => allowDash && !IsDialogueActive;
        public bool CanSwordAttack => allowSwordAttacks && !IsDialogueActive;
        public bool CanToggleStance => allowStanceToggle && !IsDialogueActive;
        public bool CanCastMagic => allowMagicCasting && !IsDialogueActive;
        public bool CanUseFocus => allowFocusMode && !IsDialogueActive;
        #endregion

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            SyncPlayerHealthImmortality();
        }

        private void Start()
        {
            SyncPlayerHealthImmortality();
        }

        private void SyncPlayerHealthImmortality()
        {
            if (_cachedPlayerHealth == null)
            {
                _cachedPlayerHealth = GetComponent<PlayerHealth2D>() ?? FindAnyObjectByType<PlayerHealth2D>();
            }

            if (_cachedPlayerHealth != null)
            {
                _cachedPlayerHealth.CanDie = !isImmortal;
            }
        }

        private void NotifyChanged()
        {
            OnCapabilitiesChanged?.Invoke();
        }

        #region Preset Configurations for Tutorial Stages
        /// <summary>
        /// Стартовый коридор: только ходьба влево/вправо. Атаки и прыжки отключены.
        /// </summary>
        public void SetCorridorMode()
        {
            allowMovement = true;
            allowJump = false;
            allowDash = false;
            allowSwordAttacks = false;
            allowStanceToggle = false;
            allowMagicCasting = false;
            allowFocusMode = false;
            isImmortal = false;

            SyncPlayerHealthImmortality();
            NotifyChanged();
        }

        /// <summary>
        /// Обучение фехтованию: разрешены удары мечом по манекену. Магия и фокус заблокированы.
        /// </summary>
        public void SetFencingTutorialMode()
        {
            allowMovement = true;
            allowJump = true;
            allowDash = true;
            allowSwordAttacks = true;
            allowStanceToggle = false;
            allowMagicCasting = false;
            allowFocusMode = false;
            isImmortal = false;

            SyncPlayerHealthImmortality();
            NotifyChanged();
        }

        /// <summary>
        /// Обучение Архе: разрешено переключение на стойку Архе (Ctrl) и ввод огненных рун.
        /// </summary>
        public void SetArcheTutorialMode()
        {
            allowMovement = true;
            allowJump = true;
            allowDash = true;
            allowSwordAttacks = true;
            allowStanceToggle = true;
            allowMagicCasting = true;
            allowFocusMode = false;
            isImmortal = true; // Защита игрока от летального подрыва в обучении

            SyncPlayerHealthImmortality();
            NotifyChanged();
        }

        /// <summary>
        /// Бой с Вовой: фехтование + режим фокуса. Стойка Архе строго заблокирована. Игрок бессмертен.
        /// </summary>
        public void SetVovaDuelMode()
        {
            allowMovement = true;
            allowJump = true;
            allowDash = true;
            allowSwordAttacks = true;
            allowStanceToggle = false; // Магия Архе запрещена!
            allowMagicCasting = false;
            allowFocusMode = true;     // Режим фокуса разблокирован!
            isImmortal = true;         // Игрок бессмертен

            SyncPlayerHealthImmortality();
            NotifyChanged();
        }

        /// <summary>
        /// Все способности полностью разблокированы (для основной игры)
        /// </summary>
        public void SetFullCapabilities()
        {
            allowMovement = true;
            allowJump = true;
            allowDash = true;
            allowSwordAttacks = true;
            allowStanceToggle = true;
            allowMagicCasting = true;
            allowFocusMode = true;
            isImmortal = false;

            SyncPlayerHealthImmortality();
            NotifyChanged();
        }
        #endregion
    }
}
