using System;
using System.Collections.Generic;
using UnityEngine;
using Combat.UI;
using Combat.Player;

namespace Combat.Roguelike
{
    /// <summary>
    /// Менеджер Roguelike-прогрессии и генерации карточек улучшений между уровнями.
    /// Управляет накопленными боевыми баффами и глобальными множителями врагов (проклятия/усложнения).
    /// </summary>
    [DisallowMultipleComponent]
    public class RoguelikeUpgradeManager : MonoBehaviour
    {
        public static RoguelikeUpgradeManager Instance { get; private set; }

        [Header("--- Cumulative Multipliers ---")]
        [SerializeField] private float enemyHealthMultiplier = 1.0f;
        [SerializeField] private float enemySpeedMultiplier = 1.0f;
        [SerializeField] private float playerDamageMultiplier = 1.0f;
        [SerializeField] private float playerSpeedMultiplier = 1.0f;

        public float EnemyHealthMultiplier => enemyHealthMultiplier;
        public float EnemySpeedMultiplier => enemySpeedMultiplier;
        public float PlayerDamageMultiplier => playerDamageMultiplier;
        public float PlayerSpeedMultiplier => playerSpeedMultiplier;

        [Header("--- 🟢 Каталог боевых баффов (Combat Buffs) ---")]
        [SerializeField] private List<UpgradeCardDefinition> combatBuffs = new List<UpgradeCardDefinition>();

        [Header("--- 🔴 Каталог перков риска и награды (Risk & Reward) ---")]
        [SerializeField] private List<UpgradeCardDefinition> riskRewardPerks = new List<UpgradeCardDefinition>();

        public List<UpgradeCardDefinition> CombatBuffs => combatBuffs;
        public List<UpgradeCardDefinition> RiskRewardPerks => riskRewardPerks;

        public List<UpgradeCardDefinition> CurrentOffer { get; private set; } = new List<UpgradeCardDefinition>();

        public event Action<UpgradeCardDefinition> OnUpgradeSelected;

        private PlayerController2D _playerMovement;
        private PlayerHealth2D _playerHealth;
        private PlayerStamina2D _playerStamina;

        private void Reset()
        {
            EnsureDefaultCatalogs(force: true);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            EnsureDefaultCatalogs();
            ResolvePlayerReferences();
        }

        private void Start()
        {
            ResolvePlayerReferences();
        }

        public void ResolvePlayerReferences()
        {
            if (_playerMovement == null) _playerMovement = FindAnyObjectByType<PlayerController2D>();
            if (_playerHealth == null) _playerHealth = FindAnyObjectByType<PlayerHealth2D>();
            if (_playerStamina == null) _playerStamina = FindAnyObjectByType<PlayerStamina2D>();
        }

        /// <summary>
        /// Генерирует ровно count (по умолчанию 5) уникальных карточек на выбор.
        /// Заполняются боевыми баффами и двусторонними перками (риск/награда).
        /// </summary>
        public List<UpgradeCardDefinition> GenerateUpgradeOffer(int count = 5)
        {
            ResolvePlayerReferences();
            EnsureDefaultCatalogs();
            var result = new List<UpgradeCardDefinition>();
            var usedIds = new HashSet<string>();

            // 1. Собираем пул баффов и перков риска из настраиваемых в Инспекторе списков
            var otherPool = new List<UpgradeCardDefinition>();
            if (combatBuffs != null)
            {
                foreach (var card in combatBuffs)
                {
                    if (card != null) otherPool.Add(card.Clone());
                }
            }
            if (riskRewardPerks != null)
            {
                foreach (var card in riskRewardPerks)
                {
                    if (card != null) otherPool.Add(card.Clone());
                }
            }
            Shuffle(otherPool);

            // 2. Заполняем карточки уникальными элементами
            for (int i = 0; i < otherPool.Count && result.Count < count; i++)
            {
                var card = otherPool[i];
                if (!usedIds.Contains(card.id))
                {
                    result.Add(card);
                    usedIds.Add(card.id);
                }
            }

            // 3. Если уникальных карточек в пуле оказалось меньше count, добираем из боевых баффов
            if (result.Count < count && combatBuffs != null && combatBuffs.Count > 0)
            {
                int safety = 0;
                while (result.Count < count && safety < 30)
                {
                    safety++;
                    var fallback = combatBuffs[UnityEngine.Random.Range(0, combatBuffs.Count)].Clone();
                    result.Add(fallback);
                }
            }

            // Перемешиваем порядок карточек перед показом игроку
            Shuffle(result);
            CurrentOffer = result;
            return result;
        }

