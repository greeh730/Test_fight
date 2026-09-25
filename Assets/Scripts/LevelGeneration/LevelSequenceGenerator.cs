using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace LevelGeneration
{
    public enum ChunkPlacementType
    {
        [Tooltip("Всегда спавнить один конкретный заготовленный префаб")]
        FixedSingle = 0,

        [Tooltip("Выбрать случайный префаб из пула вариантов")]
        RandomFromPool = 1
    }

    public enum TierOverrideMode
    {
        [Tooltip("Автоматически по количеству собранных кристаллов / номеру прохождения")]
        AutoByProgress = 0,

        [Tooltip("Принудительно 1-й уровень сложности (1-3 прохождения)")]
        ForceTier1 = 1,

        [Tooltip("Принудительно 2-й уровень сложности (4-5 прохождений)")]
        ForceTier2 = 2,

        [Tooltip("Принудительно 3-й уровень сложности (6+ прохождений)")]
        ForceTier3 = 3,

        [Tooltip("Принудительно выбранный конкретный префаб")]
        ForceSpecificPrefab = 4
    }

    public enum TierSelectionMode
    {
        [Tooltip("Случайный выбор без повторения одного и того же уровня дважды подряд")]
        RandomAvoidRepeat = 0,

        [Tooltip("Последовательный выбор по номеру прохождения")]
        SequentialByRun = 1,

        [Tooltip("Полностью случайный выбор из пула")]
        RandomFromPool = 2
    }

    [Serializable]
    public class LevelDifficultyTier
    {
        public string tierName = "Уровень 1 (1-3 прохождения)";
        [Tooltip("Минимальный номер прохождения (Run) для этого тира")]
        public int minRun = 1;
        [Tooltip("Максимальный номер прохождения (Run) для этого тира")]
        public int maxRun = 3;

        [Tooltip("Пул префабов комнат для данного тира сложности")]
        public List<LevelChunk> chunkPool = new List<LevelChunk>();
    }

    [Serializable]
    public class LevelSegmentStep
    {
        public string segmentName = "Секция уровня";
        public ChunkPlacementType placementType = ChunkPlacementType.FixedSingle;

        [Tooltip("Фиксированный префаб чанка (для типа FixedSingle)")]
        public LevelChunk fixedChunkPrefab;

        [Tooltip("Пул вариаций чанков для случайного выбора (для типа RandomFromPool)")]
        public List<LevelChunk> variantPool = new List<LevelChunk>();
    }

    /// <summary>
    /// Конфигурация баффов и параметров врагов для конкретного уровня (круга забега).
    /// Полностью настраивается в Инспекторе для каждого уровня индивидуально.
    /// </summary>
    [Serializable]
    public class EnemyLevelBuffConfig
    {
        [Tooltip("Название/описание уровня (например, 'Уровень 1 (Старт)', 'Уровень 2 (Усиление)')")]
        public string levelTitle = "Уровень 1";

        [Header("--- Характеристики врагов (Множители) ---")]
        [Tooltip("Множитель максимального здоровья врагов (1.0 = базовое 100%, 1.35 = +35% HP)")]
        public float healthMultiplier = 1.0f;

        [Tooltip("Множитель урона ударов врагов (1.0 = базовый 100%, 1.25 = +25% урона)")]
        public float damageMultiplier = 1.0f;

        [Tooltip("Множитель скорости перемещения/бега врагов (1.0 = базовая, 1.15 = +15% к скорости)")]
        public float moveSpeedMultiplier = 1.0f;

        [Tooltip("Множитель выносливости (стамины) врагов (1.0 = базовая, 1.20 = +20% к запасу сил)")]
        public float staminaMultiplier = 1.0f;

        [Tooltip("Множитель скорости замаха врагов (1.0 = базовая, 1.20 = на 20% быстрее телеграф)")]
        public float telegraphSpeedMultiplier = 1.0f;

        [Header("--- Спавн врагов на этом уровне ---")]
        [Tooltip("Точное количество врагов на этом уровне (0 = авто-расчет по формуле базового числа врагов)")]
        public int enemyCountOverride = 0;

        [Header("--- Очки стиля ---")]
        [Tooltip("Множитель очков стиля за этот уровень (1.0 = базовый)")]
        public float styleMultiplier = 1.0f;

        public EnemyLevelBuffConfig() { }

        public EnemyLevelBuffConfig(string title, float hp, float dmg, float moveSpd, float stam, float attackSpd, int count, float style)
        {
            levelTitle = title;
            healthMultiplier = hp;
            damageMultiplier = dmg;
            moveSpeedMultiplier = moveSpd;
            staminaMultiplier = stam;
            telegraphSpeedMultiplier = attackSpd;
            enemyCountOverride = count;
            styleMultiplier = style;
        }

        public EnemyLevelBuffConfig Clone()
        {
            return (EnemyLevelBuffConfig)this.MemberwiseClone();
        }
    }

    /// <summary>
    /// Генератор цепочки уровня: собирает уровень из фиксированных и случайных фрагментов
    /// с абсолютной точностью стыковки по высоте через систему сокетов.
    /// </summary>
    [DisallowMultipleComponent]
    public class LevelSequenceGenerator : MonoBehaviour
    {
        [Header("--- Стартовая привязка ---")]
        [Tooltip("Мировая координата, с которой начинается уровень (первый вход)")]
        [SerializeField] private Vector3 startOrigin = new Vector3(-20f, -2.78f, 0f);

        [Header("--- Настройки генерации ---")]
        [Tooltip("Генерировать ли уровень автоматически при старте игры?")]
        [SerializeField] private bool generateOnStart = true;

        [Tooltip("Перемещать ли игрока на PlayerSpawnPoint стартового чанка при генерации?")]
        [SerializeField] private bool repositionPlayer = true;

        [Tooltip("Клавиша быстрой перегенерации в игре для тестирования разных вариантов (F4)")]
        [SerializeField] private KeyCode regenerateHotkey = KeyCode.F4;

        [Header("--- Предпросмотр в редакторе (Editor Preview) ---")]
        [Tooltip("Принудительный выбор конкретной боевой комнаты для предпросмотра в Scene View (-1 = Случайно, 0=Вариант A, 1=Вариант B, 2=Вариант C, 3=Вариант D)")]
        [SerializeField] private int forcePreviewCombatVariantIndex = -1;
        public int ForcePreviewCombatVariantIndex
        {
            get => forcePreviewCombatVariantIndex;
            set => forcePreviewCombatVariantIndex = value;
        }

        [Tooltip("Сбрасывать ли принудительный выбор варианта на случайный при старте Play Mode")]
        [SerializeField] private bool resetForceVariantOnPlay = true;

        [Tooltip("Спавнить ли врагов при генерации предпросмотра в редакторе")]
        [SerializeField] private bool spawnEnemiesInEditorPreview = false;
        public bool SpawnEnemiesInEditorPreview
        {
            get => spawnEnemiesInEditorPreview;
            set => spawnEnemiesInEditorPreview = value;
        }

        [Header("--- Система генерации по Тирам (Winning Crystals & Tiers) ---")]
        [Tooltip("Включить генерацию по 3 уровням сложности в зависимости от собранных кристаллов победы")]
        [SerializeField] private bool useDifficultyTiers = true;

        [Tooltip("Количество собранных Кристаллов Победы (wining crystall)")]
        [SerializeField] private int crystalsCollected = 0;

        [Tooltip("Текущий номер прохождения/забега (1..3 = Тир 1, 4..5 = Тир 2, 6+ = Тир 3)")]
        [SerializeField] private int currentRun = 1;

        [Header("--- Стартовый и Финальный чанки ---")]
        [Tooltip("Стартовый чанк с точкой спавна игрока")]
        [SerializeField] private LevelChunk startChunkPrefab;

        [Tooltip("Финальный чанк с Кристаллом Победы (Victory Crystal)")]
        [SerializeField] private LevelChunk endChunkPrefab;

        [Header("--- Уровни сложности (3 Тира) ---")]
        [SerializeField] private List<LevelDifficultyTier> difficultyTiers = new List<LevelDifficultyTier>();

        [Tooltip("Алгоритм выбора комнаты внутри активного тира")]
        [SerializeField] private TierSelectionMode tierSelectionMode = TierSelectionMode.RandomAvoidRepeat;

        [Header("--- Панель тестирования в Инспекторе (Editor Testing & Override) ---")]
        [Tooltip("Принудительный выбор тира сложности или префаба для тестов")]
        [SerializeField] private TierOverrideMode tierOverrideMode = TierOverrideMode.AutoByProgress;

        [Tooltip("Принудительный префаб для мгновенного теста (если задан и выбран ForceSpecificPrefab)")]
        [SerializeField] private LevelChunk debugSpecificPrefab;

        [Tooltip("Индекс конкретного префаба из активного тира (-1 = по режиму выбора)")]
        [SerializeField] private int debugPrefabIndexInTier = -1;

        public bool UseDifficultyTiers
        {
            get => useDifficultyTiers;
            set => useDifficultyTiers = value;
        }

        public int CrystalsCollected
        {
            get => crystalsCollected;
            set
            {
                crystalsCollected = Mathf.Max(0, value);
                currentRun = crystalsCollected + 1;
                currentCycle = currentRun;
            }
        }

        public int CurrentRun
        {
            get => currentRun;
            set
            {
                currentRun = Mathf.Max(1, value);
                crystalsCollected = currentRun - 1;
                currentCycle = currentRun;
            }
        }

        public TierOverrideMode ActiveTierOverrideMode
        {
            get => tierOverrideMode;
            set => tierOverrideMode = value;
        }

        public LevelChunk DebugSpecificPrefab
        {
            get => debugSpecificPrefab;
            set => debugSpecificPrefab = value;
        }

        public int DebugPrefabIndexInTier
        {
            get => debugPrefabIndexInTier;
            set => debugPrefabIndexInTier = value;
        }

        public LevelChunk StartChunkPrefab
        {
            get => startChunkPrefab;
            set => startChunkPrefab = value;
        }

        public LevelChunk EndChunkPrefab
        {
            get => endChunkPrefab;
            set => endChunkPrefab = value;
        }

        public List<LevelDifficultyTier> DifficultyTiers => difficultyTiers;

        [Header("--- Последовательность шагов уровня (Legacy) ---")]
        [SerializeField] private List<LevelSegmentStep> levelSteps = new List<LevelSegmentStep>();

        [Header("--- Контейнер сгенерированных объектов ---")]
        [SerializeField] private Transform chunksContainer;

        [Header("--- Статичные объекты сцены (отключать при процедурной генерации) ---")]
        [Tooltip("Объекты старой статичной арены (Land, Walls, Platforms), которые отключаются при спавне чанков")]
        [SerializeField] private List<GameObject> staticObjectsToDisable = new List<GameObject>();

        [Header("--- Прогрессия сложности и циклы (Cycles) ---")]
        [Tooltip("Текущий круг/цикл забега (начинается с 1)")]
        [SerializeField] private int currentCycle = 1;
        public int CurrentCycle => currentCycle;

        [Tooltip("Базовое количество врагов на 1-м круге")]
        [SerializeField] private int baseEnemyCount = 1;

        [Tooltip("Сколько врагов добавляется за каждый новый круг")]
        [SerializeField] private int enemiesPerCycle = 1;

        [Tooltip("Максимальное количество врагов на уровне")]
        [SerializeField] private int maxEnemies = 6;

        [Header("--- Настройки усиления врагов и стиля ---")]
        [Tooltip("Прирост здоровья врагов за каждый круг (+40% по умолчанию)")]
        [SerializeField] private float healthGrowthPerCycle = 0.40f;

        [Tooltip("Прирост выносливости врагов за каждый круг (+10% по умолчанию)")]
        [SerializeField] private float staminaGrowthPerCycle = 0.10f;

        [Tooltip("Прирост скорости замаха врагов за каждый круг (+10% быстрее по умолчанию)")]
        [SerializeField] private float telegraphSpeedGrowthPerCycle = 0.10f;

        [Tooltip("Прирост множителя стиля за каждый круг (+25% к очкам стиля)")]
        [SerializeField] private float styleGrowthPerCycle = 0.25f;

        [Header("--- Настройка баффов врагов за каждый уровень (Inspector) ---")]
        [Tooltip("Включить индивидуальную настройку баффов врагов для каждого уровня из списка ниже")]
        [SerializeField] private bool useCustomLevelBuffs = true;

        [Tooltip("Список баффов для каждого уровня (Элемент 0 = Уровень 1, Элемент 1 = Уровень 2 и т.д.)")]
        [SerializeField] private List<EnemyLevelBuffConfig> levelBuffs = new List<EnemyLevelBuffConfig>();

        [Tooltip("Если уровень выше заданного списка, экстраполировать баффы на основе прироста за уровень")]
        [SerializeField] private bool extrapolateBeyondConfiguredLevels = true;

        public bool UseCustomLevelBuffs
        {
            get => useCustomLevelBuffs;
            set => useCustomLevelBuffs = value;
        }

        public List<EnemyLevelBuffConfig> LevelBuffs => levelBuffs;

        [Header("--- Префаб врага и контейнер ---")]
        [Tooltip("Префаб врага для спавна на точках EnemySpawnPoints")]
        [SerializeField] private GameObject enemyPrefab;

        [SerializeField] private Transform enemiesContainer;

        [Header("--- Барьерные двери боевого сектора ---")]
        [Tooltip("Автоматически ставить запирающиеся барьерные двери на входе и выходе боевой зоны")]
        [SerializeField] private bool enableSectorDoors = true;

        [Tooltip("Опциональный префаб барьерной двери (если не задан, создается процедурный объект)")]
        [SerializeField] private SectorBarrierDoor2D barrierDoorPrefab;

        [Header("--- События ---")]
        public UnityEvent<List<LevelChunk>> onLevelGenerated;

        // Список активных заспавненных чанков текущего уровня
        private readonly List<LevelChunk> _spawnedChunks = new List<LevelChunk>();
        public IReadOnlyList<LevelChunk> SpawnedChunks => _spawnedChunks;

        // Список заспавненных врагов текущего цикла
        private readonly List<GameObject> _spawnedEnemies = new List<GameObject>();
        public IReadOnlyList<GameObject> SpawnedEnemies => _spawnedEnemies;

        private SectorBarrierDoor2D _entranceDoorInstance;
        private SectorBarrierDoor2D _exitDoorInstance;
        private CombatSectorLock2D _combatLockInstance;

        public static LevelSequenceGenerator Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(this);
                return;
            }

            EnsureDefaultLevelBuffs();

            if (resetForceVariantOnPlay)
            {
                forcePreviewCombatVariantIndex = -1;
                debugPrefabIndexInTier = -1;
                tierOverrideMode = TierOverrideMode.AutoByProgress;
                debugSpecificPrefab = null;
            }
        }

        private void Reset()
        {
            EnsureDefaultLevelBuffs(force: true);
        }

        private void OnValidate()
        {
            if (levelBuffs == null || levelBuffs.Count == 0)
            {
                EnsureDefaultLevelBuffs();
            }
        }

        public void EnsureDefaultLevelBuffs(bool force = false)
        {
            if (force || levelBuffs == null || levelBuffs.Count == 0)
            {
                levelBuffs = GetDefaultLevelBuffs();
            }
        }

        public static List<EnemyLevelBuffConfig> GetDefaultLevelBuffs()
        {
            return new List<EnemyLevelBuffConfig>
            {
                new EnemyLevelBuffConfig("Уровень 1 (Старт)", hp: 1.00f, dmg: 1.00f, moveSpd: 1.00f, stam: 1.00f, attackSpd: 1.00f, count: 1, style: 1.00f),
                new EnemyLevelBuffConfig("Уровень 2 (Усиление)", hp: 1.35f, dmg: 1.15f, moveSpd: 1.08f, stam: 1.15f, attackSpd: 1.10f, count: 2, style: 1.25f),
                new EnemyLevelBuffConfig("Уровень 3 (Ветеран)", hp: 1.70f, dmg: 1.30f, moveSpd: 1.15f, stam: 1.30f, attackSpd: 1.20f, count: 3, style: 1.50f),
                new EnemyLevelBuffConfig("Уровень 4 (Мастер)", hp: 2.10f, dmg: 1.45f, moveSpd: 1.22f, stam: 1.50f, attackSpd: 1.30f, count: 4, style: 1.75f),
                new EnemyLevelBuffConfig("Уровень 5 (Элита)", hp: 2.60f, dmg: 1.60f, moveSpd: 1.30f, stam: 1.70f, attackSpd: 1.40f, count: 5, style: 2.00f),
            };
        }

        public EnemyLevelBuffConfig GetBuffsForCycle(int cycle)
        {
            EnsureDefaultLevelBuffs();

            if (useCustomLevelBuffs && levelBuffs != null && levelBuffs.Count > 0)
            {
                if (cycle >= 1 && cycle <= levelBuffs.Count)
                {
                    return levelBuffs[cycle - 1];
                }

                if (cycle > levelBuffs.Count && extrapolateBeyondConfiguredLevels)
                {
                    var last = levelBuffs[levelBuffs.Count - 1];
                    int extra = cycle - levelBuffs.Count;
                    return new EnemyLevelBuffConfig(
                        $"Уровень {cycle} (Экстраполяция)",
                        hp: last.healthMultiplier + extra * healthGrowthPerCycle,
                        dmg: last.damageMultiplier + extra * 0.15f,
                        moveSpd: last.moveSpeedMultiplier + extra * 0.05f,
                        stam: last.staminaMultiplier + extra * staminaGrowthPerCycle,
                        attackSpd: last.telegraphSpeedMultiplier + extra * telegraphSpeedGrowthPerCycle,
                        count: last.enemyCountOverride > 0 ? Mathf.Min(maxEnemies, last.enemyCountOverride + extra * enemiesPerCycle) : 0,
                        style: last.styleMultiplier + extra * styleGrowthPerCycle
                    );
                }

                if (levelBuffs.Count > 0)
                {
                    return levelBuffs[levelBuffs.Count - 1];
                }
            }

            // Фоллбэк: классическая линейная формула
            return new EnemyLevelBuffConfig(
                $"Круг {cycle}",
                hp: 1.0f + (cycle - 1) * healthGrowthPerCycle,
                dmg: 1.0f + (cycle - 1) * 0.15f,
                moveSpd: 1.0f + (cycle - 1) * 0.05f,
                stam: 1.0f + (cycle - 1) * staminaGrowthPerCycle,
                attackSpd: 1.0f + (cycle - 1) * telegraphSpeedGrowthPerCycle,
                count: Mathf.Clamp(baseEnemyCount + (cycle - 1) * enemiesPerCycle, 1, maxEnemies),
                style: 1.0f + (cycle - 1) * styleGrowthPerCycle
            );
        }

        private void Start()
        {
            if (generateOnStart)
            {
                GenerateLevel();
            }
        }

        private void Update()
        {
            // Быстрая перегенерация по горячей клавише F4
            bool triggerRegen = false;

#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                if (UnityEngine.InputSystem.Keyboard.current.f4Key.wasPressedThisFrame)
                {
                    triggerRegen = true;
                }
            }
            else
