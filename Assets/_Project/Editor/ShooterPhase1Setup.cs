using System.IO;
using Lightbug.CharacterControllerPro.Demo;
using Lightbug.CharacterControllerPro.Implementation;
using Shooter.Project.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Shooter.Project.Editor
{
    public static class ShooterPhase1Setup
    {
        const string DemoCharacterPath =
            "Assets/Character Controller Pro/Demo/Prefabs/Characters/Demo Character 3D.prefab";

        const string CharacterModelPath =
            "Assets/_Project/Packages/Models/Character_model.fbx";

        const string InputActionsPath =
            "Assets/InputSystem_Actions.inputactions";

        const string PlayerPrefabPath =
            "Assets/_Project/Prefabs/PlayerCharacter.prefab";

        const string TestScenePath =
            "Assets/_Project/Scenes/PlayerTest.unity";

        const string FpsDemoPackagePath =
            "Assets/_Project/Downloads/FPSAnimationFramework_Demo.unitypackage";

        [MenuItem("Shooter/Phase 0/Import FPS AF Demo Content")]
        static void ImportFpsDemoContent()
        {
            if (!File.Exists(FpsDemoPackagePath))
            {
                EditorUtility.DisplayDialog(
                    "Demo not found",
                    "Place FPSAnimationFramework_Demo.unitypackage at:\n" + FpsDemoPackagePath +
                    "\n\nOr use KINEMATION → Tools → FPS Animation Framework → Download Demo",
                    "OK");
                return;
            }

            AssetDatabase.ImportPackage(FpsDemoPackagePath, true);
        }

        [MenuItem("Shooter/Phase 1/Create Player Prefab")]
        static void CreatePlayerPrefab()
        {
            var demoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DemoCharacterPath);
            var characterModel = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterModelPath);
            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);

            if (demoPrefab == null || characterModel == null || inputActions == null)
            {
                EditorUtility.DisplayDialog(
                    "Missing assets",
                    "Could not find Demo Character 3D, Character_model, or InputSystem_Actions.",
                    "OK");
                return;
            }

            EnsureFolder("Assets/_Project/Prefabs");

            var instance = PrefabUtility.InstantiatePrefab(demoPrefab) as GameObject;
            if (instance == null)
                return;

            try
            {
                instance.name = "Player Character";
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

                var graphics = instance.transform.Find("Graphics");
                if (graphics == null)
                {
                    Debug.LogError("Graphics child not found on demo character.");
                    return;
                }

                for (int i = graphics.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(graphics.GetChild(i).gameObject);

                var modelInstance = PrefabUtility.InstantiatePrefab(characterModel, graphics) as GameObject;
                if (modelInstance != null)
                {
                    modelInstance.name = "Character_model";
                    modelInstance.transform.localPosition = Vector3.zero;
                    modelInstance.transform.localRotation = Quaternion.identity;
                    modelInstance.transform.localScale = Vector3.one;

                    var animator = modelInstance.GetComponent<Animator>();
                    if (animator != null)
                    {
                        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                            "Assets/Character Controller Pro/Demo/Animations/Character/NormalMovement.controller");
                        if (controller != null)
                            animator.runtimeAnimatorController = controller;
                    }
                }

                ConfigureInput(instance, inputActions);
                DisableExtraDemoStates(instance);

                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
                if (existing != null)
                    PrefabUtility.SaveAsPrefabAsset(instance, PlayerPrefabPath);
                else
                    PrefabUtility.SaveAsPrefabAsset(instance, PlayerPrefabPath);

                Debug.Log("Player prefab saved: " + PlayerPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Shooter/Phase 1/Create Test Scene")]
        static void CreateTestScene()
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null)
            {
                EditorUtility.DisplayDialog(
                    "Player prefab missing",
                    "Run Shooter → Phase 1 → Create Player Prefab first.",
                    "OK");
                return;
            }

            EnsureFolder("Assets/_Project/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            CreateGround();
            CreateSlope();

            var player = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
            if (player != null)
                player.transform.SetPositionAndRotation(new Vector3(0f, 2f, 0f), Quaternion.identity);

            SetupFollowCamera(player);
            ConfigureMovementReference(player, Camera.main != null ? Camera.main.transform : null);

            EditorSceneManager.SaveScene(scene, TestScenePath);
            Debug.Log("Test scene saved: " + TestScenePath);
        }

        [MenuItem("Shooter/Phase 1/Run Full Setup")]
        static void RunFullSetup()
        {
            CreatePlayerPrefab();
            CreateTestScene();
            EditorUtility.DisplayDialog(
                "Phase 1 setup",
                "Done.\n\n1. Open Assets/_Project/Scenes/PlayerTest.unity\n" +
                "2. Press Play — WASD move, mouse look, Space jump, Shift sprint, C crouch\n" +
                "3. Import FPS demo via Shooter → Phase 0 if not yet imported",
                "OK");
        }

        static void ConfigureInput(GameObject root, InputActionAsset inputActions)
        {
            var actionsTransform = root.transform.Find("Actions");
            if (actionsTransform == null)
                return;

            var brain = actionsTransform.GetComponent<CharacterBrain>();
            var inputHandler = actionsTransform.GetComponent<ShooterInputHandler>();
            if (inputHandler == null)
                inputHandler = actionsTransform.gameObject.AddComponent<ShooterInputHandler>();

            var so = new SerializedObject(inputHandler);
            so.FindProperty("inputActions").objectReferenceValue = inputActions;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (brain != null)
            {
                var brainSo = new SerializedObject(brain);
                brainSo.FindProperty("isAI").boolValue = false;
                brainSo.FindProperty("inputHandlerSettings").FindPropertyRelative("humanInputType").enumValueIndex =
                    (int)HumanInputType.Custom;
                brainSo.FindProperty("inputHandlerSettings").FindPropertyRelative("inputHandler").objectReferenceValue =
                    inputHandler;
                brainSo.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void DisableExtraDemoStates(GameObject root)
        {
            var states = root.transform.Find("States");
            if (states == null)
                return;

            DisableIfPresent<Dash>(states.gameObject);
            DisableIfPresent<LedgeHanging>(states.gameObject);
            DisableIfPresent<JetPack>(states.gameObject);
            DisableIfPresent<WallSlide>(states.gameObject);
            DisableIfPresent<RopeClimbing>(states.gameObject);
            DisableIfPresent<ZeroGravity>(states.gameObject);
        }

        static void DisableIfPresent<T>(GameObject host) where T : Behaviour
        {
            var component = host.GetComponent<T>();
            if (component != null)
                component.enabled = false;
        }

        static void CreateGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            ground.isStatic = true;
        }

        static void CreateSlope()
        {
            var slope = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slope.name = "Slope";
            slope.transform.SetPositionAndRotation(new Vector3(8f, 1.5f, 0f), Quaternion.Euler(0f, 0f, 25f));
            slope.transform.localScale = new Vector3(6f, 0.2f, 4f);
            slope.isStatic = true;
        }

        static void SetupFollowCamera(GameObject player)
        {
            var mainCamera = Camera.main;
            if (mainCamera == null || player == null)
                return;

            var graphics = player.transform.Find("Graphics");
            if (graphics == null)
                return;

            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            var cameraInput = mainCamera.GetComponent<ShooterInputHandler>();
            if (cameraInput == null)
                cameraInput = mainCamera.gameObject.AddComponent<ShooterInputHandler>();

            var inputSo = new SerializedObject(cameraInput);
            inputSo.FindProperty("inputActions").objectReferenceValue = inputActions;
            inputSo.ApplyModifiedPropertiesWithoutUndo();

            var camera3D = mainCamera.GetComponent<Camera3D>();
            if (camera3D == null)
                camera3D = mainCamera.gameObject.AddComponent<Camera3D>();

            var so = new SerializedObject(camera3D);
            so.FindProperty("targetTransform").objectReferenceValue = graphics;
            so.FindProperty("inputHandlerSettings").FindPropertyRelative("humanInputType").enumValueIndex =
                (int)HumanInputType.Custom;
            so.FindProperty("inputHandlerSettings").FindPropertyRelative("inputHandler").objectReferenceValue =
                cameraInput;
            so.FindProperty("cameraMode").enumValueIndex = 1; // ThirdPerson for phase 1 testing
            so.FindProperty("distanceToTarget").floatValue = 4f;
            so.FindProperty("hideBody").boolValue = false;
            so.FindProperty("initialPitch").floatValue = 15f;
            so.ApplyModifiedPropertiesWithoutUndo();

            mainCamera.transform.SetPositionAndRotation(new Vector3(0f, 3f, -6f), Quaternion.identity);
        }

        static void ConfigureMovementReference(GameObject player, Transform cameraTransform)
        {
            if (player == null || cameraTransform == null)
                return;

            var states = player.transform.Find("States");
            if (states == null)
                return;

            var stateController = states.GetComponent<CharacterStateController>();
            if (stateController == null)
                return;

            stateController.MovementReferenceMode = MovementReferenceParameters.MovementReferenceMode.External;
            stateController.ExternalReference = cameraTransform;

            EditorUtility.SetDirty(stateController);
        }

        [MenuItem("Shooter/Phase 1/Fix Movement Reference (current scene)")]
        static void FixMovementReferenceInOpenScene()
        {
            var player = GameObject.Find("Player Character");
            if (player == null)
            {
                EditorUtility.DisplayDialog(
                    "Player not found",
                    "No GameObject named 'Player Character' in the scene.\n" +
                    "Select your player or re-run Phase 1 setup.",
                    "OK");
                return;
            }

            var camera = Camera.main;
            if (camera == null)
            {
                EditorUtility.DisplayDialog("Camera not found", "Main Camera is missing in the scene.", "OK");
                return;
            }

            ConfigureMovementReference(player, camera.transform);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("Movement reference set to Main Camera.");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var folder = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
