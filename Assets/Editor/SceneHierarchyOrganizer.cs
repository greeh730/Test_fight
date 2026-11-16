#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyss.Editor
{
    /// <summary>
    /// Автоматизированный инструмент для очистки и организации иерархии сцен Abyss of Emotions.
    /// Удаляет тестовый мусор, устраняет дубликаты UI, очищает накопленные дочерние объекты звука
    /// и группирует объекты по единому стандарту секций:
    /// --- CAMERAS ---, --- CHARACTERS ---, --- ENVIRONMENT & LIGHTING ---, --- MANAGERS & SYSTEMS ---, --- UI ---
    /// </summary>
    public static class SceneHierarchyOrganizer
    {
        [MenuItem("Tools/Abyss/Organize Current Scene Hierarchy %&o")]
        public static void OrganizeActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("[HierarchyOrganizer] Нет активной загруженной сцены.");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Organize Scene Hierarchy");
            int undoGroup = Undo.GetCurrentGroup();

            int removedJunk = RemoveJunkObjects(scene);
            int removedDuplicates = RemoveCanvasDuplicates(scene);
            int cleanedSoundChildren = CleanSoundManagerChildren(scene);
            RenameInconsistentManagers(scene);
            OrganizeSceneHeaders(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log($"<color=#00FFAA><b>[HierarchyOrganizer]</b></color> Сцена '{scene.name}' успешно оптимизирована!\n" +
                      $"• Удалено мусорных объектов: {removedJunk}\n" +
                      $"• Удалено дубликатов UI: {removedDuplicates}\n" +
                      $"• Очищено накопленных аудио-каналов: {cleanedSoundChildren}\n" +
                      $"• Иерархия сгруппирована по стандартным секциям.");
        }

        [MenuItem("Tools/Abyss/Organize All Build Scenes")]
        public static void OrganizeAllBuildScenes()
        {
            var activePath = SceneManager.GetActiveScene().path;

            foreach (var sceneEntry in EditorBuildSettings.scenes)
            {
                if (!sceneEntry.enabled) continue;

                var scene = EditorSceneManager.OpenScene(sceneEntry.path, OpenSceneMode.Single);
                if (scene.IsValid())
                {
                    OrganizeActiveScene();
                    EditorSceneManager.SaveScene(scene);
                }
            }

            if (!string.IsNullOrEmpty(activePath))
            {
                EditorSceneManager.OpenScene(activePath, OpenSceneMode.Single);
            }

            Debug.Log("<color=#00FFAA><b>[HierarchyOrganizer]</b></color> Все сцены из Build Settings успешно организованы и сохранены!");
        }

        private static int RemoveJunkObjects(Scene scene)
        {
            int count = 0;
            var junkNames = new HashSet<string> { "0001", "0001 (2)", "TestEnemy", "TestSkeletonFeedback" };

            foreach (var root in scene.GetRootGameObjects())
            {
                if (junkNames.Contains(root.name))
                {
                    Undo.DestroyObjectImmediate(root);
                    count++;
                }
            }
            return count;
        }

        private static int RemoveCanvasDuplicates(Scene scene)
        {
            int removed = 0;
            var canvas = FindGameObjectInScene(scene, "Canvas");
            if (canvas == null) return 0;

            var allComps = Object.FindObjectsByType<Component>(FindObjectsInactive.Include);
            var toRemove = new List<GameObject>();

            for (int i = 0; i < canvas.transform.childCount; i++)
            {
                var child = canvas.transform.GetChild(i).gameObject;
                if (child.name == "DialogueUI" || child.name == "GearButton" || child.name == "SettingsPanel")
                {
                    int refCount = 0;
                    foreach (var c in allComps)
                    {
                        if (c == null || c.gameObject.scene != scene || c.transform.IsChildOf(child.transform)) continue;

                        var so = new SerializedObject(c);
                        var it = so.GetIterator();
                        while (it.NextVisible(true))
                        {
                            if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue != null)
                            {
                                if (it.objectReferenceValue == child || (it.objectReferenceValue is Component cc && cc.gameObject == child))
                                {
                                    refCount++;
                                }
                            }
                        }
                    }

                    if (refCount == 0)
                    {
                        toRemove.Add(child);
                    }
                }
            }

            foreach (var go in toRemove)
            {
                Undo.DestroyObjectImmediate(go);
                removed++;
            }

            return removed;
        }

        private static int CleanSoundManagerChildren(Scene scene)
        {
            var sm = FindGameObjectInScene(scene, "[SoundManager]");
            if (sm == null) return 0;

            int count = sm.transform.childCount;
            while (sm.transform.childCount > 0)
            {
                Undo.DestroyObjectImmediate(sm.transform.GetChild(0).gameObject);
            }
            return count;
        }

        private static void RenameInconsistentManagers(Scene scene)
        {
            var focus = FindGameObjectInScene(scene, "FocusManager");
            if (focus != null)
            {
                Undo.RecordObject(focus, "Rename FocusManager");
                focus.name = "[FocusManager]";
            }
        }

        private static void OrganizeSceneHeaders(Scene scene)
        {
            var headerCameras = GetOrCreateHeader(scene, "--- CAMERAS ---");
            var headerChars = GetOrCreateHeader(scene, "--- CHARACTERS ---");
            var headerEnv = GetOrCreateHeader(scene, "--- ENVIRONMENT & LIGHTING ---");
            var headerManagers = GetOrCreateHeader(scene, "--- MANAGERS & SYSTEMS ---");
            var headerUI = GetOrCreateHeader(scene, "--- UI ---");

            // Для SampleScene: группируем старую статичную арену под Environment
            var staticPieces = new string[] { "Land", "Wall_Left", "Wall_Right", "Arena_Platforms", "Enemy_Fighter" };
            GameObject staticArena = null;
            foreach (var p in staticPieces)
            {
                var piece = FindGameObjectInScene(scene, p);
                if (piece != null && piece.transform.parent != headerChars.transform)
                {
                    if (staticArena == null)
                    {
                        staticArena = FindGameObjectInScene(scene, "[Legacy_Static_Arena]");
                        if (staticArena == null)
                        {
                            staticArena = new GameObject("[Legacy_Static_Arena]");
                            Undo.RegisterCreatedObjectUndo(staticArena, "Create Legacy Arena Container");
                            staticArena.transform.position = Vector3.zero;
                            staticArena.transform.SetParent(headerEnv.transform, false);
                        }
                    }
                    Undo.SetTransformParent(piece.transform, staticArena.transform, "Group Legacy Static Arena");
                }
            }

            // Камеры
            MoveRootToHeader(scene, "Main Camera", headerCameras);

            // Персонажи и сущности
            MoveRootToHeader(scene, "Player", headerChars);
            MoveRootToHeader(scene, "Robert_Guardian", headerChars);
            MoveRootToHeader(scene, "CombatDummy", headerChars);
            MoveRootToHeader(scene, "Vova_Skeleton", headerChars);

            // Окружение, свет, триггеры
            MoveRootToHeader(scene, "Global Light 2D", headerEnv);
            MoveRootToHeader(scene, "DeathTrigger", headerEnv);
            MoveRootToHeader(scene, "ArenaTriggerZone", headerEnv);
            MoveRootToHeader(scene, "DummyRoomEntrancePoint", headerEnv);
            MoveRootToHeader(scene, "Door_To_DummyArena", headerEnv);
            MoveRootToHeader(scene, "Door_To_VovaArena", headerEnv);
            MoveRootToHeader(scene, "Door_To_ExitCrystal", headerEnv);

            // Менеджеры
            string[] managers = {
                "[CombatSettingsManager]", "CombatSettingsManager",
                "[CombatSaveManager]", "CombatSaveManager",
                "[DialogueManager]", "DialogueManager",
                "[FocusManager]",
                "[SoundManager]",
                "[Roguelike_System]", "Roguelike_System",
                "[LevelSequenceGenerator]", "LevelSequenceGenerator",
                "[SYSTEM] PlatformNavGraph2D",
                "[SYSTEM] NavMesh2D_Manager",
                "[SYSTEM] ArenaDirector2D",
                "[OutOfBoundsDetector]",
                "[TutorialManager]", "TutorialManager"
            };

            foreach (var m in managers)
            {
                MoveRootToHeader(scene, m, headerManagers);
            }

            // UI
            MoveRootToHeader(scene, "Canvas", headerUI);
            MoveRootToHeader(scene, "CombatCanvas", headerUI);
            MoveRootToHeader(scene, "EventSystem", headerUI);

            // Упорядочиваем корневые секции
            headerCameras.transform.SetSiblingIndex(0);
            headerChars.transform.SetSiblingIndex(1);
            headerEnv.transform.SetSiblingIndex(2);
            headerManagers.transform.SetSiblingIndex(3);
            headerUI.transform.SetSiblingIndex(4);
        }

        private static GameObject GetOrCreateHeader(Scene scene, string name)
        {
            var existing = FindGameObjectInScene(scene, name);
            if (existing != null) return existing;

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create Header " + name);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        private static void MoveRootToHeader(Scene scene, string goName, GameObject header)
        {
            var go = FindGameObjectInScene(scene, goName);
            if (go != null && go.transform.parent == null)
            {
                Undo.SetTransformParent(go.transform, header.transform, "Parent to " + header.name);
            }
        }

        private static GameObject FindGameObjectInScene(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
                var found = FindInChildren(root.transform, name);
                if (found != null) return found.gameObject;
            }
            return null;
        }

        private static Transform FindInChildren(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c.name == name) return c;
                var found = FindInChildren(c, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
#endif
