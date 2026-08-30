using System;
using UnityEngine;

namespace Shooter.Project.Weapons
{
    public enum ShooterWeaponKind
    {
        Pistol,
        Rifle,
        Melee
    }

    public enum ShooterFireMode
    {
        Semi,
        Auto
    }

    public enum WeaponBlockReason
    {
        None,
        Unarmed,
        NoWeapon,
        Sprinting,
        Airborne,
        JumpWindup,
        Ladder,
        Busy,
        EmptyMag
    }

    [Serializable]
    public struct ShooterDamageInfo
    {
        public float amount;
        public Vector3 point;
        public Vector3 normal;
        public GameObject instigator;
        public ShooterWeaponKind weaponKind;
    }

    public interface IDamageable
    {
        void ApplyDamage(in ShooterDamageInfo damage);
    }
}
