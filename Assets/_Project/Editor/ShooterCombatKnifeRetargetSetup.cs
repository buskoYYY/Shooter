#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using KINEMATION.FPSAnimationFramework.Runtime.Playables;
using KINEMATION.RetargetPro.Editor.Scripts.Bakers;
using KINEMATION.RetargetPro.Editor.Scripts.Mapping;
using KINEMATION.RetargetPro.Editor.Scripts.Window;
using KINEMATION.RetargetPro.Runtime;
using KINEMATION.RetargetPro.Runtime.Features;
using KINEMATION.RetargetPro.Runtime.Features.FPSRetargeting;
using KINEMATION.Shared.KAnimationCore.Runtime.Rig;
using Shooter.Project.Weapons;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Project.Editor
{
    /// <summary>
    /// FP_CombatKnife (Generic) → Character_model (Humanoid) via Retarget Pro:
    /// profile + FPS feature → bake Hold/Stab1/Stab2 → wire AA + Melee_Knife.
    /// </summary>
    public static class ShooterCombatKnifeRetargetSetup
    {
        const string SourceFbx = "Assets/_Project/Packages/CombatKnife/FP_CombatKnife.fbx";
        const string TargetFbx = "Assets/_Project/Packages/Models/Character_model.fbx";
        const string KnifeFolder = "Assets/_Project/Animations/Knife";
        const string ProfilePath = KnifeFolder + "/Retarget_FP_CombatKnife_Character_model.asset";
        const string HoldAaPath = "Assets/_Project/FPS/AA_Knife_Hold_Humanoid.asset";
        const string AttackAaPath = "Assets/_Project/FPS/AA_Knife_Attack_Humanoid.asset";
        const string AttackAltAaPath = "Assets/_Project/FPS/AA_Knife_AttackAlt_Humanoid.asset";
        const string PrefabPath = "Assets/_Project/Weapons/Prefabs/Melee_Knife.prefab";
        const string RigPath = "Assets/_Project/FPS/Rig_CharacterModel.asset";

        static readonly string[] BakeClipNames =
        {
            "CombatKnife_Hold",
            "CombatKnife_Stab1",
            "CombatKnife_Stab2"
        };

        [MenuItem("Shooter/Project/Retarget CombatKnife (FP → Character_model)")]
        public static void RetargetAndWire()
        {
            Directory.CreateDirectory(KnifeFolder);

            GameObject sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(SourceFbx);
            GameObject targetModel = AssetDatabase.LoadAssetAtPath<GameObject>(TargetFbx);
            if (sourceModel == null || targetModel == null)
            {
                EditorUtility.DisplayDialog(
                    "CombatKnife Retarget",
                    "Не найдены FBX:\n" + SourceFbx + "\n" + TargetFbx,
                    "OK");
                return;
            }

            RetargetProfile profile = EnsureProfile(sourceModel, targetModel, out string profileMsg);
            if (profile == null)
            {
                EditorUtility.DisplayDialog("CombatKnife Retarget", profileMsg, "OK");
                return;
            }

            FPSRetargetFeature fps = EnsureFpsFeature(profile, out string fpsMsg);
            bool fpsOk = fps != null && fps.GetStatus();

            AnimationClip holdSrc = FindClip(SourceFbx, "CombatKnife_Hold");
            AnimationClip stab1Src = FindClip(SourceFbx, "CombatKnife_Stab1");
            AnimationClip stab2Src = FindClip(SourceFbx, "CombatKnife_Stab2");
            if (holdSrc == null || stab1Src == null || stab2Src == null)
            {
                EditorUtility.DisplayDialog(
                    "CombatKnife Retarget",
                    "В FP_CombatKnife.fbx нет клипов Hold/Stab1/Stab2.",
                    "OK");
                RetargetProWindow.ShowWindow(profile);
                return;
            }

            if (!fpsOk)
            {
                RetargetProWindow.ShowWindow(profile);
                EditorUtility.DisplayDialog(
                    "CombatKnife Retarget",
                    "Профиль создан, но FPS Retarget Feature не полностью замаплен " +
                    "(нужны руки + weapon bone на source/target).\n\n" +
                    fpsMsg + "\n\n" +
                    "В окне Retarget Pro нажми Refresh / назначь Weapon вручную, " +
                    "затем снова запусти этот пункт меню для Bake.",
                    "OK");
                return;
            }

            Dictionary<string, AnimationClip> baked = BakeClips(profile, holdSrc, stab1Src, stab2Src, out string bakeError);
            if (baked == null || baked.Count == 0)
            {
                RetargetProWindow.ShowWindow(profile);
                EditorUtility.DisplayDialog(
                    "CombatKnife Retarget",
                    "Bake не удался:\n" + bakeError +
                    "\n\nОткрой Window → KINEMATION → Retarget Pro и bake вручную.",
                    "OK");
                return;
            }

            AnimationClip holdBaked = null;
            AnimationClip stab1Baked = null;
            AnimationClip stab2Baked = null;
            baked.TryGetValue("CombatKnife_Hold", out holdBaked);
            baked.TryGetValue("CombatKnife_Stab1", out stab1Baked);
            baked.TryGetValue("CombatKnife_Stab2", out stab2Baked);

            FPSAnimationAsset holdAa = EnsureAa(HoldAaPath, holdBaked, loopingPose: true);
            FPSAnimationAsset attackAa = EnsureAa(AttackAaPath, stab1Baked, loopingPose: false);
            FPSAnimationAsset attackAltAa = EnsureAa(AttackAltAaPath, stab2Baked, loopingPose: false);
            WireMeleePrefab(holdAa, attackAa, attackAltAa);

            // Keep player prefab in sync via existing knife setup wiring.
            ShooterKnifeSetup.SetupMeleeKnifeFromBaked(holdAa, attackAa, attackAltAa);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = profile;
            RetargetProWindow.ShowWindow(profile);

            EditorUtility.DisplayDialog(
                "CombatKnife Retarget",
                "Готово.\n\n" +
                "• Profile: " + ProfilePath + "\n" +
                "• Baked → " + KnifeFolder + "\n" +
                "• AA_Knife_Hold / Attack / AttackAlt\n" +
                "• Melee_Knife + Player слот 4 (клавиша 5)\n\n" +
                "Play → 5 → поза ножа → ЛКМ (Stab1/Stab2 чередуются).",
                "OK");
        }

        static RetargetProfile EnsureProfile(GameObject sourceModel, GameObject targetModel, out string message)
        {
            message = string.Empty;
            RetargetProfile existing = AssetDatabase.LoadAssetAtPath<RetargetProfile>(ProfilePath);
            if (existing != null)
            {
                existing.sourceCharacter = sourceModel;
                existing.targetCharacter = targetModel;
                existing.saveFolderPath = KnifeFolder;
                if (existing.sourceRig == null || existing.targetRig == null)
                {
                    if (!RetargetProfileModelRigUtility.TryComposeProfileRigs(existing, true, out string composeMsg))
                    {
                        message = "Не удалось собрать rigs: " + composeMsg;
                        return null;
                    }
                }

                EditorUtility.SetDirty(existing);
                return existing;
            }

            if (!RetargetProfileModelRigUtility.TryBuildRigFromModel(sourceModel, null,
                    $"{sourceModel.name}_Rig", out KRig sourceRig, out string sourceErr))
            {
                message = "Source rig: " + sourceErr;
                return null;
            }

            if (!RetargetProfileModelRigUtility.TryBuildRigFromModel(targetModel, null,
                    $"{targetModel.name}_Rig", out KRig targetRig, out string targetErr))
            {
                Object.DestroyImmediate(sourceRig);
                message = "Target rig: " + targetErr;
                return null;
            }

            RetargetProfile profile = ScriptableObject.CreateInstance<RetargetProfile>();
            profile.sourceCharacter = sourceModel;
            profile.targetCharacter = targetModel;
            profile.sourceRig = sourceRig;
            profile.targetRig = targetRig;
            profile.retargetFeatures = new List<RetargetFeature>();
            profile.saveFolderPath = KnifeFolder;

            AssetDatabase.CreateAsset(profile, ProfilePath);
            AssetDatabase.AddObjectToAsset(sourceRig, profile);
            AssetDatabase.AddObjectToAsset(targetRig, profile);
            EditorUtility.SetDirty(sourceRig);
            EditorUtility.SetDirty(targetRig);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        static FPSRetargetFeature EnsureFpsFeature(RetargetProfile profile, out string message)
        {
            message = string.Empty;
            FPSRetargetFeature fps = null;
            if (profile.retargetFeatures != null)
            {
                for (int i = 0; i < profile.retargetFeatures.Count; i++)
                {
                    if (profile.retargetFeatures[i] is FPSRetargetFeature found)
                    {
                        fps = found;
                        break;
                    }
                }
            }

            if (fps == null)
            {
                // Drop auto Basic/IK features — FPS arms need FPS Retarget only.
                ClearFeatures(profile);
                fps = ScriptableObject.CreateInstance<FPSRetargetFeature>();
                fps.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                fps.name = nameof(FPSRetargetFeature);
                fps.sourceRig = profile.sourceRig;
                fps.targetRig = profile.targetRig;
                AssetDatabase.AddObjectToAsset(fps, profile);
                profile.retargetFeatures = new List<RetargetFeature> { fps };
                fps.OnFeatureAdded();
            }
            else
            {
                fps.sourceRig = profile.sourceRig;
                fps.targetRig = profile.targetRig;
                fps.MapChains();
            }

            TryFixFpsChains(fps);
            EditorUtility.SetDirty(fps);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(fps);
            AssetDatabase.SaveAssetIfDirty(profile);

            if (!fps.GetStatus())
            {
                message = DescribeMissingChains(fps) + "\n" + fps.GetErrorMessage();
            }

            return fps;
        }

        static string DescribeMissingChains(FPSRetargetFeature fps)
        {
            var missing = new List<string>();
            if (!IsChainValid(fps.sourceRightArm)) missing.Add("sourceRightArm");
            if (!IsChainValid(fps.sourceLeftArm)) missing.Add("sourceLeftArm");
            if (!IsChainValid(fps.sourceWeapon)) missing.Add("sourceWeapon");
            if (!IsChainValid(fps.targetRightArm)) missing.Add("targetRightArm");
            if (!IsChainValid(fps.targetLeftArm)) missing.Add("targetLeftArm");
            if (!IsChainValid(fps.targetWeapon)) missing.Add("targetWeapon");
            return missing.Count == 0 ? string.Empty : "Missing: " + string.Join(", ", missing);
        }

        static void ClearFeatures(RetargetProfile profile)
        {
            if (profile.retargetFeatures == null)
                return;

            for (int i = profile.retargetFeatures.Count - 1; i >= 0; i--)
            {
                RetargetFeature feature = profile.retargetFeatures[i];
                if (feature == null)
                    continue;
                Object.DestroyImmediate(feature, true);
            }

            profile.retargetFeatures.Clear();
        }

        /// <summary>
        /// CombatKnife source rig has empty preset chains — force arms from hierarchy names.
        /// Prefer Weapon / IK WeaponBone sockets over model roots.
        /// </summary>
        static void TryFixFpsChains(FPSRetargetFeature fps)
        {
            if (!IsChainValid(fps.sourceRightArm))
            {
                fps.sourceRightArm = BuildArmByExactNames(fps.sourceRig, "RightArm",
                    new[] { "UpperArm.R", "Forearm.R", "Hand.R" });
            }

            if (!IsChainValid(fps.sourceLeftArm))
            {
                fps.sourceLeftArm = BuildArmByExactNames(fps.sourceRig, "LeftArm",
                    new[] { "UpperArm.L", "Forearm.L", "Hand.L" });
            }

            // Prefer real Weapon bone (not FP_CombatKnife root).
            KRigElement sourceWeapon = FindExactElement(fps.sourceRig, "Weapon");
            if (sourceWeapon.index < 0)
                sourceWeapon = FindBestWeaponElement(fps.sourceRig,
                    new[] { "weapon", "knife", "gun", "blade", "melee" }, skipRoot: true);
            if (sourceWeapon.index >= 0)
            {
                fps.sourceWeapon = new KRigElementChain
                {
                    chainName = sourceWeapon.name,
                    elementChain = new List<KRigElement> { sourceWeapon }
                };
            }

            if (!IsChainValid(fps.targetRightArm))
            {
                fps.targetRightArm = BuildArmByExactNames(fps.targetRig, "RightArm",
                    new[] { "upperarm_r", "lowerarm_r", "hand_r" });
            }

            if (!IsChainValid(fps.targetLeftArm))
            {
                fps.targetLeftArm = BuildArmByExactNames(fps.targetRig, "LeftArm",
                    new[] { "upperarm_l", "lowerarm_l", "hand_l" });
            }

            // Prefer FPS gun socket — never weapon_l / weapon_r prop bones.
            KRigElement targetWeapon = FindExactElement(fps.targetRig, "ik_hand_gun");
            if (targetWeapon.index < 0)
                targetWeapon = FindExactElement(fps.targetRig, "IK WeaponBone");
            if (targetWeapon.index < 0)
                targetWeapon = FindExactElement(fps.targetRig, "WeaponBone");
            if (targetWeapon.index < 0)
                targetWeapon = FindBestWeaponElement(fps.targetRig,
                    new[] { "ik hand gun", "ik_hand_gun", "ik weaponbone", "weaponbone" },
                    skipRoot: true,
                    excludePropSideBones: true);
            if (targetWeapon.index >= 0)
            {
                fps.targetWeapon = new KRigElementChain
                {
                    chainName = targetWeapon.name,
                    elementChain = new List<KRigElement> { targetWeapon }
                };
            }
        }

        static KRigElementChain BuildArmByExactNames(KRig rig, string chainName, string[] boneNames)
        {
            var chain = new KRigElementChain { chainName = chainName };
            if (rig?.rigHierarchy == null || boneNames == null)
                return chain;

            for (int i = 0; i < boneNames.Length; i++)
            {
                KRigElement el = FindExactElement(rig, boneNames[i]);
                if (el.index < 0)
                {
                    chain.elementChain.Clear();
                    return chain;
                }

                chain.elementChain.Add(el);
            }

            return chain;
        }

        static KRigElement FindExactElement(KRig rig, string boneName)
        {
            if (rig?.rigHierarchy == null || string.IsNullOrEmpty(boneName))
                return new KRigElement(-1, string.Empty);

            for (int i = 0; i < rig.rigHierarchy.Count; i++)
            {
                if (string.Equals(rig.rigHierarchy[i].name, boneName, System.StringComparison.OrdinalIgnoreCase))
                    return rig.rigHierarchy[i];
            }

            return new KRigElement(-1, string.Empty);
        }

        static KRigElement FindBestWeaponElement(KRig rig, string[] queries, bool skipRoot,
            bool excludePropSideBones = false)
        {
            KRigElement best = new KRigElement(-1, string.Empty);
            if (rig?.rigHierarchy == null)
                return best;

            int bestScore = 0;
            for (int i = 0; i < rig.rigHierarchy.Count; i++)
            {
                KRigElement el = rig.rigHierarchy[i];
                if (skipRoot && el.depth <= 0)
                    continue;

                string lower = el.name.ToLowerInvariant().Replace('_', ' ').Replace('.', ' ');
                if (excludePropSideBones &&
                    (lower == "weapon l" || lower == "weapon r" || lower == "weapon_l" || lower == "weapon_r"
                     || lower.EndsWith(" weapon l") || lower.EndsWith(" weapon r")))
                    continue;

                // Exact "weapon_l"/"weapon_r" after normalize: "weapon l"
                if (excludePropSideBones && (lower == "weapon l" || lower == "weapon r"))
                    continue;

                int score = 0;
                for (int q = 0; q < queries.Length; q++)
                {
                    string query = queries[q].Replace('_', ' ');
                    if (lower.Contains(query))
                        score = Mathf.Max(score, 10 + (queries.Length - q) * 2);
                }

                if (lower == "weapon")
                    score += 50;
                if (lower == "ik hand gun" || lower.Replace(" ", "") == "ikhandgun")
                    score += 80;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = el;
                }
            }

            return best;
        }

        static bool IsChainValid(KRigElementChain chain)
        {
            return chain != null && chain.elementChain != null && chain.elementChain.Count > 0;
        }

        static Dictionary<string, AnimationClip> BakeClips(
            RetargetProfile profile,
            AnimationClip hold,
            AnimationClip stab1,
            AnimationClip stab2,
            out string error)
        {
            error = string.Empty;
            var result = new Dictionary<string, AnimationClip>();
            var baker = new RetargetAnimBaker();
            baker.SetProfile(profile);

            if (!baker.TryInitializeBaker(out string initError))
            {
                error = initError;
                baker.UnInitializeBaker();
                return null;
            }

            try
            {
                AnimationClip[] sources = { hold, stab1, stab2 };
                for (int i = 0; i < sources.Length; i++)
                {
                    AnimationClip src = sources[i];
                    EditorUtility.DisplayProgressBar(
                        "CombatKnife Retarget",
                        "Baking " + src.name + "…",
                        (float)i / sources.Length);

                    AnimationClip baked = baker.BakeAnimation(src);
                    if (baked == null)
                    {
                        error = "BakeAnimation returned null for " + src.name;
                        return null;
                    }

                    result[BakeClipNames[i]] = baked;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                baker.UnInitializeBaker();
                baker.SetProfile(null, notify: false);
            }

            return result;
        }

        static AnimationClip FindClip(string fbxPath, string clipName)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is AnimationClip c
                    && !c.name.StartsWith("__preview")
                    && c.name == clipName)
                    return c;
            }

            return null;
        }

        static FPSAnimationAsset EnsureAa(string path, AnimationClip clip, bool loopingPose)
        {
            if (clip == null)
                return null;

            var existing = AssetDatabase.LoadAssetAtPath<FPSAnimationAsset>(path);
            FPSAnimationAsset asset = existing;
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<FPSAnimationAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.rigAsset = AssetDatabase.LoadAssetAtPath<KRig>(RigPath);
            asset.clip = clip;
            // Match working rifle overlays: null mask → controller upperBodyMask.
            asset.mask = null;
            asset.isAdditive = false;
            asset.blendTime = loopingPose
                ? new BlendTime(0.15f, 0.15f) { rateScale = 1f }
                : new BlendTime(0.08f, 0.12f) { rateScale = 1.1f };
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static void WireMeleePrefab(
            FPSAnimationAsset hold,
            FPSAnimationAsset attack,
            FPSAnimationAsset attackAlt)
        {
            MeleeWeapon melee = AssetDatabase.LoadAssetAtPath<MeleeWeapon>(PrefabPath);
            if (melee == null)
                return;

            var so = new SerializedObject(melee);
            so.FindProperty("holdOverlayPose").objectReferenceValue = hold;
            so.FindProperty("attackClip").objectReferenceValue = attack;
            so.FindProperty("attackClipAlt").objectReferenceValue = attackAlt;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(melee);
        }
    }
}
#endif
