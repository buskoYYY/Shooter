using System.Collections.Generic;
using UnityEngine;
using Lightbug.CharacterControllerPro.Core;
using Lightbug.CharacterControllerPro.Implementation;
using Lightbug.Utilities;
using static UnityEngine.EventSystems.PointerEventData;

namespace Lightbug.CharacterControllerPro.Demo
{

    [AddComponentMenu("Character Controller Pro/Demo/Character/States/Ladder Climbing")]
    public class LadderClimbing : CharacterState
    {

        [Header("Offset")]

        [SerializeField]
        protected bool useIKOffsetValues = false;

        [Condition("useIKOffsetValues", ConditionAttribute.ConditionType.IsTrue)]
        [SerializeField]
        protected Vector3 rightFootOffset = Vector3.zero;

        [Condition("useIKOffsetValues", ConditionAttribute.ConditionType.IsTrue)]
        [SerializeField]
        protected Vector3 leftFootOffset = Vector3.zero;

        [Condition("useIKOffsetValues", ConditionAttribute.ConditionType.IsTrue)]
        [SerializeField]
        protected Vector3 rightHandOffset = Vector3.zero;

        [Condition("useIKOffsetValues", ConditionAttribute.ConditionType.IsTrue)]
        [SerializeField]
        protected Vector3 leftHandOffset = Vector3.zero;

        [Header("Activation")]

        [SerializeField]
        protected bool useInteractAction = true;

        [SerializeField]
        protected bool filterByAngle = true;

        [SerializeField]
        protected float maxAngle = 70f;

        [Header("Animation")]

        [SerializeField]
        protected string bottomDownParameter = "BottomDown";

        [SerializeField]
        protected string bottomUpParameter = "BottomUp";

        [SerializeField]
        protected string topDownParameter = "TopDown";

        [SerializeField]
        protected string topUpParameter = "TopUp";

        [SerializeField]
        protected string upParameter = "Up";

        [SerializeField]
        protected string downParameter = "Down";

        [Space(10f)]

        [SerializeField]
        protected string entryStateName = "Entry";

        [SerializeField]
        protected string exitStateName = "Exit";

        [SerializeField]
        protected string idleStateName = "Idle";

        [Header("Smooth entry")]

        [SerializeField]
        protected bool useSmoothApproach = true;

        [SerializeField]
        protected float approachDuration = 0.4f;

        [SerializeField]
        protected float approachSnapDistance = 0.05f;

        public const float DefaultApproachDuration = 0.4f;
        public const float DefaultApproachSnapDistance = 0.05f;

        public float ApproachDuration
        {
            get => approachDuration;
            set => approachDuration = Mathf.Clamp(value, 0.05f, 2f);
        }

        public float ApproachSnapDistance
        {
            get => approachSnapDistance;
            set => approachSnapDistance = Mathf.Clamp(value, 0.01f, 0.5f);
        }

        public void ResetApproachDefaults()
        {
            useSmoothApproach = true;
            approachDuration = DefaultApproachDuration;
            approachSnapDistance = DefaultApproachSnapDistance;
        }
        // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
        // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

        protected Dictionary<Transform, Ladder> ladders = new Dictionary<Transform, Ladder>();

        public enum LadderClimbState
        {
            Entering,
            Exiting,
            Idling,
            Climbing,
        }

        protected LadderClimbState state;
        protected Ladder currentLadder = null;
        protected Vector3 targetPosition = Vector3.zero;
        protected int currentClimbingAnimation = 0;
        protected bool forceExit = false;
        protected AnimatorStateInfo animatorStateInfo;
        protected bool isBottom = false;
        protected bool approachingEntry = false;
        protected float approachElapsed = 0f;
        protected Vector3 approachStart = Vector3.zero;

        public bool IsApproachingEntry => approachingEntry;

        public override void CheckExitTransition()
        {
            if (forceExit)
                CharacterStateController.EnqueueTransition<NormalMovement>();
        }

