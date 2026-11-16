using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using Combat;
using Combat.Player;
using Combat.ProceduralGeneration;

namespace Combat.Sandbox
{
    /// <summary>
    /// Главный контроллер "Полигона веселья" (Sandbox Testing Grounds).
    /// Позволяет Петру на лету спавнить любых врагов, манекены, бочки,
    /// включать режим бога, бесконечную стамину, замедление времени (Slow-Mo) и чистить арену.
    /// Полностью поддерживает новый Unity Input System.
    /// </summary>
    [DisallowMultipleComponent]
    public class SandboxDirector : MonoBehaviour
    {
        public static SandboxDirector Instance { get; private set; }

        [Header("--- Prefabs to Test ---")]
        [SerializeField] private GameObject enemyFighterPrefab;
        [SerializeField] private GameObject combatDummyPrefab;
        [SerializeField] private GameObject barrelPrefab;
        [SerializeField] private GameObject potionPrefab;

        [Header("--- Spawn Locations ---")]
        [SerializeField] private Transform enemySpawnPoint;
        [SerializeField] private Transform dummySpawnPoint;
        [SerializeField] private Transform barrelSpawnPoint;

        [Header("--- Runtime Cheats & Flags ---")]
        [SerializeField] private bool godMode = false;
        [SerializeField] private bool infiniteStamina = false;
        [SerializeField] private bool slowMo = false;
        [SerializeField] private float slowMoScale = 0.25f;

        [Header("--- UI Overlay ---")]
        [SerializeField] private bool showOverlay = false;

        private PlayerHealth2D _playerHealth;
        private PlayerStamina2D _playerStamina;
        private Transform _playerTransform;
        private readonly List<GameObject> _spawnedObjects = new List<GameObject>();

