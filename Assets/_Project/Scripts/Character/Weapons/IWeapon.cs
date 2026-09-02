namespace Shooter.Project.Weapons
{
    public interface IWeapon
    {
        int SlotIndex { get; }
        bool IsBroken { get; }
        float Durability { get; }
        float MaxDurability { get; }

        void Equip();
        void Unequip();
        void Attack();
        void Reload();
        void CheckAmmo();
        void OnBreak();
    }
}
