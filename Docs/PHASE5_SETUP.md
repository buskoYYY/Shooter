# Phase 5 Setup — ⚠️ УСТАРЕЛО (legacy)

Этот документ описывает **ранний прототип** оружия на `ShooterWeaponController` (`Assets/_Project/Scripts/Weapons/`).  
Он **не используется** в текущей реализации.

---

## Актуальная система оружия

| Документ | Содержание |
|----------|------------|
| **[WEAPON_SETUP.md](WEAPON_SETUP.md)** | Практический гайд: меню, клавиши, крепление на `IK WeaponBone` |
| **[WEAPON_SYSTEM_TZ.md](WEAPON_SYSTEM_TZ.md)** | ТЗ заказчика + план подзадач 2.1–2.8 |
| **[TASKS.md](TASKS.md)** | Раздел «Задача 2» — статус и нюансы |

**Editor-меню (использовать):**

- **Shooter → Project → Add Weapon System**
- **Shooter → Project → Setup Ranged Weapons (Mk18 / AK12 / Pistol)**

**Не запускать:** `Shooter → Phase 5 → Run Full Weapon Setup` — вешает legacy-компоненты и конфликтует с `WeaponManager`.

---

## Чем legacy отличается от текущего

| | Legacy (Phase 5) | Текущий (`WeaponManager`) |
|--|------------------|---------------------------|
| Код | `ShooterWeaponController`, `ShooterWeaponInventory` | `WeaponManager`, `RangedWeapon`, `ShooterPlayerInventory` |
| Клавиши | T, G, колёсико | **1–6** (T убрана) |
| Крепление | Runtime spawn на weapon bone | `IK WeaponBone` + attach transforms |
| Armed overlay | Смешение с legacy | `ShooterHandPoseState.SetArmed()` |
| HUD | Старый debug | Ammo HUD на `WeaponManager` |

При открытии старых сцен проверьте, что на игроке **нет** `ShooterWeaponController` / `ShooterWeaponActionGate` — `WeaponManager.Awake()` отключает их, если найдёт.

---

## Архив: что делал старый setup (для справки)

1. **Shooter → Phase 5 → Run Full Weapon Setup**
2. Вешал `ShooterWeaponActionGate`, `ShooterWeaponInventory`, `ShooterWeaponController`
3. Создавал SO в `Assets/_Project/Weapons/` (Mk23, AK12, Mk18, Knife)
4. Добавлял мишень и пикапы в `PlayerTest`
5. Управление: T holster, G drop, колёсико cycle

Это было до согласованного ТЗ (30.08.2026) и заменено официальной архитектурой из [WEAPON_SYSTEM_TZ.md](WEAPON_SYSTEM_TZ.md).
