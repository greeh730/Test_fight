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

        [Header("--- Последовательность шагов уровня ---")]
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

            if (resetForceVariantOnPlay)
            {
                forcePreviewCombatVariantIndex = -1;
            }
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

            for (int i = 0; i < levelSteps.Count; i++)
            {
                var step = levelSteps[i];
                LevelChunk prefabToSpawn = PickPrefabForStep(step);

                if (prefabToSpawn == null)
                {
                    Debug.LogWarning($"[LevelGen] Пропуск шага {i} '{step.segmentName}': не найден подходящий префаб!");
                    continue;
                }

                // 1. Создаем экземпляр префаба
                LevelChunk chunkInstance = null;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    chunkInstance = (LevelChunk)UnityEditor.PrefabUtility.InstantiatePrefab(prefabToSpawn, chunksContainer);
                    if (chunkInstance != null)
                    {
                        UnityEditor.Undo.RegisterCreatedObjectUndo(chunkInstance.gameObject, "Generate Level Preview");
                    }
                }
#endif
                if (chunkInstance == null)
                {
                    chunkInstance = Instantiate(prefabToSpawn, Vector3.zero, Quaternion.identity, chunksContainer);
                }
                chunkInstance.name = $"[{i:D2}]_{step.segmentName}_{prefabToSpawn.name}";

                // 2. Бесшовно состыковываем вход нового чанка с выходом предыдущего
                chunkInstance.SnapEntryTo(currentExitSocket);

                // 3. Запоминаем выход текущего чанка для следующей секции
                currentExitSocket = chunkInstance.ExitPoint.position;

                // 4. Запоминаем спавн игрока, если это первая найденная точка
                if (firstSpawnPoint == null && chunkInstance.PlayerSpawnPoint != null)
                {
                    firstSpawnPoint = chunkInstance.PlayerSpawnPoint;
                }

                _spawnedChunks.Add(chunkInstance);
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

            Debug.Log($"<color=#00FFAA><b>[LevelGen]</b></color> Уровень успешно сгенерирован! Секций: {_spawnedChunks.Count}. Конечная точка X={currentExitSocket.x:F1}, Y={currentExitSocket.y:F2}");
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
            currentCycle++;
            Debug.Log($"<color=#FF5555><b>[LevelGen]</b></color> Переход на <b>Круг {currentCycle}</b>! Сложность и стиль повышены.");
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

            // Расчет количества врагов для текущего круга
            int targetEnemyCount = Mathf.Clamp(baseEnemyCount + (currentCycle - 1) * enemiesPerCycle, 1, maxEnemies);
            int spawnCount = Mathf.Min(targetEnemyCount, candidatePoints.Count);

            // Множители сложности (базовый рост круга + Roguelike-проклятия/баффы)
            float healthMult = 1.0f + (currentCycle - 1) * healthGrowthPerCycle;
            float staminaMult = 1.0f + (currentCycle - 1) * staminaGrowthPerCycle;
            float speedMult = 1.0f + (currentCycle - 1) * telegraphSpeedGrowthPerCycle;
            float styleMult = 1.0f + (currentCycle - 1) * styleGrowthPerCycle;

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
                    enemyAI.ApplyDifficultyScaling(healthMult, staminaMult, speedMult);
                    enemyAI.RespawnOnDeath = false;
                }

                _spawnedEnemies.Add(enemyObj);
            }

            // Передаем множитель стиля
            if (Combat.Style.StyleManager.Instance != null)
            {
                Combat.Style.StyleManager.Instance.SetCycleMultiplier(styleMult);
            }

            Debug.Log($"<color=#FF5555><b>[SPAWN]</b></color> <b>Круг {currentCycle}</b>: Заспавнено {spawnCount} врагов на платформах! (HP: x{healthMult:F2}, Выносливость: x{staminaMult:F2}, Скорость замаха: x{speedMult:F2}, Стиль: x{styleMult:F2})");
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

            if (_combatLockInstance == null)
            {
                _combatLockInstance = GetComponent<CombatSectorLock2D>();
                if (_combatLockInstance == null)
                {
                    _combatLockInstance = gameObject.AddComponent<CombatSectorLock2D>();
                }
            }

            _combatLockInstance.InitializeSector(_entranceDoorInstance, _exitDoorInstance, entrancePos, _spawnedEnemies);
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
