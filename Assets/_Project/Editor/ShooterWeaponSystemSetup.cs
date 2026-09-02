#if UNITY_EDITOR
using System;
using KINEMATION.FPSAnimationFramework.Runtime.Camera;
using KINEMATION.FPSAnimationFramework.Runtime.Core;
using KINEMATION.Shared.KAnimationCore.Runtime.Rig;
using KINEMATION.FPSAnimationFramework.Runtime.Layers.IkMotionLayer;
using KINEMATION.FPSAnimationFramework.Runtime.Playables;
using KINEMATION.FPSAnimationFramework.Runtime.Recoil;
using KINEMATION.ProceduralRecoilAnimationSystem.Runtime;
using Shooter.Project.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Project.Editor
{
    /// <summary>
    /// Creates Mk18 / AK12 / Pistol project prefabs and wires them to WeaponManager slots 0–2.
    /// </summary>
    public static class ShooterWeaponSystemSetup
    {
        const string PlayerPrefabPath = "Assets/_Project/Prefabs/PlayerCharacter.prefab";
        const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        const string WeaponsFolder = "Assets/_Project/Weapons";
        const string PrefabsFolder = "Assets/_Project/Weapons/Prefabs";

        const string Mk18Demo = "Assets/Demo/Prefabs/Mk18/Mk18_Scriptable.prefab";
        const string Ak12Demo = "Assets/Demo/Prefabs/AK12/AK12_Scriptable.prefab";
        const string PistolDemo = "Assets/Demo/Prefabs/Pistol/Mk23Mod0_Scriptable.prefab";

        const string EquipMotion = "Assets/Demo/AnimatorProfiles/IKMotions/IKMotion_Equip.asset";
        const string UnequipMotion = "Assets/Demo/AnimatorProfiles/IKMotions/IKMotion_UnEquip.asset";
        const string CameraShake = "Assets/Demo/Prefabs/AK12/RecoilCameraShake.asset";

        [MenuItem("Shooter/Project/Add Weapon System")]
        public static void AddWeaponSystemFromProjectMenu() => AddWeaponSystemImpl();

        [MenuItem("Shooter/Phase 2/Add Weapon System")]
        public static void AddWeaponSystemFromPhase2Menu() => AddWeaponSystemImpl();

        [MenuItem("Shooter/Project/Setup Ranged Weapons (Mk18 / AK12 / Pistol)")]
        public static void SetupRangedWeaponsMenu() => AddWeaponSystemImpl();

        public static void TrySetupOnPlayer(GameObject playerRoot)
        {
            if (playerRoot == null)
                return;

            Type inventoryType = Type.GetType(
                "Shooter.Project.Weapons.ShooterPlayerInventory, Shooter.Project");
            Type managerType = Type.GetType(
                "Shooter.Project.Weapons.WeaponManager, Shooter.Project");

            if (inventoryType == null || managerType == null)
            {
                Debug.LogWarning(
                    "[Shooter] Weapon types not compiled yet — skip weapon setup.");
                return;
            }

            SetupOn(playerRoot);
        }

        static void AddWeaponSystemImpl()
        {
            EnsureFolders();
            WeaponBase mk18 = CreateOrUpdateRangedPrefab(
                "Ranged_Mk18", Mk18Demo, "Mk18", AmmoType.Rifle,
                30, 90, 750f, true, 25f, 150f, 2.1f,
                "Assets/Demo/Animations/Weapons/Mk18/AA_Mk18_ReloadEmpty.asset",
                "Assets/Demo/Prefabs/Mk18/Recoil_Mk18.asset",
                "Assets/Demo/Prefabs/Mk18/RecoilPattern_AR.asset");

            WeaponBase ak12 = CreateOrUpdateRangedPrefab(
                "Ranged_AK12", Ak12Demo, "AK12", AmmoType.Rifle,
                30, 90, 600f, true, 25f, 150f, 2.2f,
                "Assets/Demo/Animations/Weapons/AK12/AA_AK12_Reload_Empty.asset",
                "Assets/Demo/Prefabs/AK12/Recoil_AK12.asset",
                "Assets/Demo/Prefabs/AK12/RecoilPattern_AK.asset");

            WeaponBase pistol = CreateOrUpdateRangedPrefab(
                "Ranged_Mk23", PistolDemo, "Mk23", AmmoType.Pistol,
                12, 36, 400f, false, 22f, 80f, 1.6f,
                "Assets/Demo/Animations/Weapons/Pistol/AA_PistolReloadEmpty.asset",
                "Assets/Demo/Prefabs/Pistol/Recoil_Pistol.asset",
                "Assets/Demo/Prefabs/Pistol/RecoilPattern_Pistol.asset");

            AssetDatabase.SaveAssets();

            int updated = 0;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab != null)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (instance != null)
                {
                    try
                    {
                        SetupOn(instance, mk18, ak12, pistol);
                        PrefabUtility.SaveAsPrefabAsset(instance, PlayerPrefabPath);
                        updated++;
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(instance);
                    }
                }
            }

            GameObject scenePlayer = GameObject.Find("PlayerCharacter");
            if (scenePlayer != null)
            {
                SetupOn(scenePlayer, mk18, ak12, pistol);
                PrefabUtility.ApplyPrefabInstance(scenePlayer, InteractionMode.UserAction);
                updated++;
            }

            AssetDatabase.SaveAssets();

            if (updated == 0)
            {
                EditorUtility.DisplayDialog(
                    "Weapon system",
                    "PlayerCharacter not found.\nRun Phase 1 setup first.",
                    "OK");
                return;
            }

            EditorUtility.DisplayDialog(
                "Ranged weapons",
                "Slots ready:\n" +
                "1 = holster\n" +
                "2 = Mk18\n" +
                "3 = AK12\n" +
                "4 = Mk23 pistol\n\n" +
                "Weapons are embedded under IK WeaponBone.\n" +
                "Edit position/rotation in the hierarchy.\n\n" +
                "LMB fire, R reload.\n" +
                "Stop Play → Play again.",
                "OK");
        }

        static void SetupOn(GameObject playerRoot, WeaponBase mk18 = null, WeaponBase ak12 = null, WeaponBase pistol = null)
        {
            if (playerRoot.GetComponent<ShooterPlayerInventory>() == null)
                playerRoot.AddComponent<ShooterPlayerInventory>();
            if (playerRoot.GetComponent<WeaponManager>() == null)
                playerRoot.AddComponent<WeaponManager>();

            var legacy = playerRoot.GetComponent<ShooterWeaponController>();
            if (legacy != null)
                legacy.enabled = false;

            Transform model = playerRoot.transform.Find("Graphics/Character_model");
            Transform weaponBone = model != null
                ? model.GetComponentInChildren<KRigComponent>(true)?.GetRigTransform(
                    new KRigElement(-1, FPSANames.IkWeaponBone))
                : null;
            if (weaponBone == null && model != null)
                weaponBone = FindDeepChild(model, WeaponPrefabUtility.IkWeaponBoneName);
            InputActionAsset inputActions =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);

            EnsureRecoilPattern(model != null ? model.gameObject : playerRoot);

            if (mk18 == null)
                mk18 = AssetDatabase.LoadAssetAtPath<WeaponBase>(PrefabsFolder + "/Ranged_Mk18.prefab");
            if (ak12 == null)
                ak12 = AssetDatabase.LoadAssetAtPath<WeaponBase>(PrefabsFolder + "/Ranged_AK12.prefab");
            if (pistol == null)
                pistol = AssetDatabase.LoadAssetAtPath<WeaponBase>(PrefabsFolder + "/Ranged_Mk23.prefab");

            var manager = playerRoot.GetComponent<WeaponManager>();
            var managerSo = new SerializedObject(manager);
            managerSo.FindProperty("inputActions").objectReferenceValue = inputActions;
            managerSo.FindProperty("weaponAttachPoint").objectReferenceValue = weaponBone;
            managerSo.FindProperty("autoCollectFromAttachPoint").boolValue = true;

            WeaponBase slot0 = EnsureEmbeddedWeapon(weaponBone, mk18, 0);
            WeaponBase slot1 = EnsureEmbeddedWeapon(weaponBone, ak12, 1);
            WeaponBase slot2 = EnsureEmbeddedWeapon(weaponBone, pistol, 2);

            SerializedProperty slots = managerSo.FindProperty("weaponSlots");
            slots.arraySize = 5;
            slots.GetArrayElementAtIndex(0).objectReferenceValue = slot0;
            slots.GetArrayElementAtIndex(1).objectReferenceValue = slot1;
            slots.GetArrayElementAtIndex(2).objectReferenceValue = slot2;
            slots.GetArrayElementAtIndex(3).objectReferenceValue = null;
            slots.GetArrayElementAtIndex(4).objectReferenceValue = null;
            managerSo.ApplyModifiedPropertiesWithoutUndo();

            var inventorySo = new SerializedObject(playerRoot.GetComponent<ShooterPlayerInventory>());
            inventorySo.FindProperty("hasGun1").boolValue = true;
            inventorySo.FindProperty("hasGun2").boolValue = true;
            inventorySo.FindProperty("hasGun3").boolValue = true;
            inventorySo.FindProperty("hasGun4").boolValue = false;
            inventorySo.FindProperty("hasGun5").boolValue = false;
            inventorySo.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(playerRoot);
        }

        static WeaponBase EnsureEmbeddedWeapon(Transform weaponBone, WeaponBase prefabAsset, int slotIndex)
        {
            if (weaponBone == null || prefabAsset == null)
                return null;

            WeaponBase[] existing = weaponBone.GetComponentsInChildren<WeaponBase>(true);
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i].SlotIndex == slotIndex)
                    return existing[i];
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefabAsset.gameObject, weaponBone) as GameObject;
            if (instance == null)
                return null;

            instance.name = prefabAsset.name;

            WeaponBase weapon = instance.GetComponent<WeaponBase>();
            if (weapon != null)
            {
                var so = new SerializedObject(weapon);
                so.FindProperty("slotIndex").intValue = slotIndex;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return weapon;
        }

        static void EnsureRecoilPattern(GameObject fpsRoot)
        {
            if (fpsRoot == null)
                return;

            var pattern = fpsRoot.GetComponent<RecoilPattern>();
            if (pattern == null)
                pattern = fpsRoot.AddComponent<RecoilPattern>();

            var so = new SerializedObject(pattern);
            SerializedProperty prop = so.FindProperty("deltaLookInputProperty");
            if (prop != null && string.IsNullOrEmpty(prop.stringValue))
            {
                prop.stringValue = "MouseDeltaInput";
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project"))
                AssetDatabase.CreateFolder("Assets", "_Project");
            if (!AssetDatabase.IsValidFolder(WeaponsFolder))
                AssetDatabase.CreateFolder("Assets/_Project", "Weapons");
            if (!AssetDatabase.IsValidFolder(PrefabsFolder))
                AssetDatabase.CreateFolder(WeaponsFolder, "Prefabs");
        }

        static WeaponBase CreateOrUpdateRangedPrefab(
            string name,
            string demoPrefabPath,
            string displayId,
            AmmoType ammoType,
            int mag,
            int reserve,
            float rpm,
            bool auto,
            float damage,
            float range,
            float reloadSeconds,
            string reloadClipPath,
            string recoilDataPath,
            string recoilPatternPath)
        {
            string path = PrefabsFolder + "/" + name + ".prefab";
            GameObject demo = AssetDatabase.LoadAssetAtPath<GameObject>(demoPrefabPath);
            if (demo == null)
            {
                Debug.LogError("[Shooter] Demo prefab missing: " + demoPrefabPath);
                return AssetDatabase.LoadAssetAtPath<WeaponBase>(path);
            }

            GameObject root = new GameObject(name);
            try
            {
                var ranged = root.AddComponent<RangedWeapon>();
                GameObject view = PrefabUtility.InstantiatePrefab(demo, root.transform) as GameObject;
                if (view != null)
                {
                    view.name = displayId + "_View";
                    view.transform.localPosition = Vector3.zero;
                    view.transform.localRotation = Quaternion.identity;
                    StripDemoGameplay(view);
                    WireFpsAnimatorEntity(view);
                    StripWeaponPhysics(view);
                }

                var so = new SerializedObject(ranged);
                so.FindProperty("weaponId").stringValue = displayId;
                so.FindProperty("ammoType").enumValueIndex = (int)ammoType;
                so.FindProperty("magazineSize").intValue = mag;
                so.FindProperty("startReserve").intValue = reserve;
                so.FindProperty("fireRateRpm").floatValue = rpm;
                so.FindProperty("supportsAuto").boolValue = auto;
                so.FindProperty("damage").floatValue = damage;
                so.FindProperty("range").floatValue = range;
                so.FindProperty("reloadSeconds").floatValue = reloadSeconds;
                so.FindProperty("reloadClip").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<FPSAnimationAsset>(reloadClipPath);
                so.FindProperty("equipMotion").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<IkMotionLayerSettings>(EquipMotion);
                so.FindProperty("unEquipMotion").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<IkMotionLayerSettings>(UnequipMotion);
                so.FindProperty("recoilData").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<RecoilAnimData>(recoilDataPath);
                so.FindProperty("recoilPatternSettings").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<RecoilPatternSettings>(recoilPatternPath);
                so.FindProperty("cameraShake").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<FPSCameraShake>(CameraShake);

                Transform aim = root.transform.Find(displayId + "_View/AimPoint")
                    ?? FindDeepChild(root.transform, "AimPoint");
                if (aim != null)
                    so.FindProperty("muzzlePoint").objectReferenceValue = aim;

                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            return AssetDatabase.LoadAssetAtPath<WeaponBase>(path);
        }

        static void WireFpsAnimatorEntity(GameObject viewRoot)
        {
            if (viewRoot == null)
                return;

            var entity = viewRoot.GetComponent<FPSAnimatorEntity>();
            if (entity == null || entity.defaultAimPoint != null)
                return;

            Transform aim = FindDeepChild(viewRoot.transform, "AimPoint")
                ?? FindDeepChild(viewRoot.transform, "PointAim")
                ?? FindDeepChild(viewRoot.transform, "AimPointBase");

            if (aim == null)
                return;

            var so = new SerializedObject(entity);
            so.FindProperty("defaultAimPoint").objectReferenceValue = aim;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void StripWeaponPhysics(GameObject root)
        {
            if (root == null)
                return;

            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            {
                if (collider != null)
                    UnityEngine.Object.DestroyImmediate(collider);
            }

            foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true))
            {
                if (body != null)
                    UnityEngine.Object.DestroyImmediate(body);
            }
        }

        static void StripDemoGameplay(GameObject root)
        {
            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                    continue;
                string ns = behaviour.GetType().Namespace ?? string.Empty;
                if (ns.StartsWith("Demo.Scripts", StringComparison.Ordinal))
                    UnityEngine.Object.DestroyImmediate(behaviour);
            }
        }

        static Transform FindDeepChild(Transform parent, string name)
        {
            Transform[] all = parent.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == name)
                    return all[i];
            }

            return null;
        }
    }
}
#endif
