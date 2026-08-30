using KINEMATION.FPSAnimationFramework.Runtime.Core;
using Shooter.Project.Character;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Project.Weapons
{
    /// <summary>
    /// Player combat: fire, reload, switch, pickup, drop. Visuals parent to WeaponBone.
    /// Does not call Demo.FPSController / LinkAnimatorProfile — that comes in phase 5.1.
    /// </summary>
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    public class ShooterWeaponController : MonoBehaviour
    {
        [SerializeField] InputActionAsset inputActions;
        [SerializeField] ShooterWeaponDefinition[] startingWeapons;
        [SerializeField] LayerMask hitMask = ~0;
        [SerializeField] bool showAmmoHud = true;

        ShooterWeaponActionGate _gate;
        ShooterWeaponInventory _inventory;
        ShooterHandPoseState _handPose;
        ShooterFpsCameraApply _cameraApply;
        Transform _weaponBone;
        Transform _viewInstance;

        InputActionMap _playerMap;
        InputAction _attack;
        InputAction _reload;
        InputAction _drop;
        InputAction _next;
        InputAction _previous;
        InputAction _weapon1;
        InputAction _weapon2;
        InputAction _weapon3;

        float _nextShotTime;
        float _nextMeleeTime;
        bool _wasAttacking;

        void Awake()
        {
            _gate = GetComponent<ShooterWeaponActionGate>() ?? gameObject.AddComponent<ShooterWeaponActionGate>();
            _inventory = GetComponent<ShooterWeaponInventory>() ?? gameObject.AddComponent<ShooterWeaponInventory>();
            _handPose = GetComponent<ShooterHandPoseState>();
            _cameraApply = GetComponent<ShooterFpsCameraApply>();
            ResolveWeaponBone();
            BindInput();
        }

        void OnEnable()
        {
            _playerMap?.Enable();
            _inventory.EquippedChanged += OnEquippedChanged;
        }

        void OnDisable()
        {
            if (_inventory != null)
                _inventory.EquippedChanged -= OnEquippedChanged;
            StopAttack();
        }

        void Start()
        {
            if (startingWeapons == null)
                return;

            for (int i = 0; i < startingWeapons.Length; i++)
            {
                if (startingWeapons[i] != null)
                    _inventory.TryAdd(startingWeapons[i], out _);
            }
        }

        void Update()
        {
            PollButtons();
            TickAttack();
            SyncViewVisibility();
        }

        void BindInput()
        {
            if (inputActions == null)
                return;

            _playerMap = inputActions.FindActionMap("Player", false);
            if (_playerMap == null)
                return;

            _attack = _playerMap.FindAction("Attack", false);
            _reload = _playerMap.FindAction("Reload", false);
            _drop = _playerMap.FindAction("DropWeapon", false);
            _next = _playerMap.FindAction("Next", false);
            _previous = _playerMap.FindAction("Previous", false);
            _weapon1 = _playerMap.FindAction("Weapon1", false);
            _weapon2 = _playerMap.FindAction("Weapon2", false);
            _weapon3 = _playerMap.FindAction("Weapon3", false);
        }

        void PollButtons()
        {
            if (WasPressed(_reload))
                TryReload();
            if (WasPressed(_drop))
                TryDrop();
            if (WasPressed(_next))
                TryCycle(1);
            if (WasPressed(_previous))
                TryCycle(-1);
            if (WasPressed(_weapon1))
                TrySelect(0);
            if (WasPressed(_weapon2))
                TrySelect(1);
            if (WasPressed(_weapon3))
                TrySelect(2);
        }

        static bool WasPressed(InputAction action)
        {
            return action != null && action.WasPressedThisFrame();
        }

        void TickAttack()
        {
            bool held = _attack != null && _attack.IsPressed();
            if (!held)
            {
                _wasAttacking = false;
                return;
            }

            if (!_gate.CanFire(out _))
            {
                _wasAttacking = false;
                return;
            }

            var loadout = _inventory.Active;
            if (loadout?.definition == null)
                return;

            if (loadout.IsMelee)
            {
                TryMelee(loadout);
                return;
            }

            bool auto = loadout.definition.fireMode == ShooterFireMode.Auto;
            if (!auto && _wasAttacking)
                return;

            TryFire(loadout);
            _wasAttacking = true;
        }

        void StopAttack()
        {
            _wasAttacking = false;
        }

        public bool TryPickupWeapon(ShooterWeaponDefinition definition)
        {
            if (definition == null)
                return false;
            if (!_inventory.TryAdd(definition, out _))
                return false;

            if (_handPose != null && _handPose.IsUnarmed && _gate.CanDraw(out _))
                _handPose.SetHandPose(false);

            return true;
        }

        public bool TryPickupAmmo(ShooterWeaponDefinition matching, int amount)
        {
            if (matching != null)
                return _inventory.TryAddReserveMatching(matching, amount);
            return _inventory.TryAddReserveToActive(amount);
        }

        void TryFire(ShooterWeaponLoadout loadout)
        {
            if (Time.time < _nextShotTime)
                return;

            float interval = 60f / Mathf.Max(1f, loadout.definition.fireRateRpm);
            _nextShotTime = Time.time + interval;

            if (!loadout.HasAmmoInMag)
            {
                PlayClip(loadout.definition.emptyClip);
                return;
            }

            loadout.TryConsumeShot();
            PlayClip(loadout.definition.fireClip);
            Hitscan(loadout.definition);
        }

        void TryMelee(ShooterWeaponLoadout loadout)
        {
            if (Time.time < _nextMeleeTime)
                return;

            _nextMeleeTime = Time.time + Mathf.Max(0.05f, loadout.definition.meleeDelay);
            PlayClip(loadout.definition.fireClip);
            _cameraApply?.AddWeaponCameraPunch(loadout.definition.meleeCameraPunch);
            MeleeSweep(loadout.definition);
        }

        void Hitscan(ShooterWeaponDefinition def)
        {
            if (!TryGetAimRay(out Ray ray))
                return;

            if (!RaycastIgnoringSelf(ray, def.range, out RaycastHit hit))
            {
                Debug.DrawRay(ray.origin, ray.direction * def.range, Color.gray, 0.08f);
                return;
            }

            Debug.DrawLine(ray.origin, hit.point, Color.yellow, 0.08f);
            ApplyHit(def, hit);
        }

        void MeleeSweep(ShooterWeaponDefinition def)
        {
            if (!TryGetAimRay(out Ray ray))
                return;

            float range = Mathf.Max(0.4f, def.range < 5f ? def.range : 1.8f);
            RaycastHit[] hits = Physics.SphereCastAll(
                ray.origin, def.meleeRadius, ray.direction, range, hitMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            RaycastHit hit = default;
            bool found = false;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].transform.IsChildOf(transform))
                    continue;
                if (hits[i].distance >= best)
                    continue;
                best = hits[i].distance;
                hit = hits[i];
                found = true;
            }

            if (!found)
                return;

            ApplyHit(def, hit);
        }

        void ApplyHit(ShooterWeaponDefinition def, RaycastHit hit)
        {
            SpawnImpact(def, hit);

            var damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable == null)
                return;

            damageable.ApplyDamage(new ShooterDamageInfo
            {
                amount = def.damage,
                point = hit.point,
                normal = hit.normal,
                instigator = gameObject,
                weaponKind = def.kind
            });
        }

        bool RaycastIgnoringSelf(Ray ray, float range, out RaycastHit hit)
        {
            hit = default;
            RaycastHit[] hits = Physics.RaycastAll(ray, range, hitMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].transform.IsChildOf(transform))
                    continue;
                if (hits[i].distance >= best)
                    continue;
                best = hits[i].distance;
                hit = hits[i];
                found = true;
            }

            return found;
        }

        bool TryGetAimRay(out Ray ray)
        {
            ray = default;
            Camera cam = Camera.main;
            if (cam == null)
                return false;
            ray = new Ray(cam.transform.position + cam.transform.forward * 0.12f, cam.transform.forward);
            return true;
        }

        void TryReload()
        {
            if (!_gate.CanReload(out _))
                return;

            var loadout = _inventory.Active;
            if (loadout == null || loadout.IsMelee)
                return;
            if (loadout.magazine >= loadout.definition.magazineSize || loadout.reserve <= 0)
                return;

            loadout.TryReload();
            PlayClip(loadout.definition.reloadClip);
            _gate.SetBusyFor(loadout.definition.reloadSeconds);
        }

        void TrySelect(int index)
        {
            if (!_gate.CanDraw(out _))
                return;
            if (_inventory.SetActive(index))
                _gate.SetBusyFor(0.2f);
        }

        void TryCycle(int direction)
        {
            if (!_gate.CanDraw(out _))
                return;
            if (_inventory.Cycle(direction))
                _gate.SetBusyFor(0.2f);
        }

        void TryDrop()
        {
            if (!_gate.CanDraw(out _))
                return;
            if (!_inventory.TryDropActive(out ShooterWeaponLoadout dropped))
                return;

            SpawnDroppedPickup(dropped);
            if (!_inventory.HasEquipped && _handPose != null && !_handPose.IsUnarmed)
                _handPose.SetHandPose(true);
        }

        void OnEquippedChanged(ShooterWeaponLoadout loadout)
        {
            RebuildView(loadout?.definition);
        }

        void RebuildView(ShooterWeaponDefinition definition)
        {
            if (_viewInstance != null)
            {
                Destroy(_viewInstance.gameObject);
                _viewInstance = null;
            }

            if (definition == null || definition.viewPrefab == null)
                return;

            ResolveWeaponBone();
            Transform parent = _weaponBone != null ? _weaponBone : transform;
            GameObject instance = Instantiate(definition.viewPrefab, parent);
            instance.name = definition.displayName + " (View)";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            DisablePackGameplayScripts(instance);
            _viewInstance = instance.transform;
            SyncViewVisibility();
        }

        void SyncViewVisibility()
        {
            if (_viewInstance == null)
                return;

            bool show = _inventory.HasEquipped && (_handPose == null || !_handPose.IsUnarmed);
            if (_viewInstance.gameObject.activeSelf != show)
                _viewInstance.gameObject.SetActive(show);
        }

        void ResolveWeaponBone()
        {
            if (_weaponBone != null)
                return;

            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == FPSANames.WeaponBone)
                {
                    _weaponBone = all[i];
                    return;
                }
            }
        }

        static void DisablePackGameplayScripts(GameObject root)
        {
            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                    continue;
                string ns = behaviour.GetType().Namespace ?? string.Empty;
                if (ns.StartsWith("Demo.Scripts") || ns.StartsWith("KINEMATION.FPSAnimationPack"))
                    behaviour.enabled = false;
            }
        }

        void SpawnDroppedPickup(ShooterWeaponLoadout dropped)
        {
            Vector3 origin = transform.position + transform.forward * 0.7f + Vector3.up * 0.9f;
            GameObject go;
            if (dropped.definition.viewPrefab != null)
            {
                go = Instantiate(dropped.definition.viewPrefab, origin, transform.rotation);
                DisablePackGameplayScripts(go);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.position = origin;
                go.transform.localScale = Vector3.one * 0.25f;
            }

            go.name = "Pickup_" + dropped.definition.displayName;
            StripColliders(go);
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 2f;
            rb.AddForce(transform.forward * 2.5f + Vector3.up * 1.5f, ForceMode.Impulse);

            go.AddComponent<ShooterWorldPickup>().ConfigureWeapon(dropped.definition);
        }

        static void StripColliders(GameObject root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                Destroy(colliders[i]);
        }

        void SpawnImpact(ShooterWeaponDefinition def, RaycastHit hit)
        {
            if (def.impactPrefab != null)
            {
                Destroy(Instantiate(def.impactPrefab, hit.point, Quaternion.LookRotation(hit.normal)), 2f);
                return;
            }

            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "Impact";
            Destroy(marker.GetComponent<Collider>());
            marker.transform.position = hit.point + hit.normal * 0.02f;
            marker.transform.localScale = Vector3.one * 0.07f;
            Destroy(marker, 0.35f);
        }

        void PlayClip(AudioClip clip)
        {
            if (clip == null)
                return;
            Vector3 pos = Camera.main != null ? Camera.main.transform.position : transform.position;
            AudioSource.PlayClipAtPoint(clip, pos, 0.8f);
        }

        void OnGUI()
        {
            if (!showAmmoHud)
                return;

            var loadout = _inventory != null ? _inventory.Active : null;
            string line;
            if (loadout?.definition == null)
                line = "Weapon: none  (pick up / T to draw)";
            else if (loadout.IsMelee)
                line = loadout.definition.displayName + "  (melee)";
            else
                line = $"{loadout.definition.displayName}  {loadout.magazine}/{loadout.reserve}";

            if (_handPose != null && _handPose.IsUnarmed)
                line += "  [holstered]";

            GUI.Label(new Rect(16f, 16f, 420f, 24f), line);
        }
    }
}
