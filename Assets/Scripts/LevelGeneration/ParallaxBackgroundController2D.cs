using System;
using UnityEngine;

namespace Combat.Background
{
    /// <summary>
    /// Контроллер кинематографичного многослойного параллакс-фона для Abyss of Emotions.
    /// Обеспечивает:
    /// 1. Автоматическое следование за камерой с идеальным покрытием вьюпорта.
    /// 2. Двухслойный независимый параллакс при перемещении камеры (Far и Mid слои).
    /// 3. Плавный непрерывный дрейф фона в простое (живая атмосфера бездны).
    /// 4. Градиент глубины по высоте (погружение в темную бездну внизу и мягкое свечение вверху).
    /// 5. Мягкие частицы пыли/спор бездны в воздухе.
    /// Работает как в Play Mode, так и в режиме редактирования ([ExecuteAlways]).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ParallaxBackgroundController2D : MonoBehaviour
    {
        [Header("--- Камера и привязка ---")]
        [Tooltip("Камера слежения (если не указана, берется Camera.main)")]
        [SerializeField] private Camera targetCamera;

        [Tooltip("Смещение по Z относительно камеры")]
        [SerializeField] private float zOffsetFromCamera = 20f;

        [Tooltip("Запас масштаба по краям камеры (чтобы не было видно краев)")]
        [SerializeField] private float viewportPadding = 1.35f;

        [Header("--- Материал фона ---")]
        [SerializeField] private Material backgroundMaterial;

        [Header("--- Параллакс стены (Wall Parallax) ---")]
        [Tooltip("Коэффициент параллакса стены фона при движении камеры")]
        [SerializeField] private Vector2 parallaxFactor = new Vector2(0.12f, 0.05f);

        [Tooltip("Скорость непрерывного дрейфа (для кирпичной стены по умолчанию 0)")]
        [SerializeField] private Vector2 driftSpeed = Vector2.zero;

        // Сохранение обратной совместимости сериализации
        [SerializeField, HideInInspector] private Vector2 farParallaxFactor = new Vector2(0.12f, 0.05f);
        [SerializeField, HideInInspector] private Vector2 farDriftSpeed = Vector2.zero;
        [SerializeField, HideInInspector] private Vector2 midParallaxFactor = new Vector2(0.12f, 0.05f);
        [SerializeField, HideInInspector] private Vector2 midDriftSpeed = Vector2.zero;

        [Header("--- Сортировка 2D ---")]
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int sortingOrder = -100;

        [Header("--- Атмосферные частицы пыли ---")]
        [Tooltip("Создавать ли плавающие частицы пыли/спор в воздухе")]
        [SerializeField] private bool enableAmbientDust = true;
        [Tooltip("Физический ассет материала для частиц пыли бездны (URP Particles/Unlit)")]
        [SerializeField] private Material dustParticleMaterial;
        [SerializeField] private ParticleSystem dustParticleSystem;

        // Внутреннее состояние
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MaterialPropertyBlock _propBlock;
        private Vector2 _accumulatedDrift = Vector2.zero;
        private float _lastTime = 0f;

        private static readonly int PropOffset = Shader.PropertyToID("_Offset");
        private static readonly int PropOffset1 = Shader.PropertyToID("_Offset1");
        private static readonly int PropOffset2 = Shader.PropertyToID("_Offset2");

        private void Awake()
        {
            InitializeComponents();
        }

        private void OnEnable()
        {
            InitializeComponents();
            EnsureQuadMesh();
            EnsureDustParticles();
            _lastTime = Application.isPlaying ? Time.time : 0f;
        }

        private void InitializeComponents()
        {
            if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
            if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();
            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

            if (_meshRenderer != null)
            {
                _meshRenderer.sortingLayerName = sortingLayerName;
                _meshRenderer.sortingOrder = sortingOrder;

                if (backgroundMaterial != null && _meshRenderer.sharedMaterial != backgroundMaterial)
                {
                    _meshRenderer.sharedMaterial = backgroundMaterial;
                }
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        private void EnsureQuadMesh()
        {
            if (_meshFilter == null) return;
            if (_meshFilter.sharedMesh == null)
            {
                _meshFilter.sharedMesh = CreateUnitQuadMesh();
            }
        }

        private Mesh CreateUnitQuadMesh()
        {
            var mesh = new Mesh { name = "ParallaxQuad" };
            mesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3( 0.5f, -0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f),
                new Vector3( 0.5f,  0.5f, 0f)
            };
            mesh.uv = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };
            mesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateNormals();
            return mesh;
        }

        private void EnsureDustMaterial()
        {
            if (dustParticleMaterial == null)
            {
                dustParticleMaterial = Resources.Load<Material>("M_AmbientDustParticle");
#if UNITY_EDITOR
                if (dustParticleMaterial == null)
                {
                    dustParticleMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_AmbientDustParticle.mat");
                }
#endif
            }
        }

        private void EnsureDustParticles()
        {
            if (!enableAmbientDust)
            {
                if (dustParticleSystem != null && dustParticleSystem.gameObject != null)
                {
                    dustParticleSystem.gameObject.SetActive(false);
                }
                return;
            }

            EnsureDustMaterial();

            if (dustParticleSystem == null)
            {
                var existingChild = transform.Find("Ambient_Abyss_Dust");
                if (existingChild != null)
                {
                    dustParticleSystem = existingChild.GetComponent<ParticleSystem>();
                }
                else
                {
                    var go = new GameObject("Ambient_Abyss_Dust");
                    go.transform.SetParent(transform, false);
                    dustParticleSystem = SetupDustParticleSystem(go);
                }
            }

            if (dustParticleSystem != null)
            {
                var psr = dustParticleSystem.GetComponent<ParticleSystemRenderer>();
                if (psr != null && dustParticleMaterial != null && psr.sharedMaterial != dustParticleMaterial)
                {
                    psr.sharedMaterial = dustParticleMaterial;
                }
                dustParticleSystem.gameObject.SetActive(true);
            }
        }

        private ParticleSystem SetupDustParticleSystem(GameObject go)
        {
            var ps = go.AddComponent<ParticleSystem>();
            var psr = go.GetComponent<ParticleSystemRenderer>();

            psr.sortingLayerName = sortingLayerName;
            psr.sortingOrder = sortingOrder + 10; // прямо перед фоном, позади платформ
            
            EnsureDustMaterial();
            if (dustParticleMaterial != null)
            {
                psr.sharedMaterial = dustParticleMaterial;
            }

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.maxParticles = 60;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 12f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.4f, 0.7f, 1.0f, 0.45f),
                new Color(0.7f, 0.9f, 1.0f, 0.75f)
            );
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 8f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(32f, 18f, 1f);

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.x = new ParticleSystem.MinMaxCurve(-0.35f, -0.1f); // медленный снос ветром влево
            vel.y = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.4f, 0.3f), new GradientAlphaKey(0.4f, 0.7f), new GradientAlphaKey(0f, 1f) }
            );
            col.color = grad;

            return ps;
        }

        private void LateUpdate()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
                if (targetCamera == null) return;
            }

            // 1. Позиционирование и масштабирование под вьюпорт камеры
            Vector3 camPos = targetCamera.transform.position;
            transform.position = new Vector3(camPos.x, camPos.y, camPos.z + zOffsetFromCamera);

            float camHeight = targetCamera.orthographicSize * 2.0f;
            float camWidth = camHeight * targetCamera.aspect;
            transform.localScale = new Vector3(camWidth * viewportPadding, camHeight * viewportPadding, 1f);

            // 2. Расчет времени для плавного дрейфа
            float currentTime = Application.isPlaying ? Time.time : (float)DateTime.Now.TimeOfDay.TotalSeconds;
            float deltaTime = 0f;
            if (_lastTime > 0f)
            {
                deltaTime = Mathf.Clamp(currentTime - _lastTime, 0f, 0.1f);
            }
            _lastTime = currentTime;

            _accumulatedDrift += driftSpeed * deltaTime;

            // 3. Вычисление смещений UV для стены фона
            Vector4 offset = new Vector4(
                camPos.x * parallaxFactor.x + _accumulatedDrift.x,
                camPos.y * parallaxFactor.y + _accumulatedDrift.y,
                0f, 0f
            );

            // 4. Передача параметров в шейдер через MaterialPropertyBlock
            if (_meshRenderer != null)
            {
                _meshRenderer.GetPropertyBlock(_propBlock);
                _propBlock.SetVector(PropOffset, offset);
                _propBlock.SetVector(PropOffset1, offset);
                _meshRenderer.SetPropertyBlock(_propBlock);
            }
        }

        public void SetTargetCamera(Camera cam)
        {
            targetCamera = cam;
        }

        public void SetBackgroundMaterial(Material mat)
        {
            backgroundMaterial = mat;
            if (_meshRenderer != null)
            {
                _meshRenderer.sharedMaterial = mat;
            }
        }
    }
}