        /// <summary>
        /// Применяет выбранное улучшение к игроку и состоянию забега.
        /// </summary>
        public void ApplyUpgrade(UpgradeCardDefinition card)
        {
            if (card == null) return;
            ResolvePlayerReferences();

            // 1. Параметры игрока
            if (card.playerHealthDelta != 0f && _playerHealth != null)
            {
                _playerHealth.ModifyMaxHealth(card.playerHealthDelta, healCurrent: card.playerHealthDelta > 0f);
            }

            if (card.playerStaminaDelta != 0f && _playerStamina != null)
            {
                _playerStamina.ModifyMaxStamina(card.playerStaminaDelta);
            }

            if (card.playerStaminaRegenMult != 1.0f && _playerStamina != null)
            {
                _playerStamina.ModifyRegenRate(card.playerStaminaRegenMult);
            }

            if (card.playerDamageMult != 1.0f)
            {
                playerDamageMultiplier *= card.playerDamageMult;
            }

            if (card.playerSpeedMult != 1.0f)
            {
                playerSpeedMultiplier *= card.playerSpeedMult;
                if (_playerMovement != null)
                {
                    _playerMovement.MoveSpeedMultiplier = playerSpeedMultiplier;
                }
            }

            // 2. Модификаторы врагов
            if (card.enemyHealthMult != 1.0f)
            {
                enemyHealthMultiplier *= card.enemyHealthMult;
            }

            if (card.enemySpeedMult != 1.0f)
            {
                enemySpeedMultiplier *= card.enemySpeedMult;
            }

            Debug.Log($"<color=#00FFAA><b>[ROGUELIKE UPGRADE]</b></color> Выбрана карточка: <b>{card.title}</b> ({card.category})");
            OnUpgradeSelected?.Invoke(card);
        }

        public void ResetRunState()
        {
            enemyHealthMultiplier = 1.0f;
            enemySpeedMultiplier = 1.0f;
            playerDamageMultiplier = 1.0f;
            playerSpeedMultiplier = 1.0f;

            if (_playerMovement != null) _playerMovement.MoveSpeedMultiplier = 1.0f;
        }

        public void EnsureDefaultCatalogs(bool force = false)
        {
            if (force || combatBuffs == null || combatBuffs.Count == 0)
            {
                combatBuffs = GetDefaultCombatBuffs();
            }

            if (force || riskRewardPerks == null || riskRewardPerks.Count == 0)
            {
                riskRewardPerks = GetDefaultRiskRewardPerks();
            }
        }

        public static List<UpgradeCardDefinition> GetDefaultCombatBuffs()
        {
            return new List<UpgradeCardDefinition>
            {
                new UpgradeCardDefinition("buff_vitality", "Эссенция Жизни", UpgradeCategory.CombatBuff, "❤️", "Древний эликсир закаляет тело воина, повышая порог выживаемости.", "+35 к Макс. HP") { playerHealthDelta = 35f },
                new UpgradeCardDefinition("buff_iron_lungs", "Стальное Дыхание", UpgradeCategory.CombatBuff, "🫁", "Особая техника дыхания снижает одышку и расширяет запас сил для затяжных комбо.", "+40 к Выносливости") { playerStaminaDelta = 40f },
                new UpgradeCardDefinition("buff_focus", "Поток Концентрации", UpgradeCategory.CombatBuff, "⚡", "Состояние боевого транса позволяет стамине восполняться почти мгновенно.", "+30% к скорости регена выносливости") { playerStaminaRegenMult = 1.30f },
                new UpgradeCardDefinition("buff_keen_edge", "Заточка Граней", UpgradeCategory.CombatBuff, "🗡️", "Идеальная полировка лезвия клинка увеличивает летальность каждого взмаха.", "+25% к урону атак") { playerDamageMult = 1.25f },
                new UpgradeCardDefinition("buff_wind_stride", "Поступь Ветра", UpgradeCategory.CombatBuff, "👟", "Легкие боевые сапоги позволяют стремительно сокращать и разрывать дистанцию.", "+15% к скорости перемещения") { playerSpeedMult = 1.15f }
            };
        }

