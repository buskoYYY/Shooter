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
    /// Wires Mixamo Stabbing.fbx → AA_Knife_Attack → Melee_Knife prefab → player slot 4 (key 5).
    /// </summary>
    public static class ShooterKnifeSetup
    {
        const string StabbingPath = "Assets/_Project/Animations/Stabbing.fbx";
        const string CharacterModelPath = "Assets/_Project/Packages/Models/Character_model.fbx";
        const string RigPath = "Assets/_Project/FPS/Rig_CharacterModel.asset";
        const string UpperBodyMaskPath = "Assets/Demo/Animations/Masks/UpperBody_Humanoid.mask";
        const string AttackAssetPath = "Assets/_Project/FPS/AA_Knife_Attack_Humanoid.asset";
        const string KnifeMeshPath = "Assets/_Project/Packages/Melee/CombatKnife/FP_CombatKnife.fbx";
        const string PrefabPath = "Assets/_Project/Weapons/Prefabs/Melee_Knife.prefab";
        const string PlayerPrefabPath = "Assets/_Project/Prefabs/PlayerCharacter.prefab";
        const string EquipMotion = "Assets/Demo/AnimatorProfiles/IKMotions/IKMotion_Equip.asset";
        const string UnequipMotion = "Assets/Demo/AnimatorProfiles/IKMotions/IKMotion_UnEquip.asset";

        static readonly Vector3 KnifeAttachPos = new Vector3(-0.02f, 0.03f, -0.08f);
        static readonly Vector3 KnifeAttachEuler = new Vector3(10f, 170f, 0f);

        [MenuItem("Shooter/Project/Setup Melee Knife (Mixamo Stabbing)")]
        public static void SetupMeleeKnife()
        {
            if (!File.Exists(StabbingPath))
            {
                EditorUtility.DisplayDialog(
                    "Knife Setup",
                    "Не найден Assets/_Project/Animations/Stabbing.fbx",
                    "OK");
                return;
            }

            AnimationClip clip = ConfigureStabbingImport();
            if (clip == null)
            {
                EditorUtility.DisplayDialog(
                    "Knife Setup",
                    "Не удалось достать AnimationClip из Stabbing.fbx.\n" +
                    "В Inspector у FBX: Rig = Humanoid, Avatar = Copy From Character_model, Apply.",
                    "OK");
                return;
            }

            FPSAnimationAsset attackAsset = EnsureAttackAsset(clip);
            MeleeWeapon prefab = EnsureKnifePrefab(attackAsset);
            WirePlayer(prefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Knife Setup",
                "Готово.\n• AA_Knife_Attack_Humanoid\n• Melee_Knife prefab\n• Слот 4 (клавиша 5)\n\nPlay → 5 → ЛКМ = удар.",
                "OK");
        }

        static AnimationClip ConfigureStabbingImport()
        {
            var importer = AssetImporter.GetAtPath(StabbingPath) as ModelImporter;
            if (importer == null)
                return null;

            ModelImporter characterImporter =
                AssetImporter.GetAtPath(CharacterModelPath) as ModelImporter;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            if (characterImporter != null && characterImporter.sourceAvatar != null)
                importer.sourceAvatar = characterImporter.sourceAvatar;
            else
            {
                // Fallback: Create From This Model
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            }

            importer.importAnimation = true;

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0)
                clips = importer.clipAnimations;

            if (clips != null && clips.Length > 0)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    clips[i].name = "Stabbing";
                    clips[i].loopTime = false;
                    clips[i].lockRootRotation = true;
                    clips[i].lockRootHeightY = true;
                    clips[i].lockRootPositionXZ = true;
                    clips[i].keepOriginalOrientation = true;
                    clips[i].keepOriginalPositionY = true;
                    clips[i].keepOriginalPositionXZ = true;
                }

                importer.clipAnimations = clips;
            }

            importer.SaveAndReimport();

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(StabbingPath);
            AnimationClip best = null;
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is AnimationClip c && !c.name.StartsWith("__preview"))
                {
                    best = c;
                    if (c.name == "Stabbing")
                        return c;
                }
            }

            return best;
        }

        static FPSAnimationAsset EnsureAttackAsset(AnimationClip clip)
        {
            var existing = AssetDatabase.LoadAssetAtPath<FPSAnimationAsset>(AttackAssetPath);
            FPSAnimationAsset asset = existing;
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<FPSAnimationAsset>();
                AssetDatabase.CreateAsset(asset, AttackAssetPath);
            }

            asset.rigAsset = AssetDatabase.LoadAssetAtPath<KRig>(RigPath);
            asset.clip = clip;
            asset.mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
            asset.isAdditive = false;
            asset.blendTime = new BlendTime(0.08f, 0.12f)
            {
                rateScale = 1.15f
            };
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static MeleeWeapon EnsureKnifePrefab(FPSAnimationAsset attackAsset)
        {
            Directory.CreateDirectory("Assets/_Project/Weapons/Prefabs");

            MeleeWeapon existing = AssetDatabase.LoadAssetAtPath<MeleeWeapon>(PrefabPath);
            if (existing != null)
            {
                var so = new SerializedObject(existing);
                so.FindProperty("attackClip").objectReferenceValue = attackAsset;
                so.FindProperty("equipMotion").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<IkMotionLayerSettings>(EquipMotion);
                so.FindProperty("unEquipMotion").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<IkMotionLayerSettings>(UnequipMotion);
                so.FindProperty("slotIndex").intValue = 3;
                so.FindProperty("weaponId").stringValue = "Knife";
                so.FindProperty("attachLocalPosition").vector3Value = KnifeAttachPos;
                so.FindProperty("attachLocalEulerAngles").vector3Value = KnifeAttachEuler;
                so.ApplyModifiedPropertiesWithoutUndo();
                existing.ApplyAttachTransform();
                EditorUtility.SetDirty(existing);
                return existing;
            }

            GameObject root = new GameObject("Melee_Knife");
            var melee = root.AddComponent<MeleeWeapon>();
            var soNew = new SerializedObject(melee);
            soNew.FindProperty("weaponId").stringValue = "Knife";
            soNew.FindProperty("slotIndex").intValue = 3;
            soNew.FindProperty("attachLocalPosition").vector3Value = KnifeAttachPos;
            soNew.FindProperty("attachLocalEulerAngles").vector3Value = KnifeAttachEuler;
            soNew.FindProperty("attackClip").objectReferenceValue = attackAsset;
            soNew.FindProperty("equipMotion").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<IkMotionLayerSettings>(EquipMotion);
            soNew.FindProperty("unEquipMotion").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<IkMotionLayerSettings>(UnequipMotion);
            soNew.FindProperty("damage").floatValue = 35f;
            soNew.FindProperty("range").floatValue = 1.8f;
            soNew.FindProperty("attackCooldown").floatValue = 0.65f;
            soNew.FindProperty("hitDelay").floatValue = 0.2f;
            soNew.ApplyModifiedPropertiesWithoutUndo();

            GameObject meshSource = AssetDatabase.LoadAssetAtPath<GameObject>(KnifeMeshPath);
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
                    // Prefer name lookup — GetRigTransform(KRigElement) throws if hierarchy map
                    // is empty / missing the bone while editing prefab contents.
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
                    so.FindProperty("attackClip").objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<FPSAnimationAsset>(AttackAssetPath);
                so.ApplyModifiedPropertiesWithoutUndo();
                return existing[i];
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset.gameObject, weaponBone);
            instance.name = prefabAsset.name;
            WeaponBase weapon = instance.GetComponent<WeaponBase>();
            var soNew = new SerializedObject(weapon);
            soNew.FindProperty("slotIndex").intValue = slotIndex;
            soNew.ApplyModifiedPropertiesWithoutUndo();
            weapon.ApplyAttachTransform();
            return weapon;
        }

        static Transform FindDeep(Transform root, string name)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
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
