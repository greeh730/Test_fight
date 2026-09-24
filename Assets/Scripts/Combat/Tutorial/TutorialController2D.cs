using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Combat.Player;
using Combat.UI;
using Combat.Tactician;
using Combat.Stances;

namespace Combat.Tutorial
{
    public enum TutorialStage
    {
        Movement = 1,
        MeleeCombat = 2,
        TacticianMagic = 3,
        DodgeDash = 4,
        ParryAndStamina = 5,
        Completed = 6
    }

    /// <summary>
    /// Контроллер пошагового обучения (Tutorial) по ГДД Abyss of emotions:
    /// 1. Передвижение и прыжок (WASD / Space)
    /// 2. Основы фехтования (вычерчивание ударов ЛКМ)
    /// 3. Колесо фехтования, смена стоек (Ctrl) и магия Тактика
    /// 4. Уклонение (Shift) в замедленном времени при атаке врага
    /// 5. Направленное парирование (ПКМ) и контроль выносливости
    /// </summary>
    [DisallowMultipleComponent]
    public class TutorialController2D : MonoBehaviour
    {
        public static TutorialController2D Instance { get; private set; }

        [Header("--- Player & Targets References ---")]
        [SerializeField] private PlayerController2D playerMovement;
        [SerializeField] private PlayerCombatController2D playerCombat;
        [SerializeField] private PlayerBlockAndParry2D playerBlockParry;
        [SerializeField] private TacticianCombatController2D playerTactician;
        [SerializeField] private CombatDummy2D trainingDummy;
        [SerializeField] private EnemyAIController2D tutorialEnemy;

        [Header("--- Positions & Triggers ---")]
        [Tooltip("Физический триггер для завершения этапа передвижения (можно двигать и масштабировать в Scene View)")]
        [SerializeField] private TutorialTriggerZone2D movementTrigger;

        [Tooltip("Резервная позиция X, если физический триггер не назначен")]
        [SerializeField] private float movementTargetX = 13.5f;

        [Header("--- UI Elements ---")]
        [SerializeField] private GameObject promptPanel;
        [SerializeField] private Text stageBadgeText;
        [SerializeField] private Text promptTitleText;
        [SerializeField] private Text promptDescriptionText;
        [SerializeField] private Text promptObjectiveText;
        [SerializeField] private Image objectiveCheckmark;
        [SerializeField] private Button skipButton;

        [Header("--- Completion Modal ---")]
        [SerializeField] private GameObject completionModal;
        [SerializeField] private Button continueToArenaButton;
        [SerializeField] private Button returnToMenuButton;

        [Header("--- Scenes ---")]
        [SerializeField] private string arenaSceneName = "SampleScene";
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        // Current runtime state
        public TutorialStage CurrentStage { get; private set; } = TutorialStage.Movement;
        private int _combatHitsCount = 0;
        private const int RequiredCombatHits = 3;
        private bool _tacticianUsed = false;
        private bool _dodgeSuccess = false;
        private bool _parrySuccess = false;
        private bool _enemyWindupTriggered = false;
        private Coroutine _enemyAttackRoutine;
        private Coroutine _stageTransitionRoutine;

        private void Awake()
        {
            Instance = this;
            Time.timeScale = 1.0f;

            if (skipButton != null)
            {
                skipButton.onClick.AddListener(SkipTutorial);
            }

            if (continueToArenaButton != null)
            {
                continueToArenaButton.onClick.AddListener(GoToArena);
            }

            if (returnToMenuButton != null)
            {
                returnToMenuButton.onClick.AddListener(GoToMainMenu);
            }

            if (completionModal != null)
            {
                completionModal.SetActive(false);
            }
        }

        private void Start()
        {
            FindSceneReferencesIfNull();
            HookPlayerEvents();

            // Начальная настройка врагов
            if (trainingDummy != null)
            {
                trainingDummy.gameObject.SetActive(false);
            }
            if (tutorialEnemy != null)
            {
                tutorialEnemy.gameObject.SetActive(false);
            }

            StartCoroutine(InitTutorialRoutine());
        }

