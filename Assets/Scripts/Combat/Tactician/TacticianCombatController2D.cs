using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Combat.Player;
using Combat.UI;
using Combat.Common;

namespace Combat.Tactician
{
    /// <summary>
    /// Контроллер стойки Тактика (Tactician Stance).
    /// Реализует 8 уникальных способностей контроля арены без отзеркаливания (прямой компас).
    /// </summary>
    [DisallowMultipleComponent]
    public class TacticianCombatController2D : MonoBehaviour
    {
        [Header("--- Abilities Configuration ---")]
        [SerializeField] private LayerMask enemyLayers = ~0;

        [Header("--- Visual Colors ---")]
        [SerializeField] private Color tacticianCyan = new Color(0f, 0.95f, 1f, 1f);
        [SerializeField] private Color tacticianPurple = new Color(0.75f, 0.35f, 1f, 1f);
        [SerializeField] private Color tacticianGold = new Color(1f, 0.85f, 0.2f, 1f);

        [Header("--- 1. ➡️ Прощупывающий выпад (Probing Thrust) ---")]
        [Tooltip("Смещение центра хитбокса выпада относительно игрока")]
        [SerializeField] private Vector2 thrustOffset = new Vector2(1.35f, 0.05f);
        [Tooltip("Размер хитбокса выпада")]
        [SerializeField] private Vector2 thrustSize = new Vector2(1.6f, 0.65f);
        [Tooltip("Сила выпада вперед при ударе")]
        [SerializeField] private float thrustLungeForce = 3.8f;
        [Tooltip("Базовый урон")]
        [SerializeField] private float thrustDamage = 18f;
        [Tooltip("Сила отталкивания")]
        [SerializeField] private Vector2 thrustKnockback = new Vector2(4f, 1f);
        [Tooltip("Длительность метки уязвимости (сек)")]
        [SerializeField] private float thrustVulnerabilityDuration = 7.0f;
        [Tooltip("Множитель входящего урона по цели с меткой")]
        [SerializeField] private float thrustVulnerabilityMultiplier = 1.35f;

        [Header("--- 2. ⬇️ Глубинная печать (Abyssal Trap) ---")]
        [Tooltip("Смещение точки установки ловушки относительно игрока")]
        [SerializeField] private Vector2 trapSpawnOffset = new Vector2(0.45f, -0.6f);
        [Tooltip("Радиус срабатывания нажимной ловушки")]
        [SerializeField] private float trapTriggerRadius = 1.1f;
        [Tooltip("Урон при подрыве ловушки")]
        [SerializeField] private float trapDamage = 15f;
        [Tooltip("Длительность обездвиживания (Root) в секундах")]
        [SerializeField] private float trapRootDuration = 1.5f;
        [Tooltip("Время жизни ловушки на земле (сек)")]
        [SerializeField] private float trapLifetime = 25f;

        [Header("--- 3. ⬆️ Гравитационный якорь (Gravity Anchor) ---")]
        [Tooltip("Смещение центра гравитационной сферы в воздухе")]
        [SerializeField] private Vector2 anchorSpawnOffset = new Vector2(1.1f, 2.35f);
        [Tooltip("Радиус захвата врагов и манекенов в воздухе")]
        [SerializeField] private float anchorCaptureRadius = 2.0f;
        [Tooltip("Время левитации/подвешивания цели в воздухе (сек)")]
        [SerializeField] private float anchorSuspensionDuration = 2.5f;
        [Tooltip("Время существования якоря в воздухе (сек)")]
        [SerializeField] private float anchorLifetime = 5.0f;

        [Header("--- 4. ⬅️ Тактический отход (Tactical Retreat) ---")]
        [Tooltip("Импульс скорости бэкдэша (отскока назад)")]
        [SerializeField] private Vector2 retreatVelocity = new Vector2(8.8f, 3.2f);
        [Tooltip("Радиус дымовой завесы")]
        [SerializeField] private float smokeRadius = 2.0f;
        [Tooltip("Длительность ослепления/дезориентации врагов в дыму (сек)")]
        [SerializeField] private float smokeDisorientDuration = 2.0f;
        [Tooltip("Время рассеивания дыма (сек)")]
        [SerializeField] private float smokeLifetime = 3.2f;

