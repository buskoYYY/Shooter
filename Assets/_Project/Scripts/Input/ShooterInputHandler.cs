using Lightbug.CharacterControllerPro.Implementation;
using Shooter.Project.Character;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Project.Input
{
    /// <summary>
    /// Bridges Unity Input System (InputSystem_Actions) to Character Controller Pro.
    /// Assign on the same GameObject as CharacterBrain and set Human Input Type to Custom.
    /// </summary>
    [AddComponentMenu("Shooter/Input/Shooter Input Handler")]
    public class ShooterInputHandler : InputHandler
    {
        [SerializeField] InputActionAsset inputActions;

        InputActionMap _playerMap;
        InputAction _move;
        InputAction _look;
        InputAction _jump;
        InputAction _sprint;
        InputAction _crouch;
        InputAction _interact;
        bool _fpsLookHandledExternally;
        ShooterJumpWindup _jumpWindup;

        void Awake()
        {
            if (inputActions == null)
            {
                Debug.LogError("[ShooterInputHandler] InputActionAsset is not assigned.", this);
                return;
            }

            _playerMap = inputActions.FindActionMap("Player", true);
            _move = _playerMap.FindAction("Move", true);
            _look = _playerMap.FindAction("Look", true);
            _jump = _playerMap.FindAction("Jump", true);
            _sprint = _playerMap.FindAction("Sprint", true);
            _crouch = _playerMap.FindAction("Crouch", true);
            _interact = _playerMap.FindAction("Interact", true);
            _fpsLookHandledExternally = GetComponentInParent<ShooterCharacterController>() != null
                || GetComponent<ShooterCharacterController>() != null;
            _jumpWindup = GetComponentInParent<ShooterJumpWindup>()
                ?? GetComponent<ShooterJumpWindup>();
        }

        void OnEnable()
        {
            _playerMap?.Enable();
        }

        void OnDisable()
        {
            _playerMap?.Disable();
        }

        public override bool GetBool(string actionName)
        {
            if (_playerMap == null)
                return false;

            return actionName switch
            {
                "Jump" => ResolveJump(),
                "Run" => _sprint.IsPressed(),
                "Interact" => _interact.IsPressed(),
                "Crouch" => _crouch.IsPressed(),
                "Dash" => false,
                "Jet Pack" => false,
                _ => false
            };
        }

        bool ResolveJump()
        {
            bool raw = _jump != null && _jump.IsPressed();
            if (_jumpWindup == null)
            {
                _jumpWindup = GetComponentInParent<ShooterJumpWindup>()
                    ?? GetComponent<ShooterJumpWindup>();
            }

            return _jumpWindup != null ? _jumpWindup.ResolveJumpPressed(raw) : raw;
        }

        public override float GetFloat(string actionName)
        {
            if (_playerMap == null)
                return 0f;

            if (_fpsLookHandledExternally)
                return 0f;

            return actionName switch
            {
                "Pitch" => _look.ReadValue<Vector2>().y,
                "Roll" => 0f,
                _ => 0f
            };
        }

        public override Vector2 GetVector2(string actionName)
        {
            if (_playerMap == null)
                return Vector2.zero;

            if (_fpsLookHandledExternally && actionName == "Camera")
                return Vector2.zero;

            return actionName switch
            {
                "Movement" => _move.ReadValue<Vector2>(),
                "Camera" => _look.ReadValue<Vector2>(),
                _ => Vector2.zero
            };
        }
    }
}
