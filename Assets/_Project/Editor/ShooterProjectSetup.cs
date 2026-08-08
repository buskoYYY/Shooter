using Lightbug.CharacterControllerPro.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Shooter.Project.Editor
{
    public static class ShooterProjectSetup
    {
        const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        static readonly string[] CcpDemoTags =
        {
            "Ice", "Mud", "Boost", "Grass", "Water", "Honey",
            "Ledge", "JumpDown", "WallSlide"
        };

        [MenuItem("Shooter/Project/Ensure CCP Demo Tags")]
        public static void EnsureCcpDemoTagsMenu()
        {
            EnsureCcpDemoTags();
            AssetDatabase.SaveAssets();
            Debug.Log("CCP demo tags ensured in Tag Manager.");
        }

        [MenuItem("Shooter/Project/Fix EventSystem for Input System (current scene)")]
        public static void FixEventSystemForInputSystem()
        {
            int fixedCount = UpgradeEventSystemsInScene();
            if (fixedCount == 0)
            {
                Debug.Log("No EventSystem with StandaloneInputModule found in scene.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"Upgraded {fixedCount} EventSystem(s) to InputSystemUIInputModule.");
        }

        [MenuItem("Shooter/Project/Fix Demo Warnings (current scene)")]
        public static void FixDemoWarningsInScene()
        {
            EnsureCcpDemoTags();
            UpgradeEventSystemsInScene();

            var player = GameObject.Find("PlayerCharacter") ?? GameObject.Find("Player Character");
            if (player != null)
            {
                StripDemoOnlyComponents(player);
                EditorUtility.SetDirty(player);
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("Demo warnings fix applied (tags, EventSystem, MaterialController).");
        }

        public static int UpgradeEventSystemsInScene()
        {
            int fixedCount = 0;
            var eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);

            foreach (var eventSystem in eventSystems)
            {
                var standalone = eventSystem.GetComponent<StandaloneInputModule>();
                if (standalone == null && eventSystem.GetComponent<InputSystemUIInputModule>() != null)
                    continue;

                if (standalone != null)
                {
                    Object.DestroyImmediate(standalone);
                    fixedCount++;
                }

                var uiModule = eventSystem.GetComponent<InputSystemUIInputModule>();
                if (uiModule == null)
                {
                    uiModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
                    fixedCount++;
                }

                if (inputActions != null)
                {
                    var so = new SerializedObject(uiModule);
                    var actionsProp = so.FindProperty("m_ActionsAsset");
                    if (actionsProp != null)
                    {
                        actionsProp.objectReferenceValue = inputActions;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }

            return fixedCount;
        }

        [MenuItem("Shooter/Project/Fix Demo Warnings (current scene)", true)]
        static bool FixDemoWarningsValidate() => !Application.isPlaying;

        [MenuItem("Shooter/Project/Fix EventSystem for Input System (current scene)", true)]
        static bool FixEventSystemValidate() => !Application.isPlaying;

        public static void EnsureCcpDemoTags()
        {
            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var tagsProp = tagManager.FindProperty("tags");

            foreach (var tag in CcpDemoTags)
                AddTagIfMissing(tagsProp, tag);

            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AddTagIfMissing(SerializedProperty tagsProp, string tag)
        {
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag)
                    return;
            }

            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
        }

        public static void StripDemoOnlyComponents(GameObject playerRoot)
        {
            if (playerRoot == null)
                return;

            var environment = playerRoot.transform.Find("Environment");
            if (environment == null)
                return;

            var materialController = environment.GetComponent<MaterialController>();
            if (materialController != null)
                Object.DestroyImmediate(materialController, true);
        }
    }
}
