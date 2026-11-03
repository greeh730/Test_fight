// Assets/Scripts/Magic/DenseFogUI.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Magic
{
    /// <summary>
    /// Полноэкранный эффект густого дыма на UI при ошибке ввода заклинания:
    /// - Полностью перекрывает обзор игроку (100% перекрытие экрана).
    /// - Собран из множества покачивающихся белых геометрических прямоугольников.
    /// - Враги во время действия дыма остаются в панике и бьют наугад.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasGroup))]
    [DisallowMultipleComponent]
    public class DenseFogUI : MonoBehaviour
    {
        public static bool IsDenseFogActive { get; private set; } = false;

        [Header("--- Параметры дыма на UI ---")]
        [Tooltip("Общая длительность эффекта дыма в секундах")]
        [SerializeField] private float duration = 6.0f;

        [Tooltip("Время быстрого заполнения экрана дымом")]
        [SerializeField] private float fadeInDuration = 0.25f;

        [Tooltip("Время плавного рассеивания дыма")]
        [SerializeField] private float fadeOutDuration = 0.85f;

        [Tooltip("Количество дополнительных анимированных прямоугольников")]
        [SerializeField] private int floatingRectCount = 50;

        [Tooltip("Спрайт прямоугольника")]
        [SerializeField] private Sprite rectSprite;

        private CanvasGroup _canvasGroup;
        private readonly List<RectTransform> _rects = new List<RectTransform>();
        private Vector2[] _basePositions;
        private float[] _baseRotations;
        private float[] _speedMultipliers;
        private float _seed;

        private void OnEnable()
        {
            IsDenseFogActive = true;
        }

        private void OnDisable()
        {
            IsDenseFogActive = false;
        }

        private void OnDestroy()
        {
            IsDenseFogActive = false;
        }

        private void Awake()
        {
            _seed = UnityEngine.Random.Range(0f, 100f);
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            EnsureCanvasParent();
            EnsureRectSprite();
            BuildWhiteRectangles();
        }

        private void Start()
        {
            if (Application.isPlaying)
            {
                StartCoroutine(DenseFogRoutine());
            }
        }

        private void Update()
        {
            if (_rects.Count == 0 || _basePositions == null) return;

            float time = Time.time;
            for (int i = 0; i < _rects.Count; i++)
            {
                if (_rects[i] == null || i >= _basePositions.Length) continue;

                float speed = _speedMultipliers != null && i < _speedMultipliers.Length ? _speedMultipliers[i] : 1.0f;
                float ox = Mathf.Sin((time * speed * 1.2f) + (i * 1.5f) + _seed) * 14f;
                float oy = Mathf.Cos((time * speed * 0.95f) + (i * 2.2f) + _seed) * 11f;
                _rects[i].anchoredPosition = _basePositions[i] + new Vector2(ox, oy);

                if (_baseRotations != null && i < _baseRotations.Length)
                {
                    float rotTilt = Mathf.Sin((time * speed * 1.3f) + (i * 1.8f) + _seed) * 5.5f;
                    _rects[i].localRotation = Quaternion.Euler(0f, 0f, _baseRotations[i] + rotTilt);
                }
            }
        }

        private void EnsureCanvasParent()
        {
            if (transform.parent == null)
            {
                var canvas = FindAnyObjectByType<Canvas>();
                if (canvas != null)
                {
                    transform.SetParent(canvas.transform, false);
                }
            }
            transform.SetAsLastSibling();

            var rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void EnsureRectSprite()
        {
            if (rectSprite == null)
            {
#if UNITY_EDITOR
                rectSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Magic/Rect_Primitive.png");
#endif
                if (rectSprite == null)
                {
                    rectSprite = Resources.Load<Sprite>("Sprites/Magic/Rect_Primitive");
                }
            }
        }

        /// <summary>
        /// Формирует экран из множества белых прямоугольников, полностью скрывающих игру.
        /// </summary>
        private void BuildWhiteRectangles()
        {
            if (_rects.Count > 0) return;

            // 1. Базовый сплошной фоновый слой крупных прямоугольников (гарантия 100% непрозрачности)
            int gridRows = 4;
            int gridCols = 5;
            for (int r = 0; r < gridRows; r++)
            {
                for (int c = 0; c < gridCols; c++)
                {
                    GameObject bgObj = new GameObject($"BaseRect_{r}_{c}", typeof(RectTransform), typeof(Image));
                    bgObj.transform.SetParent(transform, false);

                    var bgRt = bgObj.GetComponent<RectTransform>();
                    // С перекрытием соседних клеток
                    float normX = (c + 0.5f) / gridCols;
                    float normY = (r + 0.5f) / gridRows;
                    bgRt.anchorMin = new Vector2(normX, normY);
                    bgRt.anchorMax = new Vector2(normX, normY);
                    bgRt.sizeDelta = new Vector2(460f, 360f);
                    bgRt.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-8f, 8f));

                    var bgImg = bgObj.GetComponent<Image>();
                    bgImg.sprite = rectSprite;
                    bgImg.color = Color.white;
                    bgImg.raycastTarget = false;

                    _rects.Add(bgRt);
                }
            }

            // 2. Слой разнообразных плавающих белых прямоугольников разного размера поверх базы
            Color[] mistWhiteShades = new Color[]
            {
                Color.white,
                new Color(0.96f, 0.97f, 1.0f, 1f),
                new Color(0.93f, 0.95f, 0.98f, 1f),
                new Color(0.89f, 0.92f, 0.96f, 1f)
            };

            for (int i = 0; i < floatingRectCount; i++)
            {
                GameObject rectObj = new GameObject($"FloatRect_{i}", typeof(RectTransform), typeof(Image));
                rectObj.transform.SetParent(transform, false);

                var rt = rectObj.GetComponent<RectTransform>();
                // Распределяем случайным образом по экрану
                float randNormX = UnityEngine.Random.Range(0.05f, 0.95f);
                float randNormY = UnityEngine.Random.Range(0.05f, 0.95f);
                rt.anchorMin = new Vector2(randNormX, randNormY);
                rt.anchorMax = new Vector2(randNormX, randNormY);

                float width = UnityEngine.Random.Range(160f, 440f);
                float height = UnityEngine.Random.Range(90f, 280f);
                rt.sizeDelta = new Vector2(width, height);

                float rotZ = UnityEngine.Random.Range(-35f, 35f);
                rt.localRotation = Quaternion.Euler(0f, 0f, rotZ);

                var img = rectObj.GetComponent<Image>();
                img.sprite = rectSprite;
                img.color = mistWhiteShades[i % mistWhiteShades.Length];
                img.raycastTarget = false;

                _rects.Add(rt);
            }

            int count = _rects.Count;
            _basePositions = new Vector2[count];
            _baseRotations = new float[count];
            _speedMultipliers = new float[count];

            for (int i = 0; i < count; i++)
            {
                _basePositions[i] = _rects[i].anchoredPosition;
                _baseRotations[i] = _rects[i].localEulerAngles.z;
                _speedMultipliers[i] = UnityEngine.Random.Range(0.7f, 1.35f);
            }
        }

        public void Initialize(float customDuration)
        {
            duration = Mathf.Max(1.0f, customDuration);
            IsDenseFogActive = true;

            EnsureCanvasParent();
            EnsureRectSprite();
            BuildWhiteRectangles();

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
            }

            if (Application.isPlaying)
            {
                StopAllCoroutines();
                StartCoroutine(DenseFogRoutine());
            }
        }

        private IEnumerator DenseFogRoutine()
        {
            IsDenseFogActive = true;

            // 1. Быстрое заполнение экрана белыми прямоугольниками (Fade-in за 0.25 сек)
            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / fadeInDuration);
                float ease = Mathf.SmoothStep(0f, 1f, progress);

                if (_canvasGroup != null) _canvasGroup.alpha = ease;
                yield return null;
            }

            if (_canvasGroup != null) _canvasGroup.alpha = 1.0f;

            // 2. Удержание 100% перекрытия экрана
            float lingerDuration = Mathf.Max(0.1f, duration - fadeInDuration - fadeOutDuration);
            yield return new WaitForSeconds(lingerDuration);

            // 3. Плавное рассеивание (Fade-out за 0.85 сек)
            elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / fadeOutDuration);
                float ease = Mathf.SmoothStep(0f, 1f, progress);

                if (_canvasGroup != null) _canvasGroup.alpha = 1f - ease;

                if (progress >= 0.7f)
                {
                    IsDenseFogActive = false;
                }

                yield return null;
            }

            IsDenseFogActive = false;
            Destroy(gameObject);
        }

        /// <summary>
        /// Создает и запускает полноэкранный UI дым из белых прямоугольников.
        /// </summary>
        public static DenseFogUI Spawn(float duration = 6.0f)
        {
            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[DenseFogUI] Canvas не найден в сцене!");
                return null;
            }

            GameObject go = new GameObject("DenseFogUI_Screen", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(canvas.transform, false);

            var denseUI = go.AddComponent<DenseFogUI>();
            if (Combat.Audio.SoundManager.Instance != null)
            {
                Combat.Audio.SoundManager.Instance.PlaySmoke(1.15f);
            }
            denseUI.Initialize(duration);
            return denseUI;
        }
    }
}
