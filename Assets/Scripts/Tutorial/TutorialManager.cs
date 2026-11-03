using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Combat.Dialogue;
using Fencing;
using Magic;
using Combat.Player;
using Combat.UI;
using LevelGeneration;

namespace Combat.Tutorial
{
    public enum TutorialPhase
    {
        CorridorIntro = 0,
        EnterDummyArena = 1,
        SettingsWheelStep = 2,
        DummySwordHits = 3,
        ArcheStancePrompt = 4,
        ArcheExplanation = 5,
        ArcheCastTask = 6,
        EnterVovaArena = 7,
        VovaDialogue = 8,
        VovaDuel = 9,
        VovaDefeated = 10,
        CompleteAndExit = 11
    }

    /// <summary>
    /// Главный контроллер прохождения сценария обучения.
    /// Управляет фазами, событиями манекена, Архе, боем со скелетом Вовой,
    /// открытием решёток и переходом в основной игровой режим.
    /// </summary>
    [DisallowMultipleComponent]
    public class TutorialManager : MonoBehaviour
    {
        public static TutorialManager Instance { get; private set; }

        [Header("--- Current Status ---")]
        [SerializeField] private TutorialPhase currentPhase = TutorialPhase.CorridorIntro;
        [SerializeField] private int dummyHitsCount = 0;
        [SerializeField] private int requiredDummyHits = 3;
        [SerializeField] private int archeFailCount = 0;
        [SerializeField] private bool hasArcheHitDummy = false;

        [Header("--- Level & Scene References ---")]
        [SerializeField] private Transform playerSpawnPoint;
        [SerializeField] private Transform dummyRoomEntrancePoint;
        [SerializeField] private CombatDummy2D dummy;
        [SerializeField] private TutorialVovaController vovaController;
        [SerializeField] private SectorBarrierDoor2D doorToDummy;
        [SerializeField] private SectorBarrierDoor2D doorToVova;
        [SerializeField] private SectorBarrierDoor2D doorToExit;
        [SerializeField] private VictoryCrystal exitCrystal;

        [Header("--- Trigger Zones ---")]
        [SerializeField] private TutorialTriggerZone dummyRoomTriggerZone;
        [SerializeField] private TutorialTriggerZone vovaRoomTriggerZone;

        [Header("--- UI References ---")]
        [SerializeField] private DialogueManager dialogueManager;
        [SerializeField] private TutorialObjectiveHUD objectiveHUD;
        [SerializeField] private FocusUI focusUI;
        [SerializeField] private FencingWheelUI wheelUI;
        [SerializeField] private CombatSettingsUI settingsUI;

        [Header("--- Sprites ---")]
        [SerializeField] private Sprite vovaPortraitSprite;

        private PlayerController2D _player;
        private PlayerCapabilityController _caps;
        private FencingWheel _wheel;
        private PlayerMagicManager _magicManager;

        public TutorialPhase CurrentPhase => currentPhase;

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

            EnsureReferences();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            EnsureReferences();
            StartTutorial();
        }

        private void OnEnable()
        {
            CombatDummy2D.OnAnyDummyHit += HandleDummyHit;
            FireballProjectile.OnAnyFireballHitDummy += HandleFireballHitDummy;
            FireballProjectile.OnAnyFireballMissed += HandleFireballMissed;
            CombatSettingsUI.OnSettingsClosed += HandleSettingsClosed;
            CombatSettingsUI.OnShowWheelToggled += HandleWheelToggledInSettings;
            DialogueManager.OnLineStarted += HandleDialogueLineStarted;

            if (dummyRoomTriggerZone != null)
            {
                dummyRoomTriggerZone.OnPlayerEntered += HandleEnterDummyRoom;
            }
            if (vovaRoomTriggerZone != null)
            {
                vovaRoomTriggerZone.OnPlayerEntered += HandleEnterVovaRoom;
            }
            if (vovaController != null)
            {
                vovaController.OnVovaDefeated += HandleVovaDefeated;
            }
        }

