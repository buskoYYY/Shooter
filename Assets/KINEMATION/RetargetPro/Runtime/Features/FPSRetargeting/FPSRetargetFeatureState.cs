// Copyright (c) 2026 KINEMATION.
// All rights reserved.

using KINEMATION.Shared.KAnimationCore.Runtime.Core;
using KINEMATION.Shared.KAnimationCore.Runtime.Rig;
using UnityEngine;

#if UNITY_EDITOR
using KINEMATION.Shared.KAnimationCore.Editor;
using UnityEditor;
#endif

namespace KINEMATION.RetargetPro.Runtime.Features.FPSRetargeting
{
    public class FPSRetargetFeatureState : RetargetFeatureState
    {
        protected KTransformChain _sourceRightArmChain;
        protected KTransformChain _sourceLeftArmChain;
        
        protected KTransformChain _targetRightArmChain;
        protected KTransformChain _targetLeftArmChain;
        
        protected KTransformChain _sourceWeaponChain;
        protected KTransformChain _targetWeaponChain;

        protected FPSRetargetFeature _asset;

        protected KTransform _rightHandIk = KTransform.Identity;
        protected KTransform _leftHandIk = KTransform.Identity;

        protected KTransform _rightPole = KTransform.Identity;
        protected KTransform _leftPole = KTransform.Identity;

        protected Vector3 _cachedTargetWeaponPosition;

        private static void CacheWeaponChainAtRootPose(KTransformChain weaponChain, Transform characterRoot)
        {
            if (weaponChain == null || characterRoot == null || weaponChain.transformChain.Count == 0)
            {
                return;
            }

            Transform weaponBone = weaponChain.transformChain[0];
            if (weaponBone == null)
            {
                return;
            }

            Vector3 localPosition = weaponBone.localPosition;
            Quaternion localRotation = weaponBone.localRotation;

            try
            {
                weaponBone.position = characterRoot.position;
                weaponBone.rotation = characterRoot.rotation;
                weaponChain.CacheTransforms(ESpaceType.ComponentSpace, characterRoot);
            }
            finally
            {
                weaponBone.localPosition = localPosition;
                weaponBone.localRotation = localRotation;
            }
        }
        
        protected void RetargetChains(KTransformChain source, KTransformChain target)
        {
            if (source == null || target == null) return;
            if (source.transformChain.Count != target.transformChain.Count) return;
            
            int count = source.transformChain.Count;
            for (int i = 0; i < count; i++)
            {
                KTransform cachedSourcePose = source.cachedTransforms[i];
                KTransform cachedTargetPose = target.cachedTransforms[i];
                
                var baseRotation = GetTargetRoot().rotation * target.cachedTransforms[i].rotation;

                // Compute the delta dynamically, as bone sizes might differ.
                Quaternion delta = Quaternion.Inverse(cachedSourcePose.rotation) * cachedTargetPose.rotation;
                var outRotation = source.transformChain[i].rotation * delta;
                outRotation = Quaternion.Slerp(baseRotation, outRotation, _asset.featureWeight);
                
                target.transformChain[i].rotation = outRotation;
            }
        }
        
        protected void ApplyIK(KTransformChain source, KTransformChain target, Vector3 effectorOffset, 
            ref KTransform ikTarget, Vector3 poleOffset, ref KTransform pole)
        {
            RetargetChains(source, target);
            
            if (_sourceWeaponChain == null || _targetWeaponChain == null) return;
            if (_sourceWeaponChain.transformChain.Count != _targetWeaponChain.transformChain.Count) return;

            Transform sourceWeapon = _sourceWeaponChain.transformChain[0];
            Transform targetWeapon = _targetWeaponChain.transformChain[0];

            if (sourceWeapon == null || targetWeapon == null) return;
            
            if (source == null || target == null) return;
            if (source.transformChain.Count != target.transformChain.Count) return;

            int count = source.transformChain.Count;
            
            Transform tip = target.transformChain[count - 1];
            Transform mid = target.transformChain[count - 2];
            Transform root = target.transformChain[count - 3];
            
            ikTarget = new KTransform(source.transformChain[count - 1])
            {
                rotation = tip.rotation
            };
            
            KTransform targetWeaponTransform = new KTransform(targetWeapon);
            
            ikTarget = new KTransform(sourceWeapon).GetRelativeTransform(ikTarget, false);
            ikTarget = targetWeaponTransform.GetWorldTransform(ikTarget, false);
            ikTarget.position = ikTarget.TransformPoint(effectorOffset, false);
            
            KTransform rootBone = new KTransform(GetTargetRoot());
            
            pole = new KTransform(mid);
            pole.position = KAnimationMath.MoveInSpace(rootBone, pole, poleOffset, 1f);
            
            KTwoBoneIkData twoBoneIkData = new KTwoBoneIkData()
            {
                root = new KTransform(root),
                mid = new KTransform(mid),
                tip = new KTransform(tip),
                hint = pole,
                target = ikTarget,
                hasValidHint = true,
                rotWeight = _asset.featureWeight,
                posWeight = _asset.featureWeight,
                hintWeight = 1f
            };
            
            KTwoBoneIK.Solve(ref twoBoneIkData);

            root.rotation = twoBoneIkData.root.rotation;
            mid.rotation = twoBoneIkData.mid.rotation;
            tip.rotation = twoBoneIkData.tip.rotation;
        }

