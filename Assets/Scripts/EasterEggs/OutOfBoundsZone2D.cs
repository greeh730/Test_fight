using System.Collections.Generic;
using UnityEngine;

namespace EasterEggs
{
    /// <summary>
    /// Компонент-триггер зоны выхода за пределы карты (Out of Bounds).
    /// Размещается в чанках [00_Start]_Chunk_Start_Intro и [02_End]_Chunk_End_Outro над потолком, решётками или брешами.
    /// Легко перетаскивается стрелками в окне Scene и масштабируется через BoxCollider2D.
    /// </summary>
    [ExecuteAlways]
    [SelectionBase]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public class OutOfBoundsZone2D : MonoBehaviour
    {
        public static readonly List<OutOfBoundsZone2D> ActiveZones = new List<OutOfBoundsZone2D>();

        [Header("--- Zone Settings ---")]
        [Tooltip("Понятное имя или описание зоны (для отладки)")]
        [SerializeField] private string zoneDescription = "Robert Out-of-Bounds Hitbox";

        [Tooltip("Цвет подсветки зоны в окне Scene")]
        [SerializeField] private Color gizmoColor = new Color(1f, 0.15f, 0.25f, 0.45f);

        private BoxCollider2D _boxCol;

        public BoxCollider2D BoxCollider
        {
            get
            {
                if (_boxCol == null) _boxCol = GetComponent<BoxCollider2D>();
                return _boxCol;
            }
        }

        public Collider2D ZoneCollider => BoxCollider;
        public string ZoneDescription => zoneDescription;

        private void Awake()
        {
            EnsureCollider();
        }

        private void OnEnable()
        {
            EnsureCollider();
            if (!ActiveZones.Contains(this))
            {
                ActiveZones.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveZones.Remove(this);
        }

        private void Reset()
        {
            EnsureCollider();
            if (_boxCol != null)
            {
                _boxCol.size = new Vector2(6f, 4f);
                _boxCol.offset = Vector2.zero;
            }
        }

        public void EnsureCollider()
        {
            if (_boxCol == null) _boxCol = GetComponent<BoxCollider2D>();
            if (_boxCol != null)
            {
                _boxCol.isTrigger = true;
            }
        }

        /// <summary>
        /// Проверяет, перекрывает ли данный коллайдер игрока хотя бы одну активную зону выхода за карту.
        /// </summary>
        public static bool IsColliderInAnyZone(Collider2D playerCol)
        {
            if (playerCol == null) return false;

            for (int i = 0; i < ActiveZones.Count; i++)
            {
                var zone = ActiveZones[i];
                if (zone != null && zone.BoxCollider != null && zone.BoxCollider.enabled)
                {
                    if (zone.BoxCollider.IsTouching(playerCol))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Проверяет, находится ли точка (координаты игрока) внутри любой активной зоны.
        /// </summary>
        public static bool IsPointInAnyZone(Vector2 point)
        {
            for (int i = 0; i < ActiveZones.Count; i++)
            {
                var zone = ActiveZones[i];
                if (zone != null && zone.BoxCollider != null && zone.BoxCollider.enabled)
                {
                    if (zone.BoxCollider.OverlapPoint(point))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void OnDrawGizmos()
        {
            var box = BoxCollider;
            if (box == null) return;

            Vector3 center = transform.TransformPoint(box.offset);
            Vector3 size = Vector3.Scale(box.size, transform.lossyScale);
            size.z = 0.2f;

            // Полупрозрачная неоновая заливка
            Gizmos.color = gizmoColor;
            Gizmos.DrawCube(center, size);

            // Контур
            Gizmos.color = new Color(1f, 0.2f, 0.35f, 0.95f);
            Gizmos.DrawWireCube(center, size);
        }

        private void OnDrawGizmosSelected()
        {
            var box = BoxCollider;
            if (box == null) return;

            Vector3 center = transform.TransformPoint(box.offset);
            Vector3 size = Vector3.Scale(box.size, transform.lossyScale);
            size.z = 0.2f;

            // Яркая подсветка при выделении
            Gizmos.color = new Color(1f, 0.95f, 0.15f, 0.55f);
            Gizmos.DrawCube(center, size);

            Gizmos.color = new Color(1f, 0.95f, 0.15f, 1.0f);
            Gizmos.DrawWireCube(center, size);
        }
    }
}