        private void FindSceneReferencesIfNull()
        {
            if (playerMovement == null) playerMovement = FindAnyObjectByType<PlayerController2D>();
            if (playerCombat == null) playerCombat = FindAnyObjectByType<PlayerCombatController2D>();
            if (playerBlockParry == null) playerBlockParry = FindAnyObjectByType<PlayerBlockAndParry2D>();
            if (playerTactician == null) playerTactician = FindAnyObjectByType<TacticianCombatController2D>();
            if (trainingDummy == null) trainingDummy = FindAnyObjectByType<CombatDummy2D>();
            if (tutorialEnemy == null) tutorialEnemy = FindAnyObjectByType<EnemyAIController2D>();
        }

        private void HookPlayerEvents()
        {
            if (playerCombat != null)
            {
                playerCombat.onAttackHit += OnPlayerAttackHit;
                playerCombat.onStanceChanged.AddListener(OnStanceChanged);
            }

            if (playerTactician != null)
            {
                playerTactician.OnAbilityExecuted += OnTacticianAbilityExecuted;
            }

            if (playerMovement != null)
            {
                playerMovement.OnDashStarted += OnPlayerDashed;
            }

            if (playerBlockParry != null)
            {
                playerBlockParry.onParrySuccess.AddListener(OnPlayerParrySuccess);
            }

            EnemyAIController2D.OnAnyEnemyDied += OnEnemyDied;
        }

        private void OnDestroy()
        {
            Time.timeScale = 1.0f;
            EnemyAIController2D.OnAnyEnemyDied -= OnEnemyDied;

            if (playerCombat != null)
            {
                playerCombat.onAttackHit -= OnPlayerAttackHit;
                playerCombat.onStanceChanged.RemoveListener(OnStanceChanged);
            }

            if (playerTactician != null)
            {
                playerTactician.OnAbilityExecuted -= OnTacticianAbilityExecuted;
            }

            if (playerMovement != null)
            {
                playerMovement.OnDashStarted -= OnPlayerDashed;
            }

            if (playerBlockParry != null)
            {
                playerBlockParry.onParrySuccess.RemoveListener(OnPlayerParrySuccess);
            }
        }

        private IEnumerator InitTutorialRoutine()
        {
            yield return new WaitForSeconds(0.4f);
            SetStage(TutorialStage.Movement);
        }

        private void Update()
        {
            if (CurrentStage == TutorialStage.Movement)
            {
                UpdateMovementStage();
            }
        }

        // =========================================================================
        // ЭТАП 1: ПЕРЕДВИЖЕНИЕ
        // =========================================================================
        private void UpdateMovementStage()
        {
            if (playerMovement == null) return;

            // Если физический триггер не задан — используем резервную проверку по X
            if (movementTrigger == null && playerMovement.transform.position.x >= movementTargetX)
            {
                CompleteCurrentStage(TutorialStage.MeleeCombat);
            }
        }

        // =========================================================================
        // ЭТАП 2: ОСНОВЫ ФЕХТОВАНИЯ
        // =========================================================================
        private void OnPlayerAttackHit()
        {
            if (CurrentStage == TutorialStage.MeleeCombat)
            {
                _combatHitsCount++;
                UpdatePromptUI();

                if (_combatHitsCount >= RequiredCombatHits)
                {
                    CompleteCurrentStage(TutorialStage.TacticianMagic);
                }
            }
        }

        // =========================================================================
        // ЭТАП 3: СМЕНА СТОЙКИ И МАГИЯ ТАКТИКА
        // =========================================================================
        private void OnStanceChanged(CombatStance newStance)
        {
            if (CurrentStage == TutorialStage.TacticianMagic)
            {
                UpdatePromptUI();
            }
        }

        private void OnTacticianAbilityExecuted(Direction8 dir)
        {
            if (CurrentStage == TutorialStage.TacticianMagic && !_tacticianUsed)
            {
                _tacticianUsed = true;
                UpdatePromptUI();
                StartCoroutine(TransitionFromTacticianStage());
            }
        }

