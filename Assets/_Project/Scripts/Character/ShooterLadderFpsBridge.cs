using System.Collections;
using System.Reflection;
using KINEMATION.FPSAnimationFramework.Runtime.Core;
using KINEMATION.FPSAnimationFramework.Runtime.Playables;
using KINEMATION.Shared.KAnimationCore.Runtime.Input;
using Lightbug.CharacterControllerPro.Core;
using Lightbug.CharacterControllerPro.Demo;
using Lightbug.CharacterControllerPro.Implementation;
using UnityEngine;
using UnityEngine.Playables;

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
        const int FpsRestoreMaxAttempts = 90;
        const int LadderEntryTriggerAttempts = 10;
        const string BottomUpTrigger = "BottomUp";
        const string TopDownTrigger = "TopDown";

        CharacterActor _characterActor;
        CharacterBrain _characterBrain;
        CharacterStateController _stateController;
        ShooterCharacterController _shooterController;
        ShooterHandPoseState _handPoseState;
        UserInputController _userInput;
        Animator _animator;
        ShooterFpsCameraApply _cameraApply;
        FPSAnimator _fpsAnimator;
        FPSPlayablesController _playablesController;
        FPSBoneController _boneController;

        bool _ladderModeActive;
        bool _isRestoringFps;

        public bool IsLadderModeActive => _ladderModeActive;
        public bool IsRestoringFps => _isRestoringFps;
        public bool ShouldBlockFpsPlayables => _ladderModeActive || _isRestoringFps;
        bool _jumpPressed;
        bool _applyRootMotionWasEnabled;
        Coroutine _restoreFpsCoroutine;
        Coroutine _ladderSetupCoroutine;

        bool IsOnLadder =>
            _stateController != null && _stateController.CurrentState is LadderClimbing;

        Animator CharacterAnimator => _characterActor != null ? _characterActor.Animator : _animator;

        void Awake()
        {
            _characterActor = GetComponent<CharacterActor>();
            _characterBrain = GetComponentInChildren<CharacterBrain>();
            _stateController = GetComponentInChildren<CharacterStateController>();
            _shooterController = GetComponent<ShooterCharacterController>();
            _handPoseState = GetComponent<ShooterHandPoseState>();
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

            ResolveAnimatorProfileFromFpsAnimator();
        }

        void ResolveAnimatorProfileFromFpsAnimator()
        {
            if (fpsAnimatorProfile != null || _fpsAnimator == null)
                return;

            FieldInfo profileField = typeof(FPSAnimator).GetField(
                "animatorProfile",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            fpsAnimatorProfile = profileField?.GetValue(_fpsAnimator) as FPSAnimatorProfile;
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

            if (_ladderSetupCoroutine != null)
            {
                StopCoroutine(_ladderSetupCoroutine);
                _ladderSetupCoroutine = null;
            }

            _isRestoringFps = false;

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
            if (!IsOnLadder || _characterBrain == null)
                return;

            if (_characterBrain.CharacterActions.jump.Started)
                _jumpPressed = true;
        }

        void LateUpdate()
        {
            SyncLadderModeFlag();
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
            if (_isRestoringFps)
                return;

            if (IsOnLadder && !_ladderModeActive)
                EnterLadderMode();
            else if (!IsOnLadder && _ladderModeActive)
                ExitLadderMode();
        }

        void HandleStateChange(CharacterState from, CharacterState to)
        {
            if (from is LadderClimbing)
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

            if (_ladderSetupCoroutine != null)
                StopCoroutine(_ladderSetupCoroutine);

            _isRestoringFps = false;

            if (_userInput != null)
            {
                _userInput.SetValue(FPSANames.StabilizationWeight, 0f);
                _userInput.SetValue(FPSANames.PlayablesWeight, 0f);
                _userInput.SetValue(LookLayerWeightProperty, 0f);
            }

            _shooterController?.ResetPitchForLadder();

            if (_cameraApply != null)
            {
                _cameraApply.PrepareCameraBeforeInit();
                _cameraApply.ForceRefresh();
            }

            _ladderSetupCoroutine = StartCoroutine(SetupLadderModeRoutine());
        }

        IEnumerator SetupLadderModeRoutine()
        {
            ReleaseFpsForLadder();

            yield return null;
            yield return new WaitForEndOfFrame();

            PrepareLadderAnimator(rebind: true);

            for (int attempt = 0; attempt < LadderEntryTriggerAttempts; attempt++)
            {
                ApplyLadderEntryTrigger();

                if (IsLadderEntryAnimating())
                    break;

                yield return null;
                EnsureLadderController();
            }

            _ladderSetupCoroutine = null;
        }

        void ReleaseFpsForLadder()
        {
            if (_fpsAnimator != null)
            {
                _fpsAnimator.UnlinkAnimatorProfile();
                _fpsAnimator.enabled = false;
            }

            if (_boneController != null)
                _boneController.Dispose();

            if (_playablesController == null)
                return;

            if (_playablesController.enabled)
                _playablesController.SetControllerWeight(0f);

            _playablesController.enabled = false;

            PlayableGraph graph = _playablesController.GetPlayableGraph();
            if (graph.IsValid())
            {
                graph.Stop();
                graph.Destroy();
            }
        }

        void PrepareLadderAnimator(bool rebind)
        {
            EnsureLadderController();

            if (!rebind)
                return;

            Animator animator = CharacterAnimator;
            if (animator == null)
                return;

            animator.Rebind();
            animator.Update(0f);
        }

        void EnsureLadderController()
        {
            Animator animator = CharacterAnimator;
            if (animator == null)
                return;

            _applyRootMotionWasEnabled = animator.applyRootMotion;
            animator.applyRootMotion = true;

            var ladderState = _stateController.GetState<LadderClimbing>();
            if (ladderState != null && ladderState.RuntimeAnimatorController != null)
                animator.runtimeAnimatorController = ladderState.RuntimeAnimatorController;
        }

        void ApplyLadderEntryTrigger()
        {
            Animator animator = CharacterAnimator;
            if (animator == null || !TryGetLadderEntryTrigger(out string triggerName))
                return;

            animator.ResetTrigger(BottomUpTrigger);
            animator.ResetTrigger(TopDownTrigger);
            animator.SetTrigger(triggerName);
            animator.Update(0f);
        }

        bool IsLadderEntryAnimating()
        {
            Animator animator = CharacterAnimator;
            if (animator == null)
                return false;

            if (IsLadderAnimState(animator.GetCurrentAnimatorStateInfo(0)))
                return true;

            if (!animator.IsInTransition(0))
                return false;

            return IsLadderAnimState(animator.GetNextAnimatorStateInfo(0));
        }

        static bool IsLadderAnimState(AnimatorStateInfo stateInfo)
        {
            return stateInfo.IsName("BottomUp")
                || stateInfo.IsName("TopDown")
                || stateInfo.IsName("Entry");
        }

        bool TryGetLadderEntryTrigger(out string triggerName)
        {
            triggerName = null;

            Ladder ladder = FindClosestLadder();
            if (ladder == null || ladder.TopReference == null || ladder.BottomReference == null)
                return false;

            float distanceToTop = Vector3.Distance(_characterActor.Position, ladder.TopReference.position);
            float distanceToBottom = Vector3.Distance(_characterActor.Position, ladder.BottomReference.position);
            triggerName = distanceToBottom <= distanceToTop ? BottomUpTrigger : TopDownTrigger;
            return true;
        }

        Ladder FindClosestLadder()
        {
            Ladder fromTriggers = FindClosestLadderFromTriggers();
            if (fromTriggers != null)
                return fromTriggers;

#if UNITY_2023_1_OR_NEWER
            Ladder[] ladders = FindObjectsByType<Ladder>(FindObjectsSortMode.None);
#else
            Ladder[] ladders = FindObjectsOfType<Ladder>();
#endif
            Ladder closest = null;
            float closestSqrDistance = float.MaxValue;

            for (int i = 0; i < ladders.Length; i++)
            {
                Ladder ladder = ladders[i];
                if (ladder == null || ladder.TopReference == null || ladder.BottomReference == null)
                    continue;

                float sqrDistance = (_characterActor.Position - ladder.transform.position).sqrMagnitude;
                if (sqrDistance >= closestSqrDistance)
                    continue;

                closestSqrDistance = sqrDistance;
                closest = ladder;
            }

            return closest;
        }

        Ladder FindClosestLadderFromTriggers()
        {
            if (_characterActor == null)
                return null;

            Ladder closestLadder = null;
            float closestSqrDistance = float.MaxValue;

            for (int i = 0; i < _characterActor.Triggers.Count; i++)
            {
                var trigger = _characterActor.Triggers[i];
                if (trigger.gameObject == null)
                    continue;

                var ladder = trigger.transform.GetComponentInParent<Ladder>();
                if (ladder == null || ladder.TopReference == null || ladder.BottomReference == null)
                    continue;

                float sqrDistance = (_characterActor.Position - trigger.transform.position).sqrMagnitude;
                if (sqrDistance >= closestSqrDistance)
                    continue;

                closestSqrDistance = sqrDistance;
                closestLadder = ladder;
            }

            return closestLadder;
        }

        void ExitLadderMode()
        {
            if (!_ladderModeActive)
                return;

            if (_ladderSetupCoroutine != null)
            {
                StopCoroutine(_ladderSetupCoroutine);
                _ladderSetupCoroutine = null;
            }

            if (_restoreFpsCoroutine != null)
            {
                StopCoroutine(_restoreFpsCoroutine);
                _restoreFpsCoroutine = null;
            }

            _isRestoringFps = true;
            _ladderModeActive = false;
            HoldFpsOverlayUntilRestored();

            if (TryRestoreFpsStack())
            {
                _isRestoringFps = false;
                _cameraApply?.ForceRefresh();
                return;
            }

            _restoreFpsCoroutine = StartCoroutine(RestoreFpsAfterControllerSwap());
        }

        void HoldFpsOverlayUntilRestored()
        {
            if (_userInput == null)
                return;

            _userInput.SetValue(FPSANames.PlayablesWeight, 0f);
            _userInput.SetValue(LookLayerWeightProperty, 0f);
            _userInput.SetValue(FPSANames.StabilizationWeight, 0f);
        }

        void RestoreFpsLookWeights()
        {
            if (_userInput == null)
                return;

            _userInput.SetValue(FPSANames.StabilizationWeight, 1f);
            _userInput.SetValue(LookLayerWeightProperty, 1f);
        }

        void RestorePlayablesWeight()
        {
            _userInput?.SetValue(FPSANames.PlayablesWeight, 1f);
        }

        void RestoreFpsInputWeights()
        {
            RestoreFpsLookWeights();
            RestorePlayablesWeight();
        }

        IEnumerator RestoreFpsAfterControllerSwap()
        {
            if (_playablesController != null)
                _playablesController.enabled = false;

            if (_fpsAnimator != null)
                _fpsAnimator.enabled = false;

            for (int i = 0; i < FpsRestoreMaxAttempts; i++)
            {
                WarmUpAnimatorGraph();
                yield return null;

                if (TryRestoreFpsStack())
                {
                    _isRestoringFps = false;
                    _restoreFpsCoroutine = null;
                    yield break;
                }
            }

            if (TryForceAnimatorRecovery())
            {
                _isRestoringFps = false;
                _restoreFpsCoroutine = null;
                yield break;
            }

            Debug.LogWarning($"{nameof(ShooterLadderFpsBridge)}: failed to restore FPS animation stack after ladder.");
            _isRestoringFps = false;
            _restoreFpsCoroutine = null;
        }

        void WarmUpAnimatorGraph()
        {
            Animator animator = CharacterAnimator;
            if (animator == null || !animator.isActiveAndEnabled)
                return;

            animator.Update(0f);
        }

        bool TryForceAnimatorRecovery()
        {
            Animator animator = CharacterAnimator;
            if (animator == null)
                return false;

            animator.Rebind();
            WarmUpAnimatorGraph();

            for (int i = 0; i < 15; i++)
            {
                if (TryRestoreFpsStack())
                    return true;

                WarmUpAnimatorGraph();
            }

            return false;
        }

        void ApplyLocomotionController()
        {
            Animator animator = CharacterAnimator;
            if (animator == null)
                return;

            animator.applyRootMotion = _applyRootMotionWasEnabled;
            if (locomotionController != null)
                animator.runtimeAnimatorController = locomotionController;

            animator.Update(0f);
        }

        bool TryRestoreFpsStack()
        {
            if (_animator == null || _playablesController == null)
                return false;

            ApplyLocomotionController();

            if (!_animator.isActiveAndEnabled || !_animator.playableGraph.IsValid())
                return false;

            if (!_playablesController.InitializeController())
                return false;

            _playablesController.SetControllerWeight(1f);
            _playablesController.enabled = true;

            _fpsAnimator?.UnlinkAnimatorProfile();

            if (_boneController != null)
                _boneController.Initialize();

            _handPoseState?.PreparePoseForFpsRestore();

            if (_fpsAnimator != null && fpsAnimatorProfile != null)
            {
                _fpsAnimator.enabled = true;
                _fpsAnimator.LinkAnimatorProfile(fpsAnimatorProfile);
                _fpsAnimator.RebuildPlayables();
            }

            WarmUpAnimatorGraph();
            _handPoseState?.FinalizePoseAfterFpsRestore();
            RestoreFpsLookWeights();
            RestorePlayablesWeight();
            _cameraApply?.ForceRefresh();
            return _fpsAnimator != null && _fpsAnimator.HasLinkedProfile;
        }

        void JumpOffLadder()
        {
            if (_characterActor == null || _stateController == null)
                return;

            Vector3 pushDirection = -_characterActor.Forward;
            _stateController.ForceState<NormalMovement>();
            _characterActor.Velocity = Vector3.up * jumpOffUpSpeed + pushDirection * jumpOffBackSpeed;
        }
    }

}
