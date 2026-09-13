using KINEMATION.FPSAnimationFramework.Runtime.Playables;
using Shooter.Project.Weapons;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Shooter.Project.Character
{
    /// <summary>
    /// Loot pickup gesture for FreeSample ItemPickup clip.
    /// Unarmed = full upper-body; armed = left arm only (mirrored RH → LH).
    /// While playing, forces PlayablesWeight=1 (unarmed normally keeps it at 0).
    /// Also pulls the FPS camera back so the reach stays in frame (armed and unarmed).
    /// </summary>
    [DisallowMultipleComponent]
    public class ShooterItemPickupAnimation : MonoBehaviour
    {
        [SerializeField] FPSAnimationAsset unarmedPickup;
        [SerializeField] FPSAnimationAsset armedLeftHandPickup;
        [Tooltip("Skip if melee/ranged attack/reload is busy.")]
        [SerializeField] bool blockIfWeaponBusy = true;

        [Header("Pickup Camera")]
        [Tooltip("Look-space offset while picking up (negative Z = pull back).")]
        [FormerlySerializedAs("unarmedCameraPull")]
        [SerializeField] Vector3 cameraPull = new Vector3(0f, 0.08f, -0.7f);
        [FormerlySerializedAs("unarmedCameraExtraFov")]
        [SerializeField] float cameraExtraFov = 12f;
        [SerializeField] float cameraBlendIn = 0.12f;
        [SerializeField] float cameraBlendOut = 0.22f;
        [Tooltip("Soft look-down while pickup plays (positive = look down). 0 = leave pitch alone.")]
        [FormerlySerializedAs("unarmedLookDownPitch")]
        [SerializeField] float lookDownPitch = 28f;

        [Header("Debug")]
        [SerializeField] bool enableDebugKey = true;
        [SerializeField] Key debugKey = Key.P;

        IPlayablesController _playables;
        ShooterHandPoseState _handPose;
        WeaponManager _weapons;
        ShooterFpsCameraApply _cameraApply;
        ShooterCharacterController _character;
        float _forcePlayablesUntil = -1f;

        /// <summary>
        /// True while a pickup gesture needs the playables stack visible (unarmed PW is normally 0).
        /// </summary>
        public bool ForcePlayablesActive => Time.time < _forcePlayablesUntil;

        void Awake()
        {
            CacheRefs();
        }

        void Update()
        {
            if (!enableDebugKey || Keyboard.current == null)
                return;
            if (!Keyboard.current[debugKey].wasPressedThisFrame)
                return;

            bool armed = _handPose != null && !_handPose.IsUnarmed;
            FPSAnimationAsset asset = armed ? armedLeftHandPickup : unarmedPickup;
            bool ok = PlayItemPickupAnimation();
            string clipName = asset != null && asset.clip != null ? asset.clip.name : "null";
            Debug.Log(
                $"[Shooter] ItemPickup [{debugKey}] ok={ok} armed={armed} clip={clipName} " +
                $"len={GetPickupDuration():0.00}s forcePW={ForcePlayablesActive}",
                this);
        }

        void CacheRefs()
        {
            _playables = GetComponentInChildren<IPlayablesController>(true);
            _handPose = GetComponent<ShooterHandPoseState>();
            _weapons = GetComponent<WeaponManager>();
            _cameraApply = GetComponent<ShooterFpsCameraApply>();
            _character = GetComponent<ShooterCharacterController>();
        }

        /// <summary>
        /// Plays item-pickup animation for the loot system.
        /// Armed: left-arm layer only. Unarmed: upper-body + camera pullback.
        /// </summary>
        public bool PlayItemPickupAnimation()
        {
            if (_playables == null || _handPose == null || _weapons == null)
                CacheRefs();

            if (_playables == null)
            {
                Debug.LogWarning("[Shooter] PlayItemPickupAnimation: no IPlayablesController.", this);
                return false;
            }

            if (blockIfWeaponBusy && IsWeaponBusy())
            {
                Debug.LogWarning("[Shooter] PlayItemPickupAnimation: weapon busy.", this);
                return false;
            }

            bool armed = _handPose != null && !_handPose.IsUnarmed;
            FPSAnimationAsset asset = armed ? armedLeftHandPickup : unarmedPickup;
            if (!FPSAnimationAsset.IsValid(asset) || asset.clip == null)
            {
                Debug.LogWarning(
                    "[Shooter] PlayItemPickupAnimation: missing AA/clip. " +
                    "Run Shooter → Project → Setup Item Pickup Animation (2.9).",
                    this);
                return false;
            }

            bool played = _playables.PlayAnimation(asset, 0f);
            if (played)
            {
                float duration = asset.clip.length;
                float scale = asset.blendTime.rateScale > 0.01f ? asset.blendTime.rateScale : 1f;
                float blendOut = Mathf.Max(0f, asset.blendTime.blendOutTime);
                float total = (duration / scale) + blendOut + 0.05f;
                _forcePlayablesUntil = Time.time + total;
                BeginPickupCamera(total);
            }

            return played;
        }

        void BeginPickupCamera(float duration)
        {
            if (_cameraApply == null)
                _cameraApply = GetComponent<ShooterFpsCameraApply>();

            _cameraApply?.PlayPickupCameraPull(
                cameraPull,
                cameraExtraFov,
                duration,
                cameraBlendIn,
                cameraBlendOut);

            if (lookDownPitch > 0.01f && _character != null)
                _character.BeginLadderPitchBlend(lookDownPitch, cameraBlendIn);
        }

        public float GetPickupDuration()
        {
            bool armed = _handPose != null && !_handPose.IsUnarmed;
            FPSAnimationAsset asset = armed ? armedLeftHandPickup : unarmedPickup;
            if (!FPSAnimationAsset.IsValid(asset) || asset.clip == null)
                return 0f;

            float scale = asset.blendTime.rateScale > 0.01f ? asset.blendTime.rateScale : 1f;
            return asset.clip.length / scale;
        }

        bool IsWeaponBusy()
        {
            if (_weapons == null || _weapons.ActiveWeapon == null)
                return false;

            WeaponBase weapon = _weapons.ActiveWeapon;
            if (weapon is MeleeWeapon melee && melee.IsBusy)
                return true;
            if (weapon is RangedWeapon ranged && ranged.IsBusy)
                return true;
            return false;
        }
    }
}