        [Header("--- 5. ↗️ Кинетический подброс (Kinetic Launch) ---")]
        [Tooltip("Базовое смещение точки гейзера, если цель не найдена")]
        [SerializeField] private Vector2 launchDefaultOffset = new Vector2(2.6f, -0.6f);
        [Tooltip("Радиус поражения водяного гейзера")]
        [SerializeField] private float launchGeyserRadius = 1.4f;
        [Tooltip("Высота столба гейзера")]
        [SerializeField] private float launchGeyserHeight = 3.2f;
        [Tooltip("Урон подброса")]
        [SerializeField] private float launchDamage = 24f;
        [Tooltip("Импульс запуска высоко в воздух")]
        [SerializeField] private Vector2 launchKnockback = new Vector2(1.5f, 10.5f);
        [Tooltip("Максимальная дистанция авто-наведения на ближайшую цель")]
        [SerializeField] private float launchAutoTargetRange = 7.0f;

        [Header("--- 6. ↘️ Направленные шипы (Directional Spikes) ---")]
        [Tooltip("Смещение точки старта волны шипов")]
        [SerializeField] private Vector2 spikesSpawnOffset = new Vector2(0.7f, -0.55f);
        [Tooltip("Скорость движения волны шипов по земле")]
        [SerializeField] private float spikesSpeed = 9.5f;
        [Tooltip("Максимальная дистанция движения волны")]
        [SerializeField] private float spikesMaxDistance = 6.0f;
        [Tooltip("Радиус поражения волны")]
        [SerializeField] private float spikesHitRadius = 0.85f;
        [Tooltip("Урон от шипов")]
        [SerializeField] private float spikesDamage = 22f;
        [Tooltip("Импульс отталкивания шипами")]
        [SerializeField] private Vector2 spikesKnockback = new Vector2(4.5f, 2.0f);

        [Header("--- 7. ↙️ Магический гарпун (Magic Harpoon) ---")]
        [Tooltip("Смещение точки броска спектральной цепи")]
        [SerializeField] private Vector2 harpoonSpawnOffset = new Vector2(0.4f, 0.1f);
        [Tooltip("Максимальная дальность броска гарпуна")]
        [SerializeField] private float harpoonReachDistance = 6.5f;
        [Tooltip("Скорость притягивания цели к ногам игрока")]
        [SerializeField] private float harpoonPullSpeed = 13.5f;
        [Tooltip("Урон гарпуна")]
        [SerializeField] private float harpoonDamage = 16f;
        [Tooltip("Импульс опрокидывания при притягивании")]
        [SerializeField] private Vector2 harpoonKnockback = new Vector2(8f, 2f);

        [Header("--- 8. ↖️ Веерная защита (Fan Guard) ---")]
        [Tooltip("Смещение защитной арки относительно игрока")]
        [SerializeField] private Vector2 fanArcOffset = new Vector2(0f, 1.3f);
        [Tooltip("Размер защитной дуги (хёртбокс веера)")]
        [SerializeField] private Vector2 fanArcSize = new Vector2(2.8f, 2.0f);
        [Tooltip("Базовый урон веера по наземным целям")]
        [SerializeField] private float fanNormalDamage = 20f;
        [Tooltip("Критический урон при перехвате врага в воздухе (Anti-Air)")]
        [SerializeField] private float fanAntiAirDamage = 30f;
        [Tooltip("Сила сбивания воздушной цели вниз на землю")]
        [SerializeField] private float fanKnockdownForce = 7.0f;
        [Tooltip("Длительность оглушения при сбивании (сек)")]
        [SerializeField] private float fanAirRootDuration = 0.75f;

        [Header("--- Gizmos & Scene View Visualization ---")]
        [Tooltip("Отображать хитбоксы и зоны способностей Тактика в окне Scene")]
        [SerializeField] private bool showGizmosInEditor = true;
        [Tooltip("Отображать сразу все 8 способностей (если выключено, отображается только выбранная)")]
        [SerializeField] private bool showAllAbilitiesInGizmos = true;
        [Tooltip("Какую способность отображать, если выключено 'Show All'")]
        [SerializeField] private Direction8 selectedGizmoAbility = Direction8.Right;
        [Range(0.05f, 0.5f)]
        [SerializeField] private float gizmoFillAlpha = 0.18f;

