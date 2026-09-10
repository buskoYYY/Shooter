// Copyright (c) 2026 KINEMATION.
// All rights reserved.

using KINEMATION.Shared.KAnimationCore.Runtime.Rig;

namespace KINEMATION.RetargetPro.Runtime.Features.CopyBone
{
    public class CopyBoneFeatureState : RetargetFeatureState
    {
        protected KTransformChain _copyToChain;
        protected KTransformChain _copyFromChain;
        protected CopyBoneFeatureSettings _asset;

        public override bool IsValid()
        {
            return _copyToChain != null && _copyToChain.IsValid() &&
                   _copyFromChain != null && _copyFromChain.IsValid() &&
                   _copyToChain.transformChain.Count == _copyFromChain.transformChain.Count;
        }
        
        protected override void Initialize(RetargetFeature newAsset)
        {
            _asset = newAsset as CopyBoneFeatureSettings;
            if (_asset == null) return;
            
            _copyFromChain = RetargetUtility.GetTransformChain(sourceRigComponent, _asset.sourceRig, 
                _asset.copyFrom);
            
            _copyToChain = RetargetUtility.GetTransformChain(targetRigComponent, _asset.targetRig, 
                _asset.copyTo);
        }

        public override void Retarget(float time = 0)
        {
            if (!IsValid())
            {
                return;
            }

            int count = _copyToChain.transformChain.Count;

            for (int i = 0; i < count; i++)
            {
                var copyFrom = _copyFromChain.transformChain[i];
                var copyTo = _copyToChain.transformChain[i];

                copyTo.position = copyFrom.position;
                copyTo.rotation = copyFrom.rotation;
                copyTo.localScale = copyFrom.localScale;
            }
        }

#if UNITY_EDITOR
        protected override void OnBoneRenderingSceneGUI()
        {
            RenderSourceAndTargetBoneChains(_copyFromChain, _copyToChain, TargetBoneChainColor);
        }
#endif
    }
}