using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Combat;
using Combat.Player;

namespace LevelGeneration
{
    /// <summary>
    /// Контроллер боевой зоны уровня:
    /// - На старте входная дверь открыта, выходная заперта.
    /// - При входе игрока в боевой сектор входная дверь захлопывается за спиной.
    /// - Отслеживает гибель всех врагов сектора.
    /// - При победе над всеми врагами открывает вход и выход, позволяя пройти к Кристаллу Победы.
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatSectorLock2D : MonoBehaviour
    {
        private static CombatSectorLock2D _instance;
        public static CombatSectorLock2D Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<CombatSectorLock2D>();
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("--- Двери сектора ---")]
        [SerializeField] private SectorBarrierDoor2D entranceDoor;
        [SerializeField] private SectorBarrierDoor2D exitDoor;

        [Header("--- Состояние ---")]
        [SerializeField] private bool isLocked = false;
        [SerializeField] private bool isCleared = false;
        [SerializeField] private int remainingEnemiesCount = 0;

        private readonly List<EnemyAIController2D> _livingEnemies = new List<EnemyAIController2D>();
        private GameObject _entryTriggerObject;

        // UI уведомление на экране
        private string _bannerText = "";
        private Color _bannerColor = Color.white;
        private float _bannerTimer = 0f;

        public bool IsLocked => isLocked;
        public bool IsCleared => isCleared;
        public int RemainingEnemiesCount => remainingEnemiesCount;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this)
            {
                Destroy(this);
                return;
            }
        }

        private void OnEnable()
        {
            EnemyAIController2D.OnAnyEnemyDied += HandleEnemyDied;
        }

        private void OnDisable()
        {
            EnemyAIController2D.OnAnyEnemyDied -= HandleEnemyDied;
        }

        private void Update()
        {
            if (_bannerTimer > 0f)
            {
                _bannerTimer -= Time.deltaTime;
                if (_bannerTimer <= 0f && isCleared)
                {
                    _bannerText = "";
                }
            }

            // Защитная периодическая проверка на случай, если враг упал в бездну или был уничтожен без вызова события
            if (isLocked && !isCleared)
            {
                _livingEnemies.RemoveAll(e => e == null || e.IsDead);
                remainingEnemiesCount = _livingEnemies.Count;
                if (remainingEnemiesCount == 0)
                {
                    UnlockSector();
                }
            }
        }

        /// <summary>
        /// Инициализация сектора после процедурной генерации уровня
        /// </summary>
        public void InitializeSector(SectorBarrierDoor2D entrance, SectorBarrierDoor2D exit, Vector3 sectorEntryPos, List<GameObject> spawnedEnemies)
        {
            entranceDoor = entrance;
            exitDoor = exit;
            isLocked = false;
            isCleared = false;

            _livingEnemies.Clear();
            if (spawnedEnemies != null)
            {
                for (int i = 0; i < spawnedEnemies.Count; i++)
                {
                    if (spawnedEnemies[i] != null)
                    {
                        var ai = spawnedEnemies[i].GetComponent<EnemyAIController2D>();
                        if (ai != null)
                        {
                            // Отключаем возрождение для врагов процедурного боевого сектора
                            ai.RespawnOnDeath = false;
                            _livingEnemies.Add(ai);
                        }
                    }
                }
            }
            remainingEnemiesCount = _livingEnemies.Count;

            // На старте: Входная дверь ОТКРЫТА, Выходная ЗАКРЫТА
            if (entranceDoor != null) entranceDoor.OpenDoor(instant: true);
            if (exitDoor != null) exitDoor.CloseDoor(instant: true);

            // Создаем триггер фиксации входа игрока на арену (чуть правее порога входа)
            CreateEntryTrigger(sectorEntryPos + new Vector3(1.2f, 0f, 0f));

            _bannerText = "";
            _bannerTimer = 0f;

            Debug.Log($"<color=#00FFAA><b>[CombatSector]</b></color> Боевой сектор инициализирован! Врагов: {remainingEnemiesCount}. Входная дверь открыта.");
        }

        private void CreateEntryTrigger(Vector3 triggerPos)
        {
            if (_entryTriggerObject != null)
            {
                Destroy(_entryTriggerObject);
            }

            _entryTriggerObject = new GameObject("[CombatSector_EntryTrigger]");
            _entryTriggerObject.transform.SetParent(transform, false);
            _entryTriggerObject.transform.position = triggerPos;

            var col = _entryTriggerObject.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1.5f, 10.0f);
            col.offset = new Vector2(0f, 5.0f);

            var triggerHook = _entryTriggerObject.AddComponent<SectorTriggerHook2D>();
            triggerHook.OnPlayerEntered = OnPlayerCrossedEntrance;
        }

        private void OnPlayerCrossedEntrance()
        {
            if (isLocked || isCleared) return;

            LockSector();
        }

        /// <summary>
        /// Захлопнуть двери и начать боевое столкновение
        /// </summary>
        public void LockSector()
        {
            if (isLocked || isCleared) return;

            isLocked = true;

            if (entranceDoor != null) entranceDoor.CloseDoor(instant: false);
            if (exitDoor != null) exitDoor.CloseDoor(instant: false);

            if (_entryTriggerObject != null)
            {
                _entryTriggerObject.SetActive(false);
            }

            ShowBanner($"[!] АРЕНА ЗАБЛОКИРОВАНА: УНИЧТОЖЬТЕ ВРАГОВ! (Осталось: {remainingEnemiesCount})", new Color(1.0f, 0.25f, 0.25f, 1f), 4.0f);
            Debug.Log($"<color=red><b>[CombatSector]</b></color> Игрок вошел в боевой сектор! Двери ЗАХЛОПНУТЫ. Врагов в секторе: {remainingEnemiesCount}");

            // Если по какой-то причине на уровне 0 врагов — сразу открываем
            if (remainingEnemiesCount == 0)
            {
                UnlockSector();
            }
        }

        /// <summary>
        /// Открыть двери после уничтожения всех врагов
        /// </summary>
        public void UnlockSector()
        {
            if (isCleared) return;

            isCleared = true;
            isLocked = false;

            if (entranceDoor != null) entranceDoor.OpenDoor(instant: false);
            if (exitDoor != null) exitDoor.OpenDoor(instant: false);

            ShowBanner("[V] АРЕНА ЗАЧИЩЕНА! ПУТЬ К КРИСТАЛЛУ ОТКРЫТ", new Color(0.1f, 1.0f, 0.6f, 1f), 5.0f);
            Debug.Log("<color=green><b>[CombatSector]</b></color> Все враги уничтожены! Двери сектора ОТКРЫТЫ. Проход свободен.");
        }

        private void HandleEnemyDied(EnemyAIController2D defeatedEnemy)
        {
            if (_livingEnemies.Contains(defeatedEnemy))
            {
                _livingEnemies.Remove(defeatedEnemy);
            }

            _livingEnemies.RemoveAll(e => e == null || e.IsDead);
            remainingEnemiesCount = _livingEnemies.Count;

            if (isLocked && !isCleared)
            {
                if (remainingEnemiesCount > 0)
                {
                    ShowBanner($"[!] Враг повержен! Осталось врагов: {remainingEnemiesCount}", new Color(1.0f, 0.7f, 0.2f, 1f), 2.5f);
                }
                else
                {
                    UnlockSector();
                }
            }
        }

        public void ShowBanner(string text, Color color, float duration)
        {
            _bannerText = text;
            _bannerColor = color;
            _bannerTimer = duration;
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(_bannerText) || _bannerTimer <= 0f) return;

            float width = 620f;
            float height = 54f;
            float x = (Screen.width - width) * 0.5f;
            float y = 30f;

            Color prevBg = GUI.backgroundColor;
            Color prevColor = GUI.color;

            // Стильная плашка оповещения
            GUI.backgroundColor = new Color(0.08f, 0.10f, 0.15f, 0.92f);
            GUI.Box(new Rect(x - 4, y - 4, width + 8, height + 8), GUIContent.none);

            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = _bannerColor;

            GUI.Label(new Rect(x, y, width, height), _bannerText, style);

            GUI.backgroundColor = prevBg;
            GUI.color = prevColor;
        }

        public void ClearSector()
        {
            if (_entryTriggerObject != null)
            {
                Destroy(_entryTriggerObject);
                _entryTriggerObject = null;
            }

            _livingEnemies.Clear();
            isLocked = false;
            isCleared = false;
            remainingEnemiesCount = 0;
            _bannerText = "";
            _bannerTimer = 0f;
        }
    }

    /// <summary>
    /// Вспомогательный триггер пересечения игроком порога сектора
    /// </summary>
    public class SectorTriggerHook2D : MonoBehaviour
    {
        public Action OnPlayerEntered;

        private void OnTriggerEnter2D(Collider2D other)
        {
            bool isPlayer = other.CompareTag("Player") ||
                            other.GetComponentInParent<PlayerController2D>() != null ||
                            other.gameObject.name.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0;

            if (isPlayer)
            {
                OnPlayerEntered?.Invoke();
            }
        }
    }
}