        public override bool IsValid()
        {
            return sourceRigComponent != null &&
                   targetRigComponent != null &&
                   _sourceRightArmChain != null &&
                   _sourceRightArmChain.IsValid() &&
                   _sourceLeftArmChain != null &&
                   _sourceLeftArmChain.IsValid() &&
                   _targetRightArmChain != null &&
                   _targetRightArmChain.IsValid() &&
                   _targetLeftArmChain != null &&
                   _targetLeftArmChain.IsValid() &&
                   _sourceWeaponChain != null &&
                   _sourceWeaponChain.IsValid() &&
                   _targetWeaponChain != null &&
                   _targetWeaponChain.IsValid();
        }

        protected override void Initialize(RetargetFeature newAsset)
        {
            _asset = newAsset as FPSRetargetFeature;
            if (_asset == null) return;

            Transform sourceRoot = GetSourceRoot();
            Transform targetRoot = GetTargetRoot();

            // 1. Initialize all the bone chains.

            _sourceRightArmChain = RetargetUtility.GetTransformChain(sourceRigComponent, _asset.sourceRig, 
                _asset.sourceRightArm);
            _sourceLeftArmChain = RetargetUtility.GetTransformChain(sourceRigComponent, _asset.sourceRig, 
                _asset.sourceLeftArm);
            
            _targetRightArmChain = RetargetUtility.GetTransformChain(targetRigComponent, _asset.targetRig, 
                _asset.targetRightArm);
            _targetLeftArmChain = RetargetUtility.GetTransformChain(targetRigComponent, _asset.targetRig, 
                _asset.targetLeftArm);
            
            _sourceWeaponChain = RetargetUtility.GetTransformChain(sourceRigComponent, _asset.sourceRig, 
                _asset.sourceWeapon);
            _targetWeaponChain = RetargetUtility.GetTransformChain(targetRigComponent, _asset.targetRig, 
                _asset.targetWeapon);

            if (_sourceRightArmChain == null || !_sourceRightArmChain.IsValid())
            {
                return;
            }

            if (_sourceLeftArmChain == null || !_sourceLeftArmChain.IsValid())
            {
                return;
            }

            if (_targetRightArmChain == null || !_targetRightArmChain.IsValid())
            {
                return;
            }

            if (_targetLeftArmChain == null || !_targetLeftArmChain.IsValid())
            {
                return;
            }

            if (_sourceWeaponChain == null || !_sourceWeaponChain.IsValid())
            {
                return;
            }

            if (_targetWeaponChain == null || !_targetWeaponChain.IsValid())
            {
                return;
            }

            // 2. Cache the initial poses.

            Transform targetWeaponBone = _targetWeaponChain.transformChain[0];
            if (targetWeaponBone == null)
            {
                return;
            }

            _cachedTargetWeaponPosition = targetRoot.InverseTransformPoint(targetWeaponBone.position);

            CacheWeaponChainAtRootPose(_sourceWeaponChain, sourceRoot);
            _sourceRightArmChain.CacheTransforms(ESpaceType.ComponentSpace, sourceRoot);
            _sourceLeftArmChain.CacheTransforms(ESpaceType.ComponentSpace, sourceRoot);

            CacheWeaponChainAtRootPose(_targetWeaponChain, targetRoot);
            _targetRightArmChain.CacheTransforms(ESpaceType.ComponentSpace, targetRoot);
            _targetLeftArmChain.CacheTransforms(ESpaceType.ComponentSpace, targetRoot);
        }
        
        public override void Retarget(float time = 0f)
        {
            Transform sourceWeaponBone = _sourceWeaponChain.transformChain[0];
            Transform targetWeaponBone = _targetWeaponChain.transformChain[0];
            
            RetargetChains(_sourceWeaponChain, _targetWeaponChain);

            Vector3 cachedWeaponPosition = _cachedTargetWeaponPosition;
            Vector3 weaponPosition = GetSourceRoot().InverseTransformPoint(sourceWeaponBone.position);

            weaponPosition += _asset.weaponOffset;
            weaponPosition = Vector3.Lerp(cachedWeaponPosition, weaponPosition, _asset.featureWeight);
            targetWeaponBone.position = GetTargetRoot().TransformPoint(weaponPosition);
            
            ApplyIK(_sourceRightArmChain, _targetRightArmChain, _asset.rightHandOffset, ref _rightHandIk, 
                _asset.rightPoleOffset, ref _rightPole);
            ApplyIK(_sourceLeftArmChain, _targetLeftArmChain, _asset.leftHandOffset, ref _leftHandIk, 
                _asset.leftPoleOffset, ref _leftPole);
        }

#if UNITY_EDITOR
        private static float GetSelectionHandleSize(Vector3 position)
        {
            return Mathf.Max(0.03f, HandleUtility.GetHandleSize(position) * 0.08f);
        }

