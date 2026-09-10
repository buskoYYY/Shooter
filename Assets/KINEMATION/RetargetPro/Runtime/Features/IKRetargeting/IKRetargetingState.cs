// Copyright (c) 2026 KINEMATION.
// All rights reserved.

using KINEMATION.Shared.KAnimationCore.Runtime.Core;
using KINEMATION.Shared.KAnimationCore.Runtime.Rig;
using KINEMATION.RetargetPro.Runtime.Features.BasicRetargeting;
using UnityEngine;

#if UNITY_EDITOR
using KINEMATION.Shared.KAnimationCore.Editor;
using UnityEditor;
#endif

namespace KINEMATION.RetargetPro.Runtime.Features.IKRetargeting
{
    public class IKRetargetingState : BasicRetargetFeatureState
    {
        private IKRetargetFeature _asset;

        private int _sourceLength = 0;
        private int _targetLength = 0;

        private RetargetUtility.FABRIK_Data _chainIKData;
        private KTransform _ikGoal;
        private KTransform _ikPole;
        private int _jointIndex = -1;
        private Vector3 _localJointForward = Vector3.forward;
        private Vector3 _localJointUp = Vector3.up;

        private static Vector3 MoveTargetInSpace(KTransform space, Vector3 targetPosition, Vector3 offset)
        {
            KTransform target = new KTransform(targetPosition, Quaternion.identity, Vector3.one);
            return KAnimationMath.MoveInSpace(space, target, offset, 1f);
        }

        private KTransform ResolveEffectorOffsetSpace()
        {
            if (_asset == null)
            {
                return new KTransform(GetTargetRoot());
            }

            switch (_asset.effectorSpace)
            {
                case ESpaceType.BoneSpace:
                case ESpaceType.ParentBoneSpace:
                    return new KTransform(targetChain.transformChain[^1]);
                case ESpaceType.WorldSpace:
                    return KTransform.Identity;
                default:
                    return new KTransform(GetTargetRoot());
            }
        }

        private static Vector3 ClampToReach(Vector3 root, Vector3 target, float maxReach)
        {
            if (maxReach <= 0f)
            {
                return target;
            }

            Vector3 rootToTarget = target - root;
            float distance = rootToTarget.magnitude;
            if (distance <= maxReach || distance <= KMath.FloatMin)
            {
                return target;
            }

            return root + rootToTarget / distance * maxReach;
        }

        private Vector3 GetPoleTarget()
        {
            Transform joint = GetJointTransform();
            Vector3 direction = GetJointOffsetRotation() * _asset.jointOffset;
            return joint.position + joint.rotation * direction;
        }

        private int GetJointIndex()
        {
            int fallbackIndex = Mathf.Clamp(_targetLength - 2, 0, _targetLength - 1);
            int jointIndex = _asset == null ? fallbackIndex : _asset.ResolveJointBoneIndex();
            return jointIndex < 0 ? fallbackIndex : Mathf.Clamp(jointIndex, 0, _targetLength - 1);
        }

        private Transform GetJointTransform()
        {
            int jointIndex = GetJointIndex();
            if (jointIndex != _jointIndex)
            {
                _jointIndex = jointIndex;
                Transform joint = targetChain.transformChain[_jointIndex];
                _localJointForward = RetargetJobUtility.DetectClosestLocalAxis(joint.rotation, GetTargetRoot().forward);
                _localJointUp = RetargetJobUtility.DetectClosestLocalAxis(joint.rotation, GetTargetRoot().up);
            }

            return targetChain.transformChain[_jointIndex];
        }

        private Quaternion GetJointOffsetRotation()
        {
            Vector3 forward = _localJointForward;
            Vector3 up = _localJointUp;

            if (forward.sqrMagnitude <= KMath.SqrEpsilon)
            {
                forward = Vector3.forward;
            }

            if (up.sqrMagnitude <= KMath.SqrEpsilon ||
                Mathf.Abs(Vector3.Dot(forward.normalized, up.normalized)) > 0.999f)
            {
                up = Mathf.Abs(Vector3.Dot(forward.normalized, Vector3.up)) > 0.999f
                    ? Vector3.right
                    : Vector3.up;
            }

            return Quaternion.LookRotation(forward, up);
        }