        private IEnumerator TransitionFromTacticianStage()
        {
            yield return new WaitForSeconds(1.2f);
            CompleteCurrentStage(TutorialStage.DodgeDash);
        }

        // =========================================================================
        // ЭТАП 4: УКЛОНЕНИЕ (РЫВОК НА SHIFT) В СЛОУ-МО
        // =========================================================================
        private void StartEnemyAttackWithTelegraph()
        {
            if (tutorialEnemy == null) return;

            tutorialEnemy.gameObject.SetActive(true);
            tutorialEnemy.transform.position = new Vector3(21.5f, 0.55f, 0f);

            if (_enemyAttackRoutine != null) StopCoroutine(_enemyAttackRoutine);
            _enemyAttackRoutine = StartCoroutine(DodgeTutorialAttackRoutine());
        }

        private IEnumerator DodgeTutorialAttackRoutine()
        {
            yield return new WaitForSeconds(0.8f);
            if (tutorialEnemy != null)
            {
                tutorialEnemy.ForceTelegraphAttack();
            }

            yield return new WaitForSeconds(0.2f);
            _enemyWindupTriggered = true;

            // Включаем Slow-Motion для наглядного уклонения
            Time.timeScale = 0.25f;

            if (promptObjectiveText != null)
            {
                promptObjectiveText.text = "<color=#FF3355><b>⚠️ ВРАГ АТАКУЕТ! НАЖМИТЕ [ SHIFT ] ДЛЯ РЫВКА!</b></color>";
            }

            // Ждем рывка игрока в течение нескольких замедленных секунд
            float timer = 0f;
            while (timer < 3.5f && !_dodgeSuccess)
            {
                timer += Time.unscaledDeltaTime;
                yield return null;
            }

            Time.timeScale = 1.0f;

            if (_dodgeSuccess)
            {
                yield return new WaitForSeconds(1.2f);
                CompleteCurrentStage(TutorialStage.ParryAndStamina);
            }
            else
            {
                // Повторяем атаку, если игрок не успел сделать рывок
                if (CurrentStage == TutorialStage.DodgeDash)
                {
                    yield return new WaitForSeconds(1.5f);
                    StartEnemyAttackWithTelegraph();
                }
            }
        }

        private void OnPlayerDashed()
        {
            if (CurrentStage == TutorialStage.DodgeDash && _enemyWindupTriggered && !_dodgeSuccess)
            {
                _dodgeSuccess = true;
                Time.timeScale = 1.0f;

                if (promptObjectiveText != null)
                {
                    promptObjectiveText.text = "<color=#00FF88><b>✓ РЫВОК ВЫПОЛНЕН! Урон успешно избегнут!</b></color>";
                }
            }
        }

        // =========================================================================
        // ЭТАП 5: НАПРАВЛЕННОЕ ПАРИРОВАНИЕ (ПКМ) И ВЫНОСЛИВОСТЬ
        // =========================================================================
        private void StartParryTutorialAttack()
        {
            if (tutorialEnemy == null) return;

            if (_enemyAttackRoutine != null) StopCoroutine(_enemyAttackRoutine);
            _enemyAttackRoutine = StartCoroutine(ParryTutorialAttackRoutine());
        }

        private IEnumerator ParryTutorialAttackRoutine()
        {
            yield return new WaitForSeconds(1.2f);

            if (tutorialEnemy != null)
            {
                tutorialEnemy.ForceTelegraphAttack();
            }

            if (promptObjectiveText != null)
            {
                promptObjectiveText.text = "<color=#FFD700><b>⚡ Зажмите [ ПКМ ] в направлении врага для парирования!</b></color>";
            }

            float timer = 0f;
            while (timer < 4.0f && !_parrySuccess)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            if (!_parrySuccess && CurrentStage == TutorialStage.ParryAndStamina)
            {
                yield return new WaitForSeconds(1.5f);
                StartParryTutorialAttack();
            }
        }

