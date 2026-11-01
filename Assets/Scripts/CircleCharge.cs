// Assets/Scripts/CircleCharge.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Круг замаха по центру персонажа в режиме фокуса.
/// Реагирует на натяжение курсором мыши, плавно увеличивая масштаб от 1 до 1.5
/// и интерполируя цвет от белого к ярко-красному.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class CircleCharge : MonoBehaviour
{
    [Header("Параметры натяжения")]
    [Tooltip("Transform игрока — точка привязки")]
    public Transform playerAnchor;

    [Tooltip("Максимум натяжения в мировых координатах")]
    public float maxStretchDistance = 2.5f;

    [Tooltip("Скорость возврата круга к игроку, если курсор близко")]
    public float returnSpeed = 8f;

    [Tooltip("Минимальный радиус в пикселях (~20-25px, пересчитывается в ~0.5 единиц в 2D)")]
    public float minRadius = 20f;

    [Tooltip("Текущее значение натяжения (0..1)")]
    [Range(0f, 1f)]
    public float currentPull = 0f;     // 0..1

    [Header("Линия натяжения (Призраки)")]
    [Tooltip("Максимум призрачных кругов")]
    public int maxGhostCount = 5;

    [Tooltip("Радиус ближнего к игроку призрака")]
    public float ghostStartRadius = 0.35f;

    [Tooltip("Радиус круга на максимуме натяжения")]
    public float ghostEndRadius = 0.08f;

    private List<SpriteRenderer> ghostCircles = new List<SpriteRenderer>();
    private Sprite ghostSprite;

    [Header("Визуал")]
    [Tooltip("Компонент SpriteRenderer круга")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Tooltip("Цвет при нулевом натяжении")]
    public Color startColor = Color.white;

    [Tooltip("Цвет при максимальном натяжении")]
    public Color fullPullColor = Color.red;

    private Vector3 currentTargetPos;
    private float baseScale = 1f;
    private float logTimer = 0f;

    [Header("Задержка проверки")]
    [Tooltip("Время иммунитета круга после спавна")]
    [SerializeField] private float rmbReleaseGracePeriod = 0.15f;
    private float timeSinceSpawn = 0f;

    public float RmbReleaseGracePeriod => rmbReleaseGracePeriod;
    public float TimeSinceSpawn => timeSinceSpawn;

    private float flashTimer = 0f;
    private float flashDuration = 0.1f;
    private bool isHidden = false;

    public bool IsVisible => !isHidden;

    public static CircleCharge Instance { get; private set; }
    private Camera _cachedCam;
    private float _anchorSearchTimer = 0f;

    private static Sprite cachedRingSprite;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private Camera GetCamera()
    {
        if (_cachedCam == null) _cachedCam = Camera.main;
        return _cachedCam;
    }

    /// <summary>
    /// Скрывает или показывает круг замаха и все призрачные круги (например, на время исполнения удара).
    /// </summary>
    public void SetVisible(bool visible)
    {
        isHidden = !visible;
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = visible;
        }
        if (ghostCircles != null)
        {
            for (int i = 0; i < ghostCircles.Count; i++)
            {
                if (ghostCircles[i] != null)
                {
                    ghostCircles[i].enabled = visible;
                }
            }
        }
    }

    /// <summary>
    /// Возвращает степень натяжения от 0 до 1 (0 = курсор у игрока, 1 = курсор на максимуме maxStretchDistance).
    /// </summary>
    public float GetTension01()
    {
        if (playerAnchor == null) FindPlayerAnchor();
        if (playerAnchor == null) return currentPull;

        Camera cam = GetCamera();
        if (cam == null) return currentPull;

        Vector3 mouseScreen = FocusManager.ReadMousePosition();
        if (mouseScreen == Vector3.zero)
        {
            try { mouseScreen = Input.mousePosition; } catch {}
        }

        Vector3 mouseWorld = FocusManager.mockMouseWorldPosition.HasValue
            ? FocusManager.mockMouseWorldPosition.Value
            : cam.ScreenToWorldPoint(mouseScreen);
        return Mathf.Clamp01(Vector2.Distance(mouseWorld, playerAnchor.position) / maxStretchDistance);
    }

    /// <summary>
    /// Короткая визуальная вспышка на круге замаха при переходе в боевую фазу.
    /// </summary>
    public void FlashOnCombatPhase(float duration = 0.1f)
    {
        flashDuration = Mathf.Max(0.01f, duration);
        flashTimer = flashDuration;
    }

    /// <summary>
    /// Генерирует процедурный спрайт кольца с гарантированной непрозрачностью и мягкими краями.
    /// </summary>
    public static Sprite GenerateRingSprite(int size = 128, float thickness = 8f, Color color = default)
    {
        if (cachedRingSprite != null) return cachedRingSprite;

        if (color == default) color = Color.white;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float outerR = size / 2f - 1f;
        float innerR = outerR - thickness;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                float a = 0f;
                if (d <= outerR && d >= innerR)
                    a = 1f;  // внутри кольца — непрозрачно
                else if (d < innerR && d > innerR - 1.5f)
                    a = 0.5f; // мягкий внутренний край
                else if (d > outerR && d < outerR + 1.5f)
                    a = 0.5f; // мягкий внешний край

                tex.SetPixel(x, y, new Color(color.r, color.g, color.b, a));
            }
        }

        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        cachedRingSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return cachedRingSprite;
    }

    private void Awake()
    {
        Instance = this;
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (spriteRenderer != null)
        {
            // Гарантированно яркий и непрозрачный процедурный спрайт кольца
            spriteRenderer.sprite = GenerateRingSprite();
            spriteRenderer.color = Color.white;
            spriteRenderer.sortingOrder = 100; // Поверх всех спрайтов (>= 100)
            startColor = Color.white;

            // Настройка материала: Unlit-шейдер для независимости от глобального/точечного 2D-освещения
            var unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (unlitShader != null)
            {
                spriteRenderer.sharedMaterial = new Material(unlitShader);
            }
            else
            {
                var defaultShader = Shader.Find("Sprites/Default");
                if (defaultShader != null)
                {
                    spriteRenderer.sharedMaterial = new Material(defaultShader);
                }
            }
        }

        // Базовый масштаб круга в мировых координатах (~0.5f радиус)
        float baseWorldRadius = (minRadius > 2f) ? (minRadius / 40f) : minRadius;
        baseScale = baseWorldRadius * 2f;
        transform.localScale = Vector3.one * baseScale;

        FindPlayerAnchor();
        InitGhostPool();
    }

    private void OnDisable()
    {
        UpdateGhostCircles(0f);
    }

    /// <summary>
    /// Пересоздает пул призраков при изменении maxGhostCount в рантайме.
    /// </summary>
    public void RebuildGhostPool()
    {
        if (ghostCircles != null)
        {
            for (int i = 0; i < ghostCircles.Count; i++)
            {
                if (ghostCircles[i] != null)
                {
                    if (Application.isPlaying)
                        Destroy(ghostCircles[i].gameObject);
                    else
                        DestroyImmediate(ghostCircles[i].gameObject);
                }
            }
            ghostCircles.Clear();
        }
        InitGhostPool();
    }

    private void Start()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = 100; // Поверх всех спрайтов (>= 100)
            spriteRenderer.color = startColor;
        }

        if (playerAnchor == null) FindPlayerAnchor();
        if (playerAnchor != null)
        {
            transform.position = new Vector3(playerAnchor.position.x, playerAnchor.position.y, -1f);
            currentTargetPos = transform.position;
        }
    }

    private void FindPlayerAnchor()
    {
        if (playerAnchor == null)
        {
            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                playerAnchor = playerObj.transform;
            }
        }
    }

    private void Update()
    {
        timeSinceSpawn += Time.unscaledDeltaTime;

        if (isHidden)
        {
            return;
        }

        if (playerAnchor == null)
        {
            _anchorSearchTimer -= Time.deltaTime;
            if (_anchorSearchTimer <= 0f)
            {
                _anchorSearchTimer = 0.5f;
                FindPlayerAnchor();
            }
        }
        if (playerAnchor == null) return;

        Camera cam = GetCamera();
        if (cam == null) return;

        Vector3 mouseScreen = FocusManager.ReadMousePosition();
        if (mouseScreen == Vector3.zero)
        {
            try { mouseScreen = Input.mousePosition; } catch {}
        }

        Vector3 mouseWorld = FocusManager.mockMouseWorldPosition.HasValue
            ? FocusManager.mockMouseWorldPosition.Value
            : cam.ScreenToWorldPoint(mouseScreen);
        Vector3 anchor = new Vector3(playerAnchor.position.x, playerAnchor.position.y, -1f);
        Vector2 delta = (Vector2)mouseWorld - (Vector2)anchor;
        float distance = delta.magnitude;
        Vector2 direction = distance > 0.001f ? delta / distance : Vector2.right;

        // Позиция: смещение в сторону курсора не дальше maxStretchDistance
        float clampedDistance = Mathf.Min(distance, maxStretchDistance);
        currentTargetPos = anchor + (Vector3)(direction * clampedDistance);
        currentTargetPos.z = -1f;

        // Сглаживание позиции к цели
        transform.position = Vector3.Lerp(transform.position, currentTargetPos, 1f - Mathf.Exp(-returnSpeed * Time.unscaledDeltaTime));

        // Степень натяжения
        float tension01 = Mathf.Clamp01(distance / maxStretchDistance);
        currentPull = tension01;

        // Безопасность: если режим фокуса завершен после grace period — мгновенное удаление
        if (Application.isPlaying && timeSinceSpawn > rmbReleaseGracePeriod)
        {
            if (FocusManager.Instance != null && !FocusManager.Instance.isFocusActive)
            {
                Destroy(gameObject);
                return;
            }
        }

        // Визуал: масштаб немного увеличивается (до 1.15x) + пульсация при максимальном натяжении
        float targetScale = Mathf.Lerp(baseScale, baseScale * 1.15f, tension01);
        if (tension01 > 0.95f)
        {
            targetScale *= (1f + 0.05f * Mathf.Sin(Time.unscaledTime * 20f));
        }
        // Визуал: цвет от белого к красному с небольшой прозрачностью на максимуме (0.9f)
        Color c = Color.Lerp(Color.white, Color.red, tension01);
        c.a = Mathf.Lerp(1.0f, 0.9f, tension01);

        if (flashTimer > 0f)
        {
            flashTimer -= Time.unscaledDeltaTime;
            float flashT = Mathf.Clamp01(flashTimer / flashDuration);
            transform.localScale = Vector3.one * (targetScale * (1f + 0.35f * flashT));
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.Lerp(c, Color.cyan, flashT);
            }
        }
        else
        {
            transform.localScale = Vector3.one * targetScale;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = c;
            }
        }

        // Логирование раз в 0.5 сек
        logTimer += Time.unscaledDeltaTime;
        if (logTimer >= 0.5f)
        {
            logTimer = 0f;
            Debug.Log($"Pull: {currentPull:F2}, dist: {distance:F2}, mouseWorld: {mouseWorld}, circlePos: {transform.position}");
        }

        // Визуализация линии натяжения призрачными кругами
        UpdateGhostCircles(tension01);
    }

    /// <summary>
    /// Инициализация пула призрачных кругов для линии натяжения.
    /// </summary>
    private void InitGhostPool()
    {
        if (ghostCircles != null && ghostCircles.Count >= maxGhostCount) return;
        if (ghostCircles == null) ghostCircles = new List<SpriteRenderer>();
        ghostCircles.Clear();

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (spriteRenderer != null && spriteRenderer.sortingOrder == 0)
        {
            spriteRenderer.sortingOrder = 100;
        }

        if (ghostSprite == null)
        {
            ghostSprite = (spriteRenderer != null && spriteRenderer.sprite != null)
                ? spriteRenderer.sprite
                : GenerateRingSprite();
        }

        for (int i = 0; i < maxGhostCount; i++)
        {
            Transform child = transform.Find($"Ghost_{i}");
            GameObject ghost = (child != null) ? child.gameObject : new GameObject($"Ghost_{i}");
            if (child == null)
            {
                ghost.transform.SetParent(transform, false);
            }

            SpriteRenderer gsr = ghost.GetComponent<SpriteRenderer>();
            if (gsr == null) gsr = ghost.AddComponent<SpriteRenderer>();

            gsr.sprite = ghostSprite;
            if (spriteRenderer != null)
            {
                gsr.sharedMaterial = spriteRenderer.sharedMaterial;
                gsr.sortingLayerName = spriteRenderer.sortingLayerName;
                gsr.sortingOrder = spriteRenderer.sortingOrder - 1; // на слой ниже основного круга
            }
            else
            {
                gsr.sortingOrder = 99;
            }
            gsr.color = new Color(1f, 1f, 1f, 0f); // изначально скрыт
            ghostCircles.Add(gsr);
        }
    }

    /// <summary>
    /// Визуализация линии натяжения призрачными кругами между игроком и основным кругом.
    /// Чем больше натяжение — тем больше кругов (до maxGhostCount), но тем меньше их размер.
    /// </summary>
    public void UpdateGhostCircles(float tension01)
    {
        if (ghostCircles == null || ghostCircles.Count == 0)
        {
            InitGhostPool();
        }
        if (ghostCircles == null || ghostCircles.Count == 0) return;

        // Если tension01 < 0.05 или нет привязки к игроку — все призраки скрываются
        if (tension01 < 0.05f || playerAnchor == null)
        {
            for (int i = 0; i < ghostCircles.Count; i++)
            {
                if (ghostCircles[i] != null)
                {
                    Color c = ghostCircles[i].color;
                    c.a = 0f;
                    ghostCircles[i].color = c;
                }
            }
            return;
        }

        // Количество активных призраков = Mathf.CeilToInt(tension01 * maxGhostCount)
        int activeCount = Mathf.Clamp(Mathf.CeilToInt(tension01 * maxGhostCount), 1, ghostCircles.Count);

        // Призраки при tension01 > 0.8 получают мягкий красный оттенок, иначе белые
        Color ghostBaseColor = Color.white;
        if (tension01 > 0.8f)
        {
            ghostBaseColor = Color.Lerp(Color.white, new Color(1f, 0.3f, 0.3f), (tension01 - 0.8f) / 0.2f);
        }

        float baseAlpha = Mathf.Lerp(0.5f, 1.0f, tension01);

        for (int i = 0; i < activeCount; i++)
        {
            if (ghostCircles[i] == null) continue;

            // Равномерное распределение по линии от игрока к основному кругу
            float t = (i + 1f) / (activeCount + 1f);
            Vector3 ghostPos = Vector3.Lerp(playerAnchor.position, transform.position, t);
            ghostPos.z = transform.position.z - 0.01f; // чуть-чуть впереди, но не конфликтует по Z с основным
            ghostCircles[i].transform.position = ghostPos;

            // Радиус каждого призрака линейно уменьшается с tension01
            float radius = Mathf.Lerp(ghostStartRadius, ghostEndRadius, tension01);
            ghostCircles[i].transform.localScale = Vector3.one * radius;

            // Дальний призрак (ближе к основному кругу) всегда ярче ближнего
            float alpha = Mathf.Lerp(0.2f, 0.8f, (i + 1f) / activeCount) * baseAlpha;
            Color ghostColor = ghostBaseColor;
            ghostColor.a = alpha;
            ghostCircles[i].color = ghostColor;
        }

        // Неактивные призраки (i >= activeCount) скрываются
        for (int i = activeCount; i < ghostCircles.Count; i++)
        {
            if (ghostCircles[i] != null)
            {
                Color c = ghostCircles[i].color;
                c.a = 0f;
                ghostCircles[i].color = c;
            }
        }
    }
}
