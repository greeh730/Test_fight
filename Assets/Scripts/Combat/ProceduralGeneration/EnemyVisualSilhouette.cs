using System;
using System.Collections;
using UnityEngine;

namespace Combat.ProceduralGeneration
{
    /// <summary>
    /// Компонент визуальной читаемости и силуэта врага:
    /// - Отображает значок класса над головой (Череп 💀, Щит 🛡️, Крест ➕, Ловушка 🕸️)
    /// - Окрашивает силуэт врага в тематический оттенок роли
    /// - Корректирует пропорции тела (Танк крупнее, Страйкер быстрее и тоньше)
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyVisualSilhouette : MonoBehaviour
    {
        private static Sprite _iconStriker;
        private static Sprite _iconTank;
        private static Sprite _iconLeader;
        private static Sprite _iconController;

        public static Sprite IconLeader
        {
            get
            {
                if (_iconLeader == null) GenerateProceduralIcons();
                return _iconLeader;
            }
        }

        [Header("--- Отображение роли ---")]
        [SerializeField] private CombatClass currentClass;
        [SerializeField] private CombatArchetype currentArchetype;
        [SerializeField] private string roleName = "";

        private SpriteRenderer _enemySprite;
        private GameObject _badgeObject;
        private SpriteRenderer _badgeRenderer;
        private TextMesh _roleLabel;
        private Vector3 _baseScale = Vector3.one;

        private void Awake()
        {
            _enemySprite = GetComponent<SpriteRenderer>();
            _baseScale = transform.localScale;
            EnsureBadgeGraphics();
        }

        /// <summary>
        /// Применение процедурного профиля: меняет силуэт, размер, значок и оттенок
        /// </summary>
        public void ApplyProfile(EnemyPersonalityProfile profile)
        {
            if (profile == null) return;
            if (_enemySprite == null) _enemySprite = GetComponent<SpriteRenderer>();

            currentClass = profile.combatClass;
            currentArchetype = profile.archetype;
            roleName = profile.roleTitle;

            // 1. Масштаб силуэта
            transform.localScale = _baseScale * profile.scaleMultiplier;

            // 2. Цветовой оттенок силуэта
            if (_enemySprite != null)
            {
                Color c = Color.Lerp(Color.white, profile.classSilhouetteColor, 0.40f);
                _enemySprite.color = c;
            }

            // 3. Обновление значка над головой
            EnsureBadgeGraphics();
            UpdateBadgeVisuals(profile);
        }

        private void EnsureBadgeGraphics()
        {
            if (_iconStriker == null) GenerateProceduralIcons();

            if (_badgeObject == null)
            {
                _badgeObject = new GameObject("[Class_Badge_UI]");
                _badgeObject.transform.SetParent(transform);
                _badgeObject.transform.localPosition = new Vector3(0f, 1.45f, 0f);

                _badgeRenderer = _badgeObject.AddComponent<SpriteRenderer>();
                _badgeRenderer.sortingLayerName = _enemySprite != null ? _enemySprite.sortingLayerName : "Default";
                _badgeRenderer.sortingOrder = (_enemySprite != null ? _enemySprite.sortingOrder : 10) + 15;

                var textObj = new GameObject("[Role_Text]");
                textObj.transform.SetParent(_badgeObject.transform);
                textObj.transform.localPosition = new Vector3(0f, 0.35f, 0f);
                textObj.transform.localScale = new Vector3(0.12f, 0.12f, 1f);

                _roleLabel = textObj.AddComponent<TextMesh>();
                _roleLabel.alignment = TextAlignment.Center;
                _roleLabel.anchor = TextAnchor.MiddleCenter;
                _roleLabel.fontSize = 28;
                _roleLabel.fontStyle = FontStyle.Bold;

                var textRenderer = _roleLabel.GetComponent<MeshRenderer>();
                if (textRenderer != null)
                {
                    textRenderer.sortingLayerName = _badgeRenderer.sortingLayerName;
                    textRenderer.sortingOrder = _badgeRenderer.sortingOrder + 1;
                }
            }
        }

        private void UpdateBadgeVisuals(EnemyPersonalityProfile profile)
        {
            if (_badgeRenderer == null) return;

            switch (profile.combatClass)
            {
                case CombatClass.Striker:
                    _badgeRenderer.sprite = _iconStriker;
                    _badgeRenderer.color = new Color(1.0f, 0.30f, 0.25f, 0.95f);
                    if (_roleLabel != null)
                    {
                        _roleLabel.text = "УБИЙЦА";
                        _roleLabel.color = new Color(1.0f, 0.45f, 0.35f, 1f);
                    }
                    break;

                case CombatClass.Tank:
                    _badgeRenderer.sprite = _iconTank;
                    _badgeRenderer.color = new Color(0.35f, 0.70f, 1.0f, 0.95f);
                    if (_roleLabel != null)
                    {
                        _roleLabel.text = "ТАНК";
                        _roleLabel.color = new Color(0.45f, 0.80f, 1.0f, 1f);
                    }
                    break;

                case CombatClass.Leader:
                    _badgeRenderer.sprite = _iconLeader;
                    _badgeRenderer.color = new Color(0.30f, 1.0f, 0.55f, 0.95f);
                    if (_roleLabel != null)
                    {
                        _roleLabel.text = "МЕДИК";
                        _roleLabel.color = new Color(0.40f, 1.0f, 0.65f, 1f);
                    }
                    break;

                case CombatClass.Controller:
                    _badgeRenderer.sprite = _iconController;
                    _badgeRenderer.color = new Color(0.85f, 0.45f, 1.0f, 0.95f);
                    if (_roleLabel != null)
                    {
                        _roleLabel.text = "ТРАППЕР";
                        _roleLabel.color = new Color(0.90f, 0.55f, 1.0f, 1f);
                    }
                    break;
            }

            _badgeObject.transform.localScale = new Vector3(1.25f, 1.25f, 1f);
        }

        /// <summary>
        /// Анимация импульса бейджа при совершении лечения (пульсация вверх и обратно)
        /// </summary>
        public void TriggerHealPulseAnimation()
        {
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(HealPulseRoutine());
            }
        }