        private Rigidbody2D _rb;
        private PlayerCombatController2D _playerCombat;
        private HitboxVisualizer2D _visualizer;
        private Coroutine _abilityRoutine;
        private readonly HashSet<Direction8> _unlockedAbilities = new HashSet<Direction8>();

        public event System.Action<Direction8> OnAbilityUnlocked;
        public event System.Action OnAbilitiesReset;

        public bool IsAbilityUnlocked(Direction8 dir) => _unlockedAbilities.Contains(dir);

        public void UnlockAbility(Direction8 dir)
        {
            if (_unlockedAbilities.Add(dir))
            {
                Debug.Log($"<color=#00FFFF><b>[ТАКТИК]</b></color> Способность <b>{GetAbilityName(dir)}</b> разблокирована!");
                OnAbilityUnlocked?.Invoke(dir);
            }
        }

        public void LockAbility(Direction8 dir)
        {
            if (_unlockedAbilities.Remove(dir))
            {
                Debug.Log($"<color=orange><b>[ТАКТИК]</b></color> Способность <b>{GetAbilityName(dir)}</b> заблокирована!");
                OnAbilitiesReset?.Invoke();
            }
        }

        public void ResetAbilities()
        {
            _unlockedAbilities.Clear();
            OnAbilitiesReset?.Invoke();
        }

        public void UnlockAllAbilities()
        {
            foreach (Direction8 dir in System.Enum.GetValues(typeof(Direction8)))
            {
                if (dir != Direction8.None)
                {
                    _unlockedAbilities.Add(dir);
                }
            }
            OnAbilitiesReset?.Invoke();
        }

        public IReadOnlyCollection<Direction8> UnlockedAbilities => _unlockedAbilities;

        public static string GetAbilityName(Direction8 dir)
        {
            switch (dir)
            {
                case Direction8.Right: return "➡️ Прощупывающий выпад";
                case Direction8.Down: return "⬇️ Глубинная печать";
                case Direction8.Up: return "⬆️ Гравитационный якорь";
                case Direction8.Left: return "⬅️ Тактический отход";
                case Direction8.UpRight: return "↗️ Кинетический подброс";
                case Direction8.DownRight: return "↘️ Направленные шипы";
                case Direction8.DownLeft: return "↙️ Магический гарпун";
                case Direction8.UpLeft: return "↖️ Веерная защита";
                default: return dir.ToString();
            }
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _playerCombat = GetComponent<PlayerCombatController2D>();
            _visualizer = GetComponent<HitboxVisualizer2D>();
        }

        /// <summary>
        /// Выполняет способность Тактика по направлению векторного колеса (прямой маппинг 8 направлений).
        /// </summary>
        public void ExecuteAbility(Direction8 dir)
        {
            if (!IsAbilityUnlocked(dir))
            {
                Debug.LogWarning($"<color=orange>[ТАКТИК]</color> Способность {GetAbilityName(dir)} заблокирована! Соберите Кристалл Победы для открытия.");
                return;
            }

            if (_abilityRoutine != null)
            {
                StopCoroutine(_abilityRoutine);
                _abilityRoutine = null;
            }

            switch (dir)
            {
                case Direction8.Right:     // ➡️
                    _abilityRoutine = StartCoroutine(ProbingThrustRoutine());
                    break;

                case Direction8.Down:      // ⬇️
                    ExecuteAbyssalTrap();
                    break;

                case Direction8.Up:        // ⬆️
                    ExecuteGravityAnchor();
                    break;

                case Direction8.Left:      // ⬅️
                    ExecuteTacticalRetreat();
                    break;

                case Direction8.UpRight:   // ↗️
                    _abilityRoutine = StartCoroutine(KineticLaunchRoutine());
                    break;

                case Direction8.DownRight: // ↘️
                    ExecuteDirectionalSpikes();
                    break;

                case Direction8.DownLeft:  // ↙️
                    _abilityRoutine = StartCoroutine(MagicHarpoonRoutine());
                    break;

                case Direction8.UpLeft:    // ↖️
                    _abilityRoutine = StartCoroutine(FanGuardRoutine());
                    break;
            }
        }

        private float GetFacing()
        {
            return _playerCombat != null ? _playerCombat.FacingDirection : 1f;
        }

