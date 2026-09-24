using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Combat.Editor
{
    [CustomEditor(typeof(AttackHitboxVisualizer))]
    public class AttackHitboxVisualizerEditor : UnityEditor.Editor
    {
        private AttackHitboxVisualizer _target;
        private GUIStyle _headerStyle;
        private GUIStyle _subHeaderStyle;
        private GUIStyle _badgeStyle;
        private GUIStyle _cardBoxStyle;
        private GUIStyle _sceneLabelStyle;

        private void OnEnable()
        {
            _target = (AttackHitboxVisualizer)target;
            if (_target != null)
            {
                _target.EnsureDatabase();
            }
        }

        private void InitStyles()
        {
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            if (_subHeaderStyle == null)
            {
                _subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 12
                };
            }

            if (_cardBoxStyle == null)
            {
                _cardBoxStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    padding = new RectOffset(10, 10, 8, 8)
                };
            }

            if (_sceneLabelStyle == null)
            {
                _sceneLabelStyle = new GUIStyle()
                {
                    normal = { textColor = Color.white },
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(6, 6, 3, 3)
                };
                var bgTex = new Texture2D(1, 1);
                bgTex.SetPixel(0, 0, new Color(0.08f, 0.08f, 0.12f, 0.88f));
                bgTex.Apply();
                _sceneLabelStyle.normal.background = bgTex;
            }
        }

        public override void OnInspectorGUI()
        {
            InitStyles();
            _target.EnsureDatabase();

            var db = _target.Database;
            if (db == null)
            {
                EditorGUILayout.HelpBox("База данных атак (CombatAttackDatabase) не найдена!", MessageType.Error);
                if (GUILayout.Button("Создать базу данных атак в Assets/Resources"))
                {
                    _target.SetDatabase(AttackHitboxVisualizer.CreateOrFindDatabaseAsset());
                }
                return;
            }

            if (db.Attacks == null || db.Attacks.Count == 0)
            {
                EditorGUILayout.HelpBox("База данных атак пуста.", MessageType.Warning);
                if (GUILayout.Button("Заполнить стандартными приёмами (Reset to Defaults)"))
                {
                    Undo.RecordObject(db, "Populate Default Attacks");
                    db.ResetToDefaults();
                    EditorUtility.SetDirty(db);
                }
                return;
            }

            // 1. Заголовок
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("⚔️ РЕДАКТОР И ВИЗУАЛИЗАТОР ХИТБОКСОВ АТАК", _headerStyle);
            EditorGUILayout.Space(4);

            // 2. Панель управления отображением (Facing, Mode, Handles)
            EditorGUILayout.BeginVertical(_cardBoxStyle);
            EditorGUILayout.LabelField("Режимы Scene View:", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Направление:", GUILayout.Width(90));
            GUI.backgroundColor = _target.PreviewFacingRight ? new Color(0.4f, 1f, 0.4f) : Color.white;
            if (GUILayout.Button("▶ Вправо", EditorStyles.miniButtonLeft))
            {
                _target.PreviewFacingRight = true;
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = !_target.PreviewFacingRight ? new Color(0.4f, 1f, 0.4f) : Color.white;
            if (GUILayout.Button("◀ Влево", EditorStyles.miniButtonRight))
            {
                _target.PreviewFacingRight = false;
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            bool showAll = EditorGUILayout.Toggle("Показать ВСЕ хитбоксы", _target.ShowAllHitboxes);
            bool handles = EditorGUILayout.Toggle("Интерактивные ручки (Handles)", _target.EnableSceneHandles);
            bool labels = EditorGUILayout.Toggle("Текстовые бейджи в Scene View", _target.ShowLabels);
            bool arrow = EditorGUILayout.Toggle("Стрелка отталкивания/выпада", _target.ShowKnockbackArrow);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_target, "Toggle Hitbox View Settings");
                _target.ShowAllHitboxes = showAll;
                _target.EnableSceneHandles = handles;
                _target.ShowLabels = labels;
                _target.ShowKnockbackArrow = arrow;
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // 3. Быстрый выбор приёма по категориям
            EditorGUILayout.BeginVertical(_cardBoxStyle);
            EditorGUILayout.LabelField("Выбор приёма для редактирования:", EditorStyles.boldLabel);

            // Составляем список названий для Dropdown
            string[] attackOptions = new string[db.Attacks.Count];
            for (int i = 0; i < db.Attacks.Count; i++)
            {
                var a = db.Attacks[i];
                attackOptions[i] = $"[{a.category}] {a.displayName} ({a.glyphPattern})";
            }

            EditorGUI.BeginChangeCheck();
            int newIdx = EditorGUILayout.Popup("Приём:", _target.SelectedAttackIndex, attackOptions);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_target, "Select Attack");
                _target.SelectedAttackIndex = newIdx;
                SceneView.RepaintAll();
            }

            // Быстрые кнопки по 5 главным категориям
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("⚡ Вихрь", EditorStyles.miniButton)) SelectFirstByCategory(db, "Супер-финишер");
            if (GUILayout.Button("💥 Шторм", EditorStyles.miniButton)) SelectFirstByCategory(db, "Силовой выпад");
            if (GUILayout.Button("▲ Верхние", EditorStyles.miniButton)) SelectFirstByCategory(db, "Верхние и диагональные");
            if (GUILayout.Button("▶ Выпады", EditorStyles.miniButton)) SelectFirstByCategory(db, "Выпады клинком");
            if (GUILayout.Button("▼ Подсечки", EditorStyles.miniButton)) SelectFirstByCategory(db, "Нижние подсечки и срезы");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            var entry = _target.SelectedAttack;
            if (entry == null || entry.attackConfig == null) return;
            var cfg = entry.attackConfig;

            // 4. Информационная карточка выбранного приёма
            EditorGUILayout.BeginVertical(_cardBoxStyle);
            EditorGUILayout.LabelField($"<b>{entry.displayName}</b>", _subHeaderStyle);
            EditorGUILayout.LabelField($"Связка: <b>{entry.directionNote}</b> (глифы: {entry.glyphPattern})", EditorStyles.miniLabel);
            EditorGUILayout.LabelField(entry.description, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            EditorGUI.BeginChangeCheck();

            // 5. Геометрия хитбокса (Offset & Size)
            EditorGUILayout.BeginVertical(_cardBoxStyle);
            EditorGUILayout.LabelField("📐 ГЕОМЕТРИЯ ХИТБОКСА (Позиция и Размеры)", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            cfg.hitboxOffset.x = EditorGUILayout.Slider("Смещение X (Вперед):", cfg.hitboxOffset.x, -3f, 4f);
            if (GUILayout.Button("0", GUILayout.Width(22))) cfg.hitboxOffset.x = 0f;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            cfg.hitboxOffset.y = EditorGUILayout.Slider("Смещение Y (Высота):", cfg.hitboxOffset.y, -3f, 4f);
            if (GUILayout.Button("0", GUILayout.Width(22))) cfg.hitboxOffset.y = 0f;
            EditorGUILayout.EndHorizontal();

            cfg.hitboxSize.x = Mathf.Max(0.05f, EditorGUILayout.Slider("Ширина (Size X):", cfg.hitboxSize.x, 0.1f, 5f));
            cfg.hitboxSize.y = Mathf.Max(0.05f, EditorGUILayout.Slider("Высота (Size Y):", cfg.hitboxSize.y, 0.1f, 5f));

            // Пресеты размеров
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Короткий (1.2x0.9)", EditorStyles.miniButton)) { cfg.hitboxSize = new Vector2(1.2f, 0.9f); }
            if (GUILayout.Button("Длинный (1.8x0.9)", EditorStyles.miniButton)) { cfg.hitboxSize = new Vector2(1.8f, 0.9f); }
            if (GUILayout.Button("Высокий (1.3x1.5)", EditorStyles.miniButton)) { cfg.hitboxSize = new Vector2(1.3f, 1.5f); }
            if (GUILayout.Button("Широкий (1.6x1.8)", EditorStyles.miniButton)) { cfg.hitboxSize = new Vector2(1.6f, 1.8f); }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // 6. Боевые параметры (Урон, Зоны, Отталкивание, Выпад)
            EditorGUILayout.BeginVertical(_cardBoxStyle);
            EditorGUILayout.LabelField("⚔️ БОЕВЫЕ ПАРАМЕТРЫ", EditorStyles.boldLabel);

            cfg.damage = EditorGUILayout.Slider("Базовый урон:", cfg.damage, 1f, 100f);

            // Боевые зоны (Low, Mid, High)
            cfg.targetedZones = (CombatZone)EditorGUILayout.EnumFlagsField("Зоны поражения:", cfg.targetedZones);

            cfg.knockbackForce = EditorGUILayout.Vector2Field("Сила отталкивания (X, Y):", cfg.knockbackForce);

            entry.lungeForce = EditorGUILayout.Slider("Импульс выпада (Lunge):", entry.lungeForce, 0f, 12f);
            cfg.lungeForce = entry.lungeForce;

            entry.staminaCost = EditorGUILayout.Slider("Расход выносливости:", entry.staminaCost, 1f, 50f);

            entry.isLauncher = EditorGUILayout.Toggle("Подбрасывающий удар (Launcher):", entry.isLauncher);
            cfg.isLauncher = entry.isLauncher;

            cfg.hitboxColor = EditorGUILayout.ColorField("Цвет хитбокса:", cfg.hitboxColor);

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // 7. Тайминги (Startup, Active, Recovery) + графическая полоса
            EditorGUILayout.BeginVertical(_cardBoxStyle);
            EditorGUILayout.LabelField("⏱️ ТАЙМИНГИ УДАРА (в секундах)", EditorStyles.boldLabel);

            cfg.startupTime = EditorGUILayout.Slider("Замах (Startup):", cfg.startupTime, 0.01f, 0.5f);
            cfg.activeTime = EditorGUILayout.Slider("Активная фаза (Active):", cfg.activeTime, 0.01f, 0.5f);
            cfg.recoveryTime = EditorGUILayout.Slider("Восстановление (Recovery):", cfg.recoveryTime, 0.01f, 0.5f);

            float totalTime = cfg.startupTime + cfg.activeTime + cfg.recoveryTime;
            EditorGUILayout.LabelField($"Полная длительность: <b>{totalTime:F2} сек</b> ({(int)(totalTime * 60)} кадров @ 60 FPS)", EditorStyles.miniLabel);

            // Графическая полоса таймингов
            DrawTimingBar(cfg.startupTime, cfg.activeTime, cfg.recoveryTime);

            EditorGUILayout.EndVertical();

            // Если были изменения в Inspector
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(db, $"Edit Attack: {entry.displayName}");
                EditorUtility.SetDirty(db);
                SyncRuntimeAttack(entry);
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(8);

            // 8. Кнопки сброса и сохранения
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Сбросить эту атаку", GUILayout.Height(26)))
            {
                if (EditorUtility.DisplayDialog("Сброс параметров", $"Сбросить параметры приёма '{entry.displayName}' к стандартным значениям?", "Да, сбросить", "Отмена"))
                {
                    Undo.RecordObject(db, "Reset Attack Entry");
                    db.ResetEntry(entry.sequenceId);
                    EditorUtility.SetDirty(db);
                    SyncRuntimeAttack(entry);
                    SceneView.RepaintAll();
                }
            }

            if (GUILayout.Button("Сбросить ВСЕ к дефолтам", GUILayout.Height(26)))
            {
                if (EditorUtility.DisplayDialog("Сброс всех атак", "Сбросить ВСЕ атаки к стандартным значениям из спецификации?", "Да, сбросить все", "Отмена"))
                {
                    Undo.RecordObject(db, "Reset All Attacks");
                    db.ResetToDefaults();
                    EditorUtility.SetDirty(db);
                    CombatSequenceLibrary.Instance.EnsureInitialized();
                    SceneView.RepaintAll();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            if (GUILayout.Button("💾 Сохранить изменения на диск (Save Assets)", GUILayout.Height(28)))
            {
                EditorUtility.SetDirty(db);
                AssetDatabase.SaveAssets();
                Debug.Log($"<color=lime>[CombatAttackDatabase]</color> Настройки хитбоксов успешно сохранены!");
            }
        }

        private void SelectFirstByCategory(CombatAttackDatabase db, string cat)
        {
            int idx = db.Attacks.FindIndex(a => a.category == cat);
            if (idx >= 0)
            {
                Undo.RecordObject(_target, "Select Attack Category");
                _target.SelectedAttackIndex = idx;
                SceneView.RepaintAll();
            }
        }

        private void DrawTimingBar(float startup, float active, float recovery)
        {
            float total = Mathf.Max(0.001f, startup + active + recovery);
            Rect r = EditorGUILayout.GetControlRect(false, 18);

            float sW = r.width * (startup / total);
            float aW = r.width * (active / total);
            float rW = r.width * (recovery / total);

            // Startup rect (Желтый)
            Rect sr = new Rect(r.x, r.y, sW, r.height);
            EditorGUI.DrawRect(sr, new Color(1f, 0.85f, 0.15f, 0.85f));

            // Active rect (Красный)
            Rect ar = new Rect(r.x + sW, r.y, aW, r.height);
            EditorGUI.DrawRect(ar, new Color(1f, 0.25f, 0.2f, 0.95f));

            // Recovery rect (Голубой)
            Rect rr = new Rect(r.x + sW + aW, r.y, rW, r.height);
            EditorGUI.DrawRect(rr, new Color(0.2f, 0.7f, 1f, 0.85f));

            // Outline
            Handles.DrawSolidRectangleWithOutline(r, Color.clear, new Color(0f, 0f, 0f, 0.5f));

            // Mini labels inside bars if wide enough
            var centerStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.black }
            };
            if (sW > 35) GUI.Label(sr, $"{startup:F2}s", centerStyle);
            if (aW > 35) GUI.Label(ar, $"{active:F2}s", centerStyle);
            if (rW > 35) GUI.Label(rr, $"{recovery:F2}s", centerStyle);
        }

        private void OnSceneGUI()
        {
            InitStyles();
            if (_target == null) return;
            _target.EnsureDatabase();

            var db = _target.Database;
            if (db == null || db.Attacks == null || db.Attacks.Count == 0) return;

            float facing = _target.PreviewFacingRight ? 1f : -1f;
            Vector3 playerPos = _target.transform.position;

            // 1. Если включен показ всех атак, рисуем остальные приёмы полупрозрачными контурами
            if (_target.ShowAllHitboxes)
            {
                for (int i = 0; i < db.Attacks.Count; i++)
                {
                    if (i == _target.SelectedAttackIndex) continue;
                    var other = db.Attacks[i];
                    if (other.attackConfig == null) continue;

                    Vector3 otherCenter = playerPos + new Vector3(other.attackConfig.hitboxOffset.x * facing, other.attackConfig.hitboxOffset.y, 0f);
                    Vector3 otherSize = other.attackConfig.hitboxSize;
                    Color otherCol = other.attackConfig.hitboxColor;
                    otherCol.a = 0.35f;

                    DrawHitboxBox(otherCenter, otherSize, otherCol, 0.15f);

                    if (_target.ShowLabels)
                    {
                        Handles.Label(otherCenter + new Vector3(0f, otherSize.y * 0.5f + 0.15f, 0f), other.displayName, _sceneLabelStyle);
                    }
                }
            }

            // 2. Рисуем выбранную активную атаку
            var entry = _target.SelectedAttack;
            if (entry == null || entry.attackConfig == null) return;
            var cfg = entry.attackConfig;

            Vector3 center = playerPos + new Vector3(cfg.hitboxOffset.x * facing, cfg.hitboxOffset.y, 0f);
            Vector3 size = cfg.hitboxSize;
            Color col = cfg.hitboxColor;
            if (col.a < 0.05f) col = Color.red;

            // Тело хитбокса
            DrawHitboxBox(center, size, col, 0.28f);

            // Пунктирная линия от игрока к центру хитбокса
            Handles.color = new Color(1f, 1f, 1f, 0.4f);
            Handles.DrawDottedLine(playerPos, center, 3f);

            // 3. Текстовый бейдж над хитбоксом в Scene View
            if (_target.ShowLabels)
            {
                Vector3 labelPos = center + new Vector3(0f, size.y * 0.5f + 0.28f, 0f);
                string launcherBadge = entry.isLauncher ? " ⚡[LAUNCHER]" : "";
                string labelText = $"{entry.displayName}{launcherBadge}\n" +
                                   $"Урон: {cfg.damage:F0} • Зоны: {cfg.targetedZones} • Выпад: {entry.lungeForce:F1}\n" +
                                   $"Размер: {size.x:F2} × {size.y:F2} • Смещение: ({cfg.hitboxOffset.x:F2}, {cfg.hitboxOffset.y:F2})";

                Handles.Label(labelPos, labelText, _sceneLabelStyle);
            }

            // 4. Стрелка вектора отталкивания и выпада
            if (_target.ShowKnockbackArrow && cfg.knockbackForce.sqrMagnitude > 0.01f)
            {
                Vector3 arrowDir = new Vector3(cfg.knockbackForce.x * facing, cfg.knockbackForce.y, 0f).normalized;
                float arrowLen = Mathf.Clamp(cfg.knockbackForce.magnitude * 0.15f, 0.6f, 2.0f);
                Vector3 arrowEnd = center + arrowDir * arrowLen;

                Handles.color = new Color(1f, 0.85f, 0.15f, 0.95f);
                Handles.DrawLine(center, arrowEnd);
                Handles.ConeHandleCap(0, arrowEnd, Quaternion.LookRotation(arrowDir, Vector3.forward), 0.22f, EventType.Repaint);
            }

            // 5. Интерактивные ручки (Scene Handles)
            if (_target.EnableSceneHandles)
            {
                // а) Центральная ручка перемещения (Offset)
                EditorGUI.BeginChangeCheck();
                Vector3 newCenter = Handles.PositionHandle(center, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(db, "Move Hitbox Offset");
                    float newOffX = (newCenter.x - playerPos.x) * facing;
                    float newOffY = newCenter.y - playerPos.y;
                    cfg.hitboxOffset = new Vector2(newOffX, newOffY);
                    EditorUtility.SetDirty(db);
                    SyncRuntimeAttack(entry);
                }

                // б) 4 ручки на краях для изменения размеров (Resize handles)
                float handleSize = 0.08f;
                Handles.color = Color.white;

                // Передняя грань (в направлении взгляда)
                Vector3 frontPos = center + new Vector3(size.x * 0.5f * facing, 0f, 0f);
                EditorGUI.BeginChangeCheck();
                Vector3 newFront = Handles.Slider(frontPos, facing > 0 ? Vector3.right : Vector3.left, HandleUtility.GetHandleSize(frontPos) * handleSize, Handles.DotHandleCap, 0.05f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(db, "Resize Hitbox Width Front");
                    float deltaX = (newFront.x - frontPos.x) * facing;
                    cfg.hitboxSize.x = Mathf.Max(0.1f, cfg.hitboxSize.x + deltaX);
                    cfg.hitboxOffset.x += deltaX * 0.5f;
                    EditorUtility.SetDirty(db);
                    SyncRuntimeAttack(entry);
                }

                // Задняя грань (в сторону персонажа)
                Vector3 rearPos = center - new Vector3(size.x * 0.5f * facing, 0f, 0f);
                EditorGUI.BeginChangeCheck();
                Vector3 newRear = Handles.Slider(rearPos, facing > 0 ? Vector3.left : Vector3.right, HandleUtility.GetHandleSize(rearPos) * handleSize, Handles.DotHandleCap, 0.05f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(db, "Resize Hitbox Width Rear");
                    float deltaX = (rearPos.x - newRear.x) * facing;
                    cfg.hitboxSize.x = Mathf.Max(0.1f, cfg.hitboxSize.x + deltaX);
                    cfg.hitboxOffset.x -= deltaX * 0.5f;
                    EditorUtility.SetDirty(db);
                    SyncRuntimeAttack(entry);
                }

                // Верхняя грань
                Vector3 topPos = center + new Vector3(0f, size.y * 0.5f, 0f);
                EditorGUI.BeginChangeCheck();
                Vector3 newTop = Handles.Slider(topPos, Vector3.up, HandleUtility.GetHandleSize(topPos) * handleSize, Handles.DotHandleCap, 0.05f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(db, "Resize Hitbox Height Top");
                    float deltaY = newTop.y - topPos.y;
                    cfg.hitboxSize.y = Mathf.Max(0.1f, cfg.hitboxSize.y + deltaY);
                    cfg.hitboxOffset.y += deltaY * 0.5f;
                    EditorUtility.SetDirty(db);
                    SyncRuntimeAttack(entry);
                }

                // Нижняя грань
                Vector3 bottomPos = center - new Vector3(0f, size.y * 0.5f, 0f);
                EditorGUI.BeginChangeCheck();
                Vector3 newBottom = Handles.Slider(bottomPos, Vector3.down, HandleUtility.GetHandleSize(bottomPos) * handleSize, Handles.DotHandleCap, 0.05f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(db, "Resize Hitbox Height Bottom");
                    float deltaY = bottomPos.y - newBottom.y;
                    cfg.hitboxSize.y = Mathf.Max(0.1f, cfg.hitboxSize.y + deltaY);
                    cfg.hitboxOffset.y -= deltaY * 0.5f;
                    EditorUtility.SetDirty(db);
                    SyncRuntimeAttack(entry);
                }
            }
        }

        private static void DrawHitboxBox(Vector3 center, Vector3 size, Color baseCol, float fillAlpha = 0.25f)
        {
            Vector3[] verts = new Vector3[4]
            {
                center + new Vector3(-size.x * 0.5f, -size.y * 0.5f, 0f),
                center + new Vector3(-size.x * 0.5f,  size.y * 0.5f, 0f),
                center + new Vector3( size.x * 0.5f,  size.y * 0.5f, 0f),
                center + new Vector3( size.x * 0.5f, -size.y * 0.5f, 0f)
            };

            Color fill = new Color(baseCol.r, baseCol.g, baseCol.b, fillAlpha);
            Color outline = new Color(baseCol.r, baseCol.g, baseCol.b, 0.95f);

            Handles.DrawSolidRectangleWithOutline(verts, fill, outline);
        }

        private void SyncRuntimeAttack(CombatAttackDatabase.AttackEntry entry)
        {
            if (entry == null) return;
            var lib = CombatSequenceLibrary.Instance;
            if (lib != null)
            {
                lib.UpdateSequenceAttack(entry.sequenceId, entry.attackConfig, entry.staminaCost, entry.isLauncher, entry.lungeForce);
            }
        }

        [MenuItem("Tools/Combat/⚔️ Открыть редактор хитбоксов игрока", false, 10)]
        public static void OpenHitboxEditorMenu()
        {
            var visualizer = GameObject.FindAnyObjectByType<AttackHitboxVisualizer>();
            if (visualizer == null)
            {
                var player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
                if (player != null)
                {
                    visualizer = player.AddComponent<AttackHitboxVisualizer>();
                }
            }

            if (visualizer != null)
            {
                Selection.activeGameObject = visualizer.gameObject;
                if (SceneView.lastActiveSceneView != null)
                {
                    SceneView.lastActiveSceneView.FrameSelected();
                }
                Debug.Log("<color=lime>[Hitbox Visualizer]</color> Редактор хитбоксов выбран и сфокусирован!");
            }
            else
            {
                Debug.LogWarning("[Hitbox Visualizer] Персонаж Player не найден в активной сцене!");
            }
        }
    }
}
