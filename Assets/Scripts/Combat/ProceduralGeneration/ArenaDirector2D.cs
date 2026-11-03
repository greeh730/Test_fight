using System;
using System.Collections;
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

                // Спавн ярких визуальных и звуковых эффектов лечения
                SpawnMedicHealEffects(medic, bestTarget, healAmount);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Комплексный визуальный эффект лечения медиком:
        /// 1. Всплывающий боевой текст над головой медика и цели
        /// 2. Изумрудная вспышка спрайтов обоих мобов
        /// 3. Пульсирующая анимация бейджа медика
        /// 4. Расширяющаяся аура-кольцо исцеления
        /// 5. Светящийся энергетический луч от медика к союзнику
        /// 6. Взлетающие зеленые крестики-частицы
        /// 7. Гармоничный приятный звуковой перезвон
        /// </summary>
        public void SpawnMedicHealEffects(EnemyAIController2D medic, EnemyAIController2D target, float healAmount)
        {
            Vector3 medicPos = medic != null ? medic.transform.position : transform.position;
            Vector3 targetPos = target != null ? target.transform.position : medicPos;

            // 1. Всплывающий боевой текст
            Combat.Common.CombatFloatingText.Spawn(
                medicPos + Vector3.up * 1.8f,
                "➕ ИМПУЛЬС ЛЕЧЕНИЯ!",
                new Color(0.25f, 1.0f, 0.5f, 1f),
                1.1f, 0.9f, 0.082f, 42, 90
            );

            Combat.Common.CombatFloatingText.Spawn(
                targetPos + Vector3.up * 1.8f,
                $"+{healAmount:F0} HP 💚",
                new Color(0.15f, 1.0f, 0.45f, 1f),
                1.35f, 1.25f, 0.09f, 46, 95
            );

            // 2. Вспышка спрайта и анимация бейджа
            if (medic != null)
            {
                medic.FlashColor(new Color(0.25f, 1.0f, 0.5f, 1f), 0.40f);
                var sil = medic.GetComponent<EnemyVisualSilhouette>();
                if (sil != null) sil.TriggerHealPulseAnimation();
            }

            if (target != null && target != medic)
            {
                target.FlashColor(new Color(0.15f, 1.0f, 0.45f, 1f), 0.45f);
            }

            // 3. Расширяющаяся аура-кольцо вокруг медика
            StartCoroutine(PulseRingRoutine(medicPos));

            // 4. Светящийся соединительный луч от медика к союзнику
            StartCoroutine(HealBeamRoutine(medicPos + Vector3.up * 0.8f, targetPos + Vector3.up * 0.8f));

            // 5. Взлетающие зеленые крестики вокруг исцеленного союзника
            SpawnHealCrossParticles(targetPos + Vector3.up * 0.6f);

            // 6. Гармоничный звуковой перезвон
            PlayProceduralHealChime(targetPos);
        }

        private static Sprite _pulseRingSprite;
        private static Sprite GetPulseRingSprite()
        {
            if (_pulseRingSprite != null) return _pulseRingSprite;
            int res = 64;
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            float center = (res - 1) * 0.5f;
            float radius = center * 0.95f;
            float innerRadius = radius * 0.60f;
            Color[] cols = new Color[res * res];
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float alpha = 0f;
                    if (dist <= radius && dist >= innerRadius)
                    {
                        float ringMid = (radius + innerRadius) * 0.5f;
                        float halfThick = (radius - innerRadius) * 0.5f;
                        alpha = 1.0f - Mathf.Abs(dist - ringMid) / halfThick;
                        alpha = Mathf.SmoothStep(0f, 1f, alpha);
                    }
                    cols[y * res + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(cols);
            tex.Apply();
            _pulseRingSprite = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
            return _pulseRingSprite;
        }

        private IEnumerator PulseRingRoutine(Vector3 center)
        {
            var ringGo = new GameObject("Heal_PulseRing");
            ringGo.hideFlags = HideFlags.DontSave;
            ringGo.transform.position = center;
            var sr = ringGo.AddComponent<SpriteRenderer>();
            sr.sprite = GetPulseRingSprite();
            sr.sortingLayerName = "Default";
            sr.sortingOrder = 80;

            float duration = 0.55f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float scale = Mathf.Lerp(0.5f, 4.2f, t);
                ringGo.transform.localScale = new Vector3(scale, scale, 1f);
                float alpha = (1.0f - t) * 0.9f;
                sr.color = new Color(0.2f, 1.0f, 0.5f, alpha);
                yield return null;
            }
            Destroy(ringGo);
        }

        private IEnumerator HealBeamRoutine(Vector3 start, Vector3 end)
        {
            var beamGo = new GameObject("Heal_Beam");
            beamGo.hideFlags = HideFlags.DontSave;
            var lr = beamGo.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
            lr.useWorldSpace = true;
            lr.startWidth = 0.28f;
            lr.endWidth = 0.28f;

            var mat = new Material(Shader.Find("Sprites/Default"));
            lr.material = mat;
            lr.sortingLayerName = "Default";
            lr.sortingOrder = 90;

            float duration = 0.45f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float alpha = (1.0f - t);
                lr.startColor = new Color(0.2f, 1.0f, 0.5f, alpha * 0.95f);
                lr.endColor = new Color(0.1f, 0.85f, 0.65f, alpha * 0.95f);
                float w = Mathf.Lerp(0.28f, 0.04f, t);
                lr.startWidth = w;
                lr.endWidth = w;
                yield return null;
            }
            Destroy(mat);
            Destroy(beamGo);
        }

        private void SpawnHealCrossParticles(Vector3 center)
        {
            int count = 6;
            for (int i = 0; i < count; i++)
            {
                var crossGo = new GameObject("Heal_CrossParticle");
                crossGo.hideFlags = HideFlags.DontSave;
                Vector3 spawnOffset = new Vector3(UnityEngine.Random.Range(-0.45f, 0.45f), UnityEngine.Random.Range(-0.35f, 0.35f), 0f);
                crossGo.transform.position = center + spawnOffset;
                crossGo.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.65f, 0.95f);

                var sr = crossGo.AddComponent<SpriteRenderer>();
                sr.sprite = EnemyVisualSilhouette.IconLeader;
                sr.color = new Color(0.2f, 1.0f, 0.45f, 0.95f);
                sr.sortingLayerName = "Default";
                sr.sortingOrder = 92;

                StartCoroutine(CrossParticleRoutine(crossGo, sr));
            }
        }

        private IEnumerator CrossParticleRoutine(GameObject crossGo, SpriteRenderer sr)
        {
            float duration = UnityEngine.Random.Range(0.55f, 0.80f);
            float elapsed = 0f;
            Vector3 vel = new Vector3(UnityEngine.Random.Range(-0.4f, 0.4f), UnityEngine.Random.Range(1.8f, 3.2f), 0f);
            Vector3 baseScale = crossGo.transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                if (crossGo == null) yield break;
                crossGo.transform.position += vel * Time.deltaTime;
                vel.y = Mathf.MoveTowards(vel.y, 0.8f, Time.deltaTime * 2.5f);

                float alpha = (1.0f - t);
                if (sr != null) sr.color = new Color(0.2f, 1.0f, 0.45f, alpha);
                crossGo.transform.localScale = baseScale * (1f + t * 0.35f);
                yield return null;
            }
            if (crossGo != null) Destroy(crossGo);
        }

        private void PlayProceduralHealChime(Vector3 pos)
        {
            var sfxObj = new GameObject("Heal_ChimeSFX");
            sfxObj.hideFlags = HideFlags.DontSave;
            sfxObj.transform.position = pos;
            var audio = sfxObj.AddComponent<AudioSource>();
            audio.spatialBlend = 0.25f;
            audio.volume = 0.80f;

            int sampleRate = 22050;
            float duration = 0.40f;
            int numSamples = (int)(sampleRate * duration);
            float[] samples = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 8f);
                float tone = (Mathf.Sin(2f * Mathf.PI * 523.25f * t) * 0.45f)
                           + (Mathf.Sin(2f * Mathf.PI * 659.25f * t) * 0.35f)
                           + (Mathf.Sin(2f * Mathf.PI * 783.99f * t) * 0.25f)
                           + (Mathf.Sin(2f * Mathf.PI * 1046.50f * t) * 0.15f);
                samples[i] = tone * env * 0.40f;
            }

            var clip = AudioClip.Create("HealChime", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            audio.PlayOneShot(clip);
            Destroy(sfxObj, 0.65f);
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
