using System;
using UnityEngine;
using Combat.Player;

namespace LevelGeneration
{
    /// <summary>
    /// Физический односторонний барьер, непреодолимый для врагов.
    /// Автоматически игнорирует коллизии с коллайдерами игрока,
    /// позволяя игроку беспрепятственно входить и выходить из сектора,
    /// но удерживая врагов строго внутри арены в любой момент времени.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public class EnemyBlockerBarrier2D : MonoBehaviour
    {
        private BoxCollider2D _collider;

        public BoxCollider2D BlockerCollider
        {
            get
            {
                if (_collider == null) _collider = GetComponent<BoxCollider2D>();
                return _collider;
            }
        }

        private void Awake()
        {
            EnsureCollider();
            IgnoreAllPlayerColliders();
        }

        private void Start()
        {
            IgnoreAllPlayerColliders();
        }

        private void OnEnable()
        {
            IgnoreAllPlayerColliders();
        }

        private void EnsureCollider()
        {
            if (_collider == null)
            {
                _collider = GetComponent<BoxCollider2D>();
                if (_collider == null)
                {
                    _collider = gameObject.AddComponent<BoxCollider2D>();
                }
            }
            _collider.isTrigger = false;
            gameObject.layer = 2; // Built-in "Ignore Raycast"
        }

        /// <summary>
        /// Настройка геометрии барьера (толщина, высота, смещение)
        /// </summary>
        public void ConfigureGeometry(Vector2 size, Vector2 offset)
        {
            EnsureCollider();
            _collider.size = size;
            _collider.offset = offset;
            _collider.isTrigger = false;
        }

        /// <summary>
        /// Найти игрока в сцене и отключить коллизии между всеми коллайдерами игрока и этим барьером
        /// </summary>
        public void IgnoreAllPlayerColliders()
        {
            EnsureCollider();

            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj == null)
            {
                var pc = FindAnyObjectByType<PlayerController2D>();
                if (pc != null) playerObj = pc.gameObject;
            }

            if (playerObj != null)
            {
                IgnorePlayerObject(playerObj);
            }
        }

        /// <summary>
        /// Отключить коллизии для всех коллайдеров переданного игрока
        /// </summary>
        public void IgnorePlayerObject(GameObject playerObj)
        {
            if (playerObj == null) return;
            EnsureCollider();

            var colliders = playerObj.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && colliders[i] != _collider)
                {
                    Physics2D.IgnoreCollision(colliders[i], _collider, true);
                }
            }
        }

        /// <summary>
        /// Отключить коллизию с конкретным коллайдером
        /// </summary>
        public void IgnoreCollider(Collider2D col)
        {
            if (col == null) return;
            EnsureCollider();
            Physics2D.IgnoreCollision(col, _collider, true);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            CheckAndIgnoreIfPlayer(collision.collider);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            CheckAndIgnoreIfPlayer(collision.collider);
        }

        private void CheckAndIgnoreIfPlayer(Collider2D col)
        {
            if (col == null) return;

            bool isPlayer = col.CompareTag("Player") ||
                            col.GetComponentInParent<PlayerController2D>() != null ||
                            col.gameObject.name.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0;

            if (isPlayer)
            {
                EnsureCollider();
                Physics2D.IgnoreCollision(col, _collider, true);
            }
        }
    }
}
