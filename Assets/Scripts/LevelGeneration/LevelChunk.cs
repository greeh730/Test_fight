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

        public Transform EntryPoint => entryPoint != null ? entryPoint : transform;
        public Transform ExitPoint => exitPoint != null ? exitPoint : transform;
        public Transform PlayerSpawnPoint => playerSpawnPoint != null ? playerSpawnPoint : null;

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
        }
    }
}
