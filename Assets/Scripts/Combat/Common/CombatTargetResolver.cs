using System.Collections.Generic;
using UnityEngine;

namespace Combat.Common
{
    /// <summary>
    /// Универсальный менеджер и резолвер боевых целей (ICombatEntity2D).
    /// </summary>
    public static class CombatTargetResolver
    {
        private static readonly List<ICombatEntity2D> _activeEntities = new List<ICombatEntity2D>();

        public static void Register(ICombatEntity2D entity)
        {
            if (entity != null && !_activeEntities.Contains(entity))
            {
                _activeEntities.Add(entity);
            }
        }

        public static void Unregister(ICombatEntity2D entity)
        {
            if (entity != null)
            {
                _activeEntities.Remove(entity);
            }
        }

        /// <summary>
        /// Извлекает ICombatEntity2D из коллайдера (включая Hurtbox или родительские объекты).
        /// </summary>
        public static bool TryResolve(Collider2D col, out ICombatEntity2D entity)
        {
            entity = null;
            if (col == null) return false;

            // 1. Проверяем CombatHurtbox2D
            var hb = col.GetComponent<CombatHurtbox2D>();
            if (hb != null)
            {
                entity = hb.GetTargetReceiver() as ICombatEntity2D;
                if (entity != null) return true;
            }

            // 2. Проверяем напрямую на объекте и его родителях
            entity = col.GetComponent<ICombatEntity2D>() ?? col.GetComponentInParent<ICombatEntity2D>();
            return entity != null;
        }

        /// <summary>
        /// Проверяет, является ли коллайдер живой и доступной целью.
        /// </summary>
        public static bool IsAliveTarget(Collider2D col, out ICombatEntity2D entity)
        {
            if (TryResolve(col, out entity))
            {
                return entity != null && !entity.IsDead;
            }
            return false;
        }

        /// <summary>
        /// Находит ближайшую живую боевую цель в заданном радиусе.
        /// </summary>
        public static bool FindNearest(Vector3 origin, float maxDistance, out ICombatEntity2D nearestEntity)
        {
            nearestEntity = null;
            float minDist = maxDistance;

            // Очищаем null ссылки из реестра
            for (int i = _activeEntities.Count - 1; i >= 0; i--)
            {
                var ent = _activeEntities[i];
                if (ent == null || ent.gameObject == null)
                {
                    _activeEntities.RemoveAt(i);
                    continue;
                }

                if (ent.IsDead) continue;

                float dist = Vector2.Distance(origin, ent.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearestEntity = ent;
                }
            }

            // Fallback: если в реестре никого нет, ищем через MonoBehaviour
            if (nearestEntity == null && _activeEntities.Count == 0)
            {
                var all = Object.FindObjectsByType<MonoBehaviour>();
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] is ICombatEntity2D ent && !ent.IsDead)
                    {
                        float dist = Vector2.Distance(origin, ent.transform.position);
                        if (dist < minDist)
                        {
                            minDist = dist;
                            nearestEntity = ent;
                        }
                    }
                }
            }

            return nearestEntity != null;
        }

        /// <summary>
        /// Находит ближайшую живую боевую цель с заданной стороны по горизонтали (+1 вправо, -1 влево).
        /// </summary>
        public static bool FindNearestInDirection(Vector3 origin, float dirSignX, float maxDistance, out ICombatEntity2D nearestEntity)
        {
            nearestEntity = null;
            float minDist = maxDistance;

            for (int i = _activeEntities.Count - 1; i >= 0; i--)
            {
                var ent = _activeEntities[i];
                if (ent == null || ent.gameObject == null)
                {
                    _activeEntities.RemoveAt(i);
                    continue;
                }

                if (ent.IsDead) continue;

                float diffX = ent.transform.position.x - origin.x;
                if (Mathf.Sign(diffX) != Mathf.Sign(dirSignX)) continue;

                float dist = Vector2.Distance(origin, ent.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearestEntity = ent;
                }
            }

            return nearestEntity != null;
        }

        /// <summary>
        /// Фильтрует массив коллайдеров и возвращает список уникальных живых целей.
        /// </summary>
        public static List<ICombatEntity2D> GetUniqueAliveTargets(Collider2D[] colliders, GameObject self = null)
        {
            var result = new List<ICombatEntity2D>();
            var seen = new HashSet<ICombatEntity2D>();

            if (colliders == null) return result;

            for (int i = 0; i < colliders.Length; i++)
            {
                var col = colliders[i];
                if (col == null) continue;
                if (self != null && (col.gameObject == self || col.transform.IsChildOf(self.transform))) continue;

                if (IsAliveTarget(col, out var entity))
                {
                    if (seen.Add(entity))
                    {
                        result.Add(entity);
                    }
                }
            }

            return result;
        }
    }
}
