using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Combat.Player;
using Combat.UI;

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

        private Rigidbody2D _rb;
        private PlayerCombatController2D _playerCombat;
        private HitboxVisualizer2D _visualizer;
        private Coroutine _abilityRoutine;

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
            float facing = GetFacing();
            Vector2 center = (Vector2)transform.position + new Vector2(facing * 1.35f, 0.05f);
            Vector2 size = new Vector2(1.6f, 0.65f);

            if (_visualizer != null)
            {
                _visualizer.ShowHitbox(center, size, tacticianCyan, isFinisher: false, isCharged: false);
            }

            // Быстрый выпад вперед
            if (_rb != null)
            {
                _rb.linearVelocity = new Vector2(facing * 3.8f, _rb.linearVelocity.y);
            }

            yield return new WaitForSeconds(0.12f);

            // Проверка попадания
            var cols = Physics2D.OverlapBoxAll(center, size, 0f, enemyLayers);
            for (int i = 0; i < cols.Length; i++)
            {
                var c = cols[i];
                if (c == null || c.CompareTag("Player")) continue;

                var enemy = c.GetComponent<EnemyAIController2D>() ?? c.GetComponentInParent<EnemyAIController2D>();
                if (enemy != null && !enemy.IsDead)
                {
                    var attack = new AttackConfig(
                        "Прощупывающий выпад",
                        CombatZone.Mid,
                        Vector2.zero,
                        size,
                        0.05f, 0.1f, 0.1f,
                        18f,
                        new Vector2(facing * 4f, 1f),
                        tacticianCyan
                    );
                    enemy.TakeHit(attack, CombatZone.Mid, center, new Vector2(facing, 0.2f).normalized);
                    enemy.ApplyVulnerabilityMark(7.0f, 1.35f);
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
            float facing = GetFacing();
            Vector3 spawnPos = transform.position + new Vector3(facing * 0.45f, -0.6f, 0f);

            var trapObj = new GameObject("Tactician_Trap");
            trapObj.transform.position = spawnPos;
            trapObj.AddComponent<TacticianTrap2D>();

            SpawnPopupText(spawnPos + Vector3.up * 0.8f, "[ГЛУБИННАЯ ПЕЧАТЬ]", tacticianCyan);
            Debug.Log("<color=#00E5FF><b>[ТАКТИК]</b></color> Установлена Глубинная печать прямо под ногами!");
        }

        // ==========================================
        // 3. ⬆️ ГРАВИТАЦИОННЫЙ ЯКОРЬ (Сфера левитации)
        // ==========================================
        private void ExecuteGravityAnchor()
        {
            float facing = GetFacing();
            Vector3 spawnPos = transform.position + new Vector3(facing * 1.1f, 2.35f, 0f);

            var anchorObj = new GameObject("Tactician_GravityAnchor");
            anchorObj.transform.position = spawnPos;
            anchorObj.AddComponent<TacticianGravityAnchor2D>();

            SpawnPopupText(spawnPos + Vector3.up * 0.9f, "[ГРАВИТАЦИОННЫЙ ЯКОРЬ]", tacticianPurple);
            Debug.Log("<color=#B266FF><b>[ТАКТИК]</b></color> Создан Гравитационный якорь в воздухе на 5 секунд!");
        }

        // ==========================================
        // 4. ⬅️ ТАКТИЧЕСКИЙ ОТХОД (Бэкдэш + Дым)
        // ==========================================
        private void ExecuteTacticalRetreat()
        {
            float facing = GetFacing();
            Vector3 smokePos = transform.position;

            // 1. Создание дымовой завесы на прежнем месте
            var smokeObj = new GameObject("Tactician_SmokeCloud");
            smokeObj.transform.position = smokePos;
            smokeObj.AddComponent<TacticianSmokeCloud2D>();

            // 2. Резкий рывок назад
            if (_rb != null)
            {
                _rb.linearVelocity = new Vector2(-facing * 8.8f, 3.2f);
            }

            SpawnPopupText(smokePos + Vector3.up * 1.2f, "[ТАКТИЧЕСКИЙ ОТХОД]", Color.white);
            Debug.Log("<color=#FFFFFF><b>[ТАКТИК]</b></color> Резкий отскок назад со сбросом дымовой завесы!");
        }

        // ==========================================
        // 5. ↗️ КИНЕТИЧЕСКИЙ ПОДБРОС (Juggle Starter)
        // ==========================================
        private IEnumerator KineticLaunchRoutine()
        {
            float facing = GetFacing();

            // Ищем ближайшего врага перед нами для точечного взрыва
            Vector3 targetGroundPos = transform.position + new Vector3(facing * 2.6f, -0.6f, 0f);
            var enemy = FindNearestEnemy(7.0f);
            if (enemy != null && Mathf.Abs(enemy.transform.position.x - transform.position.x) > 0.5f)
            {
                targetGroundPos = new Vector3(enemy.transform.position.x, transform.position.y - 0.6f, 0f);
            }

            // Короткий замах
            yield return new WaitForSeconds(0.12f);

            // Создаем гейзер под ногами цели
            StartCoroutine(KineticGeyserVisualRoutine(targetGroundPos));

            var hits = Physics2D.OverlapCircleAll(targetGroundPos, 1.4f, enemyLayers);
            for (int i = 0; i < hits.Length; i++)
            {
                var col = hits[i];
                if (col == null || col.CompareTag("Player")) continue;

                var targetEnemy = col.GetComponent<EnemyAIController2D>() ?? col.GetComponentInParent<EnemyAIController2D>();
                if (targetEnemy != null && !targetEnemy.IsDead)
                {
                    var launchAttack = new AttackConfig(
                        "Кинетический подброс",
                        CombatZone.Mid | CombatZone.High,
                        Vector2.zero,
                        new Vector2(1.8f, 2.5f),
                        0f, 0.1f, 0.1f,
                        24f,
                        new Vector2(facing * 1.5f, 10.5f), // Мощнейший запуск прямо в воздух!
                        tacticianCyan
                    );
                    targetEnemy.TakeHit(launchAttack, CombatZone.Mid, targetGroundPos, Vector2.up);
                }
            }

            SpawnPopupText(targetGroundPos + Vector3.up * 2.2f, "[КИНЕТИЧЕСКИЙ ПОДБРОС!]", tacticianCyan);
            _abilityRoutine = null;
        }

        private IEnumerator KineticGeyserVisualRoutine(Vector3 groundPos)
        {
            var geyserObj = new GameObject("KineticGeyser");
            geyserObj.transform.position = groundPos;

            var lr = geyserObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.startWidth = 0.65f;
            lr.endWidth = 0.15f;
            lr.positionCount = 2;
            lr.SetPosition(0, Vector3.zero);
            lr.SetPosition(1, new Vector3(0f, 3.2f, 0f));
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
            float facing = GetFacing();
            Vector3 spawnPos = transform.position + new Vector3(facing * 0.7f, -0.55f, 0f);

            var spikeObj = new GameObject("Tactician_SpikeWave");
            spikeObj.transform.position = spawnPos;
            var wave = spikeObj.AddComponent<TacticianSpikeWave2D>();
            wave.Initialize(facing);

            SpawnPopupText(spawnPos + Vector3.up * 0.9f, "[НАПРАВЛЕННЫЕ ШИПЫ]", tacticianCyan);
            Debug.Log("<color=#00E5FF><b>[ТАКТИК]</b></color> Запущена волна направленных шипов по земле!");
        }

        // ==========================================
        // 7. ↙️ МАГИЧЕСКИЙ ГАРПУН / ХЛЫСТ
        // ==========================================
        private IEnumerator MagicHarpoonRoutine()
        {
            float facing = GetFacing();
            Vector3 origin = transform.position + new Vector3(facing * 0.4f, 0.1f, 0f);

            var enemy = FindNearestEnemy(6.5f);
            Vector3 targetPos = enemy != null ? enemy.transform.position : origin + new Vector3(facing * 5.5f, -0.4f, 0f);

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

            if (enemy != null && !enemy.IsDead)
            {
                var harpoonAttack = new AttackConfig(
                    "Магический гарпун",
                    CombatZone.Mid | CombatZone.Low,
                    Vector2.zero,
                    Vector2.one,
                    0f, 0.1f, 0.1f,
                    16f,
                    new Vector2(-facing * 8f, 2f),
                    tacticianPurple
                );

                enemy.TakeHit(harpoonAttack, CombatZone.Mid, targetPos, -Vector2.right * facing);
                // Притягиваем врага прямо под ноги игроку!
                enemy.PullTowards(transform.position + new Vector3(facing * 1.2f, 0f, 0f), 13.5f);
                SpawnPopupText(origin + Vector3.up * 1.0f, "[МАГИЧЕСКИЙ ГАРПУН!]", tacticianPurple);
            }

            yield return new WaitForSeconds(0.22f);
            Destroy(tetherObj);
            _abilityRoutine = null;
        }

        // ==========================================
        // 8. ↖️ ВЕЕРНАЯ ЗАЩИТА (Анти-эйр веер дротиков)
        // ==========================================
        private IEnumerator FanGuardRoutine()
        {
            float facing = GetFacing();
            Vector2 arcCenter = (Vector2)transform.position + new Vector2(0f, 1.3f);
            Vector2 arcSize = new Vector2(2.8f, 2.0f);

            if (_visualizer != null)
            {
                _visualizer.ShowHitbox(arcCenter, arcSize, tacticianGold, isFinisher: false, isCharged: false);
            }

            yield return new WaitForSeconds(0.08f);

            var hits = Physics2D.OverlapBoxAll(arcCenter, arcSize, 0f, enemyLayers);
            for (int i = 0; i < hits.Length; i++)
            {
                var col = hits[i];
                if (col == null || col.CompareTag("Player")) continue;

                var enemy = col.GetComponent<EnemyAIController2D>() ?? col.GetComponentInParent<EnemyAIController2D>();
                if (enemy != null && !enemy.IsDead)
                {
                    bool isAirborne = enemy.transform.position.y > transform.position.y + 0.35f;
                    float dmg = isAirborne ? 30f : 20f;

                    var fanAttack = new AttackConfig(
                        isAirborne ? "Анти-Эйр Крит!" : "Веерная защита",
                        CombatZone.High,
                        Vector2.zero,
                        arcSize,
                        0f, 0.1f, 0.1f,
                        dmg,
                        new Vector2(facing * 2f, isAirborne ? -7f : 1f), // Сбивает прыгающих врагов вниз!
                        tacticianGold
                    );

                    enemy.TakeHit(fanAttack, CombatZone.High, arcCenter, isAirborne ? Vector2.down : Vector2.up);
                    if (isAirborne)
                    {
                        enemy.ApplyRoot(0.75f); // Микро-стан при сбивании с воздуха
                        SpawnPopupText(enemy.transform.position + Vector3.up * 1.2f, "[АНТИ-ЭЙР КРИТ!]", tacticianGold);
                    }
                }
            }

            yield return new WaitForSeconds(0.1f);
            if (_visualizer != null) _visualizer.HideHitbox();
            _abilityRoutine = null;
        }

        private EnemyAIController2D FindNearestEnemy(float maxDist)
        {
            var enemies = FindObjectsByType<EnemyAIController2D>(FindObjectsSortMode.None);
            EnemyAIController2D nearest = null;
            float minDist = maxDist;

            for (int i = 0; i < enemies.Length; i++)
            {
                var e = enemies[i];
                if (e == null || e.IsDead) continue;
                float d = Vector2.Distance(transform.position, e.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    nearest = e;
                }
            }
            return nearest;
        }

        private void SpawnPopupText(Vector3 pos, string text, Color color)
        {
            StartCoroutine(PopupTextRoutine(pos, text, color));
        }

        private IEnumerator PopupTextRoutine(Vector3 pos, string text, Color color)
        {
            var go = new GameObject("Tactician_Popup");
            go.transform.position = pos;

            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 42;
            tm.characterSize = 0.082f;
            tm.alignment = TextAlignment.Center;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.fontStyle = FontStyle.Bold;
            tm.color = color;

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 80;

            float dur = 1.0f;
            float el = 0f;
            Vector3 startPos = pos;
            Vector3 endPos = pos + new Vector3(0f, 0.9f, 0f);

            while (el < dur)
            {
                el += Time.deltaTime;
                float t = el / dur;
                if (go != null)
                {
                    go.transform.position = Vector3.Lerp(startPos, endPos, t);
                    Color c = tm.color;
                    c.a = Mathf.Clamp01(1f - t);
                    tm.color = c;
                }
                yield return null;
            }

            if (go != null) Destroy(go);
        }
    }
}
