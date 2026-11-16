using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering.Universal;
using UnityEngine.EventSystems;
using Combat.Cameras;
using Combat.Sandbox;
using Combat.ProceduralGeneration;
using Combat.Navigation;

namespace TestFight.Editor
{
    public static class SandboxSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/SandboxScene.unity";

        [MenuItem("Tools/Abyss/Build Sandbox Scene (Полигон)", priority = 10)]
        public static void BuildSandboxScene()
        {
            // 1. Создаем новую пустую сцену
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Загружаем спрайт для геометрии
            var squareSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/v2/Square.png");
            if (squareSprite == null)
            {
                squareSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            }

            // --- КОРНЕВЫЕ ГРУППЫ ДЛЯ ЧИСТОЙ ИЕРАРХИИ ---
            var rootEnv = new GameObject("--- ENVIRONMENT & ARENA ---");
            var rootLights = new GameObject("--- LIGHTS & CAMERA ---");
            var rootManagers = new GameObject("--- MANAGERS & SYSTEMS ---");
            var rootEntities = new GameObject("--- CHARACTERS & TARGETS ---");

            // 2. EventSystem
            var eventSystemObj = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            eventSystemObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            eventSystemObj.AddComponent<StandaloneInputModule>();
#endif
            eventSystemObj.transform.SetParent(rootManagers.transform);

            // 3. Global Light 2D
            var lightObj = new GameObject("Global Light 2D", typeof(Light2D));
            lightObj.transform.SetParent(rootLights.transform);
            var light2D = lightObj.GetComponent<Light2D>();
            light2D.lightType = Light2D.LightType.Global;
            light2D.color = new Color(0.82f, 0.90f, 1.0f, 1f);
            light2D.intensity = 0.85f;

            // Точечные неоновые источники света над зонами
            CreatePointLight("Light_DummyZone", new Vector3(-8f, 3.2f, 0f), new Color(0.4f, 0.9f, 1.0f), 2.2f, 7f, rootLights.transform);
            CreatePointLight("Light_CenterArena", new Vector3(0f, 6.0f, 0f), new Color(1.0f, 0.75f, 0.3f), 2.8f, 9f, rootLights.transform);
            CreatePointLight("Light_PropsZone", new Vector3(13f, 3.2f, 0f), new Color(1.0f, 0.35f, 0.2f), 2.2f, 7f, rootLights.transform);

            // 4. Main Camera
            var camObj = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(UniversalAdditionalCameraData), typeof(CameraFollow2D));
            camObj.transform.SetParent(rootLights.transform);
            camObj.tag = "MainCamera";
            var cam = camObj.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6.8f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.08f, 0.12f, 1.0f);
            camObj.transform.position = new Vector3(-12f, 2.5f, -10f);

            var camFollow = camObj.GetComponent<CameraFollow2D>();
            camFollow.DeadzoneSize = new Vector2(0.6f, 0.4f);
            camFollow.Offset = new Vector2(0f, 1.0f);
            camFollow.SmoothTimeX = 0.12f;
            camFollow.SmoothTimeY = 0.15f;

            // 5. Загрузка префабов
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Player.prefab");
            var dummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/CombatDummy.prefab");
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Enemy_Fighter.prefab");
            var canvasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/CombatCanvas.prefab");
            var barrelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Props/DestructibleBarrel.prefab");
            var potionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Props/HealingPotion.prefab");
            var soundMgrPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Audio/SoundManager.prefab");
            var settingsMgrPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Managers/CombatSettingsManager.prefab");
            var saveMgrPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Managers/CombatSaveManager.prefab");
            var circleChargePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Focus/CircleCharge.prefab");
            var weakPointPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Focus/WeakPoint.prefab");
            var crossSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Focus/CrossSprite.png");

            // 6. Геометрия арены (Пол, Стены, Потолок, Платформы)
            Color groundColor = new Color(0.12f, 0.15f, 0.20f, 1.0f);
            Color wallColor = new Color(0.09f, 0.11f, 0.16f, 1.0f);
            Color platformColor = new Color(0.20f, 0.25f, 0.35f, 1.0f);

            // Задняя стена арены
            var bgWall = new GameObject("Background_Arena_Wall", typeof(SpriteRenderer));
            bgWall.transform.position = new Vector3(0f, 6.5f, 0.5f);
            bgWall.transform.localScale = new Vector3(38f, 14f, 1f);
            bgWall.transform.SetParent(rootEnv.transform);
            var bgSr = bgWall.GetComponent<SpriteRenderer>();
            bgSr.sprite = squareSprite;
            bgSr.color = new Color(0.05f, 0.06f, 0.09f, 1.0f);
            bgSr.sortingOrder = -20;

            // Основной пол (36 метров)
            CreateBox("Floor_Main", new Vector3(0f, -0.5f, 0f), new Vector3(38f, 1.0f, 1f), groundColor, squareSprite, rootEnv.transform, false);

            // Ограничивающие стены (высота 14 метров)
            CreateBox("Wall_Left", new Vector3(-19f, 6.0f, 0f), new Vector3(1.5f, 14f, 1f), wallColor, squareSprite, rootEnv.transform, false);
            CreateBox("Wall_Right", new Vector3(19f, 6.0f, 0f), new Vector3(1.5f, 14f, 1f), wallColor, squareSprite, rootEnv.transform, false);
            CreateBox("Ceiling", new Vector3(0f, 13.5f, 0f), new Vector3(38f, 1.0f, 1f), wallColor, squareSprite, rootEnv.transform, false);

            // Платформы с One-Way Effector (чтобы можно было прыгать снизу и спускаться)
            var platsRoot = new GameObject("Arena_Platforms");
            platsRoot.transform.SetParent(rootEnv.transform);

            CreatePlatform("Platform_Left_Low", new Vector3(-8f, 2.3f, 0f), new Vector3(6.5f, 0.35f, 1f), platformColor, squareSprite, platsRoot.transform);
            CreatePlatform("Platform_Center_High", new Vector3(0f, 5.0f, 0f), new Vector3(7.5f, 0.35f, 1f), platformColor, squareSprite, platsRoot.transform);
            CreatePlatform("Platform_Right_Mid", new Vector3(8.5f, 3.0f, 0f), new Vector3(6.5f, 0.35f, 1f), platformColor, squareSprite, platsRoot.transform);

            // Зонные текстовые таблички в пространстве мира
            CreateWorldLabel("Label_Dummy", "МАНЕКЕН & DPS", new Vector3(-8f, 4.0f, 0f), new Color(0.3f, 0.85f, 1.0f), rootEnv.transform);
            CreateWorldLabel("Label_Arena", "БОЕВОЙ РИНГ", new Vector3(0f, 6.8f, 0f), new Color(1.0f, 0.8f, 0.3f), rootEnv.transform);
            CreateWorldLabel("Label_Props", "МАГИЯ & ФИЗИКА", new Vector3(13.5f, 4.0f, 0f), new Color(1.0f, 0.4f, 0.25f), rootEnv.transform);

            // 7. Размещение сущностей
            // Игрок
            GameObject playerObj = null;
            if (playerPrefab != null)
            {
                playerObj = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
                playerObj.name = "Player";
                playerObj.transform.position = new Vector3(-12f, 0.5f, 0f);
                playerObj.transform.SetParent(rootEntities.transform);
                camFollow.Target = playerObj.transform;
            }

            // Манекен в левой зоне
            if (dummyPrefab != null)
            {
                var dummyObj = (GameObject)PrefabUtility.InstantiatePrefab(dummyPrefab, scene);
                dummyObj.name = "CombatDummy";
                dummyObj.transform.position = new Vector3(-8f, 0.5f, 0f);
                dummyObj.transform.SetParent(rootEntities.transform);
            }

            // Начальный боец в центре ринга
            if (enemyPrefab != null)
            {
                var enemyObj = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab, scene);
                enemyObj.name = "Enemy_Fighter_Initial";
                enemyObj.transform.position = new Vector3(5f, 0.5f, 0f);
                enemyObj.transform.SetParent(rootEntities.transform);
            }

            // Разрушаемые бочки и зелье в правой зоне
            if (barrelPrefab != null)
            {
                var b1 = (GameObject)PrefabUtility.InstantiatePrefab(barrelPrefab, scene);
                b1.name = "DestructibleBarrel_1";
                b1.transform.position = new Vector3(12.5f, 0.55f, 0f);
                b1.transform.SetParent(rootEntities.transform);

                var b2 = (GameObject)PrefabUtility.InstantiatePrefab(barrelPrefab, scene);
                b2.name = "DestructibleBarrel_2";
                b2.transform.position = new Vector3(14.0f, 0.55f, 0f);
                b2.transform.SetParent(rootEntities.transform);

                var b3 = (GameObject)PrefabUtility.InstantiatePrefab(barrelPrefab, scene);
                b3.name = "DestructibleBarrel_3";
                b3.transform.position = new Vector3(13.25f, 1.65f, 0f);
                b3.transform.SetParent(rootEntities.transform);
            }

            if (potionPrefab != null)
            {
                var pot = (GameObject)PrefabUtility.InstantiatePrefab(potionPrefab, scene);
                pot.name = "HealingPotion";
                pot.transform.position = new Vector3(15.5f, 0.5f, 0f);
                pot.transform.SetParent(rootEntities.transform);
            }

            // 8. Размещение Canvas
            GameObject canvasObj = null;
            if (canvasPrefab != null)
            {
                canvasObj = (GameObject)PrefabUtility.InstantiatePrefab(canvasPrefab, scene);
                canvasObj.name = "CombatCanvas";
                canvasObj.transform.SetParent(rootManagers.transform);
                var hbChild = canvasObj.transform.Find("PlayerHealthBar");
                if (hbChild == null)
                {
                    var hbGo = new GameObject("PlayerHealthBar", typeof(RectTransform), typeof(Combat.UI.PlayerHealthBarUI));
                    hbGo.transform.SetParent(canvasObj.transform, false);
                }
            }

            // 9. Менеджеры
            if (settingsMgrPrefab != null)
            {
                var sm = (GameObject)PrefabUtility.InstantiatePrefab(settingsMgrPrefab, scene);
                sm.name = "[CombatSettingsManager]";
                sm.transform.SetParent(rootManagers.transform);
            }

            if (saveMgrPrefab != null)
            {
                var svm = (GameObject)PrefabUtility.InstantiatePrefab(saveMgrPrefab, scene);
                svm.name = "[CombatSaveManager]";
                svm.transform.SetParent(rootManagers.transform);
            }

            if (soundMgrPrefab != null)
            {
                var snd = (GameObject)PrefabUtility.InstantiatePrefab(soundMgrPrefab, scene);
                snd.name = "[SoundManager]";
                snd.transform.SetParent(rootManagers.transform);
            }

            // FocusManager
            var focusObj = new GameObject("FocusManager", typeof(FocusManager), typeof(WeakPointSpawner));
            focusObj.transform.SetParent(rootManagers.transform);
            var focusMgr = focusObj.GetComponent<FocusManager>();
            var wps = focusObj.GetComponent<WeakPointSpawner>();

            var fmSer = new SerializedObject(focusMgr);
            fmSer.FindProperty("circleChargePrefab").objectReferenceValue = circleChargePrefab;
            fmSer.FindProperty("weakPointSpawner").objectReferenceValue = wps;
            if (playerObj != null)
            {
                fmSer.FindProperty("playerFocusController").objectReferenceValue = playerObj.GetComponent<Combat.Player.PlayerFocusController>();
                var fw = playerObj.GetComponentInChildren<Fencing.FencingWheel>();
                if (fw != null) fmSer.FindProperty("fencingWheel").objectReferenceValue = fw;
            }
            if (canvasObj != null)
            {
                var vignette = canvasObj.transform.Find("FocusVignette");
                if (vignette != null) fmSer.FindProperty("vignetteCanvasGroup").objectReferenceValue = vignette.GetComponent<CanvasGroup>();
            }
            fmSer.ApplyModifiedPropertiesWithoutUndo();

            var wpsSer = new SerializedObject(wps);
            wpsSer.FindProperty("weakPointPrefab").objectReferenceValue = weakPointPrefab;
            wpsSer.FindProperty("crossSprite").objectReferenceValue = crossSprite;
            wpsSer.ApplyModifiedPropertiesWithoutUndo();

            // Системы навигации и директоров
            var navGraphObj = new GameObject("[SYSTEM] PlatformNavGraph2D", typeof(PlatformNavGraph2D));
            navGraphObj.transform.SetParent(rootManagers.transform);

            var navMeshObj = new GameObject("[SYSTEM] NavMesh2D_Manager", typeof(NavMesh2DManager), typeof(NavMeshPlus.Components.NavMeshSurface), typeof(NavMeshPlus.Extensions.CollectSources2d));
            navMeshObj.transform.SetParent(rootManagers.transform);

            var directorObj = new GameObject("[SYSTEM] ArenaDirector2D", typeof(ArenaDirector2D));
            directorObj.transform.SetParent(rootManagers.transform);

            // 10. Точки спавна для песочницы
            var spawnsRoot = new GameObject("--- SPANW POINTS ---");
            spawnsRoot.transform.SetParent(rootEnv.transform);
            var dummySpawnPt = new GameObject("Spawn_Dummy").transform;
            dummySpawnPt.position = new Vector3(-8f, 0.5f, 0f);
            dummySpawnPt.SetParent(spawnsRoot.transform);

            var enemySpawnPt = new GameObject("Spawn_Enemy").transform;
            enemySpawnPt.position = new Vector3(4.5f, 0.5f, 0f);
            enemySpawnPt.SetParent(spawnsRoot.transform);

            var barrelSpawnPt = new GameObject("Spawn_Barrels").transform;
            barrelSpawnPt.position = new Vector3(13f, 0.55f, 0f);
            barrelSpawnPt.SetParent(spawnsRoot.transform);

            // 11. [SandboxDirector] — супервайзер полигона
            var sandboxDirectorObj = new GameObject("[SandboxDirector]", typeof(SandboxDirector));
            sandboxDirectorObj.transform.SetParent(rootManagers.transform);
            var sbDirector = sandboxDirectorObj.GetComponent<SandboxDirector>();

            var sbSer = new SerializedObject(sbDirector);
            sbSer.FindProperty("enemyFighterPrefab").objectReferenceValue = enemyPrefab;
            sbSer.FindProperty("combatDummyPrefab").objectReferenceValue = dummyPrefab;
            sbSer.FindProperty("barrelPrefab").objectReferenceValue = barrelPrefab;
            sbSer.FindProperty("potionPrefab").objectReferenceValue = potionPrefab;
            sbSer.FindProperty("enemySpawnPoint").objectReferenceValue = enemySpawnPt;
            sbSer.FindProperty("dummySpawnPoint").objectReferenceValue = dummySpawnPt;
            sbSer.FindProperty("barrelSpawnPoint").objectReferenceValue = barrelSpawnPt;
            sbSer.ApplyModifiedPropertiesWithoutUndo();

            // 12. Сохраняем сцену
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            // Добавляем в Build Settings
            AddSceneToBuildSettings(ScenePath);

            Debug.Log($"<color=#00FFAA><b>[Полигон]</b></color> Сцена <b>{ScenePath}</b> успешно создана и сохранена!");
        }

        private static void CreateBox(string name, Vector3 pos, Vector3 size, Color col, Sprite sprite, Transform parent, bool isTrigger)
        {
            var go = new GameObject(name, typeof(SpriteRenderer), typeof(BoxCollider2D));
            go.transform.position = pos;
            go.transform.localScale = size;
            go.transform.SetParent(parent);

            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = col;
            sr.drawMode = SpriteDrawMode.Simple;

            var box = go.GetComponent<BoxCollider2D>();
            box.size = Vector2.one;
            box.offset = Vector2.zero;
            box.isTrigger = isTrigger;
        }

        private static void CreatePlatform(string name, Vector3 pos, Vector3 size, Color col, Sprite sprite, Transform parent)
        {
            var go = new GameObject(name, typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(PlatformEffector2D));
            go.transform.position = pos;
            go.transform.localScale = size;
            go.transform.SetParent(parent);

            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = col;

            var box = go.GetComponent<BoxCollider2D>();
            box.size = Vector2.one;
            box.offset = Vector2.zero;
            box.usedByEffector = true;

            var effector = go.GetComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 170f;
        }

        private static void CreatePointLight(string name, Vector3 pos, Color color, float intensity, float radius, Transform parent)
        {
            var lightGo = new GameObject(name, typeof(Light2D));
            lightGo.transform.position = pos;
            lightGo.transform.SetParent(parent);

            var l2d = lightGo.GetComponent<Light2D>();
            l2d.lightType = Light2D.LightType.Point;
            l2d.color = color;
            l2d.intensity = intensity;
            l2d.pointLightOuterRadius = radius;
            l2d.pointLightInnerRadius = radius * 0.25f;
        }

        private static void CreateWorldLabel(string name, string text, Vector3 pos, Color color, Transform parent)
        {
            var go = new GameObject(name, typeof(TextMesh));
            go.transform.position = pos;
            go.transform.SetParent(parent);

            var tm = go.GetComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 28;
            tm.characterSize = 0.08f;
            tm.alignment = TextAlignment.Center;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = color;
            tm.fontStyle = FontStyle.Bold;
        }

        private static void AddSceneToBuildSettings(string path)
        {
            var currentScenes = EditorBuildSettings.scenes;
            foreach (var s in currentScenes)
            {
                if (s.path == path) return;
            }

            var newScenes = new EditorBuildSettingsScene[currentScenes.Length + 1];
            for (int i = 0; i < currentScenes.Length; i++)
            {
                newScenes[i] = currentScenes[i];
            }
            newScenes[newScenes.Length - 1] = new EditorBuildSettingsScene(path, true);
            EditorBuildSettings.scenes = newScenes;
        }
    }
}
