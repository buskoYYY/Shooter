using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using KINEMATION.FPSAnimationFramework.Runtime.Core;
using KINEMATION.FPSAnimationFramework.Runtime.Layers.PoseSamplerLayer;
using KINEMATION.FPSAnimationFramework.Runtime.Playables;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

namespace Shooter.Project.Character
{
    /// <summary>
    /// Switches upper-body overlay pose between unarmed (hands down) and armed rifle pose.
    /// Controlled by <see cref="Weapons.WeaponManager"/> (keys 1–6), not a toggle key.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    [DisallowMultipleComponent]
    public class ShooterHandPoseState : MonoBehaviour
    {
        const float SlotStopBlend = 0.08f;
        const float DefaultTransitionBlend = 0.45f;
        const float DefaultOverlayBlendIn = 0.5f;
        const float DefaultOverlayBlendOut = 0.25f;
        const float DefaultTurnInPlaceFadeOutDuration = 0.35f;
        const string TurnInPlaceLayerName = "TurnInPlace";
        const string TurnInPlaceEmptyStateName = "Empty";
        const string IkAnimatorLayerName = "IK";

        static readonly int TurnLeftHash = Animator.StringToHash("TurnLeft");
        static readonly int TurnRightHash = Animator.StringToHash("TurnRight");

        [SerializeField] Transform fpsCharacterRoot;
        [SerializeField] InputActionAsset inputActions;
        [SerializeField] FPSAnimatorProfile fpsAnimatorProfile;
        [SerializeField] FPSAnimationAsset unarmedOverlayPose;
        [SerializeField] FPSAnimationAsset armedOverlayPose;
        [SerializeField] FPSAnimationAsset equipClip;
        [SerializeField] FPSAnimationAsset unequipClip;
        [SerializeField] RuntimeAnimatorController armedLocomotionController;
        [SerializeField] RuntimeAnimatorController unarmedLocomotionOverride;
        [SerializeField] bool startUnarmed = true;

        FPSPlayablesController _playablesController;
        PoseSamplerLayerSettings _poseSampler;
        Animator _animator;
        ShooterCharacterController _characterController;
        bool _isUnarmed;
        bool _applyStartupUnarmed;
        bool _snapStartOverlay;
        bool _isTransitioning;
        Coroutine _transitionCoroutine;
        int _turnInPlaceLayerIndex = -1;
        int _ikAnimatorLayerIndex = -1;
        float _turnInPlaceLayerWeight = 1f;
        float _turnInPlaceFadeOutDuration = DefaultTurnInPlaceFadeOutDuration;

        AnimatorOverrideController _runtimeLocomotionOverride;
        List<KeyValuePair<AnimationClip, AnimationClip>> _unarmedClipOverrides;
        List<KeyValuePair<AnimationClip, AnimationClip>> _armedClipOverrides;

        static readonly int StandingStateHash = Animator.StringToHash("Standing");
        static readonly int EmptyStateHash = Animator.StringToHash("Empty");
        static readonly int InAirBoolHash = Animator.StringToHash("InAir");


        static FieldInfo OverlayPoseMixerField;
        static FieldInfo OverlayActiveIndexField;
        static FieldInfo OverlayBlendingInField;
        static FieldInfo OverlayBlendTimeField;

        public bool IsUnarmed => _isUnarmed;
        public bool IsTransitioning => _isTransitioning;
        public float TurnInPlaceWeight => _turnInPlaceLayerWeight;
        public FPSAnimationAsset EquipClip => equipClip;
        public FPSAnimationAsset UnequipClip => unequipClip;
        public FPSAnimationAsset ArmedOverlayPose => armedOverlayPose;
        public FPSAnimationAsset UnarmedOverlayPose => unarmedOverlayPose;
        public FPSAnimatorProfile FpsAnimatorProfile => fpsAnimatorProfile;
        public Transform FpsCharacterRoot => fpsCharacterRoot;

