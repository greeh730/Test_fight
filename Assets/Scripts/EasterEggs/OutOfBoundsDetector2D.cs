using Combat.Player;
using System;
using UnityEngine;

namespace EasterEggs
{
    /// <summary>
    /// Детектор нахождения игрока за пределами карты (Out of Bounds).
    /// Использует гибридную систему проверки:
    /// 1. Пересечение с триггерами OutOfBoundsZone2D (зоны над решётками и дырами в коллизии).
    /// 2. Превышение безопасной высоты потолка (Y > ceilingHeightThreshold).
    /// Имеет задержку гистерезиса для защиты от ложных срабатываний и мерцаний.
    /// </summary>
    [DisallowMultipleComponent]
    public class OutOfBoundsDetector2D : MonoBehaviour
    {
        public static OutOfBoundsDetector2D Instance { get; private set; }

        public event Action<bool> OnOutOfBoundsChanged;

        [Header("--- Target Player ---")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private Collider2D playerCollider;

        [Header("--- Height Boundary Settings ---")]
        [Tooltip("Включить автоматическую проверку превышения высоты потолка (отключено по умолчанию для предотвращения ложных срабатываний при прыжках)")]
        [SerializeField] private bool enableCeilingHeightCheck = false;

        [Tooltip("Пороговая высота Y. Если игрок выше этой координаты — он находится на крыше/за картой")]
        [SerializeField] private float ceilingHeightThreshold = 3.9f;

        [Header("--- Stability / Debounce ---")]
        [Tooltip("Минимальное время непрерывного нахождения за картой перед активацией Роберта")]
        [SerializeField] private float enterCooldown = 0.15f;

        [Tooltip("Задержка подтверждения возвращения игрока обратно в комнату")]
        [SerializeField] private float exitCooldown = 0.35f;

        private bool _isOutOfBounds = false;
        private float _outTimer = 0f;
        private float _inTimer = 0f;

        public bool IsOutOfBounds => _isOutOfBounds;
        public float CeilingHeightThreshold { get => ceilingHeightThreshold; set => ceilingHeightThreshold = value; }
        public bool EnableCeilingHeightCheck { get => enableCeilingHeightCheck; set => enableCeilingHeightCheck = value; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            EnsurePlayerReference();
        }

        private void Start()
        {
            EnsurePlayerReference();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void EnsurePlayerReference()
        {
            if (playerTransform == null)
            {
                var p = FindAnyObjectByType<PlayerController2D>();
                if (p != null)
                {
                    playerTransform = p.transform;
                    playerCollider = p.GetComponent<Collider2D>();
                }
            }
        }

        private void Update()
        {
            if (playerTransform == null)
            {
                EnsurePlayerReference();
                if (playerTransform == null) return;
            }

            bool rawOutOfBounds = CheckRawOutOfBounds();

            if (rawOutOfBounds)
            {
                _inTimer = 0f;
                _outTimer += Time.deltaTime;

                if (!_isOutOfBounds && _outTimer >= enterCooldown)
                {
                    SetOutOfBoundsState(true);
                }
            }
            else
            {
                _outTimer = 0f;
                _inTimer += Time.deltaTime;

                if (_isOutOfBounds && _inTimer >= exitCooldown)
                {
                    SetOutOfBoundsState(false);
                }
            }
        }

        private bool CheckRawOutOfBounds()
        {
            // 1. Проверка по триггерным зонам OutOfBoundsZone2D
            if (playerCollider != null && OutOfBoundsZone2D.IsColliderInAnyZone(playerCollider))
            {
                return true;
            }

            if (OutOfBoundsZone2D.IsPointInAnyZone(playerTransform.position))
            {
                return true;
            }

            // 2. Проверка по высоте потолка (игрок вылез на крышу комнаты)
            if (enableCeilingHeightCheck && playerTransform.position.y >= ceilingHeightThreshold)
            {
                return true;
            }

            return false;
        }

        private void SetOutOfBoundsState(bool outOfBounds)
        {
            if (_isOutOfBounds == outOfBounds) return;

            _isOutOfBounds = outOfBounds;
            OnOutOfBoundsChanged?.Invoke(_isOutOfBounds);

            if (_isOutOfBounds)
            {
                if (RobertGuardian2D.Instance == null)
                {
                    var robertPrefab = Resources.Load<GameObject>("Prefabs/Robert_Guardian");
                    if (robertPrefab != null)
                    {
                        var go = Instantiate(robertPrefab);
                        go.name = "Robert_Guardian";
                    }
                }
                Debug.Log($"<color=#FF0044><b>[OUT OF BOUNDS]</b></color> Игрок вышел за пределы карты! Позиция: {playerTransform.position}");
            }
            else
            {
                Debug.Log("<color=#00FFAA><b>[IN BOUNDS]</b></color> Игрок вернулся в пределы карты!");
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (enableCeilingHeightCheck)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.7f);
                Vector3 center = transform.position;
                center.y = ceilingHeightThreshold;
                Gizmos.DrawLine(center + Vector3.left * 100f, center + Vector3.right * 100f);
            }
        }
    }
}
