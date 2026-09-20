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

        [Header("--- Последовательность шагов уровня ---")]
        [SerializeField] private List<LevelSegmentStep> levelSteps = new List<LevelSegmentStep>();

        [Header("--- Контейнер сгенерированных объектов ---")]
        [SerializeField] private Transform chunksContainer;

        [Header("--- Статичные объекты сцены (отключать при процедурной генерации) ---")]
        [Tooltip("Объекты старой статичной арены (Land, Walls, Platforms), которые отключаются при спавне чанков")]
        [SerializeField] private List<GameObject> staticObjectsToDisable = new List<GameObject>();

        [Header("--- События ---")]
        public UnityEvent<List<LevelChunk>> onLevelGenerated;

        // Список активных заспавненных чанков текущего уровня
        private readonly List<LevelChunk> _spawnedChunks = new List<LevelChunk>();
        public IReadOnlyList<LevelChunk> SpawnedChunks => _spawnedChunks;

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
                LevelChunk chunkInstance = Instantiate(prefabToSpawn, Vector3.zero, Quaternion.identity, chunksContainer);
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
        /// Удаляет ранее сгенерированные чанки
        /// </summary>
        [ContextMenu("Очистить уровень (Clear Old Chunks)")]
        public void ClearOldChunks()
        {
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