        public bool IsTurnInPlacePlaying()
        {
            if (_animator == null || _turnInPlaceLayerIndex < 0)
                return false;

            if (_animator.IsInTransition(_turnInPlaceLayerIndex))
            {
                AnimatorStateInfo current = _animator.GetCurrentAnimatorStateInfo(_turnInPlaceLayerIndex);
                AnimatorStateInfo next = _animator.GetNextAnimatorStateInfo(_turnInPlaceLayerIndex);
                return !current.IsName(TurnInPlaceEmptyStateName) || !next.IsName(TurnInPlaceEmptyStateName);
            }

            return !_animator.GetCurrentAnimatorStateInfo(_turnInPlaceLayerIndex).IsName(TurnInPlaceEmptyStateName);
        }

        public void TickTurnInPlaceBlend(bool animatorMoving, bool hasMoveInput)
        {
            // Procedural TurnLayer + animator TIP (LowerBody mask — feet step, arms untouched).
            if (!_isUnarmed)
            {
                _turnInPlaceLayerWeight = 1f;
                ApplyTurnInPlaceLayerWeight();
                return;
            }

            bool wantsLocomotion = animatorMoving || hasMoveInput;
            float target = wantsLocomotion ? 0f : 1f;
            float fadeStep = Time.deltaTime / Mathf.Max(0.01f, _turnInPlaceFadeOutDuration);
            _turnInPlaceLayerWeight = Mathf.MoveTowards(_turnInPlaceLayerWeight, target, fadeStep);
            ApplyTurnInPlaceLayerWeight();
        }

        public void ResetTransitionBlendDefaults()
        {
            SetAssetBlendTime(equipClip, DefaultTransitionBlend, DefaultTransitionBlend);
            SetAssetBlendTime(unequipClip, DefaultTransitionBlend, DefaultTransitionBlend);
            SetAssetBlendTime(armedOverlayPose, DefaultOverlayBlendIn, DefaultOverlayBlendOut);
            SetAssetBlendTime(unarmedOverlayPose, DefaultOverlayBlendIn, DefaultOverlayBlendOut);
        }

        void Awake()
        {
            if (startUnarmed)
                _applyStartupUnarmed = true;

            ResolveReferences();
            CachePoseSampler();
            _characterController = GetComponent<ShooterCharacterController>();
        }

        void OnDisable()
        {
            StopActiveTransition();
        }

        void Start()
        {
            if (_applyStartupUnarmed)
            {
                _applyStartupUnarmed = false;
                _snapStartOverlay = true;
                SetHandPose(true);
                return;
            }

            _isUnarmed = startUnarmed;
            ApplyLocomotionController(_isUnarmed);
            ApplyPoseInstant(startUnarmed ? unarmedOverlayPose : armedOverlayPose);
        }

        void LateUpdate()
        {
            if (_animator != null && _isUnarmed && !_isTransitioning)
                ApplyFullBodyWeightForCurrentState();
        }

        public void SetUnarmed() => SetHandPose(true);

        public void SetArmed() => SetHandPose(false);

        /// <summary>
        /// Re-applies locomotion controller and pose sampler after an external animator swap (e.g. ladder exit).
        /// </summary>
        public void RefreshLocomotionAfterExternalSwap()
        {
            if (_animator != null && _runtimeLocomotionOverride != null
                && _animator.runtimeAnimatorController != _runtimeLocomotionOverride)
            {
                _animator.runtimeAnimatorController = _runtimeLocomotionOverride;
            }

            ApplyLocomotionController(_isUnarmed);
            SyncPoseSamplerSettings(_isUnarmed ? unarmedOverlayPose : armedOverlayPose);
        }

        /// <summary>
        /// Sync profile pose sampler before FPS layers link (e.g. ladder exit).
        /// PoseSamplerLayerJob.Initialize will call PlayPose once — do not call PlayPose again after.
        /// </summary>
        public void PreparePoseForFpsRestore()
        {
            SyncPoseSamplerSettings(_isUnarmed ? unarmedOverlayPose : armedOverlayPose);
        }