        private void OnDisable()
        {
            CombatDummy2D.OnAnyDummyHit -= HandleDummyHit;
            FireballProjectile.OnAnyFireballHitDummy -= HandleFireballHitDummy;
            FireballProjectile.OnAnyFireballMissed -= HandleFireballMissed;
            CombatSettingsUI.OnSettingsClosed -= HandleSettingsClosed;
            CombatSettingsUI.OnShowWheelToggled -= HandleWheelToggledInSettings;
            DialogueManager.OnLineStarted -= HandleDialogueLineStarted;

            if (dummyRoomTriggerZone != null)
            {
                dummyRoomTriggerZone.OnPlayerEntered -= HandleEnterDummyRoom;
            }
            if (vovaRoomTriggerZone != null)
            {
                vovaRoomTriggerZone.OnPlayerEntered -= HandleEnterVovaRoom;
            }
            if (vovaController != null)
            {
                vovaController.OnVovaDefeated -= HandleVovaDefeated;
            }
        }

        private string _lastVovaDuelInstruction = null;

        private void Update()
        {
            // Начиная со встречи со скелетом Вовой шкала фокуса всегда должна быть разблокирована и видна в UI
            if (currentPhase >= TutorialPhase.EnterVovaArena && currentPhase <= TutorialPhase.CompleteAndExit)
            {
                if (focusUI != null && !focusUI.gameObject.activeSelf)
                {
                    UnlockAndShowFocusUI();
                }
            }

            if (currentPhase == TutorialPhase.VovaDuel)
            {
                UpdateVovaDuelHUD();
            }
        }

        private void UpdateVovaDuelHUD()
        {
            if (objectiveHUD == null) return;

            bool isFocusActive = FocusManager.Instance != null && FocusManager.Instance.IsFocusActive;
            bool isVovaStunned = vovaController != null && vovaController.IsStunned;

            string instruction;
            string progress;
            string header = "ДОБИВАНИЕ ВОВЫ";

            if (!isFocusActive)
            {
                if (isVovaStunned)
                {
                    instruction = "⭐ ВОВА ОГЛУШЁН! Зажмите и удерживайте [ПКМ] для перехода в Режим Фокуса!";
                    progress = "Удерживайте [ПКМ] (Фокус)";
                }
                else
                {
                    instruction = "Оглушите Вову: сбивайте его выносливость (жёлтая шкала) ударами меча или парированием!";
                    progress = "Сбейте выносливость Вовы";
                }
            }
            else
            {
                float tension = (FocusManager.Instance.ActiveCircleCharge != null)
                    ? FocusManager.Instance.ActiveCircleCharge.GetTension01()
                    : 0f;

                float threshold = FocusManager.Instance.maxTensionThreshold;

                if (tension < threshold)
                {
                    instruction = "Оттяните мышь НАЗАД (как тетиву лука), наводя белую линию на Вову!";
                    progress = $"Натяжение: {(tension / threshold * 100f):F0}%";
                }
                else
                {
                    instruction = "⚔️ ЗАМАХ МАКСИМАЛЕН! Нажмите [ЛКМ] для смертельного выпада!";
                    progress = "Нажмите [ЛКМ] (Удар!)";
                }
            }

            if (instruction != _lastVovaDuelInstruction)
            {
                _lastVovaDuelInstruction = instruction;
                objectiveHUD.ShowObjective(instruction, progress, header);
            }
            else
            {
                objectiveHUD.UpdateProgress(progress);
            }
        }

        public void EnsureReferences()
        {
            if (_player == null) _player = FindAnyObjectByType<PlayerController2D>();
            if (_caps == null && _player != null) _caps = _player.GetComponent<PlayerCapabilityController>();
            if (_caps == null) _caps = PlayerCapabilityController.Instance;
            if (_wheel == null) _wheel = FindAnyObjectByType<FencingWheel>();
            if (_magicManager == null && _player != null) _magicManager = _player.GetComponent<PlayerMagicManager>();

            if (dialogueManager == null) dialogueManager = DialogueManager.Instance;
            if (objectiveHUD == null) objectiveHUD = TutorialObjectiveHUD.Instance;
            if (focusUI == null) focusUI = FindAnyObjectByType<FocusUI>(FindObjectsInactive.Include);
            if (wheelUI == null) wheelUI = FindAnyObjectByType<FencingWheelUI>(FindObjectsInactive.Include);
            if (settingsUI == null) settingsUI = FindAnyObjectByType<CombatSettingsUI>(FindObjectsInactive.Include);

            if (vovaPortraitSprite == null)
            {
#if UNITY_EDITOR
                vovaPortraitSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Vova/Ходьба/Ходьба1/0000.png");
#endif
            }
        }

