using System;
using System.Collections.Generic;
using UnityEngine;

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
        [SerializeField] private float edgeInset = 0.25f;

        [Header("--- Bot Physics Capabilities ---")]
        [Tooltip("Базовая гравитация для расчетов")]
        [SerializeField] private float effectiveGravity = 34.34f; // 9.81 * 3.5

        [Tooltip("Максимальная сила прыжка бота")]
        [SerializeField] private float maxJumpForce = 14.0f;

        [Tooltip("Скорость горизонтального бега")]
        [SerializeField] private float botRunSpeed = 3.6f;

        [Tooltip("Запас высоты при расчете дуги прыжка (clearing margin)")]
        [SerializeField] private float jumpHeightClearance = 0.45f;

        [Header("--- Scene View Gizmos ---")]
        [Tooltip("Показывать ли всю структуру графа навигации в Scene View (зеленые платформы и желтые связи)")]
        [SerializeField] private bool showFullGraphInScene = false;

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
                if (col.name.Contains("Hurtbox") || col.name.Contains("Hitbox") || col.name.Contains("Wall")) continue;

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
                    // Платформа B выше или на том же уровне или немного ниже (но не прямо под ногами)
                    if (dy <= maxJumpHeight && dy > -3.5f)
                    {
                        // А. Прыжок с правого края segA в сторону segB
                        if (segB.XMin > segA.XMax - 0.5f)
                        {
                            Vector2 takeoff = segA.RightLedge;
                            Vector2 landing = new Vector2(Mathf.Clamp(segB.XMin + 0.5f, segB.XMin, segB.XMax), segB.YTop);
                            TryCreateJumpLink(segA, segB, takeoff, landing);
                        }

                        // Б. Прыжок с левого края segA в сторону segB
                        if (segB.XMax < segA.XMin + 0.5f)
                        {
                            Vector2 takeoff = segA.LeftLedge;
                            Vector2 landing = new Vector2(Mathf.Clamp(segB.XMax - 0.5f, segB.XMin, segB.XMax), segB.YTop);
                            TryCreateJumpLink(segA, segB, takeoff, landing);
                        }

                        // В. Прыжок с платформы A вверх на перекрывающую платформу B (вертикальный подскок)
                        if (dy > 0.4f && dy <= maxJumpHeight)
                        {
                            float overlapLeft = Mathf.Max(segA.XMin, segB.XMin);
                            float overlapRight = Mathf.Min(segA.XMax, segB.XMax);
                            if (overlapRight > overlapLeft + 0.5f)
                            {
                                float midX = (overlapLeft + overlapRight) * 0.5f;
                                Vector2 takeoff = new Vector2(midX, segA.YTop);
                                Vector2 landing = new Vector2(midX, segB.YTop);
                                TryCreateJumpLink(segA, segB, takeoff, landing);
                            }
                        }
                    }

                    // 2. СПУСКИ И ПАДЕНИЯ (DROP LINKS)
                    // Платформа B находится ниже платформы A
                    if (dy < -0.8f && dy > -8.0f)
                    {
                        // Спуск с правого края A на B
                        if (segA.RightLedge.x >= segB.XMin - 0.6f && segA.RightLedge.x <= segB.XMax + 0.6f)
                        {
                            Vector2 takeoff = segA.RightLedge;
                            Vector2 landing = new Vector2(Mathf.Clamp(takeoff.x + 0.4f, segB.XMin, segB.XMax), segB.YTop);
                            CreateDropLink(segA, segB, takeoff, landing);
                        }

                        // Спуск с левого края A на B
                        if (segA.LeftLedge.x >= segB.XMin - 0.6f && segA.LeftLedge.x <= segB.XMax + 0.6f)
                        {
                            Vector2 takeoff = segA.LeftLedge;
                            Vector2 landing = new Vector2(Mathf.Clamp(takeoff.x - 0.4f, segB.XMin, segB.XMax), segB.YTop);
                            CreateDropLink(segA, segB, takeoff, landing);
                        }
                    }
                }
            }
        }

        private void TryCreateJumpLink(PlatformSegment from, PlatformSegment to, Vector2 takeoff, Vector2 landing)
        {
            float dx = landing.x - takeoff.x;
            float dy = landing.y - takeoff.y;

            // Расчет физики прыжка
            float peakY = Mathf.Max(takeoff.y, landing.y) + jumpHeightClearance;
            float hUp = peakY - takeoff.y;
            float hDown = peakY - landing.y;

            if (hUp < 0.1f) hUp = 0.1f;
            if (hDown < 0.1f) hDown = 0.1f;

            float vy = Mathf.Sqrt(2f * effectiveGravity * hUp);
            if (vy > maxJumpForce * 1.15f) return; // Слишком высоко

            float tUp = vy / effectiveGravity;
            float tDown = Mathf.Sqrt((2f * hDown) / effectiveGravity);
            float totalTime = tUp + tDown;

            if (totalTime <= 0.05f) return;

            float vx = dx / totalTime;
            // Проверяем, укладывается ли горизонтальная скорость в бег с ускорением
            if (Mathf.Abs(vx) > botRunSpeed * 1.6f) return; // Слишком далеко по горизонтали

            var link = new PlatformLink
            {
                From = from,
                To = to,
                Action = NavActionType.Jump,
                TakeoffPoint = takeoff,
                LandingPoint = landing,
                RequiredJumpForce = Mathf.Clamp(vy, 9.0f, maxJumpForce * 1.05f),
                RequiredForwardSpeed = vx,
                FlightDuration = totalTime,
                Cost = Vector2.Distance(takeoff, landing) * 1.2f + 1.5f
            };

            from.Links.Add(link);
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

            // Если не удалось определить платформы — прямой шаг к цели
            if (startSeg == null || goalSeg == null)
            {
                outPath.Add(new NavPathStep
                {
                    Position = goalPos,
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
                    float tentativeG = gScore[current] + link.Cost;

                    if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                    {
                        cameFrom[neighbor] = link;
                        gScore[neighbor] = tentativeG;
                        fScore[neighbor] = tentativeG + Vector2.Distance(neighbor.Center, goalSeg.Center);

                        if (!openSet.Contains(neighbor))
                        {
                            openSet.Add(neighbor);
                        }
                    }
                }
            }

            if (!found)
            {
                // Если пути нет — приближаемся насколько возможно по прямой
                outPath.Add(new NavPathStep
                {
                    Position = goalPos,
                    Action = NavActionType.Walk,
                    Description = "Unreachable Direct Step"
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
                        DrawJumpArcGizmo(link.TakeoffPoint, link.LandingPoint, link.RequiredJumpForce, link.RequiredForwardSpeed, link.FlightDuration);
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
        public static void DrawJumpArcGizmo(Vector2 takeoff, Vector2 landing, float vy, float vx, float duration, int steps = 14)
        {
            float g = 34.34f;
            Vector2 prev = takeoff;

            for (int s = 1; s <= steps; s++)
            {
                float t = (float)s / steps * duration;
                float px = takeoff.x + vx * t;
                float py = takeoff.y + (vy * t - 0.5f * g * t * t);
                Vector2 curr = new Vector2(px, py);
                Gizmos.DrawLine(prev, curr);
                prev = curr;
            }
            Gizmos.DrawLine(prev, landing);
        }
    }
}
