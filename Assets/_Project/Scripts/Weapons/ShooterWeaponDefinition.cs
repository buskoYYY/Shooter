using UnityEngine;

namespace Shooter.Project.Weapons
{
    [CreateAssetMenu(fileName = "Weapon", menuName = "Shooter/Weapon Definition")]
    public class ShooterWeaponDefinition : ScriptableObject
    {
        public string displayName = "Weapon";
        public ShooterWeaponKind kind = ShooterWeaponKind.Rifle;
        public GameObject viewPrefab;

        [Header("Hitscan / melee")]
        public float damage = 25f;
        public float range = 120f;
        public float fireRateRpm = 600f;
        public ShooterFireMode fireMode = ShooterFireMode.Auto;

        [Header("Ammo (ignored for melee)")]
        public int magazineSize = 30;
        public int startReserveAmmo = 90;
        public float reloadSeconds = 2.2f;

        [Header("Melee")]
        public float meleeDelay = 0.45f;
        public float meleeRadius = 0.35f;
        public Vector2 meleeCameraPunch = new Vector2(-2.2f, 0.6f);

        [Header("Placeholders (replace later)")]
        public AudioClip fireClip;
        public AudioClip emptyClip;
        public AudioClip reloadClip;
        public GameObject impactPrefab;
    }
}
