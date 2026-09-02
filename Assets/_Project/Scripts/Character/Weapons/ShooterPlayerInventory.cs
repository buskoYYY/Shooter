using UnityEngine;

namespace Shooter.Project.Weapons
{
    [DisallowMultipleComponent]
    public class ShooterPlayerInventory : MonoBehaviour
    {
        [SerializeField] bool hasGun1 = true;
        [SerializeField] bool hasGun2;
        [SerializeField] bool hasGun3;
        [SerializeField] bool hasGun4;
        [SerializeField] bool hasGun5;

        public bool HasWeapon(int slotIndex)
        {
            return slotIndex switch
            {
                0 => hasGun1,
                1 => hasGun2,
                2 => hasGun3,
                3 => hasGun4,
                4 => hasGun5,
                _ => false
            };
        }

        public void SetHasWeapon(int slotIndex, bool owned)
        {
            switch (slotIndex)
            {
                case 0: hasGun1 = owned; break;
                case 1: hasGun2 = owned; break;
                case 2: hasGun3 = owned; break;
                case 3: hasGun4 = owned; break;
                case 4: hasGun5 = owned; break;
            }
        }

        public int SlotCount => 5;
    }
}
