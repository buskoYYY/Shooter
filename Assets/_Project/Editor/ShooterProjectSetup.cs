using Lightbug.CharacterControllerPro.Demo;
using UnityEditor;
using UnityEngine;

namespace Shooter.Project.Editor
{
    public static class ShooterProjectSetup
    {
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

        [MenuItem("Shooter/Project/Fix Demo Warnings (current scene)")]
        public static void FixDemoWarningsInScene()
        {
            EnsureCcpDemoTags();

            var player = GameObject.Find("PlayerCharacter") ?? GameObject.Find("Player Character");
            if (player != null)
            {
                StripDemoOnlyComponents(player);
                EditorUtility.SetDirty(player);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("CCP demo tags ensured. MaterialController removed from Environment (if present).");
        }

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
