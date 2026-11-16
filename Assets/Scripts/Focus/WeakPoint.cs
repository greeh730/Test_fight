// Assets/Scripts/WeakPoint.cs
using System.Collections.Generic;
using UnityEngine;
using Fencing;

/// <summary>
/// Слабая точка на теле врага, появляющаяся в режиме фокуса.
/// Появляется ТОЛЬКО когда враг оглушён, и ровно в количестве 1 штуки на каждого оглушенного врага.
/// Является летальной (красная, пульсирует по альфе), позволяя нанести добивающий выпад.
/// Двигается вместе с врагом, будучи дочерним объектом его Transform.
/// При выходе врага из стана или гибели точка автоматически самоуничтожается.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class WeakPoint : MonoBehaviour
{
    /// <summary>
    /// Статический список всех активных слабых точек в сцене.
    /// </summary>
    public static readonly List<WeakPoint> ActiveWeakPoints = new List<WeakPoint>();

    [Header("Параметры")]
    [Tooltip("Является ли точка летальной (смертельной)")]
    public bool isLethal = true;

    [Tooltip("Радиус попадания в world units (~0.35f)")]
    public float hitRadius = 0.35f;

    [Tooltip("Ссылка на объект-владельца")]
    public GameObject ownerEnemy;

    [Header("Визуал")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Tooltip("Скорость пульсации альфы для летальных точек")]
    public float pulseSpeed = 4f;

    public float HitRadiusWorld => (hitRadius > 2f) ? (hitRadius / 50f) : hitRadius;

    private void Awake()
    {
        if (hitRadius > 2f)
        {
            hitRadius = hitRadius / 50f;
        }
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
    }

    private void OnEnable()
    {
        if (!ActiveWeakPoints.Contains(this))
        {
            ActiveWeakPoints.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveWeakPoints.Remove(this);
    }

    private void Update()
    {
        // В режиме игры: если враг погиб, исчез или больше не оглушен - точка немедленно удаляется
        if (Application.isPlaying && (ownerEnemy == null || !WeakPointSpawner.IsEnemyStunned(ownerEnemy)))
        {
            Destroy(gameObject);
            return;
        }

        // Lethal = красный + мигание (пульсация alpha через Mathf.PingPong в реальном времени)
        if (isLethal && spriteRenderer != null)
        {
            float alpha = Mathf.Lerp(0.35f, 1.0f, Mathf.PingPong(Time.unscaledTime * pulseSpeed, 1f));
            spriteRenderer.color = new Color(1f, 0.15f, 0.15f, alpha);
        }
    }

    /// <summary>
    /// Инициализация параметров слабой точки.
    /// </summary>
    public void Initialize(GameObject owner, bool lethal = true)
    {
        ownerEnemy = owner;
        isLethal = lethal;
        if (hitRadius > 2f)
        {
            hitRadius = hitRadius / 50f;
        }

        if (!ActiveWeakPoints.Contains(this))
        {
            ActiveWeakPoints.Add(this);
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = 105; // Рисуется поверх всех спрайтов сцены и поверх круга
            if (isLethal)
            {
                spriteRenderer.color = new Color(1f, 0.15f, 0.15f, 1f);
            }
            else
            {
                spriteRenderer.color = new Color(1f, 1f, 1f, 0.95f);
            }
        }
    }

    /// <summary>
    /// Уничтожает все активные слабые точки на сцене.
    /// </summary>
    public static void ClearAll()
    {
        for (int i = ActiveWeakPoints.Count - 1; i >= 0; i--)
        {
            if (ActiveWeakPoints[i] != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(ActiveWeakPoints[i].gameObject);
                }
                else
                {
                    DestroyImmediate(ActiveWeakPoints[i].gameObject);
                }
            }
        }
        ActiveWeakPoints.Clear();

#if UNITY_6000_0_OR_NEWER
        var remaining = FindObjectsByType<WeakPoint>(FindObjectsInactive.Include);
#else
        var remaining = FindObjectsOfType<WeakPoint>();
#endif
        foreach (var r in remaining)
        {
            if (r != null)
            {
                if (Application.isPlaying) Destroy(r.gameObject);
                else DestroyImmediate(r.gameObject);
            }
        }
    }
}
