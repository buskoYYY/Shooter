using Lightbug.CharacterControllerPro.Core;
using Lightbug.CharacterControllerPro.Demo;
using Lightbug.CharacterControllerPro.Implementation;
using Shooter.Project.Character;
using UnityEngine;

namespace Shooter.Project.Weapons
{
    /// <summary>
    /// Single place that decides whether the player may fire, reload, or draw/switch a weapon.
    /// Matches the customer rule: no shooting or drawing while jumping, sprinting, or on a ladder.
    /// </summary>
    [DisallowMultipleComponent]
    public class ShooterWeaponActionGate : MonoBehaviour
    {
        ShooterCharacterController _character;
        ShooterHandPoseState _handPose;
        ShooterJumpWindup _jumpWindup;
        ShooterLadderFpsBridge _ladder;
        CharacterActor _actor;
        CharacterStateController _states;

        bool _busy;
        float _busyUntil = -1f;

        public bool IsBusy => _busy || Time.unscaledTime < _busyUntil;

        public void SetBusy(bool busy)
        {
            _busy = busy;
            if (!busy)
                _busyUntil = -1f;
        }

        public void SetBusyFor(float seconds)
        {
            _busy = false;
            _busyUntil = Time.unscaledTime + Mathf.Max(0f, seconds);
        }

        void Awake()
        {
            _character = GetComponent<ShooterCharacterController>();
            _handPose = GetComponent<ShooterHandPoseState>();
            _jumpWindup = GetComponent<ShooterJumpWindup>();
            _ladder = GetComponent<ShooterLadderFpsBridge>();
            _actor = GetComponent<CharacterActor>();
            _states = GetComponentInChildren<CharacterStateController>();
        }

        public bool CanFire(out WeaponBlockReason reason)
        {
            if (!CanUseHands(out reason))
                return false;

            if (_handPose != null && _handPose.IsUnarmed)
            {
                reason = WeaponBlockReason.Unarmed;
                return false;
            }

            reason = WeaponBlockReason.None;
            return true;
        }

        public bool CanDraw(out WeaponBlockReason reason)
        {
            return CanUseHands(out reason);
        }

        public bool CanReload(out WeaponBlockReason reason)
        {
            if (!CanFire(out reason))
                return false;

            reason = WeaponBlockReason.None;
            return true;
        }

        bool CanUseHands(out WeaponBlockReason reason)
        {
            if (IsBusy)
            {
                reason = WeaponBlockReason.Busy;
                return false;
            }

            if (IsOnLadder())
            {
                reason = WeaponBlockReason.Ladder;
                return false;
            }

            if (_jumpWindup != null && _jumpWindup.IsWindingUp)
            {
                reason = WeaponBlockReason.JumpWindup;
                return false;
            }

            if (_actor != null && !_actor.IsGrounded)
            {
                reason = WeaponBlockReason.Airborne;
                return false;
            }

            if (_character != null && _character.IsSprinting)
            {
                reason = WeaponBlockReason.Sprinting;
                return false;
            }

            reason = WeaponBlockReason.None;
            return true;
        }

        bool IsOnLadder()
        {
            if (_ladder != null && (_ladder.IsLadderModeActive || _ladder.ShouldUseLadderCamera))
                return true;

            return _states != null && _states.CurrentState is LadderClimbing;
        }
    }
}
