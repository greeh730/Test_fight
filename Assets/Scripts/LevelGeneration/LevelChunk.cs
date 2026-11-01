using UnityEngine;

namespace LevelGeneration
{
    /// <summary>
    /// Компонент фрагмента (чанка) уровня.
    /// Содержит точки сопряжения (Entry / Exit Sockets) для бесшовной стыковки
    /// без смещений по высоте и горизонтальных зазоров.
    /// </summary>
    [DisallowMultipleComponent]
    [SelectionBase]
    public class LevelChunk : MonoBehaviour
    {
        [Header("--- Точки стыковки (Sockets) ---")]
        [Tooltip("Точка входа: место, где начинается пол на левом краю чанка")]
        [SerializeField] private Transform entryPoint;

        [Tooltip("Точка выхода: место, где заканчивается пол на правом краю чанка")]
        [SerializeField] private Transform exitPoint;

        [Header("--- Спавн игрока (опционально) ---")]
        [Tooltip("Точка появления игрока (актуально для стартового чанка)")]
        [SerializeField] private Transform playerSpawnPoint;

        [Header("--- Точки спавна врагов (Enemy Spawn Points) ---")]
        [Tooltip("Фиксированные точки на платформах комнаты, где могут появляться враги")]
        [SerializeField] private System.Collections.Generic.List<Transform> enemySpawnPoints = new System.Collections.Generic.List<Transform>();

        public Transform EntryPoint => entryPoint != null ? entryPoint : transform;
        public Transform ExitPoint => exitPoint != null ? exitPoint : transform;
        public Transform PlayerSpawnPoint => playerSpawnPoint != null ? playerSpawnPoint : null;
        public System.Collections.Generic.IReadOnlyList<Transform> EnemySpawnPoints => enemySpawnPoints;

        /// <summary>
        /// Длина чанка между точками входа и выхода
        /// </summary>
        public float Length => Vector3.Distance(EntryPoint.position, ExitPoint.position);

        /// <summary>
        /// Математически точно выравнивает данный чанк так,
        /// чтобы его EntryPoint в точности совпал с targetExitPosition.
        /// Гарантирует смещение по высоте = 0.0000.
        /// </summary>
        public void SnapEntryTo(Vector3 targetExitPosition)
        {
            Vector3 offset = targetExitPosition - EntryPoint.position;
            transform.position += offset;
        }

        private void Reset()
        {
            // Автоматически создаем и настраиваем сокеты при добавлении компонента
            if (entryPoint == null || exitPoint == null)
            {
                CreateDefaultSockets();
            }
        }

        /// <summary>
        /// Автоматически создает сокеты входа и выхода в 1 клик
        /// </summary>
        [ContextMenu("Создать сокеты (Create Default Sockets)")]
        public void CreateDefaultSockets(float defaultLength = 20f)
        {
            if (entryPoint == null)
            {
                var existingEntry = transform.Find("Socket_Entry");
                if (existingEntry == null)
                {
                    var go = new GameObject("Socket_Entry");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = Vector3.zero;
                    entryPoint = go.transform;
                }
                else
                {
                    entryPoint = existingEntry;
                }
            }

            if (exitPoint == null)
            {
                var existingExit = transform.Find("Socket_Exit");
                if (existingExit == null)
                {
                    var go = new GameObject("Socket_Exit");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = new Vector3(defaultLength, 0f, 0f);
                    exitPoint = go.transform;
                }
                else
                {
                    exitPoint = existingExit;
                }
            }
        }

        /// <summary>
        /// Добавляет точку спавна игрока внутри чанка
        /// </summary>
        [ContextMenu("Добавить точку спавна игрока (Add Player Spawn Point)")]
        public void AddPlayerSpawnPoint()
        {
            if (playerSpawnPoint == null)
            {
                var existingSpawn = transform.Find("PlayerSpawnPoint");
                if (existingSpawn == null)
                {
                    var go = new GameObject("PlayerSpawnPoint");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = new Vector3(3.5f, 1.55f, 0f);
                    playerSpawnPoint = go.transform;
                }
                else
                {
                    playerSpawnPoint = existingSpawn;
                }
            }
        }

        /// <summary>
        /// Гарантирует, что оба сокета находятся строго на высоте Y = 0
        /// </summary>
        [ContextMenu("Выровнять сокеты по Y=0 (Align Sockets to Y=0)")]
        public void AlignSocketsToZero()
        {
            if (entryPoint != null)
            {
                var p = entryPoint.localPosition;
                entryPoint.localPosition = new Vector3(p.x, 0f, p.z);
            }
            if (exitPoint != null)
            {
                var p = exitPoint.localPosition;
                exitPoint.localPosition = new Vector3(p.x, 0f, p.z);
            }
        }

