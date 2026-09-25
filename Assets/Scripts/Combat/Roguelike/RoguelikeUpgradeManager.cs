using System;
using System.Collections.Generic;
using UnityEngine;
using Combat.UI;
using Combat.Player;
using Combat.Tactician;

namespace Combat.Roguelike
{
    /// <summary>
    /// Менеджер Roguelike-прогрессии и генерации карточек улучшений между уровнями.
    /// Управляет разблокировкой способностей Тактика, накопленными боевыми баффами
    /// и глобальными множителями врагов (проклятия/усложнения).
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

        [Header("--- 🔹 Карточки способностей Тактика (8 направлений) ---")]
        [SerializeField] private List<UpgradeCardDefinition> tacticianCards = new List<UpgradeCardDefinition>();

        [Header("--- 🟢 Каталог боевых баффов (Combat Buffs) ---")]
        [SerializeField] private List<UpgradeCardDefinition> combatBuffs = new List<UpgradeCardDefinition>();

        [Header("--- 🔴 Каталог перков риска и награды (Risk & Reward) ---")]
        [SerializeField] private List<UpgradeCardDefinition> riskRewardPerks = new List<UpgradeCardDefinition>();

        public List<UpgradeCardDefinition> TacticianCards => tacticianCards;
        public List<UpgradeCardDefinition> CombatBuffs => combatBuffs;
        public List<UpgradeCardDefinition> RiskRewardPerks => riskRewardPerks;

        public List<UpgradeCardDefinition> CurrentOffer { get; private set; } = new List<UpgradeCardDefinition>();

        public event Action<UpgradeCardDefinition> OnUpgradeSelected;

