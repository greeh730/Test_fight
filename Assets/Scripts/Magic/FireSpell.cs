// Assets/Scripts/Magic/FireSpell.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fencing;

namespace Magic
{
    /// <summary>
    /// Механика заклинания "Огонь":
    /// - Базируется на универсальном классе BaseMagicSpell.
    /// - Генерирует случайную комбинацию из 3 направлений (стрелок) в строке над колесом.
    /// - Имеет огненный стиль: обводка цвета огня, стрелки огненного оттенка и иконка огня слева сверху.
    /// - При успехе: выстреливает фаерболом по направлению взгляда персонажа.
    /// - При ошибке: взрыв в эпицентре (50 урона), самоподжог игрока и вылет огненных частиц на землю.
    /// </summary>
    [DisallowMultipleComponent]
    public class FireSpell : BaseMagicSpell
    {
        [Header("--- Настройки фаербола (Fireball) ---")]
        [Tooltip("Префаб выстреливаемого фаербола")]
        [SerializeField] private GameObject fireballPrefab;

        [Tooltip("Смещение точки выстрела относительно центра тела игрока (0,0 = ровно из центра)")]
        [SerializeField] private Vector2 fireballSpawnOffset = Vector2.zero;

        [Tooltip("Скорость полёта фаербола")]
        [SerializeField] private float fireballSpeed = 15f;

        [Tooltip("Размер фаербола (масштаб)")]
        [SerializeField] private float fireballScale = 1.0f;

        [Header("--- Настройки урона и поджога фаербола ---")]
        [Tooltip("Урон от прямого попадания фаербола")]
        [SerializeField] private float fireballDamage = 50f;

        [Tooltip("Длительность эффекта горения в секундах")]
        [SerializeField] private float burnDuration = 3f;

        [Tooltip("Урон от горения за один тик (в секунду)")]
        [SerializeField] private float burnDamagePerTick = 5f;

        [Tooltip("Радиус распространения огня на подошедших рядом")]
        [SerializeField] private float burnSpreadRadius = 1.5f;

        [Header("--- Настройки последствий провала (Failure Effects) ---")]
        [Tooltip("Поджигать ли самого игрока при ошибке ввода")]
        [SerializeField] private bool selfBurnOnFailure = true;

        [Tooltip("Длительность поджога игрока при провале в секундах")]
        [SerializeField] private float failSelfBurnDuration = 3f;

        [Tooltip("Периодический урон от самоподжога в секунду")]
        [SerializeField] private float failSelfBurnDamage = 5f;

        [Tooltip("Количество вылетающих огненных частиц при провале")]
        [Range(3, 20)]
        [SerializeField] private int failEmberCount = 8;

        [Tooltip("Минимальная скорость разлёта частиц")]
        [SerializeField] private float emberSpeedMin = 5f;

        [Tooltip("Максимальная скорость разлёта частиц")]
        [SerializeField] private float emberSpeedMax = 9f;

        [Tooltip("Длительность горения частиц на земле в секундах")]
        [SerializeField] private float groundFireDuration = 3f;

        [Tooltip("Префаб огненной частицы (Ember)")]
        [SerializeField] private GameObject emberPrefab;

        [Header("--- Взрыв при провале (Explosion on Failure) ---")]
        [Tooltip("Происходит ли взрыв в эпицентре игрока при провале заклинания")]
        [SerializeField] private bool enableExplosionOnFailure = true;

        [Tooltip("Урон от взрыва по игроку и окружающим целям")]
        [SerializeField] private float explosionDamage = 50f;

        [Tooltip("Радиус поражения взрыва в метрах")]
        [SerializeField] private float explosionRadius = 3.0f;

        [Tooltip("Сила отталкивания окружающих врагов от взрыва")]
        [SerializeField] private float explosionKnockbackForce = 8f;

        [Tooltip("Префаб эффекта взрыва")]
        [SerializeField] private GameObject explosionPrefab;

        #region Properties
        public GameObject FireballPrefab
        {
            get => fireballPrefab;
            set => fireballPrefab = value;
        }

        public float FireballSpeed
        {
            get => fireballSpeed;
            set => fireballSpeed = Mathf.Max(1f, value);
        }

        public float FireballScale
        {
            get => fireballScale;
            set => fireballScale = Mathf.Max(0.2f, value);
        }

        public Vector2 FireballSpawnOffset
        {
            get => fireballSpawnOffset;
            set => fireballSpawnOffset = value;
        }