        private void OnPlayerParrySuccess()
        {
            if (CurrentStage == TutorialStage.ParryAndStamina && !_parrySuccess)
            {
                _parrySuccess = true;

                if (tutorialEnemy != null)
                {
                    tutorialEnemy.Stun(5.0f);
                }

                if (promptObjectiveText != null)
                {
                    promptObjectiveText.text = "<color=#00FF88><b>✓ ИДЕАЛЬНОЕ ПАРИРОВАНИЕ! Враг оглушен! Добейте его!</b></color>";
                }

                StartCoroutine(FinishParryStageRoutine());
            }
        }

        private IEnumerator FinishParryStageRoutine()
        {
            yield return new WaitForSeconds(1.8f);
            if (tutorialEnemy != null)
            {
                tutorialEnemy.CancelAttack();
                tutorialEnemy.gameObject.SetActive(false);
            }
            if (playerMovement != null)
            {
                var pHealth = playerMovement.GetComponent<PlayerHealth2D>();
                if (pHealth != null)
                {
                    pHealth.CanDie = false;
                    pHealth.SetInvulnerable(true);
                }
            }
            CompleteCurrentStage(TutorialStage.Completed);
        }

        private void OnEnemyDied(EnemyAIController2D enemy)
        {
            if (CurrentStage == TutorialStage.ParryAndStamina)
            {
                CompleteCurrentStage(TutorialStage.Completed);
            }
        }

        // =========================================================================
        // СМЕНА ЭТАПОВ И ОБНОВЛЕНИЕ ИНТЕРФЕЙСА
        // =========================================================================
        private void CompleteCurrentStage(TutorialStage nextStage)
        {
            if (_stageTransitionRoutine != null) StopCoroutine(_stageTransitionRoutine);
            _stageTransitionRoutine = StartCoroutine(TransitionToStageRoutine(nextStage));
        }

        private IEnumerator TransitionToStageRoutine(TutorialStage nextStage)
        {
            if (objectiveCheckmark != null) objectiveCheckmark.color = new Color(0f, 1f, 0.5f, 1f);

            if (nextStage == TutorialStage.Completed)
            {
                // Немедленно нейтрализуем врагов и защищаем игрока от любой случайной гибели
                if (tutorialEnemy != null)
                {
                    tutorialEnemy.CancelAttack();
                    tutorialEnemy.gameObject.SetActive(false);
                }
                if (playerMovement != null)
                {
                    var pHealth = playerMovement.GetComponent<PlayerHealth2D>();
                    if (pHealth != null)
                    {
                        pHealth.CanDie = false;
                        pHealth.SetInvulnerable(true);
                    }
                }
            }

            yield return new WaitForSeconds(0.4f);
            SetStage(nextStage);
        }

        public void SetStage(TutorialStage newStage)
        {
            CurrentStage = newStage;

            switch (newStage)
            {
                case TutorialStage.Movement:
                    if (trainingDummy != null) trainingDummy.gameObject.SetActive(false);
                    if (tutorialEnemy != null) tutorialEnemy.gameObject.SetActive(false);
                    break;

                case TutorialStage.MeleeCombat:
                    if (trainingDummy != null)
                    {
                        trainingDummy.gameObject.SetActive(true);
                        trainingDummy.transform.position = new Vector3(16.5f, 0.55f, 0f);
                    }
                    _combatHitsCount = 0;
                    break;

                case TutorialStage.TacticianMagic:
                    if (playerTactician != null)
                    {
                        playerTactician.UnlockAllAbilities();
                    }
                    _tacticianUsed = false;
                    break;

                case TutorialStage.DodgeDash:
                    if (trainingDummy != null) trainingDummy.gameObject.SetActive(false);
                    _dodgeSuccess = false;
                    _enemyWindupTriggered = false;
                    StartEnemyAttackWithTelegraph();
                    break;

                case TutorialStage.ParryAndStamina:
                    _parrySuccess = false;
                    StartParryTutorialAttack();
                    break;

                case TutorialStage.Completed:
                    ShowCompletionModal();
                    break;
            }

            UpdatePromptUI();
        }

