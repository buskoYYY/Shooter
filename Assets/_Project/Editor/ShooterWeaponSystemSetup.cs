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
using UnityEditor.SceneManagement;
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
        const string VfxFolder = "Assets/_Project/Weapons/VFX";
        const string TestScenePath = "Assets/_Project/Scenes/PlayerTest.unity";

        const string SfxRifleFire = "Assets/KINEMATION/FPSAnimationPack/SFX/AK/S_AKX200_Shot_0.WAV";
        const string SfxRifleReload = "Assets/KINEMATION/FPSAnimationPack/SFX/AK/S_AKX200_Reload_Empty_Full.WAV";
        const string SfxMk18Reload = "Assets/KINEMATION/FPSAnimationPack/SFX/Mk14EBR/S_MK14_Reload_Empty_Full.WAV";
        const string SfxPistolFire = "Assets/KINEMATION/FPSAnimationPack/SFX/MX16A4/S_MX16A4_Fire_A.WAV";
        const string SfxPistolReload = "Assets/KINEMATION/FPSAnimationPack/SFX/M1911/S_M1911_Reload_Empty.wav";

        // Tuned local offsets relative to IK WeaponBone (do not zero on rebuild).
        static readonly Vector3 Mk18AttachPos = new Vector3(-0.039f, 0.05f, -0.009f);
        static readonly Vector3 Mk18AttachEuler = new Vector3(0.22f, 347.7f, 359.7f);
        static readonly Vector3 Ak12AttachPos = new Vector3(-0.033f, 0.07f, -0.007f);
        static readonly Vector3 Ak12AttachEuler = new Vector3(0.25f, 347.6f, 0.13f);
        static readonly Vector3 Mk23AttachPos = new Vector3(-0.016f, 0.026f, -0.156f);
        static readonly Vector3 Mk23AttachEuler = new Vector3(0.77f, 345.55f, 359.83f);

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

        [MenuItem("Shooter/Project/Add Weapon Test Targets")]
        public static void AddWeaponTestTargetsMenu() => AddWeaponTestTargets();

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
            GameObject shell = EnsureShellCasingPrefab();

            WeaponBase mk18 = CreateOrUpdateRangedPrefab(
                "Ranged_Mk18", Mk18Demo, "Mk18", AmmoType.Rifle,
                30, 90, 750f, true, 25f, 150f, 2.1f,
                "Assets/Demo/Animations/Weapons/Mk18/AA_Mk18_ReloadEmpty.asset",
                "Assets/Demo/Prefabs/Mk18/Recoil_Mk18.asset",
                "Assets/Demo/Prefabs/Mk18/RecoilPattern_AR.asset",
                SfxRifleFire, SfxMk18Reload, shell,
                0, Mk18AttachPos, Mk18AttachEuler);

            WeaponBase ak12 = CreateOrUpdateRangedPrefab(
                "Ranged_AK12", Ak12Demo, "AK12", AmmoType.Rifle,
                30, 90, 600f, true, 25f, 150f, 2.2f,
                "Assets/Demo/Animations/Weapons/AK12/AA_AK12_Reload_Empty.asset",
                "Assets/Demo/Prefabs/AK12/Recoil_AK12.asset",
                "Assets/Demo/Prefabs/AK12/RecoilPattern_AK.asset",
                SfxRifleFire, SfxRifleReload, shell,
                1, Ak12AttachPos, Ak12AttachEuler);

            WeaponBase pistol = CreateOrUpdateRangedPrefab(
                "Ranged_Mk23", PistolDemo, "Mk23", AmmoType.Pistol,
                12, 36, 400f, false, 22f, 80f, 1.6f,
                "Assets/Demo/Animations/Weapons/Pistol/AA_PistolReloadEmpty.asset",
                "Assets/Demo/Prefabs/Pistol/Recoil_Pistol.asset",
                "Assets/Demo/Prefabs/Pistol/RecoilPattern_Pistol.asset",
                SfxPistolFire, SfxPistolReload, shell,
                2, Mk23AttachPos, Mk23AttachEuler);

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
                "Weapons keep attach / muzzle flash on rebuild.\n" +
                "After moving a weapon: RMB component → Capture Attach From Transform.\n\n" +
                "LMB fire, R reload.\n" +
                "Optional: Shooter → Project → Add Weapon Test Targets.\n" +
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
                if (existing[i].SlotIndex != slotIndex)
                    continue;

                // Do not overwrite attach/transform on weapons already placed/tuned in the hierarchy.
                var so = new SerializedObject(existing[i]);
                so.FindProperty("slotIndex").intValue = slotIndex;
                so.ApplyModifiedPropertiesWithoutUndo();
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
                weapon.ApplyAttachTransform();
                EditorUtility.SetDirty(weapon);
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
            if (!AssetDatabase.IsValidFolder(VfxFolder))
                AssetDatabase.CreateFolder(WeaponsFolder, "VFX");
        }

        static GameObject EnsureMuzzleFlashPrefab()
        {
            string path = VfxFolder + "/MuzzleFlash.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
                return existing;

            var root = new GameObject("MuzzleFlash");
            try
            {
                var ps = root.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 0.06f;
                main.loop = false;
                main.playOnAwake = true;
                main.startLifetime = 0.05f;
                main.startSpeed = 0.2f;
                main.startSize = 0.12f;
                main.startColor = new Color(1f, 0.82f, 0.35f, 1f);
                main.maxParticles = 12;
                main.simulationSpace = ParticleSystemSimulationSpace.World;

                var emission = ps.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)8) });

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.02f;

                var lightGo = new GameObject("FlashLight");
                lightGo.transform.SetParent(root.transform, false);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 2f;
                light.intensity = 2.5f;
                light.color = new Color(1f, 0.75f, 0.3f);

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        static GameObject EnsureShellCasingPrefab()
        {
            string path = VfxFolder + "/ShellCasing.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
                return existing;

            var shell = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shell.name = "ShellCasing";
            try
            {
                shell.transform.localScale = new Vector3(0.01f, 0.018f, 0.01f);
                shell.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                var rb = shell.AddComponent<Rigidbody>();
                rb.mass = 0.008f;
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

                var renderer = shell.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = new Color(0.82f, 0.62f, 0.18f);
                    renderer.sharedMaterial = mat;
                }

                PrefabUtility.SaveAsPrefabAsset(shell, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(shell);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        static void AddWeaponTestTargets()
        {
            if (!System.IO.File.Exists(TestScenePath))
            {
                EditorUtility.DisplayDialog(
                    "Weapon test targets",
                    "Scene not found:\n" + TestScenePath,
                    "OK");
                return;
            }

            var scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
            Transform root = GetOrCreateRoot("WeaponTest").transform;

            CreateDummyTarget(root, "DummyTarget_Near", new Vector3(4f, 1f, 8f), new Vector3(0.8f, 2f, 0.4f));
            CreateDummyTarget(root, "DummyTarget_Mid", new Vector3(-2f, 1.2f, 12f), new Vector3(0.8f, 2.2f, 0.4f));
            CreateDummyTarget(root, "DummyTarget_Far", new Vector3(0f, 1.5f, 20f), new Vector3(1f, 2.5f, 0.5f));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            EditorUtility.DisplayDialog(
                "Weapon test targets",
                "Added 3 damageable targets under WeaponTest.\n\n" +
                "Play → equip weapon (2–4) → shoot targets.\n" +
                "They tint red as health drops.",
                "OK");
        }

        static GameObject GetOrCreateRoot(string name)
        {
            var existing = GameObject.Find(name);
            if (existing != null)
                return existing;

            return new GameObject(name);
        }

        static void CreateDummyTarget(Transform parent, string name, Vector3 position, Vector3 scale)
        {
            if (parent.Find(name) != null)
                return;

            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = name;
            target.transform.SetParent(parent, false);
            target.transform.position = position;
            target.transform.localScale = scale;
            target.AddComponent<ShooterDummyDamageable>();

            var renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(renderer.sharedMaterial);
                mat.color = new Color(0.75f, 0.45f, 0.2f);
                renderer.sharedMaterial = mat;
            }
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
            string recoilPatternPath,
            string fireSfxPath,
            string reloadSfxPath,
            GameObject shellPrefab,
            int defaultSlotIndex,
            Vector3 attachLocalPosition,
            Vector3 attachLocalEulerAngles)
        {
            string path = PrefabsFolder + "/" + name + ".prefab";
            GameObject demo = AssetDatabase.LoadAssetAtPath<GameObject>(demoPrefabPath);
            if (demo == null)
            {
                Debug.LogError("[Shooter] Demo prefab missing: " + demoPrefabPath);
                return AssetDatabase.LoadAssetAtPath<WeaponBase>(path);
            }

            var existingWeapon = AssetDatabase.LoadAssetAtPath<WeaponBase>(path);
            if (existingWeapon != null)
            {
                // Update stats/SFX in place — never wipe attach / muzzle flash / hierarchy pose.
                ApplyRangedSerializedFields(
                    existingWeapon,
                    displayId,
                    ammoType,
                    mag,
                    reserve,
                    rpm,
                    auto,
                    damage,
                    range,
                    reloadSeconds,
                    reloadClipPath,
                    recoilDataPath,
                    recoilPatternPath,
                    fireSfxPath,
                    reloadSfxPath,
                    shellPrefab,
                    defaultSlotIndex,
                    preserveAttach: true,
                    attachLocalPosition,
                    attachLocalEulerAngles,
                    preserveMuzzleFlash: true);

                existingWeapon.ApplyAttachTransform();
                EditorUtility.SetDirty(existingWeapon);
                EditorUtility.SetDirty(existingWeapon.gameObject);
                return existingWeapon;
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

                ApplyRangedSerializedFields(
                    ranged,
                    displayId,
                    ammoType,
                    mag,
                    reserve,
                    rpm,
                    auto,
                    damage,
                    range,
                    reloadSeconds,
                    reloadClipPath,
                    recoilDataPath,
                    recoilPatternPath,
                    fireSfxPath,
                    reloadSfxPath,
                    shellPrefab,
                    defaultSlotIndex,
                    preserveAttach: false,
                    attachLocalPosition,
                    attachLocalEulerAngles,
                    preserveMuzzleFlash: false);

                ranged.ApplyAttachTransform();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            return AssetDatabase.LoadAssetAtPath<WeaponBase>(path);
        }

        static void ApplyRangedSerializedFields(
            WeaponBase weapon,
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
            string recoilPatternPath,
            string fireSfxPath,
            string reloadSfxPath,
            GameObject shellPrefab,
            int defaultSlotIndex,
            bool preserveAttach,
            Vector3 attachLocalPosition,
            Vector3 attachLocalEulerAngles,
            bool preserveMuzzleFlash)
        {
            var so = new SerializedObject(weapon);
            so.FindProperty("weaponId").stringValue = displayId;
            so.FindProperty("slotIndex").intValue = defaultSlotIndex;

            if (!preserveAttach)
            {
                so.FindProperty("attachLocalPosition").vector3Value = attachLocalPosition;
                so.FindProperty("attachLocalEulerAngles").vector3Value = attachLocalEulerAngles;
            }

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

            if (so.FindProperty("muzzlePoint").objectReferenceValue == null)
            {
                Transform aim = FindDeepChild(weapon.transform, "AimPoint")
                    ?? FindDeepChild(weapon.transform, "PointAim");
                if (aim != null)
                    so.FindProperty("muzzlePoint").objectReferenceValue = aim;
            }

            if (!preserveMuzzleFlash)
                so.FindProperty("muzzleFlashPrefab").objectReferenceValue = null;

            if (so.FindProperty("shellEjectPrefab").objectReferenceValue == null && shellPrefab != null)
                so.FindProperty("shellEjectPrefab").objectReferenceValue = shellPrefab;

            if (so.FindProperty("fireSfx").objectReferenceValue == null)
            {
                so.FindProperty("fireSfx").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<AudioClip>(fireSfxPath);
            }

            if (so.FindProperty("reloadSfx").objectReferenceValue == null)
            {
                so.FindProperty("reloadSfx").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<AudioClip>(reloadSfxPath);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
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
