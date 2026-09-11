#if UNITY_EDITOR
using System.IO;
using KINEMATION.FPSAnimationFramework.Runtime.Core;
using KINEMATION.FPSAnimationFramework.Runtime.Layers.IkMotionLayer;
using KINEMATION.FPSAnimationFramework.Runtime.Playables;
using KINEMATION.Shared.KAnimationCore.Runtime.Rig;
using Shooter.Project.Weapons;
using UnityEditor;
using UnityEngine;

namespace Shooter.Project.Editor
{
    /// <summary>
    /// Humanoid knife hold/attack + CombatKnife mesh → slot 4 (key 5).
    /// Prefer <see cref="ShooterCombatKnifeRetargetSetup"/> for FP_CombatKnife bake.
    /// </summary>
    public static class ShooterKnifeSetup
    {
        const string CharacterModelPath = "Assets/_Project/Packages/Models/Character_model.fbx";
        const string RigPath = "Assets/_Project/FPS/Rig_CharacterModel.asset";
        const string HoldClipFbx = "Assets/Demo/Animations/Locomotion/Humanoid/Knife/C_Knife_Static_Humanoid.fbx";
        const string AttackClipFbx = "Assets/Demo/Animations/Locomotion/Humanoid/Knife/C_Stabbing_Humanoid.fbx";
        const string HoldAssetPath = "Assets/_Project/FPS/AA_Knife_Hold_Humanoid.asset";
        const string AttackAssetPath = "Assets/_Project/FPS/AA_Knife_Attack_Humanoid.asset";
        const string KnifeMeshPath = "Assets/_Project/Packages/CombatKnife/Pickup_CombatKnife.fbx";
        const string KnifeMeshFallback = "Assets/_Project/Packages/CombatKnife/FP_CombatKnife.fbx";
        const string KnifeMeshFallbackAlt = "Assets/_Project/Packages/Melee/CombatKnife/FP_CombatKnife.fbx";
        const string PrefabPath = "Assets/_Project/Weapons/Prefabs/Melee_Knife.prefab";
        const string PlayerPrefabPath = "Assets/_Project/Prefabs/PlayerCharacter.prefab";
        const string EquipMotion = "Assets/Demo/AnimatorProfiles/IKMotions/IKMotion_Equip.asset";
        const string UnequipMotion = "Assets/Demo/AnimatorProfiles/IKMotions/IKMotion_UnEquip.asset";

        static readonly Vector3 KnifeAttachPos = new Vector3(-0.02f, 0.03f, -0.08f);
        static readonly Vector3 KnifeAttachEuler = new Vector3(10f, 170f, 0f);

        /// <summary>Called after CombatKnife bake — reuses AA assets and rewires player slot.</summary>
        public static void SetupMeleeKnifeFromBaked(
            FPSAnimationAsset holdAsset,
            FPSAnimationAsset attackAsset,
            FPSAnimationAsset attackAltAsset)
        {
            if (holdAsset == null || attackAsset == null)
                return;

            MeleeWeapon prefab = EnsureKnifePrefab(holdAsset, attackAsset, attackAltAsset);
            WirePlayer(prefab);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Shooter/Project/Setup Melee Knife (Humanoid)")]
        public static void SetupMeleeKnife()
        {
            ConfigureHumanoidImport(HoldClipFbx);
            ConfigureHumanoidImport(AttackClipFbx);

            AnimationClip holdClip = LoadFirstClip(HoldClipFbx);
            AnimationClip attackClip = LoadFirstClip(AttackClipFbx);
            if (holdClip == null || attackClip == null)
            {
                EditorUtility.DisplayDialog(
                    "Knife Setup",
                    "Не найдены Humanoid knife клипы в Demo/.../Humanoid/Knife/.\n" +
                    "Нужны C_Knife_Static_Humanoid и C_Stabbing_Humanoid.",
                    "OK");
                return;
            }

            FPSAnimationAsset holdAsset = EnsureAaAsset(HoldAssetPath, holdClip, loopingPose: true);
            FPSAnimationAsset attackAsset = EnsureAaAsset(AttackAssetPath, attackClip, loopingPose: false);
            MeleeWeapon prefab = EnsureKnifePrefab(holdAsset, attackAsset, null);
            WirePlayer(prefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Knife Setup",
                "Готово (Humanoid demo clips + CombatKnife mesh).\n\n" +
                "• AA_Knife_Hold_Humanoid\n" +
                "• AA_Knife_Attack_Humanoid\n" +
                "• Melee_Knife → слот 4 (клавиша 5)\n\n" +
                "Для FP_CombatKnife: Shooter → Project → Retarget CombatKnife.\n\n" +
                "Play → 5 → поза ножа → ЛКМ удар.",
                "OK");
        }

        // Keep old menu name as alias so existing docs still work.
        [MenuItem("Shooter/Project/Setup Melee Knife (Mixamo Stabbing)")]
        public static void SetupMeleeKnifeLegacyAlias() => SetupMeleeKnife();

        static void ConfigureHumanoidImport(string fbxPath)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
                return;

            ModelImporter characterImporter =
                AssetImporter.GetAtPath(CharacterModelPath) as ModelImporter;

            importer.animationType = ModelImporterAnimationType.Human;
            if (characterImporter != null && characterImporter.sourceAvatar != null)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = characterImporter.sourceAvatar;
            }

