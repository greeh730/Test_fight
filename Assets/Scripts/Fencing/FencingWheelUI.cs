// Assets/Scripts/Fencing/FencingWheelUI.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Fencing
{
    /// <summary>
    /// Контроллер наглядного UI-колеса в правом нижнем углу экрана.
    /// Синхронизирует положение шарика, управляет процедурным рендером секторов,
    /// подсвечивает активные лепестки и мертвые зоны, а также предоставляет гибкие настройки стиля.
    /// </summary>
    public class FencingWheelUI : MonoBehaviour
    {
        [Header("Приоритет Инспектора")]
        [Tooltip("Если включено: размер колеса и стрелок берутся прямо из Инспектора и НЕ перезаписываются PlayerPrefs при запуске игры!")]
        [SerializeField] private bool useInspectorSettings = true;

        [Header("Модель данных")]
        [Tooltip("Ссылка на мировое колесо фехтования")]
        [SerializeField] private FencingWheel wheel;

        [Header("Визуальный стиль UI")]
        [Tooltip("Радиус UI колеса в пикселях")]
        [SerializeField] private float uiRadius = 90f;

        [Tooltip("Отображать ли текстовые метки направлений (L, UL, UR и т.д.) вокруг колеса")]
        [SerializeField] private bool showDirectionLabels = false;

        [Header("Расположение UI")]
        [Tooltip("Положение колеса на экране (слева внизу / справа внизу)")]
        [SerializeField] private Combat.Settings.FencingWheelPlacement placement = Combat.Settings.FencingWheelPlacement.BottomLeft;

        [Header("Компоненты")]
        [SerializeField] private FencingWheelGraphic wheelGraphic;
        [SerializeField] private RectTransform orbIndicator;
        private CanvasGroup canvasGroup;

        [Header("Стрелки на лепестках колеса (Магическая стойка)")]
        [Tooltip("Размер иконки стрелки на лепестке колеса в пикселях")]
        [SerializeField] private float arrowIconSize = 48f;

        private readonly List<Image> arrowIcons = new List<Image>();

        [Header("Строка действий / стрелок (UI над колесом)")]
        [Tooltip("Объект строки над колесом (черный прямоугольник с белой обводкой)")]
        [SerializeField] private RectTransform magicArrowBar;

        [Tooltip("Ширина пустой строки в пикселях")]
        [SerializeField] private float runeBarWidth = 240f;

        [Tooltip("Высота пустой строки в пикселях")]
        [SerializeField] private float runeBarHeight = 48f;

        [Tooltip("Смещение строки относительно верха колеса (X, Y в пикселях)")]
        [SerializeField] private Vector2 runeBarOffset = new Vector2(0f, 26f);

        [Tooltip("Толщина белой рамки в пикселях")]
        [SerializeField] private float runeBarBorderThickness = 2f;

        [Tooltip("Цвет обводки рамки строки")]
        [SerializeField] private Color runeBarBorderColor = Color.white;

        [Tooltip("Цвет фона внутри строки")]
        [SerializeField] private Color runeBarFillColor = new Color(0.04f, 0.04f, 0.06f, 0.96f);

        [Tooltip("Показывать пустую строку только в магической стойке")]
        [SerializeField] private bool showBarOnlyInMagicStance = true;

        [Header("Заклинания и темы магии")]
        [Tooltip("Активное заклинание (BaseMagicSpell)")]
        [SerializeField] private Magic.BaseMagicSpell activeSpell;

        [Tooltip("Компонент механики заклинания Огонь (для обратной совместимости)")]
        [SerializeField] private Magic.FireSpell fireSpell;

        [Header("Иконка заклинания на счётчике")]
        [Tooltip("Размер иконки/значка заклинания в пикселях")]
        [SerializeField] private float spellBadgeSize = 28f;

        [Tooltip("Смещение иконки относительно левого верхнего угла счётчика")]
        [SerializeField] private Vector2 spellBadgeOffset = new Vector2(2f, 2f);

        private float flashTimer = 0f;
        private AttackDirection lastAttackDirection = AttackDirection.None;
        private readonly List<Text> directionLabels = new List<Text>();

        private static readonly Dictionary<AttackDirection, Sprite> cachedArrowSprites = new Dictionary<AttackDirection, Sprite>();

        public FencingWheel Wheel => wheel;
        public FencingWheelGraphic Graphic => wheelGraphic;
        public RectTransform MagicArrowBar => magicArrowBar;
        public RectTransform MagicRuneBar => magicArrowBar;
        public Transform ArrowSlotsContainer => (magicArrowBar != null) ? magicArrowBar.Find("Fill/ArrowSlots") : null;
        public Transform RuneSlotsContainer => ArrowSlotsContainer;
        public Magic.FireSpell FireSpell
        {
            get => (fireSpell != null) ? fireSpell : (activeSpell as Magic.FireSpell);
            set
            {
                fireSpell = value;
                activeSpell = value;
            }
        }
        public Magic.BaseMagicSpell ActiveSpell
        {
            get
            {
                if (activeSpell == null && fireSpell != null) activeSpell = fireSpell;
                return activeSpell;
            }
            set
            {
                activeSpell = value;
                if (activeSpell is Magic.FireSpell fs) fireSpell = fs;
            }
        }
        public float SpellBadgeSize
        {
            get => spellBadgeSize;
            set
            {
                spellBadgeSize = Mathf.Max(12f, value);
                UpdateSpellIconBadgeLayout();
            }
        }
        public Vector2 SpellBadgeOffset
        {
            get => spellBadgeOffset;
            set
            {
                spellBadgeOffset = value;
                UpdateSpellIconBadgeLayout();
            }
        }
        public float ArrowIconSize
        {
            get => arrowIconSize;
            set
            {
                arrowIconSize = Mathf.Max(12f, value);
                UpdateArrowIconsPosition();
            }
        }
        public float RuneIconSize
        {
            get => arrowIconSize;
            set => ArrowIconSize = value;
        }
        public List<Image> ArrowIcons => arrowIcons;
        public List<Image> RuneIcons => arrowIcons;

        public float RuneBarWidth
        {
            get => runeBarWidth;
            set
            {
                runeBarWidth = Mathf.Max(20f, value);
                UpdateMagicRuneBarStyle();
            }
        }

        public float RuneBarHeight
        {
            get => runeBarHeight;
            set
            {
                runeBarHeight = Mathf.Max(10f, value);
                UpdateMagicRuneBarStyle();
            }
        }

        public Vector2 RuneBarOffset
        {
            get => runeBarOffset;
            set
            {
                runeBarOffset = value;
                UpdateMagicRuneBarStyle();
            }
        }

        public float RuneBarBorderThickness
        {
            get => runeBarBorderThickness;
            set
            {
                runeBarBorderThickness = Mathf.Max(0.5f, value);
                UpdateMagicRuneBarStyle();
            }
        }

        public Color RuneBarBorderColor
        {
            get => runeBarBorderColor;
            set
            {
                runeBarBorderColor = value;
                UpdateMagicRuneBarStyle();
            }
        }

        public Color RuneBarFillColor
        {
            get => runeBarFillColor;
            set
            {
                runeBarFillColor = value;
                UpdateMagicRuneBarStyle();
            }
        }

        public bool ShowBarOnlyInMagicStance
        {
            get => showBarOnlyInMagicStance;
            set
            {
                showBarOnlyInMagicStance = value;
                if (wheel != null) HandleStanceChanged(wheel.CurrentStance);
            }
        }

        public float UIRadius
        {
            get => uiRadius;
            set
            {
                uiRadius = Mathf.Max(40f, value);
                if (wheelGraphic != null) wheelGraphic.UIRadius = uiRadius;
                UpdateLabelsPosition();
                UpdateRuneIconsPosition();
                UpdateMagicRuneBarPosition();
            }
        }

        public bool ShowDirectionLabels
        {
            get => showDirectionLabels;
            set
            {
                showDirectionLabels = value;
                if (value && directionLabels.Count == 0)
                {
                    CreateDirectionLabels();
                }
                else
                {
                    foreach (var lbl in directionLabels)
                    {
                        if (lbl != null) lbl.gameObject.SetActive(value);
                    }
                }
            }
        }

        public Combat.Settings.FencingWheelPlacement Placement
        {
            get => placement;
            set
            {
                placement = value;
                ApplyPlacement();
            }
        }

        public void ApplyPlacement()
        {
            var rt = GetComponent<RectTransform>();
            if (rt == null) return;

            rt.pivot = new Vector2(0.5f, 0.5f);
            float margin = uiRadius + 35f;

            if (placement == Combat.Settings.FencingWheelPlacement.BottomRight)
            {
                rt.anchorMin = new Vector2(1f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.anchoredPosition = new Vector2(-margin, margin);
            }
            else
            {
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 0f);
                rt.anchoredPosition = new Vector2(margin, margin);
            }
            rt.sizeDelta = new Vector2(uiRadius * 2.5f, uiRadius * 2.5f);
        }

        public bool UseInspectorSettings
        {
            get => useInspectorSettings;
            set => useInspectorSettings = value;
        }

        public void ApplySettings(Combat.Settings.WheelSettingsData data)
        {
            if (data == null) return;

            if (useInspectorSettings)
            {
                placement = data.wheelPlacement;
                ApplyPlacement();

                if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
                canvasGroup.alpha = data.showFencingWheel ? 1f : 0f;
                canvasGroup.blocksRaycasts = data.showFencingWheel;
                canvasGroup.interactable = data.showFencingWheel;

                UpdateArrowIconsPosition();

                if (magicArrowBar == null)
                {
                    var t = transform.Find("MagicArrowBar");
                    if (t == null) t = transform.Find("MagicRuneBar");
                    if (t != null) magicArrowBar = t.GetComponent<RectTransform>();
                }
                UpdateMagicArrowBarStyle();

                if (magicArrowBar != null && wheel != null)
                {
                    bool isMagic = (wheel.CurrentStance == StanceType.Magic);
                    magicArrowBar.gameObject.SetActive(!showBarOnlyInMagicStance || isMagic);
                }
                return;
            }

            placement = data.wheelPlacement;
            UIRadius = data.wheelSize;
            ApplyPlacement();

            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = data.showFencingWheel ? 1f : 0f;
            canvasGroup.blocksRaycasts = data.showFencingWheel;
            canvasGroup.interactable = data.showFencingWheel;

            if (data.arrowIconSize > 0f) arrowIconSize = data.arrowIconSize;
            UpdateArrowIconsPosition();

            if (data.runeBarWidth > 0f) runeBarWidth = data.runeBarWidth;
            if (data.runeBarHeight > 0f) runeBarHeight = data.runeBarHeight;
            runeBarOffset = new Vector2(data.runeBarOffsetX, data.runeBarOffsetY);
            showBarOnlyInMagicStance = data.showRuneBarOnlyInMagic;
            if (magicArrowBar == null)
            {
                var t = transform.Find("MagicArrowBar");
                if (t == null) t = transform.Find("MagicRuneBar");
                if (t != null) magicArrowBar = t.GetComponent<RectTransform>();
            }
            UpdateMagicArrowBarStyle();

            if (magicArrowBar != null && wheel != null)
            {
                bool isMagic = (wheel.CurrentStance == StanceType.Magic);
                magicArrowBar.gameObject.SetActive(!showBarOnlyInMagicStance || isMagic);
            }
        }

        private void Awake()
        {
            if (wheel == null)
            {
#if UNITY_6000_0_OR_NEWER
                wheel = FindAnyObjectByType<FencingWheel>();
#else
                wheel = FindObjectOfType<FencingWheel>();
#endif
            }

            BuildUIHierarchyIfNeeded();
        }

        private void Start()
        {
            if (Combat.Settings.CombatSettingsManager.Instance != null)
            {
                ApplySettings(Combat.Settings.CombatSettingsManager.Instance.CurrentSettings);
            }
        }

        private void OnEnable()
        {
            if (wheel != null)
            {
                wheel.OnAttack += HandleAttack;
                wheel.OnGeometryChanged += HandleGeometryChanged;
                wheel.OnStanceChanged += HandleStanceChanged;
                HandleStanceChanged(wheel.CurrentStance);
            }

            Combat.Settings.CombatSettingsManager.OnSettingsChanged += HandleSettingsChanged;
            if (Combat.Settings.CombatSettingsManager.Instance != null)
            {
                ApplySettings(Combat.Settings.CombatSettingsManager.Instance.CurrentSettings);
            }
        }

        private void OnDisable()
        {
            if (wheel != null)
            {
                wheel.OnAttack -= HandleAttack;
                wheel.OnGeometryChanged -= HandleGeometryChanged;
                wheel.OnStanceChanged -= HandleStanceChanged;
            }

            Combat.Settings.CombatSettingsManager.OnSettingsChanged -= HandleSettingsChanged;
        }

        private void HandleSettingsChanged(Combat.Settings.WheelSettingsData data)
        {
            ApplySettings(data);
        }

        private void HandleStanceChanged(StanceType stance)
        {
            bool isMagic = (stance == StanceType.Magic);

            if (wheelGraphic != null)
            {
                wheelGraphic.SetStance(stance);
            }

            if (orbIndicator != null)
            {
                var img = orbIndicator.GetComponent<Image>();
                if (img != null)
                {
                    img.color = isMagic 
                        ? new Color(0.25f, 0.85f, 1.0f, 1f) 
                        : new Color(1.0f, 0.42f, 0.25f, 1f);
                }
            }

            // Стрелки отображаются ТОЛЬКО в магической стойке
            for (int i = 0; i < arrowIcons.Count; i++)
            {
                if (arrowIcons[i] != null)
                {
                    arrowIcons[i].gameObject.SetActive(isMagic);
                }
            }

            for (int i = 0; i < directionLabels.Count; i++)
            {
                if (directionLabels[i] != null)
                {
                    directionLabels[i].gameObject.SetActive(isMagic && showDirectionLabels);
                }
            }

            // Пустая строка над колесом
            if (magicArrowBar != null)
            {
                magicArrowBar.gameObject.SetActive(!showBarOnlyInMagicStance || isMagic);
            }
        }

        private void Update()
        {
            if (wheel == null) return;

            // Синхронизация палитры колеса со стойкой
            if (wheelGraphic != null && wheelGraphic.CurrentStance != wheel.CurrentStance)
            {
                HandleStanceChanged(wheel.CurrentStance);
            }

            bool isMagic = (wheel.CurrentStance == StanceType.Magic);

            if (magicArrowBar != null)
            {
                bool targetBarActive = (!showBarOnlyInMagicStance || isMagic);
                if (magicArrowBar.gameObject.activeSelf != targetBarActive)
                {
                    magicArrowBar.gameObject.SetActive(targetBarActive);
                }
            }

            if (wheel.Orb == null || orbIndicator == null) return;

            // 1. Позиция шарика в UI
            Vector2 rawLocal = wheel.Orb.LocalPosition;
            float currentWheelRadius = Mathf.Max(0.01f, wheel.WheelRadius);
            Vector2 normalizedPos = rawLocal / currentWheelRadius;

            orbIndicator.anchoredPosition = normalizedPos * uiRadius;

            // 2. Определение текущего подсвечиваемого сектора
            AttackDirection hovered = wheel.GetAttackDirection(rawLocal);
            if (hovered == AttackDirection.None && rawLocal.magnitude > wheel.FreeZoneRadius * 0.35f)
            {
                hovered = wheel.GetAimDirection(rawLocal);
            }

            // 3. Затухание вспышки удара
            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime * 4.5f;
                if (flashTimer < 0f) flashTimer = 0f;
            }

            // 4. Передача данных в процедурный графический компонент
            if (wheelGraphic != null)
            {
                wheelGraphic.UpdateOrbState(normalizedPos, hovered, lastAttackDirection, flashTimer);
            }

            // 5. Интерактивная подсветка и масштабирование стрелок на лепестках колеса (ТОЛЬКО в стойке магии, цвет ВСЕГДА белый)
            for (int i = 0; i < arrowIcons.Count && i < wheel.AttackSectors.Count; i++)
            {
                var arrowImg = arrowIcons[i];
                if (arrowImg == null) continue;

                if (!isMagic)
                {
                    if (arrowImg.gameObject.activeSelf) arrowImg.gameObject.SetActive(false);
                    continue;
                }
                else
                {
                    if (!arrowImg.gameObject.activeSelf) arrowImg.gameObject.SetActive(true);
                }

                AttackDirection dir = wheel.AttackSectors[i].direction;

                bool isHovered = (hovered == dir);
                bool isFlashing = (lastAttackDirection == dir && flashTimer > 0.01f);

                float targetScale = isHovered ? 1.35f : 1.05f;
                arrowImg.rectTransform.localScale = Vector3.Lerp(arrowImg.rectTransform.localScale, Vector3.one * targetScale, Time.deltaTime * 14f);

                if (wheel.IsMagicLocked)
                {
                    arrowImg.color = new Color(0.4f, 0.4f, 0.45f, 0.3f);
                }
                else if (isFlashing || isHovered)
                {
                    arrowImg.color = (ActiveSpell != null && ActiveSpell.ThemeWheelArrows)
                        ? Color.Lerp(ActiveSpell.ArrowColor, Color.white, 0.5f)
                        : Color.white;
                }
                else
                {
                    arrowImg.color = (ActiveSpell != null && ActiveSpell.ThemeWheelArrows)
                        ? ActiveSpell.ArrowColor
                        : new Color(1f, 1f, 1f, 0.85f);
                }
            }

            // 6. Подсветка внешних текстовых меток (ТОЛЬКО в стойке магии)
            for (int i = 0; i < directionLabels.Count && i < wheel.AttackSectors.Count; i++)
            {
                var lbl = directionLabels[i];
                if (lbl == null) continue;

                if (!isMagic || !showDirectionLabels)
                {
                    if (lbl.gameObject.activeSelf) lbl.gameObject.SetActive(false);
                    continue;
                }
                else
                {
                    if (!lbl.gameObject.activeSelf) lbl.gameObject.SetActive(true);
                }

                AttackDirection dir = wheel.AttackSectors[i].direction;
                Color baseColor = GetDirectionRuneColor(dir);

                bool isHovered = (hovered == dir);
                bool isFlashing = (lastAttackDirection == dir && flashTimer > 0.01f);

                float targetScale = isHovered ? 1.2f : 1.0f;
                lbl.rectTransform.localScale = Vector3.Lerp(lbl.rectTransform.localScale, Vector3.one * targetScale, Time.deltaTime * 14f);

                if (isFlashing)
                {
                    lbl.color = Color.Lerp(baseColor, Color.white, flashTimer);
                }
                else if (isHovered)
                {
                    lbl.color = Color.Lerp(baseColor, Color.white, 0.35f);
                }
                else
                {
                    Color c = baseColor;
                    c.a = 0.85f;
                    lbl.color = c;
                }
            }
        }

        private void HandleAttack(AttackDirection dir)
        {
            lastAttackDirection = dir;
            flashTimer = 1.0f;
        }

        private void HandleGeometryChanged()
        {
            if (wheelGraphic != null)
            {
                wheelGraphic.SetVerticesDirty();
            }
            UpdateLabelsPosition();
            UpdateArrowIconsPosition();
            UpdateMagicArrowBarStyle();
        }

        /// <summary>
        /// Строит иерархию элементов UI-колеса в левом нижнем углу Canvas.
        /// </summary>
        public void BuildUIHierarchyIfNeeded()
        {
            RectTransform rootRect = GetComponent<RectTransform>();
            if (rootRect == null) rootRect = gameObject.AddComponent<RectTransform>();

            ApplyPlacement();

            // Удаляем старые дочерние объекты при пересборке
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }
            directionLabels.Clear();

            // 1. Процедурная графика колеса
            GameObject graphicObj = new GameObject("WheelMeshGraphic", typeof(RectTransform));
            graphicObj.transform.SetParent(transform, false);
            RectTransform gRect = graphicObj.GetComponent<RectTransform>();
            gRect.sizeDelta = new Vector2(uiRadius * 2.2f, uiRadius * 2.2f);
            gRect.anchoredPosition = Vector2.zero;

            wheelGraphic = graphicObj.AddComponent<FencingWheelGraphic>();
            wheelGraphic.UIRadius = uiRadius;
            wheelGraphic.BindWheel(wheel);

            // 2. Стрелки направлений прямо на лепестках колеса (чистый белый цвет)
            CreateArrowIconsOnWheel();

            // 3. Пустая строка действий / стрелок над колесом (черный прямоугольник с белой обводкой)
            CreateMagicArrowBar();

            if (activeSpell == null && fireSpell == null)
            {
#if UNITY_6000_0_OR_NEWER
                activeSpell = FindAnyObjectByType<Magic.BaseMagicSpell>();
#else
                activeSpell = FindObjectOfType<Magic.BaseMagicSpell>();
#endif
                if (activeSpell is Magic.FireSpell fs) fireSpell = fs;
            }
            if (ActiveSpell != null)
            {
                ActiveSpell.ApplySpellTheme(this);
                ActiveSpell.UpdateSlotsUI();
            }

            // 4. Индикатор шарика (яркая светящаяся точка)
            GameObject orbObj = new GameObject("OrbIndicator", typeof(RectTransform), typeof(Image));
            orbObj.transform.SetParent(transform, false);
            orbIndicator = orbObj.GetComponent<RectTransform>();
            orbIndicator.sizeDelta = new Vector2(20f, 20f);

            Image orbImg = orbObj.GetComponent<Image>();
            orbImg.sprite = CreateCircleSprite(48);
            orbImg.color = new Color(1f, 0.92f, 0.15f, 1f);
            orbImg.raycastTarget = false;

            // Точка в центре шарика (для прицела)
            GameObject orbDot = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            orbDot.transform.SetParent(orbObj.transform, false);
            RectTransform dotRect = orbDot.GetComponent<RectTransform>();
            dotRect.sizeDelta = new Vector2(6f, 6f);
            Image dotImg = orbDot.GetComponent<Image>();
            dotImg.sprite = CreateCircleSprite(16);
            dotImg.color = Color.white;
            dotImg.raycastTarget = false;

            // 4. Метки направлений вокруг колеса (отключены по умолчанию)
            if (showDirectionLabels)
            {
                CreateDirectionLabels();
            }

            if (wheel != null)
            {
                HandleStanceChanged(wheel.CurrentStance);
            }
        }

        private void CreateArrowIconsOnWheel()
        {
            arrowIcons.Clear();
            if (wheel == null || wheel.AttackSectors == null) return;

            foreach (var sec in wheel.AttackSectors)
            {
                GameObject arrowObj = new GameObject($"Arrow_{sec.direction}", typeof(RectTransform), typeof(Image));
                arrowObj.transform.SetParent(transform, false);

                RectTransform rt = arrowObj.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(arrowIconSize, arrowIconSize);

                Image img = arrowObj.GetComponent<Image>();
                img.sprite = GetArrowSprite(sec.direction);
                img.color = Color.white; // Все стрелки чисто белые!
                img.raycastTarget = false;

                arrowIcons.Add(img);
            }

            UpdateArrowIconsPosition();
        }

        public void CreateRuneIconsOnWheel() => CreateArrowIconsOnWheel();

        private void UpdateArrowIconsPosition()
        {
            if (wheel == null || wheel.AttackSectors == null) return;

            if (arrowIcons.Count == 0)
            {
                foreach (var sec in wheel.AttackSectors)
                {
                    var t = transform.Find($"Arrow_{sec.direction}");
                    if (t == null) t = transform.Find($"Rune_{sec.direction}");
                    if (t != null)
                    {
                        var img = t.GetComponent<Image>();
                        if (img != null) arrowIcons.Add(img);
                    }
                }
            }

            float iconRadius = uiRadius * 0.66f; // По центру между внутренней свободной зоной и внешним краем
            for (int i = 0; i < wheel.AttackSectors.Count && i < arrowIcons.Count; i++)
            {
                if (arrowIcons[i] == null) continue;
                AttackSector sec = wheel.AttackSectors[i];
                float rad = sec.angleCenter * Mathf.Deg2Rad;
                Vector2 pos = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * iconRadius;
                arrowIcons[i].rectTransform.anchoredPosition = pos;
                arrowIcons[i].rectTransform.sizeDelta = new Vector2(arrowIconSize, arrowIconSize);
            }
        }

        public void UpdateRuneIconsPosition() => UpdateArrowIconsPosition();

        private void CreateMagicArrowBar()
        {
            float barY = uiRadius + runeBarOffset.y;

            GameObject barObj = new GameObject("MagicArrowBar", typeof(RectTransform), typeof(Image));
            barObj.transform.SetParent(transform, false);

            magicArrowBar = barObj.GetComponent<RectTransform>();
            magicArrowBar.sizeDelta = new Vector2(runeBarWidth, runeBarHeight);
            magicArrowBar.anchoredPosition = new Vector2(runeBarOffset.x, barY);

            // Белая обводка (внешний прямоугольник)
            Image borderImg = barObj.GetComponent<Image>();
            borderImg.color = runeBarBorderColor;
            borderImg.raycastTarget = false;

            // Черная заливка (внутренний прямоугольник с отступом толщины рамки)
            GameObject fillObj = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObj.transform.SetParent(barObj.transform, false);

            RectTransform fillRt = fillObj.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(runeBarBorderThickness, runeBarBorderThickness);
            fillRt.offsetMax = new Vector2(-runeBarBorderThickness, -runeBarBorderThickness);

            Image fillImg = fillObj.GetComponent<Image>();
            fillImg.color = runeBarFillColor;
            fillImg.raycastTarget = false;

            // Контейнер для будущих стрелок / действий
            GameObject slotsObj = new GameObject("ArrowSlots", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            slotsObj.transform.SetParent(fillObj.transform, false);

            RectTransform slotsRt = slotsObj.GetComponent<RectTransform>();
            slotsRt.anchorMin = Vector2.zero;
            slotsRt.anchorMax = Vector2.one;
            slotsRt.offsetMin = new Vector2(4f, 2f);
            slotsRt.offsetMax = new Vector2(-4f, -2f);

            HorizontalLayoutGroup hlg = slotsObj.GetComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 6f;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;

            // Значок / иконка заклинания слева сверху счётчика
            CreateSpellIconBadge(barObj.transform);

            bool isMagic = (wheel != null && wheel.CurrentStance == StanceType.Magic);
            magicArrowBar.gameObject.SetActive(!showBarOnlyInMagicStance || isMagic);

            if (ActiveSpell != null)
            {
                ActiveSpell.ApplySpellTheme(this);
            }
        }

        private void CreateSpellIconBadge(Transform parent)
        {
            if (parent == null) return;

            Transform existing = parent.Find("SpellIconBadge");
            if (existing != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(existing.gameObject);
                else Destroy(existing.gameObject);
#else
                Destroy(existing.gameObject);
#endif
            }

            Color currentBorder = (ActiveSpell != null) ? ActiveSpell.BorderColor : runeBarBorderColor;
            Color currentFill = (ActiveSpell != null) ? ActiveSpell.FillColor : runeBarFillColor;
            Sprite iconSprite = (ActiveSpell != null) ? ActiveSpell.SpellIcon : null;

            // Внешняя рамка бейджа иконки
            GameObject badgeObj = new GameObject("SpellIconBadge", typeof(RectTransform), typeof(Image));
            badgeObj.transform.SetParent(parent, false);

            RectTransform badgeRt = badgeObj.GetComponent<RectTransform>();
            badgeRt.anchorMin = new Vector2(0f, 1f);
            badgeRt.anchorMax = new Vector2(0f, 1f);
            badgeRt.pivot = new Vector2(0.5f, 0.5f);
            badgeRt.sizeDelta = new Vector2(spellBadgeSize, spellBadgeSize);
            badgeRt.anchoredPosition = spellBadgeOffset;

            Image borderImg = badgeObj.GetComponent<Image>();
            borderImg.color = currentBorder;
            borderImg.raycastTarget = false;

            // Внутренний темный фон
            GameObject innerObj = new GameObject("InnerBg", typeof(RectTransform), typeof(Image));
            innerObj.transform.SetParent(badgeObj.transform, false);

            RectTransform innerRt = innerObj.GetComponent<RectTransform>();
            innerRt.anchorMin = Vector2.zero;
            innerRt.anchorMax = Vector2.one;
            innerRt.offsetMin = new Vector2(runeBarBorderThickness, runeBarBorderThickness);
            innerRt.offsetMax = new Vector2(-runeBarBorderThickness, -runeBarBorderThickness);

            Image innerImg = innerObj.GetComponent<Image>();
            innerImg.color = currentFill;
            innerImg.raycastTarget = false;

            // Изображение самой иконки
            GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(innerObj.transform, false);

            RectTransform iconRt = iconObj.GetComponent<RectTransform>();
            iconRt.anchorMin = Vector2.zero;
            iconRt.anchorMax = Vector2.one;
            iconRt.offsetMin = new Vector2(2f, 2f);
            iconRt.offsetMax = new Vector2(-2f, -2f);

            Image iconImg = iconObj.GetComponent<Image>();
            iconImg.sprite = iconSprite;
            iconImg.color = Color.white;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            badgeObj.SetActive(iconSprite != null);
        }

        private void UpdateSpellIconBadgeLayout()
        {
            if (magicArrowBar == null) return;
            var badgeT = magicArrowBar.Find("SpellIconBadge");
            if (badgeT == null) return;

            RectTransform badgeRt = badgeT.GetComponent<RectTransform>();
            if (badgeRt != null)
            {
                badgeRt.sizeDelta = new Vector2(spellBadgeSize, spellBadgeSize);
                badgeRt.anchoredPosition = spellBadgeOffset;
            }
        }

        /// <summary>
        /// Применяет визуальную тему активного заклинания: цвет обводки, фона и иконку слева сверху счётчика.
        /// </summary>
        public void SetSpellTheme(Sprite icon, Color borderColor, Color arrowColor, Color? fillColor = null)
        {
            if (magicArrowBar == null)
            {
                var t = transform.Find("MagicArrowBar");
                if (t == null) t = transform.Find("MagicRuneBar");
                if (t != null) magicArrowBar = t.GetComponent<RectTransform>();
            }
            if (magicArrowBar == null) return;

            // 1. Обводка счётчика под цвет заклинания
            Image borderImg = magicArrowBar.GetComponent<Image>();
            if (borderImg != null) borderImg.color = borderColor;

            // 2. Фон счётчика
            if (fillColor.HasValue)
            {
                Transform fillT = magicArrowBar.Find("Fill");
                if (fillT != null)
                {
                    Image fillImg = fillT.GetComponent<Image>();
                    if (fillImg != null) fillImg.color = fillColor.Value;
                }
            }

            // 3. Иконка заклинания слева сверху
            Transform badgeT = magicArrowBar.Find("SpellIconBadge");
            if (badgeT == null)
            {
                CreateSpellIconBadge(magicArrowBar);
                badgeT = magicArrowBar.Find("SpellIconBadge");
            }

            if (badgeT != null)
            {
                badgeT.gameObject.SetActive(icon != null);
                Image badgeBorder = badgeT.GetComponent<Image>();
                if (badgeBorder != null) badgeBorder.color = borderColor;

                if (fillColor.HasValue)
                {
                    Transform innerBgT = badgeT.Find("InnerBg");
                    if (innerBgT != null)
                    {
                        Image innerImg = innerBgT.GetComponent<Image>();
                        if (innerImg != null) innerImg.color = fillColor.Value;
                    }
                }

                Transform iconT = badgeT.Find("InnerBg/Icon");
                if (iconT != null)
                {
                    Image iconImg = iconT.GetComponent<Image>();
                    if (iconImg != null)
                    {
                        iconImg.sprite = icon;
                        iconImg.color = Color.white;
                    }
                }
            }
        }

        public void CreateMagicRuneBar() => CreateMagicArrowBar();

        public void UpdateMagicArrowBarStyle()
        {
            if (magicArrowBar == null)
            {
                var t = transform.Find("MagicArrowBar");
                if (t == null) t = transform.Find("MagicRuneBar");
                if (t != null) magicArrowBar = t.GetComponent<RectTransform>();
            }
            if (magicArrowBar == null) return;
            float barY = uiRadius + runeBarOffset.y;
            magicArrowBar.anchoredPosition = new Vector2(runeBarOffset.x, barY);
            magicArrowBar.sizeDelta = new Vector2(runeBarWidth, runeBarHeight);

            Color currentBorder = (ActiveSpell != null) ? ActiveSpell.BorderColor : runeBarBorderColor;
            Color currentFill = (ActiveSpell != null) ? ActiveSpell.FillColor : runeBarFillColor;

            Image borderImg = magicArrowBar.GetComponent<Image>();
            if (borderImg != null) borderImg.color = currentBorder;

            Transform fillT = magicArrowBar.Find("Fill");
            if (fillT != null)
            {
                RectTransform fillRt = fillT.GetComponent<RectTransform>();
                if (fillRt != null)
                {
                    fillRt.offsetMin = new Vector2(runeBarBorderThickness, runeBarBorderThickness);
                    fillRt.offsetMax = new Vector2(-runeBarBorderThickness, -runeBarBorderThickness);
                }
                Image fillImg = fillT.GetComponent<Image>();
                if (fillImg != null) fillImg.color = currentFill;
            }

            UpdateSpellIconBadgeLayout();
        }

        public void SetBarAppearance(Color borderColor, Color fillColor, float alpha = 1f)
        {
            if (magicArrowBar == null)
            {
                var t = transform.Find("MagicArrowBar");
                if (t == null) t = transform.Find("MagicRuneBar");
                if (t != null) magicArrowBar = t.GetComponent<RectTransform>();
            }
            if (magicArrowBar == null) return;

            var borderImg = magicArrowBar.GetComponent<Image>();
            if (borderImg != null) borderImg.color = borderColor;

            var fillT = magicArrowBar.Find("Fill");
            if (fillT != null)
            {
                var fillImg = fillT.GetComponent<Image>();
                if (fillImg != null) fillImg.color = fillColor;
            }

            var badgeT = magicArrowBar.Find("SpellIconBadge");
            if (badgeT != null)
            {
                var badgeBorder = badgeT.GetComponent<Image>();
                if (badgeBorder != null) badgeBorder.color = borderColor;
            }

            var cg = magicArrowBar.GetComponent<CanvasGroup>();
            if (cg == null) cg = magicArrowBar.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = alpha;
        }

        public void ResetBarAppearance()
        {
            Color border = (ActiveSpell != null) ? ActiveSpell.BorderColor : runeBarBorderColor;
            Color fill = (ActiveSpell != null) ? ActiveSpell.FillColor : runeBarFillColor;
            SetBarAppearance(border, fill, 1f);

            if (ActiveSpell != null)
            {
                SetSpellTheme(ActiveSpell.SpellIcon, ActiveSpell.BorderColor, ActiveSpell.ArrowColor, ActiveSpell.FillColor);
            }
        }

        public void UpdateMagicRuneBarStyle() => UpdateMagicArrowBarStyle();

        private void UpdateMagicRuneBarPosition()
        {
            UpdateMagicArrowBarStyle();
        }

        private void OnValidate()
        {
            uiRadius = Mathf.Max(20f, uiRadius);
            arrowIconSize = Mathf.Max(12f, arrowIconSize);
            runeBarWidth = Mathf.Max(20f, runeBarWidth);
            runeBarHeight = Mathf.Max(10f, runeBarHeight);
            runeBarBorderThickness = Mathf.Max(0.5f, runeBarBorderThickness);
            spellBadgeSize = Mathf.Max(12f, spellBadgeSize);

            if (wheelGraphic != null)
            {
                wheelGraphic.UIRadius = uiRadius;
            }
            UpdateLabelsPosition();
            UpdateArrowIconsPosition();
            UpdateMagicArrowBarStyle();
            UpdateSpellIconBadgeLayout();
        }

        private void CreateDirectionLabels()
        {
            if (wheel == null || wheel.AttackSectors == null) return;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            foreach (var sec in wheel.AttackSectors)
            {
                GameObject lblObj = new GameObject($"Label_{sec.direction}", typeof(RectTransform), typeof(Text), typeof(Outline));
                lblObj.transform.SetParent(transform, false);

                RectTransform rt = lblObj.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(32f, 22f);

                Text txt = lblObj.GetComponent<Text>();
                txt.font = font;
                txt.fontSize = 12;
                txt.fontStyle = FontStyle.Bold;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.raycastTarget = false;

                Color runeColor = GetDirectionRuneColor(sec.direction);
                txt.color = runeColor;
                txt.text = GetDirectionAbbreviation(sec.direction);

                // Контур для высокой читаемости
                Outline outline = lblObj.GetComponent<Outline>();
                outline.effectColor = new Color(0.03f, 0.04f, 0.08f, 0.95f);
                outline.effectDistance = new Vector2(1.2f, -1.2f);

                directionLabels.Add(txt);
            }

            UpdateLabelsPosition();
        }

        private static string GetDirectionAbbreviation(AttackDirection dir)
        {
            switch (dir)
            {
                case AttackDirection.UpRight:   return "UR";
                case AttackDirection.Right:     return "R";
                case AttackDirection.DownRight: return "DR";
                case AttackDirection.DownLeft:  return "DL";
                case AttackDirection.Left:      return "L";
                case AttackDirection.UpLeft:    return "UL";
                default:                        return "";
            }
        }

        private void UpdateLabelsPosition()
        {
            if (wheel == null || wheel.AttackSectors == null) return;

            float labelRadius = uiRadius + 22f;
            for (int i = 0; i < wheel.AttackSectors.Count && i < directionLabels.Count; i++)
            {
                if (directionLabels[i] == null) continue;

                AttackSector sec = wheel.AttackSectors[i];
                float rad = sec.angleCenter * Mathf.Deg2Rad;
                Vector2 pos = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * labelRadius;
                directionLabels[i].rectTransform.anchoredPosition = pos;
            }
        }

        /// <summary>
        /// Возвращает текстовый символ стрелки для каждого направления.
        /// </summary>
        public static string GetDirectionArrow(AttackDirection dir)
        {
            switch (dir)
            {
                case AttackDirection.UpRight:   return "↗";
                case AttackDirection.Right:     return "→";
                case AttackDirection.DownRight: return "↘";
                case AttackDirection.DownLeft:  return "↙";
                case AttackDirection.Left:      return "←";
                case AttackDirection.UpLeft:    return "↖";
                default:                        return "";
            }
        }

        public static string GetDirectionRune(AttackDirection dir) => GetDirectionArrow(dir);

        /// <summary>
        /// Возвращает угол в градусах для отображения стрелки.
        /// </summary>
        public static float GetDirectionAngle(AttackDirection dir)
        {
            switch (dir)
            {
                case AttackDirection.UpRight:   return 45f;
                case AttackDirection.Right:     return 0f;
                case AttackDirection.DownRight: return -45f;
                case AttackDirection.DownLeft:  return 225f;
                case AttackDirection.Left:      return 180f;
                case AttackDirection.UpLeft:    return 135f;
                default:                        return 0f;
            }
        }

        /// <summary>
        /// Возвращает чистый белый цвет для всех стрелок.
        /// </summary>
        public static Color GetDirectionArrowColor(AttackDirection dir)
        {
            return Color.white;
        }

        public static Color GetDirectionRuneColor(AttackDirection dir) => Color.white;

        /// <summary>
        /// Возвращает название стрелки направления.
        /// </summary>
        public static string GetArrowName(AttackDirection dir)
        {
            switch (dir)
            {
                case AttackDirection.UpRight:   return "Стрелка Вверх-Вправо (↗)";
                case AttackDirection.Right:     return "Стрелка Вправо (→)";
                case AttackDirection.DownRight: return "Стрелка Вниз-Вправо (↘)";
                case AttackDirection.DownLeft:  return "Стрелка Вниз-Влево (↙)";
                case AttackDirection.Left:      return "Стрелка Влево (←)";
                case AttackDirection.UpLeft:    return "Стрелка Вверх-Влево (↖)";
                default:                        return "";
            }
        }

        public static string GetRuneName(AttackDirection dir) => GetArrowName(dir);

        /// <summary>
        /// Возвращает процедурный неоновый белый спрайт стрелки для заданного направления.
        /// </summary>
        public static Sprite GetArrowSprite(AttackDirection dir)
        {
            if (cachedArrowSprites.TryGetValue(dir, out Sprite cached) && cached != null)
            {
                return cached;
            }

            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color clear = new Color(0, 0, 0, 0);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    tex.SetPixel(x, y, clear);
                }
            }

            System.Action<Vector2, Vector2, float, float> drawLine = (p0, p1, width, alpha) =>
            {
                int minX = Mathf.Clamp((int)Mathf.Min(p0.x, p1.x) - (int)width - 2, 0, size - 1);
                int maxX = Mathf.Clamp((int)Mathf.Max(p0.x, p1.x) + (int)width + 2, 0, size - 1);
                int minY = Mathf.Clamp((int)Mathf.Min(p0.y, p1.y) - (int)width - 2, 0, size - 1);
                int maxY = Mathf.Clamp((int)Mathf.Max(p0.y, p1.y) + (int)width + 2, 0, size - 1);

                Vector2 d = p1 - p0;
                float lenSq = d.sqrMagnitude;

                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                        float t = (lenSq > 0.0001f) ? Mathf.Clamp01(Vector2.Dot(p - p0, d) / lenSq) : 0f;
                        Vector2 proj = p0 + t * d;
                        float dist = Vector2.Distance(p, proj);
                        if (dist <= width)
                        {
                            float a = Mathf.Clamp01((width - dist) / Mathf.Max(0.5f, width * 0.5f)) * alpha;
                            Color cur = tex.GetPixel(x, y);
                            float newA = Mathf.Max(cur.a, a);
                            tex.SetPixel(x, y, new Color(1f, 1f, 1f, newA)); // Чистый белый цвет
                        }
                    }
                }
            };

            System.Action<Vector2, Vector2> drawGlowLine = (p0, p1) =>
            {
                drawLine(p0, p1, 4.2f, 0.38f); // Мягкое белое свечение
                drawLine(p0, p1, 2.2f, 1.0f);  // Четкая белая сердцевина
            };

            float angleDeg = GetDirectionAngle(dir);
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector2 u = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            Vector2 n = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad));
            Vector2 center = new Vector2(32f, 32f);

            Vector2 pBase = center - u * 15f;
            Vector2 pTip = center + u * 17f;
            Vector2 wing1 = pTip - u * 13f + n * 9.5f;
            Vector2 wing2 = pTip - u * 13f - n * 9.5f;

            drawGlowLine(pBase, pTip);
            drawGlowLine(pTip, wing1);
            drawGlowLine(pTip, wing2);
            drawGlowLine(wing1, wing2);

            tex.Apply();
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            cachedArrowSprites[dir] = sprite;
            return sprite;
        }

        public static Sprite GetRuneSprite(AttackDirection dir) => GetArrowSprite(dir);

        private static Sprite CreateCircleSprite(int res)
        {
            Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            float radius = res * 0.46f;
            Vector2 center = new Vector2(res * 0.5f, res * 0.5f);

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center);
                    float a = Mathf.Clamp01(radius - d + 1f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f));
        }
    }
}
