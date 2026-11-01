// Assets/Scripts/WeakPointSpawner.cs
using System.Collections.Generic;
using UnityEngine;
using Fencing;

/// <summary>
/// Спавнер слабых точек на врагах при активации режима фокуса.
/// Точки появляются ТОЛЬКО когда противник оглушён (Stunned).
/// На каждого оглушенного противника появляется ровно ОДНА летальная слабая точка.
/// Если оглушение спадает, точка немедленно исчезает.
/// </summary>
public class WeakPointSpawner : MonoBehaviour
{
    [Header("Параметры радиуса и количества")]
    [Tooltip("Радиус поиска врагов в пикселях экрана (~300px -> 6.0 единиц в 2D)")]
    public float spawnRadius = 300f;

    [Tooltip("Минимальное количество точек на одного врага (фиксировано 1)")]
    public int minPointsPerEnemy = 1;

    [Tooltip("Максимальное количество точек на одного врага (фиксировано 1)")]
    public int maxPointsPerEnemy = 1;

    [Tooltip("Минимальное количество летальных точек")]
    public int minLethalCount = 1;

    [Tooltip("Максимальное количество летальных точек")]
    public int maxLethalCount = 1;

    [Header("Слой врагов")]
    [Tooltip("Слой для фильтрации врагов (Enemy)")]
    public LayerMask enemyLayer;

    [Header("Префаб / Спрайт точки")]
    [Tooltip("Префаб слабой точки (опционально)")]
    [SerializeField] private GameObject weakPointPrefab;

    [Tooltip("Спрайт креста для слабой точки")]
    [SerializeField] private Sprite crossSprite;

    /// <summary>
    /// Конвертирует spawnRadius в мировые единицы (300px -> 6.0 world units, если масштаб камеры 6.0).
    /// </summary>
    public float SpawnRadiusWorld => (spawnRadius > 20f) ? (spawnRadius / 50f) : spawnRadius;

    private void Awake()
    {
        if (crossSprite == null)
        {
#if UNITY_EDITOR
            crossSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/CrossSprite.png");
#endif
        }
    }

    /// <summary>
    /// Проверяет, оглушён ли данный враг в текущий момент.
    /// </summary>
    public static bool IsEnemyStunned(GameObject obj)
    {
        if (obj == null) return false;

        // 1. Проверка через интерфейс боевой сущности
        var combatEntity = obj.GetComponentInParent<Combat.Common.ICombatEntity2D>();
        if (combatEntity != null)
        {
            return !combatEntity.IsDead && combatEntity.IsStunned;
        }

        // 2. Проверка через контроллер врага
        var enemyAI = obj.GetComponentInParent<Combat.EnemyAIController2D>();
        if (enemyAI != null)
        {
            return enemyAI.CurrentState == Combat.EnemyState.Stunned;
        }

        // 3. Проверка через тренировочный манекен
        var dummy = obj.GetComponentInParent<Combat.CombatDummy2D>();
        if (dummy != null)
        {
            return !dummy.IsDead && dummy.IsStunned;
        }

        return false;
    }

    /// <summary>
    /// Проверяет, является ли объект валидной целью-врагом (не игрок, не окружение, не триггер, ЖИВОЙ и ОГЛУШЁН).
    /// </summary>
    public static bool IsValidEnemyTarget(GameObject obj, out Collider2D mainCollider)
    {
        mainCollider = null;
        if (obj == null) return false;

        // Игрок и его части строго исключаются
        if (obj.CompareTag("Player") || obj.transform.root.CompareTag("Player")) return false;

        // 1. Проверка через интерфейс боевой сущности
        var combatEntity = obj.GetComponentInParent<Combat.Common.ICombatEntity2D>();
        if (combatEntity != null)
        {
            if (combatEntity.IsDead || !combatEntity.IsStunned) return false;
            mainCollider = combatEntity.gameObject.GetComponent<Collider2D>();
            return mainCollider != null && !mainCollider.isTrigger;
        }

        // 2. Проверка через контроллер врага
        var enemyAI = obj.GetComponentInParent<Combat.EnemyAIController2D>();
        if (enemyAI != null)
        {
            if (enemyAI.CurrentState != Combat.EnemyState.Stunned) return false;
            mainCollider = enemyAI.gameObject.GetComponent<Collider2D>();
            return mainCollider != null && !mainCollider.isTrigger;
        }

        // 3. Проверка через тренировочный манекен
        var dummy = obj.GetComponentInParent<Combat.CombatDummy2D>();
        if (dummy != null)
        {
            if (dummy.IsDead || !dummy.IsStunned) return false;
            mainCollider = dummy.gameObject.GetComponent<Collider2D>();
            return mainCollider != null && !mainCollider.isTrigger;
        }

        return false;
    }

