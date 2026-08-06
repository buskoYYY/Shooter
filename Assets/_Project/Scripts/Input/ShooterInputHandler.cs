using Lightbug.CharacterControllerPro.Implementation;
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
                "Jump" => _jump.IsPressed(),
                "Run" => _sprint.IsPressed(),
                "Interact" => _interact.IsPressed(),
                "Crouch" => _crouch.IsPressed(),
                "Dash" => false,
                "Jet Pack" => false,
                _ => false
            };
        }

        public override float GetFloat(string actionName)
        {
            if (_playerMap == null)
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

            return actionName switch
            {
                "Movement" => _move.ReadValue<Vector2>(),
                "Camera" => _look.ReadValue<Vector2>(),
                _ => Vector2.zero
            };
        }
    }
}