#endif
            {
                try
                {
                    if (regenerateHotkey != KeyCode.None && Input.GetKeyDown(regenerateHotkey))
                    {
                        triggerRegen = true;
                    }
                }
                catch { /* Игнорируем в случае строгого New Input System */ }
            }

            if (triggerRegen)
            {
                Debug.Log("<color=yellow>[LevelGen]</color> Нажата горячая клавиша перегенерации (F4)...");
                GenerateLevel();
            }
        }

        public int GetActiveTierIndex()
        {
            if (tierOverrideMode == TierOverrideMode.ForceTier1) return 0;
            if (tierOverrideMode == TierOverrideMode.ForceTier2) return 1;
            if (tierOverrideMode == TierOverrideMode.ForceTier3) return 2;

            if (difficultyTiers == null || difficultyTiers.Count == 0) return 0;

            int run = currentRun;
            for (int i = 0; i < difficultyTiers.Count; i++)
            {
                var tier = difficultyTiers[i];
                if (tier != null && run >= tier.minRun && run <= tier.maxRun)
                {
                    return i;
                }
            }

            // Если превышает все пороги (6+), возвращаем последний тир
            return difficultyTiers.Count - 1;
        }

        public LevelDifficultyTier GetActiveTier()
        {
            int idx = GetActiveTierIndex();
            if (difficultyTiers != null && idx >= 0 && idx < difficultyTiers.Count)
            {
                return difficultyTiers[idx];
            }
            return null;
        }

        private LevelChunk _lastPickedTierChunk = null;

        public LevelChunk PickPrefabForActiveTier()
        {
            if (tierOverrideMode == TierOverrideMode.ForceSpecificPrefab && debugSpecificPrefab != null)
            {
                return debugSpecificPrefab;
            }

            var tier = GetActiveTier();
            if (tier == null || tier.chunkPool == null || tier.chunkPool.Count == 0)
            {
                return null;
            }

            var validPool = tier.chunkPool.FindAll(c => c != null);
            if (validPool.Count == 0) return null;

            if (debugPrefabIndexInTier >= 0)
            {
                int safeIdx = Mathf.Clamp(debugPrefabIndexInTier, 0, validPool.Count - 1);
                return validPool[safeIdx];
            }

            if (tierSelectionMode == TierSelectionMode.SequentialByRun)
            {
                int seqIdx = Mathf.Abs(currentRun - tier.minRun) % validPool.Count;
                _lastPickedTierChunk = validPool[seqIdx];
                return _lastPickedTierChunk;
            }
            else if (tierSelectionMode == TierSelectionMode.RandomAvoidRepeat && validPool.Count > 1)
            {
                var filtered = validPool.FindAll(c => c != _lastPickedTierChunk);
                if (filtered.Count > 0)
                {
                    _lastPickedTierChunk = filtered[UnityEngine.Random.Range(0, filtered.Count)];
                    return _lastPickedTierChunk;
                }
            }

            _lastPickedTierChunk = validPool[UnityEngine.Random.Range(0, validPool.Count)];
            return _lastPickedTierChunk;
        }

        public void SetTestRun(int runNumber)
        {
            CurrentRun = runNumber;
            tierOverrideMode = TierOverrideMode.AutoByProgress;
            debugSpecificPrefab = null;
            debugPrefabIndexInTier = -1;
            GenerateLevel();
        }

        public void SetTestTier(int tierIndex)
        {
            if (tierIndex == 0)
            {
                tierOverrideMode = TierOverrideMode.ForceTier1;
                CurrentRun = 1;
            }
            else if (tierIndex == 1)
            {
                tierOverrideMode = TierOverrideMode.ForceTier2;
                CurrentRun = 4;
            }
            else if (tierIndex == 2)
            {
                tierOverrideMode = TierOverrideMode.ForceTier3;
                CurrentRun = 6;
            }
            debugSpecificPrefab = null;
            debugPrefabIndexInTier = -1;
            GenerateLevel();
        }

        public void SetTestPrefab(LevelChunk prefab)
        {
            if (prefab == null) return;
            debugSpecificPrefab = prefab;
            tierOverrideMode = TierOverrideMode.ForceSpecificPrefab;
            debugPrefabIndexInTier = -1;
            GenerateLevel();
        }

        private LevelChunk SpawnChunkInstance(LevelChunk prefab, string instanceName)
        {
            LevelChunk chunkInstance = null;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                chunkInstance = (LevelChunk)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, chunksContainer);
                if (chunkInstance != null)
                {
                    UnityEditor.Undo.RegisterCreatedObjectUndo(chunkInstance.gameObject, "Generate Level Preview");
                }
            }