        protected Vector3 GetEffector()
        {
            Transform sourceTip = sourceChain.transformChain[^1];
            
            KTransform sourceTransform = new KTransform(GetSourceRoot());
            KTransform targetTransform = new KTransform(GetTargetRoot());
            
            Vector3 sourcePose = sourceChain.cachedTransforms[^1].position;
            Vector3 targetPose = targetChain.cachedTransforms[^1].position;
            
            Vector3 additive = sourceTip.position - sourceTransform.TransformPoint(sourcePose, true);
            
            float scale = Mathf.Lerp(1f, chainScale, _asset.scaleWeight);
            additive = additive * scale + targetTransform.TransformPoint(targetPose, true);
            
            return MoveTargetInSpace(ResolveEffectorOffsetSpace(), additive, _asset.effectorOffset);
        }
        
        protected override void Initialize(RetargetFeature newAsset)
        {
            base.Initialize(newAsset);

            _asset = newAsset as IKRetargetFeature;
            if (_asset == null || sourceChain == null || targetChain == null)
            {
                return;
            }

            _sourceLength = sourceChain.transformChain.Count;
            _targetLength = targetChain.transformChain.Count;

            _chainIKData.positions = new Vector3[_targetLength];
            _chainIKData.lengths = new float[_targetLength];

            _chainIKData.tolerance = 0.001f;
            _chainIKData.maxIterations = 25;
            _chainIKData.reachMultiplier = _asset.GetReachMultiplier();

            GetJointTransform();
            _chainIKData.poleIndex = _jointIndex;
        }

        protected virtual void ApplyTwoBoneIK()
        {
            Transform tip = targetChain.transformChain[^1];
            Transform mid = targetChain.transformChain[^2];
            Transform root = targetChain.transformChain[^3];
            
            _ikGoal = new KTransform()
            {
                position = GetEffector(),
                rotation = Quaternion.identity,
                scale = Vector3.one
            };

            float rawReach = Vector3.Distance(root.position, mid.position) + Vector3.Distance(mid.position, tip.position);
            float maxReach = rawReach * _asset.GetReachMultiplier();
            _ikGoal.position = ClampToReach(root.position, _ikGoal.position, maxReach);

            _ikPole = new KTransform()
            {
                position = GetPoleTarget(),
                rotation = Quaternion.identity,
                scale = Vector3.one
            };

            float weight = _asset.ikWeight * _asset.featureWeight;
            float poleWeight = Mathf.Clamp01(_asset.poleWeight) * weight;
            
            KTwoBoneIkData twoBoneIkData = new KTwoBoneIkData()
            {
                root = new KTransform(root),
                mid = new KTransform(mid),
                tip = new KTransform(tip),
                hint = _ikPole,
                target = _ikGoal,
                hasValidHint = poleWeight > 0f,
                rotWeight = 0f,
                posWeight = weight,
                hintWeight = poleWeight,
            };
            
            KTwoBoneIK.Solve(ref twoBoneIkData);

            root.rotation = twoBoneIkData.root.rotation;
            mid.rotation = twoBoneIkData.mid.rotation;
            tip.rotation = twoBoneIkData.tip.rotation;
        }

