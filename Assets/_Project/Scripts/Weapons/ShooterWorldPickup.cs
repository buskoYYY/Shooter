using UnityEngine;

namespace Shooter.Project.Weapons
{
    /// <summary>
    /// World pickup. Uses a trigger so it does not steal Interact from the ladder.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ShooterWorldPickup : MonoBehaviour
    {
        public enum PickupKind
        {
            Weapon,
            Ammo
        }

        [SerializeField] PickupKind kind = PickupKind.Weapon;
        [SerializeField] ShooterWeaponDefinition weapon;
        [SerializeField] int ammoAmount = 30;
        [SerializeField] AudioClip pickupClip;

        public void ConfigureWeapon(ShooterWeaponDefinition definition)
        {
            kind = PickupKind.Weapon;
            weapon = definition;
        }

        public void ConfigureAmmo(ShooterWeaponDefinition matching, int amount)
        {
            kind = PickupKind.Ammo;
            weapon = matching;
            ammoAmount = amount;
        }

        void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            var inventory = other.GetComponentInParent<ShooterWeaponInventory>();
            var controller = other.GetComponentInParent<ShooterWeaponController>();
            if (inventory == null || controller == null)
                return;

            if (kind == PickupKind.Ammo)
            {
                if (!controller.TryPickupAmmo(weapon, ammoAmount))
                    return;
            }
            else if (!controller.TryPickupWeapon(weapon))
            {
                return;
            }

            if (pickupClip != null)
                AudioSource.PlayClipAtPoint(pickupClip, transform.position);

            Destroy(gameObject);
        }
    }
}