        private static float SignedTriangleArea(Vector2 point, Vector2 vertex1, Vector2 vertex2)
        {
            return (point.x - vertex2.x) * (vertex1.y - vertex2.y) -
                   (vertex1.x - vertex2.x) * (point.y - vertex2.y);
        }

        private static bool IsPointInTriangle(Vector2 point, Vector2 vertex1, Vector2 vertex2, Vector2 vertex3)
        {
            if (Mathf.Abs(SignedTriangleArea(vertex1, vertex2, vertex3)) <= 0.001f)
            {
                return false;
            }

            float area1 = SignedTriangleArea(point, vertex1, vertex2);
            float area2 = SignedTriangleArea(point, vertex2, vertex3);
            float area3 = SignedTriangleArea(point, vertex3, vertex1);

            bool hasNegative = area1 < 0f || area2 < 0f || area3 < 0f;
            bool hasPositive = area1 > 0f || area2 > 0f || area3 > 0f;
            return !(hasNegative && hasPositive);
        }

        private static bool IsMouseInsideProjectedQuad(Vector2 mousePosition, Vector3 center, Vector3 axis1,
            Vector3 axis2)
        {
            Vector2 vertex1 = HandleUtility.WorldToGUIPoint(center - axis1 - axis2);
            Vector2 vertex2 = HandleUtility.WorldToGUIPoint(center + axis1 - axis2);
            Vector2 vertex3 = HandleUtility.WorldToGUIPoint(center + axis1 + axis2);
            Vector2 vertex4 = HandleUtility.WorldToGUIPoint(center - axis1 + axis2);

            return IsPointInTriangle(mousePosition, vertex1, vertex2, vertex3) ||
                   IsPointInTriangle(mousePosition, vertex1, vertex3, vertex4);
        }

        private static bool IsMouseInsideProjectedCube(Vector3 position, Quaternion rotation, float size)
        {
            Vector2 mousePosition = Event.current.mousePosition;
            float halfSize = size * 0.5f;

            Vector3 right = rotation * Vector3.right * halfSize;
            Vector3 up = rotation * Vector3.up * halfSize;
            Vector3 forward = rotation * Vector3.forward * halfSize;

            return IsMouseInsideProjectedQuad(mousePosition, position + forward, right, up) ||
                   IsMouseInsideProjectedQuad(mousePosition, position - forward, right, up) ||
                   IsMouseInsideProjectedQuad(mousePosition, position + right, forward, up) ||
                   IsMouseInsideProjectedQuad(mousePosition, position - right, forward, up) ||
                   IsMouseInsideProjectedQuad(mousePosition, position + up, right, forward) ||
                   IsMouseInsideProjectedQuad(mousePosition, position - up, right, forward);
        }

        private static void WireCubeHandleCap(int controlId, Vector3 position, Quaternion rotation, float size,
            EventType eventType)
        {
            switch (eventType)
            {
                case EventType.Layout:
                    float distance = IsMouseInsideProjectedCube(position, rotation, size)
                        ? 0f
                        : HandleUtility.DistanceToCube(position, rotation, size);
                    HandleUtility.AddControl(controlId, distance);
                    break;
                case EventType.Repaint:
                    Matrix4x4 originalMatrix = Handles.matrix;
                    try
                    {
                        Handles.matrix = Matrix4x4.TRS(position, rotation, Vector3.one);
                        Handles.DrawWireCube(Vector3.zero, Vector3.one * size);
                    }
                    finally
                    {
                        Handles.matrix = originalMatrix;
                    }
                    break;
            }
        }

        private static bool RenderBoneSelectionCube(Vector3 position, Quaternion rotation, float size, bool isActive)
        {
            return !isActive && Handles.Button(position, rotation, size, size, WireCubeHandleCap);
        }

        private void SelectHandle(bool weapon, bool rightHand, bool leftHand, bool rightPole, bool leftPole)
        {
            _asset.enableWeaponHandle = weapon;
            _asset.enableRightHandHandle = rightHand;
            _asset.enableLeftHandle = leftHand;
            _asset.enableRightPole = rightPole;
            _asset.enableLeftPole = leftPole;
        }

