// Copyright (c) 2026 KINEMATION.
// All rights reserved.

using KINEMATION.Shared.KAnimationCore.Runtime.Rig;
using KINEMATION.Shared.KAnimationCore.Runtime.Core;

using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;

namespace KINEMATION.RetargetPro.Runtime.Features.IKRetargeting
{
    public struct IKRetargetJob : IRetargetJob, IAnimationJob
    {
        public IKRetargetFeature ikFeature;
        public NativeArray<RetargetSceneAtom> sourceChain;
        public NativeArray<IKRetargetStreamAtom> targetChain;
        
        public IKRetargetData ikData;
        
        public void Setup(RetargetFeature feature, Animator animator, KRigComponent source, KRigComponent target)
        {
            ikFeature = feature as IKRetargetFeature;

            Transform sourceRootTransform = source.transform.parent == null ? source.transform : source.transform.parent;
            Transform targetRootTransform = target.transform.parent == null ? target.transform : target.transform.parent;
            
            ikData.basicData.sourceRootPose = new KTransform(sourceRootTransform);
            ikData.basicData.targetRoot = animator.BindSceneTransform(targetRootTransform);

            KTransformChain sourceChainT = RetargetUtility.GetTransformChain(source, ikFeature.sourceRig, 
                ikFeature.sourceChain);
            KTransformChain targetChainT =  RetargetUtility.GetTransformChain(target, ikFeature.targetRig, 
                ikFeature.targetChain);
            
            if(!sourceChainT.IsValid() || !targetChainT.IsValid())
            {
                Debug.LogError("IKRetargetJob: Source or Target chains are NULL!");
                return;
            }
            
            RetargetJobUtility.SetupSceneAtomChain(animator, ref sourceChain, sourceChainT.transformChain.ToArray(), 
                sourceRootTransform);
            RetargetJobUtility.SetupStreamIkAtomChain(animator, ref targetChain, targetChainT.transformChain.ToArray(),
                targetRootTransform);

            int targetJointIndex = Mathf.Clamp(ikFeature.ResolveJointBoneIndex(), 0,
                targetChainT.transformChain.Count - 1);
            Transform joint = targetChainT.transformChain[targetJointIndex];
            ikData.jointIndex = targetJointIndex;
            ikData.localJointForward = RetargetJobUtility.DetectClosestLocalAxis(joint.rotation,
                targetRootTransform.forward);
            ikData.localJointUp = RetargetJobUtility.DetectClosestLocalAxis(joint.rotation,
                targetRootTransform.up);

            float sourceLength = sourceChainT.GetLength(sourceRootTransform);
            float targetLength = targetChainT.GetLength(targetRootTransform);
            
            if (Mathf.Approximately(sourceLength, 0f))
            {
                ikData.basicData.scale = 1f;
                return;
            }
            
            ikData.basicData.scale = targetLength / sourceLength;
        }

        public void UpdateSourceRootPose(KTransform sourceRootPose)
        {
            ikData.basicData.sourceRootPose = sourceRootPose;
        }

        public void SetJobData(AnimationScriptPlayable playable)
        {
            ikData.basicData.featureWeight = ikFeature.featureWeight;
            ikData.basicData.scaleWeight = ikFeature.scaleWeight;
            ikData.basicData.translationWeight = ikFeature.GetTranslationBlend();
            ikData.basicData.offset = ikFeature.offset;

            ikData.effectorOffset = ikFeature.effectorOffset;
            ikData.effectorSpace = ikFeature.effectorSpace;
            ikData.jointOffset = ikFeature.jointOffset;
            ikData.ikWeight = ikFeature.ikWeight;
            ikData.poleWeight = ikFeature.poleWeight;
            ikData.jointIndex = targetChain.IsCreated && targetChain.Length > 0
                ? Mathf.Clamp(ikFeature.ResolveJointBoneIndex(), 0, targetChain.Length - 1)
                : -1;
            ikData.reachMultiplier = ikFeature.GetReachMultiplier();

            ikData.maxIterations = 16;
            ikData.tolerance = 0.001f;
            
            playable.SetJobData(this);
        }

        public void Dispose()
        {
            if (sourceChain.IsCreated) sourceChain.Dispose();
            if (targetChain.IsCreated) targetChain.Dispose();
        }
        
        public void ProcessAnimation(AnimationStream stream)
        {
            RetargetJobUtility.BasicRetarget(stream, sourceChain, targetChain, ikData.basicData);

            if (Mathf.Approximately(ikData.ikWeight, 0f) || targetChain.Length < 3)
            {
                return;
            }

            if (targetChain.Length == 3)
            {
                RetargetJobUtility.SolveTwoBoneIK(stream, sourceChain, targetChain, ikData);
                return;
            }
            
            RetargetJobUtility.SolveChainIK(stream, sourceChain, targetChain, ikData);
        }

        public void ProcessRootMotion(AnimationStream stream)
        {
        }
    }
}