        public static List<UpgradeCardDefinition> GetDefaultRiskRewardPerks()
        {
            return new List<UpgradeCardDefinition>
            {
                new UpgradeCardDefinition("risk_bloodlust", "Жажда Крови", UpgradeCategory.RiskAndReward, "🩸", "Кровавое безумие дарует колоссальную силу удара ценой здоровья.", "+45% к урону", "-25 к Макс. HP") { playerDamageMult = 1.45f, playerHealthDelta = -25f },
                new UpgradeCardDefinition("risk_glass_cannon", "Стеклянный Клинок", UpgradeCategory.RiskAndReward, "💎", "Лезвие рассекает сталь как шелк, но любая ошибка фатальна.", "+60% к урону", "Враги получают +30% к скорости атак") { playerDamageMult = 1.60f, enemySpeedMult = 1.30f },
                new UpgradeCardDefinition("risk_colossus_might", "Мощь Титана", UpgradeCategory.RiskAndReward, "🛡️", "Тяжелая броня превращает воина в несокрушимую крепость, замедляя шаг.", "+70 к Макс. HP", "-15% к скорости бега") { playerHealthDelta = 70f, playerSpeedMult = 0.85f },
                new UpgradeCardDefinition("risk_adrenal_surge", "Адреналиновый Шок", UpgradeCategory.RiskAndReward, "💉", "Бурлящая энергия питает неутомимость, но враги становятся яростнее.", "+60 к Выносливости и +40% к регену", "Враги получают +35% к HP") { playerStaminaDelta = 60f, playerStaminaRegenMult = 1.40f, enemyHealthMult = 1.35f }
            };
        }

        private static void Shuffle<T>(List<T> list)
        {
            if (list == null || list.Count <= 1) return;
            for (int i = 0; i < list.Count; i++)
            {
                int rnd = UnityEngine.Random.Range(i, list.Count);
                T temp = list[i];
                list[i] = list[rnd];
                list[rnd] = temp;
            }
        }

        public void SyncCardTextsFromStats()
        {
            if (combatBuffs != null)
            {
                foreach (var card in combatBuffs)
                {
                    if (card == null) continue;
                    var posParts = new List<string>();
                    if (card.playerHealthDelta != 0f) posParts.Add($"{(card.playerHealthDelta > 0 ? "+" : "")}{card.playerHealthDelta:F0} к Макс. HP");
                    if (card.playerStaminaDelta != 0f) posParts.Add($"{(card.playerStaminaDelta > 0 ? "+" : "")}{card.playerStaminaDelta:F0} к Выносливости");
                    if (card.playerStaminaRegenMult != 1f) posParts.Add($"{(card.playerStaminaRegenMult > 1f ? "+" : "")}{(card.playerStaminaRegenMult - 1f) * 100f:F0}% к скорости регена");
                    if (card.playerDamageMult != 1f) posParts.Add($"{(card.playerDamageMult > 1f ? "+" : "")}{(card.playerDamageMult - 1f) * 100f:F0}% к урону");
                    if (card.playerSpeedMult != 1f) posParts.Add($"{(card.playerSpeedMult > 1f ? "+" : "")}{(card.playerSpeedMult - 1f) * 100f:F0}% к скорости бега");
                    if (posParts.Count > 0) card.positiveEffectText = string.Join(" и ", posParts);
                }
            }

            if (riskRewardPerks != null)
            {
                foreach (var card in riskRewardPerks)
                {
                    if (card == null) continue;
                    var posParts = new List<string>();
                    var negParts = new List<string>();

                    if (card.playerHealthDelta > 0f) posParts.Add($"+{card.playerHealthDelta:F0} к Макс. HP игрока");
                    else if (card.playerHealthDelta < 0f) negParts.Add($"{card.playerHealthDelta:F0} к Макс. HP игрока");

                    if (card.playerStaminaDelta > 0f) posParts.Add($"+{card.playerStaminaDelta:F0} к Выносливости");
                    else if (card.playerStaminaDelta < 0f) negParts.Add($"{card.playerStaminaDelta:F0} к Выносливости");

                    if (card.playerStaminaRegenMult > 1f) posParts.Add($"+{(card.playerStaminaRegenMult - 1f) * 100f:F0}% к регену выносливости");
                    else if (card.playerStaminaRegenMult < 1f) negParts.Add($"-{(1f - card.playerStaminaRegenMult) * 100f:F0}% к скорости регена выносливости");

                    if (card.playerDamageMult > 1f) posParts.Add($"+{(card.playerDamageMult - 1f) * 100f:F0}% к урону игрока");
                    if (card.playerSpeedMult > 1f) posParts.Add($"+{(card.playerSpeedMult - 1f) * 100f:F0}% к скорости бега игрока");

                    if (card.enemyHealthMult > 1f) negParts.Add($"Враги получают +{(card.enemyHealthMult - 1f) * 100f:F0}% к максимальному HP");
                    if (card.enemySpeedMult > 1f) negParts.Add($"Враги атакуют и замахиваются на +{(card.enemySpeedMult - 1f) * 100f:F0}% быстрее");

                    if (posParts.Count > 0) card.positiveEffectText = string.Join(" и ", posParts);
                    if (negParts.Count > 0) card.negativeEffectText = string.Join(", ", negParts);
                }
            }
        }

