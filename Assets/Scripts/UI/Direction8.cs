using System;
using UnityEngine;

namespace Combat.UI
{
    /// <summary>
    /// 8 направлений векторного колеса (шаг 45 градусов).
    /// </summary>
    public enum Direction8
    {
        None = -1,
        Right = 0,       // 0° (Восток)
        UpRight = 1,     // 45° (Северо-Восток)
        Up = 2,          // 90° (Север)
        UpLeft = 3,      // 135° (Северо-Запад)
        Left = 4,        // 180° (Запад)
        DownLeft = 5,    // 225° (Юго-Запад)
        Down = 6,        // 270° (Юг)
        DownRight = 7    // 315° (Юго-Восток)
    }

    public static class Direction8Extensions
    {
        private static readonly float Sqrt2Inv = 1f / Mathf.Sqrt(2f);

        /// <summary>
        /// Возвращает центральный угол направления в градусах [0..360).
        /// </summary>
        public static float ToAngle(this Direction8 dir)
        {
            return dir switch
            {
                Direction8.Right => 0f,
                Direction8.UpRight => 45f,
                Direction8.Up => 90f,
                Direction8.UpLeft => 135f,
                Direction8.Left => 180f,
                Direction8.DownLeft => 225f,
                Direction8.Down => 270f,
                Direction8.DownRight => 315f,
                _ => 0f
            };
        }

        /// <summary>
        /// Возвращает единичный двумерный вектор направления.
        /// </summary>
        public static Vector2 ToVector2(this Direction8 dir)
        {
            return dir switch
            {
                Direction8.Right => Vector2.right,
                Direction8.UpRight => new Vector2(Sqrt2Inv, Sqrt2Inv),
                Direction8.Up => Vector2.up,
                Direction8.UpLeft => new Vector2(-Sqrt2Inv, Sqrt2Inv),
                Direction8.Left => Vector2.left,
                Direction8.DownLeft => new Vector2(-Sqrt2Inv, -Sqrt2Inv),
                Direction8.Down => Vector2.down,
                Direction8.DownRight => new Vector2(Sqrt2Inv, -Sqrt2Inv),
                _ => Vector2.zero
            };
        }

        /// <summary>
        /// Определяет Direction8 по углу в градусах [0..360).
        /// Сектор центрирован вокруг своего угла (+- 22.5°).
        /// </summary>
        public static Direction8 FromAngle(float angleDeg)
        {
            angleDeg = Mathf.Repeat(angleDeg, 360f);
            float shifted = Mathf.Repeat(angleDeg + 22.5f, 360f);
            int index = Mathf.FloorToInt(shifted / 45f);
            return (Direction8)Mathf.Clamp(index, 0, 7);
        }

        /// <summary>
        /// Определяет Direction8 по вектору направления с учетом зоны нечувствительности.
        /// </summary>
        public static Direction8 FromVector2(Vector2 vector, float deadzone = 0.05f)
        {
            if (vector.sqrMagnitude < deadzone * deadzone)
            {
                return Direction8.None;
            }

            float angle = Mathf.Atan2(vector.y, vector.x) * Mathf.Rad2Deg;
            return FromAngle(angle);
        }
    }
}
