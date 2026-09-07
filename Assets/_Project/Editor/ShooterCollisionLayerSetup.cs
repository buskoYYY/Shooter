#if UNITY_EDITOR
using System.Collections.Generic;
using KINEMATION.FPSAnimationFramework.Runtime.Core;
using KINEMATION.FPSAnimationFramework.Runtime.Layers.CollisionLayer;
using KINEMATION.Shared.KAnimationCore.Runtime.Core;
using KINEMATION.Shared.KAnimationCore.Runtime.Rig;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Project.Editor
{
    /// <summary>
    /// Task 2.5 — wire FPS AF CollisionLayer so weapons lift against walls.
    /// </summary>
    public static class ShooterCollisionLayerSetup
    {
        const string ProfilePath = "Assets/_Project/FPS/AnimatorProfile_CharacterModel.asset";
        const string EnvironmentLayerName = "Environment";
        const int EnvironmentLayerIndex = 6;
        const string TestWallName = "WeaponCollisionWall";

        [MenuItem("Shooter/Project/Setup Weapon Collision Layer")]
        public static void SetupMenu()
        {
            EnsureEnvironmentLayer();

            var profile = AssetDatabase.LoadAssetAtPath<FPSAnimatorProfile>(ProfilePath);
            if (profile == null)
            {
                EditorUtility.DisplayDialog(
                    "Collision Layer",
                    "Не найден AnimatorProfile_CharacterModel.\nСначала Phase 2 / FPS setup.",
                    "OK");
                return;
            }

            CollisionLayerSettings layer = FindCollisionLayer(profile);
            bool created = layer == null;
            if (created)
            {
                layer = ScriptableObject.CreateInstance<CollisionLayerSettings>();
                layer.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                layer.name = nameof(CollisionLayerSettings);
                layer.isStandalone = false;
                AssetDatabase.AddObjectToAsset(layer, profile);
                if (profile.settings == null)
                    profile.settings = new List<FPSAnimatorLayerSettings>();
                profile.settings.Add(layer);
            }

            ConfigureLayer(layer, profile.rigAsset);
            profile.OnRigUpdated();
            EditorUtility.SetDirty(layer);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            PlaceOrUpdateTestWall();

            EditorUtility.DisplayDialog(
                "Collision Layer",
                created
                    ? "Добавлен CollisionLayer в AnimatorProfile_CharacterModel.\n\n" +
                      "• Layer mask: Environment (layer 6)\n" +
                      "• В сцене: куб WeaponCollisionWall\n\n" +
                      "Play → достань оружие → подойди вплотную к стене — ствол должен подняться."
                    : "CollisionLayer обновлён.\n\n" +
                      "Play → оружие → к стене WeaponCollisionWall.",
                "OK");
        }

        static CollisionLayerSettings FindCollisionLayer(FPSAnimatorProfile profile)
        {
            if (profile.settings == null)
                return null;

            for (int i = 0; i < profile.settings.Count; i++)
            {
                if (profile.settings[i] is CollisionLayerSettings collision)
                    return collision;
            }

            return null;
        }

        static void ConfigureLayer(CollisionLayerSettings layer, KRig rig)
        {
            layer.rigAsset = rig;
            layer.alpha = 1f;
            layer.linkDynamically = false;
            layer.isStandalone = false;
            layer.weaponIkBone = new KRigElement(-1, FPSANames.IkWeaponBone);
            layer.primaryPose = new KTransform(
                new Vector3(0f, 0.05f, -0.15f),
                new Quaternion(-0.67559016f, 0f, 0f, 0.7372773f));
            layer.secondaryPose = new KTransform(
                new Vector3(-0.12f, 0f, -0.15f),
                new Quaternion(0f, -0.70710677f, 0f, 0.70710677f));
            layer.targetSpace = ESpaceType.ComponentSpace;
            layer.useSecondaryPose = true;
            layer.smoothingSpeed = 12f;
            layer.layerMask = 1 << EnvironmentLayerIndex;
            layer.rayStartOffset = 0.5f;
            layer.barrelLength = 1.32f;

            layer.curveBlending = new List<CurveBlend>
            {
                new CurveBlend
                {
                    name = "FullBodyWeight",
                    mode = ECurveBlendMode.Mask,
                    clampMin = 0f,
                    source = ECurveSource.Animator
                }
            };
        }

        static void EnsureEnvironmentLayer()
        {
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            if (layers == null || layers.arraySize <= EnvironmentLayerIndex)
                return;

            SerializedProperty slot = layers.GetArrayElementAtIndex(EnvironmentLayerIndex);
            if (slot == null)
                return;

            string current = slot.stringValue;
            if (!string.IsNullOrEmpty(current) && current != EnvironmentLayerName)
            {
                Debug.LogWarning(
                    $"[Shooter] User layer {EnvironmentLayerIndex} already named '{current}'. " +
                    $"CollisionLayer mask still uses bit {EnvironmentLayerIndex}. " +
                    "Put walls on that layer or reconfigure the mask.");
                return;
            }

            if (current != EnvironmentLayerName)
            {
                slot.stringValue = EnvironmentLayerName;
                tagManager.ApplyModifiedProperties();
                Debug.Log($"[Shooter] Named user layer {EnvironmentLayerIndex} → {EnvironmentLayerName}.");
            }
        }

        static void PlaceOrUpdateTestWall()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            GameObject wall = GameObject.Find(TestWallName);
            if (wall == null)
            {
                wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = TestWallName;
                Undo.RegisterCreatedObjectUndo(wall, "Create WeaponCollisionWall");
            }

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

            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
#endif
