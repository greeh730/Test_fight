using System;
using UnityEngine;

namespace Combat.Tutorial
{
    /// <summary>
    /// Физическая зона триггера обучения (BoxCollider2D IsTrigger).
    /// Позволяет визуально настраивать размеры и положение зоны проверки прямо в Scene View Unity.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public class TutorialTriggerZone2D : MonoBehaviour
    {
        [Header("--- Настройки триггера ---")]
        [Tooltip("Название цели или подсказка в редакторе")]
        [SerializeField] private string triggerLabel = "Зона завершения Этапа 1 (Передвижение)";

        [Tooltip("Тег объекта, который может активировать триггер (по умолчанию 'Player')")]
        [SerializeField] private string targetTag = "Player";

        [Tooltip("Цвет отображения зоны в окне Scene")]
        [SerializeField] private Color gizmoColor = new Color(0.2f, 0.95f, 1f, 0.35f);

        [Header("--- Состояние ---")]
        [Tooltip("Срабатывать только один раз")]
        [SerializeField] private bool triggerOnce = true;
        private bool _isTriggered = false;

        public event Action<Collider2D> OnPlayerEntered;

        private BoxCollider2D _boxCollider;

        public bool IsTriggered => _isTriggered;
        public BoxCollider2D BoxCollider => _boxCollider != null ? _boxCollider : (_boxCollider = GetComponent<BoxCollider2D>());

        private void Awake()
        {
            _boxCollider = GetComponent<BoxCollider2D>();
            if (_boxCollider != null)
            {
                _boxCollider.isTrigger = true;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_isTriggered && triggerOnce) return;

            if (other.CompareTag(targetTag) || other.GetComponent<Combat.Player.PlayerController2D>() != null)
            {
                _isTriggered = true;
                Debug.Log($"<color=#00FF88><b>[TUTORIAL TRIGGER]</b></color> Игрок вошел в триггер: <b>{triggerLabel}</b>");
                OnPlayerEntered?.Invoke(other);
            }
        }

        public void ResetTrigger()
        {
            _isTriggered = false;
        }

        private void OnDrawGizmos()
        {
            var col = GetComponent<BoxCollider2D>();
            if (col == null) return;

            Vector3 center = transform.position + (Vector3)col.offset;
            Vector3 size = new Vector3(col.size.x * transform.lossyScale.x, col.size.y * transform.lossyScale.y, 1f);

            // Полупрозрачный заполненный бокс
            Gizmos.color = gizmoColor;
            Gizmos.DrawCube(center, size);

            // Четкая рамка
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.95f);
            Gizmos.DrawWireCube(center, size);

#if UNITY_EDITOR
            // Текстовая метка в Scene View
            UnityEditor.Handles.Label(center + Vector3.up * (size.y * 0.5f + 0.35f), $"🏁 {triggerLabel}");
#endif
        }
    }
}
