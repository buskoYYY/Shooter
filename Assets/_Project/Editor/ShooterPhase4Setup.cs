using KINEMATION.FPSAnimationFramework.Runtime.Core;
using Lightbug.CharacterControllerPro.Demo;
using Lightbug.CharacterControllerPro.Implementation;
using Shooter.Project.Character;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Project.Editor
{
    public static class ShooterPhase4Setup
    {
        const string TestScenePath = "Assets/_Project/Scenes/PlayerTest.unity";
        const string PlayerPrefabPath = "Assets/_Project/Prefabs/PlayerCharacter.prefab";
        const string LadderControllerPath =
            "Assets/Character Controller Pro/Demo/Animations/Character/LadderClimbing.controller";
        const string HumanoidControllerPath =
            "Assets/Demo/Animations/Locomotion/FPSAnimator_Humanoid.controller";
        const string FpsProfilePath =
            "Assets/_Project/FPS/AnimatorProfile_CharacterModel.asset";

        [MenuItem("Shooter/Phase 4/Setup Ladder on Player")]
        public static void SetupLadderOnPlayer()
        {
            var player = FindPlayerInSceneOrPrefab();
            if (player == null)
            {
                EditorUtility.DisplayDialog("Player not found", "Open PlayerTest or the player prefab.", "OK");
                return;
            }

            if (!ConfigurePlayerForLadder(player, out string error))
            {
                EditorUtility.DisplayDialog("Ladder setup failed", error, "OK");
                return;
            }

            EditorUtility.SetDirty(player);
            if (player.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(player.scene);

            Debug.Log("Ladder climbing enabled on " + player.name);
        }

        [MenuItem("Shooter/Phase 4/Setup Ladder on Player (current scene)")]
        public static void SetupLadderOnPlayerInScene()
        {
            var player = GameObject.Find("PlayerCharacter") ?? GameObject.Find("Player Character");
            if (player == null)
            {
                EditorUtility.DisplayDialog("Player not found", "Place PlayerCharacter in the open scene first.", "OK");
                return;
            }

            if (!ConfigurePlayerForLadder(player, out string error))
            {
                EditorUtility.DisplayDialog("Ladder setup failed", error, "OK");
                return;
            }

            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("Ladder climbing enabled on " + player.name + " in current scene.");
        }

        [MenuItem("Shooter/Phase 4/Add Test Ladder to Scene")]
        public static void AddTestLadderToScene()
        {
            if (!System.IO.File.Exists(TestScenePath))
            {
                EditorUtility.DisplayDialog("Scene missing", "Run Phase 1 Create Test Scene first.", "OK");
                return;
            }

            var scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
            CreateTestLadder(new Vector3(12f, 0f, 0f), 4f);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Test ladder added to " + TestScenePath);
        }

        [MenuItem("Shooter/Phase 4/Run Full Phase 4 Setup")]
        public static void RunFullPhase4Setup()
        {
            if (!System.IO.File.Exists(TestScenePath))
            {
                EditorUtility.DisplayDialog("Scene missing", "Run Phase 1 Create Test Scene first.", "OK");
                return;
            }

            EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
            var player = GameObject.Find("PlayerCharacter");
            if (player == null)
            {
                EditorUtility.DisplayDialog("Player missing", "PlayerCharacter not found in scene.", "OK");
                return;
            }

            if (!ConfigurePlayerForLadder(player, out string error))
            {
                EditorUtility.DisplayDialog("Ladder setup failed", error, "OK");
                return;
            }

            if (GameObject.Find("TestLadder") == null)
                CreateTestLadder(new Vector3(12f, 0f, 0f), 4f);

            SavePlayerToPrefab(player);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

            EditorUtility.DisplayDialog(
                "Phase 4 setup",
                "Done.\n\nWalk to the ladder, press Interact (E).\n" +
                "W/S or Up/Down to climb, Interact to exit.",
                "OK");
        }

        static GameObject FindPlayerInSceneOrPrefab()
        {
            var player = GameObject.Find("PlayerCharacter") ?? GameObject.Find("Player Character");
            if (player != null)
                return player;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
                return null;

            return PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        }

        static bool ConfigurePlayerForLadder(GameObject playerRoot, out string error)
        {
            error = null;

            var ladderController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(LadderControllerPath);
            var locomotionController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(HumanoidControllerPath);

            if (ladderController == null || locomotionController == null)
            {
                error = "LadderClimbing.controller or FPSAnimator_Humanoid not found.";
                return false;
            }

            var states = playerRoot.transform.Find("States");
            if (states == null)
            {
                error = "States child not found on player.";
                return false;
            }

            ConfigureNormalMovement(states.gameObject);
            ConfigureLadderState(states.gameObject, ladderController);
            ConfigureLadderBridge(playerRoot, locomotionController);
            return true;
        }

        static void ConfigureNormalMovement(GameObject states)
        {
            var normalMovement = states.GetComponent<NormalMovement>();
            if (normalMovement == null)
                return;

            var so = new SerializedObject(normalMovement);
            so.FindProperty("overrideAnimatorController").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigureLadderState(GameObject states, RuntimeAnimatorController ladderController)
        {
            var ladderState = states.GetComponent<LadderClimbing>();
            if (ladderState == null)
                ladderState = states.AddComponent<LadderClimbing>();

            ladderState.enabled = true;

            var so = new SerializedObject(ladderState);
            so.FindProperty("overrideAnimatorController").boolValue = true;
            so.FindProperty("runtimeAnimatorController").objectReferenceValue = ladderController;
            so.FindProperty("useInteractAction").boolValue = true;
            so.FindProperty("filterByAngle").boolValue = true;
            so.FindProperty("maxAngle").floatValue = 70f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigureLadderBridge(GameObject playerRoot, RuntimeAnimatorController locomotionController)
        {
            var bridge = playerRoot.GetComponent<ShooterLadderFpsBridge>();
            if (bridge == null)
                bridge = playerRoot.AddComponent<ShooterLadderFpsBridge>();

            var graphics = playerRoot.transform.Find("Graphics");
            Transform model = graphics != null && graphics.childCount > 0 ? graphics.GetChild(0) : null;
            var profile = AssetDatabase.LoadAssetAtPath<FPSAnimatorProfile>(FpsProfilePath);

            var so = new SerializedObject(bridge);
            so.FindProperty("fpsCharacterRoot").objectReferenceValue = model;
            so.FindProperty("locomotionController").objectReferenceValue = locomotionController;
            so.FindProperty("fpsAnimatorProfile").objectReferenceValue = profile;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void CreateTestLadder(Vector3 position, float height)
        {
            var ladderGo = new GameObject("TestLadder");
            ladderGo.transform.SetPositionAndRotation(position, Quaternion.identity);

            var bottom = new GameObject("BottomReference").transform;
            bottom.SetParent(ladderGo.transform, false);
            bottom.localPosition = new Vector3(0f, 0.4f, 0.35f);

            var top = new GameObject("TopReference").transform;
            top.SetParent(ladderGo.transform, false);
            top.localPosition = new Vector3(0f, height, 0.35f);

            var trigger = ladderGo.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(1.2f, height, 0.9f);
            trigger.center = new Vector3(0f, height * 0.5f, 0.2f);

            var ladder = ladderGo.AddComponent<Ladder>();
            var so = new SerializedObject(ladder);
            so.FindProperty("topReference").objectReferenceValue = top;
            so.FindProperty("bottomReference").objectReferenceValue = bottom;
            so.FindProperty("climbingAnimations").intValue = 1;
            so.FindProperty("facingDirection").enumValueIndex = 4; // Forward
            so.ApplyModifiedPropertiesWithoutUndo();

            var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rail.name = "LadderRail";
            rail.transform.SetParent(ladderGo.transform, false);
            rail.transform.localScale = new Vector3(0.25f, height, 0.08f);
            rail.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            Object.DestroyImmediate(rail.GetComponent<Collider>());
        }

        static void SavePlayerToPrefab(GameObject playerRoot)
        {
            var prefabRoot = PrefabUtility.GetCorrespondingObjectFromSource(playerRoot);
            if (prefabRoot != null)
                PrefabUtility.SaveAsPrefabAssetAndConnect(playerRoot, PlayerPrefabPath, InteractionMode.AutomatedAction);
            else
                PrefabUtility.SaveAsPrefabAsset(playerRoot, PlayerPrefabPath);
        }
    }
}
