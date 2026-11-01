// Assets/Scripts/Fencing/AttackDirection.cs
using UnityEngine;

namespace Fencing
{
    /// <summary>
    /// Направления фехтовальных атак.
    /// Включает 6 направлений лепестков, вертикальные/горизонтальные векторы и нейтральное значение None.
    /// </summary>
    public enum AttackDirection
    {
        Left,
        Center,
        Right,
        Up,
        UpRight,
        DownRight,
        Down,
        DownLeft,
        UpLeft,
        None
    }

    /// <summary>
    /// Вспомогательные методы для работы с направлениями атак.
    /// </summary>
    public static class AttackDirectionExtensions
    {
        /// <summary>
        /// Возвращает единичный двумерный вектор, соответствующий направлению атаки.
        /// </summary>
        public static Vector2 ToVector2(this AttackDirection direction)
        {
            switch (direction)
            {
                case AttackDirection.Up:        return Vector2.up;
                case AttackDirection.UpRight:   return new Vector2(0.766f, 0.643f).normalized;   // ~40°
                case AttackDirection.Right:     return Vector2.right;                            // 0°
                case AttackDirection.DownRight: return new Vector2(0.766f, -0.643f).normalized;  // -40°
                case AttackDirection.Down:      return Vector2.down;
                case AttackDirection.DownLeft:  return new Vector2(-0.766f, -0.643f).normalized; // -140°
                case AttackDirection.Left:      return Vector2.left;                             // 180°
                case AttackDirection.UpLeft:    return new Vector2(-0.766f, 0.643f).normalized;  // 140°
                default:                        return Vector2.zero;
            }
        }

        /// <summary>
        /// Возвращает центральный угол направления в градусах [-180, 180].
        /// </summary>
        public static float ToAngle(this AttackDirection direction)
        {
            switch (direction)
            {
                case AttackDirection.Up:        return 90f;
                case AttackDirection.UpRight:   return 40f;
                case AttackDirection.Right:     return 0f;
                case AttackDirection.DownRight: return -40f;
                case AttackDirection.Down:      return -90f;
                case AttackDirection.DownLeft:  return -140f;
                case AttackDirection.Left:      return 180f;
                case AttackDirection.UpLeft:    return 140f;
                default:                        return 0f;
            }
        }

        /// <summary>
        /// Преобразует направление атаки в вертикальный уровень фехтования (Голова, Тело, Ноги).
        /// </summary>
        public static CombatLevel ToCombatLevel(this AttackDirection direction)
        {
            switch (direction)
            {
                case AttackDirection.UpLeft:
                case AttackDirection.UpRight:
                case AttackDirection.Up:
                    return CombatLevel.High;

                case AttackDirection.Left:
                case AttackDirection.Right:
                case AttackDirection.Center:
                    return CombatLevel.Mid;

                case AttackDirection.DownLeft:
                case AttackDirection.DownRight:
                case AttackDirection.Down:
                    return CombatLevel.Low;

                default:
                    return CombatLevel.Mid;
            }
        }
    }

    /// <summary>
    /// Вертикальные уровни боевой системы: Верх (Голова), Центр (Тело), Низ (Ноги).
    /// </summary>
    public enum CombatLevel
    {
        High,
        Mid,
        Low
    }
}
