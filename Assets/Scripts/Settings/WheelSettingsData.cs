using System;
using UnityEngine;

namespace Combat.Settings
{
    public enum ActivationMode
    {
        Hold = 0,    // Зажатие ЛКМ -> жест -> отпускание для удара
        Toggle = 1   // Клик ЛКМ (старт) -> жест -> повторный клик для удара
    }

    public enum WheelPlacement
    {
        BottomRight = 0, // Правый нижний угол
        CenterScreen = 1,// Центр экрана (вокруг прицела)
        BottomLeft = 2   // Левый нижний угол (для левшей)
    }

    public enum WheelColorTheme
    {
        CrimsonRed = 0,      // Бело-красная боевая классика
        CyberCyan = 1,       // Кибернетический аквамарин/неон
        SamuraiGold = 2,     // Золотой янтарь
        EmeraldGreen = 3,    // Изумрудный неоновый
        HighContrastYellow = 4 // Высококонтрастный желтый
    }

    [Serializable]
    public class WheelSettingsData
    {
        [Header("--- Controls & Gameplay ---")]
        public float gestureSensitivity = 1.0f;     // 0.25x - 2.5x
        public float deadzone = 25f;                // 10 - 60 px
        public ActivationMode activationMode = ActivationMode.Hold;
        public bool invertX = false;
        public bool invertY = false;
        public bool quickCastOnEdge = false;
        public bool enableGamepad = true;

        [Header("--- Visuals & HUD ---")]
        public WheelPlacement placement = WheelPlacement.BottomRight;
        public float hudScale = 1.0f;               // 0.75x - 1.4x
        public float idleOpacity = 0.45f;           // 0.0 - 1.0
        public bool showAttackPlaque = true;
        public WheelColorTheme colorTheme = WheelColorTheme.CrimsonRed;

        [Header("--- Audio & Volume ---")]
        public float masterVolume = 1.0f;          // 0.0 - 1.0
        public float sfxVolume = 1.0f;             // 0.0 - 1.0
        public float footstepsVolume = 0.85f;      // 0.0 - 1.0
        public bool isMuted = false;

        public static WheelSettingsData CreateDefault()
        {
            return new WheelSettingsData();
        }

        public static void GetThemeColors(WheelColorTheme theme, out Color primary, out Color secondary, out Color core)
        {
            switch (theme)
            {
                case WheelColorTheme.CyberCyan:
                    primary = new Color(0f, 0.9f, 1f, 0.7f);       // Cyan neon
                    secondary = new Color(0f, 0.6f, 1f, 0.9f);
                    core = new Color(1f, 1f, 1f, 1f);
                    break;

                case WheelColorTheme.SamuraiGold:
                    primary = new Color(1f, 0.78f, 0.15f, 0.75f);  // Gold
                    secondary = new Color(1f, 0.5f, 0.05f, 0.9f);
                    core = new Color(1f, 1f, 0.95f, 1f);
                    break;

                case WheelColorTheme.EmeraldGreen:
                    primary = new Color(0.1f, 1f, 0.45f, 0.7f);    // Emerald
                    secondary = new Color(0f, 0.8f, 0.35f, 0.9f);
                    core = new Color(0.95f, 1f, 0.95f, 1f);
                    break;

                case WheelColorTheme.HighContrastYellow:
                    primary = new Color(1f, 1f, 0f, 0.85f);        // High contrast yellow
                    secondary = new Color(1f, 0.85f, 0f, 1f);
                    core = Color.white;
                    break;

                case WheelColorTheme.CrimsonRed:
                default:
                    primary = new Color(1f, 0.12f, 0.25f, 0.65f);  // Crimson Red
                    secondary = new Color(1f, 0.15f, 0.25f, 0.9f);
                    core = Color.white;
                    break;
            }
        }
    }
}
