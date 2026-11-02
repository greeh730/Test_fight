using System;
using UnityEngine;

namespace Combat.ProceduralGeneration
{
    /// <summary>
    /// Процедурный генератор черт врага по 4 векторам:
    /// - Генерирует черты (0..100) по 4 категориям
    /// - Вычисляет класс (Страйкер, Танк, Лидер, Контроллер) и архетип
    /// - Настраивает модификаторы силы, здоровья, скорости и силуэта
    /// </summary>
    public static class EnemyVectorGenerator
    {
        /// <summary>
        /// Генерирует процедурный профиль с возможностью задать предпочтительную роль для балансировки группы
        /// </summary>
        public static EnemyPersonalityProfile GenerateProfile(CombatClass? preferredClass = null)
        {
            var profile = new EnemyPersonalityProfile();

            if (preferredClass.HasValue)
            {
                ApplyBiasedTraits(profile, preferredClass.Value);
            }
            else
            {
                // Полностью случайная генерация черт (0-100)
                profile.acrobatics = UnityEngine.Random.Range(15, 95);
                profile.mobility = UnityEngine.Random.Range(15, 95);
                profile.patience = UnityEngine.Random.Range(15, 95);
                profile.anticipation = UnityEngine.Random.Range(15, 95);
                profile.empathy = UnityEngine.Random.Range(15, 95);
                profile.subordination = UnityEngine.Random.Range(15, 95);
                profile.selfPreservation = UnityEngine.Random.Range(15, 95);
                profile.parrySkill = UnityEngine.Random.Range(15, 95);
            }

            profile.CalculateVectorScores();
            ResolveClassAndArchetype(profile, preferredClass);
            ApplyClassStatsModifiers(profile);

            return profile;
        }

        private static void ApplyBiasedTraits(EnemyPersonalityProfile p, CombatClass role)
        {
            switch (role)
            {
                case CombatClass.Striker:
                    // Высокий Мир (мобильность, акробатика) и среднее самосохранение
                    p.acrobatics = UnityEngine.Random.Range(70, 100);
                    p.mobility = UnityEngine.Random.Range(75, 100);
                    p.patience = UnityEngine.Random.Range(20, 50);
                    p.anticipation = UnityEngine.Random.Range(50, 85);
                    p.empathy = UnityEngine.Random.Range(10, 40);
                    p.subordination = UnityEngine.Random.Range(20, 50);
                    p.selfPreservation = UnityEngine.Random.Range(60, 90);
                    p.parrySkill = UnityEngine.Random.Range(20, 50);
                    break;

                case CombatClass.Tank:
                    // Высокие Свои (эмпатия/прикрытие) и Я (парирование/стойкость)
                    p.acrobatics = UnityEngine.Random.Range(10, 40);
                    p.mobility = UnityEngine.Random.Range(20, 50);
                    p.patience = UnityEngine.Random.Range(65, 95);
                    p.anticipation = UnityEngine.Random.Range(40, 75);
                    p.empathy = UnityEngine.Random.Range(75, 100);
                    p.subordination = UnityEngine.Random.Range(70, 95);
                    p.selfPreservation = UnityEngine.Random.Range(20, 45); // Готов жертвовать собой
                    p.parrySkill = UnityEngine.Random.Range(65, 95);
                    break;

                case CombatClass.Leader:
                    // Высокие Свои (эмпатия/поддержка) и Я (самосохранение в тылу)
                    p.acrobatics = UnityEngine.Random.Range(40, 70);
                    p.mobility = UnityEngine.Random.Range(55, 80);
                    p.patience = UnityEngine.Random.Range(50, 80);
                    p.anticipation = UnityEngine.Random.Range(60, 85);
                    p.empathy = UnityEngine.Random.Range(85, 100);
                    p.subordination = UnityEngine.Random.Range(80, 100);
                    p.selfPreservation = UnityEngine.Random.Range(75, 100); // Держится сзади
                    p.parrySkill = UnityEngine.Random.Range(20, 50);
                    break;

                case CombatClass.Controller:
                    // Высокий Мир (рельеф/ловушки) и Игрок (терпение/упреждение)
                    p.acrobatics = UnityEngine.Random.Range(50, 80);
                    p.mobility = UnityEngine.Random.Range(40, 70);
                    p.patience = UnityEngine.Random.Range(75, 100);
                    p.anticipation = UnityEngine.Random.Range(80, 100);
                    p.empathy = UnityEngine.Random.Range(30, 60);
                    p.subordination = UnityEngine.Random.Range(40, 70);
                    p.selfPreservation = UnityEngine.Random.Range(50, 75);
                    p.parrySkill = UnityEngine.Random.Range(40, 70);
                    break;
            }
        }

