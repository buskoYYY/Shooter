using System.Collections;
using KINEMATION.FPSAnimationFramework.Runtime.Camera;
using KINEMATION.FPSAnimationFramework.Runtime.Core;
using KINEMATION.FPSAnimationFramework.Runtime.Layers.IkMotionLayer;
using KINEMATION.FPSAnimationFramework.Runtime.Playables;
using KINEMATION.FPSAnimationFramework.Runtime.Recoil;
using KINEMATION.ProceduralRecoilAnimationSystem.Runtime;
using Shooter.Project.Character;
using UnityEngine;

namespace Shooter.Project.Weapons
{
    /// <summary>
    /// Hitscan ranged weapon. Uses FPS AF playables / recoil like demo Weapon.cs, without Demo.FPSController.
    /// </summary>
    public class RangedWeapon : WeaponBase
    {
        [Header("Ammo")]
        [SerializeField] AmmoType ammoType = AmmoType.Rifle;
        [SerializeField] int magazineSize = 30;
        [SerializeField] int startReserve = 90;
        [SerializeField] float reloadSeconds = 2.2f;

        [Header("Fire")]
        [SerializeField] float fireRateRpm = 750f;
        [SerializeField] bool supportsAuto = true;
        [SerializeField] float damage = 25f;
        [SerializeField] float range = 150f;
        [SerializeField] LayerMask hitMask = ~0;
        [SerializeField] float wearPerShot = 0.15f;

        [Header("FPS AF / recoil")]
        [SerializeField] FPSAnimationAsset reloadClip;
        [SerializeField] FPSAnimationAsset fireClip;
        [SerializeField] IkMotionLayerSettings equipMotion;
        [SerializeField] IkMotionLayerSettings unEquipMotion;
        [SerializeField] RecoilAnimData recoilData;
        [SerializeField] RecoilPatternSettings recoilPatternSettings;
        [SerializeField] FPSCameraShake cameraShake;

        [Header("VFX / SFX")]
        [Tooltip("AimPoint — shell eject origin (and aim reference).")]
        [SerializeField] Transform muzzlePoint;
        [Tooltip("Muzzle flash spawn point. Assign an empty at the barrel tip.")]
        [SerializeField] Transform muzzleFlashPoint;
        [Tooltip("Optional override for shell eject. If empty, uses Muzzle Point (AimPoint).")]
        [SerializeField] Transform shellEjectPoint;
        [SerializeField] Vector3 shellEjectLocalOffset = new Vector3(0.06f, 0.03f, -0.02f);
        [SerializeField] float shellEjectForce = 2.4f;
        [Tooltip("Assign your muzzle flash prefab here.")]
        [SerializeField] GameObject muzzleFlashPrefab;
        [SerializeField] GameObject shellEjectPrefab;
        [SerializeField] AudioClip fireSfx;
        [SerializeField] AudioClip emptySfx;
        [SerializeField] AudioClip reloadSfx;

        GameObject _owner;
        IPlayablesController _playables;
        FPSAnimator _fpsAnimator;
        FPSCameraController _camera;
        RecoilAnimation _recoilAnimation;
        RecoilPattern _recoilPattern;
        ShooterFpsCameraApply _cameraApply;
        Animator _weaponAnimator;

        int _magazine;
        int _reserve;
        float _nextShotTime;
        bool _firing;
        bool _reloading;
        Coroutine _reloadRoutine;

        static readonly int FireHash = Animator.StringToHash("Fire");
        static readonly int ReloadHash = Animator.StringToHash("Reload");

        public AmmoType AmmoType => ammoType;
        public int Magazine => _magazine;
        public int Reserve => _reserve;
        public int MagazineSize => magazineSize;
        public bool SupportsAuto => supportsAuto;
        public bool IsReloading => _reloading;
        public bool IsBusy => _reloading;

        public void BindOwner(GameObject owner)
        {
            _owner = owner;
            if (owner == null)
                return;

            _playables = owner.GetComponentInChildren<IPlayablesController>(true);
            _fpsAnimator = owner.GetComponentInChildren<FPSAnimator>(true);
            _camera = owner.GetComponentInChildren<FPSCameraController>(true);
            _recoilAnimation = owner.GetComponentInChildren<RecoilAnimation>(true);
            _recoilPattern = owner.GetComponentInChildren<RecoilPattern>(true);
            _cameraApply = owner.GetComponent<ShooterFpsCameraApply>();
            _weaponAnimator = GetComponentInChildren<Animator>(true);

            if (muzzlePoint == null)
                muzzlePoint = transform.Find("AimPoint") ?? transform;
        }