        // ==========================================
        // 1. ➡️ ПРОЩУПЫВАЮЩИЙ ВЫПАД
        // ==========================================
        private IEnumerator ProbingThrustRoutine()
        {
            if (_playerCombat != null) _playerCombat.SetFacingDirection(1f);

            Vector2 center = (Vector2)transform.position + new Vector2(thrustOffset.x, thrustOffset.y);
            Vector2 size = thrustSize;

            if (_visualizer != null)
            {
                _visualizer.ShowHitbox(center, size, tacticianCyan, isFinisher: false, isCharged: false);
            }

            // Быстрый выпад вправо (+X)
            if (_rb != null)
            {
                _rb.linearVelocity = new Vector2(thrustLungeForce, _rb.linearVelocity.y);
            }

            yield return new WaitForSeconds(0.12f);

            // Проверка попадания
            var cols = Physics2D.OverlapBoxAll(center, size, 0f, enemyLayers);
            var attack = new AttackConfig(
                "Прощупывающий выпад",
                CombatZone.Mid,
                Vector2.zero,
                size,
                0.05f, 0.1f, 0.1f,
                thrustDamage,
                new Vector2(thrustKnockback.x, thrustKnockback.y),
                tacticianCyan
            );

            var hitReceivers = new HashSet<ICombatEntity2D>();
            for (int i = 0; i < cols.Length; i++)
            {
                var c = cols[i];
                if (c == null || c.CompareTag("Player")) continue;

                if (CombatTargetResolver.IsAliveTarget(c, out var target))
                {
                    if (hitReceivers.Add(target))
                    {
                        target.TakeHit(attack, CombatZone.Mid, center, new Vector2(1f, 0.2f).normalized);
                        target.ApplyVulnerabilityMark(thrustVulnerabilityDuration, thrustVulnerabilityMultiplier);
                    }
                }
            }

            yield return new WaitForSeconds(0.08f);
            if (_visualizer != null) _visualizer.HideHitbox();
            _abilityRoutine = null;
        }

        // ==========================================
        // 2. ⬇️ ГЛУБИННАЯ ПЕЧАТЬ (Нажимная ловушка)
        // ==========================================
        private void ExecuteAbyssalTrap()
        {
            Vector3 spawnPos = transform.position + new Vector3(0f, trapSpawnOffset.y, 0f);

            var trapObj = new GameObject("Tactician_Trap");
            trapObj.transform.position = spawnPos;
            var trap = trapObj.AddComponent<TacticianTrap2D>();
            trap.Initialize(trapTriggerRadius, trapDamage, trapRootDuration, trapLifetime, tacticianCyan);

            SpawnPopupText(spawnPos + Vector3.up * 0.8f, "[ГЛУБИННАЯ ПЕЧАТЬ]", tacticianCyan);
            Debug.Log("<color=#00E5FF><b>[ТАКТИК]</b></color> Установлена Глубинная печать прямо под ногами!");
        }

        // ==========================================
        // 3. ⬆️ ГРАВИТАЦИОННЫЙ ЯКОРЬ (Сфера левитации)
        // ==========================================
        private void ExecuteGravityAnchor()
        {
            Vector3 spawnPos = transform.position + new Vector3(0f, anchorSpawnOffset.y, 0f);

            var anchorObj = new GameObject("Tactician_GravityAnchor");
            anchorObj.transform.position = spawnPos;
            var anchor = anchorObj.AddComponent<TacticianGravityAnchor2D>();
            anchor.Initialize(anchorCaptureRadius, anchorSuspensionDuration, anchorLifetime, tacticianPurple);

            SpawnPopupText(spawnPos + Vector3.up * 0.9f, "[ГРАВИТАЦИОННЫЙ ЯКОРЬ]", tacticianPurple);
            Debug.Log("<color=#B266FF><b>[ТАКТИК]</b></color> Создан Гравитационный якорь в воздухе на 5 секунд!");
        }

        // ==========================================
        // 4. ⬅️ ТАКТИЧЕСКИЙ ОТХОД (Бэкдэш + Дым)
        // ==========================================
        private void ExecuteTacticalRetreat()
        {
            Vector3 smokePos = transform.position;

            // 1. Создание дымовой завесы на прежнем месте
            var smokeObj = new GameObject("Tactician_SmokeCloud");
            smokeObj.transform.position = smokePos;
            var smoke = smokeObj.AddComponent<TacticianSmokeCloud2D>();
            smoke.Initialize(smokeRadius, smokeDisorientDuration, smokeLifetime, Color.white);

            // 2. Резкий рывок влево (-X)
            if (_rb != null)
            {
                _rb.linearVelocity = new Vector2(-retreatVelocity.x, retreatVelocity.y);
            }

            SpawnPopupText(smokePos + Vector3.up * 1.2f, "[ТАКТИЧЕСКИЙ ОТХОД]", Color.white);
            Debug.Log("<color=#FFFFFF><b>[ТАКТИК]</b></color> Резкий отскок назад влево со сбросом дымовой завесы!");
        }