        /// <summary>
        /// Sample bones and snap overlay mixer to full weight after a single PlayPose from PoseSampler init.
        /// </summary>
        public void FinalizePoseAfterFpsRestore()
        {
            FPSAnimationAsset pose = _isUnarmed ? unarmedOverlayPose : armedOverlayPose;
            if (pose?.clip == null || fpsCharacterRoot == null)
                return;

            ClearSlotAnimations();
            pose.clip.SampleAnimation(fpsCharacterRoot.gameObject, 0f);
            ForceOverlayPoseFullWeight();
        }

        public void SetHandPose(bool unarmed, bool instant = false)
        {
            FPSAnimationAsset pose = unarmed ? unarmedOverlayPose : armedOverlayPose;
            if (pose == null || _poseSampler == null)
                return;

            if (_isUnarmed == unarmed && _poseSampler.poseToSample == pose && !_isTransitioning)
                return;

            _isUnarmed = unarmed;

            // Kill Animator IK (C_CurveIdle → weapon bones) as soon as holster starts —
            // otherwise idle arms stay twisted until the player moves.
            ApplyIkAnimatorLayerWeight();

            if (unarmed)
                _characterController?.CancelIkMotions();

            // Startup snap: unarmed locomotion first (Docs/TASKS.md sprint/FBW).
            // Holster: defer remap — early unarmed clips + dying armed IK twists hands.
            // Equip: defer remap until overlay settles (Docs/FPS_CAMERA_AND_HANDS.md).
            bool remapLocomotionNow = instant || (unarmed && _snapStartOverlay);
            if (remapLocomotionNow)
                ApplyLocomotionController(unarmed);
            else if (!unarmed)
                ApplyFullBodyWeightForCurrentState();

            _characterController?.SyncFpsLayerWeights();

            if (instant)
            {
                StopActiveTransition();
                ApplyPoseInstant(pose);
                return;
            }

            StopActiveTransition();
            _transitionCoroutine = StartCoroutine(TransitionToPose(pose, unarmed, !remapLocomotionNow));
        }

        IEnumerator TransitionToPose(FPSAnimationAsset targetPose, bool toUnarmed, bool remapLocomotionAtEnd)
        {
            _isTransitioning = true;

            if (_playablesController == null || targetPose.clip == null)
            {
                SyncPoseSamplerSettings(targetPose);
                if (remapLocomotionAtEnd)
                    ApplyLocomotionController(toUnarmed);
                _isTransitioning = false;
                _transitionCoroutine = null;
                _characterController?.SyncFpsLayerWeights();
                yield break;
            }

            PlayableGraph graph = _playablesController.GetPlayableGraph();
            if (!graph.IsValid())
            {
                SyncPoseSamplerSettings(targetPose);
                if (remapLocomotionAtEnd)
                    ApplyLocomotionController(toUnarmed);
                _isTransitioning = false;
                _transitionCoroutine = null;
                _characterController?.SyncFpsLayerWeights();
                yield break;
            }

            // Clear leftover slot/override motion from previous toggles — avoids twisted hands stacking up.
            ClearSlotAnimations();

            bool snapStart = _snapStartOverlay;
            _snapStartOverlay = false;

            if (snapStart)
            {
                SyncPoseSamplerSettings(targetPose);
                ApplyPoseInstant(targetPose);
            }

            yield return null;
            ClearSlotAnimations();

            SyncPoseSamplerSettings(targetPose);

            if (snapStart)
            {
                ApplyPoseInstant(targetPose);
            }
            else
            {
                float blendIn = GetTransitionBlendIn(toUnarmed);
                ApplyOverlayBlend(targetPose, blendIn);
                yield return new WaitForSeconds(Mathf.Max(blendIn, 0.05f));
            }

            ClearSlotAnimations();
            ForceOverlayPoseFullWeight();

            // Locomotion after overlay is stable (holster + equip).
            if (remapLocomotionAtEnd)
                ApplyLocomotionController(toUnarmed);

            _isTransitioning = false;
            _transitionCoroutine = null;
            _characterController?.SyncFpsLayerWeights();
        }

