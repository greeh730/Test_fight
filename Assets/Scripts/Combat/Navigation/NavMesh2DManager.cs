using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.AI;
using NavMeshPlus.Components;
using NavMeshPlus.Extensions;
using Debug = UnityEngine.Debug;

namespace Combat.Navigation
{
    /// <summary>
    /// Централизованный менеджер 2D NavMesh (NavMeshPlus) в плоскости XY.
    /// Отвечает за динамическое перепекание навигационной сетки при процедурной генерации комнат,
    /// синхронизацию геометрии и валидацию точек для NavMeshAgent.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(-50)]
    public class NavMesh2DManager : MonoBehaviour
    {
        private static NavMesh2DManager _instance;
        public static NavMesh2DManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<NavMesh2DManager>();
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("--- Компоненты NavMeshPlus ---")]
        [SerializeField] private NavMeshSurface surface;
        [SerializeField] private CollectSources2d collectSources2d;

        [Header("--- Настройки запекания ---")]
        [Tooltip("Автоматически запекать навмеш на старте сцены")]
        [SerializeField] private bool bakeOnStart = true;

        [Tooltip("Выводить диагностические логи о времени запекания в консоль")]
        [SerializeField] private bool logBakeDiagnostics = true;

        /// <summary>
        /// Событие, вызываемое после успешного завершения перепекания NavMesh.
        /// </summary>
        public event Action OnNavMeshRebuilt;

        public NavMeshSurface Surface => surface;
        public bool IsNavMeshReady => surface != null && surface.navMeshData != null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureComponentsConfigured();
        }

        private void Start()
        {
            if (bakeOnStart)
            {
                RebuildNavMesh();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Гарантирует корректную ориентацию (-90 градусов по X) и наличие необходимых компонентов.
        /// </summary>
        public void EnsureComponentsConfigured()
        {
            // NavMeshPlus проецирует XY на XZ через поворот Transform на -90 градусов по оси X
            transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            if (surface == null)
            {
                surface = GetComponent<NavMeshSurface>();
                if (surface == null)
                {
                    surface = gameObject.AddComponent<NavMeshSurface>();
                }
            }

            if (collectSources2d == null)
            {
                collectSources2d = GetComponent<CollectSources2d>();
                if (collectSources2d == null)
                {
                    collectSources2d = gameObject.AddComponent<CollectSources2d>();
                }
            }

            // Настройка физических 2D коллайдеров как источника геометрии
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.collectObjects = CollectObjects.All;
            surface.hideEditorLogs = true;
        }

        /// <summary>
        /// Полное динамическое перепекание 2D NavMesh в рантайме.
        /// Вызывается генератором уровней после завершения расстановки чанков комнат.
        /// </summary>
        [ContextMenu("Перепечь 2D NavMesh (Rebuild)")]
        public void RebuildNavMesh()
        {
            EnsureComponentsConfigured();

            var sw = Stopwatch.StartNew();

            try
            {
                // Принудительно синхронизируем физические коллайдеры перед сбором геометрии
                Physics2D.SyncTransforms();

                surface.BuildNavMesh();
                sw.Stop();

                if (logBakeDiagnostics)
                {
                    var bounds = surface.navMeshData != null ? surface.navMeshData.sourceBounds : default;
                    Debug.Log($"<color=#00FFAA><b>[NavMesh2D]</b></color> 2D NavMesh успешно построен за <b>{sw.ElapsedMilliseconds} мс</b>! Границы сетки: {bounds.size.x:F1}x{bounds.size.z:F1}");
                }

                OnNavMeshRebuilt?.Invoke();
            }
            catch (Exception ex)
            {
                sw.Stop();
                Debug.LogError($"<color=red><b>[NavMesh2D]</b></color> Ошибка при построении NavMesh: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Проверяет, проецируется ли точка на валидный участок NavMesh в пределах допустимого радиуса.
        /// </summary>
        public bool SampleNavMeshPosition(Vector2 queryPoint, out Vector2 sampledPoint, float maxDistance = 2.0f)
        {
            if (NavMesh.SamplePosition(new Vector3(queryPoint.x, queryPoint.y, 0f), out NavMeshHit hit, maxDistance, NavMesh.AllAreas))
            {
                sampledPoint = new Vector2(hit.position.x, hit.position.y);
                return true;
            }

            sampledPoint = queryPoint;
            return false;
        }
    }
}