        // ==========================================
        // 5. ↗️ КИНЕТИЧЕСКИЙ ПОДБРОС (Juggle Starter)
        // ==========================================
        private IEnumerator KineticLaunchRoutine()
        {
            if (_playerCombat != null) _playerCombat.SetFacingDirection(1f);

            // Ищем ближайшую цель справа (+X) или берем стандартное смещение вправо
            Vector3 targetGroundPos = transform.position + new Vector3(launchDefaultOffset.x, launchDefaultOffset.y, 0f);
            FindNearestTargetInDirection(1f, launchAutoTargetRange, out var targetT, out _);
            if (targetT != null && targetT.position.x > transform.position.x + 0.3f)
            {
                targetGroundPos = new Vector3(targetT.position.x, transform.position.y - 0.6f, 0f);
            }

            Vector2 geyserBoxSize = new Vector2(launchGeyserRadius * 2f, launchGeyserHeight);
            Vector2 geyserCenter = (Vector2)targetGroundPos + new Vector2(0f, launchGeyserHeight * 0.5f);

            // Короткий замах
            yield return new WaitForSeconds(0.12f);

            // Отображаем визуальный хитбокс гейзера в рантайме
            if (_visualizer != null)
            {
                _visualizer.ShowHitbox(geyserCenter, geyserBoxSize, tacticianCyan);
            }

            // Создаем гейзер под ногами цели
            StartCoroutine(KineticGeyserVisualRoutine(targetGroundPos));

            var hits = Physics2D.OverlapBoxAll(geyserCenter, geyserBoxSize, 0f, enemyLayers);
            var launchAttack = new AttackConfig(
                "Кинетический подброс",
                CombatZone.Mid,
                Vector2.zero,
                geyserBoxSize,
                0f, 0.15f, 0.1f,
                launchDamage,
                new Vector2(launchKnockback.x, launchKnockback.y),
                tacticianCyan
            );

            var targets = CombatTargetResolver.GetUniqueAliveTargets(hits, gameObject);
            for (int i = 0; i < targets.Count; i++)
            {
                targets[i].TakeHit(launchAttack, CombatZone.Mid, targetGroundPos, Vector2.up);
            }

            yield return new WaitForSeconds(0.18f);
            if (_visualizer != null) _visualizer.HideHitbox();

            SpawnPopupText(targetGroundPos + Vector3.up * 2.2f, "[КИНЕТИЧЕСКИЙ ПОДБРОС!]", tacticianCyan);
            _abilityRoutine = null;
        }

        private IEnumerator KineticGeyserVisualRoutine(Vector3 groundPos)
        {
            var geyserObj = new GameObject("KineticGeyser");
            geyserObj.transform.position = groundPos;

            var lr = geyserObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.startWidth = launchGeyserRadius * 0.9f;
            lr.endWidth = 0.2f;
            lr.positionCount = 2;
            lr.SetPosition(0, Vector3.zero);
            lr.SetPosition(1, new Vector3(0f, launchGeyserHeight, 0f));
            lr.sortingOrder = 40;

            var mat = new Material(Shader.Find("Sprites/Default"));
            lr.material = mat;
            lr.startColor = tacticianCyan;
            lr.endColor = new Color(0.7f, 0.3f, 1f, 0.8f);

            float dur = 0.28f;
            float el = 0f;
            while (el < dur)
            {
                el += Time.deltaTime;
                float t = el / dur;
                Color sc = lr.startColor;
                sc.a = Mathf.Clamp01(1f - t);
                lr.startColor = sc;
                yield return null;
            }

            Destroy(geyserObj);
        }

