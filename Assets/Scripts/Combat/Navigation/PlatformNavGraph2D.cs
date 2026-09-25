using System;
using System.Collections.Generic;
using UnityEngine;
using LevelGeneration;

namespace Combat.Navigation
{
    public enum NavActionType
    {
        Walk,
        Jump,
        Drop
    }

    [Serializable]
    public class NavPathStep
    {
        public Vector2 Position;
        public NavActionType Action = NavActionType.Walk;
        public Vector2 LandingTarget;
        public float JumpForce = 13.5f;
        public float ForwardSpeed = 3.6f;
        public float FlightDuration = 0.5f;
        public PlatformSegment ToSegment;
        public string Description = "";
    }

    public class PlatformLink
    {
        public PlatformSegment From;
        public PlatformSegment To;
        public NavActionType Action;
        public Vector2 TakeoffPoint;
        public Vector2 LandingPoint;
        public float RequiredJumpForce;
        public float RequiredForwardSpeed;
        public float FlightDuration;
        public float Cost;
    }

    public class PlatformSegment
    {
        public int Id;
        public Collider2D Collider;
        public string Name;
        public float XMin;
        public float XMax;
        public float YTop;
        public Bounds Bounds;
        public List<PlatformLink> Links = new List<PlatformLink>();

        public Vector2 LeftLedge => new Vector2(XMin, YTop);
        public Vector2 RightLedge => new Vector2(XMax, YTop);
        public Vector2 Center => new Vector2((XMin + XMax) * 0.5f, YTop);

        public bool ContainsX(float x, float tolerance = 0.3f)
        {
            return x >= (XMin - tolerance) && x <= (XMax + tolerance);
        }

        public float DistanceTo(Vector2 point)
        {
            float clampedX = Mathf.Clamp(point.x, XMin, XMax);
            return Vector2.Distance(new Vector2(clampedX, YTop), point);
        }
    }