        public float FireballDamage { get => fireballDamage; set => fireballDamage = Mathf.Max(0f, value); }
        public float BurnDuration { get => burnDuration; set => burnDuration = Mathf.Max(0.1f, value); }
        public float BurnDamagePerTick { get => burnDamagePerTick; set => burnDamagePerTick = Mathf.Max(0f, value); }
        public float BurnSpreadRadius { get => burnSpreadRadius; set => burnSpreadRadius = Mathf.Max(0.2f, value); }

        public bool SelfBurnOnFailure { get => selfBurnOnFailure; set => selfBurnOnFailure = value; }
        public float FailSelfBurnDuration { get => failSelfBurnDuration; set => failSelfBurnDuration = Mathf.Max(0.1f, value); }
        public float FailSelfBurnDamage { get => failSelfBurnDamage; set => failSelfBurnDamage = Mathf.Max(0f, value); }
        public int FailEmberCount { get => failEmberCount; set => failEmberCount = Mathf.Clamp(value, 0, 30); }
        public float GroundFireDuration { get => groundFireDuration; set => groundFireDuration = Mathf.Max(0.5f, value); }
        public GameObject EmberPrefab { get => emberPrefab; set => emberPrefab = value; }

        public bool EnableExplosionOnFailure { get => enableExplosionOnFailure; set => enableExplosionOnFailure = value; }
        public float ExplosionDamage { get => explosionDamage; set => explosionDamage = Mathf.Max(0f, value); }
        public float ExplosionRadius { get => explosionRadius; set => explosionRadius = Mathf.Max(0.5f, value); }
        public float ExplosionKnockbackForce { get => explosionKnockbackForce; set => explosionKnockbackForce = value; }
        public GameObject ExplosionPrefab { get => explosionPrefab; set => explosionPrefab = value; }
        #endregion

        protected virtual void Awake()
        {
            spellName = "Огонь";
            borderColor = new Color(1.0f, 0.42f, 0.05f, 1f);
            arrowColor = new Color(1.0f, 0.68f, 0.15f, 1f);
            fillColor = new Color(0.04f, 0.02f, 0.01f, 0.96f);

            EnsureFireIcon();
        }

        private void EnsureFireIcon()
        {
            if (spellIcon == null)
            {
#if UNITY_EDITOR
                spellIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Magic/Fire_Icon.png");
#endif
                if (spellIcon == null)
                {
                    spellIcon = Resources.Load<Sprite>("Sprites/Magic/Fire_Icon");
                }
            }
        }

        protected override void OnCastSuccess()
        {
            if (Combat.Audio.SoundManager.Instance != null)
            {
                Combat.Audio.SoundManager.Instance.PlayFireball();
            }
            CastFireball();
        }

        protected override void OnCastFailure(string reason)
        {
            TriggerFailureExplosion();

            if (selfBurnOnFailure)
            {
                BurnStatusEffect.Apply(gameObject, failSelfBurnDuration, failSelfBurnDamage, 1.0f, burnSpreadRadius);
            }

            SpawnFailureEmbers();
        }

        /// <summary>
        /// Выстрел фаерболом в сторону взгляда персонажа.
        /// </summary>
        [ContextMenu("Тестовый выстрел фаерболом (Cast Fireball)")]
        public void CastFireball()
        {
            float facing = 1f;
            var pc = GetComponent<Combat.Player.PlayerController2D>();
            if (pc != null)
            {
                facing = pc.CurrentFacing;
            }
            else
            {
                var sr = GetComponent<SpriteRenderer>();
                if (sr != null && sr.flipX) facing = -1f;
                else if (transform.localScale.x < 0f) facing = -1f;
            }

            Vector2 flyDirection = new Vector2(facing, 0f).normalized;

            // Выстрел ровно из центра тела игрока
            Vector3 bodyCenter = transform.position;
            var playerCol = GetComponent<Collider2D>();
            if (playerCol != null)
            {
                bodyCenter = playerCol.bounds.center;
            }

            Vector3 spawnPos = bodyCenter + new Vector3(facing * fireballSpawnOffset.x, fireballSpawnOffset.y, 0f);

            GameObject fbObj = null;
            if (fireballPrefab != null)
            {
                fbObj = Instantiate(fireballPrefab, spawnPos, Quaternion.identity);
            }
            else
            {
                var loaded = Resources.Load<GameObject>("Prefabs/Fireball");
#if UNITY_EDITOR
                if (loaded == null)
                {
                    loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Magic/Fireball.prefab");
                }
#endif
                if (loaded != null)
                {
                    fbObj = Instantiate(loaded, spawnPos, Quaternion.identity);
                }
            }

            if (fbObj != null)
            {
                var projectile = fbObj.GetComponent<FireballProjectile>();
                if (projectile != null)
                {
                    projectile.Initialize(transform, flyDirection, fireballSpeed, fireballScale, fireballDamage, burnDuration, burnDamagePerTick);
                }
                Debug.Log($"<color=#FFAA00>[Огонь]</color> Фаербол запущен в направлении: <b>{(facing > 0 ? "ВПРАВО ->" : "<- ВЛЕВО")}</b> из центра тела (скорость: {fireballSpeed:F1}, размер: {fireballScale:F1})");
            }
            else
            {
                Debug.LogWarning("[Огонь] Префаб фаербола не назначен!");
            }
        }