#endif
            if (chunkInstance == null)
            {
                chunkInstance = Instantiate(prefab, Vector3.zero, Quaternion.identity, chunksContainer);
            }
            chunkInstance.name = instanceName;
            return chunkInstance;
        }

#if UNITY_EDITOR
        [ContextMenu("Автозаполнить тиры по умолчанию из папок")]
        public void PopulateDefaultTiers()
        {
            useDifficultyTiers = true;

            startChunkPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelChunk>("Assets/Prefabs/LevelChunks/Starn and end/Chunk_Start_Intro.prefab");
            endChunkPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelChunk>("Assets/Prefabs/LevelChunks/Starn and end/Chunk_End_Outro.prefab");

            difficultyTiers.Clear();

            // Тир 1: 1-3 прохождения
            var tier1 = new LevelDifficultyTier
            {
                tierName = "Уровень 1 (1-3 прохождения)",
                minRun = 1,
                maxRun = 3,
                chunkPool = new List<LevelChunk>
                {
                    UnityEditor.AssetDatabase.LoadAssetAtPath<LevelChunk>("Assets/Prefabs/LevelChunks/1rd level/First_Level.prefab"),
                    UnityEditor.AssetDatabase.LoadAssetAtPath<LevelChunk>("Assets/Prefabs/LevelChunks/1rd level/Second_Level.prefab"),
                    UnityEditor.AssetDatabase.LoadAssetAtPath<LevelChunk>("Assets/Prefabs/LevelChunks/1rd level/Third_level.prefab")
                }
            };
            difficultyTiers.Add(tier1);

            // Тир 2: 4-5 прохождений
            var tier2 = new LevelDifficultyTier
            {
                tierName = "Уровень 2 (4-5 прохождений)",
                minRun = 4,
                maxRun = 5,
                chunkPool = new List<LevelChunk>
                {
                    UnityEditor.AssetDatabase.LoadAssetAtPath<LevelChunk>("Assets/Prefabs/LevelChunks/2rd level/First_Level.prefab")
                }
            };
            difficultyTiers.Add(tier2);

            // Тир 3: 6+ прохождений
            var tier3 = new LevelDifficultyTier
            {
                tierName = "Уровень 3 (6+ прохождений)",
                minRun = 6,
                maxRun = 9999,
                chunkPool = new List<LevelChunk>
                {
                    UnityEditor.AssetDatabase.LoadAssetAtPath<LevelChunk>("Assets/Prefabs/LevelChunks/3rd level/Chunk_Variant_A_CombatArena.prefab"),
                    UnityEditor.AssetDatabase.LoadAssetAtPath<LevelChunk>("Assets/Prefabs/LevelChunks/3rd level/Chunk_Variant_B_TwoTierElevation.prefab"),
                    UnityEditor.AssetDatabase.LoadAssetAtPath<LevelChunk>("Assets/Prefabs/LevelChunks/3rd level/Chunk_Variant_C_SplitPath.prefab"),
                    UnityEditor.AssetDatabase.LoadAssetAtPath<LevelChunk>("Assets/Prefabs/LevelChunks/3rd level/Chucn_varint_D.prefab")
                }
            };
            difficultyTiers.Add(tier3);

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("<color=#00FFAA><b>[LevelGen]</b></color> Тиры сложности успешно заполнены по умолчанию из папок ассетов!");
        }
