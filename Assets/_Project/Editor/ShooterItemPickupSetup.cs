#if UNITY_EDITOR
using System.IO;
using KINEMATION.FPSAnimationFramework.Runtime.Playables;
using KINEMATION.Shared.KAnimationCore.Runtime.Rig;
using Shooter.Project.Character;
using UnityEditor;
using UnityEngine;

namespace Shooter.Project.Editor
{
    /// <summary>
    /// FreeSample ItemPickup RH clip → unarmed upper-body AA + mirrored left-arm AA for armed loot.
    /// </summary>
    public static class ShooterItemPickupSetup
    {
        const string SourceFbx =
            "Assets/VanillaLoopStudio/FreeSampleAnimationSet/Art/Animations/ItemPickupSet/Mannequin/A_ItemPickup_fromIdle_RH_100cm.fbx";
        const string CharacterModelPath = "Assets/_Project/Packages/Models/Character_model.fbx";
        const string RigPath = "Assets/_Project/FPS/Rig_CharacterModel.asset";
        const string UpperBodyMaskPath = "Assets/Demo/Animations/Masks/UpperBody_Humanoid.mask";
        const string OutFolder = "Assets/_Project/Animations/Loot";
        const string LeftArmMaskPath = OutFolder + "/LeftArm_Humanoid.mask";
        const string MirroredClipPath = OutFolder + "/A_ItemPickup_fromIdle_LH_Mirrored.anim";
        const string UnarmedAaPath = "Assets/_Project/FPS/AA_ItemPickup_Unarmed.asset";
        const string ArmedAaPath = "Assets/_Project/FPS/AA_ItemPickup_ArmedLeft.asset";
        const string PlayerPrefabPath = "Assets/_Project/Prefabs/PlayerCharacter.prefab";

        [MenuItem("Shooter/Project/Setup Item Pickup Animation (2.9)")]
        public static void Setup()
        {
            Directory.CreateDirectory(OutFolder);
            ConfigureHumanoidImport(SourceFbx);

            AnimationClip sourceClip = LoadFirstClip(SourceFbx);
            if (sourceClip == null)
            {
                EditorUtility.DisplayDialog(
                    "Item Pickup",
                    "Не найден клип в:\n" + SourceFbx,
                    "OK");
                return;
            }

            AvatarMask upperMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
            AvatarMask leftMask = EnsureLeftArmMask();
            AnimationClip mirrored = EnsureMirroredClip(sourceClip);

            FPSAnimationAsset unarmed = EnsureAa(
                UnarmedAaPath, sourceClip, upperMask, useControllerMask: true);
            FPSAnimationAsset armed = EnsureAa(
                ArmedAaPath, mirrored, leftMask, useControllerMask: false);

            WirePlayer(unarmed, armed);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Item Pickup",
                "Готово.\n\n" +
                "• AA_ItemPickup_Unarmed — upper body (RH clip)\n" +
                "• AA_ItemPickup_ArmedLeft — left arm (mirrored)\n" +
                "• ShooterItemPickupAnimation на PlayerCharacter\n\n" +
                "API: PlayItemPickupAnimation()\n" +
                "Play → Context Menu на компоненте для теста.",
                "OK");
        }

        static void ConfigureHumanoidImport(string fbxPath)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
                return;

            Avatar targetAvatar = null;
            Object[] characterAssets = AssetDatabase.LoadAllAssetsAtPath(CharacterModelPath);
            for (int i = 0; i < characterAssets.Length; i++)
            {
                if (characterAssets[i] is Avatar av && av.isHuman)
                {
                    targetAvatar = av;
                    break;
                }
            }

            importer.animationType = ModelImporterAnimationType.Human;
            if (targetAvatar != null)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = targetAvatar;
            }

            importer.importAnimation = true;
            importer.SaveAndReimport();
        }

        static AnimationClip LoadFirstClip(string fbxPath)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is AnimationClip c && !c.name.StartsWith("__preview"))
                    return c;
            }

            return null;
        }

        static AvatarMask EnsureLeftArmMask()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AvatarMask>(LeftArmMaskPath);
            if (existing != null)
                return existing;

            var mask = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);

            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);

            AssetDatabase.CreateAsset(mask, LeftArmMaskPath);
            return mask;
        }

        static AnimationClip EnsureMirroredClip(AnimationClip source)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(MirroredClipPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(source, existing);
                ApplyMirror(existing);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var clip = new AnimationClip { name = "A_ItemPickup_fromIdle_LH_Mirrored" };
            EditorUtility.CopySerialized(source, clip);
            clip.name = "A_ItemPickup_fromIdle_LH_Mirrored";
            ApplyMirror(clip);
            AssetDatabase.CreateAsset(clip, MirroredClipPath);
            return clip;
        }

        static void ApplyMirror(AnimationClip clip)
        {
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.mirror = true;
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        static FPSAnimationAsset EnsureAa(
            string path, AnimationClip clip, AvatarMask mask, bool useControllerMask)
        {
            var asset = AssetDatabase.LoadAssetAtPath<FPSAnimationAsset>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<FPSAnimationAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.rigAsset = AssetDatabase.LoadAssetAtPath<KRig>(RigPath);
            asset.clip = clip;
            // null = FPSPlayablesController.upperBodyMask (same path as rifle equip).
            asset.mask = useControllerMask ? null : mask;
            asset.isAdditive = false;
            asset.blendTime = new BlendTime(0.12f, 0.2f) { rateScale = 1f };
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static void WirePlayer(FPSAnimationAsset unarmed, FPSAnimationAsset armed)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var pickup = root.GetComponent<ShooterItemPickupAnimation>();
                if (pickup == null)
                    pickup = root.AddComponent<ShooterItemPickupAnimation>();

                var so = new SerializedObject(pickup);
                so.FindProperty("unarmedPickup").objectReferenceValue = unarmed;
                so.FindProperty("armedLeftHandPickup").objectReferenceValue = armed;
                so.FindProperty("blockIfWeaponBusy").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
#endif