        private static void ResolveClassAndArchetype(EnemyPersonalityProfile p, CombatClass? forcedClass)
        {
            if (forcedClass.HasValue)
            {
                p.combatClass = forcedClass.Value;
            }
            else
            {
                // Вычисление класса на основе доминирующих векторов
                float strikerScore = (p.worldScore * 0.6f) + (p.selfScore * 0.4f) + (p.mobility * 0.2f);
                float tankScore = (p.alliesScore * 0.45f) + (p.parrySkill * 0.45f) + (100 - p.selfPreservation) * 0.25f;
                float leaderScore = (p.empathy * 0.65f) + (p.selfPreservation * 0.35f);
                float controllerScore = (p.playerScore * 0.55f) + (p.worldScore * 0.35f) + (p.patience * 0.2f);

                if (tankScore >= strikerScore && tankScore >= leaderScore && tankScore >= controllerScore)
                {
                    p.combatClass = CombatClass.Tank;
                }
                else if (strikerScore >= leaderScore && strikerScore >= controllerScore)
                {
                    p.combatClass = CombatClass.Striker;
                }
                else if (leaderScore >= controllerScore)
                {
                    p.combatClass = CombatClass.Leader;
                }
                else
                {
                    p.combatClass = CombatClass.Controller;
                }
            }

            // Назначение архетипа внутри класса
            switch (p.combatClass)
            {
                case CombatClass.Striker:
                    p.archetype = CombatArchetype.Assassin;
                    p.roleTitle = "Страйкер (Убийца)";
                    break;

                case CombatClass.Tank:
                    p.archetype = CombatArchetype.Bodyguard;
                    p.roleTitle = "Танк (Телохранитель)";
                    break;

                case CombatClass.Leader:
                    p.archetype = CombatArchetype.Medic;
                    p.roleTitle = "Лидер (Медик)";
                    break;

                case CombatClass.Controller:
                    p.archetype = CombatArchetype.Trapper;
                    p.roleTitle = "Контроллер (Траппер)";
                    break;
            }
        }

        private static void ApplyClassStatsModifiers(EnemyPersonalityProfile p)
        {
            switch (p.combatClass)
            {
                case CombatClass.Striker:
                    p.healthMultiplier = 0.75f;             // Хрупкий (стеклопушка)
                    p.speedMultiplier = 1.25f;              // Высокая скорость передвижения
                    p.damageMultiplier = 1.35f;             // Сокрушительный урон со спины
                    p.attackIntervalMultiplier = 0.80f;     // Частые удары
                    p.scaleMultiplier = 0.92f;              // Поджарый, быстрый силуэт
                    p.fleeHealthThreshold = 0.30f;          // Отступает при опасности
                    p.classSilhouetteColor = new Color(1.0f, 0.35f, 0.25f, 1f); // Красно-оранжевый
                    break;

                case CombatClass.Tank:
                    p.healthMultiplier = 1.70f;             // Огромный запас HP
                    p.speedMultiplier = 0.85f;              // Медленный, монументальный
                    p.damageMultiplier = 0.90f;             // Базовый умеренный урон
                    p.attackIntervalMultiplier = 1.15f;     // Тяжелые замахи
                    p.scaleMultiplier = 1.18f;              // Крупный, массивный силуэт
                    p.fleeHealthThreshold = 0.05f;          // Стоит до конца, прикрывая своих
                    p.classSilhouetteColor = new Color(0.35f, 0.65f, 1.0f, 1f); // Сине-стальной
                    break;

                case CombatClass.Leader:
                    p.healthMultiplier = 0.90f;             // Среднее здоровье
                    p.speedMultiplier = 1.05f;              // Подвижный в тылу
                    p.damageMultiplier = 0.70f;             // Слабый собственный урон
                    p.attackIntervalMultiplier = 1.30f;
                    p.scaleMultiplier = 0.95f;
                    p.fleeHealthThreshold = 0.45f;          // Осторожен, держит тыл
                    p.classSilhouetteColor = new Color(0.30f, 1.0f, 0.50f, 1f); // Изумрудно-зеленый
                    break;

                case CombatClass.Controller:
                    p.healthMultiplier = 1.05f;             // Стандартное здоровье
                    p.speedMultiplier = 0.95f;
                    p.damageMultiplier = 0.95f;
                    p.attackIntervalMultiplier = 1.0f;
                    p.scaleMultiplier = 1.02f;
                    p.fleeHealthThreshold = 0.25f;
                    p.classSilhouetteColor = new Color(0.85f, 0.45f, 1.0f, 1f); // Фиолетовый
                    break;
            }
        }
    }
}