    /// <summary>
    /// Менеджер навигации по 2D-платформам:
    /// Автоматически строит граф платформ (сегментов), вычисляет возможные прыжки (Jump)
    /// и спуски (Drop), находит кратчайший путь через алгоритм A* и визуализирует маршруты
    /// в окне Scene View.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class PlatformNavGraph2D : MonoBehaviour
    {
        private static PlatformNavGraph2D _instance;
        public static PlatformNavGraph2D Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<PlatformNavGraph2D>();
                    if (_instance == null)
                    {
                        var go = new GameObject("[SYSTEM] PlatformNavGraph2D");
                        _instance = go.AddComponent<PlatformNavGraph2D>();
                    }
                }
                return _instance;
            }
        }

        [Header("--- Layers & Detection ---")]
        [Tooltip("Слои земли и платформ для построения графа")]
        [SerializeField] private LayerMask groundLayer = ~0;

        [Tooltip("Отступ от краев коллайдера вовнутрь (чтобы боты не срывались)")]
        [SerializeField] private float edgeInset = 0.30f;

        [Header("--- Bot Physics Capabilities ---")]
        [Tooltip("Базовая гравитация для расчетов")]
        [SerializeField] private float effectiveGravity = 34.34f; // 9.81 * 3.5

        [Tooltip("Смещение центра (пивота) бота над поверхностью платформы (половина высоты коллайдера)")]
        [SerializeField] private float botPivotOffsetY = 0.50f;

        [Tooltip("Максимальная сила прыжка бота")]
        [SerializeField] private float maxJumpForce = 15.5f;

        [Tooltip("Скорость горизонтального бега")]
        [SerializeField] private float botRunSpeed = 3.8f;

        [Tooltip("Запас высоты над посадочной платформой при расчете дуги прыжка (clearing margin)")]
        [SerializeField] private float jumpHeightClearance = 0.85f;

        [Tooltip("Отступ точки приземления внутрь платформы от края")]
        [SerializeField] private float landingEdgeInset = 1.10f;

        [Header("--- Scene View Gizmos ---")]
        [Tooltip("Показывать ли всю структуру графа навигации в Scene View (зеленые платформы и желтые связи)")]
        [SerializeField] private bool showFullGraphInScene = true;

        // Построенные сегменты платформ
        private readonly List<PlatformSegment> _segments = new List<PlatformSegment>();
        public IReadOnlyList<PlatformSegment> Segments => _segments;

        private bool _isGraphBuilt = false;
        private float _lastBuildTime = 0f;

        private void Awake()
        {
            if (_instance == null) _instance = this;
            BuildNavGraph();
        }

        private void Start()
        {
            BuildNavGraph();
        }

        private void OnEnable()
        {
            BuildNavGraph();
        }

        /// <summary>
        /// Сканирует все коллайдеры сцены и перестраивает граф навигации
        /// </summary>
        [ContextMenu("Перестроить граф навигации (Rebuild NavGraph)")]
        public void BuildNavGraph()
        {
            _segments.Clear();
            _isGraphBuilt = false;

            var allColliders = FindObjectsByType<Collider2D>();
            int segId = 0;

            for (int i = 0; i < allColliders.Length; i++)
            {
                var col = allColliders[i];
                if (col == null || col.isTrigger || !col.gameObject.activeInHierarchy) continue;

                // Проверяем принадлежность слою земли
                if (((1 << col.gameObject.layer) & groundLayer) == 0) continue;

                // Исключаем коллайдеры персонажей и боевых сущностей
                if (col.CompareTag("Player") || col.name.Contains("Player") || col.GetComponentInParent<Combat.Player.PlayerHealth2D>() != null) continue;
                if (col.GetComponent<Combat.Common.ICombatEntity2D>() != null || col.GetComponentInParent<Combat.Common.ICombatEntity2D>() != null) continue;
                if (col.name.Contains("Hurtbox") || col.name.Contains("Hitbox") || col.name.Contains("Wall") || col.name.Contains("Barrier") || col.name.Contains("Blocker")) continue;
                if (col.GetComponentInParent<SectorBarrierDoor2D>() != null || col.GetComponentInParent<EnemyBlockerBarrier2D>() != null) continue;

                Bounds b = col.bounds;
                // Исключаем вертикальные тонкие стены
                if (b.size.x < 0.4f && b.size.y > 1.5f) continue;
                // Исключаем слишком маленькие объекты
                if (b.size.x < 0.6f) continue;

                var seg = new PlatformSegment
                {
                    Id = segId++,
                    Collider = col,
                    Name = col.gameObject.name,
                    Bounds = b,
                    XMin = b.min.x + edgeInset,
                    XMax = b.max.x - edgeInset,
                    YTop = b.max.y
                };

                if (seg.XMax > seg.XMin)
                {
                    _segments.Add(seg);
                }
            }

            // Построение связей (Links) между сегментами
            GeneratePlatformLinks();
            _isGraphBuilt = true;
            _lastBuildTime = Time.time;
        }

        private void GeneratePlatformLinks()
        {
            float maxJumpHeight = (maxJumpForce * maxJumpForce) / (2f * effectiveGravity);

            for (int i = 0; i < _segments.Count; i++)
            {
                var segA = _segments[i];

                for (int j = 0; j < _segments.Count; j++)
                {
                    if (i == j) continue;
                    var segB = _segments[j];

                    float dy = segB.YTop - segA.YTop;

                    // 1. ПРЫЖКОВЫЕ СВЯЗИ (JUMP LINKS)
                    // Платформа B выше или на том же уровне или немного ниже
                    if (dy <= maxJumpHeight && dy > -3.5f)
                    {
                        // А. segB находится справа от segA
                        if (segB.XMin >= segA.XMax - 0.5f)
                        {
                            float setback = Mathf.Clamp(1.25f + (dy * 0.25f), 1.25f, 2.2f);
                            float takeoffX = dy > 0.35f
                                ? Mathf.Clamp(segB.XMin - setback, segA.XMin + 0.35f, segA.RightLedge.x)
                                : segA.RightLedge.x;
                            float landX = Mathf.Clamp(segB.XMin + landingEdgeInset, segB.XMin + 0.50f, segB.XMax - 0.35f);
                            Vector2 takeoff = new Vector2(takeoffX, segA.YTop);
                            Vector2 landing = new Vector2(landX, segB.YTop);
                            TryCreateJumpLink(segA, segB, takeoff, landing);
                        }
                        // Б. segB находится слева от segA
                        else if (segB.XMax <= segA.XMin + 0.5f)
                        {
                            float setback = Mathf.Clamp(1.25f + (dy * 0.25f), 1.25f, 2.2f);
                            float takeoffX = dy > 0.35f
                                ? Mathf.Clamp(segB.XMax + setback, segA.LeftLedge.x, segA.XMax - 0.35f)
                                : segA.LeftLedge.x;
                            float landX = Mathf.Clamp(segB.XMax - landingEdgeInset, segB.XMin + 0.35f, segB.XMax - 0.50f);
                            Vector2 takeoff = new Vector2(takeoffX, segA.YTop);
                            Vector2 landing = new Vector2(landX, segB.YTop);
                            TryCreateJumpLink(segA, segB, takeoff, landing);
                        }
                        // В. segA и segB перекрываются по горизонтали (segB выше segA)
                        else if (dy > 0.35f)
                        {
                            float setback = Mathf.Clamp(1.25f + (dy * 0.25f), 1.35f, 2.2f);

                            // Прыжок с левого открытого края segA в обход левого края segB
                            if (segA.XMin <= segB.XMin - 0.35f)
                            {
                                float takeoffX = Mathf.Clamp(segB.XMin - setback, segA.XMin + 0.25f, segB.XMin - 0.50f);
                                float landX = Mathf.Clamp(segB.XMin + landingEdgeInset, segB.XMin + 0.50f, segB.XMax - 0.35f);
                                Vector2 takeoff = new Vector2(takeoffX, segA.YTop);
                                Vector2 landing = new Vector2(landX, segB.YTop);
                                TryCreateJumpLink(segA, segB, takeoff, landing);
                            }

                            // Прыжок с правого открытого края segA в обход правого края segB
                            if (segA.XMax >= segB.XMax + 0.35f)
                            {
                                float takeoffX = Mathf.Clamp(segB.XMax + setback, segB.XMax + 0.50f, segA.XMax - 0.25f);
                                float landX = Mathf.Clamp(segB.XMax - landingEdgeInset, segB.XMin + 0.35f, segB.XMax - 0.50f);
                                Vector2 takeoff = new Vector2(takeoffX, segA.YTop);
                                Vector2 landing = new Vector2(landX, segB.YTop);
                                TryCreateJumpLink(segA, segB, takeoff, landing);
                            }

                            // Исключение: ТОЛЬКО для односторонних сквозных платформ (PlatformEffector2D с useOneWay)
                            // разрешен вертикальный прыжок сквозь платформу снизу вверх
                            if (segB.Collider != null && segB.Collider.usedByEffector)
                            {
                                var effector = segB.Collider.GetComponent<PlatformEffector2D>();
                                if (effector != null && effector.useOneWay)
                                {
                                    float overlapLeft = Mathf.Max(segA.XMin, segB.XMin);
                                    float overlapRight = Mathf.Min(segA.XMax, segB.XMax);
                                    if (overlapRight > overlapLeft + 0.6f)
                                    {
                                        float midX = (overlapLeft + overlapRight) * 0.5f;
                                        Vector2 takeoff = new Vector2(midX, segA.YTop);
                                        Vector2 landing = new Vector2(midX, segB.YTop);
                                        TryCreateJumpLink(segA, segB, takeoff, landing);
                                    }
                                }
                            }
                        }
                    }

                    // 2. СПУСКИ И ПАДЕНИЯ (DROP LINKS)
                    // Платформа B находится ниже платформы A
                    if (dy < -0.8f && dy > -8.5f)
                    {
                        // Спуск с правого края A на B
                        if (segA.RightLedge.x >= segB.XMin - 0.6f && segA.RightLedge.x <= segB.XMax + 0.6f)
                        {
                            Vector2 takeoff = segA.RightLedge;
                            Vector2 landing = new Vector2(Mathf.Clamp(takeoff.x + 0.45f, segB.XMin + 0.3f, segB.XMax - 0.3f), segB.YTop);
                            CreateDropLink(segA, segB, takeoff, landing);
                        }

                        // Спуск с левого края A на B
                        if (segA.LeftLedge.x >= segB.XMin - 0.6f && segA.LeftLedge.x <= segB.XMax + 0.6f)
                        {
                            Vector2 takeoff = segA.LeftLedge;
                            Vector2 landing = new Vector2(Mathf.Clamp(takeoff.x - 0.45f, segB.XMin + 0.3f, segB.XMax - 0.3f), segB.YTop);
                            CreateDropLink(segA, segB, takeoff, landing);
                        }
                    }
                }
            }
        }

        private void TryCreateJumpLink(PlatformSegment from, PlatformSegment to, Vector2 takeoff, Vector2 landing)
        {
            Vector2 takeoffPivot = takeoff + new Vector2(0f, botPivotOffsetY);
            Vector2 landingPivot = landing + new Vector2(0f, botPivotOffsetY);

            float dx = landingPivot.x - takeoffPivot.x;
            float dy = landingPivot.y - takeoffPivot.y;

            // Кандидаты высоты подъема над наивысшей точкой (clearance)
            // Проверяем несколько вариантов дуг: от стандартной до более высокой или пологой
            float[] clearanceCandidates = new float[] { jumpHeightClearance, jumpHeightClearance + 0.35f, jumpHeightClearance + 0.70f, 0.45f };

            PlatformLink bestLink = null;
            float bestCost = float.MaxValue;

            for (int c = 0; c < clearanceCandidates.Length; c++)
            {
                float clearance = clearanceCandidates[c];
                float peakY = Mathf.Max(takeoffPivot.y, landingPivot.y) + clearance;
                float hUp = peakY - takeoffPivot.y;
                if (hUp < 0.15f) hUp = 0.15f;

                float vy = Mathf.Sqrt(2f * effectiveGravity * hUp);
                if (vy > maxJumpForce) continue; // Не превышаем физический лимит силы прыжка бота

                float radicand = (vy * vy) - (2f * effectiveGravity * dy);
                if (radicand < 0f) continue; // Не долетит по высоте

                float totalTime = (vy + Mathf.Sqrt(radicand)) / effectiveGravity;
                if (totalTime <= 0.08f) continue;

                float vx = dx / totalTime;
                if (Mathf.Abs(vx) > botRunSpeed * 1.65f) continue; // Превышает максимальную скорость бега

                // Проверяем коллизии по всей дуге с реальным размером коллайдера бота (Box 0.80 x 0.95)
                if (!IsJumpTrajectoryClear(takeoffPivot, landingPivot, vy, vx, totalTime, from.Collider, to.Collider))
                {
                    continue;
                }

                // Стоимость траектории (предпочитаем естественные и быстрые дуги)
                float cost = Vector2.Distance(takeoff, landing) * 1.1f + totalTime * 1.5f + (vy * 0.08f);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestLink = new PlatformLink
                    {
                        From = from,
                        To = to,
                        Action = NavActionType.Jump,
                        TakeoffPoint = takeoff,
                        LandingPoint = landing,
                        RequiredJumpForce = vy,      // ТОЧНОЕ значение vy (без Clamp, синхронизировано с totalTime)
                        RequiredForwardSpeed = vx,   // ТОЧНОЕ значение vx
                        FlightDuration = totalTime,  // ТОЧНОЕ время полета
                        Cost = cost
                    };
                }
            }

            if (bestLink != null)
            {
                from.Links.Add(bestLink);
            }
        }

        private bool IsJumpTrajectoryClear(Vector2 takeoffPivot, Vector2 landingPivot, float vy, float vx, float totalTime, Collider2D fromCol, Collider2D toCol)
        {
            int steps = 20;
            Vector2 botBoxSize = new Vector2(0.80f, 0.95f);
            float botHalfHeight = botBoxSize.y * 0.5f;
            var toBounds = toCol != null ? toCol.bounds : default;

            for (int i = 1; i < steps; i++)
            {
                float t = (totalTime * i) / steps;
                float px = takeoffPivot.x + vx * t;
                float py = takeoffPivot.y + (vy * t - 0.5f * effectiveGravity * t * t);
                Vector2 checkPos = new Vector2(px, py);

                var hits = Physics2D.OverlapBoxAll(checkPos, botBoxSize, 0f, groundLayer);
                for (int h = 0; h < hits.Length; h++)
                {
                    var hit = hits[h];
                    if (hit == null || hit.isTrigger) continue;

                    // Стартовый коллайдер игнорируем только в самом начале отрыва (первые ~15% времени)
                    if (hit == fromCol && (float)i / steps <= 0.15f) continue;

                    // Проверка коллизии с целевой платформой
                    if (hit == toCol)
                    {
                        // Если бот касается целевой платформы:
                        // Его ноги (нижняя грань коллайдера) ОБЯЗАНЫ быть не ниже верхней поверхности платформы!
                        // Иначе бот врезается в боковую грань или потолок платформы на подъеме.
                        float feetY = py - botHalfHeight;
                        if (feetY < toBounds.max.y - 0.08f)
                        {
                            return false; // Врезался в боковую грань или потолок!
                        }
                        continue; // Ноги выше платформы -> безопасное приземление сверху
                    }

                    // Односторонние платформы (PlatformEffector2D): снизу вверх проходить можно
                    if (hit.usedByEffector)
                    {
                        var effector = hit.GetComponent<PlatformEffector2D>();
                        if (effector != null && effector.useOneWay)
                        {
                            float currentVy = vy - effectiveGravity * t;
                            if (currentVy > 0f) continue;
                        }
                    }

                    // Любое другое препятствие (потолок платформы снизу, стена, чужая платформа)
                    return false;
                }
            }

            return true;
        }

        private void CreateDropLink(PlatformSegment from, PlatformSegment to, Vector2 takeoff, Vector2 landing)
        {
            var link = new PlatformLink
            {
                From = from,
                To = to,
                Action = NavActionType.Drop,
                TakeoffPoint = takeoff,
                LandingPoint = landing,
                RequiredJumpForce = 0f,
                RequiredForwardSpeed = Mathf.Sign(landing.x - takeoff.x) * (botRunSpeed * 0.8f),
                FlightDuration = Mathf.Sqrt(2f * Mathf.Abs(landing.y - takeoff.y) / effectiveGravity),
                Cost = Vector2.Distance(takeoff, landing) * 0.8f + 0.5f
            };

            from.Links.Add(link);
        }

        /// <summary>
        /// Находит сегмент платформы, на котором находится указанная точка
        /// </summary>
        public PlatformSegment GetSegmentAt(Vector2 point, float maxDist = 3.5f)
        {
            if (!_isGraphBuilt || _segments.Count == 0) BuildNavGraph();

            PlatformSegment bestSeg = null;
            float minDist = float.MaxValue;

            for (int i = 0; i < _segments.Count; i++)
            {
                var seg = _segments[i];
                // Если точка находится прямо над платформой
                if (seg.ContainsX(point.x, 0.4f))
                {
                    float verticalDist = point.y - seg.YTop;
                    if (verticalDist >= -0.3f && verticalDist <= 2.2f)
                    {
                        return seg;
                    }
                }

                float dist = seg.DistanceTo(point);
                if (dist < minDist)
                {
                    minDist = dist;
                    bestSeg = seg;
                }
            }

            return minDist <= maxDist ? bestSeg : null;
        }

        /// <summary>
        /// Проверяет, находится ли точка на поверхности указанного сегмента платформы
        /// </summary>
        public bool IsPositionOnSegment(PlatformSegment seg, Vector2 pos, float marginX = 0.45f, float marginY = 0.65f)
        {
            if (seg == null) return false;
            bool xMatch = pos.x >= (seg.XMin - marginX) && pos.x <= (seg.XMax + marginX);
            bool yMatch = pos.y >= (seg.YTop - marginY) && pos.y <= (seg.YTop + 1.25f);
            return xMatch && yMatch;
        }

        /// <summary>
        /// Поиск оптимального пути от startPos до goalPos по графу платформ (A*)
        /// </summary>
        public bool FindPath(Vector2 startPos, Vector2 goalPos, List<NavPathStep> outPath)
        {
            if (outPath == null) return false;
            outPath.Clear();

            if (!_isGraphBuilt || _segments.Count == 0 || Time.time - _lastBuildTime > 8.0f)
            {
                BuildNavGraph();
            }

            var startSeg = GetSegmentAt(startPos);
            var goalSeg = GetSegmentAt(goalPos);

            // Если не удалось определить платформы
            if (startSeg == null || goalSeg == null)
            {
                Vector2 targetPos = goalPos;
                if (startSeg != null)
                {
                    targetPos = new Vector2(Mathf.Clamp(goalPos.x, startSeg.XMin, startSeg.XMax), startSeg.YTop);
                }

                outPath.Add(new NavPathStep
                {
                    Position = targetPos,
                    Action = NavActionType.Walk,
                    Description = "Direct Walk (No NavSegment)"
                });
                return true;
            }

            // 1. Обе точки на одной и той же платформе -> прямой бег
            if (startSeg == goalSeg)
            {
                outPath.Add(new NavPathStep
                {
                    Position = goalPos,
                    Action = NavActionType.Walk,
                    Description = $"Walk on {startSeg.Name}"
                });
                return true;
            }

            // 2. A* поиск пути по графу платформ
            var openSet = new List<PlatformSegment> { startSeg };
            var cameFrom = new Dictionary<PlatformSegment, PlatformLink>();
            var gScore = new Dictionary<PlatformSegment, float> { [startSeg] = 0f };
            var fScore = new Dictionary<PlatformSegment, float> { [startSeg] = Vector2.Distance(startSeg.Center, goalSeg.Center) };

            PlatformSegment current = null;
            bool found = false;

            while (openSet.Count > 0)
            {
                // Извлекаем сегмент с наименьшим fScore
                current = openSet[0];
                float lowestF = fScore.ContainsKey(current) ? fScore[current] : float.MaxValue;
                int lowestIndex = 0;

                for (int i = 1; i < openSet.Count; i++)
                {
                    var cand = openSet[i];
                    float f = fScore.ContainsKey(cand) ? fScore[cand] : float.MaxValue;
                    if (f < lowestF)
                    {
                        lowestF = f;
                        current = cand;
                        lowestIndex = i;
                    }
                }

                if (current == goalSeg)
                {
                    found = true;
                    break;
                }

                openSet.RemoveAt(lowestIndex);

                for (int l = 0; l < current.Links.Count; l++)
                {
                    var link = current.Links[l];
                    var neighbor = link.To;

                    // Учитываем горизонтальное расстояние перемещения по платформе до точки отрыва (TakeoffPoint)
                    float distToTakeoff = (current == startSeg)
                        ? Mathf.Abs(startPos.x - link.TakeoffPoint.x)
                        : (cameFrom.ContainsKey(current) ? Mathf.Abs(cameFrom[current].LandingPoint.x - link.TakeoffPoint.x) : 0f);

                    float tentativeG = gScore[current] + link.Cost + (distToTakeoff / Mathf.Max(0.5f, botRunSpeed));

                    if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                    {
                        cameFrom[neighbor] = link;
                        gScore[neighbor] = tentativeG;
                        float heuristic = (neighbor == goalSeg)
                            ? Mathf.Abs(link.LandingPoint.x - goalPos.x) / Mathf.Max(0.5f, botRunSpeed)
                            : (Vector2.Distance(neighbor.Center, goalSeg.Center) / Mathf.Max(0.5f, botRunSpeed));
                        fScore[neighbor] = tentativeG + heuristic;

                        if (!openSet.Contains(neighbor))
                        {
                            openSet.Add(neighbor);
                        }
                    }
                }
            }

            if (!found)
            {
                // Если пути нет — направляем бота к точке на стартовой платформе, ближайшей к цели,
                // чтобы он не шел прямо под потолок и не пытался запрыгнуть сквозь препятствие!
                float bestX = Mathf.Clamp(goalPos.x, startSeg.XMin, startSeg.XMax);
                outPath.Add(new NavPathStep
                {
                    Position = new Vector2(bestX, startSeg.YTop),
                    Action = NavActionType.Walk,
                    Description = "Stand Near Target Ledge"
                });
                return false;
            }

            // Восстановление пути из цепочки ссылок (Reconstruct Path)
            var linksPath = new List<PlatformLink>();
            var currTrace = goalSeg;
            while (cameFrom.ContainsKey(currTrace))
            {
                var link = cameFrom[currTrace];
                linksPath.Add(link);
                currTrace = link.From;
            }
            linksPath.Reverse();

            // Преобразование связей в путевые точки (Waypoints)
            for (int p = 0; p < linksPath.Count; p++)
            {
                var link = linksPath[p];

                // Точка подхода перед прыжком/спуском
                outPath.Add(new NavPathStep
                {
                    Position = link.TakeoffPoint,
                    Action = NavActionType.Walk,
                    Description = $"Walk to edge on {link.From.Name}"
                });

                // Сам прыжок или спуск на следующую платформу
                outPath.Add(new NavPathStep
                {
                    Position = link.TakeoffPoint,
                    LandingTarget = link.LandingPoint,
                    Action = link.Action,
                    JumpForce = link.RequiredJumpForce,
                    ForwardSpeed = link.RequiredForwardSpeed,
                    FlightDuration = link.FlightDuration,
                    ToSegment = link.To,
                    Description = $"{link.Action} from {link.From.Name} to {link.To.Name}"
                });
            }

            // Финальный шаг — подход к цели на целевой платформе
            outPath.Add(new NavPathStep
            {
                Position = goalPos,
                Action = NavActionType.Walk,
                Description = $"Arrive at goal on {goalSeg.Name}"
            });

            return true;
        }

        private void OnDrawGizmos()
        {
            if (!showFullGraphInScene) return;

            // Отрисовка всех обнаруженных платформ
            for (int i = 0; i < _segments.Count; i++)
            {
                var seg = _segments[i];
                Gizmos.color = new Color(0.2f, 1.0f, 0.4f, 0.85f);
                Gizmos.DrawLine(new Vector2(seg.XMin, seg.YTop), new Vector2(seg.XMax, seg.YTop));
                Gizmos.DrawSphere(seg.LeftLedge, 0.10f);
                Gizmos.DrawSphere(seg.RightLedge, 0.10f);

                // Отрисовка связей
                for (int l = 0; l < seg.Links.Count; l++)
                {
                    var link = seg.Links[l];
                    if (link.Action == NavActionType.Jump)
                    {
                        Gizmos.color = new Color(1.0f, 0.75f, 0.1f, 0.5f);
                        DrawJumpArcGizmo(link.TakeoffPoint, link.LandingPoint, link.RequiredJumpForce, link.RequiredForwardSpeed, link.FlightDuration, 16, botPivotOffsetY, effectiveGravity);
                    }
                    else if (link.Action == NavActionType.Drop)
                    {
                        Gizmos.color = new Color(0.3f, 0.7f, 1.0f, 0.4f);
                        Gizmos.DrawLine(link.TakeoffPoint, link.LandingPoint);
                    }
                }
            }
        }

        /// <summary>
        /// Рисует физическую параболическую дугу прыжка в Scene View
        /// </summary>
        public static void DrawJumpArcGizmo(Vector2 takeoff, Vector2 landing, float vy, float vx, float duration, int steps = 16, float pivotOffsetY = 0.5f, float gravity = 34.34f)
        {
            float g = gravity > 0.01f ? gravity : 34.34f;
            Vector2 prev = takeoff + new Vector2(0f, pivotOffsetY);

            for (int s = 1; s <= steps; s++)
            {
                float t = (float)s / steps * duration;
                float px = takeoff.x + vx * t;
                float py = takeoff.y + pivotOffsetY + (vy * t - 0.5f * g * t * t);
                Vector2 curr = new Vector2(px, py);
                Gizmos.DrawLine(prev, curr);
                prev = curr;
            }
            Gizmos.DrawLine(prev, landing + new Vector2(0f, pivotOffsetY));
        }
    }
}
