using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Shooter.Project.Editor
{
    public static class ShooterAnimatorJumpSetup
    {
        const string FpsFolder = "Assets/_Project/FPS";
        const string DemoHumanoidControllerPath =
            "Assets/Demo/Animations/Locomotion/FPSAnimator_Humanoid.controller";
        const string ProjectHumanoidControllerPath = FpsFolder + "/FPSAnimator_Humanoid.controller";

        const string JumpStartFbxPath =
            "Assets/Demo/Animations/Locomotion/Humanoid/InAir/C_JumpStart_Humanoid.fbx";
        const string JumpLoopFbxPath =
            "Assets/Demo/Animations/Locomotion/Humanoid/InAir/C_JumpLoop_Humanoid.fbx";
        const string JumpEndFbxPath =
            "Assets/Demo/Animations/Locomotion/Humanoid/InAir/C_JumpEnd_Humanoid.fbx";

        const string InAirLayerName = "InAir";

        [MenuItem("Shooter/Phase 2/Add Humanoid Jump Animation Layer")]
        public static void AddJumpLayerMenu()
        {
            if (!EnsureHumanoidJumpLayer(out string error))
            {
                EditorUtility.DisplayDialog("Jump layer setup failed", error, "OK");
                return;
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(
                "Done",
                "InAir jump layer added to:\n" + ProjectHumanoidControllerPath,
                "OK");
        }

        public static RuntimeAnimatorController EnsureProjectHumanoidController()
        {
            EnsureHumanoidJumpLayer(out _);
            return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ProjectHumanoidControllerPath);
        }

        public static bool EnsureHumanoidJumpLayer(out string error)
        {
            error = null;

            if (!File.Exists(DemoHumanoidControllerPath))
            {
                error = "Demo humanoid controller not found.\nImport FPS demo (Phase 0).";
                return false;
            }

            Directory.CreateDirectory(FpsFolder);

            if (!File.Exists(ProjectHumanoidControllerPath))
            {
                if (!AssetDatabase.CopyAsset(DemoHumanoidControllerPath, ProjectHumanoidControllerPath))
                {
                    error = "Failed to copy controller to:\n" + ProjectHumanoidControllerPath;
                    return false;
                }
            }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ProjectHumanoidControllerPath);
            if (controller == null)
            {
                error = "Could not load project humanoid controller.";
                return false;
            }

            if (HasInAirJumpLayer(controller))
            {
                EditorUtility.SetDirty(controller);
                return true;
            }

            AnimationClip jumpStart = LoadClipFromFbx(JumpStartFbxPath, "C_JumpStart_Humanoid");
            AnimationClip jumpLoop = LoadClipFromFbx(JumpLoopFbxPath, "C_JumpLoop_Humanoid");
            AnimationClip jumpEnd = LoadClipFromFbx(JumpEndFbxPath, "C_JumpEnd_Humanoid");

            if (jumpStart == null || jumpLoop == null || jumpEnd == null)
            {
                error = "Humanoid jump clips not found under Demo/InAir.\nImport FPS demo (Phase 0).";
                return false;
            }

            EnsureAnimatorParameter(controller, "InAir", AnimatorControllerParameterType.Bool);
            AddInAirLayer(controller, jumpStart, jumpLoop, jumpEnd);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return true;
        }

        static bool HasInAirJumpLayer(AnimatorController controller)
        {
            for (int i = 0; i < controller.layers.Length; i++)
            {
                AnimatorControllerLayer layer = controller.layers[i];
                if (layer.name != InAirLayerName)
                    continue;

                foreach (ChildAnimatorState child in layer.stateMachine.states)
                {
                    if (child.state.name == "JumpStart")
                        return true;
                }

                controller.RemoveLayer(i);
                return false;
            }

            return false;
        }

        static void EnsureAnimatorParameter(
            AnimatorController controller,
            string name,
            AnimatorControllerParameterType type)
        {
            foreach (AnimatorControllerParameter parameter in controller.parameters)
            {
                if (parameter.name == name)
                    return;
            }

            controller.AddParameter(name, type);
        }

        static void AddInAirLayer(
            AnimatorController controller,
            AnimationClip jumpStart,
            AnimationClip jumpLoop,
            AnimationClip jumpEnd)
        {
            controller.AddLayer(InAirLayerName);
            int layerIndex = controller.layers.Length - 1;
            AnimatorControllerLayer layer = controller.layers[layerIndex];
            layer.defaultWeight = 1f;
            layer.blendingMode = AnimatorLayerBlendingMode.Override;

            AnimatorStateMachine stateMachine = layer.stateMachine;

            AnimatorState empty = stateMachine.AddState("Empty", new Vector3(300f, 0f, 0f));
            empty.writeDefaultValues = true;

            AnimatorState start = stateMachine.AddState("JumpStart", new Vector3(300f, 120f, 0f));
            start.motion = jumpStart;
            start.speed = 1.3f;
            start.cycleOffset = 0.5f;

            AnimatorState loop = stateMachine.AddState("JumpLoop", new Vector3(300f, 240f, 0f));
            loop.motion = jumpLoop;

            AnimatorState end = stateMachine.AddState("JumpEnd", new Vector3(300f, 360f, 0f));
            end.motion = jumpEnd;
            end.speed = 1.3f;

            stateMachine.defaultState = empty;

            AnimatorStateTransition emptyToStart = empty.AddTransition(start);
            emptyToStart.hasExitTime = false;
            emptyToStart.duration = 0.1f;
            emptyToStart.AddCondition(AnimatorConditionMode.If, 0f, "InAir");

            AnimatorStateTransition startToLoop = start.AddTransition(loop);
            startToLoop.hasExitTime = true;
            startToLoop.exitTime = 1f;
            startToLoop.duration = 0f;

            AnimatorStateTransition loopToEnd = loop.AddTransition(end);
            loopToEnd.hasExitTime = true;
            loopToEnd.exitTime = 0f;
            loopToEnd.duration = 0f;
            loopToEnd.AddCondition(AnimatorConditionMode.IfNot, 0f, "InAir");

            AnimatorStateTransition endToEmpty = end.AddTransition(empty);
            endToEmpty.hasExitTime = true;
            endToEmpty.exitTime = 0.95f;
            endToEmpty.duration = 0.15f;

            controller.layers[layerIndex] = layer;
        }

        static AnimationClip LoadClipFromFbx(string fbxPath, string clipName)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (asset is AnimationClip clip && clip.name == clipName)
                    return clip;
            }

            return null;
        }
    }
}
