using System;
using UnityEngine;
using UnityEngine.Events;
using Combat.Player;
using Combat.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Combat
{
    /// <summary>
    /// Быстрый скрипт зоны смерти / смертельного хитбокса.
    /// Вешается на любой пустой объект с коллайдером (триггером или обычным).
    /// При касании игроком немедленно убивает его и выводит экран с надписью "ВЫ УМЕРЛИ".
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public class DeathTrigger2D : MonoBehaviour
    {
        [Header("--- Текст сообщения о гибели ---")]
        [Tooltip("Главный заголовок экрана смерти")]
        [SerializeField] private string deathTitle = "ВЫ УМЕРЛИ";

        [Tooltip("Подзаголовок / подсказка для игрока")]
        [SerializeField] private string deathSubtitle = "Нажмите [ R ], чтобы начать заново";

        [Tooltip("Показывать ли парящий красный текст над игроком в игровом мире")]
        [SerializeField] private bool showWorldFloatingText = true;

        [Tooltip("Показывать ли кинематографичный полноэкранный экран смерти")]
        [SerializeField] private bool showFullscreenDeathScreen = true;

        [Header("--- Параметры уничтожения ---")]
        [Tooltip("Мгновенно убивать игрока (игнорируя щиты и текущее HP)")]
        [SerializeField] private bool killInstantly = true;

        [Tooltip("Урон, если мгновенное убийство отключено")]
        [SerializeField] private float damageIfNotInstant = 9999f;

        [Header("--- Визуализация в Scene View (Gizmos) ---")]
        [Tooltip("Отображать хитбокс зоны смерти в окне Scene View")]
        [SerializeField] private bool showGizmo = true;

        [Tooltip("Цвет подсветки зоны смерти")]
        [SerializeField] private Color gizmoColor = new Color(1f, 0.12f, 0.2f, 0.35f);

        [Header("--- События (UnityEvent) ---")]
        public UnityEvent onPlayerEntered;

        private Collider2D _col;
        private bool _hasTriggered = false;

        private void Reset()
        {
            _col = GetComponent<Collider2D>();
            if (_col == null)
            {
                var box = gameObject.AddComponent<BoxCollider2D>();
                box.size = new Vector2(3f, 1.5f);
                box.isTrigger = true;
                _col = box;
            }
            else
            {
                _col.isTrigger = true;
            }
        }

        private void Awake()
        {
            _col = GetComponent<Collider2D>();
            if (_col != null && !_col.isTrigger)
            {
                _col.isTrigger = true;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            ProcessHit(other.gameObject, other.bounds.center);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            Vector2 contact = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)collision.transform.position;
            ProcessHit(collision.gameObject, contact);
        }

        private void ProcessHit(GameObject hitObj, Vector2 contactPoint)
        {
            if (_hasTriggered && Application.isPlaying) return;

            // Проверяем, является ли объект игроком
            PlayerHealth2D playerHealth = hitObj.GetComponent<PlayerHealth2D>() 
                                       ?? hitObj.GetComponentInParent<PlayerHealth2D>();

            if (playerHealth == null && hitObj.CompareTag("Player"))
            {
                playerHealth = hitObj.GetComponentInChildren<PlayerHealth2D>();
            }

            if (playerHealth == null) return;

            _hasTriggered = true;
            onPlayerEntered?.Invoke();

            Debug.Log($"<color=red><b>[DEATH TRIGGER]</b></color> Игрок коснулся зоны смерти <b>'{gameObject.name}'</b>! Причина: {deathTitle}");

            // 1. Парящий текст над игроком
            if (showWorldFloatingText)
            {
                SpawnFloatingDeathText(playerHealth.transform.position + new Vector3(0f, 1.3f, 0f), deathTitle);
            }

            // 2. Убийство игрока
            if (killInstantly)
            {
                playerHealth.CanDie = true;
                playerHealth.Die();
            }
            else
            {
                playerHealth.TakeHit(
                    new AttackConfig("Зона смерти", CombatZone.Low | CombatZone.Mid | CombatZone.High, Vector2.zero, Vector2.one, 0f, 0.1f, 0f, damageIfNotInstant, Vector2.up * 5f, Color.red),
                    CombatZone.Mid,
                    contactPoint,
                    Vector2.up
                );
            }

            // 3. Показ экрана "ВЫ УМЕРЛИ" с кастомным текстом
            if (showFullscreenDeathScreen)
            {
                PlayerDeathScreenUI.ShowDeathScreen(deathTitle, deathSubtitle);
            }
        }

        private void SpawnFloatingDeathText(Vector3 pos, string text)
        {
            var go = new GameObject("Death_Popup_Text");
            go.transform.position = pos;

            var tm = go.AddComponent<TextMesh>();
            tm.text = $"☠ {text} ☠";
            tm.fontSize = 52;
            tm.characterSize = 0.085f;
            tm.alignment = TextAlignment.Center;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.fontStyle = FontStyle.Bold;
            tm.color = new Color(1f, 0.15f, 0.2f, 1f);

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 3500;

            var comp = go.AddComponent<DeathPopupFade>();
            comp.Init(tm);
        }

        private void OnDrawGizmos()
        {
            if (!showGizmo) return;
            if (_col == null) _col = GetComponent<Collider2D>();
            if (_col == null) return;

            Bounds b = _col.bounds;

            // Заливка хитбокса
            Gizmos.color = gizmoColor;
            Gizmos.DrawCube(b.center, b.size);

            // Контур
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.95f);
            Gizmos.DrawWireCube(b.center, b.size);

#if UNITY_EDITOR
            // Текстовая метка в Scene View
            var labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.25f, 0.25f, 1f) }
            };
            Handles.Label(b.center, $"☠ ЗОНА СМЕРТИ\n({deathTitle})", labelStyle);
#endif
        }

#if UNITY_EDITOR
        [MenuItem("GameObject/2D Object/💀 Зона смерти (Killbox - Вы умерли)", false, 10)]
        public static void CreateDeathTriggerMenu(MenuCommand menuCommand)
        {
            var go = new GameObject("DeathZone_Killbox");
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(4f, 2f);
            box.isTrigger = true;

            go.AddComponent<DeathTrigger2D>();

            // Если кликнули ПКМ по объекту в иерархии, делаем дочерним
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(go, "Create Death Trigger");
            Selection.activeGameObject = go;

            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }

            Debug.Log("<color=red><b>[DeathTrigger2D]</b></color> Создан пустой объект со смертельным хитбоксом!");
        }
#endif
    }

    /// <summary>
    /// Простой компонент плавного растворения и подъема парящего текста
    /// </summary>
    internal class DeathPopupFade : MonoBehaviour
    {
        private TextMesh _tm;
        private float _elapsed = 0f;
        private const float Duration = 2.0f;
        private Vector3 _startPos;

        public void Init(TextMesh tm)
        {
            _tm = tm;
            _startPos = transform.position;
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_elapsed / Duration);

            transform.position = _startPos + new Vector3(0f, t * 1.5f, 0f);

            if (_tm != null)
            {
                Color c = _tm.color;
                c.a = Mathf.Clamp01(1f - t);
                _tm.color = c;
            }

            if (_elapsed >= Duration)
            {
                Destroy(gameObject);
            }
        }
    }
}
