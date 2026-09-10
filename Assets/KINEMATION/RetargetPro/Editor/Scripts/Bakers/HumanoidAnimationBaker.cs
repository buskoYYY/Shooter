// Copyright (c) 2026 KINEMATION.
// All rights reserved.

using System;
using System.Collections.Generic;
using KINEMATION.Shared.KAnimationCore.Runtime.Rig;
using UnityEditor;
using UnityEngine;

namespace KINEMATION.RetargetPro.Editor.Scripts.Bakers
{
    public class HumanoidAnimationBaker : IRetargetProBaker
    {
        private sealed class AnimatorQuaternionCurves
        {
            public EditorCurveBinding xBinding;
            public EditorCurveBinding yBinding;
            public EditorCurveBinding zBinding;
            public EditorCurveBinding wBinding;
            public AnimationCurve xCurve;
            public AnimationCurve yCurve;
            public AnimationCurve zCurve;
            public AnimationCurve wCurve;

            public bool IsComplete =>
                xCurve != null && yCurve != null && zCurve != null && wCurve != null;
        }

        public const string BakerId = "Humanoid";

        private const float TimeEpsilon = 0.000001f;

        private static readonly string[] RootPositionPropertyNames = { "RootT.x", "RootT.y", "RootT.z" };
        private static readonly string[] RootRotationPropertyNames = { "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
        private static readonly HashSet<string> MusclePropertyNames = BuildMusclePropertyNames();

        public string Id => BakerId;
        public string DisplayName => "Humanoid";

        private HumanPoseHandler _poseHandler;
        private HumanPose _pose;
        private AnimationCurve[] _muscleCurves;
        private AnimationCurve[] _rootPositionCurves;
        private AnimationCurve[] _rootRotationCurves;
        private float[] _lastMuscleValues;
        private Vector3 _lastRootPosition;
        private Quaternion _lastRootRotation;
        private bool _hasMuscleSample;
        private bool _hasRootPositionSample;
        private bool _hasRootRotationSample;
        private bool _initialized;

        public bool TryGetRootMotionTransformPath(out string path)
        {
            path = string.Empty;
            return false;
        }

        public void Initialize(KRigComponent rigComponent, KRigElement rootMotionBone)
        {
            _initialized = false;
            _poseHandler = null;
            _pose = new HumanPose();
            _muscleCurves = null;
            _rootPositionCurves = null;
            _rootRotationCurves = null;
            _lastMuscleValues = null;
            _lastRootPosition = Vector3.zero;
            _lastRootRotation = Quaternion.identity;
            _hasMuscleSample = false;
            _hasRootPositionSample = false;
            _hasRootRotationSample = false;
            
            Animator animator = rigComponent.GetComponent<Animator>();
            if (animator == null || animator.isHuman) return;
            
            _poseHandler = new HumanPoseHandler(animator.avatar, animator.transform);

            int muscleCount = HumanTrait.MuscleCount;
            _muscleCurves = new AnimationCurve[muscleCount];
            _lastMuscleValues = new float[muscleCount];
            for (int i = 0; i < muscleCount; i++)
            {
                _muscleCurves[i] = new AnimationCurve();
            }

            _rootPositionCurves = new[]
            {
                new AnimationCurve(),
                new AnimationCurve(),
                new AnimationCurve()
            };

            _rootRotationCurves = new[]
            {
                new AnimationCurve(),
                new AnimationCurve(),
                new AnimationCurve(),
                new AnimationCurve()
            };

            _initialized = true;
        }

        public void BakeAnimationFrame(float time)
        {
            if (!_initialized || _poseHandler == null)
            {
                return;
            }

            _poseHandler.GetHumanPose(ref _pose);

            for (int i = 0; i < _muscleCurves.Length; i++)
            {
                float fallback = _hasMuscleSample ? _lastMuscleValues[i] : 0f;
                float value = SanitizeMuscle(_pose.muscles[i], fallback);
                AddLinearKey(_muscleCurves[i], time, value);
                _lastMuscleValues[i] = value;
            }
            _hasMuscleSample = true;

            Vector3 rootPosition = SanitizeVector3(_pose.bodyPosition,
                _hasRootPositionSample ? _lastRootPosition : Vector3.zero);
            AddLinearKey(_rootPositionCurves[0], time, rootPosition.x);
            AddLinearKey(_rootPositionCurves[1], time, rootPosition.y);
            AddLinearKey(_rootPositionCurves[2], time, rootPosition.z);
            _lastRootPosition = rootPosition;
            _hasRootPositionSample = true;

            Quaternion normalizedBodyRotation = NormalizeSafe(_pose.bodyRotation,
                _hasRootRotationSample ? _lastRootRotation : Quaternion.identity);
            if (_hasRootRotationSample && Quaternion.Dot(_lastRootRotation, normalizedBodyRotation) < 0f)
            {
                normalizedBodyRotation = NegateQuaternion(normalizedBodyRotation);
            }

            AddLinearKey(_rootRotationCurves[0], time, normalizedBodyRotation.x);
            AddLinearKey(_rootRotationCurves[1], time, normalizedBodyRotation.y);
            AddLinearKey(_rootRotationCurves[2], time, normalizedBodyRotation.z);
            AddLinearKey(_rootRotationCurves[3], time, normalizedBodyRotation.w);
            _lastRootRotation = normalizedBodyRotation;
            _hasRootRotationSample = true;
        }

        public void WriteToClip(AnimationClip clip)
        {
            if (!_initialized || clip == null)
            {
                return;
            }

            for (int i = 0; i < _muscleCurves.Length; i++)
            {
                EditorCurveBinding binding = EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator),
                    HumanTrait.MuscleName[i]);
                AnimationUtility.SetEditorCurve(clip, binding, _muscleCurves[i]);
            }
        }

