using KINEMATION.FPSAnimationFramework.Runtime.Camera;
using KINEMATION.FPSAnimationFramework.Runtime.Core;
using KINEMATION.FPSAnimationFramework.Runtime.Layers.IkMotionLayer;
using KINEMATION.FPSAnimationFramework.Runtime.Recoil;
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
        public const float DefaultLocomotionSmoothingStart = 7f;
        public const float DefaultLocomotionSmoothingStop = 8f;
        public const float DefaultMovingStartThreshold = 0.18f;
        public const float DefaultMovingStopThreshold = 0.05f;
        public const float DefaultTurnInPlaceLocomotionSmoothing = 2.5f;
        public const float DefaultJumpBlendTime = 0.15f;
        public const float DefaultJumpPlayRate = 1f;
        public const float DefaultStopBlendTime = 0.35f;
        public const float DefaultStopPlayRate = 0.75f;
        public const float DefaultCrouchBlendTime = 0.3f;
        public const float DefaultCrouchPlayRate = 0.9f;
        public const float DefaultStabilizationWeight = 0f;
        public const float DefaultLookLayerWeightUnarmed = 0.3f;
        public const float DefaultLookLayerWeightArmed = 1f;
        public const float LegacyStabilizationWeight = 1f;
        public const float LegacyLookLayerWeight = 1f;

        public static bool UseLegacySlouchedPosture { get; private set; }

        public static string PostureCompareLabel =>
            UseLegacySlouchedPosture ? "Legacy (slouched)" : "Current (straight)";

        public static void TogglePostureCompareMode() =>
            UseLegacySlouchedPosture = !UseLegacySlouchedPosture;

        public static void GetActivePostureWeights(bool isArmed, out float stabilizationWeight, out float lookLayerWeight)
        {
            if (UseLegacySlouchedPosture)
            {
                stabilizationWeight = LegacyStabilizationWeight;
                lookLayerWeight = LegacyLookLayerWeight;
                return;
            }

            stabilizationWeight = DefaultStabilizationWeight;
            lookLayerWeight = isArmed ? DefaultLookLayerWeightArmed : DefaultLookLayerWeightUnarmed;
        }

        [SerializeField] Transform fpsCharacterRoot;
        [SerializeField] InputActionAsset inputActions;
        [SerializeField] IkMotionLayerSettings jumpMotion;
        [SerializeField] IkMotionLayerSettings stopMotion;
        [SerializeField] IkMotionLayerSettings crouchMotion;
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
        [Tooltip("Slower leg blend ramp when starting to move during a turn-in-place step.")]
        [SerializeField] float turnInPlaceLocomotionSmoothing = DefaultTurnInPlaceLocomotionSmoothing;
        [SerializeField] float jumpBlendTime = DefaultJumpBlendTime;
        [SerializeField] float jumpPlayRate = DefaultJumpPlayRate;
        [SerializeField] float stopBlendTime = DefaultStopBlendTime;
        [SerializeField] float stopPlayRate = DefaultStopPlayRate;
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
        RecoilPattern _recoilPattern;

        Vector2 _mouseInput;
        Vector2 _animatorVelocity;
        float _sprintWeight;
        bool _ladderPitchBlendActive;
        float _ladderPitchTarget;
        float _ladderPitchSmoothTime = 0.35f;
        float _ladderPitchVelocity;
        bool _wasCrouching;
        bool _wasAnimatorMoving;
        bool _playedJumpMotionSinceLand;
        bool _jumpWindupVisualActive;
        int _inAirLayerIndex = -1;

        static readonly int JumpStartStateHash = Animator.StringToHash("JumpStart");

        public float Pitch => _mouseInput.y;
        public FPSCameraController FpsCamera => _fpsCamera;
        public IkMotionLayerSettings JumpMotion => jumpMotion;
        public IkMotionLayerSettings StopMotion => stopMotion;
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

        public bool HasMoveInput =>
            _move != null && _move.ReadValue<Vector2>().sqrMagnitude > 0.01f;

        public bool IsSprinting =>
            _sprint != null && _sprint.IsPressed() && HasMoveInput &&
            (_characterActor == null || _characterActor.IsGrounded);

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
            crouchBlendTime = DefaultCrouchBlendTime;
            crouchPlayRate = DefaultCrouchPlayRate;
            ApplyMotionTuning();
        }

        public void ApplyMotionTuning()
        {
            ApplyIkMotionTuning(jumpMotion, jumpBlendTime, jumpPlayRate);
            ApplyIkMotionTuning(stopMotion, stopBlendTime, stopPlayRate);
            ApplyIkMotionTuning(crouchMotion, crouchBlendTime, crouchPlayRate);
        }

        public void BeginLadderPitchBlend(float targetPitch, float smoothTime)
        {
            _ladderPitchBlendActive = true;
            _ladderPitchTarget = Mathf.Clamp(targetPitch, -pitchClamp, pitchClamp);
            _ladderPitchSmoothTime = Mathf.Max(0.05f, smoothTime);
            _ladderPitchVelocity = 0f;
        }

        public void EndLadderPitchBlend()
        {
            _ladderPitchBlendActive = false;
            _ladderPitchVelocity = 0f;
        }

        InputActionMap _playerMap;
        InputAction _move;
        InputAction _look;
        InputAction _sprint;
        InputAction _crouch;

        const string LookLayerWeightProperty = "LookLayerWeight";
        const string TurnInPlaceWeightProperty = "TurnInPlaceWeight";

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
                _recoilPattern = fpsCharacterRoot.GetComponent<RecoilPattern>();
                EnsureEventReceiver(fpsCharacterRoot.gameObject);
            }

            // Detached FPS Camera lives under the player root, not under Graphics.
            if (_fpsCamera == null)
                _fpsCamera = GetComponentInChildren<FPSCameraController>(true);

            if (inputActions != null)
            {
                _playerMap = inputActions.FindActionMap("Player", true);
                _move = _playerMap.FindAction("Move", true);
                _look = _playerMap.FindAction("Look", true);
                _sprint = _playerMap.FindAction("Sprint", true);
                _crouch = _playerMap.FindAction("Crouch", true);
            }

            EnsureFpsCameraApplyOnSelf();
            EnsureJumpWindupOnSelf();
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

        static void EnsureEventReceiver(GameObject animatorRoot)
        {
            if (animatorRoot == null)
                return;
            if (animatorRoot.GetComponent<EventReceiver>() == null)
                animatorRoot.AddComponent<EventReceiver>();
        }

        void EnsureJumpWindupOnSelf()
        {
            if (GetComponent<ShooterJumpWindup>() != null)
                return;

            gameObject.AddComponent<ShooterJumpWindup>();
        }

        void EnsureBodySizeTuningOnSelf()
        {
            if (GetComponent<ShooterBodySizeTuning>() != null)
                return;

            if (GetComponent<CharacterBody>() == null)
                return;

            gameObject.AddComponent<ShooterBodySizeTuning>();
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
            EnsureBodySizeTuningOnSelf();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            ConfigureCcpForFps();

            BindJumpEvents();
            SyncFpsLayerWeights();
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

            if (!_jumpWindupVisualActive)
                ApplyJumpBodyAnimationImmediate();
            else
                EnsureInAirFlag();

            _jumpWindupVisualActive = false;
            PlayIkMotion(jumpMotion);
        }

        public void BeginJumpWindupVisual()
        {
            _jumpWindupVisualActive = true;
            ApplyJumpBodyAnimationImmediate();
            PlayIkMotion(crouchMotion);
        }

        public void CancelJumpWindupVisual()
        {
            if (!_jumpWindupVisualActive)
                return;

            _jumpWindupVisualActive = false;

            if (_animator != null && (_characterActor == null || _characterActor.IsGrounded))
                _animator.SetBool(InAirHash, false);
        }

        void EnsureInAirFlag()
        {
            if (_animator != null)
                _animator.SetBool(InAirHash, true);
        }

        void HandleLanded(Vector3 _)
        {
            if (_animator != null)
                _animator.SetBool(InAirHash, false);

            _jumpWindupVisualActive = false;

            if (!_playedJumpMotionSinceLand)
                return;

            _playedJumpMotionSinceLand = false;
            PlayIkMotion(jumpMotion);
        }

        void ApplyJumpBodyAnimationImmediate()
        {
            if (_animator == null)
                return;

            _animator.SetBool(InAirHash, true);

            if (_inAirLayerIndex < 0)
                _inAirLayerIndex = _animator.GetLayerIndex("InAir");

            if (_inAirLayerIndex >= 0)
                _animator.Play(JumpStartStateHash, _inAirLayerIndex, 0f);
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

        void LateUpdate()
        {
            if (ShooterBalanceTuningPanel.IsOpen || _userInput == null)
                return;

            if (IsInLadderState() || ShouldDeferFpsLocomotion())
                return;

            SyncFpsLayerWeights();
        }

        bool ShouldDeferFpsLocomotion() =>
            _ladderBridge != null && _ladderBridge.ShouldBlockFpsPlayables;

        bool IsInLadderState() =>
            _stateController != null && _stateController.CurrentState is LadderClimbing;

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

            if (_ladderPitchBlendActive)
            {
                if (Mathf.Abs(lookDelta.y) > 0.05f)
                {
                    _ladderPitchBlendActive = false;
                }
                else
                {
                    _mouseInput.y = Mathf.SmoothDamp(
                        _mouseInput.y,
                        _ladderPitchTarget,
                        ref _ladderPitchVelocity,
                        _ladderPitchSmoothTime);
                    _mouseInput.y = Mathf.Clamp(_mouseInput.y, -pitchClamp, pitchClamp);

                    if (Mathf.Abs(_mouseInput.y - _ladderPitchTarget) < 0.15f)
                        _ladderPitchBlendActive = false;
                }
            }

            if (IsInLadderState())
                return;

            _mouseInput.x += lookDelta.x;
            _characterActor.Rotation *= Quaternion.Euler(0f, lookDelta.x, 0f);

            if (_recoilPattern != null)
            {
                Vector2 recoil = _recoilPattern.GetRecoilDelta();
                _mouseInput.y += recoil.y;
                _mouseInput.y = Mathf.Clamp(_mouseInput.y, -pitchClamp, pitchClamp);
                _characterActor.Rotation *= Quaternion.Euler(0f, recoil.x, 0f);
            }

            _characterActor.ResetInterpolationRotation();
        }

        void UpdateFpsInput()
        {
            if (_userInput == null)
                return;

            Vector2 lookDelta = _look != null ? _look.ReadValue<Vector2>() * lookSensitivity : Vector2.zero;

            _userInput.SetValue(FPSANames.MouseDeltaInput, new Vector4(lookDelta.x, lookDelta.y, 0f, 0f));
            _userInput.SetValue(FPSANames.MouseInput, new Vector4(_mouseInput.x, _mouseInput.y, 0f, 0f));
            _userInput.SetValue(FPSANames.MoveInput, new Vector4(_animatorVelocity.x, _animatorVelocity.y, 0f, 0f));
            _userInput.SetValue(FPSANames.LeanInput, 0f);

            if (ShouldDeferFpsLocomotion())
                return;

            ApplyFpsLayerWeights();
        }

        public void SyncFpsLayerWeights()
        {
            ApplyFpsLayerWeights();
        }

        void ApplyFpsLayerWeights()
        {
            if (_userInput == null)
                return;

            bool isArmed = _handPoseState == null || !_handPoseState.IsUnarmed;
            GetActivePostureWeights(isArmed, out float stabilizationWeight, out float lookLayerWeight);
            _userInput.SetValue(FPSANames.StabilizationWeight, stabilizationWeight);
            _userInput.SetValue(LookLayerWeightProperty, lookLayerWeight);
            UpdateTurnInPlaceWeight();
            UpdatePlayablesWeight();
        }

        void UpdateTurnInPlaceWeight()
        {
            if (_userInput == null)
                return;

            float weight = _handPoseState != null
                ? _handPoseState.TurnInPlaceWeight
                : 1f;

            _userInput.SetValue(TurnInPlaceWeightProperty, weight);
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

            bool inAir = _characterActor != null && !_characterActor.IsGrounded;
            Vector2 moveInput = _move != null ? _move.ReadValue<Vector2>() : Vector2.zero;
            Vector2 targetVelocity = inAir ? Vector2.zero : moveInput;

            float targetSpeed = targetVelocity.magnitude;
            float currentSpeed = _animatorVelocity.magnitude;
            bool accelerating = targetSpeed > currentSpeed + 0.01f;
            float smoothing = accelerating ? locomotionSmoothingStart : locomotionSmoothingStop;

            if (_handPoseState != null
                && _handPoseState.IsUnarmed
                && _handPoseState.IsTurnInPlacePlaying()
                && targetSpeed > 0.01f)
            {
                smoothing = Mathf.Min(smoothing, turnInPlaceLocomotionSmoothing);
            }

            float blendAlpha = KMath.ExpDecayAlpha(smoothing, Time.deltaTime);
            _animatorVelocity = Vector2.Lerp(_animatorVelocity, targetVelocity, blendAlpha);

            float speed = Mathf.Clamp01(_animatorVelocity.magnitude);
            bool animatorMoving = EvaluateAnimatorMoving(speed, inAir);
            _handPoseState?.TickTurnInPlaceBlend(animatorMoving, HasMoveInput);
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