            importer.importAnimation = true;
            importer.SaveAndReimport();
        }

        static AnimationClip LoadFirstClip(string fbxPath)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            AnimationClip best = null;
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is not AnimationClip c || c.name.StartsWith("__preview"))
                    continue;
                best = c;
                break;
            }

            return best;
        }

        static FPSAnimationAsset EnsureAaAsset(string path, AnimationClip clip, bool loopingPose)
        {
            var existing = AssetDatabase.LoadAssetAtPath<FPSAnimationAsset>(path);
            FPSAnimationAsset asset = existing;
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<FPSAnimationAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.rigAsset = AssetDatabase.LoadAssetAtPath<KRig>(RigPath);
            asset.clip = clip;
            asset.mask = null;
            asset.isAdditive = false;
            asset.blendTime = loopingPose
                ? new BlendTime(0.15f, 0.15f) { rateScale = 1f }
                : new BlendTime(0.08f, 0.12f) { rateScale = 1.1f };
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static MeleeWeapon EnsureKnifePrefab(
            FPSAnimationAsset holdAsset,
            FPSAnimationAsset attackAsset,
            FPSAnimationAsset attackAltAsset)
        {
            Directory.CreateDirectory("Assets/_Project/Weapons/Prefabs");

            if (AssetDatabase.LoadAssetAtPath<MeleeWeapon>(PrefabPath) != null)
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
                try
                {
                    var melee = contents.GetComponent<MeleeWeapon>();
                    if (melee == null)
                        melee = contents.AddComponent<MeleeWeapon>();

                    ApplyMeleeFields(melee, holdAsset, attackAsset, attackAltAsset);
                    EnsureKnifeMesh(contents);
                    melee.ApplyAttachTransform();
                    PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }

                return AssetDatabase.LoadAssetAtPath<MeleeWeapon>(PrefabPath);
            }

            GameObject root = new GameObject("Melee_Knife");
            var created = root.AddComponent<MeleeWeapon>();
            ApplyMeleeFields(created, holdAsset, attackAsset, attackAltAsset);
            EnsureKnifeMesh(root);
            created.ApplyAttachTransform();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<MeleeWeapon>(PrefabPath);
        }

        static void EnsureKnifeMesh(GameObject root)
        {
            if (root == null)
                return;

            Transform existingMesh = root.transform.Find("CombatKnife_Mesh");
            if (existingMesh == null)
                existingMesh = root.transform.Find("FP_CombatKnife");
            if (existingMesh != null)
            {
                StripArmRenderers(existingMesh.gameObject);
                WeaponPrefabUtility.StripPhysicsComponents(existingMesh.gameObject);
                return;
            }

            string meshPath = ResolveKnifeMeshPath();
            GameObject meshSource = AssetDatabase.LoadAssetAtPath<GameObject>(meshPath);
            if (meshSource == null)
            {
                Debug.LogError("[Shooter] Knife mesh not found at " + meshPath);
                return;
            }

            GameObject mesh = (GameObject)PrefabUtility.InstantiatePrefab(meshSource, root.transform);
            if (mesh == null)
                return;

            mesh.name = meshPath.Contains("Pickup_") ? "CombatKnife_Mesh" : "FP_CombatKnife";
            mesh.transform.localPosition = Vector3.zero;
            mesh.transform.localRotation = Quaternion.identity;
            mesh.transform.localScale = Vector3.one;
            StripArmRenderers(mesh);
            WeaponPrefabUtility.StripPhysicsComponents(mesh);
        }

        static string ResolveKnifeMeshPath()
        {
            if (File.Exists(KnifeMeshPath))
                return KnifeMeshPath;
            if (File.Exists(KnifeMeshFallback))
                return KnifeMeshFallback;
            return KnifeMeshFallbackAlt;
        }

        /// <summary>FP_CombatKnife includes arm SMRs — hide them on the held weapon mesh.</summary>
        static void StripArmRenderers(GameObject root)
        {
            if (root == null)
                return;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null)
                    continue;
                string n = r.gameObject.name.ToLowerInvariant();
                if (n.Contains("arm_standard"))
                    r.enabled = false;
            }
        }

        static void ApplyMeleeFields(
            MeleeWeapon melee,
            FPSAnimationAsset holdAsset,
            FPSAnimationAsset attackAsset,
            FPSAnimationAsset attackAltAsset)
        {
            var so = new SerializedObject(melee);
            so.FindProperty("weaponId").stringValue = "Knife";
            so.FindProperty("slotIndex").intValue = 3;
            so.FindProperty("attachLocalPosition").vector3Value = KnifeAttachPos;
            so.FindProperty("attachLocalEulerAngles").vector3Value = KnifeAttachEuler;
            so.FindProperty("holdOverlayPose").objectReferenceValue = holdAsset;
            so.FindProperty("attackClip").objectReferenceValue = attackAsset;
            if (attackAltAsset != null)
                so.FindProperty("attackClipAlt").objectReferenceValue = attackAltAsset;
            so.FindProperty("equipMotion").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<IkMotionLayerSettings>(EquipMotion);
            so.FindProperty("unEquipMotion").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<IkMotionLayerSettings>(UnequipMotion);
            so.FindProperty("damage").floatValue = 35f;
            so.FindProperty("range").floatValue = 1.8f;
            so.FindProperty("attackCooldown").floatValue = 0.65f;
            so.FindProperty("hitDelay").floatValue = 0.2f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WirePlayer(MeleeWeapon knifePrefab)
        {
            if (knifePrefab == null)
                return;

            GameObject playerRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Transform model = playerRoot.transform.Find("Graphics/Character_model");
                Transform weaponBone = null;
                if (model != null)
                {
                    weaponBone = FindDeep(model, WeaponPrefabUtility.IkWeaponBoneName);
                    if (weaponBone == null)
                    {
                        var rig = model.GetComponentInChildren<KRigComponent>(true);
                        if (rig != null)
                            weaponBone = rig.GetRigTransform(FPSANames.IkWeaponBone);
                    }
                }

                if (weaponBone == null)
                {
                    Debug.LogError("[Shooter] IK WeaponBone not found on PlayerCharacter.");
                    return;
                }

                WeaponBase knife = EnsureEmbedded(weaponBone, knifePrefab, 3);

                var manager = playerRoot.GetComponent<WeaponManager>();
                if (manager != null)
                {
                    var managerSo = new SerializedObject(manager);
                    SerializedProperty slots = managerSo.FindProperty("weaponSlots");
                    if (slots.arraySize < 5)
                        slots.arraySize = 5;
                    slots.GetArrayElementAtIndex(3).objectReferenceValue = knife;
                    managerSo.ApplyModifiedPropertiesWithoutUndo();
                }

                var inventory = playerRoot.GetComponent<ShooterPlayerInventory>();
                if (inventory != null)
                {
                    var invSo = new SerializedObject(inventory);
                    invSo.FindProperty("hasGun4").boolValue = true;
                    invSo.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(playerRoot, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        static WeaponBase EnsureEmbedded(Transform weaponBone, MeleeWeapon prefabAsset, int slotIndex)
        {
            WeaponBase[] existing = weaponBone.GetComponentsInChildren<WeaponBase>(true);
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i].SlotIndex != slotIndex)
                    continue;

                // Nested instance without mesh → replace from updated prefab.
                bool hasRenderer = existing[i].GetComponentInChildren<Renderer>(true) != null;
                if (!hasRenderer)
                {
                    Object.DestroyImmediate(existing[i].gameObject);
                    break;
                }

                var so = new SerializedObject(existing[i]);
                so.FindProperty("slotIndex").intValue = slotIndex;
                if (existing[i] is MeleeWeapon)
                {
                    var prefabSo = new SerializedObject(prefabAsset);
                    so.FindProperty("holdOverlayPose").objectReferenceValue =
                        prefabSo.FindProperty("holdOverlayPose").objectReferenceValue;
                    so.FindProperty("attackClip").objectReferenceValue =
                        prefabSo.FindProperty("attackClip").objectReferenceValue;
                    so.FindProperty("attackClipAlt").objectReferenceValue =
                        prefabSo.FindProperty("attackClipAlt").objectReferenceValue;
                }

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

        static Transform FindDeep(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
                return null;
            if (root.name == name)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
#endif
