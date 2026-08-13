using KINEMATION.FPSAnimationFramework.Runtime.Core;
using KINEMATION.FPSAnimationFramework.Runtime.Layers.PoseSamplerLayer;
using KINEMATION.FPSAnimationFramework.Runtime.Playables;
using UnityEngine;
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
            _poseSampler.poseToSample = pose;
            _poseSampler.overwriteWeaponBone = pose == armedOverlayPose;

            if (_playablesController == null || pose.clip == null)
                return;

            PlayableGraph graph = _playablesController.GetPlayableGraph();
            if (!graph.IsValid())
                return;

            pose.clip.SampleAnimation(fpsCharacterRoot.gameObject, 0f);
            _playablesController.PlayPose(pose);
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
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
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
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

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