        // ==========================================
        // ФАЗА 0: СТАРТОВЫЙ КОРИДОР
        // ==========================================
        public void StartTutorial()
        {
            currentPhase = TutorialPhase.CorridorIntro;

            // По умолчанию колесо фехтования выключено в настройках и скрыто в UI
            if (Settings.CombatSettingsManager.Instance != null)
            {
                var s = Settings.CombatSettingsManager.Instance.CurrentSettings;
                if (s != null && s.showFencingWheel)
                {
                    s.showFencingWheel = false;
                    Settings.CombatSettingsManager.Instance.UpdateSettings(s);
                }
            }
            if (wheelUI != null)
            {
                var cg = wheelUI.GetComponent<CanvasGroup>();
                if (cg == null) cg = wheelUI.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                cg.blocksRaycasts = false;
                cg.interactable = false;
            }

            // Скрываем шкалу фокуса до арены Вовы
            if (focusUI != null)
            {
                focusUI.gameObject.SetActive(false);
            }

            // Игрок может ТОЛЬКО ходить (атаки, магия и прыжки заблокированы)
            if (_caps != null)
            {
                _caps.SetCorridorMode();
            }

            // Двери заперты
            if (doorToDummy != null) doorToDummy.SetClosed(true, instant: true);
            if (doorToVova != null) doorToVova.SetClosed(true, instant: true);
            if (doorToExit != null) doorToExit.SetClosed(true, instant: true);

            // Блокируем магию только на Огонь: заклинание Туман полностью исключено из обучения
            if (_magicManager != null)
            {
                var fireSpell = _magicManager.GetComponent<FireSpell>();
                if (fireSpell != null)
                {
                    _magicManager.LockToSpell(fireSpell);
                }
                var fogSpell = _magicManager.GetComponent<FogSpell>();
                if (fogSpell != null)
                {
                    fogSpell.enabled = false;
                }
            }

            // Стартовый диалог в коридоре
            if (dialogueManager != null)
            {
                dialogueManager.PlaySequence(TutorialDialogueData.GetIntroCorridorLines(), () =>
                {
                    Debug.Log("<color=#00FFAA>[TUTORIAL]</color> Вводный диалог завершён. Решётка открыта! Игрок идёт на арену с манекеном.");
                    if (doorToDummy != null)
                    {
                        doorToDummy.SetClosed(false);
                    }
                });
            }
        }

        // ==========================================
        // ФАЗА 1: ВХОД НА АРЕНУ С МАНЕКЕНОМ
        // ==========================================
        private void HandleEnterDummyRoom(PlayerController2D p)
        {
            if (currentPhase != TutorialPhase.CorridorIntro) return;

            currentPhase = TutorialPhase.EnterDummyArena;

            // Сразу закрываем решётку за спиной игрока
            if (doorToDummy != null)
            {
                doorToDummy.SetClosed(true);
            }

            StartCoroutine(EnterDummyRoomRoutine());
        }

        private IEnumerator EnterDummyRoomRoutine()
        {
            // Ждём, пока решётка опустится и захлопнется за спиной (0.5 сек)
            yield return new WaitForSeconds(0.5f);

            // И только потом проигрывается диалог Незнакомца
            if (dialogueManager != null)
            {
                dialogueManager.PlaySequence(TutorialDialogueData.GetEnterDummyArenaLines(), () =>
                {
                    currentPhase = TutorialPhase.SettingsWheelStep;
                    if (objectiveHUD != null)
                    {
                        objectiveHUD.ShowObjective(
                            "Откройте меню настроек (Esc) и включите «Отображать колесо в UI».",
                            "Включите колесо фехтования в настройках (Esc)"
                        );
                    }
                    Debug.Log("<color=#00FFAA>[TUTORIAL]</color> Ожидание открытия настроек по Esc и включения колеса фехтования.");
                });
            }
        }

