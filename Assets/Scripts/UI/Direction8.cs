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

        /// <summary>
        /// Возвращает юникод-символ стрелки для данного направления.
        /// </summary>
        public static string ToGlyph(this Direction8 dir)
        {
            return dir switch
            {
                Direction8.Right => "⮕",
                Direction8.UpRight => "⬈",
                Direction8.Up => "⬆",
                Direction8.UpLeft => "⬉",
                Direction8.Left => "⬅",
                Direction8.DownLeft => "⬋",
                Direction8.Down => "⬇",
                Direction8.DownRight => "⬊",
                _ => "•"
            };
        }

        /// <summary>
        /// Преобразует символ стрелки в Direction8.
        /// </summary>
        public static Direction8 FromGlyph(char c)
        {
            return c switch
            {
                '⮕' or '→' or 'R' or 'r' => Direction8.Right,
                '⬈' or '↗' => Direction8.UpRight,
                '⬆' or '↑' or 'U' or 'u' => Direction8.Up,
                '⬉' or '↖' => Direction8.UpLeft,
                '⬅' or '←' or 'L' or 'l' => Direction8.Left,
                '⬋' or '↙' => Direction8.DownLeft,
                '⬇' or '↓' or 'D' or 'd' => Direction8.Down,
                '⬊' or '↘' => Direction8.DownRight,
                _ => Direction8.None
            };
        }

        /// <summary>
        /// Парсит строку символов (например: "⬅⮕" или "⬋⬇⬊⮕⬈") в список направлений.
        /// Пропускает пробелы, запятые и неизвестные знаки.
        /// </summary>
        public static System.Collections.Generic.List<Direction8> ParseSequence(string sequenceString)
        {
            var list = new System.Collections.Generic.List<Direction8>();
            if (string.IsNullOrEmpty(sequenceString)) return list;

            for (int i = 0; i < sequenceString.Length; i++)
            {
                char c = sequenceString[i];
                if (char.IsWhiteSpace(c) || c == ',' || c == '-' || c == '>') continue;
                Direction8 d = FromGlyph(c);
                if (d != Direction8.None)
                {
                    list.Add(d);
                }
            }
            return list;
        }

        /// <summary>
        /// Форматирует последовательность направлений в строку символов со стрелками.
        /// </summary>
        public static string ToGlyphString(this System.Collections.Generic.IEnumerable<Direction8> dirs, string separator = "")
        {
            if (dirs == null) return "";
            var sb = new System.Text.StringBuilder();
            bool first = true;
            foreach (var d in dirs)
            {
                if (!first && !string.IsNullOrEmpty(separator)) sb.Append(separator);
                sb.Append(d.ToGlyph());
                first = false;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Отражает направление по горизонтали, если персонаж смотрит влево (Facing-Relative).
        /// </summary>
        public static Direction8 ToFacingRelative(this Direction8 dir, float facingSign)
        {
            if (facingSign >= 0f || dir == Direction8.None) return dir;

            return dir switch
            {
                Direction8.Right => Direction8.Left,
                Direction8.UpRight => Direction8.UpLeft,
                Direction8.Up => Direction8.Up,
                Direction8.UpLeft => Direction8.UpRight,
                Direction8.Left => Direction8.Right,
                Direction8.DownLeft => Direction8.DownRight,
                Direction8.Down => Direction8.Down,
                Direction8.DownRight => Direction8.DownLeft,
                _ => dir
            };
        }

        /// <summary>
        /// Возвращает расстояние в шагах секторов (0..4) между двумя направлениями по кругу 8 направлений.
        /// 0 = идентичны, 1 = соседний сектор (45°), 2 = 90°, 3 = 135°, 4 = 180° (противоположные).
        /// </summary>
        public static int StepDistance(this Direction8 a, Direction8 b)
        {
            if (a == Direction8.None || b == Direction8.None) return 4;
            int diff = Mathf.Abs((int)a - (int)b);
            if (diff > 4) diff = 8 - diff;
            return diff;
        }

        /// <summary>
        /// Возвращает угловое расстояние в градусах [0..180] между двумя направлениями.
        /// </summary>
        public static float AngularDistance(this Direction8 a, Direction8 b)
        {
            return StepDistance(a, b) * 45f;
        }

        /// <summary>
        /// Определяет Direction8 с учетом "умного магнетизма" (Smart Magnetism) к ожидаемым продолжениям комбо.
        /// Если угол находится в расширенном секторе (по умолчанию до 32.5°) одного из приоритетных направлений,
        /// выбирается это приоритетное направление.
        /// </summary>
        public static Direction8 FromAngleWithMagnetism(float angleDeg, System.Collections.Generic.ICollection<Direction8> favoredDirections, float magneticThresholdDeg = 32.5f)
        {
            if (favoredDirections != null && favoredDirections.Count > 0)
            {
                Direction8 bestFavored = Direction8.None;
                float minFavoredDist = float.MaxValue;

                foreach (var dir in favoredDirections)
                {
                    if (dir == Direction8.None) continue;
                    float dirAngle = dir.ToAngle();
                    float delta = Mathf.Abs(Mathf.DeltaAngle(angleDeg, dirAngle));
                    if (delta <= magneticThresholdDeg && delta < minFavoredDist)
                    {
                        minFavoredDist = delta;
                        bestFavored = dir;
                    }
                }

                if (bestFavored != Direction8.None)
                {
                    return bestFavored;
                }
            }

            return FromAngle(angleDeg);
        }
    }
}