        protected virtual void ApplyChainIK()
        {
            _chainIKData.maxReach = 0f;
            
            // 1. Gather position and length chain data.
            for (int i = 0; i < _targetLength; i++)
            {
                Vector3 position = targetChain.transformChain[i].position;

                float distance = 0f;
                if (i != _targetLength - 1)
                {
                    distance = Vector3.Distance(position, targetChain.transformChain[i + 1].position);
                }
                
                _chainIKData.positions[i] = position;
                _chainIKData.lengths[i] = distance;
                _chainIKData.maxReach += distance;
            }

            float rawReach = _chainIKData.maxReach;

            _ikGoal = new KTransform()
            {
                position = GetEffector(),
                rotation = Quaternion.identity,
                scale = Vector3.one
            };

            _ikPole = new KTransform()
            {
                position = GetPoleTarget(),
                rotation = Quaternion.identity,
                scale = Vector3.one
            };

            _chainIKData.target = _ikGoal.position;
            _chainIKData.pole = _ikPole.position;
            _chainIKData.usePole = _asset.poleWeight > 0f;
            _chainIKData.poleWeight = _asset.poleWeight;
            _chainIKData.poleIndex = _jointIndex;
            _chainIKData.reachMultiplier = _asset.GetReachMultiplier();

            // 2. Solve Chain IK.
            if (!RetargetUtility.SolveFABRIK(ref _chainIKData)) return;
            
            int tipIndex = _targetLength - 1;
            
            // 3. Apply rotations.
            for (int i = 0; i < tipIndex; ++i)
            {
                KTransform thisTransform = targetChain.cachedTransforms[i];
                KTransform nextTransform = targetChain.cachedTransforms[i + 1];
                
                var prevDir = nextTransform.position - thisTransform.position;
                var newDir = _chainIKData.positions[i + 1] - _chainIKData.positions[i];

                Quaternion baseRot = targetChain.transformChain[i].rotation;
                Quaternion targetRot = KMath.FromToRotation(prevDir, newDir) * thisTransform.rotation;
                
                targetRot = Quaternion.Slerp(baseRot, targetRot, _asset.ikWeight);
                baseRot = GetTargetRoot().rotation * thisTransform.rotation;
                targetRot = Quaternion.Slerp(baseRot, targetRot, _asset.featureWeight);
                
                targetChain.transformChain[i].rotation = targetRot;
            }
            
            RetargetBone(_sourceLength - 1, _targetLength - 1);
        }

        public override void Retarget(float normalizedTime = 0f)
        {
            base.Retarget(normalizedTime);

            if (_targetLength < 3)
            {
                return;
            }

            if (_targetLength == 3 || !_asset.useChainIk)
            {
                ApplyTwoBoneIK();
            }
            else
            {
                ApplyChainIK();
            }
            
            KTransform rootTransform = new KTransform(GetTargetRoot());
            _ikGoal = rootTransform.GetRelativeTransform(_ikGoal, false);
            _ikPole = rootTransform.GetRelativeTransform(_ikPole, false);
        }
        
#if UNITY_EDITOR
        private Vector3 RenderHandle(KTransform space, KTransform target)
        {
            Vector3 handlePos = KTransformHandles.PositionHandle(target.position, space.rotation, 
                KTransformHandles.PositionHandleSettings.Default);
            return space.InverseTransformPoint(handlePos, false) 
                   - space.InverseTransformPoint(target.position, false);
        }

        private Vector3 RenderJointOffsetHandle(KTransform target)
        {
            Transform joint = GetJointTransform();
            Quaternion offsetRotation = joint.rotation * GetJointOffsetRotation();
            Vector3 handlePos = KTransformHandles.PositionHandle(target.position, offsetRotation,
                KTransformHandles.PositionHandleSettings.Default);

            return Quaternion.Inverse(offsetRotation) * (handlePos - target.position);
        }
        
        protected override void OnBoneRenderingSceneGUI()
        {
            RenderSourceAndTargetBoneChains(sourceChain, targetChain, TargetBoneChainColor);
        }

        protected override void OnTransformHandlesSceneGUI()
        {
            if (Mathf.Approximately(_asset.ikWeight, 0f))
            {
                return;
            }

            KTransform rootTransform = new KTransform(GetTargetRoot());
            KTransform goal = rootTransform.GetWorldTransform(_ikGoal, false);
            KTransform pole = rootTransform.GetWorldTransform(_ikPole, false);
            
            _asset.effectorOffset += RenderHandle(ResolveEffectorOffsetSpace(), goal);
            Handles.Label(rootTransform.position, "IK Goal");
            
            _asset.jointOffset += RenderJointOffsetHandle(pole);
            Handles.Label(rootTransform.position, "IK Pole");
        }
#endif
    }
}