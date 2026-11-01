// Assets/Scripts/Editor/FencingSetupHelper.cs
using UnityEditor;
using UnityEngine;
using Fencing;

namespace Fencing.Editor
{
    public static class FencingSetupHelper
    {
        [MenuItem("Fencing/Прикрепить механику фехтования к выбранному игроку", false, 1)]
        [MenuItem("GameObject/Fencing/Add Fencing & Focus to Selected Player", false, 10)]
        public static void AttachToSelectedPlayer()
        {
            GameObject playerObj = Selection.activeGameObject;
            if (playerObj == null)
            {
                playerObj = GameObject.FindWithTag("Player");
            }

            if (playerObj == null)
            {
                EditorUtility.DisplayDialog("Fencing Setup", "Пожалуйста, выделите объект вашего игрока в иерархии (или назначьте ему тег 'Player').", "OK");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(playerObj, "Add Fencing Mechanics");

            // 1. Убеждаемся, что тег Player назначен
            if (!playerObj.CompareTag("Player"))
            {
                playerObj.tag = "Player";
            }

            // 2. FencingPlayer
            FencingPlayer fencingPlayer = playerObj.GetComponent<FencingPlayer>();
            if (fencingPlayer == null)
            {
                fencingPlayer = playerObj.AddComponent<FencingPlayer>();
            }

            // 3. SwordHitbox
            Transform hitboxTransform = playerObj.transform.Find("SwordHitbox");
            GameObject hitboxObj = (hitboxTransform != null) ? hitboxTransform.gameObject : new GameObject("SwordHitbox");
            hitboxObj.transform.SetParent(playerObj.transform, false);
            hitboxObj.transform.localPosition = Vector3.zero;

            SwordHitbox swordHitbox = hitboxObj.GetComponent<SwordHitbox>();
            if (swordHitbox == null)
            {
                swordHitbox = hitboxObj.AddComponent<SwordHitbox>();
            }
            swordHitbox.EnsureInitialized();

            // 4. FencingWheel
            Transform wheelTransform = playerObj.transform.Find("FencingWheel");
            GameObject wheelObj = (wheelTransform != null) ? wheelTransform.gameObject : new GameObject("FencingWheel");
            wheelObj.transform.SetParent(playerObj.transform, false);
            wheelObj.transform.localPosition = Vector3.zero;

            FencingWheel wheel = wheelObj.GetComponent<FencingWheel>();
            if (wheel == null)
            {
                wheel = wheelObj.AddComponent<FencingWheel>();
            }

            // AttackOrb
            Transform orbTransform = wheelObj.transform.Find("AttackOrb");
            GameObject orbObj = (orbTransform != null) ? orbTransform.gameObject : new GameObject("AttackOrb");
            orbObj.transform.SetParent(wheelObj.transform, false);
            orbObj.transform.localPosition = Vector3.zero;

            AttackOrb orb = orbObj.GetComponent<AttackOrb>();
            if (orb == null)
            {
                orb = orbObj.AddComponent<AttackOrb>();
            }

            // FencingWheelGraphic (процедурный меш на персонаже)
            FencingWheelGraphic graphic = wheelObj.GetComponent<FencingWheelGraphic>();
            if (graphic == null)
            {
                graphic = wheelObj.AddComponent<FencingWheelGraphic>();
            }

            wheel.RebuildSectors();

            // Привязываем сериализованные ссылки через SerializedObject
            SerializedObject soPlayer = new SerializedObject(fencingPlayer);
            var propWheel = soPlayer.FindProperty("fencingWheel");
            if (propWheel != null) propWheel.objectReferenceValue = wheel;
            var propHitbox = soPlayer.FindProperty("swordHitbox");
            if (propHitbox != null) propHitbox.objectReferenceValue = swordHitbox;
            soPlayer.ApplyModifiedProperties();

            SerializedObject soWheel = new SerializedObject(wheel);
            var propOrb = soWheel.FindProperty("orb");
            if (propOrb != null) propOrb.objectReferenceValue = orb;
            soWheel.ApplyModifiedProperties();

            SerializedObject soOrb = new SerializedObject(orb);
            var propOrbWheel = soOrb.FindProperty("wheel");
            if (propOrbWheel != null) propOrbWheel.objectReferenceValue = wheel;
            soOrb.ApplyModifiedProperties();

            // 5. FocusManager на сцене
#if UNITY_6000_0_OR_NEWER
            FocusManager focusManager = Object.FindAnyObjectByType<FocusManager>();
#else
            FocusManager focusManager = Object.FindObjectOfType<FocusManager>();
#endif
            if (focusManager == null)
            {
                GameObject focusObj = new GameObject("FocusManager");
                focusManager = focusObj.AddComponent<FocusManager>();
                Undo.RegisterCreatedObjectUndo(focusObj, "Create FocusManager");
            }

            // Назначаем префабы и компоненты в FocusManager
            SerializedObject soFocus = new SerializedObject(focusManager);
            var propChargePrefab = soFocus.FindProperty("circleChargePrefab");
            if (propChargePrefab != null && propChargePrefab.objectReferenceValue == null)
            {
                var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CircleCharge.prefab");
                if (prefabAsset != null) propChargePrefab.objectReferenceValue = prefabAsset;
            }

            var propSpawner = soFocus.FindProperty("weakPointSpawner");
            if (propSpawner != null && propSpawner.objectReferenceValue == null)
            {
                var spawnerComp = focusManager.GetComponent<WeakPointSpawner>();
                if (spawnerComp == null) spawnerComp = focusManager.gameObject.AddComponent<WeakPointSpawner>();
                propSpawner.objectReferenceValue = spawnerComp;
            }
            soFocus.ApplyModifiedProperties();

            EditorUtility.SetDirty(playerObj);
            EditorUtility.SetDirty(focusManager.gameObject);

            Selection.activeGameObject = playerObj;
            Debug.Log($"<color=#00FF88>[Fencing]</color> Механики фехтования и фокуса успешно подключены к объекту: <b>{playerObj.name}</b>!");
            EditorUtility.DisplayDialog("Fencing Setup", $"Механики фехтования и фокуса успешно подключены к '{playerObj.name}'!\n\n- Колесо фехтования (FencingWheel + AttackOrb)\n- Хитбокс меча (SwordHitbox)\n- Менеджер фокуса (FocusManager + WeakPointSpawner)\n\nВсе ссылки выставлены автоматически.", "Готово");
        }

        [MenuItem("Fencing/Экспорт ЧИСТОГО пакета фехтования и фокуса", false, 2)]
        public static void ExportPureCorePackage()
        {
            string[] assetPaths = new string[]
            {
                "Assets/Scripts/Fencing/AttackDirection.cs",
                "Assets/Scripts/Fencing/AttackSector.cs",
                "Assets/Scripts/Fencing/AttackOrb.cs",
                "Assets/Scripts/Fencing/FencingWheel.cs",
                "Assets/Scripts/Fencing/FencingWheelGraphic.cs",
                "Assets/Scripts/Fencing/FencingWheelUI.cs",
                "Assets/Scripts/Fencing/SwordHitbox.cs",
                "Assets/Scripts/Fencing/FencingPlayer.cs",
                "Assets/Scripts/FocusManager.cs",
                "Assets/Scripts/CircleCharge.cs",
                "Assets/Scripts/StrikeTrajectory.cs",
                "Assets/Scripts/WeakPoint.cs",
                "Assets/Scripts/WeakPointSpawner.cs",
                "Assets/Scripts/FocusUI.cs",
                "Assets/Scripts/Editor/FencingSetupHelper.cs",
                "Assets/Prefabs/CircleCharge.prefab",
                "Assets/Prefabs/WeakPoint.prefab",
                "Assets/Materials/CircleCharge_Unlit.mat",
                "Assets/Sprites/CrossSprite.png",
                "Assets/Sprites/VignetteRadial.png",
                "Assets/Sprites/RingSprite.png"
            };

            string exportPath = "FencingAndFocus_Only.unitypackage";
            AssetDatabase.ExportPackage(assetPaths, exportPath, ExportPackageOptions.Default);
            Debug.Log($"<color=#00FF88>[Export]</color> Чистый пакет фехтования и фокуса экспортирован: <b>{exportPath}</b>");
        }
    }
}
