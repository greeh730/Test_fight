using System;
using UnityEngine;
using Combat.Common;

namespace Combat.Style
{
    public enum StyleRank
    {
        D = 0,
        C = 1,
        B = 2,
        A = 3,
        S = 4,
        SS = 5,
        SSS = 6
    }

    /// <summary>
    /// Главный менеджер боевого стиля (Style Meter).
    /// Начисляет очки стиля за убийства врагов, контратаки и парирования.
    /// Управляет рангом стиля (D -> C -> B -> A -> S -> SS -> SSS) и шкалой спада.
    /// </summary>
    [DisallowMultipleComponent]
    public class StyleManager : MonoBehaviour
    {
        private static StyleManager _instance;
        public static StyleManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<StyleManager>();
                }
                return _instance;
            }
            private set => _instance = value;
        }

        public static event Action<int, string, StyleRank> OnStyleAdded;
        public static event Action<StyleRank> OnRankChanged;
        public static event Action<int> OnTotalScoreChanged;

        [Header("--- Очки за убийства врагов ---")]
        [Tooltip("Базовые очки стиля за убийство обычного врага")]
        [SerializeField] private int baseKillPoints = 500;

        [Tooltip("Бонусные очки за убийство врага контратакой")]
        [SerializeField] private int counterKillPoints = 850;

        [Tooltip("Бонусные очки за убийство оглушенного/парированного врага")]
        [SerializeField] private int stunnedKillPoints = 750;

        [Tooltip("Очки стиля за уничтожение манекена")]
        [SerializeField] private int dummyKillPoints = 300;

        [Header("--- Параметры спада стиля (Decay) ---")]
        [Tooltip("Задержка перед началом спада шкалы стиля (сек)")]
        [SerializeField] private float decayGracePeriod = 4.5f;

        [Tooltip("Скорость спада очков текущего ранга в секунду")]
        [SerializeField] private float decayRate = 140f;

        [Header("--- Пороги рангов стиля ---")]
        [SerializeField] private float thresholdC = 400f;
        [SerializeField] private float thresholdB = 1000f;
        [SerializeField] private float thresholdA = 1800f;
        [SerializeField] private float thresholdS = 2800f;
        [SerializeField] private float thresholdSS = 4000f;
        [SerializeField] private float thresholdSSS = 5500f;

        // Текущее состояние
        public int TotalScore { get; private set; } = 0;
        public float CurrentRankPoints { get; private set; } = 0f;
        public StyleRank CurrentRank { get; private set; } = StyleRank.D;
        public int TotalKills { get; private set; } = 0;

        private float _decayTimer = 0f;

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
            UpdateDecay(Time.deltaTime);
        }

        private void UpdateDecay(float dt)
        {
            if (_decayTimer > 0f)
            {
                _decayTimer -= dt;
                return;
            }

            if (CurrentRankPoints > 0f)
            {
                CurrentRankPoints = Mathf.Max(0f, CurrentRankPoints - decayRate * dt);
                UpdateRank();
            }
        }

        [Header("--- Множитель стиля за циклы кристалла ---")]
        [Tooltip("Текущий множитель стиля (увеличивается с каждым кругом кристалла)")]
        [SerializeField] private float cycleScoreMultiplier = 1.0f;
        public float CycleScoreMultiplier => cycleScoreMultiplier;

        public void SetCycleMultiplier(float mult)
        {
            cycleScoreMultiplier = Mathf.Max(1.0f, mult);
        }

        /// <summary>
        /// Главный метод начисления очков стиля
        /// </summary>
        public void AddStyle(int points, string reason, Vector3? worldPos = null)
        {
            if (points <= 0) return;

            int scaledPoints = Mathf.RoundToInt(points * cycleScoreMultiplier);

            TotalScore += scaledPoints;
            CurrentRankPoints += scaledPoints;
            _decayTimer = decayGracePeriod;

            StyleRank oldRank = CurrentRank;
            UpdateRank();

            string displayReason = cycleScoreMultiplier > 1.01f ? $"{reason} (x{cycleScoreMultiplier:F2})" : reason;

            OnStyleAdded?.Invoke(scaledPoints, displayReason, CurrentRank);
            OnTotalScoreChanged?.Invoke(TotalScore);

            Color rankCol = GetRankColor(CurrentRank);

            // Всплывающий боевой текст в мировом пространстве над врагом
            if (worldPos.HasValue)
            {
                CombatFloatingText.Spawn(
                    worldPos.Value + Vector3.up * 1.4f,
                    $"+{scaledPoints} {displayReason}",
                    rankCol,
                    1.2f,
                    1.3f,
                    0.09f,
                    46,
                    80
                );
            }

            Debug.Log($"<color=#FFB800><b>[STYLE +{scaledPoints}]</b></color> <b>{displayReason}</b>! Ранг: <color=#{ColorUtility.ToHtmlStringRGB(rankCol)}><b>{CurrentRank}</b></color> (Всего: {TotalScore} PTS)");
        }

        /// <summary>
        /// Начисление стиля за убийство противника
        /// </summary>
        public void AddEnemyKill(EnemyAIController2D enemy, bool wasCounter, Vector3 worldPos)
        {
            TotalKills++;

            if (wasCounter)
            {
                AddStyle(counterKillPoints, "COUNTER FINISHER!", worldPos);
            }
            else if (enemy != null && enemy.CurrentState == EnemyState.Stunned)
            {
                AddStyle(stunnedKillPoints, "STUN EXECUTION!", worldPos);
            }
            else
            {
                AddStyle(baseKillPoints, "ENEMY SLAIN!", worldPos);
            }
        }

        /// <summary>
        /// Начисление стиля за уничтожение манекена
        /// </summary>
        public void AddDummyKill(CombatDummy2D dummy, Vector3 worldPos)
        {
            TotalKills++;
            AddStyle(dummyKillPoints, "DUMMY DESTROYED!", worldPos);
        }

        private void UpdateRank()
        {
            StyleRank newRank;
            if (CurrentRankPoints >= thresholdSSS) newRank = StyleRank.SSS;
            else if (CurrentRankPoints >= thresholdSS) newRank = StyleRank.SS;
            else if (CurrentRankPoints >= thresholdS) newRank = StyleRank.S;
            else if (CurrentRankPoints >= thresholdA) newRank = StyleRank.A;
            else if (CurrentRankPoints >= thresholdB) newRank = StyleRank.B;
            else if (CurrentRankPoints >= thresholdC) newRank = StyleRank.C;
            else newRank = StyleRank.D;

            if (newRank != CurrentRank)
            {
                CurrentRank = newRank;
                OnRankChanged?.Invoke(CurrentRank);
            }
        }

        public float GetRankProgressNormalized()
        {
            float min = 0f;
            float max = thresholdC;

            switch (CurrentRank)
            {
                case StyleRank.D: min = 0f; max = thresholdC; break;
                case StyleRank.C: min = thresholdC; max = thresholdB; break;
                case StyleRank.B: min = thresholdB; max = thresholdA; break;
                case StyleRank.A: min = thresholdA; max = thresholdS; break;
                case StyleRank.S: min = thresholdS; max = thresholdSS; break;
                case StyleRank.SS: min = thresholdSS; max = thresholdSSS; break;
                case StyleRank.SSS: return 1.0f;
            }

            return Mathf.Clamp01((CurrentRankPoints - min) / Mathf.Max(1f, max - min));
        }

        public static string GetRankTitle(StyleRank rank)
        {
            switch (rank)
            {
                case StyleRank.D: return "DULL";
                case StyleRank.C: return "COOL";
                case StyleRank.B: return "BRAVO";
                case StyleRank.A: return "AWESOME";
                case StyleRank.S: return "STYLISH!";
                case StyleRank.SS: return "SUPREME!";
                case StyleRank.SSS: return "SMOKIN' STYLE!";
                default: return "";
            }
        }

        public static Color GetRankColor(StyleRank rank)
        {
            switch (rank)
            {
                case StyleRank.D: return new Color(0.72f, 0.76f, 0.82f, 1f); // Серебристый
                case StyleRank.C: return new Color(0.20f, 0.85f, 1.0f, 1f);  // Голубой
                case StyleRank.B: return new Color(0.25f, 0.95f, 0.45f, 1f); // Изумрудно-зеленый
                case StyleRank.A: return new Color(1.0f, 0.88f, 0.20f, 1f);  // Золотисто-желтый
                case StyleRank.S: return new Color(1.0f, 0.55f, 0.15f, 1f);  // Яркий оранжевый
                case StyleRank.SS: return new Color(1.0f, 0.20f, 0.25f, 1f); // Багровый
                case StyleRank.SSS: return new Color(0.95f, 0.25f, 1.0f, 1f);// Неоновый маджента
                default: return Color.white;
            }
        }

        public void ResetStyle()
        {
            TotalScore = 0;
            CurrentRankPoints = 0f;
            CurrentRank = StyleRank.D;
            _decayTimer = 0f;
            OnRankChanged?.Invoke(CurrentRank);
            OnTotalScoreChanged?.Invoke(TotalScore);
        }
    }
}
