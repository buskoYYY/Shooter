#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Project.Editor
{
    /// <summary>
    /// Task 2.8 — one-click weapon acceptance scene (PlayerTest).
    /// </summary>
    public static class ShooterWeaponTestSceneSetup
    {
        const string TestScenePath = "Assets/_Project/Scenes/PlayerTest.unity";
        const int EnvironmentLayerIndex = 6;
        const string EnvironmentLayerName = "Environment";
        const string WallName = "WeaponCollisionWall";

        [MenuItem("Shooter/Project/Setup Weapon Test Scene (2.8)")]
        public static void SetupMenu()
        {
            if (!File.Exists(TestScenePath))
            {
                EditorUtility.DisplayDialog(
                    "Weapon test scene",
                    "Не найдена сцена:\n" + TestScenePath + "\nСначала Phase 1.",
                    "OK");
                return;
            }

            EnsureEnvironmentLayer();

            Scene scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
            Transform root = GetOrCreateRoot("WeaponTest").transform;

            ShooterWeaponSystemSetup.EnsureTestContentForScene(root);
            EnsureCollisionWall(root);

            // Collision layer on character profile (safe if already present).
            ShooterCollisionLayerSetup.SetupSilent();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            EditorUtility.DisplayDialog(
                "Weapon test scene (2.8)",
                "PlayerTest готов для приёмки оружия.\n\n" +
                "В сцене:\n" +
                "• Мишени (DummyTarget_*)\n" +
                "• Патроны Rifle / Pistol\n" +
                "• Стена WeaponCollisionWall (layer Environment)\n" +
                "• CollisionLayer в профиле персонажа\n" +
                "• Лестница / склон — как были\n\n" +
                "Чеклист: Docs/WEAPON_SETUP.md → «Приёмка 2.8».\n" +
                "Play → 1–5, стрельба, стена, патроны, лестница.",
                "OK");
        }

        public static void EnsureCollisionWall(Transform parent)
        {
            GameObject wall = GameObject.Find(WallName);
            if (wall == null)
            {
                wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = WallName;
                Undo.RegisterCreatedObjectUndo(wall, "Create WeaponCollisionWall");
            }

            if (parent != null && wall.transform.parent != parent)
                wall.transform.SetParent(parent, true);

            wall.transform.position = new Vector3(0f, 1.25f, 3.5f);
            wall.transform.localScale = new Vector3(4f, 2.5f, 0.4f);
            wall.layer = EnvironmentLayerIndex;

            var renderer = wall.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                var mat = new Material(renderer.sharedMaterial);
                mat.color = new Color(0.45f, 0.55f, 0.65f);
                renderer.sharedMaterial = mat;
            }
        }

        static void EnsureEnvironmentLayer()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
                return;

            var tagManager = new SerializedObject(assets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            if (layers == null || layers.arraySize <= EnvironmentLayerIndex)
                return;

            SerializedProperty slot = layers.GetArrayElementAtIndex(EnvironmentLayerIndex);
            if (slot == null)
                return;

            if (string.IsNullOrEmpty(slot.stringValue) || slot.stringValue == EnvironmentLayerName)
            {
                slot.stringValue = EnvironmentLayerName;
                tagManager.ApplyModifiedProperties();
            }
        }

        static GameObject GetOrCreateRoot(string name)
        {
            var existing = GameObject.Find(name);
            if (existing != null)
                return existing;
            return new GameObject(name);
        }
    }
}
#endif