        /// <summary>
        /// Добавляет точку спавна врага на заданной локальной позиции
        /// </summary>
        [ContextMenu("Добавить точку спавна врага (Add Enemy Spawn Point)")]
        public Transform AddEnemySpawnPoint(Vector3? localPos = null)
        {
            Vector3 pos = localPos ?? new Vector3(Length * 0.5f, 0.55f, 0f);
            int nextIndex = enemySpawnPoints.Count + 1;
            var go = new GameObject($"EnemySpawnPoint_{nextIndex}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            enemySpawnPoints.Add(go.transform);
            return go.transform;
        }

        /// <summary>
        /// Автоматически находит все точки спавна врагов (включая ArenaEnemySpawnPoint2D)
        /// среди дочерних объектов чанка и заносит их в список.
        /// </summary>
        [ContextMenu("Собрать все точки спавна (Scan & Collect Spawn Points)")]
        public void CollectAllSpawnPoints()
        {
            if (enemySpawnPoints == null) enemySpawnPoints = new System.Collections.Generic.List<Transform>();
            enemySpawnPoints.Clear();

            var richSpawners = GetComponentsInChildren<ArenaEnemySpawnPoint2D>(true);
            for (int i = 0; i < richSpawners.Length; i++)
            {
                if (richSpawners[i] != null && !enemySpawnPoints.Contains(richSpawners[i].transform))
                {
                    enemySpawnPoints.Add(richSpawners[i].transform);
                }
            }

            // Дополнительно ищем старые объекты с именем EnemySpawnPoint
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.name.IndexOf("EnemySpawnPoint", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (!enemySpawnPoints.Contains(child))
                    {
                        enemySpawnPoints.Add(child);
                    }
                }
            }

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// Сканирует тайлмап Tilemap_Solid, находит крайнюю правую плитку пола
        /// и автоматически выставляет сокет выхода (Socket_Exit) точно на край арены!
        /// Идеально для арен любого размера (наклонных, вертикальных, сверхшироких).
        /// </summary>
        [ContextMenu("Подогнать сокет выхода под край тайлов (Auto-Fit Exit Socket)")]
        public bool AutoFitExitSocketToTilemap()
        {
            var tilemaps = GetComponentsInChildren<UnityEngine.Tilemaps.Tilemap>(true);
            UnityEngine.Tilemaps.Tilemap solidTm = null;
            for (int i = 0; i < tilemaps.Length; i++)
            {
                if (tilemaps[i].name.IndexOf("Solid", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    solidTm = tilemaps[i];
                    break;
                }
            }

            if (solidTm == null && tilemaps.Length > 0) solidTm = tilemaps[0];
            if (solidTm == null)
            {
                Debug.LogWarning($"[LevelChunk] '{name}': Не найден тайлмап Solid для автоподгонки сокета выхода!");
                return false;
            }

            solidTm.CompressBounds();
            var bounds = solidTm.cellBounds;

            int maxX = int.MinValue;
            int rightmostFloorY = 0;
            bool foundTile = false;

            for (int x = bounds.xMax; x >= bounds.xMin; x--)
            {
                for (int y = bounds.yMin; y <= bounds.yMax; y++)
                {
                    var tile = solidTm.GetTile(new Vector3Int(x, y, 0));
                    if (tile != null)
                    {
                        if (x > maxX)
                        {
                            maxX = x;
                            rightmostFloorY = y;
                            foundTile = true;
                        }
                    }
                }
                if (foundTile) break;
            }

            if (!foundTile)
            {
                Debug.LogWarning($"[LevelChunk] '{name}': В тайлмапе нет плиток для автоподгонки сокета выхода!");
                return false;
            }

            CreateDefaultSockets();
            if (exitPoint != null)
            {
                // Ставим сокет на стыке: следующий X за крайней плиткой, пол на уровне верхней грани плитки пола (+1 по Y)
                Vector3Int cellPos = new Vector3Int(maxX + 1, rightmostFloorY + 1, 0);
                Vector3 localPos = solidTm.transform.localPosition + solidTm.CellToLocal(cellPos);
                exitPoint.localPosition = localPos;

#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
                Debug.Log($"<color=#00FFAA><b>[LevelChunk]</b></color> Сокет выхода '{name}' автоматически установлен на позицию: {localPos}");
                return true;
            }

            return false;
        }

        public void ClearEnemySpawnPoints()
        {
            for (int i = enemySpawnPoints.Count - 1; i >= 0; i--)
            {
                if (enemySpawnPoints[i] != null)
                {
                    DestroyImmediate(enemySpawnPoints[i].gameObject);
                }
            }
            enemySpawnPoints.Clear();
        }

        private void OnDrawGizmos()
        {
            // Визуализация сокетов в окне редактора Unity
            Vector3 entry = EntryPoint.position;
            Vector3 exit = ExitPoint.position;

            // Зеленый маркер: точка входа
            Gizmos.color = new Color(0.1f, 1.0f, 0.3f, 0.9f);
            Gizmos.DrawWireSphere(entry, 0.45f);
            Gizmos.DrawLine(entry + Vector3.down * 0.4f, entry + Vector3.up * 0.4f);

            // Красный маркер: точка выхода
            Gizmos.color = new Color(1.0f, 0.2f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(exit, 0.45f);
            Gizmos.DrawLine(exit + Vector3.down * 0.4f, exit + Vector3.up * 0.4f);

            // Соединительная пунктирная линия уровня пола
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.45f);
            Gizmos.DrawLine(entry, exit);

            // Если есть спавн игрока
            if (playerSpawnPoint != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(playerSpawnPoint.position + Vector3.up * 0.75f, new Vector3(0.8f, 1.5f, 0.1f));
            }

            // Багровые маркеры для точек спавна врагов
            if (enemySpawnPoints != null)
            {
                Gizmos.color = new Color(1.0f, 0.25f, 0.25f, 0.9f);
                for (int i = 0; i < enemySpawnPoints.Count; i++)
                {
                    var sp = enemySpawnPoints[i];
                    if (sp != null)
                    {
                        Gizmos.DrawWireSphere(sp.position, 0.35f);
                        Gizmos.DrawWireCube(sp.position + Vector3.up * 0.5f, new Vector3(0.8f, 1.0f, 0.1f));
                    }
                }
            }
        }
    }
}
