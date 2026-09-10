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
    /// Humanoid knife hold/attack (demo clips) + CombatKnife mesh → slot 4 (key 5).
    /// FP_CombatKnife Generic clips need Retarget Pro bake — see Docs TASKS 2.10.
    /// </summary>
    public static class ShooterKnifeSetup
    {
        const string CharacterModelPath = "Assets/_Project/Packages/Models/Character_model.fbx";
        const string RigPath = "Assets/_Project/FPS/Rig_CharacterModel.asset";
        const string UpperBodyMaskPath = "Assets/Demo/Animations/Masks/UpperBody_Humanoid.mask";
        const string HoldClipFbx = "Assets/Demo/Animations/Locomotion/Humanoid/Knife/C_Knife_Static_Humanoid.fbx";
        const string AttackClipFbx = "Assets/Demo/Animations/Locomotion/Humanoid/Knife/C_Stabbing_Humanoid.fbx";
        const string HoldAssetPath = "Assets/_Project/FPS/AA_Knife_Hold_Humanoid.asset";
        const string AttackAssetPath = "Assets/_Project/FPS/AA_Knife_Attack_Humanoid.asset";
        const string KnifeMeshPath = "Assets/_Project/Packages/CombatKnife/FP_CombatKnife.fbx";
        const string KnifeMeshFallback = "Assets/_Project/Packages/Melee/CombatKnife/FP_CombatKnife.fbx";
        const string PrefabPath = "Assets/_Project/Weapons/Prefabs/Melee_Knife.prefab";
        const string PlayerPrefabPath = "Assets/_Project/Prefabs/PlayerCharacter.prefab";
        const string EquipMotion = "Assets/Demo/AnimatorProfiles/IKMotions/IKMotion_Equip.asset";
        const string UnequipMotion = "Assets/Demo/AnimatorProfiles/IKMotions/IKMotion_UnEquip.asset";

        static readonly Vector3 KnifeAttachPos = new Vector3(-0.02f, 0.03f, -0.08f);
        static readonly Vector3 KnifeAttachEuler = new Vector3(10f, 170f, 0f);

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
            MeleeWeapon prefab = EnsureKnifePrefab(holdAsset, attackAsset);
            WirePlayer(prefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Knife Setup",
                "Готово (Humanoid demo clips + CombatKnife mesh).\n\n" +
                "• AA_Knife_Hold_Humanoid\n" +
                "• AA_Knife_Attack_Humanoid\n" +
                "• Melee_Knife → слот 4 (клавиша 5)\n\n" +
                "FP_CombatKnife Stab1/Stab2 — через Retarget Pro (меню KINEMATION),\n" +
                "потом подставь baked клипы в hold/attack на префабе.\n\n" +
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
            asset.mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
            asset.isAdditive = false;
            asset.blendTime = loopingPose
                ? new BlendTime(0.15f, 0.15f) { rateScale = 1f }
                : new BlendTime(0.08f, 0.12f) { rateScale = 1.1f };
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static MeleeWeapon EnsureKnifePrefab(FPSAnimationAsset holdAsset, FPSAnimationAsset attackAsset)
        {
            Directory.CreateDirectory("Assets/_Project/Weapons/Prefabs");

            MeleeWeapon existing = AssetDatabase.LoadAssetAtPath<MeleeWeapon>(PrefabPath);
            if (existing != null)
            {
                ApplyMeleeFields(existing, holdAsset, attackAsset);
                existing.ApplyAttachTransform();
                EditorUtility.SetDirty(existing);
                return existing;
            }

            GameObject root = new GameObject("Melee_Knife");
            var melee = root.AddComponent<MeleeWeapon>();
            ApplyMeleeFields(melee, holdAsset, attackAsset);

            string meshPath = File.Exists(KnifeMeshPath) ? KnifeMeshPath : KnifeMeshFallback;
            GameObject meshSource = AssetDatabase.LoadAssetAtPath<GameObject>(meshPath);
            if (meshSource != null)
            {
                GameObject mesh = (GameObject)PrefabUtility.InstantiatePrefab(meshSource, root.transform);
                if (mesh != null)
                {
                    mesh.name = "FP_CombatKnife";
                    mesh.transform.localPosition = Vector3.zero;
                    mesh.transform.localRotation = Quaternion.identity;
                    mesh.transform.localScale = Vector3.one;
                    WeaponPrefabUtility.StripPhysicsComponents(mesh);
                }
            }

            melee.ApplyAttachTransform();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<MeleeWeapon>(PrefabPath);
        }

        static void ApplyMeleeFields(MeleeWeapon melee, FPSAnimationAsset holdAsset, FPSAnimationAsset attackAsset)
        {
            var so = new SerializedObject(melee);
            so.FindProperty("weaponId").stringValue = "Knife";
            so.FindProperty("slotIndex").intValue = 3;
            so.FindProperty("attachLocalPosition").vector3Value = KnifeAttachPos;
            so.FindProperty("attachLocalEulerAngles").vector3Value = KnifeAttachEuler;
            so.FindProperty("holdOverlayPose").objectReferenceValue = holdAsset;
            so.FindProperty("attackClip").objectReferenceValue = attackAsset;
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

                var so = new SerializedObject(existing[i]);
                so.FindProperty("slotIndex").intValue = slotIndex;
                if (existing[i] is MeleeWeapon)
                {
                    so.FindProperty("holdOverlayPose").objectReferenceValue =
                        new SerializedObject(prefabAsset).FindProperty("holdOverlayPose").objectReferenceValue;
                    so.FindProperty("attackClip").objectReferenceValue =
                        new SerializedObject(prefabAsset).FindProperty("attackClip").objectReferenceValue;
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