        #region Inspector Test Helpers

        [ContextMenu("Тест: Показать 3 карточки на экране")]
        public void TestShowUpgradeScreen()
        {
            var ui = RoguelikeUpgradeUI.Instance ?? FindAnyObjectByType<RoguelikeUpgradeUI>();
            if (ui != null)
            {
                var offer = GenerateUpgradeOffer(3);
                ui.ShowSelection(offer, () =>
                {
                    Debug.Log("<color=#00FFAA>[TEST]</color> Выбор карточки завершен!");
                });
            }
            else
            {
                Debug.LogWarning("[RoguelikeUpgradeManager] RoguelikeUpgradeUI не найден на сцене!");
            }
        }

        [ContextMenu("Тест: Скрыть окно карточек")]
        public void TestHideUpgradeScreen()
        {
            var ui = RoguelikeUpgradeUI.Instance ?? FindAnyObjectByType<RoguelikeUpgradeUI>();
            if (ui != null)
            {
                ui.Hide();
                Time.timeScale = 1.0f;
            }
        }

        public void TestPickCard(int index)
        {
            if (CurrentOffer != null && index >= 0 && index < CurrentOffer.Count)
            {
                var card = CurrentOffer[index];
                ApplyUpgrade(card);
                var ui = RoguelikeUpgradeUI.Instance ?? FindAnyObjectByType<RoguelikeUpgradeUI>();
                if (ui != null)
                {
                    ui.Hide();
                    Time.timeScale = 1.0f;
                }
            }
            else
            {
                Debug.LogWarning($"[RoguelikeUpgradeManager] Карточка с индексом {index} недоступна. Сначала откройте окно выбора карточек.");
            }
        }

        public void TestAddHealth(float amount)
        {
            ResolvePlayerReferences();
            if (_playerHealth != null) _playerHealth.ModifyMaxHealth(amount);
        }

        public void TestAddStamina(float amount)
        {
            ResolvePlayerReferences();
            if (_playerStamina != null) _playerStamina.ModifyMaxStamina(amount);
        }

        public void TestAddDamage(float percent)
        {
            ResolvePlayerReferences();
            playerDamageMultiplier *= (1f + percent / 100f);
        }

        public void TestAddSpeed(float percent)
        {
            ResolvePlayerReferences();
            playerSpeedMultiplier *= (1f + percent / 100f);
            if (_playerMovement != null) _playerMovement.MoveSpeedMultiplier = playerSpeedMultiplier;
        }

        #endregion
    }
}
