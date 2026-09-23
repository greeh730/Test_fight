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

        public event Action<UpgradeCardDefinition> OnUpgradeSelected;

        private PlayerController2D _playerMovement;
        private PlayerCombatController2D _playerCombat;
        private PlayerHealth2D _playerHealth;
        private PlayerStamina2D _playerStamina;
        private TacticianCombatController2D _tactician;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
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
        /// Генерирует ровно count (по умолчанию 3) уникальных карточек на выбор.
        /// При наличии заблокированных способностей Тактика хотя бы 1-2 карточки будут предлагать новые умения.
        /// Остальные слоты заполняются боевыми баффами и двусторонними перками (риск/награда).
        /// </summary>
        public List<UpgradeCardDefinition> GenerateUpgradeOffer(int count = 3)
        {
            ResolvePlayerReferences();
            var result = new List<UpgradeCardDefinition>();
            var usedIds = new HashSet<string>();

            // 1. Собираем неразблокированные способности Тактика
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

            // 2. Собираем пул баффов и перков риска
            var otherPool = new List<UpgradeCardDefinition>();
            otherPool.AddRange(GetCombatBuffsCatalog());
            otherPool.AddRange(GetRiskRewardCatalog());
            Shuffle(otherPool);

            // 3. Заполняем оставшиеся карточки
            for (int i = 0; i < otherPool.Count && result.Count < count; i++)
            {
                var card = otherPool[i];
                if (!usedIds.Contains(card.id))
                {
                    result.Add(card);
                    usedIds.Add(card.id);
                }
            }

            // Перемешиваем порядок 3 карточек перед показом игроку
            Shuffle(result);
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

        private List<UpgradeCardDefinition> GetLockedTacticianCards()
        {
            var list = new List<UpgradeCardDefinition>();

            var all = new (Direction8 dir, string id, string title, string icon, string desc, string pos)[]
            {
                (Direction8.Right, "tac_thrust", "Прощупывающий выпад", "➡️", "Быстрый колющий выпад вперед. Вешает метку уязвимости на врага (+35% урона на 7 сек).", "Разблокирует способность ➡️ в стойке Тактика"),
                (Direction8.Down, "tac_trap", "Глубинная печать", "⬇️", "Устанавливает нажимную руническую мину под ногами. Наносит урон и парализует врага на 1.5 сек.", "Разблокирует способность ⬇️ в стойке Тактика"),
                (Direction8.Up, "tac_anchor", "Гравитационный якорь", "⬆️", "Создает сферу искажения в воздухе. Подвешивает и лишает подвижности врагов на 2.5 сек.", "Разблокирует способность ⬆️ в стойке Тактика"),
                (Direction8.Left, "tac_retreat", "Тактический отход", "⬅️", "Стремительный бэкдэш назад с густой дымовой завесой, ослепляющей врагов.", "Разблокирует способность ⬅️ в стойке Тактика"),
                (Direction8.UpRight, "tac_launch", "Кинетический подброс", "↗️", "Водяной гейзер с авто-наведением на ближайшего врага. Запускает цель высоко в воздух.", "Разблокирует способность ↗️ в стойке Тактика"),
                (Direction8.DownRight, "tac_spikes", "Направленные шипы", "↘️", "Волна каменных шипов, проносящаяся по платформе и сметающая толпы врагов.", "Разблокирует способность ↘️ в стойке Тактика"),
                (Direction8.DownLeft, "tac_harpoon", "Магический гарпун", "↙️", "Спектральная цепь дальнего боя. Хватает врага на дистанции и рывком притягивает к вашим ногам.", "Разблокирует способность ↙️ в стойке Тактика"),
                (Direction8.UpLeft, "tac_fan", "Веерная защита", "↖️", "Защитная дуга из спектральных клинков над головой. Сбивает прыгающих врагов на землю (Anti-Air).", "Разблокирует способность ↖️ в стойке Тактика")
            };

            foreach (var item in all)
            {
                if (_tactician == null || !_tactician.IsAbilityUnlocked(item.dir))
                {
                    var card = new UpgradeCardDefinition(item.id, item.title, UpgradeCategory.TacticianAbility, item.icon, item.desc, item.pos)
                    {
                        abilityDirection = item.dir
                    };
                    list.Add(card);
                }
            }

            return list;
        }

        private List<UpgradeCardDefinition> GetCombatBuffsCatalog()
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

        private List<UpgradeCardDefinition> GetRiskRewardCatalog()
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
    }
}
