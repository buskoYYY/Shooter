# Настройка оружия (Задача 2 — практический гайд)

**Статус:** актуально для текущей реализации (`WeaponManager` + `RangedWeapon`).  
**ТЗ и план подзадач:** [WEAPON_SYSTEM_TZ.md](WEAPON_SYSTEM_TZ.md)  
**Архитектура и история:** [TASKS.md](TASKS.md) → раздел «Задача 2».

> **Важно:** меню **Shooter → Phase 5 → Run Full Weapon Setup** — **устаревший** каркас (`ShooterWeaponController`). Используйте только **Shooter → Project** (см. ниже). Подробнее: [PHASE5_SETUP.md](PHASE5_SETUP.md).

---

## Быстрый старт

1. **Shooter → Project → Add Weapon System** — компоненты, Input Actions, `WeaponManager` на `PlayerCharacter`.
2. **Shooter → Project → Setup Ranged Weapons (Mk18 / AK12 / Pistol)** — префабы в `Assets/_Project/Weapons/Prefabs/`, слоты 0–2 на менеджере.
3. Play в `PlayerTest`.

### Управление

| Клавиша | Действие |
|---------|----------|
| **1** | Holster (пустые руки, unarmed overlay) |
| **2** | Mk18 (слот 0) |
| **3** | AK12 (слот 1) |
| **4** | Mk23 pistol (слот 2) |
| **5 / 6** | Резерв (melee / доп. слоты) |
| **ЛКМ** | Огонь |
| **R** | Перезарядка |

**Запрещено:** стрельба, перезарядка, смена оружия во время **спринта**, **прыжка** (в воздухе), **лестницы**. На лестнице — auto holster, при выходе — restore предыдущего слота.

Клавиша **T** (armed/unarmed) **удалена** — только **1–6**.

---

## Архитектура (код)

```
PlayerCharacter
├── ShooterPlayerInventory     — hasGun1…hasGun5
├── WeaponManager              — слоты, ввод, gates, ammo HUD
├── ShooterHandPoseState       — unarmed/armed overlay (клавиша 1)
├── ShooterLadderFpsBridge     — NotifyLadderEnter/Exit → holster/restore
└── Graphics/Character_model/
    └── … / IK WeaponBone      — точка крепления меша оружия
```

Скрипты: `Assets/_Project/Scripts/Character/Weapons/`

| Класс | Назначение |
|-------|------------|
| `IWeapon` / `WeaponBase` | Интерфейс + прочность, attach transform |
| `RangedWeapon` | Hitscan, ammo, reload, recoil, camera shake |
| `MeleeWeapon` | Заглушка (Задача 2.3) |
| `WeaponManager` | Слоты, holster/equip, gates, HUD патронов |
| `ShooterPlayerInventory` | Флаги `hasGun1…5` |
| `WeaponPrefabUtility` | Снятие MeshCollider/Rigidbody с demo-мешей |

Префабы: `Assets/_Project/Weapons/Prefabs/Ranged_Mk18`, `Ranged_AK12`, `Ranged_Mk23`.

---

## Крепление оружия (критично)

### Родитель — `IK WeaponBone`, не `WeaponBone`

Оружие должно быть дочерним объектом **`IK WeaponBone`** (в иерархии `Character_model` → rig).  
`WeaponBone` — только для IK-цели рук; при parent на него меш **не следует** за анимацией торса.

`WeaponManager` ищет кость через `KRigComponent` / имя `IK WeaponBone` (`WeaponPrefabUtility.IkWeaponBoneName`).

### Два способа размещения

| Способ | Когда использовать |
|--------|-------------------|
| **В иерархии префаба** | Удобно для ручной подгонки позы в Editor (предпочтительно) |
| **Runtime spawn** | Fallback: если в иерархии нет экземпляра, менеджер создаёт из `weaponSlots[]` |

На `WeaponManager`:
- `weaponAttachPoint` — ссылка на `IK WeaponBone` (auto-resolve)
- `autoCollectFromAttachPoint` — собрать дочерние `WeaponBase` по `Slot Index`
- `weaponSlots[]` — ссылки на scene-объекты **или** prefab-шаблоны для spawn

### Attach transform (local к IK WeaponBone)

Поля на `WeaponBase`: `attachLocalPosition`, `attachLocalEulerAngles`.  
Применяются в `InitializeForSlot()` / `ApplyAttachTransform()`.

**Текущие значения (сентябрь 2026):**

| Оружие | Position | Rotation (Euler) |
|--------|----------|------------------|
| Mk18 | (-0.039, 0.05, -0.009) | (0.22, 347.70, 359.70) |
| AK12 | (-0.033, 0.07, -0.007) | (0.25, 347.60, 0.13) |
| Mk23 | (-0.016, 0.026, -0.156) | (0.77, 345.55, 359.83) |

Подгонка: выбрать оружие под `IK WeaponBone` в префабе `PlayerCharacter`, двигать в Scene view, скопировать local pos/rot в prefab оружия.

---

## Equip / armed overlay — что НЕ делать

| ❌ Нельзя | Почему |
|----------|--------|
| `LinkAnimatorProfile` demo-оружия на `Character_model` | Ломает Humanoid-скелет («взрыв» меша) |
| `Demo.FPSController` на CCP-игроке | Конфликт с `ShooterCharacterController` |
| Parent на `WeaponBone` | Оружие не следует за анимацией |
| Оставлять MeshCollider на holosight | Ошибка concave mesh collider |

**Правильный equip:** `ShooterHandPoseState.SetArmed()` + активировать меш на `IK WeaponBone`.  
Overlay armed/unarmed — см. [FPS_CAMERA_AND_HANDS.md](FPS_CAMERA_AND_HANDS.md).

При spawn editor setup снимает коллайдеры и rigidbody с demo-частей (`WeaponPrefabUtility.StripPhysics`).

---

## Editor-меню

| Меню | Действие |
|------|----------|
| **Shooter → Project → Add Weapon System** | `WeaponManager`, inventory, bootstrap, input bindings |
| **Shooter → Project → Setup Ranged Weapons** | Mk18 / AK12 / Mk23 prefabs + wire slots |
| **Shooter → Phase 2 → Add Weapon System** | То же (алиас) |

---

## Проверка в Play Mode

1. Старт **unarmed** (руки опущены).
2. **2** — Mk18 в руках, HUD `30/90` (левый верхний угол).
3. **ЛКМ** — выстрел, отдача камеры; **R** — перезарядка.
4. **Shift** (спринт) — огонь не идёт.
5. **Space** (прыжок) — огонь/reload/swap заблокированы в воздухе.
6. Лестница — auto holster; после слезания — restore слота.
7. **1** — holster, unarmed overlay.

---

## Что осталось по ТЗ

См. чеклисты **2.2–2.8** в [WEAPON_SYSTEM_TZ.md](WEAPON_SYSTEM_TZ.md):

- Muzzle flash / shell VFX (слоты есть, назначить ассеты)
- Melee combo (2.3)
- CollisionLayer у стены (2.5)
- Inspect, check ammo, break, pickups (2.6–2.7)
- Тест-сцена с мишенью и пикапами (2.8)

---

## См. также

- [WEAPON_SYSTEM_TZ.md](WEAPON_SYSTEM_TZ.md) — полное ТЗ заказчика
- [FPS_CAMERA_AND_HANDS.md](FPS_CAMERA_AND_HANDS.md) — не ломать overlay при equip
- [PHASE5_SETUP.md](PHASE5_SETUP.md) — legacy setup (не использовать)
