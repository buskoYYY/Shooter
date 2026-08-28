using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Shooter.Project.Editor
{
    /// <summary>
    /// Writes Animation Events onto locomotion / jump clips so EventReceiver on Character_model fires.
    /// Menu: Shooter / Bake Animation Events (Step, Jump, Falldown)
    /// </summary>
    public static class ShooterAnimEventBaker
    {
        const string LocomotionRoot = "Assets/Demo/Animations/Locomotion/Humanoid";

        [MenuItem("Shooter/Bake Animation Events (Step, Jump, Falldown)")]
        public static void BakeMenu()
        {
            int clips = BakeAll();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "Animation events",
                "Updated " + clips + " clips.\nWire EventReceiver UnityEvents on Character_model.",
                "OK");
        }

        public static int BakeAll()
        {
            int count = 0;
            string[] guids = AssetDatabase.FindAssets("t:Model t:AnimationClip", new[] { LocomotionRoot });
            var seen = new HashSet<string>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!seen.Add(path))
                    continue;

                if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".FBX", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (BakeFbx(path))
                        count++;
                    continue;
                }

                if (path.EndsWith(".anim", System.StringComparison.OrdinalIgnoreCase))
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    if (clip != null && BakeStandaloneClip(clip, Path.GetFileNameWithoutExtension(path)))
                        count++;
                }
            }

            return count;
        }

        static bool BakeFbx(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                return false;

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
                clips = importer.defaultClipAnimations;

            if (clips == null || clips.Length == 0)
                return false;

            bool changed = false;
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationEvent[] events = BuildEvents(clips[i].name, clips[i].firstFrame, clips[i].lastFrame);
                if (events == null)
                    continue;

                clips[i].events = events;
                changed = true;
            }

            if (!changed)
                return false;

            importer.clipAnimations = clips;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            return true;
        }

        static bool BakeStandaloneClip(AnimationClip clip, string name)
        {
            AnimationEvent[] events = BuildEvents(name, 0f, clip.length * 30f);
            if (events == null)
                return false;

            AnimationUtility.SetAnimationEvents(clip, events);
            EditorUtility.SetDirty(clip);
            return true;
        }

        static AnimationEvent[] BuildEvents(string clipName, float firstFrame, float lastFrame)
        {
            if (string.IsNullOrEmpty(clipName))
                return null;

            string n = clipName;
            if (IsIdleOrNoFoot(n))
                return null;

            float duration = Mathf.Max(0.01f, (lastFrame - firstFrame) / 30f);

            if (n.IndexOf("JumpStart", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return new[] { Ev("Jump", Mathf.Min(0.08f, duration * 0.15f)) };

            if (n.IndexOf("JumpEnd", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return new[] { Ev("Falldown", Mathf.Clamp(duration * 0.22f, 0.08f, duration * 0.45f)) };

            if (n.IndexOf("JumpLoop", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return System.Array.Empty<AnimationEvent>();

            if (!LooksLikeLocomotion(n))
                return null;

            if (duration < 0.45f)
                return new[] { Ev("Step", duration * 0.5f) };

            return new[]
            {
                Ev("Step", duration * 0.28f),
                Ev("Step", duration * 0.72f)
            };
        }

        static bool IsIdleOrNoFoot(string n)
        {
            return Contains(n, "Idle") || Contains(n, "Static") || Contains(n, "Reload") ||
                   Contains(n, "Throw") || Contains(n, "Stab") || Contains(n, "Grenade");
        }

        static bool LooksLikeLocomotion(string n)
        {
            return Contains(n, "Run") || Contains(n, "Walk") || Contains(n, "Jog") ||
                   Contains(n, "Sprint") || Contains(n, "Strafe") || Contains(n, "Turn") ||
                   Contains(n, "Prone_Forward") || Contains(n, "Sliding");
        }

        static bool Contains(string n, string token)
        {
            return n.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static AnimationEvent Ev(string functionName, float time)
        {
            return new AnimationEvent
            {
                functionName = functionName,
                time = time,
                messageOptions = SendMessageOptions.DontRequireReceiver
            };
        }
    }
}