        public void WriteRootMotion(AnimationClip source, AnimationClip target)
        {
            if (!_initialized || target == null)
            {
                return;
            }

            for (int i = 0; i < _rootPositionCurves.Length; i++)
            {
                OptimizeCurveKeepingEndpoints(_rootPositionCurves[i]);

                EditorCurveBinding binding = EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator),
                    RootPositionPropertyNames[i]);
                AnimationUtility.SetEditorCurve(target, binding, _rootPositionCurves[i]);
            }

            for (int i = 0; i < _rootRotationCurves.Length; i++)
            {
                EditorCurveBinding binding = EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator),
                    RootRotationPropertyNames[i]);
                AnimationUtility.SetEditorCurve(target, binding, _rootRotationCurves[i]);
            }

            CopySupplementalAnimatorCurves(source, target);
            //EnsureAnimatorQuaternionContinuity(target);
        }

        private static Animator ResolveBakingAnimator(GameObject target, Transform[] hierarchy)
        {
            if (target == null)
            {
                return null;
            }

            Animator[] animators = target.GetComponentsInChildren<Animator>(true);
            Animator best = null;
            int bestScore = int.MinValue;

            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null || !animator.isHuman || animator.avatar == null || !animator.avatar.isValid)
                {
                    continue;
                }

                int score = ScoreAnimatorAgainstHierarchy(animator.transform, hierarchy);
                if (animator.transform == target.transform)
                {
                    score += 4;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = animator;
                }
            }

            return best != null ? best : target.GetComponentInChildren<Animator>(true);
        }

        private static int ScoreAnimatorAgainstHierarchy(Transform animatorRoot, Transform[] hierarchy)
        {
            if (animatorRoot == null || hierarchy == null || hierarchy.Length == 0)
            {
                return 0;
            }

            int score = 0;
            for (int i = 0; i < hierarchy.Length; i++)
            {
                Transform bone = hierarchy[i];
                if (bone == null)
                {
                    continue;
                }

                if (bone == animatorRoot || bone.IsChildOf(animatorRoot))
                {
                    score++;
                }
            }

            return score;
        }

        private void AddLinearKey(AnimationCurve curve, float time, float value)
        {
            int index = curve.length - 1;
            if (index >= 0 && Mathf.Abs(curve.keys[index].time - time) <= TimeEpsilon)
            {
                Keyframe key = curve.keys[index];
                key.value = value;
                curve.MoveKey(index, key);
            }
            else
            {
                index = curve.AddKey(time, value);
            }

            if (index < 0 || index >= curve.keys.Length)
            {
                return;
            }

            AnimationUtility.SetKeyLeftTangentMode(curve, index, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, index, AnimationUtility.TangentMode.Linear);
        }

        private static void OptimizeCurveKeepingEndpoints(AnimationCurve curve)
        {
            if (curve == null || curve.length <= 2)
            {
                return;
            }

            int index = 1;
            while (index < curve.length - 1)
            {
                Keyframe[] keys = curve.keys;
                float previous = keys[index - 1].value;
                float current = keys[index].value;
                float next = keys[index + 1].value;

                if (!Mathf.Approximately(previous, current) || !Mathf.Approximately(current, next))
                {
                    index++;
                    continue;
                }

                curve.RemoveKey(index);

                int leftIndex = index - 1;
                if (leftIndex >= 0 && leftIndex < curve.length)
                {
                    AnimationUtility.SetKeyRightTangentMode(curve, leftIndex, AnimationUtility.TangentMode.Constant);
                }

                int rightIndex = index;
                if (rightIndex >= 0 && rightIndex < curve.length)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, rightIndex, AnimationUtility.TangentMode.Constant);
                }
            }
        }

        private static void CopySupplementalAnimatorCurves(AnimationClip source, AnimationClip target)
        {
            if (source == null || target == null)
            {
                return;
            }

            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(source);
            for (int i = 0; i < bindings.Length; i++)
            {
                EditorCurveBinding binding = bindings[i];
                if (binding.type != typeof(Animator) || !string.IsNullOrEmpty(binding.path))
                {
                    continue;
                }

                string propertyName = binding.propertyName;
                if (!IsSupportedSupplementalAnimatorProperty(propertyName))
                {
                    continue;
                }

                AnimationCurve sourceCurve = AnimationUtility.GetEditorCurve(source, binding);
                if (sourceCurve == null)
                {
                    continue;
                }

                AnimationCurve copiedCurve = new AnimationCurve(sourceCurve.keys)
                {
                    preWrapMode = sourceCurve.preWrapMode,
                    postWrapMode = sourceCurve.postWrapMode
                };
                AnimationUtility.SetEditorCurve(target, binding, copiedCurve);
            }
        }

        private static bool IsSupportedSupplementalAnimatorProperty(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                return false;
            }

            if (IsRootMotionProperty(propertyName) || IsMuscleProperty(propertyName))
            {
                return false;
            }

            return IsHumanoidIkGoalProperty(propertyName) || IsHumanoidLookAtProperty(propertyName);
        }

        private static bool IsHumanoidIkGoalProperty(string propertyName)
        {
            return propertyName.StartsWith("LeftHandQ.", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.StartsWith("LeftHandT.", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.StartsWith("RightHandQ.", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.StartsWith("RightHandT.", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.StartsWith("LeftFootQ.", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.StartsWith("LeftFootT.", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.StartsWith("RightFootQ.", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.StartsWith("RightFootT.", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsHumanoidLookAtProperty(string propertyName)
        {
            return propertyName.StartsWith("LookAt", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRootMotionProperty(string propertyName)
        {
            return propertyName.StartsWith("RootT.", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.StartsWith("RootQ.", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMuscleProperty(string propertyName)
        {
            if (MusclePropertyNames.Contains(propertyName))
            {
                return true;
            }

            string canonical = CanonicalizeDottedHandMuscleName(propertyName);
            return !string.IsNullOrEmpty(canonical) && MusclePropertyNames.Contains(canonical);
        }

        private static string CanonicalizeDottedHandMuscleName(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                return null;
            }

            if (propertyName.StartsWith("LeftHand.", StringComparison.Ordinal))
            {
                return "Left " + propertyName.Substring("LeftHand.".Length).Replace('.', ' ');
            }

            if (propertyName.StartsWith("RightHand.", StringComparison.Ordinal))
            {
                return "Right " + propertyName.Substring("RightHand.".Length).Replace('.', ' ');
            }

            return null;
        }

        private static HashSet<string> BuildMusclePropertyNames()
        {
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < HumanTrait.MuscleCount; i++)
            {
                names.Add(HumanTrait.MuscleName[i]);
            }

            return names;
        }

        private static void EnsureAnimatorQuaternionContinuity(AnimationClip clip)
        {
            if (clip == null)
            {
                return;
            }

            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            Dictionary<string, AnimatorQuaternionCurves> groups =
                new Dictionary<string, AnimatorQuaternionCurves>(StringComparer.Ordinal);

            for (int i = 0; i < bindings.Length; i++)
            {
                EditorCurveBinding binding = bindings[i];
                if (binding.type != typeof(Animator) || !string.IsNullOrEmpty(binding.path))
                {
                    continue;
                }

                if (!TryGetQuaternionPropertyGroup(binding.propertyName, out string groupName, out char component))
                {
                    continue;
                }

                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null)
                {
                    continue;
                }

                if (!groups.TryGetValue(groupName, out AnimatorQuaternionCurves group))
                {
                    group = new AnimatorQuaternionCurves();
                    groups.Add(groupName, group);
                }

                switch (component)
                {
                    case 'x':
                        group.xBinding = binding;
                        group.xCurve = curve;
                        break;
                    case 'y':
                        group.yBinding = binding;
                        group.yCurve = curve;
                        break;
                    case 'z':
                        group.zBinding = binding;
                        group.zCurve = curve;
                        break;
                    case 'w':
                        group.wBinding = binding;
                        group.wCurve = curve;
                        break;
                }
            }

            foreach (KeyValuePair<string, AnimatorQuaternionCurves> pair in groups)
            {
                AnimatorQuaternionCurves group = pair.Value;
                if (!group.IsComplete || !AreQuaternionCurvesSynchronized(group))
                {
                    continue;
                }

                Keyframe[] xKeys = group.xCurve.keys;
                Keyframe[] yKeys = group.yCurve.keys;
                Keyframe[] zKeys = group.zCurve.keys;
                Keyframe[] wKeys = group.wCurve.keys;

                Quaternion previous = NormalizeSafe(
                    new Quaternion(xKeys[0].value, yKeys[0].value, zKeys[0].value, wKeys[0].value),
                    Quaternion.identity);

                for (int keyIndex = 1; keyIndex < xKeys.Length; keyIndex++)
                {
                    Quaternion current = NormalizeSafe(
                        new Quaternion(xKeys[keyIndex].value, yKeys[keyIndex].value, zKeys[keyIndex].value,
                            wKeys[keyIndex].value),
                        previous);

                    if (Quaternion.Dot(previous, current) < 0f)
                    {
                        current = NegateQuaternion(current);
                        SetCurveKeyValue(group.xCurve, keyIndex, current.x);
                        SetCurveKeyValue(group.yCurve, keyIndex, current.y);
                        SetCurveKeyValue(group.zCurve, keyIndex, current.z);
                        SetCurveKeyValue(group.wCurve, keyIndex, current.w);
                    }

                    previous = current;
                }

                AnimationUtility.SetEditorCurve(clip, group.xBinding, group.xCurve);
                AnimationUtility.SetEditorCurve(clip, group.yBinding, group.yCurve);
                AnimationUtility.SetEditorCurve(clip, group.zBinding, group.zCurve);
                AnimationUtility.SetEditorCurve(clip, group.wBinding, group.wCurve);
            }
        }

        private static bool TryGetQuaternionPropertyGroup(string propertyName, out string groupName, out char component)
        {
            groupName = string.Empty;
            component = '\0';

            if (string.IsNullOrEmpty(propertyName) || propertyName.Length < 3)
            {
                return false;
            }

            int dotIndex = propertyName.Length - 2;
            if (propertyName[dotIndex] != '.')
            {
                return false;
            }

            char axis = char.ToLowerInvariant(propertyName[propertyName.Length - 1]);
            if (axis != 'x' && axis != 'y' && axis != 'z' && axis != 'w')
            {
                return false;
            }

            groupName = propertyName.Substring(0, dotIndex);
            if (!groupName.EndsWith("Q", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            component = axis;
            return true;
        }

        private static bool AreQuaternionCurvesSynchronized(AnimatorQuaternionCurves group)
        {
            if (group == null || !group.IsComplete)
            {
                return false;
            }

            int keyCount = group.xCurve.length;
            if (keyCount == 0 || group.yCurve.length != keyCount || group.zCurve.length != keyCount ||
                group.wCurve.length != keyCount)
            {
                return false;
            }

            Keyframe[] xKeys = group.xCurve.keys;
            Keyframe[] yKeys = group.yCurve.keys;
            Keyframe[] zKeys = group.zCurve.keys;
            Keyframe[] wKeys = group.wCurve.keys;

            for (int i = 0; i < keyCount; i++)
            {
                float time = xKeys[i].time;
                if (Mathf.Abs(yKeys[i].time - time) > TimeEpsilon ||
                    Mathf.Abs(zKeys[i].time - time) > TimeEpsilon ||
                    Mathf.Abs(wKeys[i].time - time) > TimeEpsilon)
                {
                    return false;
                }
            }

            return true;
        }

        private static void SetCurveKeyValue(AnimationCurve curve, int keyIndex, float value)
        {
            Keyframe key = curve.keys[keyIndex];
            key.value = value;
            curve.MoveKey(keyIndex, key);
        }

        private static float SanitizeMuscle(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return fallback;
            }

            return value;
        }

        private static Vector3 SanitizeVector3(Vector3 value, Vector3 fallback)
        {
            if (float.IsNaN(value.x) || float.IsInfinity(value.x))
            {
                value.x = fallback.x;
            }

            if (float.IsNaN(value.y) || float.IsInfinity(value.y))
            {
                value.y = fallback.y;
            }

            if (float.IsNaN(value.z) || float.IsInfinity(value.z))
            {
                value.z = fallback.z;
            }

            return value;
        }

        private static Quaternion NormalizeSafe(Quaternion rotation, Quaternion fallback)
        {
            if (float.IsNaN(rotation.x) || float.IsNaN(rotation.y) || float.IsNaN(rotation.z) ||
                float.IsNaN(rotation.w))
            {
                return fallback;
            }

            float magnitude = Mathf.Sqrt(rotation.x * rotation.x + rotation.y * rotation.y +
                                         rotation.z * rotation.z + rotation.w * rotation.w);
            if (magnitude <= TimeEpsilon)
            {
                return fallback;
            }

            float invMagnitude = 1f / magnitude;
            return new Quaternion(rotation.x * invMagnitude, rotation.y * invMagnitude, rotation.z * invMagnitude,
                rotation.w * invMagnitude);
        }

        private static Quaternion NegateQuaternion(Quaternion rotation)
        {
            return new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
        }
    }
}