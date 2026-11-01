// Assets/Scripts/Magic/IMagicSpell.cs
using System.Collections.Generic;
using UnityEngine;
using Fencing;

namespace Magic
{
    /// <summary>
    /// Универсальный интерфейс заклинания магии.
    /// Позволяет подключать любые заклинания (Огонь, Лёд, Молния и др.)
    /// со своими уникальными цветами обводки, стрелок, иконками и поведением.
    /// </summary>
    public interface IMagicSpell
    {
        string SpellName { get; }
        Sprite SpellIcon { get; }
        Color BorderColor { get; }
        Color ArrowColor { get; }
        Color FillColor { get; }
        Color SuccessColor { get; }
        Color FailureColor { get; }
        bool ThemeWheelArrows { get; }

        IReadOnlyList<AttackDirection> CurrentSequence { get; }
        int CurrentInputIndex { get; }
        float TimeRemaining { get; }
        float RegenerationInterval { get; }
        bool IsLockedOut { get; }
        float LockoutTimeRemaining { get; }

        void RegenerateSpell();
        void ApplySpellTheme(FencingWheelUI wheelUI);
    }
}