        void Awake()
        {
            _magazine = Mathf.Clamp(magazineSize, 0, magazineSize);
            _reserve = Mathf.Max(0, startReserve);
        }

        public override void Equip()
        {
            base.Equip();
            _reloading = false;
            _firing = false;

            if (_recoilAnimation != null && recoilData != null)
            {
                FireMode mode = supportsAuto ? FireMode.Auto : FireMode.Semi;
                _recoilAnimation.Init(recoilData, fireRateRpm, mode);
                _recoilAnimation.fireMode = mode;
            }

            if (_recoilPattern != null && recoilPatternSettings != null)
                _recoilPattern.Init(recoilPatternSettings);

            PlayEquipMotion();
        }

        public override void Unequip()
        {
            PlayUnequipMotion();
            StopFire();
            CancelReload();
            base.Unequip();
        }

        public override void Attack()
        {
            TryFire();
        }

        public bool TryFire()
        {
            if (IsBroken || _reloading || _owner == null)
                return false;

            if (!supportsAuto && _firing)
                return false;

            if (Time.time < _nextShotTime)
                return false;

            float interval = 60f / Mathf.Max(1f, fireRateRpm);
            _nextShotTime = Time.time + interval;
            _firing = true;

            if (_magazine <= 0)
            {
                PlaySfx(emptySfx);
                StopFire();
                return false;
            }

            _magazine--;
            ApplyWear(wearPerShot);
            PlayShotFeedback();
            Hitscan();
            return true;
        }

        public void StopFire()
        {
            _firing = false;
            _recoilAnimation?.Stop();
            _recoilPattern?.OnFireEnd();
        }

        public override void Reload()
        {
            if (IsBroken || _reloading)
                return;
            if (_magazine >= magazineSize || _reserve <= 0)
                return;

            StopFire();
            CancelReload();
            _reloadRoutine = StartCoroutine(ReloadRoutine());
        }

        public override void CheckAmmo()
        {
            // Phase 2.6 — animation + UI. Debug for now.
            Debug.Log($"[{WeaponId}] mag {_magazine}/{magazineSize}, reserve {_reserve} ({ammoType})", this);
        }

        public bool TryAddAmmo(AmmoType type, int amount)
        {
            if (type != ammoType || amount <= 0)
                return false;
            _reserve += amount;
            return true;
        }

        public override void OnBreak()
        {
            StopFire();
            CancelReload();
            PlaySfx(emptySfx);
            base.OnBreak();
        }

        IEnumerator ReloadRoutine()
        {
            _reloading = true;
            PlaySfx(reloadSfx);

            if (FPSAnimationAsset.IsValid(reloadClip) && _playables != null)
                _playables.PlayAnimation(reloadClip, 0f);

            if (_weaponAnimator != null)
            {
                _weaponAnimator.Rebind();
                _weaponAnimator.Play(ReloadHash, 0, 0f);
            }

            float wait = reloadSeconds;
            if (FPSAnimationAsset.IsValid(reloadClip) && reloadClip.clip != null)
                wait = Mathf.Max(0.1f, reloadClip.clip.length * 0.85f);

            yield return new WaitForSeconds(wait);

            int need = magazineSize - _magazine;
            int take = Mathf.Min(need, _reserve);
            _magazine += take;
            _reserve -= take;
            _reloading = false;
            _reloadRoutine = null;
        }

        void CancelReload()
        {
            if (_reloadRoutine != null)
            {
                StopCoroutine(_reloadRoutine);
                _reloadRoutine = null;
            }

            _reloading = false;
        }

        void PlayShotFeedback()
        {
            PlaySfx(fireSfx);

            if (_weaponAnimator != null)
                _weaponAnimator.Play(FireHash, 0, 0f);

            if (FPSAnimationAsset.IsValid(fireClip) && _playables != null)
                _playables.PlayAnimation(fireClip, 0f);

            if (_camera != null && cameraShake != null)
                _camera.PlayCameraShake(cameraShake);
            else
                _cameraApply?.AddWeaponCameraPunch(new Vector2(-0.35f, Random.Range(-0.2f, 0.2f)));

            if (_recoilAnimation != null && recoilData != null)
                _recoilAnimation.Play();

            _recoilPattern?.OnFireStart();
            SpawnMuzzleFlash();
            SpawnShell();
        }

        void Hitscan()
        {
            if (!TryGetAimRay(out Ray ray))
                return;

            RaycastHit[] hits = Physics.RaycastAll(ray, range, hitMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            RaycastHit hit = default;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                if (_owner != null && hits[i].transform.IsChildOf(_owner.transform))
                    continue;
                if (hits[i].distance >= best)
                    continue;
                best = hits[i].distance;
                hit = hits[i];
                found = true;
            }

            if (!found)
            {
                Debug.DrawRay(ray.origin, ray.direction * range, Color.gray, 0.05f);
                return;
            }

            Debug.DrawLine(ray.origin, hit.point, Color.yellow, 0.05f);
            SpawnImpactMarker(hit);

            var damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable == null)
                return;