        // ==========================================
        // ФАЗА 2: НАСТРОЙКИ КОЛЕСА ПО ESC
        // ==========================================
        private void HandleWheelToggledInSettings(bool isWheelOn)
        {
            if (currentPhase == TutorialPhase.SettingsWheelStep)
            {
                if (isWheelOn)
                {
                    Debug.Log("<color=#00FFAA>[TUTORIAL]</color> Игрок включил колесо в настройках!");
                    if (objectiveHUD != null)
                    {
                        objectiveHUD.ShowObjective(
                            "Отлично! Закройте меню настроек (Esc или ✕), чтобы продолжить.",
                            "Колесо фехтования включено [✓]"
                        );
                    }
                }
                else
                {
                    if (objectiveHUD != null)
                    {
                        objectiveHUD.ShowObjective(
                            "Включите «Отображать колесо в UI» в секции колеса фехтования.",
                            "Включите колесо фехтования в настройках (Esc)"
                        );
                    }
                }
            }
        }

        private void HandleSettingsClosed()
        {
            if (currentPhase == TutorialPhase.SettingsWheelStep)
            {
                // Проверяем, включено ли колесо фехтования
                var settingsData = Settings.CombatSettingsManager.Instance != null ? Settings.CombatSettingsManager.Instance.CurrentSettings : null;
                bool isWheelOn = settingsData != null ? settingsData.showFencingWheel : false;

                if (isWheelOn)
                {
                    OnWheelSuccessfullyEnabled();
                }
                else
                {
                    // Игрок закрыл настройки, не включив колесо — требуем обязательно включить
                    if (objectiveHUD != null)
                    {
                        objectiveHUD.ShowObjective(
                            "Вы не включили колесо! Нажмите Esc и включите «Отображать колесо в UI».",
                            "Обязательно включите колесо в настройках (Esc)"
                        );
                    }
                }
            }
        }

        private void OnWheelSuccessfullyEnabled()
        {
            currentPhase = TutorialPhase.DummySwordHits;

            if (dialogueManager != null)
            {
                dialogueManager.PlaySequence(TutorialDialogueData.GetAfterWheelEnabledLines(), () =>
                {
                    // Разрешаем удары мечом
                    if (_caps != null)
                    {
                        _caps.SetFencingTutorialMode();
                    }

                    dummyHitsCount = 0;
                    if (objectiveHUD != null)
                    {
                        objectiveHUD.ShowObjective(
                            "Зажмите ЛКМ и проведите мышью по направлениям, чтобы нанести удар.",
                            $"Удар по манекену 0/{requiredDummyHits}"
                        );
                    }
                });
            }
        }

        // ==========================================
        // ФАЗА 3: УДАРЫ ПО МАНЕКЕНУ (0/3)
        // ==========================================
        private void HandleDummyHit(CombatDummy2D targetDummy, AttackConfig attack)
        {
            if (currentPhase != TutorialPhase.DummySwordHits) return;

            dummyHitsCount++;
            if (objectiveHUD != null)
            {
                objectiveHUD.UpdateCounter(dummyHitsCount, requiredDummyHits, "Удар по манекену");
            }

            if (dummyHitsCount >= requiredDummyHits)
            {
                OnDummyHitsCompleted();
            }
        }

        private void OnDummyHitsCompleted()
        {
            currentPhase = TutorialPhase.ArcheStancePrompt;

            if (objectiveHUD != null)
            {
                objectiveHUD.HideObjective();
            }

            if (dialogueManager != null)
            {
                dialogueManager.PlaySequence(TutorialDialogueData.GetAfterDummySwordHitsLines(), () =>
                {
                    // Разрешаем переход в стойку Архе по Ctrl
                    if (_caps != null)
                    {
                        _caps.AllowStanceToggle = true;
                    }

                    StartCoroutine(WaitForArcheStanceSwitch());
                });
            }
        }

        private IEnumerator WaitForArcheStanceSwitch()
        {
            while (_wheel != null && _wheel.CurrentStance != StanceType.Magic)
            {
                yield return null;
            }

            OnArcheStanceEntered();
        }

