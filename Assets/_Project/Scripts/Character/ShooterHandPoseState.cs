using System.Collections;
using System.Reflection;
using KINEMATION.FPSAnimationFramework.Runtime.Core;
using KINEMATION.FPSAnimationFramework.Runtime.Layers.PoseSamplerLayer;
using KINEMATION.FPSAnimationFramework.Runtime.Playables;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

namespace Shooter.Project.Character
{
    /// <summary>
    /// Switches upper-body overlay pose between unarmed (hands down) and armed rifle pose.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    [DisallowMultipleComponent]
    public class ShooterHandPoseState : MonoBehaviour
    {
        const float SlotStopBlend = 0.08f;
        const float DefaultTransitionBlend = 0.45f;
        const float DefaultOverlayBlendIn = 0.5f;
        const float DefaultOverlayBlendOut = 0.25f;

        [SerializeField] Transform fpsCharacterRoot;
        [SerializeField] InputActionAsset inputActions;
        [SerializeField] FPSAnimatorProfile fpsAnimatorProfile;
        [SerializeField] FPSAnimationAsset unarmedOverlayPose;
        [SerializeField] FPSAnimationAsset armedOverlayPose;
        [SerializeField] FPSAnimationAsset equipClip;
        [SerializeField] FPSAnimationAsset unequipClip;
        [SerializeField] bool startUnarmed = true;

        FPSPlayablesController _playablesController;
        PoseSamplerLayerSettings _poseSampler;
        InputActionMap _playerMap;
        InputAction _toggleHandPose;
        bool _isUnarmed;
        bool _toggleRequested;
        bool _isTransitioning;
        Coroutine _transitionCoroutine;

        static FieldInfo OverlayPoseMixerField;
        static FieldInfo OverlayActiveIndexField;
        static FieldInfo OverlayBlendingInField;
        static FieldInfo OverlayBlendTimeField;

        public bool IsUnarmed => _isUnarmed;
        public bool IsTransitioning => _isTransitioning;
        public FPSAnimationAsset EquipClip => equipClip;
        public FPSAnimationAsset UnequipClip => unequipClip;
        public FPSAnimationAsset ArmedOverlayPose => armedOverlayPose;
        public FPSAnimationAsset UnarmedOverlayPose => unarmedOverlayPose;
        public FPSAnimatorProfile FpsAnimatorProfile => fpsAnimatorProfile;

        public void ResetTransitionBlendDefaults()
        {
            SetAssetBlendTime(equipClip, DefaultTransitionBlend, DefaultTransitionBlend);
            SetAssetBlendTime(unequipClip, DefaultTransitionBlend, DefaultTransitionBlend);
            SetAssetBlendTime(armedOverlayPose, DefaultOverlayBlendIn, DefaultOverlayBlendOut);
            SetAssetBlendTime(unarmedOverlayPose, DefaultOverlayBlendIn, DefaultOverlayBlendOut);
        }

        void Awake()
        {
            ResolveReferences();
            CachePoseSampler();
            BindToggleAction();
            EnsureBalanceTuningPanel();
        }

        void OnEnable()
        {
            _playerMap?.Enable();
        }

        void OnDisable()
        {
            _playerMap?.Disable();
            StopActiveTransition();
        }

        void Start()
        {
            _isUnarmed = startUnarmed;
            ApplyPoseInstant(startUnarmed ? unarmedOverlayPose : armedOverlayPose);
        }

        void Update()
        {
            if (_isTransitioning)
                return;

            if (_toggleHandPose != null && _toggleHandPose.WasPressedThisFrame())
                _toggleRequested = true;
        }

        void LateUpdate()
        {
            if (!_toggleRequested)
                return;

            _toggleRequested = false;
            SetHandPose(!_isUnarmed);
        }

        public void SetUnarmed() => SetHandPose(true);

        public void SetArmed() => SetHandPose(false);

        /// <summary>
        /// Sync profile pose sampler before FPS layers link (e.g. ladder exit).
        /// PoseSamplerLayerJob.Initialize will call PlayPose once — do not call PlayPose again after.
        /// </summary>
        public void PreparePoseForFpsRestore()
        {
            SyncPoseSamplerSettings(_isUnarmed ? unarmedOverlayPose : armedOverlayPose);
        }

