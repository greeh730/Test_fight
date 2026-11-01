using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Combat.Common
{
    /// <summary>
    /// Генератор эффекта разлёта белых частиц/искр при ударе оружием (White Hit Sparks).
    /// Создаёт яркие белые осколки/искры в точке соприкосновения клинка.
    /// Не требует префабов и работает процедурно.
    /// </summary>
    public static class CombatHitSparks
    {
        private static Sprite s_SparkSprite;

        private static void EnsureSparkSprite()
        {
            if (s_SparkSprite != null) return;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            Color c = Color.white;
            tex.SetPixels(new Color[] { c, c, c, c });
            tex.Apply();
            s_SparkSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
        }

        /// <summary>
        /// Спавнит веер белых искр/частиц в точке удара hitPoint.
        /// </summary>
        public static void SpawnWhiteSparks(Vector2 hitPoint, Vector2 knockbackDir, int count = 12)
        {
            if (!Application.isPlaying) return;
            EnsureSparkSprite();
            if (s_SparkSprite == null) return;

            float baseDirX = Mathf.Abs(knockbackDir.x) > 0.05f ? Mathf.Sign(knockbackDir.x) : 1f;

            Color[] sparkColors = new Color[]
            {
                Color.white,
                new Color(0.96f, 0.98f, 1f, 1f),
                new Color(0.90f, 0.95f, 1f, 1f),
                new Color(1f, 1f, 0.96f, 1f)
            };

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Hit_WhiteSpark");
                go.hideFlags = HideFlags.DontSave;
                go.transform.position = (Vector3)hitPoint + new Vector3(Random.Range(-0.08f, 0.08f), Random.Range(-0.08f, 0.08f), 0f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = s_SparkSprite;
                sr.color = sparkColors[Random.Range(0, sparkColors.Length)];
                sr.sortingOrder = 90;

                // Размер искры
                float size = Random.Range(0.06f, 0.13f);
                go.transform.localScale = new Vector3(size, size, 1f);

                // Вектор разлёта во все стороны веером
                float angleDeg = Random.Range(20f, 160f);
                if (baseDirX < 0f) angleDeg = 180f - angleDeg;
                float rad = angleDeg * Mathf.Deg2Rad;

                float speed = Random.Range(3.5f, 7.5f);
                Vector2 vel = new Vector2(
                    Mathf.Cos(rad) * speed + Random.Range(-1.2f, 1.2f),
                    Mathf.Sin(rad) * speed + Random.Range(-0.5f, 1.5f)
                );

                float lifetime = Random.Range(0.22f, 0.38f);
                float rotSpeed = Random.Range(-720f, 720f);

                var runner = go.AddComponent<SparkRunner>();
                runner.Initialize(vel, rotSpeed, lifetime, sr);
            }
        }

        private class SparkRunner : MonoBehaviour
        {
            private Vector2 _vel;
            private float _rotSpeed;
            private float _lifetime;
            private float _elapsed;
            private SpriteRenderer _sr;
            private Color _startColor;
            private const float Gravity = -20f;

            public void Initialize(Vector2 vel, float rotSpeed, float lifetime, SpriteRenderer sr)
            {
                _vel = vel;
                _rotSpeed = rotSpeed;
                _lifetime = Mathf.Max(0.08f, lifetime);
                _sr = sr;
                _startColor = sr != null ? sr.color : Color.white;
            }

            private void Update()
            {
                _elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(_elapsed / _lifetime);

                _vel.y += Gravity * Time.deltaTime;
                transform.position += (Vector3)(_vel * Time.deltaTime);
                transform.Rotate(0f, 0f, _rotSpeed * Time.deltaTime);

                if (_sr != null)
                {
                    Color c = _startColor;
                    c.a = Mathf.Clamp01(1f - (t * t));
                    _sr.color = c;
                }

                if (_elapsed >= _lifetime)
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