        private void UpdatePromptUI()
        {
            if (promptPanel == null) return;

            if (CurrentStage == TutorialStage.Completed)
            {
                promptPanel.SetActive(false);
                return;
            }

            promptPanel.SetActive(true);
            if (objectiveCheckmark != null) objectiveCheckmark.color = new Color(0.4f, 0.45f, 0.55f, 0.6f);

            switch (CurrentStage)
            {
                case TutorialStage.Movement:
                    stageBadgeText.text = "ЭТАП 1 / 5  •  ОСНОВЫ ДВИЖЕНИЯ";
                    promptTitleText.text = "ПЕРЕДВИЖЕНИЕ И ПРЫЖОК";
                    promptDescriptionText.text = 
                        "• Клавиши <b>[ W A S D ]</b> — перемещение персонажа влево и вправо.\n" +
                        "• Клавиша <b>[ ПРОБЕЛ ]</b> (или W) — прыжок для преодоления препятствий.";
                    promptObjectiveText.text = "Пройдите вперед по коридору и перепрыгните через возвышение.";
                    break;

                case TutorialStage.MeleeCombat:
                    stageBadgeText.text = "ЭТАП 2 / 5  •  ФЕХТОВАНИЕ КЛИНКОМ";
                    promptTitleText.text = "ВЫЧЕРЧИВАНИЕ АТАК ЖЕСТАМИ";
                    promptDescriptionText.text = 
                        "В игре нет пустых одиночных ударов — атаки наносятся связками внутри векторного колеса:\n" +
                        "• <b>Выпад клинком ▶:</b> зажмите ЛКМ и проведите <b>← →</b> (оттяжка и укол)\n" +
                        "• <b>Верхний рубящий ▲:</b> зажмите ЛКМ и проведите <b>↓ ↑</b> (снизу вверх)";
                    promptObjectiveText.text = $"Нанесите удары по тренировочной цели: <b>[{_combatHitsCount} / {RequiredCombatHits}]</b>";
                    break;

                case TutorialStage.TacticianMagic:
                    bool isTactician = playerCombat != null && playerCombat.CurrentStance == CombatStance.Tactician;
                    stageBadgeText.text = "ЭТАП 3 / 5  •  КОЛЕСО И СМЕНА СТОЙКИ";
                    promptTitleText.text = "МАГИЯ ТАКТИКА (МАГИЯ КРИСТАЛЛА)";
                    promptDescriptionText.text = 
                        "Нажмите <b>[ Левый CTRL ]</b>, чтобы сменить стойку на <b>ТАКТИКА</b>.\n" +
                        "В стойке магии свайпы активируют заклинания контроля арены:\n" +
                        "• Свайп вправо <b>→</b>: Направленные шипы   • Вниз <b>↓</b>: Дымовая бомба\n" +
                        "• Свайп вверх <b>↑</b>: Гравитационный якорь   • Влево <b>←</b>: Отскок";
                    promptObjectiveText.text = isTactician 
                        ? "<color=#00E5FF><b>Стойка Тактика активна! Вычертите свайп колеса для магии.</b></color>" 
                        : "Нажмите <b>[ CTRL ]</b> для входа в стойку Тактика.";
                    break;

                case TutorialStage.DodgeDash:
                    stageBadgeText.text = "ЭТАП 4 / 5  •  ТАКТИЧЕСКИЙ РЫВОК";
                    promptTitleText.text = "УКЛОНЕНИЕ (DASH)";
                    promptDescriptionText.text = 
                        "Когда враг замахивается, время замедляется для принятия решения!\n" +
                        "Нажмите <b>[ SHIFT ]</b> (или дважды A / D), чтобы совершить рывок уклонения.\n" +
                        "Во время рывка персонаж неуязвим (i-frames).";
                    promptObjectiveText.text = _dodgeSuccess 
                        ? "<color=#00FF88><b>✓ Уклонение успешно выполнено!</b></color>" 
                        : "Дождитесь атаки скелета и нажмите <b>[ SHIFT ]</b> для уклонения.";
                    break;

                case TutorialStage.ParryAndStamina:
                    stageBadgeText.text = "ЭТАП 5 / 5  •  ЗАЩИТА И КОНТРАТАКА";
                    promptTitleText.text = "НАПРАВЛЕННОЕ ПАРИРОВАНИЕ И ВЫНОСЛИВОСТЬ";
                    promptDescriptionText.text = 
                        "• Зажмите <b>[ ПКМ ]</b>, укажите направление летящего удара и отпустите в момент контакта.\n" +
                        "• <b>Выносливость (над персонажем):</b> расходуется при ударах и рывках, восстанавливается при передышке.\n" +
                        "Успешное парирование сокрушает стамину врага и оглушает его для контратаки!";
                    promptObjectiveText.text = _parrySuccess 
                        ? "<color=#00FF88><b>✓ Враг оглушён! Нанесите решающий удар!</b></color>" 
                        : "Отразите удар врага через парирование <b>[ ПКМ ]</b>.";
                    break;
            }
        }