            damageable.ApplyDamage(new ShooterDamageInfo
            {
                amount = damage,
                point = hit.point,
                normal = hit.normal,
                instigator = _owner,
                weaponKind = ammoType == AmmoType.Pistol ? ShooterWeaponKind.Pistol : ShooterWeaponKind.Rifle
            });
        }

        bool TryGetAimRay(out Ray ray)
        {
            ray = default;
            Camera cam = Camera.main;
            if (cam == null)
                return false;
            ray = new Ray(cam.transform.position + cam.transform.forward * 0.15f, cam.transform.forward);
            return true;
        }

        void SpawnMuzzleFlash()
        {
            if (muzzleFlashPrefab == null || muzzleFlashPoint == null)
                return;

            GameObject fx = Instantiate(
                muzzleFlashPrefab,
                muzzleFlashPoint.position,
                muzzleFlashPoint.rotation,
                muzzleFlashPoint);

            float life = 0.5f;
            var systems = fx.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                var main = systems[i].main;
                float candidate = main.duration + main.startLifetime.constantMax;
                life = Mathf.Max(life, candidate);
            }

            Destroy(fx, Mathf.Clamp(life, 0.05f, 3f));
        }

        void SpawnShell()
        {
            if (shellEjectPrefab == null)
                return;

            // Shells use AimPoint (muzzlePoint) / optional shellEjectPoint — not the flash tip.
            Transform basis = transform;
            Transform eject = shellEjectPoint != null ? shellEjectPoint : muzzlePoint;
            Vector3 origin;
            if (eject != null)
            {
                origin = eject.position
                    + basis.right * shellEjectLocalOffset.x
                    + basis.up * shellEjectLocalOffset.y
                    + basis.forward * shellEjectLocalOffset.z;
            }
            else
            {
                origin = basis.TransformPoint(shellEjectLocalOffset);
            }

            Vector3 ejectRight = basis.right;
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 camRight = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up);
                if (camRight.sqrMagnitude > 0.001f)
                    ejectRight = camRight.normalized;
            }

            GameObject shell = Instantiate(shellEjectPrefab, origin + ejectRight * 0.02f, Random.rotation);
            IgnoreOwnerCollisions(shell);

            var rb = shell.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 force = ejectRight * shellEjectForce
                    + Vector3.up * (shellEjectForce * 0.55f)
                    + basis.forward * Random.Range(-0.25f, 0.25f);
                rb.AddForce(force, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * 4f, ForceMode.Impulse);
            }

            Destroy(shell, 2.5f);
        }

        void IgnoreOwnerCollisions(GameObject shell)
        {
            if (_owner == null || shell == null)
                return;

            Collider[] shellCols = shell.GetComponentsInChildren<Collider>(true);
            Collider[] ownerCols = _owner.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < shellCols.Length; i++)
            {
                if (shellCols[i] == null)
                    continue;
                for (int j = 0; j < ownerCols.Length; j++)
                {
                    if (ownerCols[j] == null)
                        continue;
                    Physics.IgnoreCollision(shellCols[i], ownerCols[j], true);
                }
            }
        }

        static void SpawnImpactMarker(RaycastHit hit)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "BulletImpact";
            Object.Destroy(marker.GetComponent<Collider>());
            marker.transform.position = hit.point + hit.normal * 0.02f;
            marker.transform.localScale = Vector3.one * 0.06f;
            Object.Destroy(marker, 0.3f);
        }

        void PlayEquipMotion()
        {
            if (_fpsAnimator != null && equipMotion != null)
                _fpsAnimator.LinkAnimatorLayer(equipMotion);
        }

        void PlayUnequipMotion()
        {
            if (_fpsAnimator != null && unEquipMotion != null)
                _fpsAnimator.LinkAnimatorLayer(unEquipMotion);
        }

        void PlaySfx(AudioClip clip)
        {
            if (clip == null)
                return;
            Vector3 pos = muzzlePoint != null ? muzzlePoint.position : transform.position;
            AudioSource.PlayClipAtPoint(clip, pos, 0.85f);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            magazineSize = Mathf.Max(1, magazineSize);
            startReserve = Mathf.Max(0, startReserve);
            fireRateRpm = Mathf.Max(1f, fireRateRpm);
            range = Mathf.Max(1f, range);
            reloadSeconds = Mathf.Max(0.1f, reloadSeconds);
        }
#endif
    }
}
