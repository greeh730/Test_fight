// Assets/Scripts/Magic/FogSpell.cs
using System;
using UnityEngine;
using Fencing;

namespace Magic
{
    /// <summary>
    /// Заклинание "Туман" (Fog Spell):
    /// - Базируется на универсальном классе BaseMagicSpell.
    /// - Имеет визуальный стиль тумана: белая обводка, белые/пепельные стрелки и иконка тумана слева сверху.
    /// - При успешном вводе создает полупрозрачное облако клубящегося тумана на небольшом расстоянии перед игроком.
    /// - Враги теряют игрока, если игрок скрыт внутри тумана или за ним, и переходят в режим патрулирования.
    /// </summary>
    [DisallowMultipleComponent]
    public class FogSpell : BaseMagicSpell
    {
        [Header("--- Настройки заклинания Туман ---")]
        [Tooltip("Длительность действия тумана в секундах")]
        [SerializeField] private float fogDuration = 7.0f;

        [Tooltip("Радиус облака тумана в метрах (чуть больше тела игрока)")]
        [SerializeField] private float fogRadius = 1.85f;

        [Tooltip("Смещение точки появления тумана относительно центра модельки игрока (по умолчанию 0, 0 — ровно на себе)")]
        [SerializeField] private Vector2 fogSpawnOffset = Vector2.zero;

        [Tooltip("Префаб облака тумана")]
        [SerializeField] private GameObject fogCloudPrefab;

        [Header("--- Провал заклинания (Dense Fog on Failure) ---")]
        [Tooltip("Длительность плотной дымовой завесы на всю локацию при ошибке ввода")]
        [SerializeField] private float failDenseFogDuration = 6.0f;

        #region Properties
        public float FogDuration { get => fogDuration; set => fogDuration = Mathf.Max(0.5f, value); }
        public float FogRadius { get => fogRadius; set => fogRadius = Mathf.Max(0.5f, value); }
        public Vector2 FogSpawnOffset { get => fogSpawnOffset; set => fogSpawnOffset = value; }
        public GameObject FogCloudPrefab { get => fogCloudPrefab; set => fogCloudPrefab = value; }
        public float FailDenseFogDuration { get => failDenseFogDuration; set => failDenseFogDuration = Mathf.Max(1f, value); }
        #endregion

        protected virtual void Awake()
        {
            spellName = "Туман";
            // Белая обводка по требованию пользователя
            borderColor = Color.white;
            arrowColor = new Color(0.92f, 0.96f, 1.0f, 1f);
            fillColor = new Color(0.04f, 0.05f, 0.07f, 0.96f);

            EnsureFogIcon();
            EnsureFogPrefab();
        }

        private void EnsureFogIcon()
        {
            if (spellIcon == null)
            {
#if UNITY_EDITOR
                spellIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Magic/Fog_Icon.png");
#endif
                if (spellIcon == null)
                {
                    spellIcon = Resources.Load<Sprite>("Sprites/Magic/Fog_Icon");
                }
            }
        }

        private void EnsureFogPrefab()
        {
            if (fogCloudPrefab == null)
            {
#if UNITY_EDITOR
                fogCloudPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Magic/FogCloud.prefab");
#endif
                if (fogCloudPrefab == null)
                {
                    fogCloudPrefab = Resources.Load<GameObject>("Prefabs/Magic/FogCloud");
                }
            }
        }

        protected override void OnCastSuccess()
        {
            if (Combat.Audio.SoundManager.Instance != null)
            {
                Combat.Audio.SoundManager.Instance.PlaySmoke(1.0f);
            }
            CastFog();
        }

        /// <summary>
        /// Создает облако тумана ровно на модельке игрока, моментально скрывая его за дымом.
        /// </summary>
        [ContextMenu("Тестовый вызов тумана (Cast Fog)")]
        public void CastFog()
        {
            float facing = 1f;
            var pc = GetComponent<Combat.Player.PlayerController2D>();
            if (pc != null)
            {
                facing = pc.CurrentFacing;
            }
            else
            {
                var sr = GetComponent<SpriteRenderer>();
                if (sr != null && sr.flipX) facing = -1f;
                else if (transform.localScale.x < 0f) facing = -1f;
            }

            // Центрируем точно по центру хитбокса / модельки тела игрока
            Vector3 spawnPos = transform.position;
            var col = GetComponent<Collider2D>();
            if (col != null)
            {
                spawnPos = col.bounds.center;
            }

            // Добавляем пользовательское смещение (по умолчанию Vector2.zero)
            spawnPos += new Vector3(facing * fogSpawnOffset.x, fogSpawnOffset.y, 0f);

            GameObject cloudObj = null;
            if (fogCloudPrefab != null)
            {
                cloudObj = Instantiate(fogCloudPrefab, spawnPos, Quaternion.identity);
            }
            else
            {
                var loaded = Resources.Load<GameObject>("Prefabs/Magic/FogCloud");
#if UNITY_EDITOR
                if (loaded == null)
                {
                    loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Magic/FogCloud.prefab");
                }
#endif
                if (loaded != null)
                {
                    cloudObj = Instantiate(loaded, spawnPos, Quaternion.identity);
                }
            }

            if (cloudObj != null)
            {
                var cloud = cloudObj.GetComponent<FogCloud>();
                if (cloud != null)
                {
                    cloud.Initialize(fogDuration, fogRadius);
                }
                Debug.Log($"<color=#E0F0FF>[Туман] УСПЕХ!</color> Облако полупрозрачного тумана создано в {spawnPos} (радиус: {fogRadius:F1}м, длительность: {fogDuration:F1}сек).");
            }
            else
            {
                Debug.LogWarning("[Туман] Префаб FogCloud не найден!");
            }
        }

        protected override void OnCastFailure(string reason)
        {
            if (Combat.Audio.SoundManager.Instance != null)
            {
                Combat.Audio.SoundManager.Instance.PlaySmoke(1.2f);
            }
            Debug.LogWarning($"<color=#B0C4DE>[Туман] ОШИБКА ВВОДА!</color> {reason}. Выброс сверхплотного дыма на всю локацию! Враги ослеплены и атакуют наугад.");
            TriggerDenseFogFailure();
        }

        /// <summary>
        /// Полноэкранный густой дым на UI из множества белых прямоугольников: игрок ничего не видит, а враги бьют наугад в панике.
        /// </summary>
        [ContextMenu("Тестовый выброс плотного дыма (Trigger Dense Fog)")]
        public void TriggerDenseFogFailure()
        {
            // Создаем полноэкранный непроницаемый UI дым из белых прямоугольников
            DenseFogUI.Spawn(failDenseFogDuration);

            // Все враги на уровне ослепляются и переходят в панику с ударами наугад
            var enemies = FindObjectsByType<Combat.EnemyAIController2D>(FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i] != null && !enemies[i].IsDead)
                {
                    enemies[i].BlindPanicFromDenseFog(failDenseFogDuration);
                }
            }
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            EnsureFogIcon();
            EnsureFogPrefab();
        }
    }
}
