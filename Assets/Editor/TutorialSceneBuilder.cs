using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.EventSystems;
using Combat.Cameras;
using Combat.Dialogue;
using Fencing;
using Combat.Player;
using Combat.Tutorial;
using Combat.UI;
using LevelGeneration;

namespace Combat.Editor
{
    public static class TutorialSceneBuilder
    {
        [MenuItem("Combat/Build Tutorial Scene (Full Auto)")]
        public static void BuildTutorialScene()
        {
            string scenePath = "Assets/Scenes/TutorialScene.unity";

            // 1. Создаем новую пустую сцену
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 2. EventSystem
            var eventSystemObj = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            eventSystemObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            eventSystemObj.AddComponent<StandaloneInputModule>();
#endif

            // 3. Global Light 2D
            var lightObj = new GameObject("Global Light 2D", typeof(Light2D));
            var light2D = lightObj.GetComponent<Light2D>();
            light2D.lightType = Light2D.LightType.Global;
            light2D.color = new Color(0.92f, 0.95f, 1.0f, 1f);
            light2D.intensity = 0.95f;

            // 4. Загрузка префабов уровней
            var startChunkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/LevelChunks/Chunk_Start_Intro.prefab");
            var arenaChunkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/LevelChunks/1rd level/First_Level.prefab");
            var endChunkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/LevelChunks/Chunk_Exit_Corridor.prefab");
            if (endChunkPrefab == null)
            {
                endChunkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/LevelChunks/Chunk_End_Outro.prefab");
            }

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Player.prefab");
            var dummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/CombatDummy.prefab");
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Enemy_Fighter.prefab");
            var doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/DungeonPortcullisDoor.prefab");
            var canvasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/CombatCanvas.prefab");
            var dialogueUIPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/DialogueUI.prefab");
            var objectiveHUDPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/TutorialObjectiveHUD.prefab");

            // 5. Размещение чанков геометрии
            var chunksRoot = new GameObject("--- LEVEL CHUNKS ---");

            // Чанк 0: Коридор (Старт)
            GameObject chunk0 = (GameObject)PrefabUtility.InstantiatePrefab(startChunkPrefab, scene);
            chunk0.name = "Chunk_0_StartCorridor";
            chunk0.transform.SetParent(chunksRoot.transform);
            chunk0.transform.position = new Vector3(0f, 0f, 0f);

            // Чанк 1: Арена 1 (Манекен)
            GameObject chunk1 = (GameObject)PrefabUtility.InstantiatePrefab(arenaChunkPrefab, scene);
            chunk1.name = "Chunk_1_DummyArena";
            chunk1.transform.SetParent(chunksRoot.transform);
            chunk1.transform.position = new Vector3(20f, 0f, 0f);

            // Чанк 2: Арена 2 (Вова)
            GameObject chunk2 = (GameObject)PrefabUtility.InstantiatePrefab(arenaChunkPrefab, scene);
            chunk2.name = "Chunk_2_VovaArena";
            chunk2.transform.SetParent(chunksRoot.transform);
            chunk2.transform.position = new Vector3(40f, 0f, 0f);

            // Чанк 3: Финал (Кристалл)
            GameObject chunk3 = (GameObject)PrefabUtility.InstantiatePrefab(endChunkPrefab, scene);
            chunk3.name = "Chunk_3_ExitCrystal";
            chunk3.transform.SetParent(chunksRoot.transform);
            chunk3.transform.position = new Vector3(60f, 0f, 0f);

            // 6. Размещение игрока
            Vector3 playerSpawnPos = new Vector3(4.5f, 1.55f, 0f);
            var startChunkComp = chunk0.GetComponent<LevelChunk>();
            if (startChunkComp != null && startChunkComp.PlayerSpawnPoint != null)
            {
                playerSpawnPos = startChunkComp.PlayerSpawnPoint.position;
            }

            GameObject playerObj = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            playerObj.name = "Player";
            playerObj.transform.position = playerSpawnPos;
            playerObj.tag = "Player";

            var playerCaps = playerObj.GetComponent<PlayerCapabilityController>();
            if (playerCaps == null) playerCaps = playerObj.AddComponent<PlayerCapabilityController>();

            // 7. Камера слежения
            var camObj = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(CameraFollow2D), typeof(UniversalAdditionalCameraData));
            camObj.tag = "MainCamera";
            var cam = camObj.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.09f, 1f);
            cam.orthographic = true;
            cam.orthographicSize = 6.0f;
            camObj.transform.position = new Vector3(playerSpawnPos.x, playerSpawnPos.y, -10f);