        // ==========================================
        // ФАЗА 4: ПЕРЕХОД НА АРХЕ И ОБЪЯСНЕНИЕ
        // ==========================================
        private void OnArcheStanceEntered()
        {
            currentPhase = TutorialPhase.ArcheExplanation;

            // Настраиваем только огненное заклинание (Туман полностью отключён)
            if (_magicManager != null)
            {
                var fireSpell = _magicManager.GetComponent<FireSpell>();
                if (fireSpell != null)
                {
                    _magicManager.LockToSpell(fireSpell);
                }
                var fogSpell = _magicManager.GetComponent<FogSpell>();
                if (fogSpell != null)
                {
                    fogSpell.enabled = false;
                }
            }

            // Подписываемся на события заклинания
            var activeSpell = _magicManager != null ? _magicManager.CurrentActiveSpell : null;
            if (activeSpell != null)
            {
                activeSpell.OnSpellFailed += HandleSpellCastFailed;
            }

            // Мгновенный диалог, блокирующий действия
            if (dialogueManager != null)
            {
                dialogueManager.PlaySequence(TutorialDialogueData.GetArcheExplanationLines(), () =>
                {
                    currentPhase = TutorialPhase.ArcheCastTask;

                    if (_caps != null)
                    {
                        _caps.SetArcheTutorialMode();
                    }

                    if (objectiveHUD != null)
                    {
                        objectiveHUD.ShowObjective("Успешно сложите комбинацию рун и попадите по манекену.");
                    }
                });
            }
        }

        // ==========================================
        // ФАЗА 5: КАСТ АРХЕ — ПРОВАЛЫ, ПРОМАХИ И УСПЕХ
        // ==========================================
        private bool _isHandlingExplosionCut = false;

        private void HandleSpellCastFailed(string reason)
        {
            if (currentPhase != TutorialPhase.ArcheCastTask) return;
            if (_isHandlingExplosionCut) return;

            archeFailCount++;
            StartCoroutine(ComedicExplosionCutRoutine(archeFailCount == 1));
        }

        private IEnumerator ComedicExplosionCutRoutine(bool isFirstFail)
        {
            _isHandlingExplosionCut = true;

            // 1. Взрыв начинается: даём буквально 0.1 секунды начального взрыва для комедийного эффекта срыва (smash-cut)
            yield return new WaitForSecondsRealtime(0.10f);

            // 2. Экран мгновенно гаснет в абсолютно чёрный цвет
            var fader = ScreenFadeTransition2D.Instance;
            if (fader != null)
            {
                fader.SnapToBlack();
            }

            // 3. Мгновенно обрубаем все звуки (как выдернутый штекер)
            AudioListener.pause = true;

            var soundMgr = Combat.Audio.SoundManager.Instance;
            if (soundMgr != null)
            {
                soundMgr.StopAllSounds();
                soundMgr.UpdateMovementAudio(false, false, false, 0f);
            }

            var allSources = FindObjectsByType<AudioSource>();
            for (int i = 0; i < allSources.Length; i++)
            {
                if (allSources[i] != null)
                {
                    allSources[i].Stop();
                }
            }

            // 4. Комедийная микро-пауза в полной темноте и тишине
            yield return new WaitForSecondsRealtime(0.20f);

            // 5. Очищаем остатки взрыва, горящие частицы и статусы
            var explosions = FindObjectsByType<FireExplosion>();
            for (int i = 0; i < explosions.Length; i++)
            {
                if (explosions[i] != null) Destroy(explosions[i].gameObject);
            }

            var embers = FindObjectsByType<FireEmber>();
            for (int i = 0; i < embers.Length; i++)
            {
                if (embers[i] != null) Destroy(embers[i].gameObject);
            }

            if (_player != null)
            {
                var burn = _player.GetComponent<BurnStatusEffect>();
                if (burn != null) Destroy(burn);

                var pHealth = _player.GetComponent<Combat.Player.PlayerHealth2D>();
                if (pHealth != null)
                {
                    pHealth.ResetHealth();
                }

                var rb = _player.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                }

                var pc = _player.GetComponent<PlayerController2D>();
                if (pc != null)
                {
                    pc.SetFacing(1f);
                }

                // Мгновенный возврат ко входу в комнату манекена
                if (dummyRoomEntrancePoint != null)
                {
                    _player.transform.position = dummyRoomEntrancePoint.position;
                }
                else
                {
                    _player.transform.position = new Vector3(22f, 1.5f, 0f);
                }
            }

            // Мгновенная центровка камеры на игроке без лага и тряски
            var camFollow = Camera.main != null ? Camera.main.GetComponent<Combat.Cameras.CameraFollow2D>() : null;
            if (camFollow == null) camFollow = FindAnyObjectByType<Combat.Cameras.CameraFollow2D>();
            if (camFollow != null)
            {
                camFollow.StopShake();
                camFollow.SnapToTarget();
            }