        void ApplyLocomotionController(bool unarmed)
        {
            if (_animator == null)
                return;

            if (!EnsureRuntimeLocomotionOverride())
            {
                // Fallback: legacy controller swap (may hitch pose — keep as last resort).
                RuntimeAnimatorController target = unarmed && unarmedLocomotionOverride != null
                    ? unarmedLocomotionOverride
                    : armedLocomotionController;

                if (target != null && _animator.runtimeAnimatorController != target)
                {
                    _animator.runtimeAnimatorController = target;
                    ResetLocomotionAnimatorPose();
                }

                ApplyFullBodyWeightForCurrentState();
                return;
            }

            _runtimeLocomotionOverride.ApplyOverrides(unarmed ? _unarmedClipOverrides : _armedClipOverrides);

            // Armed: only clear stuck jump — Play(Standing) flashes rifle idle (Docs).
            // Unarmed: must rebind Standing — ApplyOverrides keeps the already-playing
            // rifle idle until Moving exits/re-enters (twisted hands in idle, OK when walking).
            if (unarmed)
                RebindStandingIdleAfterOverride();
            else
                ClearStuckInAirLocomotion();

            ApplyFullBodyWeightForCurrentState();
        }

        void RebindStandingIdleAfterOverride()
        {
            if (_animator == null)
                return;

            ClearStuckInAirLocomotion();
            _animator.Play(StandingStateHash, 0, 0f);
            _animator.Update(0f);
        }

        bool EnsureRuntimeLocomotionOverride()
        {
            if (_runtimeLocomotionOverride != null)
                return true;

            if (_animator == null || unarmedLocomotionOverride == null)
                return false;

            RuntimeAnimatorController baseController = armedLocomotionController;
            if (baseController is AnimatorOverrideController existingOverride)
                baseController = existingOverride.runtimeAnimatorController;

            if (baseController == null)
                baseController = unarmedLocomotionOverride is AnimatorOverrideController templateBase
                    ? templateBase.runtimeAnimatorController
                    : null;

            if (baseController == null)
                return false;

            // Runtime instance — never ApplyOverrides on the project asset (would dirty it).
            _runtimeLocomotionOverride = new AnimatorOverrideController(baseController)
            {
                name = "Runtime_UnarmedLocomotion"
            };

            _unarmedClipOverrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            _armedClipOverrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();

            if (unarmedLocomotionOverride is AnimatorOverrideController template)
            {
                foreach (var pair in template.clips)
                {
                    AnimationClip original = pair.originalClip;
                    if (original == null)
                        continue;

                    AnimationClip unarmedClip = pair.overrideClip != null ? pair.overrideClip : original;
                    _unarmedClipOverrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(original, unarmedClip));
                    _armedClipOverrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(original, original));
                }
            }

            if (_unarmedClipOverrides.Count == 0)
            {
                _runtimeLocomotionOverride = null;
                return false;
            }