        /// <summary>
        /// Выброс огненных частиц во все стороны при провале заклинания.
        /// </summary>
        [ContextMenu("Тестовый выброс частиц провала (Spawn Embers)")]
        public void SpawnFailureEmbers()
        {
            if (failEmberCount <= 0) return;

            Vector3 spawnOrigin = transform.position + new Vector3(0f, 0.45f, 0f);

            GameObject prefabToUse = emberPrefab;
            if (prefabToUse == null)
            {
                prefabToUse = Resources.Load<GameObject>("Prefabs/Magic/FireEmber");
#if UNITY_EDITOR
                if (prefabToUse == null)
                {
                    prefabToUse = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Magic/FireEmber.prefab");
                }
#endif
            }

            if (prefabToUse == null)
            {
                Debug.LogWarning("[Огонь] Префаб FireEmber не найден!");
                return;
            }

            float angleStep = 140f / Mathf.Max(1, failEmberCount - 1);
            float startAngle = 20f;

            for (int i = 0; i < failEmberCount; i++)
            {
                float baseAngle = startAngle + i * angleStep;
                float angle = baseAngle + UnityEngine.Random.Range(-8f, 8f);
                float rad = angle * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                float speed = UnityEngine.Random.Range(emberSpeedMin, emberSpeedMax);

                GameObject emberObj = Instantiate(prefabToUse, spawnOrigin, Quaternion.identity);
                var ember = emberObj.GetComponent<FireEmber>();
                if (ember != null)
                {
                    ember.Launch(dir * speed, groundFireDuration, burnDuration, burnDamagePerTick);
                }
            }

            Debug.Log($"<color=#FF4400>[Огонь]</color> Выброс <b>{failEmberCount}</b> горящих частиц при провале заклинания!");
        }

        /// <summary>
        /// Взрыв игрока в эпицентре при провале заклинания:
        /// Наносит 50 урона игроку и всем стоящим рядом врагам, создаёт ударную волну и тряску камеры.
        /// </summary>
        [ContextMenu("Тестовый взрыв при провале (Trigger Explosion)")]
        public void TriggerFailureExplosion()
        {
            if (!enableExplosionOnFailure) return;

            Vector3 epicenter = transform.position + new Vector3(0f, 0.45f, 0f);

            var playerHealth = GetComponent<Combat.Player.PlayerHealth2D>();
            if (playerHealth != null)
            {
                playerHealth.TakeDirectDamage(explosionDamage);
                Debug.Log($"<color=#FF2200>[ВЗРЫВ]</color> Игрок подорвался на собственном заклинании: <b>-{explosionDamage:F0} HP</b>!");
            }

            GameObject prefabToUse = explosionPrefab;
            if (prefabToUse == null)
            {
                prefabToUse = Resources.Load<GameObject>("Prefabs/Magic/FireExplosion");
#if UNITY_EDITOR
                if (prefabToUse == null)
                {
                    prefabToUse = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Magic/FireExplosion.prefab");
                }
#endif
            }

            if (prefabToUse != null)
            {
                GameObject explObj = Instantiate(prefabToUse, epicenter, Quaternion.identity);
                var explComp = explObj.GetComponent<FireExplosion>();
                if (explComp != null)
                {
                    explComp.Explode(explosionDamage, explosionRadius, explosionKnockbackForce, gameObject);
                }
            }
            else
            {
                // Если префаб взрыва не найден, всё равно проигрываем звук взрыва
                if (Combat.Audio.SoundManager.Instance != null)
                {
                    Combat.Audio.SoundManager.Instance.PlayExplosion();
                }
            }
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            EnsureFireIcon();
        }
    }
}
