using KINEMATION.FPSAnimationFramework.Runtime.Camera;
using KINEMATION.FPSAnimationFramework.Runtime.Core;
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
        [SerializeField] float lookSensitivity = 0.15f;
        [SerializeField] float pitchClamp = 89f;

        CharacterActor _characterActor;
        CharacterStateController _stateController;
        NormalMovement _normalMovement;
        FPSAnimator _fpsAnimator;
        UserInputController _userInput;
        Animator _animator;
        FPSCameraController _fpsCamera;

        Vector2 _mouseInput;

        public float Pitch => _mouseInput.y;
        public FPSCameraController FpsCamera => _fpsCamera;

        InputActionMap _playerMap;
        InputAction _move;
        InputAction _look;
        InputAction _sprint;
        InputAction _crouch;

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
            }

            // FPSPlayablesController.Update runs before FPSAnimator.Start — init early.
            if (_fpsAnimator != null)
                _fpsAnimator.Initialize();
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

            if (_fpsCamera != null && _userInput != null)
                _fpsCamera.Initialize();
        }

        void Update()
        {
            UpdateLook();

            if (IsOnLadder())
                return;

            UpdateFpsInput();
            SyncAnimatorFromCcp();
        }

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
            if (_look == null)
                return;

            Vector2 lookDelta = _look.ReadValue<Vector2>() * lookSensitivity;

            _mouseInput.x += lookDelta.x;
            _mouseInput.y -= lookDelta.y;
            _mouseInput.y = Mathf.Clamp(_mouseInput.y, -pitchClamp, pitchClamp);

            transform.rotation *= Quaternion.Euler(0f, lookDelta.x, 0f);
        }

        void UpdateFpsInput()
        {
            if (_userInput == null)
                return;

            Vector2 lookDelta = _look != null ? _look.ReadValue<Vector2>() * lookSensitivity : Vector2.zero;
            Vector2 move = _move != null ? _move.ReadValue<Vector2>() : Vector2.zero;
            bool sprinting = _sprint != null && _sprint.IsPressed();

            _userInput.SetValue(FPSANames.MouseDeltaInput, new Vector4(lookDelta.x, lookDelta.y, 0f, 0f));
            _userInput.SetValue(FPSANames.MouseInput, new Vector4(_mouseInput.x, _mouseInput.y, 0f, 0f));
            _userInput.SetValue(FPSANames.MoveInput, new Vector4(move.x, move.y, 0f, 0f));
            _userInput.SetValue(FPSANames.StabilizationWeight, sprinting ? 0f : 1f);
            _userInput.SetValue(FPSANames.PlayablesWeight, sprinting ? 0f : 1f);
        }

        void SyncAnimatorFromCcp()
        {
            if (_animator == null || _characterActor == null)
                return;

            Vector3 localVelocity = transform.InverseTransformDirection(_characterActor.PlanarVelocity);
            Vector2 animatorVelocity = new Vector2(localVelocity.x, localVelocity.z);
            float speed = _characterActor.PlanarVelocity.magnitude;
            bool moving = speed > 0.1f;
            bool sprinting = _sprint != null && _sprint.IsPressed();
            bool crouching = _crouch != null && _crouch.IsPressed();

            _animator.SetFloat(MoveXHash, animatorVelocity.x);
            _animator.SetFloat(MoveYHash, animatorVelocity.y);
            _animator.SetFloat(VelocityHash, speed);
            _animator.SetBool(InAirHash, !_characterActor.IsGrounded);
            _animator.SetBool(MovingHash, moving);
            _animator.SetBool(CrouchingHash, crouching);
            _animator.SetFloat(SprintingHash, sprinting && moving ? 1f : 0f);
        }
    }

}