            // Сброс и мгновенная готовность заклинания огня
            if (_magicManager != null)
            {
                var fireSpell = _magicManager.GetComponent<FireSpell>();
                if (fireSpell != null)
                {
                    fireSpell.ResetSpellImmediate();
                    _magicManager.LockToSpell(fireSpell);
                }
            }

            // 6. Восстанавливаем звук и мгновенно проявляем картинку БЕЗ РАССЕИВАНИЯ
            AudioListener.pause = false;

            if (fader != null)
            {
                fader.SnapToClear();
            }

            _isHandlingExplosionCut = false;

            // 7. Озвучивание комедийной реплики Незнакомца
            if (isFirstFail)
            {
                if (dialogueManager != null)
                {
                    dialogueManager.PlaySequence(TutorialDialogueData.GetFirstExplosionFailLines(), () =>
                    {
                        if (objectiveHUD != null)
                        {
                            objectiveHUD.ShowObjective("Успешно сложите комбинацию рун и попадите по манекену.");
                        }
                    });
                }
            }
            else
            {
                if (dialogueManager != null)
                {
                    dialogueManager.PlayNextExplosionFailPhrase(() =>
                    {
                        if (objectiveHUD != null)
                        {
                            objectiveHUD.ShowObjective("Успешно сложите комбинацию рун и попадите по манекену.");
                        }
                    });
                }
                else if (objectiveHUD != null)
                {
                    objectiveHUD.ShowObjective("Успешно сложите комбинацию рун и попадите по манекену.");
                }
            }
        }

        private void HandleFireballMissed(FireballProjectile projectile)
        {
            if (currentPhase != TutorialPhase.ArcheCastTask || hasArcheHitDummy) return;

            // Воспроизводится одна из 4 фраз при промахе (без повторений)
            if (dialogueManager != null)
            {
                dialogueManager.PlayNextSpellMissPhrase();
            }
        }

        private void HandleFireballHitDummy(FireballProjectile projectile, CombatDummy2D hitDummy)
        {
            if (currentPhase != TutorialPhase.ArcheCastTask || hasArcheHitDummy) return;

            hasArcheHitDummy = true;
            OnArcheTaskSuccess();
        }

        private void OnArcheTaskSuccess()
        {
            currentPhase = TutorialPhase.EnterVovaArena;

            if (objectiveHUD != null)
            {
                objectiveHUD.HideObjective();
            }

            if (dialogueManager != null)
            {
                dialogueManager.PlaySequence(TutorialDialogueData.GetArcheSuccessLines(), () =>
                {
                    // Открываем решётку в комнату Вовы
                    if (doorToVova != null)
                    {
                        doorToVova.SetClosed(false);
                    }
                    Debug.Log("<color=#00FFAA>[TUTORIAL]</color> Решётка открыта! Игрок идёт на вторую арену к Вове.");
                });
            }
        }

        // ==========================================
        // ФАЗА 6: АРЕНА ВОВЫ (СКЕЛЕТА)
        // ==========================================
        private void HandleEnterVovaRoom(PlayerController2D p)
        {
            if (currentPhase != TutorialPhase.EnterVovaArena) return;

            currentPhase = TutorialPhase.VovaDialogue;

            // Сразу закрываем решётку за спиной игрока
            if (doorToVova != null)
            {
                doorToVova.SetClosed(true);
            }

            // Шкала фокуса сразу отображается при входе на арену с Вовой
            UnlockAndShowFocusUI();

            StartCoroutine(EnterVovaRoomRoutine());
        }

        private IEnumerator EnterVovaRoomRoutine()
        {
            UnlockAndShowFocusUI();
            yield return new WaitForSeconds(0.5f);

            if (dialogueManager != null)
            {
                dialogueManager.PlaySequence(TutorialDialogueData.GetVovaEncounterLines(vovaPortraitSprite), () =>
                {
                    StartVovaCombat();
                });
            }
        }

        private void StartVovaCombat()
        {
            currentPhase = TutorialPhase.VovaDuel;

            // Переключаем игрока в стойку фехтования и блокируем Архе
            if (_wheel != null)
            {
                _wheel.SetStance(StanceType.Fencing);
            }

            // Настраиваем способности: фехтование + режим фокуса. Архе заблокировано. Игрок бессмертен.
            if (_caps != null)
            {
                _caps.SetVovaDuelMode();
            }

            // Активируем Вову
            if (vovaController != null)
            {
                vovaController.ActivateCombat();
            }

            // Показываем индикатор фокуса слева сверху
            UnlockAndShowFocusUI();

            // Показываем плашку задания справа сверху
            if (objectiveHUD != null)
            {
                objectiveHUD.ShowObjective(
                    "Оглушите противника, зажмите ПКМ, наведите белую линию на противника и при максимальном натяжении нажмите ЛКМ."
                );
            }
        }

        private void HandleDialogueLineStarted(DialogueLine line)
        {
            if (line == null) return;

            // Как только Незнакомец начинает объяснять механику стойки фокуса — сразу добавляем её в UI слева сверху
            if (line.eventTag == "ShowFocusStanceUI" || line.eventTag == "StartVovaFight" ||
                (!string.IsNullOrEmpty(line.text) && (line.text.Contains("сфокусированные") || line.text.Contains("стойку фокуса"))))
            {
                UnlockAndShowFocusUI();
            }
        }

        /// <summary>
        /// Добавляет и отображает стойку фокуса в UI слева сверху.
        /// </summary>
        public void UnlockAndShowFocusUI()
        {
            if (focusUI != null)
            {
                if (!focusUI.gameObject.activeSelf)
                {
                    focusUI.gameObject.SetActive(true);
                }
                focusUI.ApplyCurrentSettings();

                float targetScale = 1.91f;
                if (Combat.Settings.CombatSettingsManager.Instance != null && Combat.Settings.CombatSettingsManager.Instance.CurrentSettings != null)
                {
                    targetScale = Mathf.Max(1.91f, Combat.Settings.CombatSettingsManager.Instance.CurrentSettings.focusBarScale);
                }
                focusUI.transform.localScale = Vector3.one * targetScale;
            }

            if (_caps != null)
            {
                _caps.AllowFocusMode = true;
            }

            if (FocusManager.Instance != null)
            {
                FocusManager.Instance.RestoreFocusToFull();
            }

            Debug.Log("<color=#00FFAA><b>[TutorialManager]</b></color> Стойка фокуса добавлена в UI слева сверху!");
        }

        // ==========================================
        // ФАЗА 7: ВОВА ПОВЕРЖЕН
        // ==========================================
        private void HandleVovaDefeated()
        {
            if (currentPhase != TutorialPhase.VovaDuel) return;

            currentPhase = TutorialPhase.VovaDefeated;
            _lastVovaDuelInstruction = null;

            if (objectiveHUD != null)
            {
                objectiveHUD.HideObjective();
            }

            if (dialogueManager != null)
            {
                dialogueManager.PlaySequence(TutorialDialogueData.GetVovaDefeatedLines(), () =>
                {
                    // Открываем решётку к кристаллу выхода
                    if (doorToExit != null)
                    {
                        doorToExit.SetClosed(false);
                    }
                    currentPhase = TutorialPhase.CompleteAndExit;
                    Debug.Log("<color=#00FFAA>[TUTORIAL]</color> Финальная решётка открыта! Игрок может коснуться кристалла выхода.");
                });
            }
        }

        // ==========================================
        // ФАЗА 8: ЗАВЕРШЕНИЕ ОБУЧЕНИЯ
        // ==========================================
        public void CompleteTutorialAndExit()
        {
            Debug.Log("<color=#00FFAA><b>[TUTORIAL COMPLETED]</b></color> Обучение успешно пройдено! Загрузка основной игры...");
            PlayerPrefs.SetInt("TutorialCompleted", 1);
            PlayerPrefs.Save();

            ScreenFadeTransition2D.FadeAndLoadScene("SampleScene", 0.5f);
        }

        /// <summary>
        /// Пропустить обучение (вызывается из меню паузы / настроек)
        /// </summary>
        public void SkipTutorial()
        {
            Debug.Log("<color=#FFAA00>[TUTORIAL]</color> Обучение пропущено пользователем.");
            PlayerPrefs.SetInt("TutorialCompleted", 1);
            PlayerPrefs.Save();

            ScreenFadeTransition2D.FadeAndLoadScene("SampleScene", 0.35f);
        }
    }
}
