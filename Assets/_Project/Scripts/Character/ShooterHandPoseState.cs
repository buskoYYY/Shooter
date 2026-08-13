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
        [SerializeField] Transform fpsCharacterRoot;
        [SerializeField] InputActionAsset inputActions;
        [SerializeField] FPSAnimatorProfile fpsAnimatorProfile;
        [SerializeField] FPSAnimationAsset unarmedOverlayPose;
        [SerializeField] FPSAnimationAsset armedOverlayPose;
        [SerializeField] bool startUnarmed = true;

        FPSPlayablesController _playablesController;
        PoseSamplerLayerSettings _poseSampler;
        InputActionMap _playerMap;
        InputAction _toggleHandPose;
        bool _isUnarmed;
        bool _toggleRequested;

        static FieldInfo OverlayPoseMixerField;
        static FieldInfo OverlayActiveIndexField;
        static FieldInfo OverlayBlendingInField;
        static FieldInfo OverlayBlendTimeField;

        public bool IsUnarmed => _isUnarmed;

        void Awake()
        {
            ResolveReferences();
            CachePoseSampler();
            BindToggleAction();
        }

        void OnEnable()
        {
            _playerMap?.Enable();
        }

        void OnDisable()
        {
            _playerMap?.Disable();
        }

        void Start()
        {
            _isUnarmed = startUnarmed;
            ApplyPoseSettings(startUnarmed ? unarmedOverlayPose : armedOverlayPose);
        }

        void Update()
        {
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

            pose.clip.SampleAnimation(fpsCharacterRoot.gameObject, 0f);
            ForceOverlayPoseFullWeight();
        }

        public void SetHandPose(bool unarmed)
        {
            FPSAnimationAsset pose = unarmed ? unarmedOverlayPose : armedOverlayPose;
            if (pose == null || _poseSampler == null)
                return;

            if (_isUnarmed == unarmed && _poseSampler.poseToSample == pose)
                return;

            _isUnarmed = unarmed;
            ApplyPoseSettings(pose);
        }

        void ApplyPoseSettings(FPSAnimationAsset pose)
        {
            SyncPoseSamplerSettings(pose);

            if (_playablesController == null || pose.clip == null || fpsCharacterRoot == null)
                return;

            PlayableGraph graph = _playablesController.GetPlayableGraph();
            if (!graph.IsValid())
                return;

            pose.clip.SampleAnimation(fpsCharacterRoot.gameObject, 0f);
            _playablesController.PlayPose(pose);
        }

        void SyncPoseSamplerSettings(FPSAnimationAsset pose)
        {
            if (pose == null || _poseSampler == null)
                return;

            _poseSampler.poseToSample = pose;
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

            FPSAnimator fpsAnimator = fpsCharacterRoot.GetComponent<FPSAnimator>();
            _playablesController = fpsCharacterRoot.GetComponent<FPSPlayablesController>();

            if (fpsAnimatorProfile == null && fpsAnimator != null)
            {
                var profileField = typeof(FPSAnimator).GetField(
                    "animatorProfile",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                fpsAnimatorProfile = profileField?.GetValue(fpsAnimator) as FPSAnimatorProfile;
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
