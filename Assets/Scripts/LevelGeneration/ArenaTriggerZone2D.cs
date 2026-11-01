using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace LevelGeneration
{
    public enum TriggerTargetType
    {
        [Tooltip("Реагирует только на игрока")]
        PlayerOnly = 0,

        [Tooltip("Реагирует только на врагов")]
        EnemiesOnly = 1,

        [Tooltip("Реагирует и на игрока, и на врагов")]
        PlayerAndEnemies = 2
    }

    public enum TriggerExecutionMode
    {
        [Tooltip("Срабатывает ровно один раз за забег")]
        Once = 0,

        [Tooltip("Срабатывает каждый раз при входе (с учетом кулдауна)")]
        Repeatable = 1
    }

    /// <summary>
    /// Универсальная зона-триггер для уникальных событий на аренах (засады, кат-сцены,
    /// закрытие дверей, активация ловушек, появление платформ, вывод сообщений).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public class ArenaTriggerZone2D : MonoBehaviour
    {
        [Header("--- Настройки активации ---")]
        [Tooltip("На кого реагирует триггер")]
        [SerializeField] private TriggerTargetType targetType = TriggerTargetType.PlayerOnly;

        [Tooltip("Режим срабатывания")]
        [SerializeField] private TriggerExecutionMode executionMode = TriggerExecutionMode.Once;

        [Tooltip("Кулдаун между срабатываниями (для режима Repeatable)")]
        [SerializeField] private float cooldown = 1.0f;

        [Header("--- Быстрые действия без кода ---")]
        [Tooltip("Объекты, которые нужно включить (SetActive = true) при срабатывании")]
        [SerializeField] private List<GameObject> objectsToActivate = new List<GameObject>();

        [Tooltip("Объекты, которые нужно выключить (SetActive = false) при срабатывании")]
        [SerializeField] private List<GameObject> objectsToDeactivate = new List<GameObject>();

        [Tooltip("Ловушки, которые нужно активировать")]
        [SerializeField] private List<ArenaHazard2D> hazardsToTrigger = new List<ArenaHazard2D>();

        [Header("--- Экранное сообщение ---")]
        [Tooltip("Показывать ли текстовый баннер на экране")]
        [SerializeField] private bool showBanner = false;

        [Tooltip("Текст баннера")]
        [SerializeField] private string bannerText = "[!] ВНИМАНИЕ: ЗАСАДА!";

        [Tooltip("Цвет баннера")]
        [SerializeField] private Color bannerColor = new Color(1f, 0.4f, 0.1f, 1f);

        [Tooltip("Длительность показа баннера")]
        [SerializeField] private float bannerDuration = 3f;

        [Header("--- Аудио ---")]
        [Tooltip("Звук при активации")]
        [SerializeField] private AudioClip triggerSound;

        [Header("--- Визуализация в Редакторе ---")]
        [SerializeField] private Color gizmoColor = new Color(0.2f, 1f, 0.5f, 0.35f);
        [SerializeField] private string triggerLabel = "Триггер";

        [Header("--- События (UnityEvent) ---")]
        public UnityEvent<GameObject> onTriggerEntered;
        public UnityEvent<GameObject> onTriggerExited;

        private Collider2D _col;
        private bool _hasTriggered = false;
        private float _lastTriggerTime = -999f;

        private void Awake()
        {
            _col = GetComponent<Collider2D>();
            _col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_hasTriggered && executionMode == TriggerExecutionMode.Once) return;
            if (Time.time - _lastTriggerTime < cooldown) return;

            bool isPlayer = other.CompareTag("Player");
            bool isEnemy = other.CompareTag("Enemy");

            if (targetType == TriggerTargetType.PlayerOnly && !isPlayer) return;
            if (targetType == TriggerTargetType.EnemiesOnly && !isEnemy) return;
            if (targetType == TriggerTargetType.PlayerAndEnemies && !isPlayer && !isEnemy) return;

            ExecuteTrigger(other.gameObject);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            bool isPlayer = other.CompareTag("Player");
            bool isEnemy = other.CompareTag("Enemy");

            if (targetType == TriggerTargetType.PlayerOnly && !isPlayer) return;
            if (targetType == TriggerTargetType.EnemiesOnly && !isEnemy) return;

            onTriggerExited?.Invoke(other.gameObject);
        }

        private void ExecuteTrigger(GameObject triggeringObject)
        {
            _hasTriggered = true;
            _lastTriggerTime = Time.time;

            // 1. Активация объектов
            for (int i = 0; i < objectsToActivate.Count; i++)
            {
                if (objectsToActivate[i] != null) objectsToActivate[i].SetActive(true);
            }

            // 2. Деактивация объектов
            for (int i = 0; i < objectsToDeactivate.Count; i++)
            {
                if (objectsToDeactivate[i] != null) objectsToDeactivate[i].SetActive(false);
            }

            // 3. Активация ловушек
            for (int i = 0; i < hazardsToTrigger.Count; i++)
            {
                if (hazardsToTrigger[i] != null) hazardsToTrigger[i].TriggerHazard();
            }

            // 4. Баннер
            if (showBanner && !string.IsNullOrEmpty(bannerText))
            {
                if (CombatSectorLock2D.Instance != null)
                {
                    CombatSectorLock2D.Instance.ShowBanner(bannerText, bannerColor, bannerDuration);
                }
            }

            // 5. Звук
            if (triggerSound != null)
            {
                AudioSource.PlayClipAtPoint(triggerSound, transform.position);
            }

            // 6. UnityEvent
            onTriggerEntered?.Invoke(triggeringObject);
        }

        /// <summary>
        /// Сбросить состояние триггера для нового забега
        /// </summary>
        public void ResetTrigger()
        {
            _hasTriggered = false;
            _lastTriggerTime = -999f;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            var col = GetComponent<Collider2D>();
            Vector3 center = transform.position;
            Vector3 size = Vector3.one;

            if (col is BoxCollider2D box)
            {
                center = transform.position + (Vector3)box.offset;
                size = box.size;
                Gizmos.DrawCube(center, size);
                Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.9f);
                Gizmos.DrawWireCube(center, size);
            }
            else
            {
                Gizmos.DrawSphere(center, 0.6f);
            }

            UnityEditor.Handles.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
            UnityEditor.Handles.Label(center + Vector3.up * (size.y * 0.5f + 0.3f), $"[{triggerLabel}]");
        }
#endif
    }
}