        // ==========================================
        // 6. ↘️ НАПРАВЛЕННЫЕ ШИПЫ (Земляная волна)
        // ==========================================
        private void ExecuteDirectionalSpikes()
        {
            if (_playerCombat != null) _playerCombat.SetFacingDirection(1f);

            Vector3 spawnPos = transform.position + new Vector3(spikesSpawnOffset.x, spikesSpawnOffset.y, 0f);

            var spikeObj = new GameObject("Tactician_SpikeWave");
            spikeObj.transform.position = spawnPos;
            var wave = spikeObj.AddComponent<TacticianSpikeWave2D>();
            wave.Initialize(1f, spikesSpeed, spikesMaxDistance, spikesHitRadius, spikesDamage, spikesKnockback, tacticianCyan);

            SpawnPopupText(spawnPos + Vector3.up * 0.9f, "[НАПРАВЛЕННЫЕ ШИПЫ]", tacticianCyan);
            Debug.Log("<color=#00E5FF><b>[ТАКТИК]</b></color> Запущена волна направленных шипов вправо!");
        }

        // ==========================================
        // 7. ↙️ МАГИЧЕСКИЙ ГАРПУН / ХЛЫСТ
        // ==========================================
        private IEnumerator MagicHarpoonRoutine()
        {
            if (_playerCombat != null) _playerCombat.SetFacingDirection(-1f);

            Vector3 origin = transform.position + new Vector3(-harpoonSpawnOffset.x, harpoonSpawnOffset.y, 0f);

            // Ищем цель слева (-X)
            FindNearestTargetInDirection(-1f, harpoonReachDistance, out var targetT, out var targetEntity);
            Vector3 targetPos = targetT != null ? targetT.position : origin + new Vector3(-harpoonReachDistance * 0.85f, -0.4f, 0f);

            // Отрисовка спектральной цепи/хлыста
            var tetherObj = new GameObject("Harpoon_Tether");
            var lr = tetherObj.AddComponent<LineRenderer>();
            lr.startWidth = 0.12f;
            lr.endWidth = 0.06f;
            lr.positionCount = 2;
            lr.SetPosition(0, origin);
            lr.SetPosition(1, targetPos);
            lr.sortingOrder = 42;

            var mat = new Material(Shader.Find("Sprites/Default"));
            lr.material = mat;
            lr.startColor = tacticianPurple;
            lr.endColor = tacticianCyan;

            if (_visualizer != null)
            {
                _visualizer.ShowHitbox(targetPos, new Vector2(1.2f, 1.2f), tacticianPurple);
            }

            var harpoonAttack = new AttackConfig(
                "Магический гарпун",
                CombatZone.Mid | CombatZone.Low,
                Vector2.zero,
                Vector2.one,
                0f, 0.1f, 0.1f,
                harpoonDamage,
                new Vector2(harpoonKnockback.x, harpoonKnockback.y),
                tacticianPurple
            );

            if (targetEntity != null && !targetEntity.IsDead)
            {
                targetEntity.TakeHit(harpoonAttack, CombatZone.Mid, targetPos, Vector2.right);
                // Притягиваем цель к ногам игрока слева
                targetEntity.PullTowards(transform.position + new Vector3(-1.2f, 0f, 0f), harpoonPullSpeed);
                SpawnPopupText(origin + Vector3.up * 1.0f, "[МАГИЧЕСКИЙ ГАРПУН!]", tacticianPurple);
            }

            yield return new WaitForSeconds(0.22f);
            if (_visualizer != null) _visualizer.HideHitbox();
            Destroy(tetherObj);
            _abilityRoutine = null;
        }