        public override bool CheckEnterTransition(CharacterState fromState)
        {
            if (!CharacterActor.IsGrounded)
                return false;

            if (useInteractAction && !CharacterActions.interact.Started)
                return false;

            for (int i = 0; i < CharacterActor.Triggers.Count; i++)
            {
                Trigger trigger = CharacterActor.Triggers[i];

                Ladder ladder = ladders.GetOrRegisterValue(trigger.transform);

                if (ladder != null)
                {
                    if (!useInteractAction && CharacterActor.WasGrounded && !trigger.firstContact)
                    {
                        return false;
                    }

                    currentLadder = ladder;

                    // Check if the character is closer to the top of the ladder.
                    float distanceToTop = Vector3.Distance(CharacterActor.Position, currentLadder.TopReference.position);
                    float distanceToBottom = Vector3.Distance(CharacterActor.Position, currentLadder.BottomReference.position);

                    isBottom = distanceToBottom < distanceToTop;

                    if (filterByAngle)
                    {
                        Vector3 ladderToCharacter = CharacterActor.Position - currentLadder.transform.position;
                        ladderToCharacter = Vector3.ProjectOnPlane(ladderToCharacter, currentLadder.transform.up);

                        float angle = Vector3.Angle(currentLadder.FacingDirectionVector, ladderToCharacter);

                        if (isBottom)
                        {
                            if (angle >= maxAngle)
                                return true;
                            else
                                continue;
                        }
                        else
                        {
                            if (angle <= maxAngle)
                                return true;
                            else
                                continue;
                        }
                    }
                    else
                    {
                        return true;
                    }
                }
            }
            return false;
        }



        public override void EnterBehaviour(float dt, CharacterState fromState)
        {
            CharacterActor.Velocity = Vector3.zero;
            CharacterActor.IsKinematic = true;

            currentClimbingAnimation = isBottom ? 0 : currentLadder.ClimbingAnimations;

            targetPosition = isBottom ? currentLadder.BottomReference.position : currentLadder.TopReference.position;
            CharacterActor.Forward = currentLadder.FacingDirectionVector;

            float distance = Vector3.Distance(CharacterActor.Position, targetPosition);
            if (useSmoothApproach && distance > approachSnapDistance)
            {
                approachingEntry = true;
                approachElapsed = 0f;
                approachStart = CharacterActor.Position;
                return;
            }

            CompleteLadderEntry();
        }

        void CompleteLadderEntry()
        {
            approachingEntry = false;
            CharacterActor.alwaysNotGrounded = true;
            CharacterActor.Position = targetPosition;
            CharacterActor.Forward = currentLadder.FacingDirectionVector;

            CharacterActor.SetUpRootMotion(
                true,
                PhysicsActor.RootMotionVelocityType.SetVelocity,
                false
            );
            CharacterActor.Animator.SetTrigger(isBottom ? bottomUpParameter : topDownParameter);

            state = LadderClimbState.Entering;
        }



        public override void ExitBehaviour(float dt, CharacterState toState)
        {
            approachingEntry = false;
            CharacterActor.Up = Vector3.up;
            forceExit = false;
            CharacterActor.IsKinematic = false;
            CharacterActor.alwaysNotGrounded = false;
            currentLadder = null;
            CharacterStateController.ResetIKWeights();
            CharacterActor.Velocity = Vector3.zero;
            CharacterActor.ForceGrounded();
        }

        protected override void Awake()
        {
            base.Awake();

#if UNITY_2023_1_OR_NEWER
            Ladder[] laddersArray = FindObjectsByType<Ladder>(FindObjectsSortMode.None);
#else
            Ladder[] laddersArray = FindObjectsOfType<Ladder>();
#endif
            for (int i = 0; i < laddersArray.Length; i++)
                ladders.Add(laddersArray[i].transform, laddersArray[i]);
        }

        protected override void Start()
        {
            base.Start();

            if (CharacterActor.Animator == null)
            {
                Debug.Log("The LadderClimbing state needs the character to have a reference to an Animator component. Destroying this state...");
                Destroy(this);
            }
        }

