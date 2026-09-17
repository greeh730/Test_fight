using System;
using UnityEngine;

namespace Combat
{
    public enum AttackHeight
    {
        High = 0,
        Mid = 1,
        Low = 2
    }

    public enum StrikeDirection
    {
        Forward = 0,
        Backward = 1
    }

    public enum AttackDirection
    {
        Right = 0,
        Up = 1,
        Down = 2,
        Left = 3
    }

    /// <summary>
    /// Полная конфигурация атаки и её хитбокса.
    /// Все параметры доступны для редактирования в Inspector.
    /// </summary>
    [Serializable]
    public class AttackConfig
    {
        [Header("--- Информация об ударе ---")]
        [Tooltip("Отображаемое название приема")]
        public string attackName = "Атака";

        [Tooltip("Боевые зоны, по которым наносится урон")]
        public CombatZone targetedZones = CombatZone.Mid;

        [Header("--- Хитбокс (Геометрия) ---")]
        [Tooltip("Смещение центра хитбокса относительно центра персонажа (X автоматически зеркалится при повороте влево)")]
        public Vector2 hitboxOffset = new Vector2(1.1f, 0f);

        [Tooltip("Размеры коробки хитбокса (Ширина, Высота)")]
        public Vector2 hitboxSize = new Vector2(1.4f, 0.9f);

        [Header("--- Тайминги (в секундах) ---")]
        [Tooltip("Время замаха (до появления активного хитбокса)")]
        [Range(0.01f, 1.0f)]
        public float startupTime = 0.08f;

        [Tooltip("Время активности хитбокса (когда удар наносит урон)")]
        [Range(0.02f, 1.0f)]
        public float activeTime = 0.16f;

        [Tooltip("Время восстановления после удара (задержка перед следующим действием)")]
        [Range(0.01f, 1.0f)]
        public float recoveryTime = 0.18f;

        [Header("--- Урон и физика ---")]
        [Tooltip("Базовый урон")]
        public float damage = 20f;

        [Tooltip("Сила и вектор отталкивания цели (X зеркалится по направлению удара)")]
        public Vector2 knockbackForce = new Vector2(6.5f, 2.0f);

        [Header("--- Визуализация ---")]
        [Tooltip("Цвет отображения хитбокса в Scene View и Game View")]
        public Color hitboxColor = new Color(1f, 0.2f, 0.3f, 0.7f);

        public AttackConfig() { }

        public AttackConfig(string name, CombatZone zones, Vector2 offset, Vector2 size, float startup, float active, float recovery, float dmg, Vector2 kb, Color col)
        {
            attackName = name;
            targetedZones = zones;
            hitboxOffset = offset;
            hitboxSize = size;
            startupTime = startup;
            activeTime = active;
            recoveryTime = recovery;
            damage = dmg;
            knockbackForce = kb;
            hitboxColor = col;
        }
    }
}
