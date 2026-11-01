// Assets/Scripts/StrikeTrajectory.cs
using System.Collections;
using UnityEngine;

/// <summary>
/// Предпросмотр и визуализация траектории разреза в режиме фокуса.
/// До нажатия ЛКМ динамически показывает белую линию от края спрайта игрока в сторону, противоположную натяжению.
/// После нажатия ЛКМ фиксирует направление (перестаёт реагировать на мышь), анимирует вытягивание разреза
/// и короткую вспышку в момент разрешения удара.
/// </summary>
public class StrikeTrajectory : MonoBehaviour
{
    [Header("Привязка и размеры")]
    [Tooltip("Transform игрока — центр отсчёта")]
    public Transform playerAnchor;

    [Tooltip("Длина линии в world units при максимальном натяжении")]
    public float lineLength = 6f;

    [Tooltip("Минимальная длина линии при нулевом натяжении")]
    public float minLineLength = 1.5f;

    [Tooltip("Отступ начала линии от центра игрока в сторону удара, чтобы не перекрывалась спрайтом")]
    public float radPlayer = 0.4f;

    private LineRenderer lineRend;
    private CircleCharge circle;
    private float _circleSearchTimer = 0f;

    private Vector2 currentStrikeDir = Vector2.right;
    private bool isLocked = false;
    private bool isFlashing = false;
    private Vector3 lockedOrigin;
    private Vector2 lockedDir = Vector2.right;
    private float lockedRange = 6f;
    private Coroutine flashRoutine;

    public LineRenderer LineRend => lineRend;
    public bool IsLocked => isLocked;
    public bool IsFlashing => isFlashing;
    public Vector2 CurrentStrikeDir => currentStrikeDir;

    /// <summary>
    /// Задаёт ссылку на активный компонент CircleCharge для чтения натяжения.
    /// </summary>
    public void SetCircle(CircleCharge targetCircle)
    {
        circle = targetCircle;
    }

    private void Awake()
    {
        lineRend = GetComponent<LineRenderer>();
        if (lineRend == null)
        {
            lineRend = gameObject.AddComponent<LineRenderer>();
        }

        lineRend.positionCount = 2;
        lineRend.useWorldSpace = true;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        }
        if (shader != null)
        {
            Material mat = new Material(shader);
            mat.mainTexture = Texture2D.whiteTexture;
            lineRend.material = mat;
        }

