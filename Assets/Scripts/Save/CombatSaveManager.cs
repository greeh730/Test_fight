using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Combat.Player;
using Combat.Tactician;
using Combat.Stances;

namespace Combat.Save
{
    /// <summary>
    /// Главный менеджер сохранения и загрузки состояния игры.
    /// Сериализует состояние Игрока, Врага и Манекена в JSON.
    /// Предоставляет методы для Меню Паузы и горячие клавиши (F5 / F9).
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatSaveManager : MonoBehaviour
    {
        public static CombatSaveManager Instance { get; private set; }

        public static event Action<bool, string> OnSaveCompleted;
        public static event Action<bool, string> OnLoadCompleted;

        [Header("--- Hotkeys ---")]
        [SerializeField] private KeyCode quickSaveKey = KeyCode.F5;
        [SerializeField] private KeyCode quickLoadKey = KeyCode.F9;

        [Header("--- Save Settings ---")]
        [SerializeField] private string defaultSaveSlot = "quicksave";

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
        }

        private void Update()
        {
            if (IsKeyPressed(quickSaveKey))
            {
                SaveGame();
            }
            else if (IsKeyPressed(quickLoadKey))
            {
                LoadGame();
            }
        }

        private static bool IsKeyPressed(KeyCode key)
        {
            if (key == KeyCode.None) return false;
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (key == KeyCode.F5 && kb.f5Key.wasPressedThisFrame) return true;
                if (key == KeyCode.F9 && kb.f9Key.wasPressedThisFrame) return true;
            }
#endif
            try
            {
                return Input.GetKeyDown(key);
            }
            catch
            {
                return false;
            }
        }

        public string GetSaveFilePath(string slotName = null)
        {
            string slot = string.IsNullOrEmpty(slotName) ? defaultSaveSlot : slotName;
            return Path.Combine(Application.persistentDataPath, $"{slot}.json");
        }

        public bool HasSave(string slotName = null)
        {
            return File.Exists(GetSaveFilePath(slotName));
        }

        /// <summary>
        /// Выполняет сохранение текущего состояния мира.
        /// </summary>
        public bool SaveGame(string slotName = null)
        {
            try
            {
                var saveData = new GameSaveData
                {
                    saveTimestamp = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"),
                    sceneName = SceneManager.GetActiveScene().name
                };

                var player = FindAnyObjectByType<PlayerCombatController2D>();
                var playerHealth = FindAnyObjectByType<PlayerHealth2D>();
                var playerStamina = FindAnyObjectByType<PlayerStamina2D>();
                if (player != null)
                {
                    saveData.player.Position = player.transform.position;
                    saveData.player.currentStance = (int)player.CurrentStance;
                    saveData.player.facingDirection = player.FacingDirection;
                }
                if (playerHealth != null)
                {
                    saveData.player.health = playerHealth.CurrentHealth;
                    saveData.player.maxHealth = playerHealth.MaxHealth;
                    saveData.player.isDead = playerHealth.IsDead;
                }
                if (playerStamina != null)
                {
                    saveData.player.stamina = playerStamina.CurrentStamina;
                    saveData.player.maxStamina = playerStamina.MaxStamina;
                    saveData.player.isExhausted = playerStamina.IsExhausted;
                }

                // 2. Сохранение Врага
                var enemy = FindAnyObjectByType<EnemyAIController2D>();
                if (enemy != null)
                {
                    saveData.enemy.Position = enemy.transform.position;
                    saveData.enemy.health = enemy.CurrentHealth;
                    saveData.enemy.maxHealth = enemy.MaxHealth;
                    saveData.enemy.stamina = enemy.CurrentStamina;
                    saveData.enemy.maxStamina = enemy.MaxStamina;
                    saveData.enemy.isDead = enemy.IsDead;
                    saveData.enemy.facingDirection = enemy.FacingDirection;
                }

                // 3. Сохранение Манекена
                var dummy = FindAnyObjectByType<CombatDummy2D>();
                if (dummy != null)
                {
                    saveData.dummy.Position = dummy.transform.position;
                    saveData.dummy.health = dummy.CurrentHealth;
                    saveData.dummy.maxHealth = dummy.MaxHealth;
                    saveData.dummy.isDead = dummy.IsDead;
                }

                // Запись в файл
                string filePath = GetSaveFilePath(slotName);
                string json = JsonUtility.ToJson(saveData, true);
                File.WriteAllText(filePath, json);

                string msg = $"Сохранение выполнено [{saveData.saveTimestamp}]";
                Debug.Log($"<color=#4EE2EC><b>[SAVE SYSTEM]</b></color> {msg} -> {filePath}");
                OnSaveCompleted?.Invoke(true, msg);
                return true;
            }
            catch (Exception ex)
            {
                string err = "Ошибка сохранения: " + ex.Message;
                Debug.LogError($"[SAVE SYSTEM] {err}");
                OnSaveCompleted?.Invoke(false, err);
                return false;
            }
        }

        /// <summary>
        /// Выполняет загрузку сохраненного состояния мира.
        /// </summary>
        public bool LoadGame(string slotName = null)
        {
            string filePath = GetSaveFilePath(slotName);
            if (!File.Exists(filePath))
            {
                string notFound = "Файл сохранения не найден!";
                Debug.LogWarning($"<color=yellow>[SAVE SYSTEM]</color> {notFound} ({filePath})");
                OnLoadCompleted?.Invoke(false, notFound);
                return false;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                var saveData = JsonUtility.FromJson<GameSaveData>(json);
                if (saveData == null)
                {
                    throw new Exception("Не удалось прочитать структуру сохранения");
                }

                // Очистка динамических объектов (ловушки, якоря, шипы, дым), чтобы не оставлять сиротских эффектов
                CleanupDynamicHazards();

                // 1. Восстановление Игрока
                var player = FindAnyObjectByType<PlayerCombatController2D>();
                var playerHealth = FindAnyObjectByType<PlayerHealth2D>();
                var playerStamina = FindAnyObjectByType<PlayerStamina2D>();
                if (playerHealth != null)
                {
                    playerHealth.RestoreState(saveData.player.Position, saveData.player.health, saveData.player.isDead);
                }
                if (playerStamina != null)
                {
                    playerStamina.RestoreState(saveData.player.stamina, saveData.player.isExhausted);
                }
                if (player != null)
                {
                    player.RestoreStance((CombatStance)saveData.player.currentStance, saveData.player.facingDirection);
                }

                // 2. Восстановление Врага
                var enemy = FindAnyObjectByType<EnemyAIController2D>();
                if (enemy != null)
                {
                    enemy.RestoreState(
                        saveData.enemy.Position,
                        saveData.enemy.health,
                        saveData.enemy.stamina,
                        saveData.enemy.facingDirection,
                        saveData.enemy.isDead
                    );
                }

                // 3. Восстановление Манекена
                var dummy = FindAnyObjectByType<CombatDummy2D>();
                if (dummy != null)
                {
                    dummy.RestoreState(
                        saveData.dummy.Position,
                        saveData.dummy.health,
                        saveData.dummy.isDead
                    );
                }

                string msg = $"Загрузка завершена [{saveData.saveTimestamp}]";
                Debug.Log($"<color=#4EE2EC><b>[SAVE SYSTEM]</b></color> {msg}");
                OnLoadCompleted?.Invoke(true, msg);
                return true;
            }
            catch (Exception ex)
            {
                string err = "Ошибка загрузки: " + ex.Message;
                Debug.LogError($"[SAVE SYSTEM] {err}");
                OnLoadCompleted?.Invoke(false, err);
                return false;
            }
        }

        private void CleanupDynamicHazards()
        {
            // Очистка активных ловушек
            var traps = FindObjectsByType<TacticianTrap2D>();
            for (int i = 0; i < traps.Length; i++)
            {
                if (traps[i] != null) Destroy(traps[i].gameObject);
            }
            TacticianTrap2D.ActiveTraps.Clear();

            // Очистка гравитационных якорей
            var anchors = FindObjectsByType<TacticianGravityAnchor2D>();
            for (int i = 0; i < anchors.Length; i++)
            {
                if (anchors[i] != null) Destroy(anchors[i].gameObject);
            }

            // Очистка волн шипов
            var spikes = FindObjectsByType<TacticianSpikeWave2D>();
            for (int i = 0; i < spikes.Length; i++)
            {
                if (spikes[i] != null) Destroy(spikes[i].gameObject);
            }

            // Очистка дымовых завес
            var smokes = FindObjectsByType<TacticianSmokeCloud2D>();
            for (int i = 0; i < smokes.Length; i++)
            {
                if (smokes[i] != null) Destroy(smokes[i].gameObject);
            }
        }
    }
}
