using System.Collections;
using KINEMATION.FPSAnimationFramework.Runtime.Core;
using KINEMATION.FPSAnimationFramework.Runtime.Playables;
using KINEMATION.Shared.KAnimationCore.Runtime.Input;
using Lightbug.CharacterControllerPro.Core;
using Lightbug.CharacterControllerPro.Demo;
using Lightbug.CharacterControllerPro.Implementation;
using UnityEngine;

namespace Shooter.Project.Character
{
    /// <summary>
    /// Releases FPS playables during CCP ladder climbing so LadderClimbing.controller + root motion work.
    /// Keeps ShooterCharacterController enabled so Input System / CharacterBrain stay live.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public class ShooterLadderFpsBridge : MonoBehaviour
    {
        [SerializeField] Transform fpsCharacterRoot;
        [SerializeField] RuntimeAnimatorController locomotionController;
        [SerializeField] FPSAnimatorProfile fpsAnimatorProfile;
        [SerializeField] float jumpOffUpSpeed = 5f;
        [SerializeField] float jumpOffBackSpeed = 4f;

        const string LookLayerWeightProperty = "LookLayerWeight";

        CharacterActor _characterActor;
        CharacterBrain _characterBrain;
        CharacterStateController _stateController;
        UserInputController _userInput;
        Animator _animator;
        ShooterFpsCameraApply _cameraApply;
        FPSAnimator _fpsAnimator;
        FPSPlayablesController _playablesController;
        FPSBoneController _boneController;

        bool _ladderModeActive;
        bool _jumpPressed;
        bool _applyRootMotionWasEnabled;
        Coroutine _restoreFpsCoroutine;

        bool IsOnLadder =>
            _stateController != null && _stateController.CurrentState is LadderClimbing;

        void Awake()
        {
            _characterActor = GetComponent<CharacterActor>();
            _characterBrain = GetComponentInChildren<CharacterBrain>();
            _stateController = GetComponentInChildren<CharacterStateController>();
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
                _fpsAnimator = fpsCharacterRoot.GetComponent<FPSAnimator>();
                _playablesController = fpsCharacterRoot.GetComponent<FPSPlayablesController>();
                _boneController = fpsCharacterRoot.GetComponent<FPSBoneController>();
            }

            if (fpsAnimatorProfile == null && fpsCharacterRoot != null)
            {
                var entity = fpsCharacterRoot.GetComponent<FPSAnimatorEntity>();
                if (entity != null)
                    fpsAnimatorProfile = entity.animatorProfile;
            }
        }

        void OnEnable()
        {
            if (_stateController != null)
                _stateController.OnStateChange += HandleStateChange;
        }

        void OnDisable()
        {
            if (_stateController != null)
                _stateController.OnStateChange -= HandleStateChange;

            if (_restoreFpsCoroutine != null)
            {
                StopCoroutine(_restoreFpsCoroutine);
                _restoreFpsCoroutine = null;
            }

            if (!_ladderModeActive)
                return;

            _ladderModeActive = false;

            if (_animator != null)
            {
                _animator.applyRootMotion = _applyRootMotionWasEnabled;
                if (locomotionController != null)
                    _animator.runtimeAnimatorController = locomotionController;
            }
        }

        void Update()
        {
            SyncLadderModeFlag();

            if (!IsOnLadder || _characterBrain == null)
                return;

            if (_characterBrain.CharacterActions.jump.Started)
                _jumpPressed = true;
        }

        void FixedUpdate()
        {
            if (!_jumpPressed || !IsOnLadder)
                return;

            _jumpPressed = false;
            JumpOffLadder();
        }

        void SyncLadderModeFlag()
        {
            if (IsOnLadder && !_ladderModeActive)
                EnterLadderMode();
            else if (!IsOnLadder && _ladderModeActive)
                ExitLadderMode();
        }

        void HandleStateChange(CharacterState from, CharacterState to)
        {
            if (to is LadderClimbing)
                EnterLadderMode();
            else if (from is LadderClimbing)
                ExitLadderMode();
        }