        lineRend.startWidth = 0.05f;
        lineRend.endWidth = 0.15f;                // линия сужается к игроку, толще к концу — как след от удара
        lineRend.numCapVertices = 4;              // сглаженные концы
        lineRend.sortingLayerName = "Default";
        lineRend.sortingOrder = 40;               // выше основного круга (30), но ниже точек (если точки на 50)
        lineRend.startColor = Color.white;
        lineRend.endColor = new Color(1f, 1f, 1f, 0.7f);
        lineRend.enabled = false;
    }

    /// <summary>
    /// Возвращает текущее направление удара (противоположное натяжению).
    /// Если удар уже зафиксирован после ЛКМ — возвращает зафиксированное направление, игнорируя мышь.
    /// </summary>
    public Vector2 GetStrikeDirection()
    {
        if (isLocked && lockedDir.sqrMagnitude > 0.0001f)
        {
            return lockedDir;
        }

        if (TryComputePullDirection(out Vector2 pullDir, out _))
        {
            if (pullDir.sqrMagnitude > 0.0001f)
            {
                currentStrikeDir = -pullDir.normalized;
            }
        }

        if (currentStrikeDir.sqrMagnitude < 0.0001f)
        {
            currentStrikeDir = Vector2.right;
        }

        return currentStrikeDir;
    }

    /// <summary>
    /// Фиксирует направление и точку старта удара в момент нажатия ЛКМ.
    /// С этого момента курсор мыши больше не влияет на линию.
    /// </summary>
    public void LockStrike(Vector3 origin, Vector2 dir, float range)
    {
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        isLocked = true;
        isFlashing = false;
        lockedOrigin = new Vector3(origin.x, origin.y, -1f);
        lockedDir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
        lockedRange = range;
        currentStrikeDir = lockedDir;

        if (lineRend != null)
        {
            lineRend.enabled = true;
            lineRend.positionCount = 2;
            lineRend.SetPosition(0, lockedOrigin);
            lineRend.SetPosition(1, lockedOrigin);
            lineRend.startWidth = 0.06f;
            lineRend.endWidth = 0.14f;
            lineRend.startColor = Color.white;
            lineRend.endColor = Color.white;
        }
    }

    /// <summary>
    /// Обновляет прогресс вытягивания «жёсткой» белой линии разреза (0..1) за время strikeDuration.
    /// </summary>
    public void UpdateStrikeProgress(Vector3 origin, Vector2 dir, float range, float progress01)
    {
        isLocked = true;
        lockedOrigin = new Vector3(origin.x, origin.y, -1f);
        lockedDir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
        lockedRange = range;
        currentStrikeDir = lockedDir;

        if (lineRend == null) return;

        float clampedProgress = Mathf.Clamp01(progress01);
        Vector3 endPos = lockedOrigin + (Vector3)(lockedDir * (range * clampedProgress));
        endPos.z = -1f;

        lineRend.enabled = true;
        lineRend.positionCount = 2;
        lineRend.SetPosition(0, lockedOrigin);
        lineRend.SetPosition(1, endPos);

        // Жёсткая белая линия: alpha 1, цвет чисто белый
        lineRend.startWidth = 0.06f;
        lineRend.endWidth = 0.14f;
        lineRend.startColor = Color.white;
        lineRend.endColor = Color.white;
    }

    /// <summary>
    /// Проигрывает короткую вспышку (scale/alpha за 0.1 сек) в момент разрешения удара,
    /// после чего скрывает линию и уничтожает объект (или возвращает в режим предпросмотра).
    /// </summary>
    public void PlayResolutionFlash(float duration = 0.1f, bool destroyAfter = true)
    {
        if (lineRend == null)
        {
            if (destroyAfter)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
            }
            return;
        }

        if (!gameObject.activeInHierarchy)
        {
            lineRend.enabled = false;
            if (destroyAfter)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
            }
            return;
        }

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
        }
        flashRoutine = StartCoroutine(FlashRoutine(Mathf.Max(0.01f, duration), destroyAfter));
    }

    private IEnumerator FlashRoutine(float duration, bool destroyAfter)
    {
        isLocked = true;
        isFlashing = true;

        Vector3 startPos = lockedOrigin;
        Vector3 endPos = lockedOrigin + (Vector3)(lockedDir * lockedRange);
        startPos.z = -1f;
        endPos.z = -1f;

        lineRend.enabled = true;
        lineRend.positionCount = 2;
        lineRend.SetPosition(0, startPos);
        lineRend.SetPosition(1, endPos);

        float initialStartWidth = 0.12f;
        float initialEndWidth = 0.24f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (lineRend != null)
            {
                float alpha = Mathf.Lerp(1f, 0f, t);
                lineRend.startWidth = Mathf.Lerp(initialStartWidth, 0.01f, t);
                lineRend.endWidth = Mathf.Lerp(initialEndWidth, 0.02f, t);
                Color c = new Color(1f, 1f, 1f, alpha);
                lineRend.startColor = c;
                lineRend.endColor = c;
            }

            yield return null;
        }

        if (lineRend != null)
        {
            lineRend.enabled = false;
        }

        isFlashing = false;
        flashRoutine = null;

        if (destroyAfter)
        {
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }
        else
        {
            isLocked = false;
        }
    }

    private bool TryComputePullDirection(out Vector2 pullDir, out Vector3 anchor)
    {
        pullDir = Vector2.zero;
        anchor = Vector3.zero;

        if (playerAnchor == null)
        {
            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                playerAnchor = playerObj.transform;
            }
        }

        if (playerAnchor == null) return false;

        Camera cam = Camera.main;
        if (cam == null) return false;

        Vector3 mouseScreen = FocusManager.ReadMousePosition();
        if (mouseScreen == Vector3.zero)
        {
            try { mouseScreen = Input.mousePosition; } catch {}
        }

        Vector3 mouseWorld = FocusManager.mockMouseWorldPosition.HasValue
            ? FocusManager.mockMouseWorldPosition.Value
            : cam.ScreenToWorldPoint(mouseScreen);

        anchor = new Vector3(playerAnchor.position.x, playerAnchor.position.y, -1f);
        pullDir = (Vector2)mouseWorld - (Vector2)anchor;
        return true;
    }

    private void Update()
    {
        // После нажатия ЛКМ направление зафиксировано — на движение мыши не реагируем
        if (isLocked || isFlashing)
        {
            return;
        }

        bool rmbHeld = FocusManager.ReadRMB();
        if (!rmbHeld || (FocusManager.Instance != null && !FocusManager.Instance.isFocusActive))
        {
            if (lineRend != null)
            {
                lineRend.enabled = false;
            }
            return;
        }

        if (!TryComputePullDirection(out Vector2 pullDir, out Vector3 anchor))
        {
            if (lineRend != null)
            {
                lineRend.enabled = false;
            }
            return;
        }

        if (circle == null)
        {
            circle = CircleCharge.Instance;
            if (circle == null)
            {
                _circleSearchTimer -= Time.deltaTime;
                if (_circleSearchTimer <= 0f)
                {
                    _circleSearchTimer = 0.5f;
#if UNITY_6000_0_OR_NEWER
                    circle = FindAnyObjectByType<CircleCharge>();
#else
                    circle = FindObjectOfType<CircleCharge>();
#endif
                }
            }
        }

        // Если курсор слишком близко к центру игрока — линия не рисуется
        if (pullDir.magnitude < 0.05f)
        {
            if (lineRend != null)
            {
                lineRend.enabled = false;
            }
            return;
        }

        // Удар идёт ПРОТИВОПОЛОЖНО натяжению (как лук)
        Vector2 strikeDir = -pullDir.normalized;
        currentStrikeDir = strikeDir;

        float tension01 = circle != null ? circle.GetTension01() : 0f;
        float currentLength = Mathf.Lerp(minLineLength, lineLength, tension01);

        // Начинаем линию на расстоянии radPlayer = 0.4f от центра игрока в сторону strikeDir
        Vector3 lineStart = anchor + (Vector3)(strikeDir * radPlayer);
        Vector3 lineEnd = anchor + (Vector3)(strikeDir * Mathf.Max(currentLength, radPlayer + 0.1f));
        lineStart.z = -1f;
        lineEnd.z = -1f;

        lineRend.enabled = true;
        lineRend.SetPosition(0, lineStart);
        lineRend.SetPosition(1, lineEnd);

        // Визуал по натяжению:
        // Alpha линии: от 0.15 (призрачная) до 1.0 (яркая) + лёгкая пульсация при tension01 > 0.9
        float alpha = Mathf.Lerp(0.15f, 1f, tension01);
        if (tension01 > 0.9f)
        {
            alpha *= (0.9f + 0.1f * Mathf.Sin(Time.unscaledTime * 15f));
        }

        // Ширина: сужается у игрока (0.02..0.05) и расширяется к концу (0.03..0.12)
        float width = Mathf.Lerp(0.03f, 0.12f, tension01);
        lineRend.startWidth = Mathf.Lerp(0.02f, 0.05f, tension01);
        lineRend.endWidth = width;

        // Цвет: от слегка серо-голубого (0.8, 0.9, 1) к чисто белому
        Color baseColor = Color.Lerp(new Color(0.8f, 0.9f, 1f, 1f), Color.white, tension01);
        lineRend.startColor = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
        lineRend.endColor = new Color(baseColor.r, baseColor.g, baseColor.b, alpha * 0.7f);
    }
}