            var camFollow = camObj.GetComponent<CameraFollow2D>();
            var camFollowSer = new SerializedObject(camFollow);
            camFollowSer.FindProperty("target").objectReferenceValue = playerObj.transform;
            camFollowSer.ApplyModifiedProperties();

            // 8. Размещение манекена на Арене 1
            var dummyObj = (GameObject)PrefabUtility.InstantiatePrefab(dummyPrefab, scene);
            dummyObj.name = "CombatDummy";
            dummyObj.transform.position = new Vector3(30f, 1.0f, 0f);
            var dummyComp = dummyObj.GetComponent<CombatDummy2D>();
            if (dummyComp != null)
            {
                dummyComp.CanDie = false; // Бессмертен
            }

            // 9. Решётка 1 (между Ареной 1 и Ареной 2)
            var door1Obj = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefab, scene);
            door1Obj.name = "Door_To_VovaArena";
            door1Obj.transform.position = new Vector3(39.8f, 0f, 0f);
            var door1 = door1Obj.GetComponent<SectorBarrierDoor2D>();
            if (door1 != null) door1.SetClosed(true, instant: true);

            // 10. Размещение Вовы на Арене 2
            var vovaObj = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab, scene);
            vovaObj.name = "Vova_Skeleton";
            vovaObj.transform.position = new Vector3(50f, 0.5f, 0f);
            var vovaController = vovaObj.GetComponent<TutorialVovaController>();
            if (vovaController == null) vovaController = vovaObj.AddComponent<TutorialVovaController>();

            // 11. Решётка 2 (между Ареной 2 и Финалом)
            var door2Obj = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefab, scene);
            door2Obj.name = "Door_To_ExitCrystal";
            door2Obj.transform.position = new Vector3(59.8f, 0f, 0f);
            var door2 = door2Obj.GetComponent<SectorBarrierDoor2D>();
            if (door2 != null) door2.SetClosed(true, instant: true);

            // 12. Поиск Кристалла победы
            var exitCrystal = chunk3.GetComponentInChildren<VictoryCrystal>();

            // 13. Триггер-зоны
            var triggerRoot = new GameObject("--- TRIGGER ZONES ---");

            var trig1Obj = new GameObject("Trigger_EnterDummyRoom", typeof(BoxCollider2D), typeof(TutorialTriggerZone));
            trig1Obj.transform.SetParent(triggerRoot.transform);
            trig1Obj.transform.position = new Vector3(21.5f, 2f, 0f);
            var trig1Col = trig1Obj.GetComponent<BoxCollider2D>();
            trig1Col.isTrigger = true;
            trig1Col.size = new Vector2(2f, 8f);
            var trig1 = trig1Obj.GetComponent<TutorialTriggerZone>();

            var trig2Obj = new GameObject("Trigger_EnterVovaRoom", typeof(BoxCollider2D), typeof(TutorialTriggerZone));
            trig2Obj.transform.SetParent(triggerRoot.transform);
            trig2Obj.transform.position = new Vector3(41.5f, 2f, 0f);
            var trig2Col = trig2Obj.GetComponent<BoxCollider2D>();
            trig2Col.isTrigger = true;
            trig2Col.size = new Vector2(2f, 8f);
            var trig2 = trig2Obj.GetComponent<TutorialTriggerZone>();

            // Точка входа в комнату манекена (для телепорта после 1 взрыва)
            var dummyEntranceObj = new GameObject("DummyRoomEntrancePoint");
            dummyEntranceObj.transform.position = new Vector3(22.5f, 1.55f, 0f);

            // 14. Менеджеры сцены
            var managersRoot = new GameObject("--- MANAGERS ---");

            var settingsMgrPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Managers/CombatSettingsManager.prefab");
            if (settingsMgrPrefab != null)
            {
                var sm = (GameObject)PrefabUtility.InstantiatePrefab(settingsMgrPrefab, scene);
                sm.transform.SetParent(managersRoot.transform);
            }

            var focusMgrObj = new GameObject("FocusManager", typeof(FocusManager));
            focusMgrObj.transform.SetParent(managersRoot.transform);

            var soundMgrObj = new GameObject("[SoundManager]", typeof(Audio.SoundManager));
            soundMgrObj.transform.SetParent(managersRoot.transform);

            var dialogueMgrObj = new GameObject("[DialogueManager]", typeof(DialogueManager));
            dialogueMgrObj.transform.SetParent(managersRoot.transform);
            var dialogueMgr = dialogueMgrObj.GetComponent<DialogueManager>();

            // 15. Canvas & UI
            GameObject canvasObj = (GameObject)PrefabUtility.InstantiatePrefab(canvasPrefab, scene);
            canvasObj.name = "CombatCanvas";

            // Добавляем DialogueUI
            GameObject dialogueUIObj = (GameObject)PrefabUtility.InstantiatePrefab(dialogueUIPrefab, scene);
            dialogueUIObj.transform.SetParent(canvasObj.transform, false);
            var dialogueUI = dialogueUIObj.GetComponent<DialogueUI>();
            if (dialogueMgr != null && dialogueUI != null)
            {
                dialogueMgr.AssignUI(dialogueUI);
            }

            // Добавляем TutorialObjectiveHUD
            GameObject objectiveHUDObj = (GameObject)PrefabUtility.InstantiatePrefab(objectiveHUDPrefab, scene);
            objectiveHUDObj.transform.SetParent(canvasObj.transform, false);
            var objectiveHUD = objectiveHUDObj.GetComponent<TutorialObjectiveHUD>();

            // 16. [TutorialManager]
            var tutManagerObj = new GameObject("[TutorialManager]", typeof(TutorialManager));
            var tutManager = tutManagerObj.GetComponent<TutorialManager>();

            var tutSer = new SerializedObject(tutManager);
            tutSer.FindProperty("playerSpawnPoint").objectReferenceValue = startChunkComp?.PlayerSpawnPoint;
            tutSer.FindProperty("dummyRoomEntrancePoint").objectReferenceValue = dummyEntranceObj.transform;
            tutSer.FindProperty("dummy").objectReferenceValue = dummyComp;
            tutSer.FindProperty("vovaController").objectReferenceValue = vovaController;
            tutSer.FindProperty("doorToVova").objectReferenceValue = door1;
            tutSer.FindProperty("doorToExit").objectReferenceValue = door2;
            tutSer.FindProperty("exitCrystal").objectReferenceValue = exitCrystal;
            tutSer.FindProperty("dummyRoomTriggerZone").objectReferenceValue = trig1;
            tutSer.FindProperty("vovaRoomTriggerZone").objectReferenceValue = trig2;
            tutSer.FindProperty("dialogueManager").objectReferenceValue = dialogueMgr;
            tutSer.FindProperty("objectiveHUD").objectReferenceValue = objectiveHUD;
            tutSer.FindProperty("focusUI").objectReferenceValue = canvasObj.GetComponentInChildren<FocusUI>(true);
            tutSer.FindProperty("wheelUI").objectReferenceValue = canvasObj.GetComponentInChildren<FencingWheelUI>(true);
            tutSer.FindProperty("settingsUI").objectReferenceValue = canvasObj.GetComponentInChildren<CombatSettingsUI>(true);

            var vovaSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Vova/Ходьба/Ходьба1/0000.png");
            tutSer.FindProperty("vovaPortraitSprite").objectReferenceValue = vovaSprite;

            tutSer.ApplyModifiedProperties();

            // 17. Сохранение сцены
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"<color=#00FFAA><b>[TutorialSceneBuilder]</b></color> Сцена успешно создана и сохранена: {scenePath}");

            // 18. Добавление в Build Settings
            AddSceneToBuildSettings(scenePath);
        }

        public static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            // Проверяем, есть ли уже эта сцена в списке
            bool alreadyInList = false;
            foreach (var s in scenes)
            {
                if (s.path == scenePath)
                {
                    alreadyInList = true;
                    s.enabled = true;
                    break;
                }
            }

            if (!alreadyInList)
            {
                // Вставляем сразу после MainMenu (на 1-ю позицию), перед SampleScene
                int insertIndex = scenes.Count > 1 ? 1 : scenes.Count;
                scenes.Insert(insertIndex, new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
                Debug.Log($"<color=#00FFAA>[BuildSettings]</color> Сцена '{scenePath}' добавлена в список сборки (Index: {insertIndex})!");
            }
        }
    }
}
