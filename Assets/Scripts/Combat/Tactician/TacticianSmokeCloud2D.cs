using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Combat.Tactician
{
    /// <summary>
    /// Тактический отход (⬅️):
    /// Дымовая завеса / ослепляющая бомба.
    /// Враги внутри дыма теряют игрока из виду и прерывают свои атаки.
    /// </summary>
    [DisallowMultipleComponent]
    public class TacticianSmokeCloud2D : MonoBehaviour
    {
        [Header("--- Smoke Settings ---")]
        [SerializeField] private float cloudRadius = 2.0f;
        [SerializeField] private float disorientDuration = 2.0f;
        [SerializeField] private float lifetime = 3.2f;
        [SerializeField] private LayerMask enemyLayers = ~0;

        [Header("--- Visuals ---")]
        [SerializeField] private Color smokeColor = new Color(0.85f, 0.9f, 0.95f, 0.5f);

        private readonly List<SpriteRenderer> _puffs = new List<SpriteRenderer>();
        private float _spawnTime;

        private void Awake()
        {
            _spawnTime = Time.time;
            CreateSmokePuffs();
        }

        private void CreateSmokePuffs()
        {
            var tex = new Texture2D(4, 4);
            var cols = new Color[16];
            for (int i = 0; i < 16; i++) cols[i] = Color.white;
            tex.SetPixels(cols);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);

            int count = 6;
            for (int i = 0; i < count; i++)
            {
                var puffObj = new GameObject($"SmokePuff_{i}");
                puffObj.transform.SetParent(transform, false);

                float angle = i * Mathf.PI * 2f / count;
                float r = Random.Range(0.2f, cloudRadius * 0.55f);
                puffObj.transform.localPosition = new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r * 0.65f, 0f);

                float sz = Random.Range(1.3f, 1.9f);
                puffObj.transform.localScale = Vector3.one * sz;

                var sr = puffObj.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = smokeColor;
                sr.sortingOrder = 38;
                _puffs.Add(sr);
            }
        }

        private void Update()
        {
            float age = Time.time - _spawnTime;

            // Движение и плавное рассеивание клубов дыма
            for (int i = 0; i < _puffs.Count; i++)
            {
                if (_puffs[i] == null) continue;
                float pulse = 1f + 0.1f * Mathf.Sin(age * 3f + i);
                _puffs[i].transform.localScale = Vector3.one * (1.5f * pulse);

                Color c = smokeColor;
                c.a = Mathf.Clamp01(smokeColor.a * (1f - (age / lifetime)));
                _puffs[i].color = c;
            }

            // Ослепление врагов в дыму
            CheckEnemiesInSmoke();

            if (age >= lifetime)
            {
                Destroy(gameObject);
            }
        }

        private void CheckEnemiesInSmoke()
        {
            var colliders = Physics2D.OverlapCircleAll(transform.position, cloudRadius, enemyLayers);
            for (int i = 0; i < colliders.Length; i++)
            {
                var col = colliders[i];
                if (col == null || col.CompareTag("Player")) continue;

                var enemy = col.GetComponent<EnemyAIController2D>() ?? col.GetComponentInParent<EnemyAIController2D>();
                if (enemy != null && !enemy.IsDead)
                {
                    enemy.DisorientFromSmoke(disorientDuration);
                }
            }
        }
    }
}
