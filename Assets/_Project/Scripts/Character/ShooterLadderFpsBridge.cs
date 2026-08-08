using KINEMATION.FPSAnimationFramework.Runtime.Core;
using KINEMATION.Shared.KAnimationCore.Runtime.Input;
using Lightbug.CharacterControllerPro.Demo;
using Lightbug.CharacterControllerPro.Implementation;
using UnityEngine;

namespace Shooter.Project.Character
{
    /// <summary>
    /// Disables FPS procedural layers during CCP ladder climbing and restores locomotion animator after.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public class ShooterLadderFpsBridge : MonoBehaviour
    {
        [SerializeField] Transform fpsCharacterRoot;
        [SerializeField] RuntimeAnimatorController locomotionController;

        const string LookLayerWeightProperty = "LookLayerWeight";

        CharacterStateController _stateController;
        UserInputController _userInput;
        Animator _animator;
        ShooterCharacterController _characterController;
        ShooterFpsCameraApply _cameraApply;

        bool _wasOnLadder;

        void Awake()
        {
            _stateController = GetComponentInChildren<CharacterStateController>();
            _characterController = GetComponent<ShooterCharacterController>();
            _cameraApply = GetComponent<ShooterFpsCameraApply>();

            if (fpsCharacterRoot == null)
            {
                var graphics = transform.Find("Graphics");
                if (graphics != null && graphics.childCount > 0)
                    fpsCharacterRoot = graphics.GetChild(0);
            }

            if (fpsCharacterRoot != null)
            {
                _userInput = fpsCharacterRoot.GetComponent<UserInputController>();
                _animator = fpsCharacterRoot.GetComponent<Animator>();
            }
        }

        void Update()
        {
            if (_stateController == null)
                return;

            bool onLadder = _stateController.CurrentState is LadderClimbing;

            if (onLadder && !_wasOnLadder)
                EnterLadderMode();
            else if (!onLadder && _wasOnLadder)
                ExitLadderMode();

            _wasOnLadder = onLadder;
        }

        void EnterLadderMode()
        {
            if (_userInput != null)
            {
                _userInput.SetValue(FPSANames.StabilizationWeight, 0f);
                _userInput.SetValue(FPSANames.PlayablesWeight, 0f);
                _userInput.SetValue(LookLayerWeightProperty, 0f);
            }

            if (_characterController != null)
                _characterController.enabled = false;
            if (_cameraApply != null)
                _cameraApply.enabled = false;
        }

        void ExitLadderMode()
        {
            if (_animator != null && locomotionController != null)
                _animator.runtimeAnimatorController = locomotionController;

            if (_userInput != null)
            {
                _userInput.SetValue(FPSANames.StabilizationWeight, 1f);
                _userInput.SetValue(FPSANames.PlayablesWeight, 1f);
                _userInput.SetValue(LookLayerWeightProperty, 1f);
            }

            if (_characterController != null)
                _characterController.enabled = true;
            if (_cameraApply != null)
                _cameraApply.enabled = true;
        }
    }

}