    /// <summary>
    /// Спавнит слабые точки строго на ОГЛУШЕННЫХ врагах в радиусе spawnRadius вокруг заданной точки.
    /// </summary>
    public void SpawnWeakPoints(Vector3 origin)
    {
        ClearWeakPoints();
        Physics2D.SyncTransforms();

        float radiusWorld = SpawnRadiusWorld;

        // 1. Поиск всех коллайдеров в радиусе
        Collider2D[] colliders = Physics2D.OverlapCircleAll(origin, radiusWorld);
        var uniqueEnemies = new Dictionary<GameObject, Collider2D>();

        foreach (var col in colliders)
        {
            if (col == null || col.isTrigger) continue;

            if (IsValidEnemyTarget(col.gameObject, out Collider2D mainCol))
            {
                var enemyRoot = mainCol.gameObject;
                if (!uniqueEnemies.ContainsKey(enemyRoot))
                {
                    uniqueEnemies[enemyRoot] = mainCol;
                }
            }
        }

        // Запасной поиск всех объектов с тегом Enemy в радиусе (на случай специфической коллизии)
        if (uniqueEnemies.Count == 0)
        {
            var taggedEnemies = GameObject.FindGameObjectsWithTag("Enemy");
            foreach (var go in taggedEnemies)
            {
                if (go == null) continue;
                if (Vector2.Distance(go.transform.position, origin) <= radiusWorld)
                {
                    if (IsValidEnemyTarget(go, out Collider2D mainCol))
                    {
                        var enemyRoot = mainCol.gameObject;
                        if (!uniqueEnemies.ContainsKey(enemyRoot))
                        {
                            uniqueEnemies[enemyRoot] = mainCol;
                        }
                    }
                }
            }
        }

        // 2. Для каждого найденного оглушенного врага генерируем ровно 1 слабую точку
        foreach (var kvp in uniqueEnemies)
        {
            SpawnPointsOnEnemy(kvp.Key, kvp.Value.bounds);
        }

        Debug.Log($"<color=#00FFAA><b>[WeakPointSpawner]</b></color> Спавн слабых точек завершен. Найдено оглушенных врагов: <b>{uniqueEnemies.Count}</b>.");
    }

    /// <summary>
    /// Динамическое обновление слабых точек в процессе нахождения в фокусе:
    /// - Удаляет точки тех врагов, чей стан закончился или кто погиб.
    /// - Спавнит ровно 1 точку на вновь оглушенных врагах в радиусе.
    /// </summary>
    public void UpdateActiveWeakPoints(Vector3 origin)
    {
        // 1. Очистка точек, чьи враги вышли из стана или погибли
        for (int i = WeakPoint.ActiveWeakPoints.Count - 1; i >= 0; i--)
        {
            var wp = WeakPoint.ActiveWeakPoints[i];
            if (wp == null || wp.ownerEnemy == null || !IsEnemyStunned(wp.ownerEnemy))
            {
                if (wp != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(wp.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(wp.gameObject);
                    }
                }
            }
        }

        // 2. Поиск оглушенных врагов в радиусе
        float radiusWorld = SpawnRadiusWorld;
        Collider2D[] colliders = Physics2D.OverlapCircleAll(origin, radiusWorld);
        var uniqueEnemies = new Dictionary<GameObject, Collider2D>();

        foreach (var col in colliders)
        {
            if (col == null || col.isTrigger) continue;

            if (IsValidEnemyTarget(col.gameObject, out Collider2D mainCol))
            {
                var enemyRoot = mainCol.gameObject;
                if (!uniqueEnemies.ContainsKey(enemyRoot))
                {
                    uniqueEnemies[enemyRoot] = mainCol;
                }
            }
        }

        // Для каждого оглушенного врага проверяем, создана ли уже точка
        foreach (var kvp in uniqueEnemies)
        {
            bool hasPoint = false;
            for (int i = 0; i < WeakPoint.ActiveWeakPoints.Count; i++)
            {
                var wp = WeakPoint.ActiveWeakPoints[i];
                if (wp != null && wp.ownerEnemy == kvp.Key)
                {
                    hasPoint = true;
                    break;
                }
            }

            if (!hasPoint)
            {
                SpawnPointsOnEnemy(kvp.Key, kvp.Value.bounds);
            }
        }
    }

    /// <summary>
    /// Генерирует ровно ОДНУ летальную слабую точку в пределах коллайдера конкретного врага.
    /// </summary>
    private void SpawnPointsOnEnemy(GameObject enemy, Bounds bounds)
    {
        if (enemy == null) return;

        // Проверяем, нет ли уже точки у этого врага
        for (int i = 0; i < WeakPoint.ActiveWeakPoints.Count; i++)
        {
            var existing = WeakPoint.ActiveWeakPoints[i];
            if (existing != null && existing.ownerEnemy == enemy)
            {
                return;
            }
        }

        // Ровно одна точка, расположенная в верхней части груди врага
        Vector3 worldPos = new Vector3(bounds.center.x, bounds.center.y + bounds.extents.y * 0.15f, -1f);

        GameObject ptObj = CreateWeakPointInstance(worldPos, enemy.transform);
        var wp = ptObj.GetComponent<WeakPoint>();
        wp.Initialize(enemy, true); // Всегда летальная точка
    }

    private GameObject CreateWeakPointInstance(Vector3 worldPos, Transform parentEnemy)
    {
        GameObject obj;
        if (weakPointPrefab != null)
        {
            obj = Instantiate(weakPointPrefab, worldPos, Quaternion.identity, parentEnemy);
        }
        else
        {
            obj = new GameObject("WeakPoint", typeof(SpriteRenderer), typeof(WeakPoint));
            obj.transform.position = worldPos;
            obj.transform.SetParent(parentEnemy, true);

            var sr = obj.GetComponent<SpriteRenderer>();
            sr.sprite = crossSprite;
            sr.sortingOrder = 105;
        }

        // Размер креста: компактный крестик на теле (~0.35 единицы)
        obj.transform.localScale = Vector3.one * 0.35f;
        return obj;
    }

    /// <summary>
    /// Очищает все слабые точки в сцене.
    /// </summary>
    public void ClearWeakPoints()
    {
        WeakPoint.ClearAll();
    }
}
