using System;
using Shooter.Project.Character;
using KINEMATION.FPSAnimationFramework.Runtime.Core;
using KINEMATION.Shared.KAnimationCore.Runtime.Rig;
using Lightbug.CharacterControllerPro.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Shooter.Project.Weapons
{
    [DefaultExecutionOrder(-140)]
    [DisallowMultipleComponent]
    public class WeaponManager : MonoBehaviour
    {
        const int MaxWeaponSlots = 5;

        [SerializeField] InputActionAsset inputActions;
        [Tooltip("Optional. Used to auto-find weapons placed under IK WeaponBone.")]
        [SerializeField] Transform weaponAttachPoint;
        [FormerlySerializedAs("weaponPrefabs")]
        [SerializeField] WeaponBase[] weaponSlots = new WeaponBase[MaxWeaponSlots];
        [SerializeField] bool autoCollectFromAttachPoint = true;
        [SerializeField] bool showAmmoHud = true;

        ShooterPlayerInventory _inventory;
        ShooterHandPoseState _handPoseState;
        ShooterLadderFpsBridge _ladderBridge;
        CharacterActor _characterActor;
        InputActionMap _playerMap;
        InputAction[] _slotActions = new InputAction[6];

        WeaponBase _activeWeapon;
        int _activeSlotIndex = -1;
        int _slotBeforeLadder = -1;
        bool _ladderHolsterActive;

        InputAction _attackAction;
        InputAction _sprintAction;
        InputAction _reloadAction;

        readonly WeaponBase[] _runtimeSlots = new WeaponBase[MaxWeaponSlots];

        public bool IsHolstered => _activeSlotIndex < 0;
        public int ActiveSlotIndex => _activeSlotIndex;
        public WeaponBase ActiveWeapon => _activeWeapon;
        public Transform WeaponAttachPoint => weaponAttachPoint;

        public event Action<int> ActiveSlotChanged;
        public event Action Holstered;

        void Awake()
        {
            _inventory = GetComponent<ShooterPlayerInventory>();
            if (_inventory == null)
                _inventory = gameObject.AddComponent<ShooterPlayerInventory>();

            _handPoseState = GetComponent<ShooterHandPoseState>();
            _ladderBridge = GetComponent<ShooterLadderFpsBridge>();
            _characterActor = GetComponent<CharacterActor>();

            var legacy = GetComponent<ShooterWeaponController>();
            if (legacy != null)
                legacy.enabled = false;

            ResolveInputAsset();
            BindSlotActions();
            ResolveWeaponAttachPoint();
            ResolveWeaponSlots();
            InitializeWeaponSlots();
        }

        void OnEnable()
        {
            _playerMap?.Enable();
        }

        void OnDisable()
        {
            StopActiveFire();
            _playerMap?.Disable();
        }

        void Update()
        {
            PollSlotInput();
            PollReloadInput();
            PollAttackInput();
        }

        public WeaponBase GetWeaponInSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxWeaponSlots)
                return null;

            return _runtimeSlots[slotIndex];
        }

        void ResolveWeaponSlots()
        {
            for (int i = 0; i < MaxWeaponSlots; i++)
                _runtimeSlots[i] = null;

            CollectWeaponsFromHierarchy();

            for (int i = 0; i < MaxWeaponSlots; i++)
            {
                WeaponBase assigned = weaponSlots != null && i < weaponSlots.Length
                    ? weaponSlots[i]
                    : null;

                WeaponBase sceneWeapon = FindSceneWeaponForSlot(i);
                if (sceneWeapon != null)
                {
                    _runtimeSlots[i] = sceneWeapon;
                    continue;
                }

                if (assigned != null && IsSceneInstance(assigned))
                {
                    _runtimeSlots[i] = assigned;
                    continue;
                }

                if (assigned != null)
                    _runtimeSlots[i] = SpawnWeaponFromTemplate(assigned, i);
            }
        }

        WeaponBase FindSceneWeaponForSlot(int slotIndex)
        {
            if (weaponAttachPoint == null)
                return null;

            WeaponBase[] found = weaponAttachPoint.GetComponentsInChildren<WeaponBase>(true);
            for (int i = 0; i < found.Length; i++)
            {
                WeaponBase weapon = found[i];
                if (weapon.transform == weaponAttachPoint)
                    continue;

                if (weapon.SlotIndex == slotIndex)
                    return weapon;
            }

            return null;
        }

        WeaponBase SpawnWeaponFromTemplate(WeaponBase template, int slotIndex)
        {
            if (template == null || weaponAttachPoint == null)
                return null;

            WeaponBase existing = FindSceneWeaponForSlot(slotIndex);
            if (existing != null)
                return existing;

            WeaponBase instance = Instantiate(template, weaponAttachPoint);
            instance.name = template.name;
            instance.InitializeForSlot(slotIndex);
            return instance;
        }

        static bool IsSceneInstance(WeaponBase weapon) =>
            weapon != null && weapon.gameObject.scene.IsValid();

        void CollectWeaponsFromHierarchy()
        {
            if (!autoCollectFromAttachPoint || weaponAttachPoint == null)
                return;

            WeaponBase[] found = weaponAttachPoint.GetComponentsInChildren<WeaponBase>(true);
            for (int i = 0; i < found.Length; i++)
            {
                WeaponBase weapon = found[i];
                if (weapon.transform == weaponAttachPoint)
                    continue;

                int slot = weapon.SlotIndex;
                if (slot < 0 || slot >= MaxWeaponSlots)
                    continue;

                if (weaponSlots[slot] == null || !IsSceneInstance(weaponSlots[slot]))
                    weaponSlots[slot] = weapon;
            }
        }

        void InitializeWeaponSlots()
        {
            for (int i = 0; i < MaxWeaponSlots; i++)
            {
                WeaponBase weapon = GetWeaponInSlot(i);
                if (weapon == null)
                    continue;

                weapon.InitializeForSlot(i);
                if (weapon is RangedWeapon ranged)
                    ranged.BindOwner(gameObject);

                WeaponPrefabUtility.StripPhysicsComponents(weapon.gameObject);
                weapon.gameObject.SetActive(false);
            }
        }

        void PollSlotInput()
        {
            if (!CanPerform(WeaponAction.ChangeWeapon))
                return;

            if (_slotActions[0] != null && _slotActions[0].WasPressedThisFrame())
            {
                Holster();
                return;
            }

            for (int i = 1; i < _slotActions.Length; i++)
            {
                if (_slotActions[i] != null && _slotActions[i].WasPressedThisFrame())
                    TryEquipSlot(i - 1);
            }
        }

        void PollReloadInput()
        {
            if (_reloadAction == null || !_reloadAction.WasPressedThisFrame())
                return;
            if (_activeWeapon == null || !CanPerform(WeaponAction.Reload))
                return;

            _activeWeapon.Reload();
        }

        void PollAttackInput()
        {
            if (_attackAction == null)
                return;

            if (_attackAction.WasReleasedThisFrame())
            {
                StopActiveFire();
                return;
            }

            if (!_attackAction.IsPressed())
                return;

            if (_activeWeapon == null || !CanPerform(WeaponAction.Attack))
            {
                StopActiveFire();
                return;
            }

            if (_activeWeapon is RangedWeapon ranged)
            {
                if (ranged.SupportsAuto || _attackAction.WasPressedThisFrame())
                    ranged.TryFire();
                return;
            }

            if (_attackAction.WasPressedThisFrame())
                _activeWeapon.Attack();
        }

        void StopActiveFire()
        {
            if (_activeWeapon is RangedWeapon ranged)
                ranged.StopFire();
        }

        public bool CanPerform(WeaponAction action)
        {
            if (_activeWeapon is RangedWeapon busy && busy.IsBusy
                && action is not WeaponAction.Unequip)
                return false;

            if (_ladderHolsterActive || (_ladderBridge != null && _ladderBridge.IsLadderModeActive))
                return action is WeaponAction.Unequip;

            bool grounded = _characterActor == null || _characterActor.IsGrounded;
            bool sprinting = _sprintAction != null && _sprintAction.IsPressed() && grounded;

            return action switch
            {
                WeaponAction.Shoot or WeaponAction.Attack =>
                    grounded && !sprinting && !_ladderHolsterActive,
                WeaponAction.Reload =>
                    grounded && !_ladderHolsterActive,
                WeaponAction.ChangeWeapon or WeaponAction.Equip or WeaponAction.Unequip =>
                    grounded && !_ladderHolsterActive,
                _ => true
            };
        }

        public void Holster(bool force = false)
        {
            if (_activeWeapon == null)
            {
                _activeSlotIndex = -1;
                _handPoseState?.SetUnarmed();
                return;
            }

            if (!force && !CanPerform(WeaponAction.Unequip))
                return;

            StopActiveFire();
            _activeWeapon.Unequip();
            _activeWeapon = null;
            _activeSlotIndex = -1;
            _handPoseState?.SetUnarmed();
            Holstered?.Invoke();
        }

        public bool TryEquipSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxWeaponSlots)
                return false;

            if (!CanPerform(WeaponAction.Equip))
                return false;

            if (_inventory != null && !_inventory.HasWeapon(slotIndex))
                return false;

            WeaponBase weapon = GetWeaponInSlot(slotIndex);
            if (weapon != null && weapon.IsBroken)
                return false;

            if (_activeSlotIndex == slotIndex && _handPoseState != null && !_handPoseState.IsUnarmed)
                return true;

            StopActiveFire();
            if (_activeWeapon != null)
                _activeWeapon.Unequip();

            _activeWeapon = weapon;
            _activeSlotIndex = slotIndex;
            _handPoseState?.SetArmed();

            if (weapon != null)
                weapon.Equip();

            ActiveSlotChanged?.Invoke(slotIndex);
            return true;
        }

        public bool TryAddAmmo(AmmoType type, int amount)
        {
            for (int i = 0; i < MaxWeaponSlots; i++)
            {
                if (GetWeaponInSlot(i) is RangedWeapon ranged && ranged.TryAddAmmo(type, amount))
                    return true;
            }

            return false;
        }

        public void NotifyLadderEnter()
        {
            if (_ladderHolsterActive)
                return;

            _ladderHolsterActive = true;
            _slotBeforeLadder = _activeSlotIndex;
            Holster(force: true);
        }

        public void NotifyLadderExit()
        {
            if (!_ladderHolsterActive)
                return;

            _ladderHolsterActive = false;
            int restoreSlot = _slotBeforeLadder;
            _slotBeforeLadder = -1;

            if (restoreSlot >= 0)
                TryEquipSlot(restoreSlot);
        }

        void ResolveInputAsset()
        {
            if (inputActions != null)
                return;

            ShooterCharacterController bridge = GetComponent<ShooterCharacterController>();
            if (bridge == null)
                return;

            var field = typeof(ShooterCharacterController).GetField(
                "inputActions",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            inputActions = field?.GetValue(bridge) as InputActionAsset;
        }

        void BindSlotActions()
        {
            if (inputActions == null)
                return;

            _playerMap = inputActions.FindActionMap("Player", true);
            _slotActions[0] = _playerMap.FindAction("WeaponSlot1", false);
            _slotActions[1] = _playerMap.FindAction("WeaponSlot2", false);
            _slotActions[2] = _playerMap.FindAction("WeaponSlot3", false);
            _slotActions[3] = _playerMap.FindAction("WeaponSlot4", false);
            _slotActions[4] = _playerMap.FindAction("WeaponSlot5", false);
            _slotActions[5] = _playerMap.FindAction("WeaponSlot6", false);
            _attackAction = _playerMap.FindAction("Attack", false);
            _sprintAction = _playerMap.FindAction("Sprint", false);
            _reloadAction = _playerMap.FindAction("Reload", false);
        }

        void ResolveWeaponAttachPoint()
        {
            Transform ikBone = FindIkWeaponBone();
            if (ikBone != null)
            {
                weaponAttachPoint = ikBone;
                return;
            }

            if (weaponAttachPoint != null)
                return;

            if (_handPoseState == null)
                _handPoseState = GetComponent<ShooterHandPoseState>();

            Transform fpsRoot = _handPoseState != null ? _handPoseState.FpsCharacterRoot : null;
            if (fpsRoot != null)
            {
                weaponAttachPoint = FindTransformByName(fpsRoot, "WeaponBone");
                if (weaponAttachPoint != null)
                    return;
            }

            weaponAttachPoint = transform.Find("Graphics/Character_model/WeaponBone");
            if (weaponAttachPoint != null)
                return;

            Transform graphics = transform.Find("Graphics");
            if (graphics != null)
            {
                for (int i = 0; i < graphics.childCount; i++)
                {
                    weaponAttachPoint = FindTransformByName(graphics.GetChild(i), "WeaponBone");
                    if (weaponAttachPoint != null)
                        return;
                }
            }

            weaponAttachPoint = FindTransformByName(transform, "WeaponBone");
        }

        Transform FindIkWeaponBone()
        {
            if (_handPoseState == null)
                _handPoseState = GetComponent<ShooterHandPoseState>();

            Transform fpsRoot = _handPoseState != null ? _handPoseState.FpsCharacterRoot : null;
            if (fpsRoot != null)
            {
                var rig = fpsRoot.GetComponent<KRigComponent>();
                if (rig != null)
                {
                    Transform ik = rig.GetRigTransform(new KRigElement(-1, FPSANames.IkWeaponBone));
                    if (ik != null)
                        return ik;
                }

                Transform named = FindTransformByName(fpsRoot, WeaponPrefabUtility.IkWeaponBoneName);
                if (named != null)
                    return named;
            }

            return FindTransformByName(transform, WeaponPrefabUtility.IkWeaponBoneName);
        }

        static Transform FindTransformByName(Transform root, string boneName)
        {
            if (root == null || string.IsNullOrEmpty(boneName))
                return null;

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == boneName)
                    return all[i];
            }

            return null;
        }

        void OnGUI()
        {
            if (!showAmmoHud)
                return;

            if (_activeWeapon is RangedWeapon ranged)
            {
                DrawAmmoLine($"{ranged.WeaponId}  {ranged.Magazine}/{ranged.Reserve}");
                return;
            }

            if (_activeSlotIndex >= 0 && !IsHolstered)
                DrawAmmoLine("Weapon slot active — assign a scene weapon to Weapon Slots");
        }

        static void DrawAmmoLine(string text)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = Color.black;
            GUI.Label(new Rect(13f, 13f, 520f, 30f), text, style);
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(12f, 12f, 520f, 30f), text, style);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (weaponSlots == null || weaponSlots.Length != MaxWeaponSlots)
            {
                var resized = new WeaponBase[MaxWeaponSlots];
                if (weaponSlots != null)
                {
                    int copy = Mathf.Min(weaponSlots.Length, MaxWeaponSlots);
                    Array.Copy(weaponSlots, resized, copy);
                }

                weaponSlots = resized;
            }
        }
#endif
    }
}
