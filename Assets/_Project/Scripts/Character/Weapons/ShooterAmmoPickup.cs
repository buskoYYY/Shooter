using UnityEngine;

namespace Shooter.Project.Weapons
{
    /// <summary>
    /// World ammo pickup (trigger). Adds reserve to a matching <see cref="RangedWeapon"/> via WeaponManager.
    /// Does not use Interact — safe alongside ladder.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class ShooterAmmoPickup : MonoBehaviour
    {
        [SerializeField] AmmoType ammoType = AmmoType.Rifle;
        [SerializeField] int amount = 30;
        [SerializeField] bool destroyOnPickup = true;
        [SerializeField] AudioClip pickupSfx;
        [SerializeField] float respawnSeconds;

        float _respawnAt = -1f;
        Collider _collider;
        Renderer[] _renderers;
        bool _picked;

        public AmmoType AmmoType => ammoType;
        public int Amount => amount;

        public void Configure(AmmoType type, int ammoAmount)
        {
            ammoType = type;
            amount = Mathf.Max(1, ammoAmount);
        }

        void Awake()
        {
            _collider = GetComponent<Collider>();
            if (_collider != null)
                _collider.isTrigger = true;

            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        void Update()
        {
            if (!_picked || respawnSeconds <= 0f)
                return;
            if (Time.time < _respawnAt)
                return;

            SetVisible(true);
            _picked = false;
        }

        void OnTriggerEnter(Collider other)
        {
            if (_picked)
                return;

            var manager = other.GetComponentInParent<WeaponManager>();
            if (manager == null)
                return;

            if (!manager.TryAddAmmo(ammoType, amount))
                return;

            if (pickupSfx != null)
                AudioSource.PlayClipAtPoint(pickupSfx, transform.position);

            manager.NotifyAmmoPickedUp(ammoType, amount);

            if (destroyOnPickup && respawnSeconds <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            _picked = true;
            SetVisible(false);
            if (respawnSeconds > 0f)
                _respawnAt = Time.time + respawnSeconds;
            else
                Destroy(gameObject);
        }

        void SetVisible(bool visible)
        {
            if (_collider != null)
                _collider.enabled = visible;

            if (_renderers == null)
                return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                    _renderers[i].enabled = visible;
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            amount = Mathf.Max(1, amount);
            respawnSeconds = Mathf.Max(0f, respawnSeconds);
            var col = GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = ammoType == AmmoType.Pistol
                ? new Color(0.9f, 0.75f, 0.2f, 0.7f)
                : new Color(0.2f, 0.85f, 0.35f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, 0.35f);
        }
#endif
    }
}
