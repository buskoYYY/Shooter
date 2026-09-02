using Shooter.Project.Weapons;
using UnityEngine;

namespace Shooter.Project.Character
{
    /// <summary>
    /// Ensures weapon components exist on the player even if prefab was not updated in Editor.
    /// </summary>
    static class ShooterWeaponRuntimeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureWeaponSystemOnPlayer()
        {
            ShooterCharacterController controller =
                Object.FindFirstObjectByType<ShooterCharacterController>();
            if (controller == null)
                return;

            GameObject player = controller.gameObject;
            if (player.GetComponent<WeaponManager>() != null)
                return;

            ShooterPlayerInventory inventory = player.GetComponent<ShooterPlayerInventory>();
            if (inventory == null)
                inventory = player.AddComponent<ShooterPlayerInventory>();

            inventory.SetHasWeapon(0, true);
            player.AddComponent<WeaponManager>();
        }
    }
}
