using Lightbug.CharacterControllerPro.Core;
using Lightbug.CharacterControllerPro.Demo;
using UnityEngine;

namespace Shooter.Project.Character
{
    /// <summary>
    /// Delays the CCP jump input so JumpStart can play a short crouch "spring" before takeoff.
    /// Also used by ShooterInputHandler — do not feed Jump to CCP until the wind-up finishes.
    /// </summary>
    [DefaultExecutionOrder(-210)]
    [DisallowMultipleComponent]
    public class ShooterJumpWindup : MonoBehaviour
    {
        public const float DefaultCrouchDelay = 0.14f;

        [Tooltip("Time crouched / coiled before the jump impulse. ~0.12–0.18 feels natural.")]
        [SerializeField] float crouchDelay = DefaultCrouchDelay;

        CharacterActor _characterActor;
        ShooterCharacterController _characterController;
        NormalMovement _normalMovement;

        bool _windupActive;
        bool _fireJump;
        float _windupTimer;
        bool _rawJumpWasPressed;

        public float CrouchDelay
        {
            get => crouchDelay;
            set => crouchDelay = Mathf.Clamp(value, 0.05f, 0.4f);
        }

        public bool IsWindingUp => _windupActive;

        void Awake()
        {
            _characterActor = GetComponent<CharacterActor>();
            _characterController = GetComponent<ShooterCharacterController>();
            _normalMovement = GetComponentInChildren<NormalMovement>();
        }

        void Update()
        {
            if (!_windupActive)
                return;

            if (_characterActor == null || !_characterActor.IsGrounded)
            {
                CancelWindup();
                return;
            }

            _windupTimer -= Time.deltaTime;
            if (_windupTimer > 0f)
                return;

            _windupActive = false;
            _fireJump = true;
        }

        /// <summary>
        /// Maps raw Space to the bool CCP should see. Call from ShooterInputHandler every poll.
        /// </summary>
        public bool ResolveJumpPressed(bool rawPressed)
        {
            bool rawStarted = rawPressed && !_rawJumpWasPressed;
            _rawJumpWasPressed = rawPressed;

            if (_fireJump)
            {
                _fireJump = false;
                return true;
            }

            if (_windupActive)
                return false;

            if (!rawStarted)
                return false;

            if (_characterActor == null || !_characterActor.IsGrounded)
                return false;

            // CCP blocks jump while crouched — wind-up is visual only, not real crouch.
            if (_normalMovement != null && IsMovementCrouched())
                return rawPressed;

            BeginWindup();
            return false;
        }

        void BeginWindup()
        {
            _windupActive = true;
            _windupTimer = crouchDelay;
            _fireJump = false;
            _characterController?.BeginJumpWindupVisual();
        }

        void CancelWindup()
        {
            _windupActive = false;
            _fireJump = false;
            _windupTimer = 0f;
            _characterController?.CancelJumpWindupVisual();
        }

        bool IsMovementCrouched()
        {
            // NormalMovement keeps crouch private; approximate via body height when available.
            if (_characterActor == null)
                return false;

            Vector2 size = _characterActor.BodySize;
            Vector2 defaultSize = _characterActor.DefaultBodySize;
            return size.y < defaultSize.y * 0.9f;
        }

        public void ResetDefaults()
        {
            crouchDelay = DefaultCrouchDelay;
        }
    }
}
