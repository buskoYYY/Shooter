using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shooter.Project.Weapons
{
    [DisallowMultipleComponent]
    public class ShooterWeaponInventory : MonoBehaviour
    {
        const int MaxSlots = 4;

        readonly List<ShooterWeaponLoadout> _slots = new List<ShooterWeaponLoadout>(MaxSlots);
        int _activeIndex = -1;

        public event Action<ShooterWeaponLoadout> EquippedChanged;
        public event Action<ShooterWeaponLoadout> Dropped;

        public int Count => _slots.Count;
        public int ActiveIndex => _activeIndex;
        public bool HasEquipped => Active != null;
        public ShooterWeaponLoadout Active =>
            _activeIndex >= 0 && _activeIndex < _slots.Count ? _slots[_activeIndex] : null;

        public IReadOnlyList<ShooterWeaponLoadout> Slots => _slots;

        public bool TryAdd(ShooterWeaponDefinition definition, out ShooterWeaponLoadout loadout)
        {
            loadout = null;
            if (definition == null || _slots.Count >= MaxSlots)
                return false;

            loadout = ShooterWeaponLoadout.FromDefinition(definition);
            _slots.Add(loadout);
            if (_activeIndex < 0)
                SetActive(_slots.Count - 1);
            return true;
        }

        public bool TryAddReserveToActive(int amount)
        {
            if (Active == null || Active.IsMelee)
                return false;
            Active.AddReserve(amount);
            return true;
        }

        public bool TryAddReserveMatching(ShooterWeaponDefinition definition, int amount)
        {
            if (definition == null)
                return false;

            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].definition == definition && !_slots[i].IsMelee)
                {
                    _slots[i].AddReserve(amount);
                    return true;
                }
            }

            return false;
        }

        public bool SetActive(int index)
        {
            if (index < 0 || index >= _slots.Count)
                return false;
            _activeIndex = index;
            EquippedChanged?.Invoke(Active);
            return true;
        }

        public bool Cycle(int direction)
        {
            if (_slots.Count <= 1)
                return false;

            int next = (_activeIndex + direction) % _slots.Count;
            if (next < 0)
                next += _slots.Count;
            return SetActive(next);
        }

        public bool TryDropActive(out ShooterWeaponLoadout dropped)
        {
            dropped = null;
            if (Active == null)
                return false;

            dropped = Active;
            _slots.RemoveAt(_activeIndex);
            if (_slots.Count == 0)
                _activeIndex = -1;
            else if (_activeIndex >= _slots.Count)
                _activeIndex = _slots.Count - 1;

            Dropped?.Invoke(dropped);
            EquippedChanged?.Invoke(Active);
            return true;
        }
    }
}
