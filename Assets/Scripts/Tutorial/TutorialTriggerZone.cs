using System;
using UnityEngine;
using Combat.Player;

namespace Combat.Tutorial
{
    /// <summary>
    /// Легковесная триггер-зона для активации этапов обучения при входе игрока.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public class TutorialTriggerZone : MonoBehaviour
    {
        public event Action<PlayerController2D> OnPlayerEntered;

        [Header("--- Settings ---")]
        [Tooltip("Срабатывать только один раз")]
        [SerializeField] private bool triggerOnce = true;

        [Tooltip("Идентификатор зоны (например: EnterDummyRoom, EnterVovaRoom)")]
        [SerializeField] private string zoneId = "";

        private bool _hasTriggered = false;

        public string ZoneId => zoneId;
        public bool HasTriggered => _hasTriggered;

        private void Awake()
        {
            var col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (triggerOnce && _hasTriggered) return;

            var player = other.GetComponent<PlayerController2D>() ?? other.GetComponentInParent<PlayerController2D>();
            if (player == null && (other.CompareTag("Player") || other.transform.root.CompareTag("Player")))
            {
                player = other.transform.root.GetComponentInChildren<PlayerController2D>();
            }

            if (player != null)
            {
                _hasTriggered = true;
                OnPlayerEntered?.Invoke(player);
            }
        }

        public void ResetTrigger()
        {
            _hasTriggered = false;
        }
    }
}
