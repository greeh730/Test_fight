// Assets/Scripts/Fencing/SwordHitbox.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Combat;
using Combat.Roguelike;

namespace Fencing
{
    using CombatZone = Combat.CombatZone;
    using AttackConfig = Combat.AttackConfig;
    using IHurtboxTarget2D = Combat.IHurtboxTarget2D;
    using CombatHurtbox2D = Combat.CombatHurtbox2D;

    /// <summary>
    /// Прямоугольный хитбокс фехтовальной атаки.
    /// Реализует 3 изолированных уровня атак (Голова, Тело, Ноги) слева и справа.
    /// Зазоры между зонами исключают случайное поражение соседних целей при ударе в центр.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SwordHitbox : MonoBehaviour
    {
        [Header("Геометрия зон атак")]
        [Tooltip("Горизонтальный вылет удара от центра игрока")]
        [SerializeField] private float horizontalReach = 1.35f;

        [Tooltip("Точка начала хитбокса у границы модельки игрока")]
        [SerializeField] private float playerBodyEdge = 0.45f;

        [Tooltip("Высота удара в голову (UpLeft / UpRight)")]
        [SerializeField] private float headHeight = 0.60f;

        [Tooltip("Высота удара в тело (Left / Right)")]
        [SerializeField] private float bodyHeight = 0.0f;

        [Tooltip("Высота удара по ногам (DownLeft / DownRight)")]
        [SerializeField] private float legsHeight = -0.60f;

        [Tooltip("Размер прямоугольника хитбокса (ширина, высота). Высота подобрана так, чтобы не задевать соседние уровни.")]
        [SerializeField] private Vector2 boxSize = new Vector2(1.50f, 0.38f);

        [Header("Визуал атаки")]
        [Tooltip("Длительность видимости прямоугольника атаки")]
        [SerializeField] private float duration = 0.18f;

        [Tooltip("Цвет вспышки прямоугольника")]
        [SerializeField] private Color slashColor = new Color(0.2f, 0.85f, 1f, 0.85f);

        [Header("Боевые параметры")]
        [Tooltip("Базовый урон удара мечом")]
        [SerializeField] private float damage = 25.0f;

        [Tooltip("Сила отталкивания цели (X, Y)")]
        [SerializeField] private Vector2 knockbackForce = new Vector2(6.5f, 2.0f);

        public float HorizontalReach => horizontalReach;
        public float PlayerBodyEdge => playerBodyEdge;
        public float HeadHeight => headHeight;
        public float BodyHeight => bodyHeight;
        public float LegsHeight => legsHeight;
        public Vector2 BoxSize => boxSize;

        public float Damage
        {
            get => damage;
            set => damage = Mathf.Max(0f, value);
        }

        public Vector2 KnockbackForce
        {
            get => knockbackForce;
            set => knockbackForce = value;
        }

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh boxMesh;
        private Material boxMaterial;
        private Coroutine activeSlashRoutine;

        private void Awake()
        {
            if (boxSize.x < 1.4f)
            {
                boxSize = new Vector2(1.50f, 0.38f);
            }

            EnsureInitialized();
        }

        private void OnValidate()
        {
            if (boxSize.x < 1.4f)
            {
                boxSize = new Vector2(1.50f, 0.38f);
            }
        }

        public void EnsureInitialized()
        {
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();

            if (boxMesh == null)
            {
                boxMesh = new Mesh { name = "AttackBoxMesh" };
                if (meshFilter != null) meshFilter.mesh = boxMesh;
            }

            if (boxMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
                if (shader == null) shader = Shader.Find("Unlit/Color");

                boxMaterial = new Material(shader);
                if (meshRenderer != null)
                {
                    meshRenderer.material = boxMaterial;
                    meshRenderer.enabled = false;
                    meshRenderer.sortingOrder = 8;
                }
            }
        }

        /// <summary>
        /// Возвращает локальное смещение центра прямоугольника для заданного направления атаки.
        /// Хитбоксы берут начало прямо от края модельки игрока и достигают противоположной стороны врага.
        /// </summary>
        public Vector2 GetAttackZoneOffset(AttackDirection direction)
        {
            float centerX = (playerBodyEdge + (horizontalReach + 0.60f)) * 0.5f;

            switch (direction)
            {
                case AttackDirection.UpLeft:    return new Vector2(-centerX, headHeight);
                case AttackDirection.Left:      return new Vector2(-centerX, bodyHeight);
                case AttackDirection.DownLeft:  return new Vector2(-centerX, legsHeight);

                case AttackDirection.UpRight:   return new Vector2(centerX, headHeight);
                case AttackDirection.Right:     return new Vector2(centerX, bodyHeight);
                case AttackDirection.DownRight: return new Vector2(centerX, legsHeight);

                case AttackDirection.Down:      return new Vector2(-centerX, legsHeight);
                case AttackDirection.Up:        return new Vector2(-centerX, headHeight);
                case AttackDirection.Center:    return new Vector2(-centerX, bodyHeight);

                default: return Vector2.zero;
            }
        }

        /// <summary>
        /// Статический метод для получения смещения зоны атаки.
        /// </summary>
        public static Vector2 GetStaticZoneOffset(AttackDirection direction, float reach = 1.35f, float head = 0.60f, float body = 0f, float legs = -0.60f, float bodyEdge = 0.45f)
        {
            float centerX = (bodyEdge + (reach + 0.60f)) * 0.5f;

            switch (direction)
            {
                case AttackDirection.UpLeft:    return new Vector2(-centerX, head);
                case AttackDirection.Left:      return new Vector2(-centerX, body);
                case AttackDirection.DownLeft:  return new Vector2(-centerX, legs);

                case AttackDirection.UpRight:   return new Vector2(centerX, head);
                case AttackDirection.Right:     return new Vector2(centerX, body);
                case AttackDirection.DownRight: return new Vector2(centerX, legs);

                case AttackDirection.Down:      return new Vector2(-centerX, legs);
                case AttackDirection.Up:        return new Vector2(-centerX, head);
                case AttackDirection.Center:    return new Vector2(-centerX, body);

                default: return Vector2.zero;
            }
        }

        /// <summary>
        /// Выполняет атаку в заданном направлении:
        /// отображает прямоугольник удара и проверяет попадание по мишеням.
        /// </summary>
        public void PerformSlash(AttackDirection direction, float? customAngle = null)
        {
            if (direction == AttackDirection.None) return;

            Vector2 localCenter = GetAttackZoneOffset(direction);
            Vector2 worldCenter = (Vector2)transform.position + localCenter;

            // 1. Построение процедурного прямоугольного меша
            BuildBoxMesh(localCenter, boxSize);

            // 2. Анимация вспышки
            if (activeSlashRoutine != null)
            {
                StopCoroutine(activeSlashRoutine);
            }
            activeSlashRoutine = StartCoroutine(AnimateSlash());

            // 3. Проверка попадания по целям строго в границах своего сектора
            CheckHitsInBox(worldCenter, boxSize, direction);
        }

        /// <summary>
        /// Конвертирует фехтовальное направление атаки в соответствующую боевую зону (High, Mid, Low).
        /// </summary>
        public static CombatZone DirectionToCombatZone(AttackDirection direction)
        {
            switch (direction)
            {
                case AttackDirection.Up:
                case AttackDirection.UpLeft:
                case AttackDirection.UpRight:
                    return CombatZone.High;

                case AttackDirection.Down:
                case AttackDirection.DownLeft:
                case AttackDirection.DownRight:
                    return CombatZone.Low;

                case AttackDirection.Left:
                case AttackDirection.Right:
                case AttackDirection.Center:
                default:
                    return CombatZone.Mid;
            }
        }

        /// <summary>
        /// Определяет знак горизонтального вектора отталкивания цели.
        /// </summary>
        private float GetHorizontalKnockbackSign(AttackDirection direction, Transform targetTransform)
        {
            switch (direction)
            {
                case AttackDirection.UpRight:
                case AttackDirection.Right:
                case AttackDirection.DownRight:
                    return 1f;

                case AttackDirection.UpLeft:
                case AttackDirection.Left:
                case AttackDirection.DownLeft:
                    return -1f;

                default:
                    if (targetTransform != null)
                    {
                        return (targetTransform.position.x >= transform.root.position.x) ? 1f : -1f;
                    }
                    return 1f;
            }
        }

        private Coroutine _hitstopRoutine;

        private void TriggerPlayerHitstop(float duration = 0.05f)
        {
            if (duration <= 0.005f) return;
            if (_hitstopRoutine != null) StopCoroutine(_hitstopRoutine);
            _hitstopRoutine = StartCoroutine(HitstopRoutine(duration));
        }

        private IEnumerator HitstopRoutine(float duration)
        {
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = 1.0f;
            _hitstopRoutine = null;
        }

        /// <summary>
        /// Событие успешного удара мечом в определенном направлении (пораженный коллайдер и направление).
        /// </summary>
        public static event Action<Collider2D, AttackDirection> OnSlashHit;

        private readonly HashSet<Collider2D> _hitTargetsInCurrentSwing = new HashSet<Collider2D>();
        private readonly HashSet<IHurtboxTarget2D> _hitReceiversInCurrentSwing = new HashSet<IHurtboxTarget2D>();

        private void CheckHitsInBox(Vector2 worldCenter, Vector2 size, AttackDirection attackDir)
        {
            _hitTargetsInCurrentSwing.Clear();
            _hitReceiversInCurrentSwing.Clear();

            // 1. Проверка попадания по любым коллайдерам целей/врагов
            Collider2D[] hits = Physics2D.OverlapBoxAll(worldCenter, size, 0f);
            bool hitAny = false;

            CombatZone targetZone = DirectionToCombatZone(attackDir);

            // Урон с учетом глобальных множителей (Roguelike upgrades и т.д.)
            float effectiveDamage = damage;
            if (RoguelikeUpgradeManager.Instance != null)
            {
                effectiveDamage *= RoguelikeUpgradeManager.Instance.PlayerDamageMultiplier;
            }

            foreach (var hit in hits)
            {
                if (hit == null || hit.CompareTag("Player") || hit.transform.IsChildOf(transform.root)) continue;
                if (_hitTargetsInCurrentSwing.Contains(hit)) continue;

                float horizontalSign = GetHorizontalKnockbackSign(attackDir, hit.transform);
                Vector2 hitPoint = hit.ClosestPoint(worldCenter);
                Vector2 knockbackDir = new Vector2(horizontalSign, 0.4f).normalized;

                AttackConfig attackConfig = new AttackConfig
                {
                    attackName = $"Фехтовальный выпад ({attackDir})",
                    damage = effectiveDamage,
                    targetedZones = targetZone,
                    knockbackForce = knockbackForce,
                    attacker = transform.root.gameObject
                };

                // Вариант 1: Цель имеет Hurtbox определенной зоны
                var hurtbox = hit.GetComponent<CombatHurtbox2D>();
                if (hurtbox != null)
                {
                    if (attackConfig.targetedZones.Overlaps(hurtbox.BodyZone))
                    {
                        var receiver = hurtbox.GetTargetReceiver();
                        if (receiver != null && _hitReceiversInCurrentSwing.Contains(receiver)) continue;

                        _hitTargetsInCurrentSwing.Add(hit);
                        if (receiver != null) _hitReceiversInCurrentSwing.Add(receiver);

                        hitAny = true;
                        OnSlashHit?.Invoke(hit, attackDir);
                        hurtbox.ReceiveHit(attackConfig, hitPoint, knockbackDir);
                        TriggerPlayerHitstop();
                    }
                }
                else
                {
                    // Вариант 2: Цель реализует IHurtboxTarget2D (EnemyAIController2D, CombatDummy2D и т.д.)
                    var target = hit.GetComponent<IHurtboxTarget2D>() ?? hit.GetComponentInParent<IHurtboxTarget2D>();
                    if (target != null)
                    {
                        if (_hitReceiversInCurrentSwing.Contains(target)) continue;

                        _hitTargetsInCurrentSwing.Add(hit);
                        _hitReceiversInCurrentSwing.Add(target);

                        hitAny = true;
                        OnSlashHit?.Invoke(hit, attackDir);
                        target.TakeHit(attackConfig, targetZone, hitPoint, knockbackDir);
                        TriggerPlayerHitstop();
                    }
                    else
                    {
                        // Вариант 3: Fallback для прочих разрушаемых объектов/декораций
                        _hitTargetsInCurrentSwing.Add(hit);
                        hitAny = true;
                        OnSlashHit?.Invoke(hit, attackDir);
                        hit.SendMessage("TakeHit", attackConfig, SendMessageOptions.DontRequireReceiver);
                        hit.SendMessage("TakeDamage", effectiveDamage, SendMessageOptions.DontRequireReceiver);
                    }
                }
            }

            if (hitAny && FocusManager.Instance != null)
            {
                FocusManager.Instance.AddFocus(FocusManager.Instance.focusGainOnHit);
            }
        }

        private IEnumerator AnimateSlash()
        {
            meshRenderer.enabled = true;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                Color c = slashColor;
                c.a = Mathf.Lerp(slashColor.a, 0f, t);
                boxMaterial.color = c;

                yield return null;
            }

            meshRenderer.enabled = false;
            activeSlashRoutine = null;
        }

