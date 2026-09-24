using System;
using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// База данных всех приёмов и атак игрока.
    /// Позволяет визуализировать и настраивать геометрию хитбоксов, тайминги, урон, отталкивание и зоны в Inspector и Scene View.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatAttackDatabase", menuName = "Combat/Combat Attack Database")]
    public class CombatAttackDatabase : ScriptableObject
    {
        [Serializable]
        public class AttackEntry
        {
            [Tooltip("Уникальный строковый идентификатор приёма")]
            public string sequenceId;

            [Tooltip("Отображаемое название приёма")]
            public string displayName;

            [Tooltip("Категория приёма")]
            public string category;

            [Tooltip("Цепочка стрелок (глифов)")]
            public string glyphPattern;

            [Tooltip("Подсказка по направлению (например: 'Вправо: ↙ ↓ ↘ → ↗')")]
            public string directionNote;

            [Tooltip("Подробное описание механики приёма")]
            [TextArea(2, 4)]
            public string description;

            [Tooltip("Полная конфигурация геометрии хитбокса, урона, отталкивания и таймингов")]
            public AttackConfig attackConfig;

            [Tooltip("Расход выносливости игрока на исполнение")]
            public float staminaCost = 14f;

            [Tooltip("Подбрасывает ли приём врага в воздух (Launcher)")]
            public bool isLauncher = false;

            [Tooltip("Сила импульса выпада/рывка вперед при ударе")]
            public float lungeForce = 4.0f;

            public AttackEntry Clone()
            {
                return new AttackEntry
                {
                    sequenceId = this.sequenceId,
                    displayName = this.displayName,
                    category = this.category,
                    glyphPattern = this.glyphPattern,
                    directionNote = this.directionNote,
                    description = this.description,
                    attackConfig = this.attackConfig != null ? this.attackConfig.Clone() : new AttackConfig(),
                    staminaCost = this.staminaCost,
                    isLauncher = this.isLauncher,
                    lungeForce = this.lungeForce
                };
            }
        }

        [Header("--- Список атак и хитбоксов ---")]
        [SerializeField] private List<AttackEntry> attacks = new List<AttackEntry>();

        public List<AttackEntry> Attacks => attacks;

        public AttackEntry GetEntry(string id)
        {
            if (attacks == null) return null;
            return attacks.Find(a => a.sequenceId == id);
        }

        public void ResetToDefaults()
        {
            attacks = CreateDefaultEntries();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        public void ResetEntry(string id)
        {
            var defaultEntries = CreateDefaultEntries();
            var def = defaultEntries.Find(a => a.sequenceId == id);
            if (def == null) return;

            var existing = GetEntry(id);
            if (existing != null)
            {
                existing.displayName = def.displayName;
                existing.category = def.category;
                existing.glyphPattern = def.glyphPattern;
                existing.directionNote = def.directionNote;
                existing.description = def.description;
                existing.attackConfig = def.attackConfig.Clone();
                existing.staminaCost = def.staminaCost;
                existing.isLauncher = def.isLauncher;
                existing.lungeForce = def.lungeForce;
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            }
        }

        public static List<AttackEntry> CreateDefaultEntries()
        {
            var list = new List<AttackEntry>();

            // 1. ВОСХОДЯЩИЙ ВИХРЬ ⚡ (Супер-финишер) - Вправо: ↙ ↓ ↘ → ↗
            list.Add(new AttackEntry
            {
                sequenceId = "finisher_launcher",
                displayName = "ВОСХОДЯЩИЙ ВИХРЬ ⚡ (Вправо)",
                category = "Супер-финишер",
                glyphPattern = "⬋⬇⬊⮕⬈",
                directionNote = "Вправо: ↙ ↓ ↘ → ↗",
                description = "Зажав ЛКМ, провести полукруг снизу вверх в сторону удара. Урон 38 • Зоны: Мид + Верх • Подбрасывает в воздух (Launcher)",
                attackConfig = new AttackConfig(
                    "Небесный апперкот вправо",
                    CombatZone.Mid | CombatZone.High,
                    new Vector2(1.1f, 0.6f),
                    new Vector2(1.6f, 1.8f),
                    0.08f, 0.20f, 0.24f,
                    38f,
                    new Vector2(3.0f, 12.0f),
                    new Color(0.95f, 0.2f, 0.95f, 0.85f),
                    launcher: true,
                    stale: false,
                    lunge: 5.5f
                ),
                staminaCost = 24f,
                isLauncher = true,
                lungeForce = 5.5f
            });

            // 1b. ВОСХОДЯЩИЙ ВИХРЬ ⚡ (Супер-финишер) - Влево: ↘ ↓ ↙ ← ↖
            list.Add(new AttackEntry
            {
                sequenceId = "finisher_launcher_left",
                displayName = "ВОСХОДЯЩИЙ ВИХРЬ ⚡ (Влево)",
                category = "Супер-финишер",
                glyphPattern = "⬊⬇⬋⬅⬉",
                directionNote = "Влево: ↘ ↓ ↙ ← ↖",
                description = "Зажав ЛКМ, провести полукруг снизу вверх в сторону удара. Урон 38 • Зоны: Мид + Верх • Подбрасывает в воздух (Launcher)",
                attackConfig = new AttackConfig(
                    "Небесный апперкот влево",
                    CombatZone.Mid | CombatZone.High,
                    new Vector2(1.1f, 0.6f),
                    new Vector2(1.6f, 1.8f),
                    0.08f, 0.20f, 0.24f,
                    38f,
                    new Vector2(3.0f, 12.0f),
                    new Color(0.95f, 0.2f, 0.95f, 0.85f),
                    launcher: true,
                    stale: false,
                    lunge: 5.5f
                ),
                staminaCost = 24f,
                isLauncher = true,
                lungeForce = 5.5f
            });

            // 2. ПРОНЗАЮЩИЙ ШТОРМ 💥 (Силовой выпад) - Вправо: ↘ → ↗
            list.Add(new AttackEntry
            {
                sequenceId = "strike_piercing_thrust",
                displayName = "ПРОНЗАЮЩИЙ ШТОРМ 💥 (Вправо)",
                category = "Силовой выпад",
                glyphPattern = "⬊⮕⬈",
                directionNote = "Вправо: ↘ → ↗",
                description = "Свайп снизу вперед и вверх. Урон 32 • Стремительный рывок вперед (Выпад: 6.8)",
                attackConfig = new AttackConfig(
                    "Силовой выпад вправо",
                    CombatZone.Mid,
                    new Vector2(1.5f, 0.0f),
                    new Vector2(1.8f, 0.9f),
                    0.07f, 0.18f, 0.22f,
                    32f,
                    new Vector2(8.5f, 2.0f),
                    new Color(1f, 0.45f, 0.05f, 0.9f),
                    launcher: false,
                    stale: false,
                    lunge: 6.8f
                ),
                staminaCost = 20f,
                isLauncher = false,
                lungeForce = 6.8f
            });

            // 2b. ПРОНЗАЮЩИЙ ШТОРМ 💥 (Силовой выпад) - Влево: ↙ ← ↖
            list.Add(new AttackEntry
            {
                sequenceId = "strike_piercing_thrust_left",
                displayName = "ПРОНЗАЮЩИЙ ШТОРМ 💥 (Влево)",
                category = "Силовой выпад",
                glyphPattern = "⬋⬅⬉",
                directionNote = "Влево: ↙ ← ↖",
                description = "Свайп снизу вперед и вверх. Урон 32 • Стремительный рывок вперед (Выпад: 6.8)",
                attackConfig = new AttackConfig(
                    "Силовой выпад влево",
                    CombatZone.Mid,
                    new Vector2(1.5f, 0.0f),
                    new Vector2(1.8f, 0.9f),
                    0.07f, 0.18f, 0.22f,
                    32f,
                    new Vector2(8.5f, 2.0f),
                    new Color(1f, 0.45f, 0.05f, 0.9f),
                    launcher: false,
                    stale: false,
                    lunge: 6.8f
                ),
                staminaCost = 20f,
                isLauncher = false,
                lungeForce = 6.8f
            });

            // 3. ВЕРХНИЕ И ДИАГОНАЛЬНЫЕ - Верхний срез ▲: ↓ ↑
            list.Add(new AttackEntry
            {
                sequenceId = "strike_high_slash",
                displayName = "Верхний срез ▲",
                category = "Верхние и диагональные",
                glyphPattern = "⬇⬆",
                directionNote = "Верхний срез ▲: ↓ ↑",
                description = "Рубящие атаки снизу вверх. Урон 26 • Зоны: Верх + Мид • Сбивают атаки в прыжке",
                attackConfig = new AttackConfig(
                    "Верхний срез",
                    CombatZone.Mid | CombatZone.High,
                    new Vector2(1.0f, 0.5f),
                    new Vector2(1.3f, 1.4f),
                    0.06f, 0.16f, 0.20f,
                    26f,
                    new Vector2(3.5f, 7.5f),
                    new Color(1f, 0.25f, 0.15f, 0.85f),
                    launcher: false,
                    stale: false,
                    lunge: 2.8f
                ),
                staminaCost = 15f,
                isLauncher = false,
                lungeForce = 2.8f
            });

            // 3b. ВЕРХНИЕ И ДИАГОНАЛЬНЫЕ - Срез ↗ (вправо): ↙ ↗
            list.Add(new AttackEntry
            {
                sequenceId = "strike_high_slash_diag",
                displayName = "Срез ↗ (вправо)",
                category = "Верхние и диагональные",
                glyphPattern = "⬋⬈",
                directionNote = "Срез ↗ (вправо): ↙ ↗",
                description = "Рубящие атаки снизу вверх. Урон 26 • Зоны: Верх + Мид • Сбивают атаки в прыжке",
                attackConfig = new AttackConfig(
                    "Верхний диагональный вправо",
                    CombatZone.Mid | CombatZone.High,
                    new Vector2(1.0f, 0.5f),
                    new Vector2(1.3f, 1.4f),
                    0.06f, 0.16f, 0.20f,
                    26f,
                    new Vector2(4.0f, 7.5f),
                    new Color(1f, 0.25f, 0.15f, 0.85f),
                    launcher: false,
                    stale: false,
                    lunge: 3.2f
                ),
                staminaCost = 15f,
                isLauncher = false,
                lungeForce = 3.2f
            });

            // 3c. ВЕРХНИЕ И ДИАГОНАЛЬНЫЕ - Срез ↖ (влево): ↘ ↖
            list.Add(new AttackEntry
            {
                sequenceId = "strike_high_slash_diag_left",
                displayName = "Срез ↖ (влево)",
                category = "Верхние и диагональные",
                glyphPattern = "⬊⬉",
                directionNote = "Срез ↖ (влево): ↘ ↖",
                description = "Рубящие атаки снизу вверх. Урон 26 • Зоны: Верх + Мид • Сбивают атаки в прыжке",
                attackConfig = new AttackConfig(
                    "Верхний диагональный влево",
                    CombatZone.Mid | CombatZone.High,
                    new Vector2(1.0f, 0.5f),
                    new Vector2(1.3f, 1.4f),
                    0.06f, 0.16f, 0.20f,
                    26f,
                    new Vector2(4.0f, 7.5f),
                    new Color(1f, 0.25f, 0.15f, 0.85f),
                    launcher: false,
                    stale: false,
                    lunge: 3.2f
                ),
                staminaCost = 15f,
                isLauncher = false,
                lungeForce = 3.2f
            });

            // 4. ВЫПАДЫ КЛИНКОМ - Вправо ▶: ← →
            list.Add(new AttackEntry
            {
                sequenceId = "strike_mid_forward",
                displayName = "Выпад клинком ▶ (Вправо)",
                category = "Выпады клинком",
                glyphPattern = "⬅⮕",
                directionNote = "Вправо ▶: ← →",
                description = "Оттяжка назад с быстрым выбросом клинка вперед. Урон 22 • Зона: Средняя • Выпад: 4.2",
                attackConfig = new AttackConfig(
                    "Средний выпад вправо",
                    CombatZone.Mid,
                    new Vector2(1.2f, 0.0f),
                    new Vector2(1.4f, 0.85f),
                    0.05f, 0.14f, 0.16f,
                    22f,
                    new Vector2(6.0f, 1.5f),
                    new Color(1f, 0.85f, 0.15f, 0.85f),
                    launcher: false,
                    stale: false,
                    lunge: 4.2f
                ),
                staminaCost = 12f,
                isLauncher = false,
                lungeForce = 4.2f
            });

            // 4b. ВЫПАДЫ КЛИНКОМ - Влево ◀: → ←
            list.Add(new AttackEntry
            {
                sequenceId = "strike_turnaround",
                displayName = "Выпад клинком ◀ (Влево)",
                category = "Выпады клинком",
                glyphPattern = "⮕⬅",
                directionNote = "Влево ◀: → ←",
                description = "Оттяжка назад с быстрым выбросом клинка вперед. Урон 22 • Зона: Средняя • Выпад: 4.2",
                attackConfig = new AttackConfig(
                    "Выпад влево",
                    CombatZone.Mid,
                    new Vector2(1.2f, 0.0f),
                    new Vector2(1.4f, 0.85f),
                    0.05f, 0.14f, 0.16f,
                    22f,
                    new Vector2(6.0f, 1.5f),
                    new Color(1f, 0.85f, 0.15f, 0.85f),
                    launcher: false,
                    stale: false,
                    lunge: 4.2f
                ),
                staminaCost = 12f,
                isLauncher = false,
                lungeForce = 4.2f
            });

            // 5. НИЖНИЕ ПОДСЕЧКИ И СРЕЗЫ - Подсечка ▼: ↑ ↓
            list.Add(new AttackEntry
            {
                sequenceId = "strike_low_sweep",
                displayName = "Подсечка ▼",
                category = "Нижние подсечки и срезы",
                glyphPattern = "⬆⬇",
                directionNote = "Подсечка ▼: ↑ ↓",
                description = "Удары сверху вниз по нижней зоне. Урон 20 • Зона: Нижняя • Защита от нижних ударов",
                attackConfig = new AttackConfig(
                    "Нижняя подсечка",
                    CombatZone.Low,
                    new Vector2(1.1f, -0.45f),
                    new Vector2(1.5f, 0.65f),
                    0.05f, 0.14f, 0.18f,
                    20f,
                    new Vector2(5.5f, 0.8f),
                    new Color(0.15f, 0.85f, 1f, 0.85f),
                    launcher: false,
                    stale: false,
                    lunge: 3.5f
                ),
                staminaCost = 12f,
                isLauncher = false,
                lungeForce = 3.5f
            });

            // 5b. НИЖНИЕ ПОДСЕЧКИ И СРЕЗЫ - Низкий срез ↘ (вправо): ↖ ↘
            list.Add(new AttackEntry
            {
                sequenceId = "strike_low_sweep_diag",
                displayName = "Низкий срез ↘ (вправо)",
                category = "Нижние подсечки и срезы",
                glyphPattern = "⬉⬊",
                directionNote = "Низкий срез ↘ (вправо): ↖ ↘",
                description = "Удары сверху вниз по нижней зоне. Урон 20 • Зона: Нижняя • Защита от нижних ударов",
                attackConfig = new AttackConfig(
                    "Низкий срез вправо",
                    CombatZone.Low,
                    new Vector2(1.1f, -0.45f),
                    new Vector2(1.5f, 0.65f),
                    0.05f, 0.14f, 0.18f,
                    20f,
                    new Vector2(5.5f, 0.8f),
                    new Color(0.15f, 0.85f, 1f, 0.85f),
                    launcher: false,
                    stale: false,
                    lunge: 3.5f
                ),
                staminaCost = 12f,
                isLauncher = false,
                lungeForce = 3.5f
            });

            // 5c. НИЖНИЕ ПОДСЕЧКИ И СРЕЗЫ - Низкий срез ↙ (влево): ↗ ↙
            list.Add(new AttackEntry
            {
                sequenceId = "strike_low_sweep_diag_left",
                displayName = "Низкий срез ↙ (влево)",
                category = "Нижние подсечки и срезы",
                glyphPattern = "⬈⬋",
                directionNote = "Низкий срез ↙ (влево): ↗ ↙",
                description = "Удары сверху вниз по нижней зоне. Урон 20 • Зона: Нижняя • Защита от нижних ударов",
                attackConfig = new AttackConfig(
                    "Низкий срез влево",
                    CombatZone.Low,
                    new Vector2(1.1f, -0.45f),
                    new Vector2(1.5f, 0.65f),
                    0.05f, 0.14f, 0.18f,
                    20f,
                    new Vector2(5.5f, 0.8f),
                    new Color(0.15f, 0.85f, 1f, 0.85f),
                    launcher: false,
                    stale: false,
                    lunge: 3.5f
                ),
                staminaCost = 12f,
                isLauncher = false,
                lungeForce = 3.5f
            });

            return list;
        }
    }
}