        /// <summary>
        /// Sample bones and snap overlay mixer to full weight after a single PlayPose from PoseSampler init.
        /// </summary>
        public void FinalizePoseAfterFpsRestore()
        {
            FPSAnimationAsset pose = _isUnarmed ? unarmedOverlayPose : armedOverlayPose;
            if (pose?.clip == null || fpsCharacterRoot == null)
                return;

            ClearSlotAnimations();
            pose.clip.SampleAnimation(fpsCharacterRoot.gameObject, 0f);
            ForceOverlayPoseFullWeight();
        }

        public void SetHandPose(bool unarmed, bool instant = false)
        {
            FPSAnimationAsset pose = unarmed ? unarmedOverlayPose : armedOverlayPose;
            if (pose == null || _poseSampler == null)
                return;

            if (_isUnarmed == unarmed && _poseSampler.poseToSample == pose && !_isTransitioning)
                return;

            _isUnarmed = unarmed;

            if (instant)
            {
                StopActiveTransition();
                ApplyPoseInstant(pose);
                return;
            }

            StopActiveTransition();
            _transitionCoroutine = StartCoroutine(TransitionToPose(pose, unarmed));
        }

        IEnumerator TransitionToPose(FPSAnimationAsset targetPose, bool toUnarmed)
        {
            _isTransitioning = true;

            if (_playablesController == null || targetPose.clip == null)
            {
                SyncPoseSamplerSettings(targetPose);
                _isTransitioning = false;
                _transitionCoroutine = null;
                yield break;
            }

            PlayableGraph graph = _playablesController.GetPlayableGraph();
            if (!graph.IsValid())
            {
                SyncPoseSamplerSettings(targetPose);
                _isTransitioning = false;
                _transitionCoroutine = null;
                yield break;
            }

            // Clear leftover slot/override motion from previous toggles — avoids twisted hands stacking up.
            ClearSlotAnimations();
            yield return null;
            ClearSlotAnimations();

            SyncPoseSamplerSettings(targetPose);
            float blendIn = GetTransitionBlendIn(toUnarmed);
            ApplyOverlayBlend(targetPose, blendIn);

            yield return new WaitForSeconds(Mathf.Max(blendIn, 0.05f));

            ClearSlotAnimations();
            ForceOverlayPoseFullWeight();

            _isTransitioning = false;
            _transitionCoroutine = null;
        }

        void ApplyOverlayBlend(FPSAnimationAsset pose, float blendInTime)
        {
            if (_playablesController == null || pose?.clip == null)
                return;

            PlayableGraph graph = _playablesController.GetPlayableGraph();
            if (!graph.IsValid())
                return;

            BlendTime savedBlend = pose.blendTime;
            BlendTime blend = savedBlend;
            blend.blendInTime = blendInTime;
            pose.blendTime = blend;

            _playablesController.PlayPose(pose);
            pose.blendTime = savedBlend;
        }

        float GetTransitionBlendIn(bool toUnarmed)
        {
            FPSAnimationAsset transition = toUnarmed ? unequipClip : equipClip;
            if (transition == null)
                return DefaultTransitionBlend;

            return Mathf.Max(0.01f, transition.blendTime.blendInTime);
        }

        static void SetAssetBlendTime(FPSAnimationAsset asset, float blendIn, float blendOut)
        {
            if (asset == null)
                return;

            BlendTime blend = asset.blendTime;
            blend.blendInTime = blendIn;
            blend.blendOutTime = blendOut;
            asset.blendTime = blend;
        }

        void ApplyPoseInstant(FPSAnimationAsset pose)
        {
            SyncPoseSamplerSettings(pose);

            if (_playablesController == null || pose?.clip == null)
                return;

            PlayableGraph graph = _playablesController.GetPlayableGraph();
            if (!graph.IsValid())
                return;

            ClearSlotAnimations();
            _playablesController.PlayPose(pose);
            ForceOverlayPoseFullWeight();
        }

        void ClearSlotAnimations()
        {
            _playablesController?.StopAnimation(SlotStopBlend);
        }

        void StopActiveTransition()
        {
            if (_transitionCoroutine == null)
                return;

            StopCoroutine(_transitionCoroutine);
            _transitionCoroutine = null;
            _isTransitioning = false;
            ClearSlotAnimations();
        }

