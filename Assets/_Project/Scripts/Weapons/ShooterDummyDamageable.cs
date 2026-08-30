using UnityEngine;

namespace Shooter.Project.Weapons
{
    [RequireComponent(typeof(Collider))]
    public class ShooterDummyDamageable : MonoBehaviour, IDamageable
    {
        [SerializeField] float maxHealth = 100f;
        [SerializeField] bool destroyOnDeath = false;

        float _health;
        Renderer _renderer;
        Color _baseColor = Color.white;

        public float Health => _health;

        void Awake()
        {
            _health = maxHealth;
            _renderer = GetComponent<Renderer>();
            if (_renderer != null)
                _baseColor = _renderer.material.color;
        }

        public void ApplyDamage(in ShooterDamageInfo damage)
        {
            _health -= damage.amount;
            if (_renderer != null)
            {
                float t = Mathf.Clamp01(_health / Mathf.Max(0.01f, maxHealth));
                _renderer.material.color = Color.Lerp(Color.red, _baseColor, t);
            }

            if (_health <= 0f && destroyOnDeath)
                Destroy(gameObject);
        }
    }
}