        public override void UpdateBehaviour(float dt)
        {
            if (approachingEntry)
            {
                approachElapsed += dt;
                float duration = Mathf.Max(0.01f, approachDuration);
                float t = Mathf.Clamp01(approachElapsed / duration);
                t = t * t * (3f - 2f * t);

                CharacterActor.Position = Vector3.Lerp(approachStart, targetPosition, t);
                CharacterActor.Forward = Vector3.Slerp(
                    CharacterActor.Forward,
                    currentLadder.FacingDirectionVector,
                    t
                );

                if (t >= 1f || Vector3.Distance(CharacterActor.Position, targetPosition) <= approachSnapDistance)
                    CompleteLadderEntry();

                return;
            }

            animatorStateInfo = CharacterActor.Animator.GetCurrentAnimatorStateInfo(0);
            switch (state)
            {
                case LadderClimbState.Entering:

                    // The LadderClimbing state has just begun, Make sure to wait for the "Idle" animation state.
                    // Important: Note that the animation clip from this state (Animator) is the same as the locomotion idle clip.
                    if (animatorStateInfo.IsName(idleStateName))
                        state = LadderClimbState.Idling;

                    break;


                case LadderClimbState.Idling:

                    // This state is responsible for handling inputs and setting animation triggers.                    
                    if (CharacterActions.interact.Started)
                    {
                        if (useInteractAction)
                            forceExit = true;
                    }
                    else
                    {
                        if (CharacterActions.movement.Up)
                        {
                            if (currentClimbingAnimation == currentLadder.ClimbingAnimations)
                            {
                                CharacterActor.Animator.SetTrigger(topUpParameter);
                                state = LadderClimbState.Exiting;
                            }
                            else
                            {
                                CharacterActor.Animator.SetTrigger(upParameter);
                                currentClimbingAnimation++;
                                state = LadderClimbState.Climbing;
                            }
                            
                        }
                        else if (CharacterActions.movement.Down)
                        {
                            
                            if (currentClimbingAnimation == 0)
                            {
                                CharacterActor.Animator.SetTrigger(bottomDownParameter);                                
                                state = LadderClimbState.Exiting;
                            }
                            else
                            {
                                CharacterActor.Animator.SetTrigger(downParameter);
                                currentClimbingAnimation--;
                                state = LadderClimbState.Climbing;
                            }
                        }
                    }

                    break;

                case LadderClimbState.Climbing:

                    // Do nothing and wait for the "Idle" animation state
                    if (animatorStateInfo.IsName(idleStateName))
                        state = LadderClimbState.Idling;
                    
                    break;
                case LadderClimbState.Exiting:

                    // Do nothing and wait for the "Exit" animation state
                    // Important: Note that the animation clip from this state (Animator) is the same as the locomotion idle clip.
                    if (animatorStateInfo.IsName(exitStateName))
                    {
                        forceExit = true;
                        CharacterActor.ForceGrounded();
                    }
                    
                    break;

            }
        }
        
        public override void UpdateIK(int layerIndex)
        {
            if (!useIKOffsetValues)
                return;


            UpdateIKElement(AvatarIKGoal.LeftFoot, leftFootOffset);
            UpdateIKElement(AvatarIKGoal.RightFoot, rightFootOffset);
            UpdateIKElement(AvatarIKGoal.LeftHand, leftHandOffset);
            UpdateIKElement(AvatarIKGoal.RightHand, rightHandOffset);

        }

        void UpdateIKElement(AvatarIKGoal avatarIKGoal, Vector3 offset)
        {
            // Get the original (weight = 0) ik position.
            CharacterActor.Animator.SetIKPositionWeight(avatarIKGoal, 0f);
            Vector3 originalRightFootPosition = CharacterActor.Animator.GetIKPosition(avatarIKGoal);

            // Affect the original ik position with the offset.
            CharacterActor.Animator.SetIKPositionWeight(avatarIKGoal, 1f);
            CharacterActor.Animator.SetIKPosition(avatarIKGoal, originalRightFootPosition + offset);
        }


    }

}
