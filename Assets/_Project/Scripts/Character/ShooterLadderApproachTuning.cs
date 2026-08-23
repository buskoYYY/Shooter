using Lightbug.CharacterControllerPro.Demo;
using UnityEngine;

namespace Shooter.Project.Character
{
    /// <summary>
    /// Tunes smooth ladder entry approach on CCP LadderClimbing state.
    /// </summary>
    [DefaultExecutionOrder(-198)]
    [DisallowMultipleComponent]
    public class ShooterLadderApproachTuning : MonoBehaviour
    {
        public const float DefaultApproachDuration = 0.55f;
        public const float DefaultApproachSnapDistance = LadderClimbing.DefaultApproachSnapDistance;

        [SerializeField] float approachDuration = DefaultApproachDuration;
        [SerializeField] float approachSnapDistance = DefaultApproachSnapDistance;

        LadderClimbing _ladderClimbing;

        public float ApproachDuration
        {
            get => approachDuration;
            set
            {
                approachDuration = Mathf.Clamp(value, 0.05f, 2f);
                ApplyTuning();
            }
        }

        public float ApproachSnapDistance
        {
            get => approachSnapDistance;
            set
            {
                approachSnapDistance = Mathf.Clamp(value, 0.01f, 0.5f);
                ApplyTuning();
            }
        }

        void Awake()
        {
            _ladderClimbing = GetComponentInChildren<LadderClimbing>();
            ApplyTuning();
        }

        void OnEnable()
        {
            ApplyTuning();
        }

        public void ResetDefaults()
        {
            approachDuration = DefaultApproachDuration;
            approachSnapDistance = DefaultApproachSnapDistance;
            ApplyTuning();
        }

        public void ApplyTuning()
        {
            if (_ladderClimbing == null)
                _ladderClimbing = GetComponentInChildren<LadderClimbing>();

            if (_ladderClimbing == null)
                return;

            _ladderClimbing.ApproachDuration = approachDuration;
            _ladderClimbing.ApproachSnapDistance = approachSnapDistance;
        }
    }
}
