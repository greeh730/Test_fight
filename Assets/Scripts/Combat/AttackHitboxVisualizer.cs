using System;
using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Компонент для визуального отображения и интерактивного редактирования хитбоксов атак игрока в окне Scene View и Inspector.
    /// Позволяет переключать атаки, настраивать их размер и смещение через визуальные ручки (Handles) прямо на персонаже.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class AttackHitboxVisualizer : MonoBehaviour
    {
        [Header("--- База данных приёмов ---")]
        [Tooltip("Ссылка на ScriptableObject с настройками всех атак")]
        [SerializeField] private CombatAttackDatabase database;

        [Header("--- Выбор атаки для редактирования ---")]
        [Tooltip("Индекс выбранной атаки из базы данных")]
        [SerializeField] private int selectedAttackIndex = 0;

        [Header("--- Режимы отображения ---")]
        [Tooltip("Направление предпросмотра (Вправо ▶ или Влево ◀)")]
        [SerializeField] private bool previewFacingRight = true;

        [Tooltip("Показывать все хитбоксы одновременно для оценки зоны покрытия")]
        [SerializeField] private bool showAllHitboxes = false;

        [Tooltip("Отображать текстовые бейджи с уроном, зоной и размерами в Scene View")]
        [SerializeField] private bool showLabels = true;

        [Tooltip("Отображать стрелку вектора отталкивания и выпада")]
        [SerializeField] private bool showKnockbackArrow = true;

        [Tooltip("Отображать интерактивные ручки перемещения и изменения размера в Scene View")]
        [SerializeField] private bool enableSceneHandles = true;

        [Header("--- Настройки Gizmos ---")]
        [Tooltip("Рисовать хитбоксы всегда (даже если объект не выбран)")]
        [SerializeField] private bool alwaysDrawGizmos = false;

        [Tooltip("Рисовать хитбоксы при выборе объекта в иерархии")]
        [SerializeField] private bool drawWhenSelected = true;

        public CombatAttackDatabase Database => database;
        public int SelectedAttackIndex
        {
            get => selectedAttackIndex;
            set
            {
                selectedAttackIndex = value;
                ClampIndex();
            }
        }

        public bool PreviewFacingRight
        {
            get => previewFacingRight;
            set => previewFacingRight = value;
        }

        public bool ShowAllHitboxes
        {
            get => showAllHitboxes;
            set => showAllHitboxes = value;
        }

        public bool ShowLabels
        {
            get => showLabels;
            set => showLabels = value;
        }

        public bool ShowKnockbackArrow
        {
            get => showKnockbackArrow;
            set => showKnockbackArrow = value;
        }

        public bool EnableSceneHandles
        {
            get => enableSceneHandles;
            set => enableSceneHandles = value;
        }

        public CombatAttackDatabase.AttackEntry SelectedAttack
        {
            get
            {
                EnsureDatabase();
                if (database == null || database.Attacks == null || database.Attacks.Count == 0) return null;
                ClampIndex();
                return database.Attacks[selectedAttackIndex];
            }
        }

        public void SetDatabase(CombatAttackDatabase db)
        {
            database = db;
            ClampIndex();
        }

        private void OnEnable()
        {
            EnsureDatabase();
            SyncFacingWithSpriteRenderer();
        }

        private void Start()
        {
            EnsureDatabase();
        }

        private void ClampIndex()
        {
            if (database != null && database.Attacks != null && database.Attacks.Count > 0)
            {
                selectedAttackIndex = Mathf.Clamp(selectedAttackIndex, 0, database.Attacks.Count - 1);
            }
            else
            {
                selectedAttackIndex = 0;
            }
        }

        public void EnsureDatabase()
        {
            if (database == null)
            {
                database = Resources.Load<CombatAttackDatabase>("CombatAttackDatabase");
#if UNITY_EDITOR
                if (database == null)
                {
                    database = UnityEditor.AssetDatabase.LoadAssetAtPath<CombatAttackDatabase>("Assets/Resources/CombatAttackDatabase.asset");
                }
                if (database == null)
                {
                    database = CreateOrFindDatabaseAsset();
                }
#endif
            }

            if (database != null && (database.Attacks == null || database.Attacks.Count == 0))
            {
                database.ResetToDefaults();
            }
        }

        private void SyncFacingWithSpriteRenderer()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null && Application.isPlaying)
            {
                previewFacingRight = !sr.flipX;
            }
        }

        public Vector2 GetHitboxCenterWorld(CombatAttackDatabase.AttackEntry entry)
        {
            if (entry == null || entry.attackConfig == null) return transform.position;
            float facing = previewFacingRight ? 1f : -1f;
            return (Vector2)transform.position + new Vector2(entry.attackConfig.hitboxOffset.x * facing, entry.attackConfig.hitboxOffset.y);
        }

        private void OnDrawGizmos()
        {
            if (alwaysDrawGizmos)
            {
                DrawGizmosInternal();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (drawWhenSelected && !alwaysDrawGizmos)
            {
                DrawGizmosInternal();
            }
        }

        private void DrawGizmosInternal()
        {
            EnsureDatabase();
            if (database == null || database.Attacks == null || database.Attacks.Count == 0) return;

            if (showAllHitboxes)
            {
                for (int i = 0; i < database.Attacks.Count; i++)
                {
                    bool isSelected = (i == selectedAttackIndex);
                    DrawSingleHitboxGizmo(database.Attacks[i], isSelected, alphaMult: isSelected ? 1f : 0.45f);
                }
            }
            else
            {
                var current = SelectedAttack;
                if (current != null)
                {
                    DrawSingleHitboxGizmo(current, true, alphaMult: 1f);
                }
            }
        }

        private void DrawSingleHitboxGizmo(CombatAttackDatabase.AttackEntry entry, bool isSelected, float alphaMult = 1f)
        {
            if (entry == null || entry.attackConfig == null) return;
            var cfg = entry.attackConfig;

            Vector2 center = GetHitboxCenterWorld(entry);
            Vector2 size = cfg.hitboxSize;

            Color baseColor = cfg.hitboxColor;
            if (baseColor.a < 0.05f) baseColor = Color.red;

            // Translucent fill
            Color fill = new Color(baseColor.r, baseColor.g, baseColor.b, 0.22f * alphaMult);
            Gizmos.color = fill;
            Gizmos.DrawCube(new Vector3(center.x, center.y, 0f), new Vector3(size.x, size.y, 0.01f));

            // Wire border
            Color border = new Color(baseColor.r, baseColor.g, baseColor.b, (isSelected ? 0.95f : 0.6f) * alphaMult);
            Gizmos.color = border;
            Gizmos.DrawWireCube(new Vector3(center.x, center.y, 0f), new Vector3(size.x, size.y, 0.01f));

            // Origin crosshair & line to player
            if (isSelected)
            {
                Gizmos.color = new Color(1f, 1f, 1f, 0.45f);
                Gizmos.DrawLine(transform.position, center);
            }
        }

#if UNITY_EDITOR
        public static CombatAttackDatabase CreateOrFindDatabaseAsset()
        {
            string folder = "Assets/Resources";
            if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
            {
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");
            }

            string path = "Assets/Resources/CombatAttackDatabase.asset";
            var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<CombatAttackDatabase>(path);
            if (existing != null) return existing;

            var newDb = ScriptableObject.CreateInstance<CombatAttackDatabase>();
            newDb.ResetToDefaults();
            UnityEditor.AssetDatabase.CreateAsset(newDb, path);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();
            Debug.Log($"<color=lime>[CombatAttackDatabase]</color> Создана база данных атак по пути: {path}");
            return newDb;
        }
#endif
    }
}
