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
        private Vector3 _sectorEntryPos;
        private Vector3 _sectorExitPos;
        private Transform _playerTransform;
        private bool _isSubscribed = false;

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
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        public void SubscribeEvents()
        {
            if (_isSubscribed) return;
            _isSubscribed = true;
            EnemyAIController2D.OnAnyEnemyDied += HandleEnemyDied;
            PlayerHealth2D.OnAnyPlayerDeath += HandlePlayerDiedOrRespawned;
            PlayerHealth2D.OnAnyPlayerRespawn += HandlePlayerDiedOrRespawned;
        }

        public void UnsubscribeEvents()
        {
            if (!_isSubscribed) return;
            _isSubscribed = false;
            EnemyAIController2D.OnAnyEnemyDied -= HandleEnemyDied;
            PlayerHealth2D.OnAnyPlayerDeath -= HandlePlayerDiedOrRespawned;
            PlayerHealth2D.OnAnyPlayerRespawn -= HandlePlayerDiedOrRespawned;
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
                    return;
                }

                // Защитная проверка положения игрока: если игрок возродился или оказался снаружи перед входом
                if (_playerTransform == null)
                {
                    var playerObj = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
                    if (playerObj != null) _playerTransform = playerObj.transform;
                }

                if (_playerTransform != null && _playerTransform.position.x < _sectorEntryPos.x - 0.4f)
                {
                    HandlePlayerDiedOrRespawned();
                }
            }
        }

        private void FixedUpdate()
        {
            if (isCleared || _livingEnemies.Count == 0) return;

            float minEnemyX = _sectorEntryPos.x + 0.35f;
            float maxEnemyX = (_sectorExitPos.sqrMagnitude > 0.01f) ? (_sectorExitPos.x - 0.35f) : float.MaxValue;

            for (int i = 0; i < _livingEnemies.Count; i++)
            {
                var enemy = _livingEnemies[i];
                if (enemy == null || enemy.IsDead) continue;

                var t = enemy.transform;
                Vector3 pos = t.position;
                bool clamped = false;

                if (pos.x < minEnemyX)
                {
                    pos.x = minEnemyX;
                    clamped = true;
                }
                else if (pos.x > maxEnemyX)
                {
                    pos.x = maxEnemyX;
                    clamped = true;
                }

                if (clamped)
                {
                    t.position = pos;
                    var rb = enemy.GetComponent<Rigidbody2D>();
                    if (rb != null)
                    {
                        float vx = rb.linearVelocity.x;
                        if ((pos.x <= minEnemyX && vx < 0f) || (pos.x >= maxEnemyX && vx > 0f))
                        {
                            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                        }
                    }
                }
            }
        }

        private void HandlePlayerDiedOrRespawned()
        {
            if (isCleared) return;

            // Игрок погиб или возродился: открываем входную дверь и активируем триггер входа заново
            if (isLocked)
            {
                isLocked = false;
                if (entranceDoor != null) entranceDoor.OpenDoor(instant: true);
                if (_entryTriggerObject != null) _entryTriggerObject.SetActive(true);
                RegisterPlayerWithDoors();
                ShowBanner($"[!] ВХОД НА АРЕНУ ОТКРЫТ (Осталось врагов: {remainingEnemiesCount})", new Color(1.0f, 0.65f, 0.2f, 1f), 3.0f);
                Debug.Log("<color=yellow><b>[CombatSector]</b></color> Игрок погиб/возродился. Входная дверь открыта для повторного входа на арену.");
            }
            else
            {
                RegisterPlayerWithDoors();
            }
        }

        public void RegisterPlayerWithDoors()
        {
            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj == null && _playerTransform != null) playerObj = _playerTransform.gameObject;
            if (playerObj != null)
            {
                if (entranceDoor != null) entranceDoor.IgnorePlayerObject(playerObj);
                if (exitDoor != null) exitDoor.IgnorePlayerObject(playerObj);
            }
        }

        public void InitializeSector(SectorBarrierDoor2D entrance, SectorBarrierDoor2D exit, Vector3 sectorEntryPos, List<GameObject> spawnedEnemies)
        {
            InitializeSector(entrance, exit, sectorEntryPos, Vector3.zero, spawnedEnemies);
        }

        /// <summary>
        /// Инициализация сектора после процедурной генерации уровня
        /// </summary>
        public void InitializeSector(SectorBarrierDoor2D entrance, SectorBarrierDoor2D exit, Vector3 sectorEntryPos, Vector3 sectorExitPos, List<GameObject> spawnedEnemies)
        {
            entranceDoor = entrance;
            exitDoor = exit;
            _sectorEntryPos = sectorEntryPos;
            _sectorExitPos = sectorExitPos;
            isLocked = false;
            isCleared = false;
            SubscribeEvents();

            float minEnemyX = _sectorEntryPos.x + 0.35f;
            float maxEnemyX = (_sectorExitPos.sqrMagnitude > 0.01f) ? (_sectorExitPos.x - 0.35f) : float.MaxValue;

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
                            ai.SetSectorBounds(minEnemyX, maxEnemyX);
                            _livingEnemies.Add(ai);
                        }
                    }
                }
            }
            remainingEnemiesCount = _livingEnemies.Count;

            // На старте: Входная дверь ОТКРЫТА, Выходная ЗАКРЫТА
            if (entranceDoor != null) entranceDoor.OpenDoor(instant: true);
            if (exitDoor != null) exitDoor.CloseDoor(instant: true);

            // Настраиваем игнорирование коллизий барьеров с игроком
            RegisterPlayerWithDoors();

            // Создаем триггер фиксации входа игрока на арену (чуть правее порога входа)
            CreateEntryTrigger(sectorEntryPos + new Vector3(1.2f, 0f, 0f));

            _bannerText = "";
            _bannerTimer = 0f;

            Debug.Log($"<color=#00FFAA><b>[CombatSector]</b></color> Боевой сектор инициализирован! Врагов: {remainingEnemiesCount}. Входная дверь открыта.");
        }

        private void CreateEntryTrigger(Vector3 triggerPos)
        {
            CleanupExistingTriggers();

            _entryTriggerObject = new GameObject("[CombatSector_EntryTrigger]");
            Transform parentTransform = entranceDoor != null ? entranceDoor.transform : transform;
            _entryTriggerObject.transform.SetParent(parentTransform, false);
            _entryTriggerObject.transform.position = triggerPos;

            var col = _entryTriggerObject.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1.5f, 10.0f);
            col.offset = new Vector2(0f, 5.0f);

            var triggerHook = _entryTriggerObject.AddComponent<SectorTriggerHook2D>();
            triggerHook.OnPlayerEntered = OnPlayerCrossedEntrance;
        }

        private void CleanupExistingTriggers()
        {
            var oldHooks = FindObjectsByType<SectorTriggerHook2D>();
            for (int i = 0; i < oldHooks.Length; i++)
            {
                if (oldHooks[i] != null)
                {
                    if (Application.isPlaying) Destroy(oldHooks[i].gameObject);
                    else DestroyImmediate(oldHooks[i].gameObject);
                }
            }
            _entryTriggerObject = null;
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
            CleanupExistingTriggers();

            _livingEnemies.Clear();
            isLocked = false;
            isCleared = false;
            remainingEnemiesCount = 0;
            _bannerText = "";
            _bannerTimer = 0f;
        }
    }
}