            _animator.runtimeAnimatorController = _runtimeLocomotionOverride;
            return true;
        }

        void ClearStuckInAirLocomotion()
        {
            if (_animator == null || !_animator.GetBool(InAirBoolHash))
                return;

            _animator.SetBool(InAirBoolHash, false);

            int inAirLayer = _animator.GetLayerIndex("InAir");
            if (inAirLayer >= 0)
                _animator.Play(EmptyStateHash, inAirLayer, 0f);
        }

        void ResetLocomotionAnimatorPose()
        {
            if (_animator == null)
                return;

            ClearStuckInAirLocomotion();

            _animator.Play(StandingStateHash, 0, 0f);
            _animator.Update(0f);
        }

        void ApplyFullBodyWeightForCurrentState()
        {
            if (_animator == null)
                return;

            _animator.SetFloat(Animator.StringToHash("FullBodyWeight"), _isUnarmed ? 1f : 0f);
            ApplyTurnInPlaceLayerWeight();
            ApplyIkAnimatorLayerWeight();
        }

        void ApplyIkAnimatorLayerWeight()
        {
            if (_animator == null)
                return;

            if (_ikAnimatorLayerIndex < 0)
                _ikAnimatorLayerIndex = _animator.GetLayerIndex(IkAnimatorLayerName);

            if (_ikAnimatorLayerIndex < 0)
                return;

            // IK layer plays C_CurveIdle (ik_hand_gun / WeaponBoneAdditive). Fine while armed;
            // after holster it twists idle arms until locomotion overrides on move.
            _animator.SetLayerWeight(_ikAnimatorLayerIndex, _isUnarmed ? 0f : 1f);
        }

        void ApplyTurnInPlaceLayerWeight()
        {
            if (_animator == null)
                return;

            if (_turnInPlaceLayerIndex < 0)
                _turnInPlaceLayerIndex = _animator.GetLayerIndex(TurnInPlaceLayerName);

            if (_turnInPlaceLayerIndex < 0)
                return;

            // LowerBody mask on controller: foot plant only, no rifle-arm overwrite.
            float weight = _turnInPlaceLayerWeight;
            _animator.SetLayerWeight(_turnInPlaceLayerIndex, weight);

            if (weight <= 0.001f && !IsTurnInPlacePlaying())
            {
                _animator.ResetTrigger(TurnLeftHash);
                _animator.ResetTrigger(TurnRightHash);
            }
        }

        void ApplyOverlayBlend(FPSAnimationAsset pose, float blendInTime)
        {
            if (_playablesController == null || pose?.clip == null)
                return;

            PlayableGraph graph = _playablesController.GetPlayableGraph();
            if (!graph.IsValid())
                return;

            BlendTime savedBlend = pose.blendTime;
            BlendTime blend = savedBlend;
            blend.blendInTime = blendInTime;
            pose.blendTime = blend;

            _playablesController.PlayPose(pose);
            pose.blendTime = savedBlend;
        }

        float GetTransitionBlendIn(bool toUnarmed)
        {
            FPSAnimationAsset transition = toUnarmed ? unequipClip : equipClip;
            if (transition == null)
                return DefaultTransitionBlend;

            return Mathf.Max(0.01f, transition.blendTime.blendInTime);
        }

        static void SetAssetBlendTime(FPSAnimationAsset asset, float blendIn, float blendOut)
        {
            if (asset == null)
                return;

            BlendTime blend = asset.blendTime;
            blend.blendInTime = blendIn;
            blend.blendOutTime = blendOut;
            asset.blendTime = blend;
        }

        void ApplyPoseInstant(FPSAnimationAsset pose)
        {
            SyncPoseSamplerSettings(pose);

            if (_playablesController == null || pose?.clip == null)
                return;

            PlayableGraph graph = _playablesController.GetPlayableGraph();
            if (!graph.IsValid())
                return;

            ClearSlotAnimations();
            _playablesController.PlayPose(pose);
            ForceOverlayPoseFullWeight();
        }

        void ClearSlotAnimations()
        {
            _playablesController?.StopAnimation(SlotStopBlend);
        }

        void StopActiveTransition()
        {
            if (_transitionCoroutine == null)
                return;

            StopCoroutine(_transitionCoroutine);
            _transitionCoroutine = null;
            _isTransitioning = false;
            ClearSlotAnimations();
        }

        void SyncPoseSamplerSettings(FPSAnimationAsset pose)
        {
            if (pose == null || _poseSampler == null)
                return;

            _poseSampler.poseToSample = pose;
            _poseSampler.overwriteRoot = false;
            _poseSampler.overwriteWeaponBone = pose == armedOverlayPose;
        }

        void ForceOverlayPoseFullWeight()
        {
            if (_playablesController is not FPSPlayablesController fpsPlayables)
                return;

            PlayableGraph graph = fpsPlayables.GetPlayableGraph();
            if (!graph.IsValid())
                return;

            CacheOverlayMixerFields();

            object boxedMixer = OverlayPoseMixerField.GetValue(fpsPlayables);
            if (boxedMixer == null)
                return;

            var overlayMixer = (FPSAnimatorMixer)boxedMixer;
            int activeIndex = (int)OverlayActiveIndexField.GetValue(boxedMixer);
            if (activeIndex < 0 || !overlayMixer.mixer.IsValid())
                return;

            var blendTime = (BlendTime)OverlayBlendTimeField.GetValue(boxedMixer);
            blendTime.blendInTime = 0f;
            OverlayBlendTimeField.SetValue(boxedMixer, blendTime);
            OverlayBlendingInField.SetValue(boxedMixer, true);

            overlayMixer = (FPSAnimatorMixer)boxedMixer;
            overlayMixer.Update();

            if (overlayMixer.mixer.IsValid())
                overlayMixer.mixer.SetInputWeight(activeIndex, 1f);

            OverlayBlendingInField.SetValue(boxedMixer, false);
            OverlayPoseMixerField.SetValue(fpsPlayables, overlayMixer);
        }

        static void CacheOverlayMixerFields()
        {
            OverlayPoseMixerField ??= typeof(FPSPlayablesController).GetField(
                "_overlayPoseMixer",
                BindingFlags.Instance | BindingFlags.NonPublic);

            OverlayActiveIndexField ??= typeof(FPSAnimatorMixer).GetField(
                "_activeIndex",
                BindingFlags.Instance | BindingFlags.NonPublic);

            OverlayBlendingInField ??= typeof(FPSAnimatorMixer).GetField(
                "_blendingIn",
                BindingFlags.Instance | BindingFlags.NonPublic);

            OverlayBlendTimeField ??= typeof(FPSAnimatorMixer).GetField(
                "_blendTime",
                BindingFlags.Instance | BindingFlags.NonPublic);
        }

        void ResolveReferences()
        {
            if (inputActions == null)
            {
                ShooterCharacterController bridge = GetComponent<ShooterCharacterController>();
                if (bridge != null)
                {
                    var field = typeof(ShooterCharacterController).GetField(
                        "inputActions",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    inputActions = field?.GetValue(bridge) as InputActionAsset;
                }
            }

            if (fpsCharacterRoot == null)
            {
                Transform graphics = transform.Find("Graphics");
                if (graphics != null && graphics.childCount > 0)
                    fpsCharacterRoot = graphics.GetChild(0);
            }

            if (fpsCharacterRoot == null)
                return;

            _playablesController = fpsCharacterRoot.GetComponent<FPSPlayablesController>();
            _animator = fpsCharacterRoot.GetComponent<Animator>();

            if (armedLocomotionController == null && _animator != null)
                armedLocomotionController = _animator.runtimeAnimatorController;

            if (fpsAnimatorProfile == null)
            {
                FPSAnimator fpsAnimator = fpsCharacterRoot.GetComponent<FPSAnimator>();
                if (fpsAnimator != null)
                {
                    var profileField = typeof(FPSAnimator).GetField(
                        "animatorProfile",
                        BindingFlags.Instance | BindingFlags.NonPublic);

                    fpsAnimatorProfile = profileField?.GetValue(fpsAnimator) as FPSAnimatorProfile;
                }
            }
        }

        void CachePoseSampler()
        {
            if (fpsAnimatorProfile == null)
                return;

            foreach (FPSAnimatorLayerSettings layer in fpsAnimatorProfile.settings)
            {
                if (layer is PoseSamplerLayerSettings sampler)
                {
                    _poseSampler = sampler;
                    return;
                }
            }
        }
    }
}