        /// <summary>
        /// Генерирует прямоугольный меш в локальных координатах.
        /// </summary>
        private void BuildBoxMesh(Vector2 center, Vector2 size)
        {
            EnsureInitialized();

            boxMesh.Clear();

            float halfX = size.x * 0.5f;
            float halfY = size.y * 0.5f;

            Vector3[] vertices = new Vector3[4]
            {
                new Vector3(center.x - halfX, center.y - halfY, 0f),
                new Vector3(center.x + halfX, center.y - halfY, 0f),
                new Vector3(center.x + halfX, center.y + halfY, 0f),
                new Vector3(center.x - halfX, center.y + halfY, 0f)
            };

            int[] triangles = new int[6]
            {
                0, 2, 1,
                0, 3, 2
            };

            Vector2[] uvs = new Vector2[4]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1)
            };

            Color[] colors = new Color[4]
            {
                slashColor,
                slashColor,
                slashColor,
                slashColor
            };

            boxMesh.vertices = vertices;
            boxMesh.triangles = triangles;
            boxMesh.uv = uvs;
            boxMesh.colors = colors;
            boxMesh.RecalculateNormals();
        }

        private void OnDrawGizmosSelected()
        {
            DrawGizmoBoxes();
        }

        private void OnDrawGizmos()
        {
            DrawGizmoBoxes();
        }

        private void DrawGizmoBoxes()
        {
            AttackDirection[] directions = new AttackDirection[]
            {
                AttackDirection.UpLeft,
                AttackDirection.Left,
                AttackDirection.DownLeft,
                AttackDirection.UpRight,
                AttackDirection.Right,
                AttackDirection.DownRight
            };

            foreach (var dir in directions)
            {
                Vector3 center = transform.position + (Vector3)GetAttackZoneOffset(dir);

                if (dir == AttackDirection.UpLeft || dir == AttackDirection.UpRight)
                {
                    Gizmos.color = new Color(0.2f, 0.9f, 0.7f, 0.7f); // Голова
                }
                else if (dir == AttackDirection.Left || dir == AttackDirection.Right)
                {
                    Gizmos.color = new Color(0.2f, 0.5f, 1.0f, 0.7f); // Корпус
                }
                else
                {
                    Gizmos.color = new Color(1.0f, 0.65f, 0.2f, 0.7f); // Ноги
                }

                Gizmos.DrawWireCube(center, new Vector3(boxSize.x, boxSize.y, 0.1f));
            }
        }

        private void OnDestroy()
        {
            if (boxMesh != null) Destroy(boxMesh);
            if (boxMaterial != null) Destroy(boxMaterial);
        }
    }
}