        void EnterLadderMode()
        {
            if (_ladderModeActive)
                return;

            _ladderModeActive = true;

            if (_restoreFpsCoroutine != null)
            {
                StopCoroutine(_restoreFpsCoroutine);
                _restoreFpsCoroutine = null;
            }

            if (_fpsAnimator != null)
                _fpsAnimator.enabled = false;

            ReleaseFpsAnimationStack();

            if (_animator != null)
            {
                _applyRootMotionWasEnabled = _animator.applyRootMotion;
                _animator.applyRootMotion = true;

                var ladderState = _stateController.GetState<LadderClimbing>();
                if (ladderState != null && ladderState.RuntimeAnimatorController != null)
                    _animator.runtimeAnimatorController = ladderState.RuntimeAnimatorController;
            }

            if (_userInput != null)
            {
                _userInput.SetValue(FPSANames.StabilizationWeight, 0f);
                _userInput.SetValue(FPSANames.PlayablesWeight, 0f);
                _userInput.SetValue(LookLayerWeightProperty, 0f);
            }

            if (_cameraApply != null)
                _cameraApply.enabled = false;
        }

        void ReleaseFpsAnimationStack()
        {
            if (_boneController != null)
                _boneController.Dispose();

            if (_playablesController == null)
                return;

            if (_playablesController.enabled)
                _playablesController.SetControllerWeight(0f);

            _playablesController.enabled = false;
        }

        void ExitLadderMode()
        {
            if (!_ladderModeActive)
                return;

            _ladderModeActive = false;

            if (_animator != null)
            {
                _animator.applyRootMotion = _applyRootMotionWasEnabled;
                if (locomotionController != null)
                    _animator.runtimeAnimatorController = locomotionController;
            }

            RestoreFpsInputWeights();
            _restoreFpsCoroutine = StartCoroutine(RestoreFpsAfterControllerSwap());

            if (_cameraApply != null)
                _cameraApply.enabled = true;
        }

        void RestoreFpsInputWeights()
        {
            if (_userInput == null)
                return;

            _userInput.SetValue(FPSANames.StabilizationWeight, 1f);
            _userInput.SetValue(FPSANames.PlayablesWeight, 1f);
            _userInput.SetValue(LookLayerWeightProperty, 1f);
        }

        IEnumerator RestoreFpsAfterControllerSwap()
        {
            if (_playablesController != null)
                _playablesController.enabled = false;

            if (_fpsAnimator != null)
                _fpsAnimator.enabled = false;

            for (int i = 0; i < 12; i++)
            {
                yield return null;

                if (TryRestoreFpsStack())
                {
                    _restoreFpsCoroutine = null;
                    yield break;
                }
            }

            TryRestoreFpsStack();
            _restoreFpsCoroutine = null;
        }

        bool TryRestoreFpsStack()
        {
            if (_animator == null || _playablesController == null)
                return false;

            if (!_animator.isActiveAndEnabled || !_animator.playableGraph.IsValid())
                return false;

            if (!_playablesController.InitializeController())
                return false;

            _playablesController.enabled = true;

            if (_boneController != null)
            {
                _boneController.Initialize();
                if (fpsAnimatorProfile != null)
                    _boneController.LinkAnimatorProfile(fpsAnimatorProfile);
            }

            if (_fpsAnimator != null)
            {
                _fpsAnimator.enabled = true;
                _fpsAnimator.RebuildPlayables();
            }

            return true;
        }

        void JumpOffLadder()
        {
            if (_characterActor == null || _stateController == null)
                return;

            Vector3 pushDirection = -_characterActor.Forward;
            _stateController.ForceState<NormalMovement>();
            _characterActor.Velocity = Vector3.up * jumpOffUpSpeed + pushDirection * jumpOffBackSpeed;

            if (_ladderModeActive)
                ExitLadderMode();
        }
    }

}