#endif

        /// <summary>
        /// Главный метод сборки уровня
        /// </summary>
        [ContextMenu("Сгенерировать уровень (Generate Level)")]
        public void GenerateLevel()
        {
            ClearOldChunks();

            // Отключаем старую статичную арену, если задана
            if (staticObjectsToDisable != null)
            {
                for (int i = 0; i < staticObjectsToDisable.Count; i++)
                {
                    var obj = staticObjectsToDisable[i];
                    if (obj != null) obj.SetActive(false);
                }
            }

            if (chunksContainer == null)
            {
                var containerGo = new GameObject("[Generated_Level_Chunks]");
                containerGo.transform.SetParent(transform);
                chunksContainer = containerGo.transform;
            }

            Vector3 currentExitSocket = startOrigin;
            Transform firstSpawnPoint = null;

            if (useDifficultyTiers)
            {
                var chunksToSpawn = new List<KeyValuePair<string, LevelChunk>>();

                LevelChunk startPrefab = startChunkPrefab;
                if (startPrefab == null && levelSteps != null && levelSteps.Count > 0)
                {
                    startPrefab = levelSteps[0].fixedChunkPrefab;
                }

                if (startPrefab != null)
                {
                    chunksToSpawn.Add(new KeyValuePair<string, LevelChunk>("00_Start", startPrefab));
                }

                LevelChunk combatPrefab = PickPrefabForActiveTier();
                if (combatPrefab == null && levelSteps != null && levelSteps.Count > 1)
                {
                    combatPrefab = PickPrefabForStep(levelSteps[1]);
                }

                if (combatPrefab != null)
                {
                    int activeTierIdx = GetActiveTierIndex();
                    chunksToSpawn.Add(new KeyValuePair<string, LevelChunk>($"01_Tier{activeTierIdx + 1}", combatPrefab));
                }

                LevelChunk endPrefab = endChunkPrefab;
                if (endPrefab == null && levelSteps != null && levelSteps.Count > 2)
                {
                    endPrefab = levelSteps[2].fixedChunkPrefab;
                }

                if (endPrefab != null)
                {
                    chunksToSpawn.Add(new KeyValuePair<string, LevelChunk>("02_End", endPrefab));
                }

                for (int i = 0; i < chunksToSpawn.Count; i++)
                {
                    var pair = chunksToSpawn[i];
                    LevelChunk prefab = pair.Value;
                    if (prefab == null) continue;

                    LevelChunk chunkInstance = SpawnChunkInstance(prefab, $"[{pair.Key}]_{prefab.name}");
                    chunkInstance.SnapEntryTo(currentExitSocket);
                    currentExitSocket = chunkInstance.ExitPoint.position;

                    if (firstSpawnPoint == null && chunkInstance.PlayerSpawnPoint != null)
                    {
                        firstSpawnPoint = chunkInstance.PlayerSpawnPoint;
                    }

                    _spawnedChunks.Add(chunkInstance);
                }
            }
            else
            {
                for (int i = 0; i < levelSteps.Count; i++)
                {
                    var step = levelSteps[i];
                    LevelChunk prefabToSpawn = PickPrefabForStep(step);

                    if (prefabToSpawn == null)
                    {
                        Debug.LogWarning($"[LevelGen] Пропуск шага {i} '{step.segmentName}': не найден подходящий префаб!");
                        continue;
                    }

                    LevelChunk chunkInstance = SpawnChunkInstance(prefabToSpawn, $"[{i:D2}]_{step.segmentName}_{prefabToSpawn.name}");
                    chunkInstance.SnapEntryTo(currentExitSocket);
                    currentExitSocket = chunkInstance.ExitPoint.position;

                    if (firstSpawnPoint == null && chunkInstance.PlayerSpawnPoint != null)
                    {
                        firstSpawnPoint = chunkInstance.PlayerSpawnPoint;
                    }

                    _spawnedChunks.Add(chunkInstance);
                }
            }

            // Перемещаем игрока на стартовую позицию уровня
            if (repositionPlayer && firstSpawnPoint != null)
            {
                RepositionPlayerTo(firstSpawnPoint.position);
            }

            Physics2D.SyncTransforms();

            // Автоматически перестраиваем граф навигации платформ под новую геометрию сгенерированных комнат
            if (Combat.Navigation.PlatformNavGraph2D.Instance != null)
            {
                Combat.Navigation.PlatformNavGraph2D.Instance.BuildNavGraph();
            }

            // Процедурный спавн врагов по нарастающей сложности на платформах
            SpawnEnemiesForCurrentCycle(_spawnedChunks);

            // Установка запирающихся барьерных дверей сектора
            if (enableSectorDoors)
            {
                SetupSectorDoors(_spawnedChunks);
            }

            Physics2D.SyncTransforms();

            int tierNum = GetActiveTierIndex() + 1;
            Debug.Log($"<color=#00FFAA><b>[LevelGen]</b></color> Уровень успешно сгенерирован! Тир: {tierNum} (Забег #{currentRun}, Кристаллов: {crystalsCollected}). Секций: {_spawnedChunks.Count}. Конечная точка X={currentExitSocket.x:F1}, Y={currentExitSocket.y:F2}");
            onLevelGenerated?.Invoke(_spawnedChunks);
        }

        private LevelChunk PickPrefabForStep(LevelSegmentStep step)
        {
            if (step == null) return null;

            if (step.placementType == ChunkPlacementType.FixedSingle)
            {
                return step.fixedChunkPrefab;
            }
            else if (step.placementType == ChunkPlacementType.RandomFromPool)
            {
                if (step.variantPool != null && step.variantPool.Count > 0)
                {
                    // Исключаем пустые элементы, если они случайно попали в список
                    var validList = step.variantPool.FindAll(p => p != null);
                    if (validList.Count > 0)
                    {
                        if (forcePreviewCombatVariantIndex >= 0)
                        {
                            int idx = Mathf.Clamp(forcePreviewCombatVariantIndex, 0, validList.Count - 1);
                            return validList[idx];
                        }

                        int randomIndex = UnityEngine.Random.Range(0, validList.Count);
                        return validList[randomIndex];
                    }
                }
            }

            return null;
        }

        private void RepositionPlayerTo(Vector3 targetPos)
        {
            var player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
            if (player != null)
            {
                var rb = player.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.position = targetPos;
                }
                player.transform.position = targetPos;

                var camFollow = Camera.main != null ? Camera.main.GetComponent<Combat.Cameras.CameraFollow2D>() : null;
                if (camFollow != null)
                {
                    camFollow.SnapToTarget();
                }
            }
        }

        /// <summary>
        /// Переход на следующий цикл сложности после касания Кристалла Победы
        /// </summary>
        [ContextMenu("Следующий круг сложности (Advance Cycle)")]
        public void AdvanceCycleAndRegenerate()
        {
            crystalsCollected++;
            currentRun++;
            currentCycle = currentRun;

            // Полное восстановление здоровья героя при переходе на новый цикл
            var playerHealth = FindAnyObjectByType<Combat.Player.PlayerHealth2D>();
            if (playerHealth != null)
            {
                playerHealth.ResetHealth();
            }
            if (Combat.UI.PlayerHealthBarUI.Instance != null)
            {
                Combat.UI.PlayerHealthBarUI.Instance.RefreshDisplay(instant: true);
                Combat.UI.PlayerHealthBarUI.Instance.TriggerHealFlash();
            }

            int tierIdx = GetActiveTierIndex();
            string tierName = (difficultyTiers != null && tierIdx >= 0 && tierIdx < difficultyTiers.Count) ? difficultyTiers[tierIdx].tierName : $"Тир {tierIdx + 1}";
            Debug.Log($"<color=#FF5555><b>[LevelGen]</b></color> 💎 <b>Кристалл Победы собран!</b> Всего кристаллов: <b>{crystalsCollected}</b>. Переход на забег <b>#{currentRun}</b> -> <b>{tierName}</b>!");
            GenerateLevel();
        }

        private void SpawnEnemiesForCurrentCycle(List<LevelChunk> spawnedChunks)
        {
            ClearEnemies();

            if (!Application.isPlaying && !spawnEnemiesInEditorPreview)
            {
                return;
            }

            // Если префаб врага не задан в инспекторе, пытаемся загрузить стандартный префаб
            if (enemyPrefab == null)
            {
#if UNITY_EDITOR
                enemyPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Enemy_Fighter.prefab");
#endif
            }
            if (enemyPrefab == null)
            {
                Debug.LogWarning("[LevelGen] Не задан enemyPrefab для спавна врагов!");
                return;
            }

            if (enemiesContainer == null)
            {
                var go = new GameObject("[Spawned_Enemies]");
                go.transform.SetParent(transform);
                enemiesContainer = go.transform;
            }

            // Отключаем старого статичного врага со сцены, если он есть
            var staticEnemy = GameObject.Find("Enemy_Fighter");
            if (staticEnemy != null && staticEnemy.transform.parent != enemiesContainer)
            {
                staticEnemy.SetActive(false);
            }

            // Собираем все точки EnemySpawnPoints только из комнат (исключая старт и финал)
            var candidatePoints = new List<Transform>();
            for (int i = 0; i < spawnedChunks.Count; i++)
            {
                if (i == 0 || i == spawnedChunks.Count - 1) continue;
                var chunk = spawnedChunks[i];
                if (chunk == null) continue;

                var points = chunk.EnemySpawnPoints;
                if (points != null)
                {
                    for (int p = 0; p < points.Count; p++)
                    {
                        if (points[p] != null) candidatePoints.Add(points[p]);
                    }
                }
            }

            if (candidatePoints.Count == 0)
            {
                Debug.LogWarning("[LevelGen] В сгенерированных комнатах нет точек EnemySpawnPoints!");
                return;
            }

            // Перемешиваем точки (Фишер-Йетс)
            for (int i = 0; i < candidatePoints.Count; i++)
            {
                int rnd = UnityEngine.Random.Range(i, candidatePoints.Count);
                var temp = candidatePoints[i];
                candidatePoints[i] = candidatePoints[rnd];
                candidatePoints[rnd] = temp;
            }

            // Получаем настройки баффов для текущего уровня/круга (настраиваются в Инспекторе)
            var buffConfig = GetBuffsForCycle(currentCycle);

            int targetEnemyCount;
            if (buffConfig != null && buffConfig.enemyCountOverride > 0)
            {
                targetEnemyCount = Mathf.Clamp(buffConfig.enemyCountOverride, 1, maxEnemies);
            }
            else
            {
                targetEnemyCount = Mathf.Clamp(baseEnemyCount + (currentCycle - 1) * enemiesPerCycle, 1, maxEnemies);
            }
            int spawnCount = Mathf.Min(targetEnemyCount, candidatePoints.Count);

            // Множители характеристик врагов за уровень
            float healthMult = buffConfig != null ? buffConfig.healthMultiplier : (1.0f + (currentCycle - 1) * healthGrowthPerCycle);
            float staminaMult = buffConfig != null ? buffConfig.staminaMultiplier : (1.0f + (currentCycle - 1) * staminaGrowthPerCycle);
            float speedMult = buffConfig != null ? buffConfig.telegraphSpeedMultiplier : (1.0f + (currentCycle - 1) * telegraphSpeedGrowthPerCycle);
            float damageMult = buffConfig != null ? buffConfig.damageMultiplier : 1.0f;
            float moveSpeedMult = buffConfig != null ? buffConfig.moveSpeedMultiplier : 1.0f;
            float styleMult = buffConfig != null ? buffConfig.styleMultiplier : (1.0f + (currentCycle - 1) * styleGrowthPerCycle);

            // Множители Roguelike-улучшений/проклятий
            if (Combat.Roguelike.RoguelikeUpgradeManager.Instance != null)
            {
                healthMult *= Combat.Roguelike.RoguelikeUpgradeManager.Instance.EnemyHealthMultiplier;
                speedMult *= Combat.Roguelike.RoguelikeUpgradeManager.Instance.EnemySpeedMultiplier;
            }

            for (int i = 0; i < spawnCount; i++)
            {
                var spawnTform = candidatePoints[i];
                var enemyObj = Instantiate(enemyPrefab, spawnTform.position, Quaternion.identity, enemiesContainer);
                enemyObj.name = $"Enemy_Fighter_Cycle{currentCycle}_{i + 1}";

                var enemyAI = enemyObj.GetComponent<Combat.EnemyAIController2D>();
                if (enemyAI != null)
                {
                    enemyAI.ApplyDifficultyScaling(healthMult, staminaMult, speedMult, damageMult, moveSpeedMult);
                    enemyAI.RespawnOnDeath = false;
                }

                _spawnedEnemies.Add(enemyObj);
            }

            // Передаем множитель стиля
            if (Combat.Style.StyleManager.Instance != null)
            {
                Combat.Style.StyleManager.Instance.SetCycleMultiplier(styleMult);
            }

            string levelName = buffConfig != null ? buffConfig.levelTitle : $"Круг {currentCycle}";
            Debug.Log($"<color=#FF5555><b>[SPAWN]</b></color> <b>{levelName} (Круг {currentCycle})</b>: Заспавнено {spawnCount} врагов на платформах! (HP: x{healthMult:F2}, Урон: x{damageMult:F2}, Скорость: x{moveSpeedMult:F2}, Замах: x{speedMult:F2}, Выносливость: x{staminaMult:F2}, Стиль: x{styleMult:F2})");
        }

        private void SetupSectorDoors(List<LevelChunk> chunks)
        {
            if (chunks == null || chunks.Count < 2) return;

            // Входной порог боевой зоны: стык между 0-м (стартовым) и 1-м (боевым) чанком
            Vector3 entrancePos = chunks[0].ExitPoint.position;
            // Выходной порог боевой зоны: вход в финальный чанк с кристаллом
            Vector3 exitPos = chunks[chunks.Count - 1].EntryPoint.position;

            _entranceDoorInstance = SpawnOrSetupDoor("[Entrance_Barrier_Door]", entrancePos);
            _exitDoorInstance = SpawnOrSetupDoor("[Exit_Barrier_Door]", exitPos);

            if (_entranceDoorInstance != null) _entranceDoorInstance.ConfigureAsEntrance();
            if (_exitDoorInstance != null) _exitDoorInstance.ConfigureAsExit();

            if (_combatLockInstance == null)
            {
                _combatLockInstance = GetComponent<CombatSectorLock2D>();
                if (_combatLockInstance == null)
                {
                    _combatLockInstance = gameObject.AddComponent<CombatSectorLock2D>();
                }
            }

            _combatLockInstance.InitializeSector(_entranceDoorInstance, _exitDoorInstance, entrancePos, exitPos, _spawnedEnemies);
        }

        private SectorBarrierDoor2D SpawnOrSetupDoor(string doorName, Vector3 worldPos)
        {
            SectorBarrierDoor2D door = null;
            if (barrierDoorPrefab != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    door = (SectorBarrierDoor2D)UnityEditor.PrefabUtility.InstantiatePrefab(barrierDoorPrefab, chunksContainer);
                    if (door != null)
                    {
                        door.transform.position = worldPos;
                        UnityEditor.Undo.RegisterCreatedObjectUndo(door.gameObject, "Spawn Door Preview");
                    }
                }
#endif
                if (door == null)
                {
                    door = Instantiate(barrierDoorPrefab, worldPos, Quaternion.identity, chunksContainer);
                }
            }
            else
            {
                var go = new GameObject(doorName);
                go.transform.SetParent(chunksContainer, false);
                go.transform.position = worldPos;
                door = go.AddComponent<SectorBarrierDoor2D>();
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Spawn Door Preview");
                }
