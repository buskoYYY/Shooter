using System;
using UnityEngine;

namespace Shooter.Project.Weapons
{
    [Serializable]
    public class ShooterWeaponLoadout
    {
        public ShooterWeaponDefinition definition;
        public int magazine;
        public int reserve;

        public bool IsMelee => definition != null && definition.kind == ShooterWeaponKind.Melee;
        public bool HasAmmoInMag => IsMelee || magazine > 0;

        public static ShooterWeaponLoadout FromDefinition(ShooterWeaponDefinition definition)
        {
            if (definition == null)
                return null;

            return new ShooterWeaponLoadout
            {
                definition = definition,
                magazine = definition.kind == ShooterWeaponKind.Melee ? 0 : definition.magazineSize,
                reserve = definition.kind == ShooterWeaponKind.Melee ? 0 : definition.startReserveAmmo
            };
        }

        public bool TryReload()
        {
            if (definition == null || IsMelee)
                return false;

            int missing = definition.magazineSize - magazine;
            if (missing <= 0 || reserve <= 0)
                return false;

            int take = Mathf.Min(missing, reserve);
            magazine += take;
            reserve -= take;
            return true;
        }

        public bool TryConsumeShot()
        {
            if (IsMelee)
                return true;
            if (magazine <= 0)
                return false;
            magazine--;
            return true;
        }

        public void AddReserve(int amount)
        {
            if (IsMelee)
                return;
            reserve = Mathf.Max(0, reserve + amount);
        }
    }
}
