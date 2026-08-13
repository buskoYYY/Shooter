using System.Collections.Generic;
using System.IO;
using KINEMATION.FPSAnimationFramework.Runtime.Layers.AdditiveLayer;
using KINEMATION.FPSAnimationFramework.Runtime.Layers.AdsLayer;
using KINEMATION.FPSAnimationFramework.Runtime.Layers.IkLayer;
using KINEMATION.FPSAnimationFramework.Runtime.Layers.IkMotionLayer;
using KINEMATION.FPSAnimationFramework.Runtime.Layers.LookLayer;
using KINEMATION.FPSAnimationFramework.Runtime.Layers.PoseSamplerLayer;
using KINEMATION.FPSAnimationFramework.Runtime.Layers.SwayLayer;
using KINEMATION.FPSAnimationFramework.Runtime.Layers.TurnLayer;
using KINEMATION.FPSAnimationFramework.Runtime.Layers.ViewLayer;
using KINEMATION.FPSAnimationFramework.Runtime.Camera;
using KINEMATION.FPSAnimationFramework.Runtime.Core;
using KINEMATION.FPSAnimationFramework.Runtime.Playables;
using KINEMATION.ProceduralRecoilAnimationSystem.Runtime;
using KINEMATION.Shared.KAnimationCore.Editor;
using KINEMATION.Shared.KAnimationCore.Runtime.Core;
using KINEMATION.Shared.KAnimationCore.Runtime.Input;
using KINEMATION.Shared.KAnimationCore.Runtime.Rig;
using Lightbug.CharacterControllerPro.Demo;
using Lightbug.CharacterControllerPro.Implementation;
using Shooter.Project.Character;
using Shooter.Project.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Shooter.Project.Editor
{
    public static class ShooterPhase2Setup
    {
        const string PlayerPrefabPath = "Assets/_Project/Prefabs/PlayerCharacter.prefab";
        const string TestScenePath = "Assets/_Project/Scenes/PlayerTest.unity";
        const string FpsFolder = "Assets/_Project/FPS";
        const string RigPath = FpsFolder + "/Rig_CharacterModel.asset";
        const string ProfilePath = FpsFolder + "/AnimatorProfile_CharacterModel.asset";

        const string InputConfigPath =
            "Assets/KINEMATION/FPSAnimationFramework/Assets/InputConfig_FPSAnimationFramework.asset";

        const string HumanoidControllerPath =
            "Assets/Demo/Animations/Locomotion/FPSAnimator_Humanoid.controller";

        const string UpperBodyMaskPath =
            "Assets/Demo/Animations/Masks/UpperBody_Humanoid.mask";

        const string OverlayPosePath =
            "Assets/Demo/Prefabs/Humanoid/AA_Rifle_OverlayPose_Humanoid.asset";

        const string UnarmedIdleFbxPath =
            "Assets/Demo/Animations/Locomotion/Humanoid/UnarmedSet/UnarmedLocomotion/Unarmed_Idle.fbx";

        const string UnarmedOverlayPosePath = FpsFolder + "/AA_Unarmed_OverlayPose_Humanoid.asset";

        const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        [MenuItem("Shooter/Phase 2/Setup FPS on Player Prefab")]
        public static void SetupFpsOnPlayerPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("Missing prefab", "Run Phase 1 setup first.", "OK");
                return;
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
                return;

            try
            {
                if (!TrySetupFpsCharacter(instance, out string error))
                {
                    EditorUtility.DisplayDialog("FPS setup failed", error, "OK");
                    return;
                }

                PrefabUtility.SaveAsPrefabAsset(instance, PlayerPrefabPath);
                Debug.Log("FPS setup saved to " + PlayerPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Shooter/Phase 2/Apply FPS to Test Scene")]
        public static void ApplyFpsToTestScene()
        {
            if (!TryOpenTestSceneAndPlayer(out var scene, out var player, out string error))
            {
                EditorUtility.DisplayDialog("Scene setup failed", error, "OK");
                return;
            }

            if (!TrySetupFpsCharacter(player, out error))
            {
                EditorUtility.DisplayDialog("FPS setup failed", error, "OK");
                return;
            }

            ConfigureFpsSceneCamera(player);
            SavePlayerToPrefab(player);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("FPS applied to scene and prefab: " + TestScenePath);
        }

        [MenuItem("Shooter/Phase 2/Fix Animator Profile (poseToSample)")]
        public static void FixAnimatorProfileMenu()
        {
            var rig = AssetDatabase.LoadAssetAtPath<KRig>(RigPath);
            var profile = AssetDatabase.LoadAssetAtPath<FPSAnimatorProfile>(ProfilePath);

            if (rig == null || profile == null)
            {
                EditorUtility.DisplayDialog(
                    "Missing assets",
                    "Rig or Profile not found in Assets/_Project/FPS/.\nRun Phase 2 setup first.",
                    "OK");
                return;
            }

            if (!TryConfigurePoseSampler(profile, rig, unarmed: true, out string error))
            {
                EditorUtility.DisplayDialog("Fix failed", error, "OK");
                return;
            }

            profile.OnRigUpdated();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log("Animator Profile fixed (unarmed pose): " + ProfilePath);
        }

        [MenuItem("Shooter/Phase 2/Apply Unarmed Hand Pose")]
        public static void ApplyUnarmedHandPoseMenu()
        {
            var rig = AssetDatabase.LoadAssetAtPath<KRig>(RigPath);
            var profile = AssetDatabase.LoadAssetAtPath<FPSAnimatorProfile>(ProfilePath);
            if (rig == null || profile == null)
            {
                EditorUtility.DisplayDialog("Missing assets", "Run Phase 2 setup first.", "OK");
                return;
            }

            if (!TryConfigurePoseSampler(profile, rig, unarmed: true, out string error))
            {
                EditorUtility.DisplayDialog("Failed", error, "OK");
                return;
            }

            profile.OnRigUpdated();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var player = GameObject.Find("PlayerCharacter");
            if (player != null)
            {
                SetupHandPoseState(player, player.transform.Find("Graphics/Character_model"), profile);
                SavePlayerToPrefab(player);
            }

            Debug.Log("Unarmed hand pose applied.");
        }

        [MenuItem("Shooter/Phase 2/Run Full Phase 2 Setup")]
        public static void RunFullPhase2Setup()
        {
            ShooterProjectSetup.EnsureCcpDemoTags();
            ApplyFpsToTestScene();
            EditorUtility.DisplayDialog(
                "Phase 2 setup",
                "Done.\n\nOpen PlayerTest scene and press Play.\n" +
                "FPS view with procedural body (Look, Turn, Sway, IK).\n\n" +
                "WASD move, mouse look, Space jump, Shift sprint, C crouch.",
                "OK");
        }

        static bool TrySetupFpsCharacter(GameObject playerRoot, out string error)
        {
            error = null;

            var model = playerRoot.transform.Find("Graphics/Character_model");
            if (model == null)
            {
                error = "Graphics/Character_model not found.";
                return false;
            }

            if (!TryFindCharacterBones(model, out var bones, out error))
                return false;

            var inputConfig = AssetDatabase.LoadAssetAtPath<UserInputConfig>(InputConfigPath);
            var animatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(HumanoidControllerPath);
            var upperBodyMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);

            if (inputConfig == null || animatorController == null || upperBodyMask == null)
            {
                error = "InputConfig, FPSAnimator_Humanoid controller, or UpperBody_Humanoid mask not found. Import FPS demo (Phase 0).";
                return false;
            }

            var overlayPose = AssetDatabase.LoadAssetAtPath<FPSAnimationAsset>(OverlayPosePath);
            if (overlayPose == null)
            {
                error = "Overlay pose not found:\n" + OverlayPosePath + "\nImport FPS demo (Phase 0).";
                return false;
            }

            EnsureFolder(FpsFolder);
            SetupIkTargets(bones);
            SetupFpsComponents(model.gameObject, bones, inputConfig, animatorController, upperBodyMask);

            var rig = CreateOrUpdateRig(bones, inputConfig, animatorController);
            var profile = CreateOrUpdateProfile(rig);

            var fpsAnimator = model.GetComponent<FPSAnimator>();
            var so = new SerializedObject(fpsAnimator);
            so.FindProperty("animatorProfile").objectReferenceValue = profile;
            so.ApplyModifiedPropertiesWithoutUndo();

            SetupFpsCamera(bones.Head);
            SetupPlayerBridge(playerRoot, model, inputActions, profile);

            ConfigureCcpMovement(playerRoot);
            ShooterProjectSetup.StripDemoOnlyComponents(playerRoot);
            return true;
        }

        struct CharacterBones
        {
            public Transform Root;
            public Transform Head;
            public Transform Pelvis;
            public Transform SpineRoot;
            public Transform RightHand;
            public Transform LeftHand;
            public Transform RightFoot;
            public Transform LeftFoot;
        }

        static bool TryFindCharacterBones(Transform model, out CharacterBones bones, out string error)
        {
            bones = default;
            error = null;

            var all = model.GetComponentsInChildren<Transform>(true);
            bones.Root = FindBoneExact(all, "root", "Root") ?? FindBone(all, "armature", "skeleton");
            bones.Head = FindBoneExact(all, "head", "Head") ?? FindBone(all, "head");
            bones.Pelvis = FindBoneExact(all, "pelvis", "Pelvis", "hips", "Hips") ?? FindBone(all, "pelvis", "hips", "hip");
            bones.SpineRoot = FindBoneExact(all, "spine_01", "spine1") ?? FindBone(all, "spine_01", "spine1", "spine_1", "spine") ?? bones.Pelvis;
            bones.RightHand = FindBoneExact(all, "hand_r", "Hand_R") ?? FindBone(all, "hand_r", "right_hand", "hand_right", "hand.r");
            bones.LeftHand = FindBoneExact(all, "hand_l", "Hand_L") ?? FindBone(all, "hand_l", "left_hand", "hand_left", "hand.l");
            bones.RightFoot = FindBoneExact(all, "foot_r", "Foot_R") ?? FindBone(all, "foot_r", "right_foot", "foot_right", "foot.r");
            bones.LeftFoot = FindBoneExact(all, "foot_l", "Foot_L") ?? FindBone(all, "foot_l", "left_foot", "foot_left", "foot.l");

            if (bones.Root == null)
                bones.Root = bones.Pelvis ?? model;

            if (bones.Head == null || bones.Pelvis == null || bones.RightHand == null || bones.LeftHand == null)
            {
                error = "Could not find required bones (Head, Pelvis, Hands). Check Character_model skeleton.";
                return false;
            }

            if (bones.RightFoot == null) bones.RightFoot = bones.LeftHand;
            if (bones.LeftFoot == null) bones.LeftFoot = bones.RightHand;

            return true;
        }

        static Transform FindBoneExact(IEnumerable<Transform> transforms, params string[] names)
        {
            foreach (var t in transforms)
            {
                foreach (var name in names)
                {
                    if (string.Equals(t.name, name, System.StringComparison.OrdinalIgnoreCase))
                        return t;
                }
            }

            return null;
        }

        static Transform FindBone(IEnumerable<Transform> transforms, params string[] candidates)
        {
            Transform best = null;
            int bestScore = 0;

            foreach (var t in transforms)
            {
                string name = t.name.ToLowerInvariant();
                if (name.Contains("mesh")) continue;

                foreach (var candidate in candidates)
                {
                    if (name.Contains(candidate) || name.EndsWith(candidate))
                    {
                        int score = candidate.Length;
                        if (score > bestScore)
                        {
                            bestScore = score;
                            best = t;
                        }
                    }
                }
            }

            return best;
        }

        static void SetupIkTargets(CharacterBones bones)
        {
            var root = bones.Root;
            var head = bones.Head;

            EnsureChild(root, FPSANames.IkRightFoot);
            EnsureChild(root, FPSANames.IkLeftFoot);
            EnsureChild(root, FPSANames.IkRightKnee);
            EnsureChild(root, FPSANames.IkLeftKnee);

            var ikWeaponRoot = EnsureChild(head, FPSANames.IkWeaponBone);
            var ikRightHand = EnsureChild(ikWeaponRoot, FPSANames.IkRightHand);
            var ikLeftHand = EnsureChild(ikWeaponRoot, FPSANames.IkLeftHand);
            var ikRightElbow = EnsureChild(ikWeaponRoot, FPSANames.IkRightElbow);
            var ikLeftElbow = EnsureChild(ikWeaponRoot, FPSANames.IkLeftElbow);

            EnsureChild(root, FPSANames.WeaponBone);
            EnsureChild(root, FPSANames.WeaponBoneAdditive);
            EnsureChild(bones.RightHand, FPSANames.IkWeaponBoneRight);
            EnsureChild(bones.LeftHand, FPSANames.IkWeaponBoneLeft);

            BindVirtual(ikRightHand, bones.RightHand);
            BindVirtual(ikLeftHand, bones.LeftHand);
            BindVirtual(ikRightElbow, bones.RightHand.parent);
            BindVirtual(ikLeftElbow, bones.LeftHand.parent);
            BindVirtual(EnsureChild(root, FPSANames.IkRightFoot), bones.RightFoot);
            BindVirtual(EnsureChild(root, FPSANames.IkLeftFoot), bones.LeftFoot);
            BindVirtual(EnsureChild(root, FPSANames.IkRightKnee), bones.RightFoot.parent);
            BindVirtual(EnsureChild(root, FPSANames.IkLeftKnee), bones.LeftFoot.parent);
        }

        static Transform EnsureChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) return child;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static void BindVirtual(Transform ikBone, Transform target)
        {
            if (target == null) return;
            var ve = ikBone.GetComponent<KVirtualElement>();
            if (ve == null) ve = ikBone.gameObject.AddComponent<KVirtualElement>();
            ve.targetBone = target;
        }

        static void SetupFpsComponents(GameObject model, CharacterBones bones, UserInputConfig inputConfig,
            RuntimeAnimatorController controller, AvatarMask upperBodyMask)
        {
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            var rigComponent = bones.Root.GetComponent<KRigComponent>();
            if (rigComponent == null) rigComponent = bones.Root.gameObject.AddComponent<KRigComponent>();
            rigComponent.RefreshHierarchy();

            EnsureComponent<FPSAnimator>(model);
            EnsureComponent<FPSBoneController>(model);

            var playables = EnsureComponent<FPSPlayablesController>(model);
            playables.upperBodyMask = upperBodyMask;

            EnsureComponent<RecoilAnimation>(model);

            var input = EnsureComponent<UserInputController>(model);
            input.inputConfig = inputConfig;
        }

        static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        static KRig CreateOrUpdateRig(CharacterBones bones, UserInputConfig inputConfig,
            RuntimeAnimatorController controller)
        {
            var rigComponent = bones.Root.GetComponent<KRigComponent>();
            rigComponent.RefreshHierarchy();

            var rig = AssetDatabase.LoadAssetAtPath<KRig>(RigPath);
            if (rig == null)
            {
                rig = ScriptableObject.CreateInstance<KRig>();
                AssetDatabase.CreateAsset(rig, RigPath);
            }

            rig.inputConfig = inputConfig;
            rig.targetAnimator = controller;
            rig.ImportRig(rigComponent);

            if (!rig.rigCurves.Contains(FPSANames.Curve_Overlay))
                rig.rigCurves.Add(FPSANames.Curve_Overlay);
            if (!rig.rigCurves.Contains(FPSANames.Curve_WeaponBoneWeight))
                rig.rigCurves.Add(FPSANames.Curve_WeaponBoneWeight);
            if (!rig.rigCurves.Contains(FPSANames.Curve_MaskAttachHand))
                rig.rigCurves.Add(FPSANames.Curve_MaskAttachHand);

            SetChain(rig, FPSANames.Chain_Pelvis, bones.Pelvis.name);
            SetChain(rig, FPSANames.Chain_SpineRoot, bones.SpineRoot.name);
            SetChain(rig, FPSANames.Chain_RightHand, bones.RightHand.name);
            SetChain(rig, FPSANames.Chain_LeftHand, bones.LeftHand.name);
            SetChain(rig, FPSANames.Chain_RightFoot, bones.RightFoot.name);
            SetChain(rig, FPSANames.Chain_LeftFoot, bones.LeftFoot.name);

            EditorUtility.SetDirty(rig);
            return rig;
        }

        static void SetChain(KRig rig, string chainName, string boneName)
        {
            var element = rig.GetElementByName(boneName);
            if (element.name == null) return;

            var existing = rig.GetElementChainByName(chainName);
            if (existing != null) rig.rigElementChains.Remove(existing);

            rig.rigElementChains.Add(new KRigElementChain
            {
                chainName = chainName,
                elementChain = new List<KRigElement> { element }
            });
        }

        static FPSAnimatorProfile CreateOrUpdateProfile(KRig rig)
        {
            var existing = AssetDatabase.LoadAssetAtPath<FPSAnimatorProfile>(ProfilePath);
            if (existing != null)
                AssetDatabase.DeleteAsset(ProfilePath);

            var profile = ScriptableObject.CreateInstance<FPSAnimatorProfile>();
            profile.rigAsset = rig;
            profile.settings = new List<FPSAnimatorLayerSettings>();
            AssetDatabase.CreateAsset(profile, ProfilePath);

            var poseSampler = ScriptableObject.CreateInstance<PoseSamplerLayerSettings>();
            AddLayer(profile, poseSampler);
            AddLayer(profile, ScriptableObject.CreateInstance<ViewLayerSettings>());
            AddLayer(profile, ScriptableObject.CreateInstance<AdsLayerSettings>());
            AddLayer(profile, ScriptableObject.CreateInstance<SwayLayerSettings>());
            AddLayer(profile, ScriptableObject.CreateInstance<IkMotionLayerSettings>());
            AddLayer(profile, ScriptableObject.CreateInstance<AdditiveLayerSettings>());

            var lookLayer = ScriptableObject.CreateInstance<LookLayerSettings>();
            lookLayer.useTurnOffset = true;
            AddLayer(profile, lookLayer);

            var turnLayer = ScriptableObject.CreateInstance<TurnLayerSettings>();
            turnLayer.animatorTurnRightTrigger = "TurnRight";
            turnLayer.animatorTurnLeftTrigger = "TurnLeft";
            turnLayer.angleThreshold = 45f;
            AddLayer(profile, turnLayer);

            var ikLayer = ScriptableObject.CreateInstance<IkLayerSettings>();
            AddLayer(profile, ikLayer);

            if (!TryConfigurePoseSampler(profile, rig, unarmed: true, out _))
            {
                Debug.LogWarning("PoseSampler not configured — overlay pose missing.");
            }

            var pelvis = rig.GetElementChainByName(FPSANames.Chain_Pelvis);
            var spineRoot = rig.GetElementChainByName(FPSANames.Chain_SpineRoot);
            var rightHand = rig.GetElementChainByName(FPSANames.Chain_RightHand);
            var leftHand = rig.GetElementChainByName(FPSANames.Chain_LeftHand);
            var rightFoot = rig.GetElementChainByName(FPSANames.Chain_RightFoot);
            var leftFoot = rig.GetElementChainByName(FPSANames.Chain_LeftFoot);

            if (pelvis != null && poseSampler != null)
            {
                poseSampler.pelvis = pelvis.elementChain[0];
                turnLayer.characterHipBone = pelvis.elementChain[0];
            }

            if (spineRoot != null && poseSampler != null)
            {
                poseSampler.spineRoot = spineRoot.elementChain[0];
                turnLayer.characterRootBone = rig.rigHierarchy[0];
            }

            ConfigureLookLayerSpine(lookLayer, rig);
            ConfigureSwayLayerHead(profile, rig);

            if (rightHand != null) ikLayer.rightHand = rightHand.elementChain[0];
            if (leftHand != null) ikLayer.leftHand = leftHand.elementChain[0];
            if (rightFoot != null) ikLayer.rightFoot = rightFoot.elementChain[0];
            if (leftFoot != null) ikLayer.leftFoot = leftFoot.elementChain[0];

            profile.OnRigUpdated();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        static bool TryConfigurePoseSampler(FPSAnimatorProfile profile, KRig rig, bool unarmed, out string error)
        {
            error = null;

            FPSAnimationAsset overlayPose = unarmed
                ? EnsureUnarmedOverlayAsset(rig)
                : AssetDatabase.LoadAssetAtPath<FPSAnimationAsset>(OverlayPosePath);

            if (overlayPose == null || overlayPose.clip == null)
            {
                error = unarmed
                    ? "Unarmed overlay pose or Unarmed_Idle clip not found.\nImport FPS demo (Phase 0)."
                    : "Overlay pose or clip not found:\n" + OverlayPosePath;
                return false;
            }

            PoseSamplerLayerSettings poseSampler = null;
            foreach (var layer in profile.settings)
            {
                if (layer is PoseSamplerLayerSettings sampler)
                {
                    poseSampler = sampler;
                    break;
                }
            }

            if (poseSampler == null)
            {
                error = "PoseSamplerLayerSettings not found in profile.";
                return false;
            }

            poseSampler.poseToSample = overlayPose;
            poseSampler.defaultWeaponPose = new KTransform
            {
                position = new Vector3(0.165f, 1.431f, 0.494f),
                rotation = Quaternion.identity,
                scale = Vector3.one
            };
            poseSampler.overwriteRoot = false;
            poseSampler.overwriteWeaponBone = !unarmed;

            var pelvis = rig.GetElementChainByName(FPSANames.Chain_Pelvis);
            var spineRoot = rig.GetElementChainByName(FPSANames.Chain_SpineRoot);
            if (pelvis != null)
                poseSampler.pelvis = pelvis.elementChain[0];
            if (spineRoot != null)
                poseSampler.spineRoot = spineRoot.elementChain[0];

            return true;
        }

        static FPSAnimationAsset EnsureUnarmedOverlayAsset(KRig rig)
        {
            var existing = AssetDatabase.LoadAssetAtPath<FPSAnimationAsset>(UnarmedOverlayPosePath);
            if (existing != null)
                return existing;

            AnimationClip idleClip = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(UnarmedIdleFbxPath))
            {
                if (asset is AnimationClip clip && clip.name == "Unarmed_Idle")
                {
                    idleClip = clip;
                    break;
                }
            }

            if (idleClip == null)
                return null;

            var overlay = ScriptableObject.CreateInstance<FPSAnimationAsset>();
            overlay.rigAsset = rig;
            overlay.clip = idleClip;
            AssetDatabase.CreateAsset(overlay, UnarmedOverlayPosePath);
            AssetDatabase.SaveAssets();
            return overlay;
        }

        [MenuItem("Shooter/Phase 2/Fix FPS Look Layer And Camera")]
        public static void FixFpsLookLayerAndCamera()
        {
            var rig = AssetDatabase.LoadAssetAtPath<KRig>(RigPath);
            var profile = AssetDatabase.LoadAssetAtPath<FPSAnimatorProfile>(ProfilePath);
            if (rig == null || profile == null)
            {
                EditorUtility.DisplayDialog("Missing assets", "Run Phase 2 setup first.", "OK");
                return;
            }

            foreach (var layer in profile.settings)
            {
                if (layer == null) continue;
                layer.isStandalone = false;

                if (layer is LookLayerSettings lookLayer)
                    ConfigureLookLayerSpine(lookLayer, rig);
            }

            ConfigureSwayLayerHead(profile, rig);
            profile.OnRigUpdated();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab != null)
            {
                var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (instance != null)
                {
                    try
                    {
                        var head = FindHeadTransform(instance.transform);
                        if (head != null)
                            SetupFpsCamera(head);

                        PrefabUtility.SaveAsPrefabAsset(instance, PlayerPrefabPath);
                    }
                    finally
                    {
                        Object.DestroyImmediate(instance);
                    }
                }
            }

            Debug.Log("FPS Look Layer and camera offset updated.");
        }

        static Transform FindHeadTransform(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "head")
                    return t;
            }

            return null;
        }

        static void ConfigureLookLayerSpine(LookLayerSettings lookLayer, KRig rig)
        {
            lookLayer.pitchOffsetElements.Clear();
            lookLayer.yawOffsetElements.Clear();
            lookLayer.rollOffsetElements.Clear();

            AddLookElement(lookLayer.pitchOffsetElements, rig, "pelvis", new Vector2(5f, 5f));
            foreach (var boneName in new[] { "spine_01", "spine_02", "spine_03", "spine_04", "neck_01", "neck_02", "head" })
                AddLookElement(lookLayer.pitchOffsetElements, rig, boneName, new Vector2(17f, 17f));

            foreach (var boneName in new[] { "spine_02", "spine_03", "spine_04" })
                AddLookElement(lookLayer.yawOffsetElements, rig, boneName, new Vector2(25f, 25f));
            AddLookElement(lookLayer.yawOffsetElements, rig, "neck_01", new Vector2(32.5f, 32.5f));

            AddLookElement(lookLayer.rollOffsetElements, rig, "pelvis", new Vector2(5f, 5f));
            foreach (var boneName in new[] { "spine_01", "spine_02", "spine_03", "spine_04" })
                AddLookElement(lookLayer.rollOffsetElements, rig, boneName, new Vector2(28.333334f, 28.333334f));
        }

        static void AddLookElement(List<LookLayerElement> list, KRig rig, string boneName, Vector2 angle)
        {
            var element = rig.GetElementByName(boneName);
            if (string.IsNullOrEmpty(element.name))
                return;

            list.Add(new LookLayerElement
            {
                rigElement = element,
                clampedAngle = angle,
                cachedClampedAngle = angle
            });
        }

        static void ConfigureSwayLayerHead(FPSAnimatorProfile profile, KRig rig)
        {
            var head = rig.GetElementByName("head");
            if (string.IsNullOrEmpty(head.name))
                return;

            foreach (var layer in profile.settings)
            {
                if (layer is SwayLayerSettings sway)
                    sway.headBone = head;
            }
        }

        static void AddLayer(FPSAnimatorProfile profile, FPSAnimatorLayerSettings layerSettings)
        {
            layerSettings.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            layerSettings.name = layerSettings.GetType().Name;
            profile.settings.Add(layerSettings);
            AssetDatabase.AddObjectToAsset(layerSettings, profile);
        }

        static void SetupFpsCamera(Transform head)
        {
            var cameraTransform = head.Find("FPS Camera");
            if (cameraTransform == null)
            {
                var cameraGo = new GameObject("FPS Camera");
                cameraTransform = cameraGo.transform;
                cameraTransform.SetParent(head, false);
                cameraGo.AddComponent<Camera>();
                cameraGo.AddComponent<AudioListener>();
            }

            cameraTransform.localPosition = new Vector3(0f, 0.06f, 0.04f);
            cameraTransform.localRotation = Quaternion.identity;

            var camera = cameraTransform.GetComponent<Camera>();
            if (camera != null)
            {
                camera.tag = "MainCamera";
                camera.nearClipPlane = 0.05f;
                camera.fieldOfView = 80f;
            }

            var fpsCamera = cameraTransform.GetComponent<FPSCameraController>();
            if (fpsCamera == null) fpsCamera = cameraTransform.gameObject.AddComponent<FPSCameraController>();

            var so = new SerializedObject(fpsCamera);
            so.FindProperty("cameraBone").objectReferenceValue = head;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetupPlayerBridge(GameObject playerRoot, Transform model, InputActionAsset inputActions,
            FPSAnimatorProfile profile)
        {
            var bridge = playerRoot.GetComponent<ShooterCharacterController>();
            if (bridge == null) bridge = playerRoot.AddComponent<ShooterCharacterController>();

            if (playerRoot.GetComponent<ShooterFpsCameraApply>() == null)
                playerRoot.AddComponent<ShooterFpsCameraApply>();

            if (playerRoot.GetComponent<ShooterFpsHeadHide>() == null)
                playerRoot.AddComponent<ShooterFpsHeadHide>();

            SetupHandPoseState(playerRoot, model, profile);

            var headHide = playerRoot.GetComponent<ShooterFpsHeadHide>();
            if (headHide != null)
            {
                var headHideSo = new SerializedObject(headHide);
                headHideSo.FindProperty("characterRoot").objectReferenceValue = model;
                headHideSo.FindProperty("hiddenMaterial").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/FPS_HeadHidden.mat");
                headHideSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var so = new SerializedObject(bridge);
            so.FindProperty("fpsCharacterRoot").objectReferenceValue = model;
            so.FindProperty("inputActions").objectReferenceValue = inputActions;
            so.FindProperty("pitchClamp").floatValue = 70f;
            var jumpMotion = AssetDatabase.LoadAssetAtPath<IkMotionLayerSettings>(
                "Assets/Demo/AnimatorProfiles/IKMotions/IKMotion_Jump.asset");
            if (jumpMotion != null)
                so.FindProperty("jumpMotion").objectReferenceValue = jumpMotion;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetupHandPoseState(GameObject playerRoot, Transform model, FPSAnimatorProfile profile)
        {
            if (playerRoot == null || profile == null)
                return;

            System.Type handPoseType = typeof(ShooterCharacterController).Assembly.GetType(
                "Shooter.Project.Character.ShooterHandPoseState");
            if (handPoseType == null)
            {
                Debug.LogError(
                    "ShooterHandPoseState not found in Shooter.Project assembly. " +
                    "Check that Assets/_Project/Scripts/Character/ShooterHandPoseState.cs compiles.");
                return;
            }

            Component handPose = playerRoot.GetComponent(handPoseType);
            if (handPose == null)
                handPose = playerRoot.AddComponent(handPoseType);

            var unarmedPose = AssetDatabase.LoadAssetAtPath<FPSAnimationAsset>(UnarmedOverlayPosePath)
                ?? EnsureUnarmedOverlayAsset(profile.rigAsset);
            var armedPose = AssetDatabase.LoadAssetAtPath<FPSAnimationAsset>(OverlayPosePath);

            var so = new SerializedObject(handPose);
            so.FindProperty("fpsCharacterRoot").objectReferenceValue = model;
            so.FindProperty("inputActions").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            so.FindProperty("fpsAnimatorProfile").objectReferenceValue = profile;
            so.FindProperty("unarmedOverlayPose").objectReferenceValue = unarmedPose;
            so.FindProperty("armedOverlayPose").objectReferenceValue = armedPose;
            so.FindProperty("startUnarmed").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigureCcpMovement(GameObject playerRoot)
        {
            var states = playerRoot.transform.Find("States");
            if (states == null) return;

            var stateController = states.GetComponent<CharacterStateController>();
            if (stateController != null)
            {
                stateController.MovementReferenceMode = MovementReferenceParameters.MovementReferenceMode.External;
                stateController.ExternalReference = playerRoot.transform;
            }

            var normalMovement = states.GetComponent<NormalMovement>();
            if (normalMovement != null)
                normalMovement.lookingDirectionParameters.changeLookingDirection = false;
        }

        static bool TryOpenTestSceneAndPlayer(out Scene scene, out GameObject player, out string error)
        {
            scene = default;
            player = null;
            error = null;

            if (!File.Exists(TestScenePath))
            {
                error = "Run Phase 1 Create Test Scene first.";
                return false;
            }

            scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
            player = GameObject.Find("PlayerCharacter");
            if (player == null)
            {
                error = "PlayerCharacter not found in scene.";
                return false;
            }

            return true;
        }

        static void SavePlayerToPrefab(GameObject playerRoot)
        {
            var prefabRoot = PrefabUtility.GetCorrespondingObjectFromSource(playerRoot);
            if (prefabRoot != null)
            {
                PrefabUtility.SaveAsPrefabAssetAndConnect(playerRoot, PlayerPrefabPath, InteractionMode.AutomatedAction);
                return;
            }

            PrefabUtility.SaveAsPrefabAsset(playerRoot, PlayerPrefabPath);
        }

        static void ConfigureFpsSceneCamera(GameObject playerRoot)
        {
            var model = playerRoot.transform.Find("Graphics/Character_model");
            if (model == null) return;

            var fpsCamera = model.GetComponentInChildren<Camera>(true);
            if (fpsCamera != null)
            {
                fpsCamera.tag = "MainCamera";
                fpsCamera.gameObject.SetActive(true);
            }

            var mainCamera = Camera.main;
            if (mainCamera != null && fpsCamera != null && mainCamera != fpsCamera)
            {
                var camera3D = mainCamera.GetComponent<Camera3D>();
                if (camera3D != null) camera3D.enabled = false;

                mainCamera.enabled = false;
                var listener = mainCamera.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = false;
            }

            ConfigureCcpMovement(playerRoot);

            var bridge = playerRoot.GetComponent<ShooterCharacterController>();
            if (bridge == null)
            {
                var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
                var profile = AssetDatabase.LoadAssetAtPath<FPSAnimatorProfile>(ProfilePath);
                SetupPlayerBridge(playerRoot, model, inputActions, profile);
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var folder = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
