using System;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Боевые зоны попадания и защиты (3-зонная модель).
    /// Битовые флаги позволяют атакам покрывать одну или несколько зон одновременно.
    /// </summary>
    [Flags]
    public enum CombatZone
    {
        None = 0,
        Low  = 1 << 0, // Нижняя зона (ноги, подсечки, стелющиеся удары)
        Mid  = 1 << 1, // Средняя зона (торс, корпус, прямые выпады)
        High = 1 << 2  // Верхняя зона (голова, верхние рубящие, воздух/Anti-Air)
    }

    public static class CombatZoneExtensions
    {
        /// <summary>
        /// Проверяет, перекрывается ли маска атаки с зоной цели.
        /// </summary>
        public static bool Overlaps(this CombatZone attackZones, CombatZone targetZone)
        {
            return (attackZones & targetZone) != 0;
        }

        /// <summary>
        /// Цвет зоны для удобной визуализации в Scene и Game view.
        /// </summary>
        public static Color GetZoneColor(this CombatZone zone, float alpha = 0.5f)
        {
            if (zone.HasFlag(CombatZone.High) && zone.HasFlag(CombatZone.Mid))
                return new Color(1f, 0.45f, 0.1f, alpha); // Оранжево-красный (High+Mid)

            if (zone.HasFlag(CombatZone.High))
                return new Color(1f, 0.2f, 0.25f, alpha); // Красный (High)

            if (zone.HasFlag(CombatZone.Mid))
                return new Color(1f, 0.85f, 0.15f, alpha); // Золотисто-желтый (Mid)

            if (zone.HasFlag(CombatZone.Low))
                return new Color(0.15f, 0.85f, 1f, alpha); // Голубой (Low)

            return new Color(1f, 1f, 1f, alpha);
        }
    }
}
