using System;
using System.Collections.Generic;
using UnityEngine;
using Combat;
using LevelGeneration;

namespace Combat.ProceduralGeneration
{
    /// <summary>
    /// Директор боевой арены (Arena Director):
    /// - Отслеживает состав противников в пределах одной боевой комнаты
    /// - Балансирует классы при процедурном спавне (гарантирует синергию групп)
    /// - Координирует союзников в реальном времени (защита Медиков Танками, командный фокус)
    /// </summary>
    [DisallowMultipleComponent]
    public class ArenaDirector2D : MonoBehaviour
    {
        private static ArenaDirector2D _instance;
        public static ArenaDirector2D Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<ArenaDirector2D>();
                    if (_instance == null)
                    {
                        var go = new GameObject("[SYSTEM] ArenaDirector2D");
                        _instance = go.AddComponent<ArenaDirector2D>();
                    }
                }
                return _instance;
            }
        }

        private readonly List<EnemyAIController2D> _registeredEnemies = new List<EnemyAIController2D>();
        private float _medicHealCooldown = 0f;

        private void Awake()
        {
            if (_instance == null) _instance = this;
            else if (_instance != this) { Destroy(gameObject); return; }
        }

        public void RegisterEnemy(EnemyAIController2D enemy)
        {
            if (enemy != null && !_registeredEnemies.Contains(enemy))
            {
                _registeredEnemies.Add(enemy);
            }
        }

        public void UnregisterEnemy(EnemyAIController2D enemy)
        {
            _registeredEnemies.Remove(enemy);
        }

        public void ClearEnemies()
        {
            _registeredEnemies.Clear();
        }

        /// <summary>
        /// Балансировка и назначение классов для группы спавнящихся врагов
        /// </summary>
        public void AssignRolesToSpawnedEnemies(List<GameObject> enemyObjects)
        {
            if (enemyObjects == null || enemyObjects.Count == 0) return;

            ClearEnemies();

            int count = enemyObjects.Count;
            var assignedClasses = new List<CombatClass>();

            if (count == 1)
            {
                // Одиночный враг: 50% Танк, 50% Страйкер
                assignedClasses.Add(UnityEngine.Random.value > 0.5f ? CombatClass.Striker : CombatClass.Tank);
            }
            else if (count == 2)
            {
                // Пара врагов: Танк + Страйкер ИЛИ Танк + Лидер (Медик)
                assignedClasses.Add(CombatClass.Tank);
                assignedClasses.Add(UnityEngine.Random.value > 0.45f ? CombatClass.Striker : CombatClass.Leader);
            }
            else
            {
                // 3+ врагов: классическая группа (Танк + Страйкер + Лидер/Контроллер)
                assignedClasses.Add(CombatClass.Tank);
                assignedClasses.Add(CombatClass.Striker);
                assignedClasses.Add(CombatClass.Leader);
                for (int i = 3; i < count; i++)
                {
                    assignedClasses.Add(UnityEngine.Random.value > 0.5f ? CombatClass.Controller : CombatClass.Striker);
                }
            }

            // Перемешиваем распределение
            for (int i = 0; i < assignedClasses.Count; i++)
            {
                int rnd = UnityEngine.Random.Range(i, assignedClasses.Count);
                var temp = assignedClasses[i];
                assignedClasses[i] = assignedClasses[rnd];
                assignedClasses[rnd] = temp;
            }

            for (int i = 0; i < enemyObjects.Count; i++)
            {
                var obj = enemyObjects[i];
                if (obj == null) continue;

                var ai = obj.GetComponent<EnemyAIController2D>();
                if (ai == null) continue;

                RegisterEnemy(ai);

                // Генерируем профиль черт под распределенный класс
                CombatClass role = (i < assignedClasses.Count) ? assignedClasses[i] : CombatClass.Striker;
                var profile = EnemyVectorGenerator.GenerateProfile(role);

                // Применяем профиль на врага
                ai.ApplyPersonalityProfile(profile);

                // Добавляем визуальный силуэт и значок над головой
                var silhouette = obj.GetComponent<EnemyVisualSilhouette>();
                if (silhouette == null) silhouette = obj.AddComponent<EnemyVisualSilhouette>();
                silhouette.ApplyProfile(profile);

                Debug.Log($"<color=#FFAA00><b>[Director]</b></color> Враг {obj.name} получил класс: <b>{profile.roleTitle}</b> (HP x{profile.healthMultiplier:F2}, Spd x{profile.speedMultiplier:F2})");
            }
        }

        /// <summary>
        /// Поиск самого уязвимого союзника для прикрытия Танком
        /// </summary>
        public EnemyAIController2D GetVulnerableAllyToProtect(EnemyAIController2D tank)
        {
            EnemyAIController2D target = null;
            float lowestHp = float.MaxValue;

            for (int i = 0; i < _registeredEnemies.Count; i++)
            {
                var ally = _registeredEnemies[i];
                if (ally == null || ally == tank || ally.IsDead || !ally.gameObject.activeInHierarchy) continue;

                var profile = ally.PersonalityProfile;
                // Приоритет защиты Медика или раненого Страйкера
                if (profile != null && (profile.combatClass == CombatClass.Leader || ally.CurrentHealth < lowestHp))
                {
                    lowestHp = ally.CurrentHealth;
                    target = ally;
                }
            }

            return target;
        }

        /// <summary>
        /// Попытка Медика полечить раненого союзника
        /// </summary>
        public bool TryMedicHealPulse(EnemyAIController2D medic, float range = 8.0f)
        {
            if (_medicHealCooldown > 0f) return false;

            EnemyAIController2D bestTarget = null;
            float maxMissingHp = 0f;

            for (int i = 0; i < _registeredEnemies.Count; i++)
            {
                var ally = _registeredEnemies[i];
                if (ally == null || ally.IsDead || !ally.gameObject.activeInHierarchy) continue;

                float dist = Vector2.Distance(medic.transform.position, ally.transform.position);
                if (dist <= range && ally.CurrentHealth < ally.MaxHealth * 0.75f)
                {
                    float missing = ally.MaxHealth - ally.CurrentHealth;
                    if (missing > maxMissingHp)
                    {
                        maxMissingHp = missing;
                        bestTarget = ally;
                    }
                }
            }

            if (bestTarget != null)
            {
                _medicHealCooldown = 5.5f;
                float healAmount = bestTarget.MaxHealth * 0.35f;
                bestTarget.Heal(healAmount);
                Debug.Log($"<color=green><b>[Director]</b></color> Медик {medic.name} исцелил союзника {bestTarget.name} на {healAmount:F0} HP!");
                return true;
            }

            return false;
        }

        private void Update()
        {
            if (_medicHealCooldown > 0f)
            {
                _medicHealCooldown -= Time.deltaTime;
            }
        }
    }
}
