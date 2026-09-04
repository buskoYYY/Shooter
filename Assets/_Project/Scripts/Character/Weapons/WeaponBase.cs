using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Shooter.Project.Weapons
{
    public abstract class WeaponBase : MonoBehaviour, IWeapon
    {
        [SerializeField] string weaponId = "weapon";
        [SerializeField] int slotIndex;
        [SerializeField] float maxDurability = 100f;
        [SerializeField] float durability = 100f;

        [Header("Attach (local to IK WeaponBone)")]
        [Tooltip("Source of truth for weapon pose. Moving the Transform alone is overwritten on Play — use Capture Attach.")]
        [SerializeField] Vector3 attachLocalPosition;
        [SerializeField] Vector3 attachLocalEulerAngles;

        public string WeaponId => weaponId;
        public int SlotIndex => slotIndex;
        public float MaxDurability => maxDurability;
        public float Durability => durability;
        public bool IsBroken => durability <= 0f;

        public virtual void Equip()
        {
            gameObject.SetActive(true);
        }

        public virtual void Unequip()
        {
            gameObject.SetActive(false);
        }

        public abstract void Attack();
        public abstract void Reload();
        public abstract void CheckAmmo();

        public virtual void OnBreak()
        {
            Unequip();
        }

        public void ApplyWear(float amount)
        {
            if (amount <= 0f || IsBroken)
                return;

            durability = Mathf.Max(0f, durability - amount);
            if (IsBroken)
                OnBreak();
        }

        public void SetDurability(float value)
        {
            durability = Mathf.Clamp(value, 0f, maxDurability);
        }

        public void InitializeForSlot(int index)
        {
            slotIndex = Mathf.Max(0, index);
            ApplyAttachTransform();
        }

        public void ApplyAttachTransform()
        {
            transform.localPosition = attachLocalPosition;
            transform.localRotation = Quaternion.Euler(attachLocalEulerAngles);
        }

        public void CaptureAttachFromTransform()
        {
            attachLocalPosition = transform.localPosition;
            attachLocalEulerAngles = transform.localEulerAngles;
        }

#if UNITY_EDITOR
        [ContextMenu("Capture Attach From Transform")]
        void CaptureAttachFromTransformMenu()
        {
            Undo.RecordObject(this, "Capture Weapon Attach");
            CaptureAttachFromTransform();
            EditorUtility.SetDirty(this);
        }

        [ContextMenu("Apply Attach To Transform")]
        void ApplyAttachToTransformMenu()
        {
            Undo.RecordObject(transform, "Apply Weapon Attach");
            ApplyAttachTransform();
            EditorUtility.SetDirty(transform);
        }

        void OnValidate()
        {
            slotIndex = Mathf.Max(0, slotIndex);
            maxDurability = Mathf.Max(1f, maxDurability);
            durability = Mathf.Clamp(durability, 0f, maxDurability);
        }
#endif
    }
}