        private IEnumerator HealPulseRoutine()
        {
            if (_badgeObject == null) yield break;
            Vector3 baseScale = _badgeObject.transform.localScale;
            float elapsed = 0f;
            float dur = 0.5f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                float bump = 1f + Mathf.Sin(t * Mathf.PI) * 0.75f;
                if (_badgeObject != null)
                {
                    _badgeObject.transform.localScale = baseScale * bump;
                }
                yield return null;
            }
            if (_badgeObject != null)
            {
                _badgeObject.transform.localScale = baseScale;
            }
        }

        private void LateUpdate()
        {
            // Фиксируем бейдж над головой, чтобы он не переворачивался при FlipX родителя
            if (_badgeObject != null)
            {
                float parentFacing = transform.lossyScale.x >= 0 ? 1f : -1f;
                _badgeObject.transform.localScale = new Vector3(Mathf.Abs(_badgeObject.transform.localScale.x) * parentFacing, 
                                                                _badgeObject.transform.localScale.y, 
                                                                _badgeObject.transform.localScale.z);
            }
        }

        /// <summary>
        /// Генерация чётких пиксельных 24x24 спрайтов для значков классов
        /// </summary>
        private static void GenerateProceduralIcons()
        {
            _iconStriker = CreateSpriteFromMatrix(GetStrikerMatrix(), 24, 24);
            _iconTank = CreateSpriteFromMatrix(GetTankMatrix(), 24, 24);
            _iconLeader = CreateSpriteFromMatrix(GetLeaderMatrix(), 24, 24);
            _iconController = CreateSpriteFromMatrix(GetControllerMatrix(), 24, 24);
        }

        private static Sprite CreateSpriteFromMatrix(bool[,] matrix, int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color clear = new Color(0, 0, 0, 0);
            Color fill = Color.white;
            Color outline = new Color(0.1f, 0.1f, 0.15f, 0.9f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (matrix[x, y])
                    {
                        tex.SetPixel(x, y, fill);
                    }
                    else
                    {
                        // Обводка вокруг значка
                        bool isNearPixel = (x > 0 && matrix[x - 1, y]) ||
                                           (x < width - 1 && matrix[x + 1, y]) ||
                                           (y > 0 && matrix[x, y - 1]) ||
                                           (y < height - 1 && matrix[x, y + 1]);
                        tex.SetPixel(x, y, isNearPixel ? outline : clear);
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 32f);
        }

        // 💀 Череп / Кинжалы (Страйкер)
        private static bool[,] GetStrikerMatrix()
        {
            var m = new bool[24, 24];
            // Лезвие / Череп
            for (int y = 10; y <= 20; y++)
            {
                for (int x = 7; x <= 16; x++)
                {
                    if (Vector2.Distance(new Vector2(x, y), new Vector2(11.5f, 15f)) <= 4.5f)
                        m[x, y] = true;
                }
            }
            // Глазницы
            m[9, 14] = false;
            m[10, 14] = false;
            m[13, 14] = false;
            m[14, 14] = false;
            // Зубы / Кинжалы
            for (int y = 4; y <= 9; y++)
            {
                m[10, y] = true;
                m[13, y] = true;
            }
            m[11, 8] = true;
            m[12, 8] = true;
            return m;
        }

        // 🛡️ Щит (Танк)
        private static bool[,] GetTankMatrix()
        {
            var m = new bool[24, 24];
            for (int y = 5; y <= 20; y++)
            {
                int halfWidth = (y >= 12) ? 6 : Mathf.Clamp((y - 4), 1, 6);
                for (int x = 12 - halfWidth; x <= 12 + halfWidth; x++)
                {
                    m[x, y] = true;
                }
            }
            return m;
        }

        // ➕ Крест (Лидер / Медик)
        private static bool[,] GetLeaderMatrix()
        {
            var m = new bool[24, 24];
            // Вертикальная балка
            for (int y = 4; y <= 20; y++)
            {
                for (int x = 10; x <= 14; x++) m[x, y] = true;
            }
            // Горизонтальная балка
            for (int y = 9; y <= 15; y++)
            {
                for (int x = 4; x <= 20; x++) m[x, y] = true;
            }
            return m;
        }

        // 🕸️ Капкан / Ловушка (Контроллер)
        private static bool[,] GetControllerMatrix()
        {
            var m = new bool[24, 24];
            // Дуга капкана и зубья
            for (int x = 4; x <= 20; x++)
            {
                m[x, 5] = true;
                m[x, 6] = true;
            }
            // Боковые стойки
            for (int y = 5; y <= 17; y++)
            {
                m[4, y] = true;
                m[5, y] = true;
                m[19, y] = true;
                m[20, y] = true;
            }
            // Зубья капкана
            for (int x = 6; x <= 18; x += 3)
            {
                m[x, 7] = true;
                m[x, 8] = true;
                m[x, 9] = true;
                m[x + 1, 8] = true;
            }
            return m;
        }
    }
}
