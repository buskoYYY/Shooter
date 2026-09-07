using System.Collections;
using KINEMATION.FPSAnimationFramework.Runtime.Core;
using KINEMATION.FPSAnimationFramework.Runtime.Layers.IkMotionLayer;
using KINEMATION.FPSAnimationFramework.Runtime.Playables;
using Shooter.Project.Character;
using UnityEngine;

namespace Shooter.Project.Weapons
{
    /// <summary>
    /// Melee knife: playables attack clip + sphere-cast hit. No Demo.FPSController.
    /// </summary>
    public class MeleeWeapon : WeaponBase
    {
        [Header("Melee")]
        [SerializeField] float wearPerAttack = 1f;
        [SerializeField] float damage = 35f;
        [SerializeField] float range = 1.8f;
        [SerializeField] float radius = 0.35f;
        [SerializeField] float attackCooldown = 0.55f;
        [SerializeField] float hitDelay = 0.18f;
        [SerializeField] LayerMask hitMask = ~0;
        [SerializeField] Vector2 cameraPunch = new Vector2(1.2f, 0.15f);

        [Header("FPS AF")]
        [SerializeField] FPSAnimationAsset attackClip;
        [SerializeField] IkMotionLayerSettings equipMotion;
        [SerializeField] IkMotionLayerSettings unEquipMotion;

        GameObject _owner;
        IPlayablesController _playables;
        FPSAnimator _fpsAnimator;
        ShooterFpsCameraApply _cameraApply;

        float _nextAttackTime;
        bool _attacking;
        Coroutine _attackRoutine;

        public bool IsBusy => _attacking;

        public void BindOwner(GameObject owner)
        {
            _owner = owner;
            if (owner == null)
                return;

            _playables = owner.GetComponentInChildren<IPlayablesController>(true);
            _fpsAnimator = owner.GetComponentInChildren<FPSAnimator>(true);
            _cameraApply = owner.GetComponent<ShooterFpsCameraApply>();
        }

        public override void Equip()
        {
            base.Equip();
            if (_fpsAnimator != null && equipMotion != null)
                _fpsAnimator.LinkAnimatorLayer(equipMotion);
        }

        public override void Unequip()
        {
            if (_attackRoutine != null)
            {
                StopCoroutine(_attackRoutine);
                _attackRoutine = null;
            }

            _attacking = false;

            if (_fpsAnimator != null && unEquipMotion != null)
                _fpsAnimator.LinkAnimatorLayer(unEquipMotion);

            base.Unequip();
        }

        public override void Attack()
        {
            if (IsBroken || _owner == null || _attacking)
                return;

            if (Time.time < _nextAttackTime)
                return;

            _nextAttackTime = Time.time + Mathf.Max(0.05f, attackCooldown);
            _attackRoutine = StartCoroutine(AttackRoutine());
        }

        public override void Reload() { }

        public override void CheckAmmo() { }

        IEnumerator AttackRoutine()
        {
            _attacking = true;

            if (FPSAnimationAsset.IsValid(attackClip) && _playables != null)
                _playables.PlayAnimation(attackClip, 0f);

            _cameraApply?.AddWeaponCameraPunch(cameraPunch);

            float delay = Mathf.Clamp(hitDelay, 0f, Mathf.Max(0.05f, attackCooldown));
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            TryHit();
            ApplyWear(wearPerAttack);

            float remain = Mathf.Max(0f, attackCooldown - delay);
            if (remain > 0f)
                yield return new WaitForSeconds(remain);

            _attacking = false;
            _attackRoutine = null;
        }

        void TryHit()
        {
            if (_owner == null)
                return;

            Camera cam = Camera.main;
            Transform origin = cam != null ? cam.transform : _owner.transform;
            Ray ray = new Ray(origin.position, origin.forward);

            float castRange = Mathf.Max(0.4f, range);
            RaycastHit[] hits = Physics.SphereCastAll(
                ray.origin, Mathf.Max(0.05f, radius), ray.direction, castRange, hitMask,
                QueryTriggerInteraction.Ignore);

            float best = float.MaxValue;
            RaycastHit bestHit = default;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.transform != null && hit.transform.IsChildOf(_owner.transform))
                    continue;
                if (hit.distance >= best)
                    continue;

                best = hit.distance;
                bestHit = hit;
                found = true;
            }

            if (!found)
                return;

            Debug.DrawLine(ray.origin, bestHit.point, Color.cyan, 0.2f);

            var damageable = bestHit.collider.GetComponentInParent<IDamageable>();
            if (damageable == null)
                return;

            damageable.ApplyDamage(new ShooterDamageInfo
            {
                amount = damage,
                point = bestHit.point,
                normal = bestHit.normal,
                instigator = _owner,
                weaponKind = ShooterWeaponKind.Melee
            });
        }
    }
}
