using System;
using UnityEngine;

namespace Combat.ProceduralGeneration
{
    /// <summary>
    /// Профиль личности и боевого класса врага, сгенерированный на основе 4 векторов.
    /// Передаёт модификаторы и параметры поведения в контроллер ИИ и визуальный силуэт.
    /// </summary>
    [Serializable]
    public class EnemyPersonalityProfile
    {
        [Header("--- Класс и Архетип ---")]
        public CombatClass combatClass;
        public CombatArchetype archetype;
        public string roleTitle = "";

        [Header("--- Вектор 1: Мир (World - W) ---")]
        [Tooltip("Акробатика: склонность прыгать по платформам и занимать высоту (0-100)")]
        [Range(0, 100)] public int acrobatics = 50;
        [Tooltip("Подвижность: динамичность перемещения влево-вправо и кайтинга (0-100)")]
        [Range(0, 100)] public int mobility = 50;

        [Header("--- Вектор 2: Игрок (Player - P) ---")]
        [Tooltip("Терпеливость: выдержка перед нанесением удара (0-100)")]
        [Range(0, 100)] public int patience = 50;
        [Tooltip("Упреждение: расчёт траектории и таймингов игрока (0-100)")]
        [Range(0, 100)] public int anticipation = 50;

        [Header("--- Вектор 3: Свои (Allies - N) ---")]
        [Tooltip("Эмпатия: готовность прикрывать и лечить союзников (0-100)")]
        [Range(0, 100)] public int empathy = 50;
        [Tooltip("Субординация: удержание строя и командный фокус (0-100)")]
        [Range(0, 100)] public int subordination = 50;

        [Header("--- Вектор 4: Я (Self - S) ---")]
        [Tooltip("Самосохранение: отступление в тыл при низком здоровье (0-100)")]
        [Range(0, 100)] public int selfPreservation = 50;
        [Tooltip("Мастерство парирования: частота и точность блоков (0-100)")]
        [Range(0, 100)] public int parrySkill = 50;

        [Header("--- Итоговые веса векторов (0-100) ---")]
        public float worldScore;
        public float playerScore;
        public float alliesScore;
        public float selfScore;

        [Header("--- Геймплейные модификаторы характеристик ---")]
        public float healthMultiplier = 1.0f;
        public float speedMultiplier = 1.0f;
        public float damageMultiplier = 1.0f;
        public float attackIntervalMultiplier = 1.0f;
        public float scaleMultiplier = 1.0f;
        public float fleeHealthThreshold = 0.25f; // Отступать, если HP ниже этого процента
        public Color classSilhouetteColor = Color.white;

        /// <summary>
        /// Пересчёт итоговых Vector Scores на основе атомарных черт
        /// </summary>
        public void CalculateVectorScores()
        {
            worldScore = (acrobatics + mobility) * 0.5f;
            playerScore = (patience + anticipation) * 0.5f;
            alliesScore = (empathy + subordination) * 0.5f;
            selfScore = (selfPreservation + parrySkill) * 0.5f;
        }
    }
}
