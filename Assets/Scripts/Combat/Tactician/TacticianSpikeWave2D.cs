using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Combat.Tactician
{
    /// <summary>
    /// Направленные шипы (↘️):
    /// Волна магических шипов, бегущая по земле вперед.
    /// Поражает врагов по нижнему уровню (Low) и досрочно детонирует установленные печати (⬇️) с усиленным резонансом!
    /// </summary>
    [DisallowMultipleComponent]
    public class TacticianSpikeWave2D : MonoBehaviour
    {
        [Header("--- Wave Settings ---")]
        [SerializeField] private float speed = 9.5f;
        [SerializeField] private float maxDistance = 6.0f;
        [SerializeField] private float damage = 22f;
        [SerializeField] private float hitRadius = 0.85f;
        [SerializeField] private LayerMask enemyLayers = ~0;

        [Header("--- Visuals ---")]
        [SerializeField] private Color spikeColor = new Color(0.1f, 0.85f, 1f, 0.95f);

        private float _direction = 1f;
        private float _traveledDistance = 0f;
        private Vector2 _knockback = new Vector2(4.5f, 2.0f);
        private readonly HashSet<object> _hitTargets = new HashSet<object>();
        private readonly HashSet<TacticianTrap2D> _detonatedTraps = new HashSet<TacticianTrap2D>();

        public void Initialize(float facingDirection)
        {
            _direction = Mathf.Sign(facingDirection);
            CreateSpikeVisual();
        }

        public void Initialize(float facingDirection, float travelSpeed, float maxDist, float radius, float dmg, Vector2 kb, Color color)
        {
            _direction = Mathf.Sign(facingDirection);
            speed = travelSpeed;
            maxDistance = maxDist;
            hitRadius = radius;
            damage = dmg;
            _knockback = kb;
            spikeColor = color;
            CreateSpikeVisual();
        }

        private void CreateSpikeVisual()
        {
            var tex = new Texture2D(2, 2);
            tex.SetPixels(new Color[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2);

            // Создаем форму кристаллического шипа
            for (int i = 0; i < 3; i++)
            {
                var spikeObj = new GameObject($"CrystalSpike_{i}");
                spikeObj.transform.SetParent(transform, false);
                spikeObj.transform.localPosition = new Vector3(i * 0.28f * _direction, i * 0.12f, 0f);
                spikeObj.transform.localRotation = Quaternion.Euler(0f, 0f, -25f * _direction + i * 12f);
                spikeObj.transform.localScale = new Vector3(0.22f, 0.7f + i * 0.2f, 1f);

                var sr = spikeObj.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = spikeColor;
                sr.sortingOrder = 32;
            }
        }

        private void Update()
        {
            float step = speed * Time.deltaTime;
            transform.position += new Vector3(_direction * step, 0f, 0f);
            _traveledDistance += step;

            // 1. Проверка поражения врагов
            CheckEnemyHits();

            // 2. Проверка детонации ловушек по пути
            CheckTrapSynergy();

            if (_traveledDistance >= maxDistance)
            {
                Destroy(gameObject);
            }
        }

        private void CheckEnemyHits()
        {
            var colliders = Physics2D.OverlapCircleAll(transform.position, hitRadius, enemyLayers);
            var spikeAttack = new AttackConfig(
                "Направленные шипы",
                CombatZone.Low,
                Vector2.zero,
                new Vector2(1.2f, 0.8f),
                0f, 0.1f, 0.1f,
                damage,
                new Vector2(_knockback.x * _direction, _knockback.y),
                spikeColor
            );

            for (int i = 0; i < colliders.Length; i++)
            {
                var col = colliders[i];
                if (col == null || col.CompareTag("Player")) continue;

                var enemy = col.GetComponent<EnemyAIController2D>() ?? col.GetComponentInParent<EnemyAIController2D>();
                if (enemy != null && !enemy.IsDead && !_hitTargets.Contains(enemy))
                {
                    _hitTargets.Add(enemy);
                    enemy.TakeHit(spikeAttack, CombatZone.Low, transform.position, new Vector2(_direction, 0.4f).normalized);
                    continue;
                }

                var dummy = col.GetComponent<CombatDummy2D>() ?? col.GetComponentInParent<CombatDummy2D>();
                if (dummy != null && !dummy.IsDead && !_hitTargets.Contains(dummy))
                {
                    _hitTargets.Add(dummy);
                    dummy.TakeHit(spikeAttack, CombatZone.Low, transform.position, new Vector2(_direction, 0.4f).normalized);
                }
            }
        }

        private void CheckTrapSynergy()
        {
            for (int i = 0; i < TacticianTrap2D.ActiveTraps.Count; i++)
            {
                var trap = TacticianTrap2D.ActiveTraps[i];
                if (trap == null || _detonatedTraps.Contains(trap)) continue;

                float dist = Vector2.Distance(transform.position, trap.transform.position);
                if (dist <= hitRadius + 0.5f)
                {
                    _detonatedTraps.Add(trap);
                    Debug.Log("<color=#FFCC00><b>[РЕЗОНАНС ЛОВУШКИ!]</b></color> Волна шипов сдетонировала Глубинную печать!");
                    trap.Detonate(null, isResonance: true);
                }
            }
        }
    }
}
