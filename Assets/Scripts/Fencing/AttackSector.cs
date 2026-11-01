// Assets/Scripts/Fencing/AttackSector.cs
using System;
using UnityEngine;

namespace Fencing
{
    /// <summary>
    /// Тип углового сектора колеса.
    /// </summary>
    public enum SectorType
    {
        Attack,
        Dead,
        Free
    }

    /// <summary>
    /// Описание сектора колеса фехтования: направление, центр, ширина угла и назначение.
    /// </summary>
    [Serializable]
    public struct AttackSector
    {
        [Tooltip("Идентификатор направления удара")]
        public AttackDirection direction;

        [Tooltip("Центральный угол сектора в градусах [-180..180]")]
        [Range(-180f, 180f)]
        public float angleCenter;

        [Tooltip("Ширина сектора в градусах")]
        [Range(1f, 180f)]
        public float angleWidth;

        [Tooltip("Тип сектора: атакующий, мертвая зона или свободная зона")]
        public SectorType type;

        public AttackSector(AttackDirection direction, float angleCenter, float angleWidth, SectorType type)
        {
            this.direction = direction;
            this.angleCenter = angleCenter;
            this.angleWidth = angleWidth;
            this.type = type;
        }

        /// <summary>
        /// Проверяет, попадает ли угол в градусах внутрь данного сектора.
        /// Использует Mathf.DeltaAngle для корректной обработки перехода через +/-180°.
        /// </summary>
        public bool ContainsAngle(float angle)
        {
            if (angleWidth <= 0.001f) return false;
            float delta = Mathf.Abs(Mathf.DeltaAngle(angleCenter, angle));
            return delta <= (angleWidth * 0.5f);
        }
    }
}
