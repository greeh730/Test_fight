// Assets/Scripts/FocusUI.cs
using UnityEngine;
using UnityEngine.UI;
using Combat.Settings;

/// <summary>
/// UI-индикатор шкалы фокуса в верхнем левом углу экрана.
/// Заполняется синим цветом, а при 100% заполнении излучает теплое золотисто-жёлтое свечение (Emission)
/// со стабильным внешним ореолом без мерцания.
/// </summary>
public class FocusUI : MonoBehaviour
{
    [Header("UI Ссылки")]
    [Tooltip("Компонент Image полоски заполнения (тип Filled, Horizontal)")]
    public Image image;

    [Tooltip("Фоновая подложка шкалы")]
    public Image backgroundImage;

    [Tooltip("Внешний мягкий ореол свечения (Emission Aura)")]
    public Image glowAuraImage;

    [Tooltip("Текстовая метка процента (отключена)")]
    public Text percentText;

    [Header("Цвета индикатора")]
    [Tooltip("Цвет при постепенном заполнении (синий)")]
    public Color normalColor = new Color(0.12f, 0.52f, 1.0f, 1.0f);

    [Tooltip("Фон рамки при обычном заполнении")]
    public Color normalBgColor = new Color(0.06f, 0.08f, 0.14f, 0.88f);

    [Tooltip("Теплый жёлтый цвет при 100% полном заряде")]
    public Color fullColor = new Color(1.0f, 0.84f, 0.18f, 1.0f);

    [Tooltip("Теплый золотистый цвет ореола свечения")]
    public Color fullGlowColor = new Color(1.0f, 0.72f, 0.15f, 0.75f);

    [Tooltip("Фон рамки при полном заряде")]
    public Color fullBgColor = new Color(0.24f, 0.17f, 0.05f, 0.95f);

    private RectTransform _rectTransform;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();

        if (image == null)
        {
            var fill = transform.Find("Fill");
            if (fill != null) image = fill.GetComponent<Image>();
            if (image == null) image = GetComponent<Image>();
        }

        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
        }

        if (glowAuraImage == null)
        {
            var glow = transform.Find("GlowAura");
            if (glow != null) glowAuraImage = glow.GetComponent<Image>();
        }

        // Настройка текста метки стойки фокуса
        var labelTransform = transform.Find("Label");
        if (labelTransform != null)
        {
            labelTransform.gameObject.SetActive(true);
            if (percentText == null) percentText = labelTransform.GetComponent<Text>();
        }

        if (percentText != null)
        {
            percentText.gameObject.SetActive(true);
            percentText.text = "ФОКУС";
            percentText.alignment = TextAnchor.MiddleCenter;
            percentText.fontStyle = FontStyle.Bold;

            var outline = percentText.GetComponent<Outline>();
            if (outline == null) outline = percentText.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1f, -1f);
        }
        ApplyCurrentSettings();
    }

    private void OnEnable()
    {
        ApplyCurrentSettings();
    }

    private void Start()
    {
        ApplyCurrentSettings();
    }

    /// <summary>
    /// Применяет масштаб и положение шкалы фокуса (Pivot и Anchors = Top-Left).
    /// </summary>
    public void ApplySettings(float scale, float posX, float posY)
    {
        // Защита: полоска здоровья героя находится на X=25, Y=-25 размером 260x44 (до Y=-69).
        // Если posY < 75 (например устаревшие настройки 25 px), полоска фокуса перекрывается полоской здоровья.
        // Автоматически корректируем координаты на правильное положение под полоской здоровья:
        if (posY < 75f)
        {
            posY = 83.43f;
        }
        if (posX <= 5f)
        {
            posX = 18.08f;
        }
        if (scale < 1.1f)
        {
            scale = 1.91f;
        }

        if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
        if (_rectTransform != null)
        {
            _rectTransform.anchorMin = new Vector2(0f, 1f);
            _rectTransform.anchorMax = new Vector2(0f, 1f);
            _rectTransform.pivot = new Vector2(0f, 1f);
            _rectTransform.anchoredPosition = new Vector2(posX, -posY);
            _rectTransform.localScale = new Vector3(scale, scale, 1f);
        }
    }

    public void ApplyCurrentSettings()
    {
        if (CombatSettingsManager.Instance != null && CombatSettingsManager.Instance.CurrentSettings != null)
        {
            var s = CombatSettingsManager.Instance.CurrentSettings;
            ApplySettings(s.focusBarScale, s.focusBarPosX, s.focusBarPosY);
        }
        else
        {
            ApplySettings(1.91f, 18.08f, 83.43f);
        }
    }

    private void Update()
    {
        if (FocusManager.Instance != null && image != null)
        {
            float maxF = Mathf.Max(1f, FocusManager.Instance.maxFocus);
            float fillRatio = Mathf.Clamp01(FocusManager.Instance.focusPercent / maxF);
            image.fillAmount = fillRatio;

            // Готов к использованию (достигнут порог активации activationThreshold) -> теплое ровное свечение
            bool isReady = FocusManager.Instance.focusPercent >= FocusManager.Instance.activationThreshold - 0.01f;
            if (isReady)
            {
                image.color = fullColor;

                if (glowAuraImage != null)
                {
                    if (!glowAuraImage.gameObject.activeSelf) glowAuraImage.gameObject.SetActive(true);
                    glowAuraImage.color = fullGlowColor;
                }

                if (backgroundImage != null)
                {
                    backgroundImage.color = fullBgColor;
                }
            }
            else
            {
                // Постепенное заполнение -> синий цвет без свечения
                image.color = normalColor;

                if (glowAuraImage != null && glowAuraImage.gameObject.activeSelf)
                {
                    glowAuraImage.gameObject.SetActive(false);
                }

                if (backgroundImage != null)
                {
                    backgroundImage.color = normalBgColor;
                }
            }
        }
    }
}