        void SyncPoseSamplerSettings(FPSAnimationAsset pose)
        {
            if (pose == null || _poseSampler == null)
                return;

            _poseSampler.poseToSample = pose;
            _poseSampler.overwriteRoot = false;
            _poseSampler.overwriteWeaponBone = pose == armedOverlayPose;
        }

        void ForceOverlayPoseFullWeight()
        {
            if (_playablesController is not FPSPlayablesController fpsPlayables)
                return;

            PlayableGraph graph = fpsPlayables.GetPlayableGraph();
            if (!graph.IsValid())
                return;

            CacheOverlayMixerFields();

            object boxedMixer = OverlayPoseMixerField.GetValue(fpsPlayables);
            if (boxedMixer == null)
                return;

            var overlayMixer = (FPSAnimatorMixer)boxedMixer;
            int activeIndex = (int)OverlayActiveIndexField.GetValue(boxedMixer);
            if (activeIndex < 0 || !overlayMixer.mixer.IsValid())
                return;

            var blendTime = (BlendTime)OverlayBlendTimeField.GetValue(boxedMixer);
            blendTime.blendInTime = 0f;
            OverlayBlendTimeField.SetValue(boxedMixer, blendTime);
            OverlayBlendingInField.SetValue(boxedMixer, true);

            overlayMixer = (FPSAnimatorMixer)boxedMixer;
            overlayMixer.Update();

            if (overlayMixer.mixer.IsValid())
                overlayMixer.mixer.SetInputWeight(activeIndex, 1f);

            OverlayBlendingInField.SetValue(boxedMixer, false);
            OverlayPoseMixerField.SetValue(fpsPlayables, overlayMixer);
        }

        static void CacheOverlayMixerFields()
        {
            OverlayPoseMixerField ??= typeof(FPSPlayablesController).GetField(
                "_overlayPoseMixer",
                BindingFlags.Instance | BindingFlags.NonPublic);

            OverlayActiveIndexField ??= typeof(FPSAnimatorMixer).GetField(
                "_activeIndex",
                BindingFlags.Instance | BindingFlags.NonPublic);

            OverlayBlendingInField ??= typeof(FPSAnimatorMixer).GetField(
                "_blendingIn",
                BindingFlags.Instance | BindingFlags.NonPublic);

            OverlayBlendTimeField ??= typeof(FPSAnimatorMixer).GetField(
                "_blendTime",
                BindingFlags.Instance | BindingFlags.NonPublic);
        }

        void BindToggleAction()
        {
            if (inputActions == null)
                return;

            _playerMap = inputActions.FindActionMap("Player", true);
            _toggleHandPose = _playerMap.FindAction("ToggleHandPose", false);
        }

        void EnsureBalanceTuningPanel()
        {
            if (GetComponent<ShooterBalanceTuningPanel>() == null)
                gameObject.AddComponent<ShooterBalanceTuningPanel>();
        }

        void ResolveReferences()
        {
            if (inputActions == null)
            {
                ShooterCharacterController bridge = GetComponent<ShooterCharacterController>();
                if (bridge != null)
                {
                    var field = typeof(ShooterCharacterController).GetField(
                        "inputActions",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    inputActions = field?.GetValue(bridge) as InputActionAsset;
                }
            }

            if (fpsCharacterRoot == null)
            {
                Transform graphics = transform.Find("Graphics");
                if (graphics != null && graphics.childCount > 0)
                    fpsCharacterRoot = graphics.GetChild(0);
            }

            if (fpsCharacterRoot == null)
                return;

            _playablesController = fpsCharacterRoot.GetComponent<FPSPlayablesController>();

            if (fpsAnimatorProfile == null)
            {
                FPSAnimator fpsAnimator = fpsCharacterRoot.GetComponent<FPSAnimator>();
                if (fpsAnimator != null)
                {
                    var profileField = typeof(FPSAnimator).GetField(
                        "animatorProfile",
                        BindingFlags.Instance | BindingFlags.NonPublic);

                    fpsAnimatorProfile = profileField?.GetValue(fpsAnimator) as FPSAnimatorProfile;
                }
            }
        }

        void CachePoseSampler()
        {
            if (fpsAnimatorProfile == null)
                return;

            foreach (FPSAnimatorLayerSettings layer in fpsAnimatorProfile.settings)
            {
                if (layer is PoseSamplerLayerSettings sampler)
                {
                    _poseSampler = sampler;
                    return;
                }
            }
        }
    }
}
