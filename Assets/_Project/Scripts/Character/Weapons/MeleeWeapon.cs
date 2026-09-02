using UnityEngine;

namespace Shooter.Project.Weapons
{
    public class MeleeWeapon : WeaponBase
    {
        [SerializeField] float wearPerAttack = 1f;

        public override void Attack()
        {
            if (IsBroken)
                return;

            ApplyWear(wearPerAttack);
        }

        public override void Reload() { }

        public override void CheckAmmo() { }
    }
}