        private Vector3 RenderHandle(Transform space, Transform bone)
        {
            Vector3 handlePos = KTransformHandles.PositionHandle(bone.position, space.rotation, 
                KTransformHandles.PositionHandleSettings.Default);
            return space.InverseTransformPoint(handlePos) - space.InverseTransformPoint(bone.position);
        }

        private Vector3 RenderPoleHandle(Transform space, KTransform bone)
        {
            Vector3 handlePos = KTransformHandles.PositionHandle(bone.position, space.rotation, 
                KTransformHandles.PositionHandleSettings.Default);
            return space.InverseTransformPoint(handlePos) - space.InverseTransformPoint(bone.position);
        }
        
        private Vector3 RenderHandHandle(Transform space, KTransform bone)
        {
            Vector3 handlePos = KTransformHandles.PositionHandle(bone.position, space.rotation, 
                KTransformHandles.PositionHandleSettings.Default);
            Vector3 delta = bone.InverseTransformPoint(handlePos, false);

            if (Mathf.Approximately(delta.magnitude, 0f)) delta = Vector3.zero;
            return delta;
        }
        
        protected override void OnBoneRenderingSceneGUI()
        {
            RenderSourceAndTargetBoneChains(_sourceWeaponChain, _targetWeaponChain, TargetBoneChainColor);
            RenderSourceAndTargetBoneChains(_sourceRightArmChain, _targetRightArmChain, TargetBoneChainColor);
            RenderSourceAndTargetBoneChains(_sourceLeftArmChain, _targetLeftArmChain, TargetBoneChainColor);
        }

        protected override void OnTransformHandlesSceneGUI()
        {
            Transform root = GetTargetRoot();
            Transform weapon = _targetWeaponChain.transformChain[0];
            Transform rightHand = _targetRightArmChain.transformChain[^1];
            Transform leftHand = _targetLeftArmChain.transformChain[^1];

            float weaponHandleSize = GetSelectionHandleSize(weapon.position);
            float rightHandHandleSize = GetSelectionHandleSize(rightHand.position);
            float leftHandHandleSize = GetSelectionHandleSize(leftHand.position);
            float rightPoleHandleSize = GetSelectionHandleSize(rightHand.parent.position);
            float leftPoleHandleSize = GetSelectionHandleSize(leftHand.parent.position);
            var color = Handles.color;
            Handles.color = Color.cyan;
            
            if (RenderBoneSelectionCube(weapon.position, root.rotation, weaponHandleSize, _asset.enableWeaponHandle))
            {
                SelectHandle(true, false, false, false, false);
            }
            
            if (RenderBoneSelectionCube(rightHand.position, weapon.rotation, rightHandHandleSize,
                    _asset.enableRightHandHandle))
            {
                SelectHandle(false, true, false, false, false);
            }
            
            if (RenderBoneSelectionCube(leftHand.position, weapon.rotation, leftHandHandleSize, _asset.enableLeftHandle))
            {
                SelectHandle(false, false, true, false, false);
            }
            
            if (RenderBoneSelectionCube(rightHand.parent.position, root.rotation, rightPoleHandleSize,
                    _asset.enableRightPole))
            {
                SelectHandle(false, false, false, true, false);
            }
            
            if (RenderBoneSelectionCube(leftHand.parent.position, root.rotation, leftPoleHandleSize,
                    _asset.enableLeftPole))
            {
                SelectHandle(false, false, false, false, true);
            }
            
            if (_asset.enableWeaponHandle)
            {
                _asset.weaponOffset += RenderHandle(root, weapon);
            }
            
            if (_asset.enableRightHandHandle)
            {
                _asset.rightHandOffset += RenderHandHandle(weapon, _rightHandIk);
            }

            if (_asset.enableLeftHandle)
            {
                _asset.leftHandOffset += RenderHandHandle(weapon, _leftHandIk);
            }

            if (_asset.enableRightPole)
            {
                _asset.rightPoleOffset += RenderPoleHandle(root, _rightPole);
            }
            
            if (_asset.enableLeftPole)
            {
                _asset.leftPoleOffset += RenderPoleHandle(root, _leftPole);
            }
            
            Handles.color = Color.white;
            if (!_asset.enableWeaponHandle) Handles.Label(weapon.position, "Gun Goal");
            if (!_asset.enableRightHandHandle) Handles.Label(rightHand.position, "Right Hand Goal");
            if (!_asset.enableLeftHandle) Handles.Label(leftHand.position, "Left Hand Goal");
            
            if (!_asset.enableRightPole) Handles.Label(rightHand.parent.position, "Right Pole");
            if (!_asset.enableLeftPole) Handles.Label(leftHand.parent.position, "Left Pole");
            Handles.color = color;
        }
#endif
    }
}