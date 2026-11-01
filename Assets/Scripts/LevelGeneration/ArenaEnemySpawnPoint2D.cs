using System;
using UnityEngine;

namespace LevelGeneration
{
    public enum EnemySpawnType
    {
        [Tooltip("Стандартный базовый боец")]
        Standard = 0,

        [Tooltip("Тяжелый / бронированный боец")]
        Heavy = 1,

        [Tooltip("Быстрый / агрессивный боец")]
        Fast = 2,

        [Tooltip("Элитный боец с повышенным здоровьем и уроном")]
        Elite = 3,

        [Tooltip("Дальнобойный боец / стрелок")]
        Ranged = 4,

        [Tooltip("Враг для засады (активируется по триггеру)")]
        Ambush = 5,

        [Tooltip("Пользовательский префаб из поля Custom Enemy Prefab")]
        Custom = 6
    }

    public enum SpawnFacingDirection
    {
        [Tooltip("Лицом влево (в сторону входа на арену)")]
        Left = 0,

        [Tooltip("Лицом вправо (в сторону выхода с арены)")]
        Right = 1,

        [Tooltip("Лицом к центру арены")]
        TowardCenter = 2,

        [Tooltip("Лицом к игроку при появлении")]
        TowardPlayer = 3
    }

    /// <summary>
    /// Точка спавна врага на арене для визуального конструктора уровней.
    /// Позволяет задавать тип врага, направление взгляда, модификаторы характеристик
    /// и индивидуальный префаб.
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class ArenaEnemySpawnPoint2D : MonoBehaviour
    {
        [Header("--- Тип и префаб врага ---")]
        [Tooltip("Классификация врага для этого спавнера")]
        [SerializeField] private EnemySpawnType spawnType = EnemySpawnType.Standard;

        [Tooltip("Опциональный персональный префаб врага (переопределяет стандартный префаб генератора)")]
        [SerializeField] private GameObject customEnemyPrefab;

        [Header("--- Направление и размещение ---")]
        [Tooltip("Начальное направление взгляда при спавне")]
        [SerializeField] private SpawnFacingDirection facingDirection = SpawnFacingDirection.Left;

        [Tooltip("Волна спавна (0 = появляется сразу при входе игрока, 1 = вторая волна и т.д.)")]
        [SerializeField] private int waveIndex = 0;

        [Tooltip("Гарантированный спавн (если включено, враг появится обязательно, даже если лимит на арену меньше)")]
        [SerializeField] private bool isGuaranteed = false;

        [Range(0f, 1f)]
        [Tooltip("Вероятность спавна врага в этой точке (1.0 = 100%)")]
        [SerializeField] private float spawnChance = 1.0f;

        [Header("--- Модификаторы характеристик ---")]
        [Tooltip("Индивидуальный множитель HP (1.0 = обычное для тира)")]
        [SerializeField] private float healthMultiplier = 1.0f;

        [Tooltip("Индивидуальный множитель урона (1.0 = обычный для тира)")]
        [SerializeField] private float damageMultiplier = 1.0f;

        [Tooltip("Индивидуальный множитель скорости (1.0 = обычный для тира)")]
        [SerializeField] private float speedMultiplier = 1.0f;

        [Header("--- Описание / заметка ---")]
        [SerializeField] private string editorNote = "";

        // Getters
        public EnemySpawnType SpawnType => spawnType;
        public GameObject CustomEnemyPrefab => customEnemyPrefab;
        public SpawnFacingDirection FacingDirection => facingDirection;
        public int WaveIndex => waveIndex;
        public bool IsGuaranteed => isGuaranteed;
        public float SpawnChance => spawnChance;
        public float HealthMultiplier => healthMultiplier;
        public float DamageMultiplier => damageMultiplier;
        public float SpeedMultiplier => speedMultiplier;
        public string EditorNote => editorNote;

        public void SetSpawnType(EnemySpawnType type) => spawnType = type;
        public void SetCustomPrefab(GameObject prefab) => customEnemyPrefab = prefab;
        public void SetFacing(SpawnFacingDirection facing) => facingDirection = facing;

        /// <summary>
        /// Примагнитить точку строго к поверхности пола под ней
        /// </summary>
        [ContextMenu("Примагнитить к полу (Snap to Floor)")]
        public void SnapToFloor()
        {
            Vector2 origin = transform.position;
            RaycastHit2D hit = Physics2D.Raycast(origin + Vector2.up * 0.5f, Vector2.down, 15f);
            if (hit.collider != null)
            {
                // Ставим точку чуть выше коллизии (на высоту ступней врага)
                transform.position = new Vector3(origin.x, hit.point.y + 0.05f, transform.position.z);
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Color baseColor;
            switch (spawnType)
            {
                case EnemySpawnType.Standard: baseColor = new Color(1.0f, 0.85f, 0.1f, 0.9f); break; // Желтый
                case EnemySpawnType.Heavy:    baseColor = new Color(1.0f, 0.45f, 0.0f, 0.95f); break; // Оранжевый
                case EnemySpawnType.Fast:     baseColor = new Color(0.2f, 0.9f, 1.0f, 0.95f); break; // Голубой
                case EnemySpawnType.Elite:    baseColor = new Color(0.95f, 0.15f, 0.35f, 1.0f); break; // Рубиновый
                case EnemySpawnType.Ranged:   baseColor = new Color(0.4f, 1.0f, 0.3f, 0.95f); break; // Зеленый
                case EnemySpawnType.Ambush:   baseColor = new Color(0.7f, 0.2f, 1.0f, 0.95f); break; // Фиолетовый
                default:                      baseColor = new Color(0.9f, 0.9f, 0.9f, 0.9f); break;
            }

            Vector3 pos = transform.position;

            // Базовый круг на платформе
            Gizmos.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.3f);
            Gizmos.DrawWireSphere(pos, 0.5f);
            Gizmos.DrawLine(pos - Vector3.right * 0.4f, pos + Vector3.right * 0.4f);

            // Силуэт тела
            Gizmos.color = baseColor;
            Gizmos.DrawLine(pos, pos + Vector3.up * 1.6f);
            Gizmos.DrawWireSphere(pos + Vector3.up * 1.35f, 0.25f); // Голова
            Gizmos.DrawWireCube(pos + Vector3.up * 0.7f, new Vector3(0.5f, 0.7f, 0.1f)); // Торс

            // Стрелка направления взгляда
            Vector3 lookDir = Vector3.left;
            if (facingDirection == SpawnFacingDirection.Right) lookDir = Vector3.right;
            else if (facingDirection == SpawnFacingDirection.TowardCenter)
            {
                var parentChunk = GetComponentInParent<LevelChunk>();
                if (parentChunk != null)
                {
                    float midX = (parentChunk.EntryPoint.position.x + parentChunk.ExitPoint.position.x) * 0.5f;
                    lookDir = (midX > pos.x) ? Vector3.right : Vector3.left;
                }
            }

            Vector3 arrowStart = pos + Vector3.up * 0.8f;
            Vector3 arrowEnd = arrowStart + lookDir * 0.8f;
            Gizmos.DrawLine(arrowStart, arrowEnd);
            Gizmos.DrawLine(arrowEnd, arrowEnd - lookDir * 0.2f + Vector3.up * 0.15f);
            Gizmos.DrawLine(arrowEnd, arrowEnd - lookDir * 0.2f - Vector3.up * 0.15f);

            // Метка типа
            string label = $"[Враг: {spawnType}]";
            if (waveIndex > 0) label += $" (Волна {waveIndex})";
            if (isGuaranteed) label += " ★";
            UnityEditor.Handles.color = baseColor;
            UnityEditor.Handles.Label(pos + Vector3.up * 1.9f, label);
        }
#endif
    }
}
