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
        [SerializeField] Transform fpsCharacterRoot;
        [SerializeField] InputActionAsset inputActions;
        [SerializeField] IkMotionLayerSettings jumpMotion;
        [SerializeField] IkMotionLayerSettings leanMotion;
        [SerializeField] IkMotionLayerSettings crouchMotion;
        [SerializeField] float leanAngle = 25f;
        [SerializeField] float lookSensitivity = 0.15f;
        [SerializeField] float pitchClamp = 70f;
        [Tooltip("How quickly leg blend parameters follow input (demo default ~5).")]
        [SerializeField] float locomotionSmoothing = 5f;
        [Tooltip("Blend tree Velocity expects 0-1 from input, not m/s.")]
        [SerializeField] float movingThreshold = 0.05f;

        CharacterActor _characterActor;
        CharacterStateController _stateController;
        NormalMovement _normalMovement;
        ShooterLadderFpsBridge _ladderBridge;
        FPSAnimator _fpsAnimator;
        UserInputController _userInput;
        Animator _animator;
        FPSCameraController _fpsCamera;

        Vector2 _mouseInput;
        Vector2 _animatorVelocity;
        float _sprintWeight;
        float _lastLeanInput;
        bool _wasGrounded = true;
        bool _wasCrouching;

        public float Pitch => _mouseInput.y;
        public FPSCameraController FpsCamera => _fpsCamera;

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

        void Awake()
        {
            _characterActor = GetComponent<CharacterActor>();
            _stateController = GetComponentInChildren<CharacterStateController>();
            _normalMovement = GetComponentInChildren<NormalMovement>();
            _ladderBridge = GetComponent<ShooterLadderFpsBridge>();

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

            // FPSPlayablesController.Update runs before FPSAnimator.Start — init early.
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

            if (_characterActor != null)
                _wasGrounded = _characterActor.IsGrounded;
        }

        void Update()
        {
            if (ShooterBalanceTuningPanel.IsOpen)
                return;

            UpdateLook();

            if (IsOnLadder() || ShouldDeferFpsLocomotion())
            {
                SyncMouseInputOnly();
                return;
            }

            SyncAnimatorFromCcp();
            UpdateFpsInput();
            UpdateJumpLandMotion();
        }

        bool ShouldDeferFpsLocomotion() =>
            _ladderBridge != null && _ladderBridge.ShouldBlockFpsPlayables;

        bool IsOnLadder() =>
            _stateController != null && _stateController.CurrentState is LadderClimbing;

        void ConfigureCcpForFps()
        {
            if (_stateController != null)
            {
                _stateController.MovementReferenceMode = MovementReferenceParameters.MovementReferenceMode.External;
                _stateController.ExternalReference = transform;
            }

            if (_normalMovement != null)
                _normalMovement.lookingDirectionParameters.changeLookingDirection = false;
        }

        void UpdateLook()
        {
            if (_look == null || _characterActor == null)
                return;

            Vector2 lookDelta = _look.ReadValue<Vector2>() * lookSensitivity;

            _mouseInput.y -= lookDelta.y;
            _mouseInput.y = Mathf.Clamp(_mouseInput.y, -pitchClamp, pitchClamp);

            if (IsOnLadder())
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

            // Demo FPS AF: sprint disables spine stabilization, not the overlay pose.
            // PlayablesWeight = 0 exposes rifle locomotion on the upper body (armed flash).
            _userInput.SetValue(FPSANames.StabilizationWeight, sprinting ? 0f : 1f);
            _userInput.SetValue(LookLayerWeightProperty, sprinting ? 0.3f : 1f);
            _userInput.SetValue(FPSANames.PlayablesWeight, 1f);

            UpdateLeanInput();
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

            _fpsAnimator.LinkAnimatorLayer(leanMotion);
            _lastLeanInput = leanValue;
        }

        void SyncMouseInputOnly()
        {
            if (_userInput == null)
                return;

            _userInput.SetValue(FPSANames.MouseInput, new Vector4(_mouseInput.x, _mouseInput.y, 0f, 0f));
        }

        void UpdateJumpLandMotion()
        {
            if (_characterActor == null || _fpsAnimator == null || jumpMotion == null)
                return;

            bool grounded = _characterActor.IsGrounded;

            if (_wasGrounded && !grounded)
                _fpsAnimator.LinkAnimatorLayer(jumpMotion);
            else if (!_wasGrounded && grounded)
                _fpsAnimator.LinkAnimatorLayer(jumpMotion);

            _wasGrounded = grounded;
        }

        void SyncAnimatorFromCcp()
        {
            if (_animator == null || _characterActor == null)
                return;

            bool inAir = !_characterActor.IsGrounded;
            Vector2 moveInput = _move != null ? _move.ReadValue<Vector2>() : Vector2.zero;
            Vector2 targetVelocity = inAir ? Vector2.zero : moveInput;

            float blendAlpha = KMath.ExpDecayAlpha(locomotionSmoothing, Time.deltaTime);
            _animatorVelocity = Vector2.Lerp(_animatorVelocity, targetVelocity, blendAlpha);

            float speed = Mathf.Clamp01(_animatorVelocity.magnitude);
            bool moving = !inAir && speed > movingThreshold;
            bool sprinting = _sprint != null && _sprint.IsPressed();
            bool crouching = _crouch != null && _crouch.IsPressed();

            float targetSprint = sprinting && moving ? 1f : 0f;
            _sprintWeight = Mathf.Lerp(_sprintWeight, targetSprint, blendAlpha);

            _animator.SetFloat(MoveXHash, _animatorVelocity.x);
            _animator.SetFloat(MoveYHash, _animatorVelocity.y);
            _animator.SetFloat(VelocityHash, speed);
            _animator.SetBool(InAirHash, inAir);
            _animator.SetBool(MovingHash, moving);
            _animator.SetBool(CrouchingHash, crouching);
            _animator.SetFloat(SprintingHash, _sprintWeight);

            if (crouching != _wasCrouching && crouchMotion != null && _fpsAnimator != null)
                _fpsAnimator.LinkAnimatorLayer(crouchMotion);

            _wasCrouching = crouching;
        }
    }

}
