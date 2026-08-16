using KINEMATION.FPSAnimationFramework.Runtime.Camera;
using KINEMATION.FPSAnimationFramework.Runtime.Core;
using KINEMATION.FPSAnimationFramework.Runtime.Layers.IkMotionLayer;
using KINEMATION.Shared.KAnimationCore.Runtime.Core;
using KINEMATION.Shared.KAnimationCore.Runtime.Input;
using Lightbug.CharacterControllerPro.Core;
using Lightbug.CharacterControllerPro.Demo;
using Lightbug.CharacterControllerPro.Implementation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Project.Character
{
    /// <summary>
    /// Bridges Character Controller Pro movement with FPS Animation Framework (body, look, sway).
    /// Place on the player root. FPS components live on the Character_model child.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class ShooterCharacterController : MonoBehaviour
    {
        public const float DefaultLocomotionSmoothingStart = 3f;
        public const float DefaultLocomotionSmoothingStop = 5f;
        public const float DefaultMovingStartThreshold = 0.18f;
        public const float DefaultMovingStopThreshold = 0.05f;
        public const float DefaultJumpBlendTime = 0.15f;
        public const float DefaultJumpPlayRate = 1f;
        public const float DefaultStopBlendTime = 0.35f;
        public const float DefaultStopPlayRate = 0.75f;
        public const float DefaultLeanBlendTime = 0.35f;
        public const float DefaultLeanPlayRate = 1f;
        public const float DefaultCrouchBlendTime = 0.3f;
        public const float DefaultCrouchPlayRate = 0.9f;

        [SerializeField] Transform fpsCharacterRoot;
        [SerializeField] InputActionAsset inputActions;
        [SerializeField] IkMotionLayerSettings jumpMotion;
        [SerializeField] IkMotionLayerSettings stopMotion;
        [SerializeField] IkMotionLayerSettings leanMotion;
        [SerializeField] IkMotionLayerSettings crouchMotion;
        [SerializeField] float leanAngle = 25f;
        [SerializeField] float lookSensitivity = 0.15f;
        [SerializeField] float pitchClamp = 70f;
        [Tooltip("How quickly leg blend parameters ramp up when starting to move.")]
        [SerializeField] float locomotionSmoothingStart = DefaultLocomotionSmoothingStart;
        [Tooltip("How quickly leg blend parameters ramp down when stopping.")]
        [SerializeField] float locomotionSmoothingStop = DefaultLocomotionSmoothingStop;
        [Tooltip("Blend tree Velocity must exceed this to enter Moving.")]
        [SerializeField] float movingStartThreshold = DefaultMovingStartThreshold;
        [Tooltip("Blend tree Velocity must fall below this to exit Moving.")]
        [SerializeField] float movingStopThreshold = DefaultMovingStopThreshold;
        [SerializeField] float jumpBlendTime = DefaultJumpBlendTime;
        [SerializeField] float jumpPlayRate = DefaultJumpPlayRate;
        [SerializeField] float stopBlendTime = DefaultStopBlendTime;
        [SerializeField] float stopPlayRate = DefaultStopPlayRate;
        [SerializeField] float leanBlendTime = DefaultLeanBlendTime;
        [SerializeField] float leanPlayRate = DefaultLeanPlayRate;
        [SerializeField] float crouchBlendTime = DefaultCrouchBlendTime;
        [SerializeField] float crouchPlayRate = DefaultCrouchPlayRate;

        CharacterActor _characterActor;
        CharacterStateController _stateController;
        NormalMovement _normalMovement;
        ShooterLadderFpsBridge _ladderBridge;
        ShooterHandPoseState _handPoseState;
        FPSAnimator _fpsAnimator;
        UserInputController _userInput;
        Animator _animator;
        FPSCameraController _fpsCamera;

        Vector2 _mouseInput;
        Vector2 _animatorVelocity;
        float _sprintWeight;
        float _lastLeanInput;
        bool _wasCrouching;
        bool _wasAnimatorMoving;
        bool _playedJumpMotionSinceLand;

        public float Pitch => _mouseInput.y;
        public FPSCameraController FpsCamera => _fpsCamera;
        public IkMotionLayerSettings JumpMotion => jumpMotion;
        public IkMotionLayerSettings StopMotion => stopMotion;
        public IkMotionLayerSettings LeanMotion => leanMotion;
        public IkMotionLayerSettings CrouchMotion => crouchMotion;

        public float LocomotionSmoothingStart
        {
            get => locomotionSmoothingStart;
            set => locomotionSmoothingStart = Mathf.Max(0.5f, value);
        }

        public float LocomotionSmoothingStop
        {
            get => locomotionSmoothingStop;
            set => locomotionSmoothingStop = Mathf.Max(0.5f, value);
        }

        public float MovingStartThreshold
        {
            get => movingStartThreshold;
            set => movingStartThreshold = Mathf.Clamp(value, 0.01f, 0.5f);
        }

        public float MovingStopThreshold
        {
            get => movingStopThreshold;
            set => movingStopThreshold = Mathf.Clamp(value, 0f, movingStartThreshold - 0.01f);
        }

        public float JumpBlendTime
        {
            get => jumpBlendTime;
            set { jumpBlendTime = Mathf.Clamp(value, 0.05f, 1f); ApplyMotionTuning(); }
        }

        public float JumpPlayRate
        {
            get => jumpPlayRate;
            set { jumpPlayRate = Mathf.Clamp(value, 0.25f, 2f); ApplyMotionTuning(); }
        }

        public float StopBlendTime
        {
            get => stopBlendTime;
            set { stopBlendTime = Mathf.Clamp(value, 0.05f, 1f); ApplyMotionTuning(); }
        }

        public float StopPlayRate
        {
            get => stopPlayRate;
            set { stopPlayRate = Mathf.Clamp(value, 0.25f, 2f); ApplyMotionTuning(); }
        }

        public float LeanBlendTime
        {
            get => leanBlendTime;
            set { leanBlendTime = Mathf.Clamp(value, 0.05f, 1f); ApplyMotionTuning(); }
        }

        public float LeanPlayRate
        {
            get => leanPlayRate;
            set { leanPlayRate = Mathf.Clamp(value, 0.25f, 2f); ApplyMotionTuning(); }
        }

        public float CrouchBlendTime
        {
            get => crouchBlendTime;
            set { crouchBlendTime = Mathf.Clamp(value, 0.05f, 1f); ApplyMotionTuning(); }
        }

        public float CrouchPlayRate
        {
            get => crouchPlayRate;
            set { crouchPlayRate = Mathf.Clamp(value, 0.25f, 2f); ApplyMotionTuning(); }
        }

        public void ResetMotionDefaults()
        {
            locomotionSmoothingStart = DefaultLocomotionSmoothingStart;
            locomotionSmoothingStop = DefaultLocomotionSmoothingStop;
            movingStartThreshold = DefaultMovingStartThreshold;
            movingStopThreshold = DefaultMovingStopThreshold;
            jumpBlendTime = DefaultJumpBlendTime;
            jumpPlayRate = DefaultJumpPlayRate;
            stopBlendTime = DefaultStopBlendTime;
            stopPlayRate = DefaultStopPlayRate;
            leanBlendTime = DefaultLeanBlendTime;
            leanPlayRate = DefaultLeanPlayRate;
            crouchBlendTime = DefaultCrouchBlendTime;
            crouchPlayRate = DefaultCrouchPlayRate;
            ApplyMotionTuning();
        }

        public void ApplyMotionTuning()
        {
            ApplyIkMotionTuning(jumpMotion, jumpBlendTime, jumpPlayRate);
            ApplyIkMotionTuning(stopMotion, stopBlendTime, stopPlayRate);
            ApplyIkMotionTuning(leanMotion, leanBlendTime, leanPlayRate);
            ApplyIkMotionTuning(crouchMotion, crouchBlendTime, crouchPlayRate);
        }

        public void ResetPitchForLadder()
        {
            _mouseInput.y = 0f;
        }

        InputActionMap _playerMap;
        InputAction _move;
        InputAction _look;
        InputAction _sprint;
        InputAction _crouch;
        InputAction _lean;

        const string LookLayerWeightProperty = "LookLayerWeight";

        static readonly int InAirHash = Animator.StringToHash("InAir");
        static readonly int MoveXHash = Animator.StringToHash("MoveX");
        static readonly int MoveYHash = Animator.StringToHash("MoveY");
        static readonly int VelocityHash = Animator.StringToHash("Velocity");
        static readonly int MovingHash = Animator.StringToHash("Moving");
        static readonly int CrouchingHash = Animator.StringToHash("Crouching");
        static readonly int SprintingHash = Animator.StringToHash("Sprinting");
        static readonly int FullBodyWeightHash = Animator.StringToHash("FullBodyWeight");

        void Awake()
        {
            _characterActor = GetComponent<CharacterActor>();
            _stateController = GetComponentInChildren<CharacterStateController>();
            _normalMovement = GetComponentInChildren<NormalMovement>();
            _ladderBridge = GetComponent<ShooterLadderFpsBridge>();
            _handPoseState = GetComponent<ShooterHandPoseState>();

            if (fpsCharacterRoot == null)
            {
                var graphics = transform.Find("Graphics");
                if (graphics != null && graphics.childCount > 0)
                    fpsCharacterRoot = graphics.GetChild(0);
            }

            if (fpsCharacterRoot != null)
            {
                _fpsAnimator = fpsCharacterRoot.GetComponent<FPSAnimator>();
                _userInput = fpsCharacterRoot.GetComponent<UserInputController>();
                _animator = fpsCharacterRoot.GetComponent<Animator>();
                _fpsCamera = fpsCharacterRoot.GetComponentInChildren<FPSCameraController>(true);
            }

            if (inputActions != null)
            {
                _playerMap = inputActions.FindActionMap("Player", true);
                _move = _playerMap.FindAction("Move", true);
                _look = _playerMap.FindAction("Look", true);
                _sprint = _playerMap.FindAction("Sprint", true);
                _crouch = _playerMap.FindAction("Crouch", true);
                _lean = _playerMap.FindAction("Lean", false);
            }

            EnsureFpsCameraApplyOnSelf();
            GetComponent<ShooterFpsCameraApply>()?.PrepareCameraBeforeInit();

            ApplyMotionTuning();

            if (_fpsAnimator != null)
                _fpsAnimator.Initialize();
        }

        void EnsureFpsCameraApplyOnSelf()
        {
            if (GetComponent<ShooterFpsCameraApply>() != null)
                return;

            gameObject.AddComponent<ShooterFpsCameraApply>();
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
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            ConfigureCcpForFps();

            BindJumpEvents();
        }

        void OnDestroy()
        {
            UnbindJumpEvents();
        }

        void BindJumpEvents()
        {
            if (_normalMovement != null)
                _normalMovement.OnJumpPerformed += HandleJumpPerformed;

            if (_characterActor != null)
                _characterActor.OnGroundedStateEnter += HandleLanded;
        }

        void UnbindJumpEvents()
        {
            if (_normalMovement != null)
                _normalMovement.OnJumpPerformed -= HandleJumpPerformed;

            if (_characterActor != null)
                _characterActor.OnGroundedStateEnter -= HandleLanded;
        }

        void HandleJumpPerformed()
        {
            _playedJumpMotionSinceLand = true;
            PlayIkMotion(jumpMotion);
        }

        void HandleLanded(Vector3 _)
        {
            if (!_playedJumpMotionSinceLand)
                return;

            _playedJumpMotionSinceLand = false;
            PlayIkMotion(jumpMotion);
        }

        void Update()
        {
            if (ShooterBalanceTuningPanel.IsOpen)
                return;

            UpdateLook();

            if (IsInLadderState() || ShouldDeferFpsLocomotion())
            {
                SyncMouseInputOnly();
                return;
            }

            SyncAnimatorFromCcp();
            UpdateFpsInput();
        }

        bool ShouldDeferFpsLocomotion() =>
            _ladderBridge != null && _ladderBridge.ShouldBlockFpsPlayables;

        bool IsInLadderState() =>
            _stateController != null && _stateController.CurrentState is LadderClimbing;

        bool IsMountedOnLadder() =>
            IsInLadderState() &&
            _stateController.CurrentState is LadderClimbing ladder &&
            !ladder.IsApproachingEntry;

        void ConfigureCcpForFps()
        {
            if (_stateController != null)
            {
                _stateController.MovementReferenceMode = MovementReferenceParameters.MovementReferenceMode.External;
                _stateController.ExternalReference = transform;
            }

            if (_normalMovement != null)
            {
                _normalMovement.lookingDirectionParameters.changeLookingDirection = false;
                ShooterCcpMovementTuning.ApplyDemoJumpSettings(_normalMovement);
            }

            var tuning = GetComponent<ShooterCcpMovementTuning>();
            if (tuning != null)
                tuning.ApplyTuning();
        }

        void UpdateLook()
        {
            if (_look == null || _characterActor == null)
                return;

            Vector2 lookDelta = _look.ReadValue<Vector2>() * lookSensitivity;

            _mouseInput.y -= lookDelta.y;
            _mouseInput.y = Mathf.Clamp(_mouseInput.y, -pitchClamp, pitchClamp);

            if (IsMountedOnLadder())
                return;

            _mouseInput.x += lookDelta.x;
            _characterActor.Rotation *= Quaternion.Euler(0f, lookDelta.x, 0f);
            _characterActor.ResetInterpolationRotation();
        }

        void UpdateFpsInput()
        {
            if (_userInput == null)
                return;

            Vector2 lookDelta = _look != null ? _look.ReadValue<Vector2>() * lookSensitivity : Vector2.zero;
            bool sprinting = _sprint != null && _sprint.IsPressed();

            _userInput.SetValue(FPSANames.MouseDeltaInput, new Vector4(lookDelta.x, lookDelta.y, 0f, 0f));
            _userInput.SetValue(FPSANames.MouseInput, new Vector4(_mouseInput.x, _mouseInput.y, 0f, 0f));
            _userInput.SetValue(FPSANames.MoveInput, new Vector4(_animatorVelocity.x, _animatorVelocity.y, 0f, 0f));

            if (ShouldDeferFpsLocomotion())
                return;

            _userInput.SetValue(FPSANames.StabilizationWeight, sprinting ? 0f : 1f);
            _userInput.SetValue(LookLayerWeightProperty, sprinting ? 0.3f : 1f);
            UpdatePlayablesWeight();

            UpdateLeanInput();
        }

        void UpdatePlayablesWeight()
        {
            if (_animator == null || _userInput == null)
                return;

            if (_handPoseState != null && _handPoseState.IsUnarmed)
                _animator.SetFloat(FullBodyWeightHash, 1f);
            else if (_characterActor != null && !_characterActor.IsGrounded)
                _animator.SetFloat(FullBodyWeightHash, 1f);
            else if (_handPoseState != null && !_handPoseState.IsUnarmed)
                _animator.SetFloat(FullBodyWeightHash, 0f);

            float playablesWeight = 1f - _animator.GetFloat(FullBodyWeightHash);
            _userInput.SetValue(FPSANames.PlayablesWeight, playablesWeight);
        }

        void UpdateLeanInput()
        {
            if (_userInput == null)
                return;

            float leanAxis = _lean != null ? _lean.ReadValue<float>() : 0f;
            float leanValue = leanAxis * leanAngle;
            _userInput.SetValue(FPSANames.LeanInput, leanValue);

            if (Mathf.Approximately(leanValue, _lastLeanInput) || leanMotion == null || _fpsAnimator == null)
                return;

            PlayIkMotion(leanMotion);
            _lastLeanInput = leanValue;
        }

        void SyncMouseInputOnly()
        {
            if (_userInput == null)
                return;

            _userInput.SetValue(FPSANames.MouseInput, new Vector4(_mouseInput.x, _mouseInput.y, 0f, 0f));
        }

        void SyncAnimatorFromCcp()
        {
            if (_animator == null || _characterActor == null)
                return;

            bool inAir = !_characterActor.IsGrounded;
            Vector2 moveInput = _move != null ? _move.ReadValue<Vector2>() : Vector2.zero;
            Vector2 targetVelocity = inAir ? Vector2.zero : moveInput;

            float targetSpeed = targetVelocity.magnitude;
            float currentSpeed = _animatorVelocity.magnitude;
            bool accelerating = targetSpeed > currentSpeed + 0.01f;
            float smoothing = accelerating ? locomotionSmoothingStart : locomotionSmoothingStop;
            float blendAlpha = KMath.ExpDecayAlpha(smoothing, Time.deltaTime);
            _animatorVelocity = Vector2.Lerp(_animatorVelocity, targetVelocity, blendAlpha);

            float speed = Mathf.Clamp01(_animatorVelocity.magnitude);
            bool animatorMoving = EvaluateAnimatorMoving(speed, inAir);
            bool sprinting = _sprint != null && _sprint.IsPressed();
            bool crouching = _crouch != null && _crouch.IsPressed();

            float targetSprint = sprinting && animatorMoving ? 1f : 0f;
            _sprintWeight = Mathf.Lerp(_sprintWeight, targetSprint, blendAlpha);

            _animator.SetFloat(MoveXHash, _animatorVelocity.x);
            _animator.SetFloat(MoveYHash, _animatorVelocity.y);
            _animator.SetFloat(VelocityHash, speed);
            _animator.SetBool(InAirHash, inAir);
            _animator.SetBool(MovingHash, animatorMoving);
            _animator.SetBool(CrouchingHash, crouching);
            _animator.SetFloat(SprintingHash, _sprintWeight);

            if (animatorMoving != _wasAnimatorMoving && !animatorMoving && stopMotion != null && _fpsAnimator != null)
                PlayIkMotion(stopMotion);

            _wasAnimatorMoving = animatorMoving;

            if (crouching != _wasCrouching && crouchMotion != null && _fpsAnimator != null)
                PlayIkMotion(crouchMotion);

            _wasCrouching = crouching;
        }

        bool EvaluateAnimatorMoving(float speed, bool inAir)
        {
            if (inAir)
                return false;

            if (_wasAnimatorMoving)
                return speed > movingStopThreshold;

            return speed > movingStartThreshold;
        }

        void PlayIkMotion(IkMotionLayerSettings motion)
        {
            if (motion == null || _fpsAnimator == null)
                return;

            _fpsAnimator.LinkAnimatorLayer(motion);
        }

        static void ApplyIkMotionTuning(IkMotionLayerSettings motion, float blendTime, float playRate)
        {
            if (motion == null)
                return;

            motion.blendTime = blendTime;
            motion.playRate = playRate;
        }
    }
}