        private void OnEnable()
        {
            if (movementTrigger != null)
            {
                movementTrigger.OnPlayerEntered -= OnMovementTriggerEntered;
                movementTrigger.OnPlayerEntered += OnMovementTriggerEntered;
            }
        }

        private void OnDisable()
        {
            Time.timeScale = 1.0f;
            if (movementTrigger != null)
            {
                movementTrigger.OnPlayerEntered -= OnMovementTriggerEntered;
            }
        }

        private void OnMovementTriggerEntered(Collider2D playerCol)
        {
            if (CurrentStage == TutorialStage.Movement)
            {
                CompleteCurrentStage(TutorialStage.MeleeCombat);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (movementTrigger == null)
            {
                Gizmos.color = new Color(0.2f, 0.95f, 1f, 0.6f);
                Gizmos.DrawLine(new Vector3(movementTargetX, -2f, 0f), new Vector3(movementTargetX, 8f, 0f));
#if UNITY_EDITOR
                UnityEditor.Handles.Label(new Vector3(movementTargetX, 4f, 0f), $"Резервная граница Этапа 1 (X={movementTargetX:F1})");
#endif
            }
        }

        private void ShowCompletionModal()
        {
            // 1. ПОЛНАЯ ОСТАНОВКА ВРЕМЕНИ
            Time.timeScale = 0f;

            // 2. Прерываем любые фоновые корутины атак и переходов
            StopAllCoroutines();

            // 3. Отключаем врагов и манекен
            if (tutorialEnemy != null)
            {
                tutorialEnemy.CancelAttack();
                tutorialEnemy.gameObject.SetActive(false);
            }
            if (trainingDummy != null)
            {
                trainingDummy.gameObject.SetActive(false);
            }

            // 4. Защищаем игрока от любой возможной гибели (снаряды, эффекты)
            if (playerMovement != null)
            {
                var pHealth = playerMovement.GetComponent<PlayerHealth2D>();
                if (pHealth != null)
                {
                    pHealth.CanDie = false;
                    pHealth.SetInvulnerable(true);
                }
            }
            if (playerCombat != null)
            {
                playerCombat.CancelAttack();
            }

            // 5. Открываем модальное окно завершения обучения
            if (completionModal != null)
            {
                completionModal.SetActive(true);
            }
        }

        public void SkipTutorial()
        {
            Time.timeScale = 1.0f;
            GoToArena();
        }

        public void GoToArena()
        {
            Time.timeScale = 1.0f;
            ScreenFadeTransition2D.FadeAndLoadScene(arenaSceneName);
        }

        public void GoToMainMenu()
        {
            Time.timeScale = 1.0f;
            ScreenFadeTransition2D.FadeAndLoadScene(mainMenuSceneName);
        }
    }
}