#endif
            }
            door.name = doorName;
            return door;
        }

        private void ClearEnemies()
        {
            _spawnedEnemies.Clear();

            if (enemiesContainer != null)
            {
                for (int i = enemiesContainer.childCount - 1; i >= 0; i--)
                {
                    var child = enemiesContainer.GetChild(i);
                    if (Application.isPlaying)
                    {
                        Destroy(child.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(child.gameObject);
                    }
                }
            }
        }

        /// <summary>
        /// Удаляет ранее сгенерированные чанки
        /// </summary>
        [ContextMenu("Очистить уровень (Clear Old Chunks)")]
        public void ClearOldChunks()
        {
            if (_combatLockInstance != null)
            {
                _combatLockInstance.ClearSector();
            }

            if (_entranceDoorInstance != null)
            {
                if (Application.isPlaying) Destroy(_entranceDoorInstance.gameObject);
                else DestroyImmediate(_entranceDoorInstance.gameObject);
                _entranceDoorInstance = null;
            }

            if (_exitDoorInstance != null)
            {
                if (Application.isPlaying) Destroy(_exitDoorInstance.gameObject);
                else DestroyImmediate(_exitDoorInstance.gameObject);
                _exitDoorInstance = null;
            }

            ClearEnemies();
            _spawnedChunks.Clear();

            // Восстанавливаем видимость статичных объектов сцены
            if (staticObjectsToDisable != null)
            {
                for (int i = 0; i < staticObjectsToDisable.Count; i++)
                {
                    var obj = staticObjectsToDisable[i];
                    if (obj != null) obj.SetActive(true);
                }
            }

            if (chunksContainer != null)
            {
                for (int i = chunksContainer.childCount - 1; i >= 0; i--)
                {
                    var child = chunksContainer.GetChild(i);
                    if (Application.isPlaying)
                    {
                        Destroy(child.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(child.gameObject);
                    }
                }
            }
        }
    }
}