        private PlayerController2D _playerMovement;
        private PlayerCombatController2D _playerCombat;
        private PlayerHealth2D _playerHealth;
        private PlayerStamina2D _playerStamina;
        private TacticianCombatController2D _tactician;

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
            if (_playerCombat == null) _playerCombat = FindAnyObjectByType<PlayerCombatController2D>();
            if (_playerHealth == null) _playerHealth = FindAnyObjectByType<PlayerHealth2D>();
            if (_playerStamina == null) _playerStamina = FindAnyObjectByType<PlayerStamina2D>();
            if (_tactician == null)
            {
                _tactician = GetComponent<TacticianCombatController2D>() ??
                             (_playerCombat != null ? _playerCombat.GetComponent<TacticianCombatController2D>() : null) ??
                             FindAnyObjectByType<TacticianCombatController2D>();
            }
        }

        /// <summary>
        /// Генерирует ровно count (по умолчанию 5) уникальных карточек на выбор.
        /// При наличии заблокированных способностей Тактика хотя бы 1-2 карточки будут предлагать новые умения.
        /// Остальные слоты заполняются боевыми баффами и двусторонними перками (риск/награда).
        /// </summary>
        public List<UpgradeCardDefinition> GenerateUpgradeOffer(int count = 5)
        {
            ResolvePlayerReferences();
            EnsureDefaultCatalogs();
            var result = new List<UpgradeCardDefinition>();
            var usedIds = new HashSet<string>();

            // 1. Собираем неразблокированные способности Тактика из настраиваемого списка
            var availableAbilities = GetLockedTacticianCards();

            // Перемешиваем доступные способности
            Shuffle(availableAbilities);

            // Если есть заблокированные способности, берем 1-2 из них
            int abilityCardsToOffer = Mathf.Clamp(availableAbilities.Count > 0 ? UnityEngine.Random.Range(1, 3) : 0, 0, availableAbilities.Count);
            for (int i = 0; i < abilityCardsToOffer && result.Count < count; i++)
            {
                result.Add(availableAbilities[i]);
                usedIds.Add(availableAbilities[i].id);
            }

            // 2. Собираем пул баффов и перков риска из настраиваемых в Инспекторе списков
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

            // 3. Заполняем оставшиеся карточки уникальными элементами
            for (int i = 0; i < otherPool.Count && result.Count < count; i++)
            {
                var card = otherPool[i];
                if (!usedIds.Contains(card.id))
                {
                    result.Add(card);
                    usedIds.Add(card.id);
                }
            }

            // 4. Если уникальных карточек в пуле оказалось меньше count, добираем из боевых баффов
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

            // 1. Способности Тактика
            if (card.category == UpgradeCategory.TacticianAbility && card.abilityDirection != Direction8.None)
            {
                if (_tactician != null)
                {
                    _tactician.UnlockAbility(card.abilityDirection);
                }
            }

            // 2. Параметры игрока
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
                if (_playerCombat != null)
                {
                    _playerCombat.AttackDamageMultiplier = playerDamageMultiplier;
                }
            }

            if (card.playerSpeedMult != 1.0f)
            {
                playerSpeedMultiplier *= card.playerSpeedMult;
                if (_playerMovement != null)
                {
                    _playerMovement.MoveSpeedMultiplier = playerSpeedMultiplier;
                }
            }

            // 3. Модификаторы врагов
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

            if (_playerCombat != null) _playerCombat.AttackDamageMultiplier = 1.0f;
            if (_playerMovement != null) _playerMovement.MoveSpeedMultiplier = 1.0f;
            if (_tactician != null) _tactician.ResetAbilities();
        }

        public void EnsureDefaultCatalogs(bool force = false)
        {
            if (force || tacticianCards == null || tacticianCards.Count == 0)
            {
                tacticianCards = GetDefaultTacticianCards();
            }

            if (force || combatBuffs == null || combatBuffs.Count == 0)
            {
                combatBuffs = GetDefaultCombatBuffs();
            }

            if (force || riskRewardPerks == null || riskRewardPerks.Count == 0)
            {
                riskRewardPerks = GetDefaultRiskRewardPerks();
            }
        }

        private List<UpgradeCardDefinition> GetLockedTacticianCards()
        {
            EnsureDefaultCatalogs();
            var list = new List<UpgradeCardDefinition>();

            foreach (var card in tacticianCards)
            {
                if (card == null || card.abilityDirection == Direction8.None) continue;
                bool isUnlocked = (_tactician != null && _tactician.IsAbilityUnlocked(card.abilityDirection));
                if (!isUnlocked)
                {
                    list.Add(card.Clone());
                }
            }

            return list;
        }

        public static List<UpgradeCardDefinition> GetDefaultTacticianCards()
        {
            return new List<UpgradeCardDefinition>
            {
                new UpgradeCardDefinition("tac_thrust", "Прощупывающий выпад", UpgradeCategory.TacticianAbility, "➡️", "Быстрый колющий выпад вперед. Вешает метку уязвимости на врага (+35% урона на 7 сек).", "Разблокирует способность ➡️ в стойке Тактика") { abilityDirection = Direction8.Right },
                new UpgradeCardDefinition("tac_trap", "Глубинная печать", UpgradeCategory.TacticianAbility, "⬇️", "Устанавливает нажимную руническую мину под ногами. Наносит урон и парализует врага на 1.5 сек.", "Разблокирует способность ⬇️ в стойке Тактика") { abilityDirection = Direction8.Down },
                new UpgradeCardDefinition("tac_anchor", "Гравитационный якорь", UpgradeCategory.TacticianAbility, "⬆️", "Создает сферу искажения в воздухе. Подвешивает и лишает подвижности врагов на 2.5 сек.", "Разблокирует способность ⬆️ в стойке Тактика") { abilityDirection = Direction8.Up },
                new UpgradeCardDefinition("tac_retreat", "Тактический отход", UpgradeCategory.TacticianAbility, "⬅️", "Стремительный бэкдэш назад с густой дымовой завесой, ослепляющей врагов.", "Разблокирует способность ⬅️ в стойке Тактика") { abilityDirection = Direction8.Left },
                new UpgradeCardDefinition("tac_launch", "Кинетический подброс", UpgradeCategory.TacticianAbility, "↗️", "Водяной гейзер с авто-наведением на ближайшего врага. Запускает цель высоко в воздух.", "Разблокирует способность ↗️ в стойке Тактика") { abilityDirection = Direction8.UpRight },
                new UpgradeCardDefinition("tac_spikes", "Направленные шипы", UpgradeCategory.TacticianAbility, "↘️", "Волна каменных шипов, проносящаяся по платформе и сметающая толпы врагов.", "Разблокирует способность ↘️ в стойке Тактика") { abilityDirection = Direction8.DownRight },
                new UpgradeCardDefinition("tac_harpoon", "Магический гарпун", UpgradeCategory.TacticianAbility, "↙️", "Спектральная цепь дальнего боя. Хватает врага на дистанции и рывком притягивает к вашим ногам.", "Разблокирует способность ↙️ в стойке Тактика") { abilityDirection = Direction8.DownLeft },
                new UpgradeCardDefinition("tac_fan", "Веерная защита", UpgradeCategory.TacticianAbility, "↖️", "Защитная дуга из спектральных клинков над головой. Сбивает прыгающих врагов на землю (Anti-Air).", "Разблокирует способность ↖️ в стойке Тактика") { abilityDirection = Direction8.UpLeft }
            };
        }

        public static List<UpgradeCardDefinition> GetDefaultCombatBuffs()
        {
            return new List<UpgradeCardDefinition>
            {
                new UpgradeCardDefinition("buff_vitality", "Закалка воина", UpgradeCategory.CombatBuff, "❤️", "Тело напитывается живительной эссенцией кристалла.", "+30 к Макс. HP и мгновенное лечение на 30 HP")
                {
                    playerHealthDelta = 30f
                },
                new UpgradeCardDefinition("buff_second_wind", "Второе дыхание", UpgradeCategory.CombatBuff, "💨", "Легкие наполняются кристальным воздухом, снижая утомление.", "+35 к Макс. Выносливости и +25% к скорости регенерации стамины")
                {
                    playerStaminaDelta = 35f,
                    playerStaminaRegenMult = 1.25f
                },
                new UpgradeCardDefinition("buff_keen_edge", "Острота клинка", UpgradeCategory.CombatBuff, "⚔️", "Оружие звенит чистой разрушительной энергией при каждом ударе.", "+20% к урону всех атак, серий и финишеров")
                {
                    playerDamageMult = 1.20f
                },
                new UpgradeCardDefinition("buff_fleet_foot", "Легкая поступь", UpgradeCategory.CombatBuff, "👟", "Подошвы почти не касаются земли, увеличивая динамику бега.", "+15% к скорости бега и горизонтального перемещения")
                {
                    playerSpeedMult = 1.15f
                },
                new UpgradeCardDefinition("buff_titan_stamina", "Титанический резерв", UpgradeCategory.CombatBuff, "⚡", "Огромный прирост запаса сил для непрерывных комбинаций ударов.", "+50 к максимальному запасу Выносливости")
                {
                    playerStaminaDelta = 50f
                }
            };
        }

        public static List<UpgradeCardDefinition> GetDefaultRiskRewardPerks()
        {
            return new List<UpgradeCardDefinition>
            {
                new UpgradeCardDefinition("risk_predator", "Стремительный хищник", UpgradeCategory.RiskAndReward, "🐺", "Вы двигаетесь молниеносно, но враги на арене становятся необычайно живучими.", "+25% к скорости бега игрока", "Враги получают +30% к максимальному здоровью")
                {
                    playerSpeedMult = 1.25f,
                    enemyHealthMult = 1.30f
                },
                new UpgradeCardDefinition("risk_berserk", "Стеклянная пушка", UpgradeCategory.RiskAndReward, "🩸", "Вы жертвуете защитой и плотью ради сокрушительного урона клинка.", "+45% к урону всех атак игрока", "-20 к максимальному здоровью игрока")
                {
                    playerDamageMult = 1.45f,
                    playerHealthDelta = -20f
                },
                new UpgradeCardDefinition("risk_titan_heart", "Каменное сердце", UpgradeCategory.RiskAndReward, "🗿", "Кожа каменеет, даруя колоссальную прочность, но дыхание становится тяжелым.", "+50 к Максимальному HP игрока", "-15% к скорости восстановления выносливости")
                {
                    playerHealthDelta = 50f,
                    playerStaminaRegenMult = 0.85f
                },
                new UpgradeCardDefinition("risk_adrenaline", "Кровавый азарт", UpgradeCategory.RiskAndReward, "⚡", "Жажда битвы удесятеряет ваши силы, но ярость охватывает и противников.", "+30% к урону и +20 к выносливости", "Враги атакуют и замахиваются на 20% быстрее")
                {
                    playerDamageMult = 1.30f,
                    playerStaminaDelta = 20f,
                    enemySpeedMult = 1.20f
                }
            };
        }

        private static void Shuffle<T>(IList<T> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                int rnd = UnityEngine.Random.Range(i, list.Count);
                var temp = list[i];
                list[i] = list[rnd];
                list[rnd] = temp;
            }
        }

        /// <summary>
        /// Автоматически генерирует текст эффектов карточек на основе их числовых параметров.
        /// </summary>
        [ContextMenu("Обновить описания эффектов по текущим цифрам")]
        public void SyncCardTextsFromStats()
        {
            if (combatBuffs != null)
            {
                foreach (var card in combatBuffs)
                {
                    if (card == null) continue;
                    var posParts = new List<string>();
                    if (card.playerHealthDelta != 0f) posParts.Add($"{(card.playerHealthDelta > 0 ? "+" : "")}{card.playerHealthDelta:F0} к Макс. HP");
                    if (card.playerStaminaDelta != 0f) posParts.Add($"{(card.playerStaminaDelta > 0 ? "+" : "")}{card.playerStaminaDelta:F0} к Макс. Выносливости");
                    if (card.playerStaminaRegenMult != 1.0f) posParts.Add($"{(card.playerStaminaRegenMult > 1f ? "+" : "")}{(card.playerStaminaRegenMult - 1f) * 100f:F0}% к регену стамины");
                    if (card.playerDamageMult != 1.0f) posParts.Add($"{(card.playerDamageMult > 1f ? "+" : "")}{(card.playerDamageMult - 1f) * 100f:F0}% к урону атак");
                    if (card.playerSpeedMult != 1.0f) posParts.Add($"{(card.playerSpeedMult > 1f ? "+" : "")}{(card.playerSpeedMult - 1f) * 100f:F0}% к скорости бега");
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

        [ContextMenu("Тест: Выбрать карточку #1")]
        public void TestPickCard1() => TestPickCard(0);

        [ContextMenu("Тест: Выбрать карточку #2")]
        public void TestPickCard2() => TestPickCard(1);

        [ContextMenu("Тест: Выбрать карточку #3")]
        public void TestPickCard3() => TestPickCard(2);

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

        public void ToggleTacticianAbility(Direction8 dir)
        {
            ResolvePlayerReferences();
            if (_tactician == null) return;
            if (_tactician.IsAbilityUnlocked(dir))
            {
                _tactician.LockAbility(dir);
            }
            else
            {
                _tactician.UnlockAbility(dir);
            }
        }

        public bool IsTacticianAbilityUnlocked(Direction8 dir)
        {
            ResolvePlayerReferences();
            return _tactician != null && _tactician.IsAbilityUnlocked(dir);
        }

        public void TestUnlockAllTactician()
        {
            ResolvePlayerReferences();
            if (_tactician != null) _tactician.UnlockAllAbilities();
        }

        public void TestResetAllTactician()
        {
            ResolvePlayerReferences();
            if (_tactician != null) _tactician.ResetAbilities();
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
            if (_playerCombat != null) _playerCombat.AttackDamageMultiplier = playerDamageMultiplier;
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