        // ==========================================
        // 8. ↖️ ВЕЕРНАЯ ЗАЩИТА (Анти-эйр веер дротиков)
        // ==========================================
        private IEnumerator FanGuardRoutine()
        {
            if (_playerCombat != null) _playerCombat.SetFacingDirection(-1f);

            Vector2 arcCenter = (Vector2)transform.position + new Vector2(-Mathf.Abs(fanArcOffset.x) - 0.45f, fanArcOffset.y);
            Vector2 arcSize = fanArcSize;

            if (_visualizer != null)
            {
                _visualizer.ShowHitbox(arcCenter, arcSize, tacticianGold, isFinisher: false, isCharged: false);
            }

            yield return new WaitForSeconds(0.08f);

            var hits = Physics2D.OverlapBoxAll(arcCenter, arcSize, 0f, enemyLayers);
            var targets = CombatTargetResolver.GetUniqueAliveTargets(hits, gameObject);
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                bool isAirborne = target.transform.position.y > transform.position.y + 0.35f;
                float dmg = isAirborne ? fanAntiAirDamage : fanNormalDamage;

                var fanAttack = new AttackConfig(
                    isAirborne ? "Анти-Эйр Крит!" : "Веерная защита",
                    CombatZone.High,
                    Vector2.zero,
                    arcSize,
                    0f, 0.1f, 0.1f,
                    dmg,
                    new Vector2(-2f, isAirborne ? -fanKnockdownForce : 1f), // Сбивает прыгающих врагов вниз!
                    tacticianGold
                );

                target.TakeHit(fanAttack, CombatZone.High, arcCenter, isAirborne ? Vector2.down : new Vector2(-1f, 1f).normalized);
                if (isAirborne)
                {
                    target.ApplyRoot(fanAirRootDuration); // Микро-стан при сбивании с воздуха
                    SpawnPopupText(target.transform.position + Vector3.up * 1.2f, "[АНТИ-ЭЙР КРИТ!]", tacticianGold);
                }
            }

            yield return new WaitForSeconds(0.1f);
            if (_visualizer != null) _visualizer.HideHitbox();
            _abilityRoutine = null;
        }

        private void FindNearestTargetInDirection(float dirX, float maxDist, out Transform targetTransform, out ICombatEntity2D targetEntity)
        {
            targetTransform = null;
            targetEntity = null;
            if (CombatTargetResolver.FindNearestInDirection(transform.position, dirX, maxDist, out targetEntity))
            {
                targetTransform = targetEntity.transform;
            }
        }

        private void FindNearestTarget(float maxDist, out Transform targetTransform, out ICombatEntity2D targetEntity)
        {
            targetTransform = null;
            targetEntity = null;
            if (CombatTargetResolver.FindNearest(transform.position, maxDist, out targetEntity))
            {
                targetTransform = targetEntity.transform;
            }
        }

        private void SpawnPopupText(Vector3 pos, string text, Color color)
        {
            CombatFloatingText.Spawn(pos, text, color);
        }

        private void OnDrawGizmosSelected()
        {
            if (!showGizmosInEditor) return;

            float facing = Application.isPlaying ? GetFacing() : (transform.localScale.x < 0 ? -1f : 1f);
            Vector3 pos = transform.position;

            if (showAllAbilitiesInGizmos || selectedGizmoAbility == Direction8.Right)
            {
                // 1. ➡️ Прощупывающий выпад
                Vector3 center = pos + new Vector3(facing * thrustOffset.x, thrustOffset.y, 0f);
                DrawBoxGizmo(center, thrustSize, tacticianCyan, "➡️ Прощупывающий выпад");
            }

            if (showAllAbilitiesInGizmos || selectedGizmoAbility == Direction8.Down)
            {
                // 2. ⬇️ Глубинная печать
                Vector3 trapPos = pos + new Vector3(facing * trapSpawnOffset.x, trapSpawnOffset.y, 0f);
                DrawCircleGizmo(trapPos, trapTriggerRadius, tacticianCyan, "⬇️ Глубинная печать (Ловушка)");
            }

            if (showAllAbilitiesInGizmos || selectedGizmoAbility == Direction8.Up)
            {
                // 3. ⬆️ Гравитационный якорь
                Vector3 anchorPos = pos + new Vector3(facing * anchorSpawnOffset.x, anchorSpawnOffset.y, 0f);
                DrawSphereGizmo(anchorPos, anchorCaptureRadius, tacticianPurple, "⬆️ Гравитационный якорь");
            }

            if (showAllAbilitiesInGizmos || selectedGizmoAbility == Direction8.Left)
            {
                // 4. ⬅️ Тактический отход
                DrawArrowGizmo(pos, pos + new Vector3(-facing * 2.5f, 1f, 0f), Color.white);
                DrawCircleGizmo(pos, smokeRadius, new Color(0.9f, 0.9f, 1f, 0.7f), "⬅️ Тактический отход (Дым)");
            }

            if (showAllAbilitiesInGizmos || selectedGizmoAbility == Direction8.UpRight)
            {
                // 5. ↗️ Кинетический подброс
                Vector3 geyserGround = pos + new Vector3(facing * launchDefaultOffset.x, launchDefaultOffset.y, 0f);
                Vector3 geyserCenter = geyserGround + new Vector3(0f, launchGeyserHeight * 0.5f, 0f);
                Vector2 geyserSize = new Vector2(launchGeyserRadius * 2f, launchGeyserHeight);
                DrawBoxGizmo(geyserCenter, geyserSize, tacticianCyan, "↗️ Кинетический подброс");
                DrawArrowGizmo(geyserGround, geyserGround + Vector3.up * launchGeyserHeight, tacticianCyan);
            }

            if (showAllAbilitiesInGizmos || selectedGizmoAbility == Direction8.DownRight)
            {
                // 6. ↘️ Направленные шипы
                Vector3 start = pos + new Vector3(facing * spikesSpawnOffset.x, spikesSpawnOffset.y, 0f);
                Vector3 end = start + new Vector3(facing * spikesMaxDistance, 0f, 0f);
                DrawArrowGizmo(start, end, tacticianCyan);
                DrawBoxGizmo((start + end) * 0.5f, new Vector2(spikesMaxDistance, spikesHitRadius * 2f), tacticianCyan, "↘️ Направленные шипы");
            }

            if (showAllAbilitiesInGizmos || selectedGizmoAbility == Direction8.DownLeft)
            {
                // 7. ↙️ Магический гарпун
                Vector3 origin = pos + new Vector3(facing * harpoonSpawnOffset.x, harpoonSpawnOffset.y, 0f);
                Vector3 reachEnd = origin + new Vector3(facing * harpoonReachDistance, -0.4f, 0f);
                DrawArrowGizmo(reachEnd, origin, tacticianPurple);
                DrawCircleGizmo(reachEnd, 0.8f, tacticianPurple, "↙️ Магический гарпун");
            }

            if (showAllAbilitiesInGizmos || selectedGizmoAbility == Direction8.UpLeft)
            {
                // 8. ↖️ Веерная защита
                Vector3 arcCenter = pos + new Vector3(fanArcOffset.x, fanArcOffset.y, 0f);
                DrawBoxGizmo(arcCenter, fanArcSize, tacticianGold, "↖️ Веерная защита (Anti-Air)");
            }
        }