        private GUIStyle _panelStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _statusStyle;
        private bool _guiInitialized = false;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); return; }

            // Гарантируем запуск физики и времени в сцене (защита от сохранения timeScale=0 в редакторе)
            Time.timeScale = 1.0f;
            Time.fixedDeltaTime = 0.02f;

            // Гарантируем, что камера выводит изображение на экран, а не в RenderTexture
            if (Camera.main != null)
            {
                if (Camera.main.targetTexture != null)
                {
                    Camera.main.targetTexture = null;
                }
                Camera.main.clearFlags = CameraClearFlags.SolidColor;
            }
        }

        private void Start()
        {
            FindPlayer();
        }

        private void FindPlayer()
        {
            var pObj = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
            if (pObj != null)
            {
                _playerTransform = pObj.transform;
                _playerHealth = pObj.GetComponent<PlayerHealth2D>();
                _playerStamina = pObj.GetComponent<PlayerStamina2D>();
            }
        }

        private void Update()
        {
            if (_playerHealth == null || _playerStamina == null)
            {
                FindPlayer();
            }

            // Обработка горячих клавиш через новый Input System
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.f1Key.wasPressedThisFrame) SpawnFighter(CombatClass.Striker);
                if (kb.f2Key.wasPressedThisFrame) SpawnDummy();
                if (kb.f3Key.wasPressedThisFrame) SpawnBarrels();
                if (kb.f4Key.wasPressedThisFrame) FullHeal();
                if (kb.f5Key.wasPressedThisFrame) ToggleGodMode();
                if (kb.f6Key.wasPressedThisFrame) ToggleSlowMo();
                if (kb.f7Key.wasPressedThisFrame) ClearAllSpawned();
                if (kb.tabKey.wasPressedThisFrame || kb.hKey.wasPressedThisFrame) showOverlay = !showOverlay;
                if (kb.rKey.wasPressedThisFrame && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed))
                {
                    RestartScene();
                }
            }

            // Поддержание читов
            if (godMode && _playerHealth != null)
            {
                _playerHealth.CanDie = false;
                if (_playerHealth.CurrentHealth < _playerHealth.MaxHealth * 0.35f)
                {
                    _playerHealth.ResetHealth();
                }
            }

            if (infiniteStamina && _playerStamina != null)
            {
                _playerStamina.RestoreState(_playerStamina.MaxStamina, false);
            }
        }

        #region Actions

        public void SpawnFighter(CombatClass role = CombatClass.Striker)
        {
            if (enemyFighterPrefab == null)
            {
                enemyFighterPrefab = Resources.Load<GameObject>("Prefabs/Enemies/Enemy_Fighter");
            }
            if (enemyFighterPrefab == null) return;

            Vector3 spawnPos = enemySpawnPoint != null ? enemySpawnPoint.position : new Vector3(4.5f, 1.55f, 0f);
            if (_playerTransform != null && enemySpawnPoint == null)
            {
                spawnPos = _playerTransform.position + Vector3.right * 4.5f;
            }

            GameObject enemyObj = Instantiate(enemyFighterPrefab, spawnPos, Quaternion.identity);
            enemyObj.name = $"Enemy_{role}_{_spawnedObjects.Count + 1}";
            _spawnedObjects.Add(enemyObj);

            var ai = enemyObj.GetComponent<EnemyAIController2D>();
            if (ai != null)
            {
                var profile = EnemyVectorGenerator.GenerateProfile(role);
                ai.ApplyPersonalityProfile(profile);

                var silhouette = enemyObj.GetComponent<EnemyVisualSilhouette>() ?? enemyObj.AddComponent<EnemyVisualSilhouette>();
                silhouette.ApplyProfile(profile);

                if (ArenaDirector2D.Instance != null)
                {
                    ArenaDirector2D.Instance.RegisterEnemy(ai);
                }
            }

            Debug.Log($"<color=#00FFAA><b>[Полигон]</b></color> Заспавнен враг класса <b>{role}</b> в {spawnPos}!");
        }

        public void SpawnDummy()
        {
            if (combatDummyPrefab == null)
            {
                combatDummyPrefab = Resources.Load<GameObject>("Prefabs/Enemies/CombatDummy");
            }
            if (combatDummyPrefab == null) return;

            Vector3 spawnPos = dummySpawnPoint != null ? dummySpawnPoint.position : new Vector3(-7f, 1.05f, 0f);
            GameObject dummy = Instantiate(combatDummyPrefab, spawnPos, Quaternion.identity);
            dummy.name = $"Dummy_{_spawnedObjects.Count + 1}";
            _spawnedObjects.Add(dummy);

            Debug.Log($"<color=#00FFAA><b>[Полигон]</b></color> Заспавнен тренировочный манекен в {spawnPos}!");
        }

        public void SpawnBarrels()
        {
            if (barrelPrefab == null) return;

            Vector3 basePos = barrelSpawnPoint != null ? barrelSpawnPoint.position : new Vector3(13f, 0.60f, 0f);

            for (int i = 0; i < 3; i++)
            {
                Vector3 p = basePos + new Vector3(i * 1.25f, 0f, 0f);
                GameObject b = Instantiate(barrelPrefab, p, Quaternion.identity);
                _spawnedObjects.Add(b);
            }

            if (potionPrefab != null)
            {
                Vector3 potPos = basePos + new Vector3(1.5f, 1.8f, 0f);
                GameObject pot = Instantiate(potionPrefab, potPos, Quaternion.identity);
                _spawnedObjects.Add(pot);
            }

            Debug.Log($"<color=#00FFAA><b>[Полигон]</b></color> Заспавнены бочки и зелье в {basePos}!");
        }

        public void FullHeal()
        {
            if (_playerHealth != null)
            {
                _playerHealth.ResetHealth();
                Combat.Common.CombatFloatingText.ShowHeal(_playerHealth.transform.position);
            }
            if (_playerStamina != null)
            {
                _playerStamina.RestoreState(_playerStamina.MaxStamina, false);
            }
            if (Combat.UI.PlayerHealthBarUI.Instance != null)
            {
                Combat.UI.PlayerHealthBarUI.Instance.RefreshDisplay(instant: true);
                Combat.UI.PlayerHealthBarUI.Instance.TriggerHealFlash();
            }
            Debug.Log("<color=#55FF88><b>[Полигон]</b></color> Игрок полностью исцелен и выносливость восстановлена!");
        }

        public void ToggleGodMode()
        {
            godMode = !godMode;
            if (_playerHealth != null)
            {
                _playerHealth.CanDie = !godMode;
                if (godMode) _playerHealth.ResetHealth();
            }
            Debug.Log($"<color=#FFD700><b>[Полигон]</b></color> Бессмертие (God Mode): {(godMode ? "ВКЛЮЧЕНО" : "ВЫКЛЮЧЕНО")}");
        }

        public void ToggleInfiniteStamina()
        {
            infiniteStamina = !infiniteStamina;
            if (infiniteStamina && _playerStamina != null)
            {
                _playerStamina.RestoreState(_playerStamina.MaxStamina, false);
            }
            Debug.Log($"<color=#33CCFF><b>[Полигон]</b></color> Бесконечная стамина: {(infiniteStamina ? "ВКЛЮЧЕНА" : "ВЫКЛЮЧЕНА")}");
        }

        public void ToggleSlowMo()
        {
            slowMo = !slowMo;
            Time.timeScale = slowMo ? slowMoScale : 1.0f;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
            Debug.Log($"<color=#AA55FF><b>[Полигон]</b></color> Замедление времени: {(slowMo ? $"{slowMoScale:F2}x" : "1.0x")}");
        }

        public void ClearAllSpawned()
        {
            int count = 0;
            for (int i = _spawnedObjects.Count - 1; i >= 0; i--)
            {
                if (_spawnedObjects[i] != null)
                {
                    Destroy(_spawnedObjects[i]);
                    count++;
                }
            }
            _spawnedObjects.Clear();

            var enemies = FindObjectsByType<EnemyAIController2D>(FindObjectsInactive.Exclude);
            foreach (var e in enemies)
            {
                if (e != null) { Destroy(e.gameObject); count++; }
            }

            Debug.Log($"<color=#FF5555><b>[Полигон]</b></color> Арена очищена! Удалено объектов: {count}");
        }

        public void RestartScene()
        {
            Time.timeScale = 1.0f;
            Time.fixedDeltaTime = 0.02f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        #endregion

        #region OnGUI HUD

        private void InitGUIStyles()
        {
            if (_guiInitialized) return;

            _panelStyle = new GUIStyle(GUI.skin.box);
            _panelStyle.normal.background = MakeTex(2, 2, new Color(0.05f, 0.08f, 0.15f, 0.90f));
            _panelStyle.padding = new RectOffset(12, 12, 10, 10);

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _headerStyle.normal.textColor = new Color(0.2f, 0.85f, 1.0f, 1.0f);

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                fixedHeight = 26
            };

            _statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                richText = true
            };

            _guiInitialized = true;
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        private void OnGUI()
        {
            if (!showOverlay)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.75f);
                if (GUI.Button(new Rect(Screen.width - 160, 12, 148, 28), "⚔ [Tab] Полигон"))
                {
                    showOverlay = true;
                }
                GUI.color = Color.white;
                return;
            }

            InitGUIStyles();

            float panelWidth = 240f;
            float x = Screen.width - panelWidth - 16f;
            float y = 16f;

            GUILayout.BeginArea(new Rect(x, y, panelWidth, 470f), _panelStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label("⚔ ПОЛИГОН ВЕСЕЛЬЯ ⚔", _headerStyle);
            if (GUILayout.Button("✕", GUILayout.Width(24), GUILayout.Height(20)))
            {
                showOverlay = false;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(4);

            string godText = godMode ? "<color=#00FFAA>ВКЛ</color>" : "<color=#FF5555>ВЫКЛ</color>";
            string stmText = infiniteStamina ? "<color=#00FFAA>ВКЛ</color>" : "<color=#FF5555>ВЫКЛ</color>";
            string sloText = slowMo ? "<color=#FFAA00>0.25x</color>" : "1.0x";

            GUILayout.Label($"<b>Бессмертие:</b> {godText} | <b>Стамина:</b> {stmText}", _statusStyle);
            GUILayout.Label($"<b>Скорость мира:</b> {sloText} | <b>Живых врагов:</b> {_spawnedObjects.Count}", _statusStyle);
            GUILayout.Space(6);

            GUILayout.Label("<b>--- СПАВН ВРАГОВ ---</b>", _headerStyle);
            if (GUILayout.Button("[F1] Боец (Striker)", _buttonStyle)) SpawnFighter(CombatClass.Striker);
            if (GUILayout.Button("[ + ] Танк (Tank)", _buttonStyle)) SpawnFighter(CombatClass.Tank);
            if (GUILayout.Button("[ + ] Лидер (Leader)", _buttonStyle)) SpawnFighter(CombatClass.Leader);
            if (GUILayout.Button("[F2] Манекен (Dummy)", _buttonStyle)) SpawnDummy();
            if (GUILayout.Button("[F3] Бочки с порохом", _buttonStyle)) SpawnBarrels();

            GUILayout.Space(6);
            GUILayout.Label("<b>--- ТЕСТОВЫЕ ЧИТЫ ---</b>", _headerStyle);
            if (GUILayout.Button("[F4] Исцелить (100% HP)", _buttonStyle)) FullHeal();

            GUI.backgroundColor = godMode ? new Color(0.2f, 0.9f, 0.4f) : Color.white;
            if (GUILayout.Button($"[F5] God Mode: {(godMode ? "ВКЛ" : "ВЫКЛ")}", _buttonStyle)) ToggleGodMode();

            GUI.backgroundColor = infiniteStamina ? new Color(0.2f, 0.7f, 1f) : Color.white;
            if (GUILayout.Button($"[ * ] Inf Stamina: {(infiniteStamina ? "ВКЛ" : "ВЫКЛ")}", _buttonStyle)) ToggleInfiniteStamina();

            GUI.backgroundColor = slowMo ? new Color(1f, 0.6f, 0.2f) : Color.white;
            if (GUILayout.Button($"[F6] Slow-Mo: {(slowMo ? "ВКЛ" : "ВЫКЛ")}", _buttonStyle)) ToggleSlowMo();

            GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
            if (GUILayout.Button("[F7] Очистить Арену", _buttonStyle)) ClearAllSpawned();

            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("[Tab] Скрыть меню", _buttonStyle)) showOverlay = false;

            GUILayout.EndArea();
        }

        #endregion
    }
}
