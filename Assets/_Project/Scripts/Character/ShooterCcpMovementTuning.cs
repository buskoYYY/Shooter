using Lightbug.CharacterControllerPro.Demo;
using UnityEngine;

namespace Shooter.Project.Character
{
    /// <summary>
    /// Tunes Character Controller Pro planar acceleration/deceleration (actual movement speed ramp).
    /// </summary>
    [DefaultExecutionOrder(-199)]
    [DisallowMultipleComponent]
    public class ShooterCcpMovementTuning : MonoBehaviour
    {
        public const float DefaultBaseSpeed = 5f;
        public const float DefaultBoostSpeed = 7.5f;
        // Was 8/10 — felt sluggish when turning while walking. Demo CCP uses ~50/40.
        public const float DefaultAcceleration = 22f;
        public const float DefaultDeceleration = 24f;

        // KINEMATION demo MovementSettings: jumpHeight=4 (vy), gravity=9 → apex ≈ 0.89m, duration ≈ 0.44s
        public const float DefaultJumpApexHeight = 16f / 18f;
        public const float DefaultJumpApexDuration = 4f / 9f;

        [SerializeField] float baseSpeedLimit = DefaultBaseSpeed;
        [SerializeField] float boostSpeedLimit = DefaultBoostSpeed;
        [SerializeField] float stableGroundedAcceleration = DefaultAcceleration;
        [SerializeField] float stableGroundedDeceleration = DefaultDeceleration;
        [SerializeField] float jumpApexHeight = DefaultJumpApexHeight;
        [SerializeField] float jumpApexDuration = DefaultJumpApexDuration;

        NormalMovement _normalMovement;

        public float BaseSpeedLimit
        {
            get => baseSpeedLimit;
            set
            {
                baseSpeedLimit = Mathf.Max(0.5f, value);
                ApplyTuning();
            }
        }

        public float BoostSpeedLimit
        {
            get => boostSpeedLimit;
            set
            {
                boostSpeedLimit = Mathf.Max(baseSpeedLimit, value);
                ApplyTuning();
            }
        }

        public float StableGroundedAcceleration
        {
            get => stableGroundedAcceleration;
            set
            {
                stableGroundedAcceleration = Mathf.Max(0.5f, value);
                ApplyTuning();
            }
        }

        public float StableGroundedDeceleration
        {
            get => stableGroundedDeceleration;
            set
            {
                stableGroundedDeceleration = Mathf.Max(0.5f, value);
                ApplyTuning();
            }
        }

        public float JumpApexHeight
        {
            get => jumpApexHeight;
            set
            {
                jumpApexHeight = Mathf.Max(0.1f, value);
                ApplyTuning();
            }
        }

        public float JumpApexDuration
        {
            get => jumpApexDuration;
            set
            {
                jumpApexDuration = Mathf.Max(0.05f, value);
                ApplyTuning();
            }
        }

        void Awake()
        {
            _normalMovement = GetComponentInChildren<NormalMovement>();
            ApplyTuning();
        }

        void OnEnable()
        {
            ApplyTuning();
        }

        public void ResetDefaults()
        {
            baseSpeedLimit = DefaultBaseSpeed;
            boostSpeedLimit = DefaultBoostSpeed;
            stableGroundedAcceleration = DefaultAcceleration;
            stableGroundedDeceleration = DefaultDeceleration;
            jumpApexHeight = DefaultJumpApexHeight;
            jumpApexDuration = DefaultJumpApexDuration;
            ApplyTuning();
        }

        public void ApplyTuning()
        {
            if (_normalMovement == null)
                _normalMovement = GetComponentInChildren<NormalMovement>();

            ApplyToMovement(_normalMovement, this);
        }

        public static void ApplyToMovement(NormalMovement movement, ShooterCcpMovementTuning tuning = null)
        {
            if (movement == null)
                return;

            if (tuning != null)
            {
                var planar = movement.planarMovementParameters;
                planar.baseSpeedLimit = tuning.baseSpeedLimit;
                planar.boostSpeedLimit = tuning.boostSpeedLimit;
                planar.stableGroundedAcceleration = tuning.stableGroundedAcceleration;
                planar.stableGroundedDeceleration = tuning.stableGroundedDeceleration;
                // No mid-air steering: keep takeoff planar velocity, ignore move input while airborne.
                planar.notGroundedAcceleration = 0f;
                planar.notGroundedDeceleration = 0f;
            }

            ApplyDemoJumpSettings(movement, tuning?.jumpApexHeight, tuning?.jumpApexDuration);
        }

        public static void ApplyDemoJumpSettings(
            NormalMovement movement,
            float? apexHeight = null,
            float? apexDuration = null)
        {
            if (movement == null)
                return;

            var vertical = movement.verticalMovementParameters;
            vertical.autoCalculate = true;
            vertical.jumpApexHeight = apexHeight ?? DefaultJumpApexHeight;
            vertical.jumpApexDuration = apexDuration ?? DefaultJumpApexDuration;
            vertical.cancelJumpOnRelease = false;
            vertical.preGroundedJumpTime = 0f;
            vertical.postGroundedJumpTime = 0f;
            vertical.availableNotGroundedJumps = 0;
            vertical.UpdateParameters();

            var planar = movement.planarMovementParameters;
            planar.notGroundedAcceleration = 0f;
            planar.notGroundedDeceleration = 0f;
        }
    }
}
