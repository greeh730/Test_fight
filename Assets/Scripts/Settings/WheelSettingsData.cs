using System;
using UnityEngine;

namespace Combat.Settings
{
    public enum ActivationMode
    {
        Hold = 0,
        Toggle = 1
    }

    public enum WheelPlacement
    {
        BottomRight = 0,
        CenterScreen = 1,
        BottomLeft = 2
    }

    public enum FencingWheelPlacement
    {
        BottomLeft = 0,
        BottomRight = 1
    }

    public enum WheelColorTheme
    {
        CrimsonRed = 0,
        CyberCyan = 1,
        SamuraiGold = 2,
        EmeraldGreen = 3,
        HighContrastYellow = 4
    }

    [Serializable]
    public class WheelSettingsData
    {
        [Header("--- Combat & Hitboxes ---")]
        public bool showHitboxes = true;

        [Header("--- Audio & Volume ---")]
        public float masterVolume = 1.0f;          // 0.0 - 1.0
        public float sfxVolume = 1.0f;             // 0.0 - 1.0
        public float footstepsVolume = 0.85f;      // 0.0 - 1.0
        public bool isMuted = false;

        [Header("--- Fencing Wheel ---")]
        [Tooltip("Отображать ли UI колеса фехтования на экране")]
        public bool showFencingWheel = false;

        [Tooltip("Вес шарика: на сколько медленно он тянется за мышкой")]
        public float orbWeight = 0.1f;             // 0.1 - 5.0

        [Tooltip("Ускорение / базовая скорость следования шарика за мышкой")]
        public float orbFollowSpeed = 21.83f;        // 2.0 - 24.0

        [Tooltip("Размер свободной зоны: пока шарик внутри, атака не воспроизводится")]
        public float freeZoneRadius = 0.95f;        // 0.2 - 1.5

        [Tooltip("Угол всех 6 атакующих лепестков в градусах")]
        public float attackSectorAngle = 60.0f;    // 15 - 60

        [Tooltip("Включить или отключить мертвые зоны")]
        public bool enableDeadZone = false;

        [Tooltip("Размер мертвых лепестков сверху и снизу в градусах")]
        public float deadZoneAngle = 0.0f;         // 0 - 90

        [Tooltip("Размер UI колеса в экранных пикселях")]
        public float wheelSize = 107.76f;            // 50 - 150

        [Tooltip("Положение колеса на экране (слева внизу / справа внизу)")]
        public FencingWheelPlacement wheelPlacement = FencingWheelPlacement.BottomLeft;

        [Header("--- Focus Bar UI ---")]
        [Tooltip("Размер шкалы фокуса (масштаб)")]
        public float focusBarScale = 1.91f;        // 0.5 - 2.5

        [Tooltip("Позиция шкалы фокуса X (от левого края экрана)")]
        public float focusBarPosX = 18.08f;         // 0 - 600

        [Tooltip("Позиция шкалы фокуса Y (от верхнего края экрана вниз)")]
        public float focusBarPosY = 83.43f;         // 0 - 400

        [Header("--- Magic Arrow Bar UI ---")]
        [Tooltip("Размер стрелок на лепестках колеса в пикселях")]
        public float arrowIconSize = 52.14f;        // 14 - 54

        [Tooltip("Ширина пустой строки стрелок / действий в пикселях")]
        public float runeBarWidth = 251.27f;        // 80 - 260

        [Tooltip("Высота пустой строки стрелок / действий в пикселях")]
        public float runeBarHeight = 48.0f;        // 16 - 48

        [Tooltip("Смещение строки по X относительно центра колеса")]
        public float runeBarOffsetX = 6.94f;        // -100 - 100

        [Tooltip("Смещение строки по Y над колесом")]
        public float runeBarOffsetY = 71.01f;       // 0 - 80

        [Tooltip("Отображать строку только в стойке магии")]
        public bool showRuneBarOnlyInMagic = true;

        [Header("--- Legacy / Controls Fallback ---")]
        public float gestureSensitivity = 1.0f;
        public float deadzone = 25f;
        public ActivationMode activationMode = ActivationMode.Hold;
        public bool invertX = false;
        public bool invertY = false;
        public bool quickCastOnEdge = false;
        public bool enableGamepad = true;
        public WheelPlacement placement = WheelPlacement.BottomRight;
        public float hudScale = 1.0f;
        public float idleOpacity = 0.45f;
        public bool showAttackPlaque = true;
        public WheelColorTheme colorTheme = WheelColorTheme.CrimsonRed;

        public static WheelSettingsData CreateDefault()
        {
            return new WheelSettingsData();
        }

        public static void GetThemeColors(WheelColorTheme theme, out Color primary, out Color secondary, out Color core)
        {
            switch (theme)
            {
                case WheelColorTheme.CyberCyan:
                    primary = new Color(0f, 0.90f, 1f, 1f);
                    secondary = new Color(0f, 0.45f, 0.85f, 0.8f);
                    core = new Color(0.7f, 0.98f, 1f, 1f);
                    break;
                case WheelColorTheme.SamuraiGold:
                    primary = new Color(1f, 0.78f, 0.15f, 1f);
                    secondary = new Color(0.85f, 0.45f, 0.05f, 0.8f);
                    core = new Color(1f, 0.95f, 0.65f, 1f);
                    break;
                case WheelColorTheme.EmeraldGreen:
                    primary = new Color(0.1f, 0.95f, 0.45f, 1f);
                    secondary = new Color(0.05f, 0.60f, 0.25f, 0.8f);
                    core = new Color(0.7f, 1f, 0.8f, 1f);
                    break;
                case WheelColorTheme.HighContrastYellow:
                    primary = new Color(1f, 1f, 0.1f, 1f);
                    secondary = new Color(0.75f, 0.75f, 0f, 0.9f);
                    core = Color.white;
                    break;
                default:
                    primary = new Color(1f, 0.18f, 0.28f, 1f);
                    secondary = new Color(0.65f, 0.08f, 0.15f, 0.85f);
                    core = new Color(1f, 0.9f, 0.9f, 1f);
                    break;
            }
        }
    }
}