        private void DrawBoxGizmo(Vector3 center, Vector2 size, Color color, string label)
        {
            Gizmos.color = color;
            Gizmos.DrawWireCube(center, new Vector3(size.x, size.y, 0.1f));
            Gizmos.color = new Color(color.r, color.g, color.b, gizmoFillAlpha);
            Gizmos.DrawCube(center, new Vector3(size.x, size.y, 0.05f));

#if UNITY_EDITOR
            UnityEditor.Handles.color = color;
            UnityEditor.Handles.Label(center + new Vector3(0f, size.y * 0.55f, 0f), label);
#endif
        }

        private void DrawCircleGizmo(Vector3 center, float radius, Color color, string label)
        {
            Gizmos.color = color;
            Gizmos.DrawWireSphere(center, radius);
            Gizmos.color = new Color(color.r, color.g, color.b, gizmoFillAlpha);
            Gizmos.DrawSphere(center, radius);

#if UNITY_EDITOR
            UnityEditor.Handles.color = color;
            UnityEditor.Handles.Label(center + new Vector3(0f, radius * 1.05f, 0f), label);
#endif
        }

        private void DrawSphereGizmo(Vector3 center, float radius, Color color, string label)
        {
            Gizmos.color = color;
            Gizmos.DrawWireSphere(center, radius);
            Gizmos.color = new Color(color.r, color.g, color.b, gizmoFillAlpha);
            Gizmos.DrawSphere(center, radius);

#if UNITY_EDITOR
            UnityEditor.Handles.color = color;
            UnityEditor.Handles.Label(center + new Vector3(0f, radius * 1.05f, 0f), label);
#endif
        }

        private void DrawArrowGizmo(Vector3 from, Vector3 to, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawLine(from, to);
            Vector3 dir = (to - from).normalized;
            if (dir.sqrMagnitude > 0.001f)
            {
                Vector3 right = Quaternion.Euler(0f, 0f, 140f) * dir * 0.45f;
                Vector3 left = Quaternion.Euler(0f, 0f, -140f) * dir * 0.45f;
                Gizmos.DrawLine(to, to + right);
                Gizmos.DrawLine(to, to + left);
            }
        }
    }
}
