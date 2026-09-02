# ТЗ: система оружия и поведения игрока

**Источник:** Robert Tur, 30.08.2026  
**Статус:** согласованный план работ (Задача 2)  
**Стек:** тот же персонаж + **FPS Animation Framework** + demo-оружия из пакета; **Retarget Pro** — для переноса рук на полное тело (по документации KINEMATION).

**Вне scope исполнителя (доработает заказчик):** финальный UI, подбор «красивых» звуков — пока любые бесплатные / из demo.

---

## 0. Предварительные пожелания заказчика (~29.08.2026)

До финального ТЗ (раздел 1 ниже) в переписке были зафиксированы ожидания:

- **Главное ограничение:** оружие не стреляет и не достаётся во время **прыжка**, **бега**, **лестницы**.
- **Модели:** временно demo из FPS Animation Pro; **Retarget Pro** для full-body; замена mesh через Blender — позже.
- **Количество:** до **3 стрелковых** (пистолет + 2 автомата) + **ближний бой** (анимации искать отдельно).
- **Функции:** стрельба, попадания, урон, перезарядка, патроны, SFX/VFX (placeholder), переключение, подбор, выбрасывание.
- Заказчик сам доработает UI и финальные звуки.

Практический гайд по текущей реализации: **[WEAPON_SETUP.md](WEAPON_SETUP.md)**.

---

## 1. Общие требования

| Требование | Реализация |
|------------|------------|
| Два типа оружия | `MeleeWeapon`, `RangedWeapon` : `IWeapon` |
| Прочность, тип патронов, функции, SFX/VFX, анимации | Базовые поля на weapon prefab + ScriptableObject при необходимости |
| Прочность = 0 | Нельзя достать; при поломке в руках — SFX + auto unequip |

---

## 2. Архитектура (обязательно по ТЗ)

```
PlayerCharacter
├── ShooterPlayerInventory      — hasGun1…hasGun5 (+ расширяемо)
├── WeaponManager               — слоты, ввод 1–6, ограничения по состоянию
├── ShooterCharacterController  — уже есть: CCP ↔ FPS AF
├── ShooterHandPoseState        — unarmed overlay (слот «пустые руки»)
└── ShooterLadderFpsBridge      — расширить: auto holster / restore на лестнице
```

### `IWeapon`

```csharp
void Equip();
void Unequip();
void Attack();      // melee combo / ranged fire entry
void Reload();
void CheckAmmo();   // inspect mag / патроны
void OnBreak();
```

### Классы

| Класс | Ответственность |
|-------|-----------------|
| **`WeaponManager`** | Активный слот, смена по 1–6, блокировки (бег/прыжок/лестница), связь с FPS AF (`Playables`, recoil, equip curves) |
| **`RangedWeapon`** | Fire mode (auto/semi), reload, inspect, muzzle flash, shell eject, ammo + durability |
| **`MeleeWeapon`** | Combo chain по повторным нажатиям, durability, расширяемый список атак |
| **`ShooterPlayerInventory`** | `bool hasGun1` … `hasGun5`; проверка перед equip |

**База для интеграции с FPS AF:** адаптировать паттерны из `Assets/Demo/Scripts/Runtime/Item/Weapon.cs` и `FPSController.cs`, но под CCP и наш `ShooterCharacterController` (не Unity `CharacterController` demo).

---

## 3. Клавиши и слоты

| Клавиша | Действие |
|---------|----------|
| **1** | Убрать оружие — `WeaponManager.Holster()` → `ShooterHandPoseState.SetUnarmed()` |
| **2–6** | Слоты оружия №1–№5 |

- Equip только если `hasGunX == true` и прочность > 0.
- **T убрана (31.08.2026):** только **1–6**, без toggle. Actions: `WeaponSlot1`…`WeaponSlot6` в `InputSystem_Actions.inputactions`.

---

## 4. Ограничения по состоянию игрока

Источник истины состояния: CCP (`CharacterState` / grounded / sprint / ladder) + `WeaponManager.CanPerform(WeaponAction)`.

| Состояние | Разрешено | Запрещено |
|-----------|-----------|-----------|
| **Карабканье** | — | стрелять, доставать, менять оружие; **перед залезом** auto unequip; **после** — auto equip предыдущего |
| **Прыжок (в воздухе)** | — | стрелять, перезаряжаться, менять оружие |
| **Бег** | — | стрелять |
| **Ходьба / присед / стояние** | стрелять, reload, смена, equip/unequip | — |

**Уже есть:** `ShooterLadderFpsBridge`, `ShooterJumpWindup`, sprint через CCP — в Задаче 2 добавить **gates** в `WeaponManager`, не дублировать логику движения.

---

## 5. Оружие и стены (FPS AF)

Требование: при упоре в стену оружие **поднимается/опускается**, не проходит сквозь геомetрию.

**В пакете уже есть:** `CollisionLayer` (`Assets/KINEMATION/.../CollisionLayer/`). В demo-профилях оружия (`AnimatorProfile_AK12`, `Mk18`, `FAL`, `Pistol`…) — `CollisionLayerSettings` с raycast от дула / IK WeaponBone.

**План:** добавить `CollisionLayerSettings` в `AnimatorProfile_CharacterModel` (или per-weapon profile), layer mask = стены уровня, primary/secondary pose — из demo-профилей.

---

## 6. Боеприпасы

**Выбор исполнителя:** **вариант Б (гибкий)** с простым стартом:

- `AmmoType` (enum или SO): Pistol, Rifle, …
- У `RangedWeapon` — список поддерживаемых типов.
- Pickup — тип + количество; при совпадении — пополнение.

Вариант А (пистолетный / автоматный тип) — частный случай двух `AmmoType`.

**UI:** простой TextMesh / debug overlay (заказчик доработает).

---

## 7. Дальний бой — функции

- [ ] Стрельба: semi / auto (`supportsAuto` как в demo `Weapon.cs`)
- [ ] Перезарядка (empty / tactical — по наличию клипов в demo)
- [ ] Осмотр оружия (`AA_Inspect` и аналоги в demo)
- [ ] Проверка патронов (`CheckAmmo` — анимация + опционально UI)
- [ ] SFX выстрела (demo / placeholder)
- [ ] Muzzle flash
- [ ] Вылетающая гильза (demo `GunPartsGeneral` / weapon animator)

---

## 8. Ближний бой — функции

- [ ] Combo: цепочка атак по повторному `Attack()` (`C_Knife_Attack` и demo knife)
- [ ] Прочность (− за удар)
- [ ] SFX удара
- [ ] Расширяемый список `FPSAnimationAsset` / clips на prefab

---

## 9. Прочность

- Параметр на каждом оружии; − при использовании (выстрел / удар).
- `0` → `hasGunX` можно оставить true, но equip блокируется; если было в руках → `OnBreak()` + unequip + break SFX.

---

## 10. План реализации (Задача 2 — подзадачи)

Оценка ориентировочная; порядок — от фундамента к полировке.

### 2.1 — Каркас и инвентарь (~4–6 ч)

- [x] `IWeapon`, базовый `WeaponBase` (durability, slot index)
- [x] `ShooterPlayerInventory` (`hasGun1`…`hasGun5`, API `HasWeapon(slot)`)
- [x] `WeaponManager`: слоты, ввод **1–6**, holster/equip, gates (бег/прыжок/лестница)
- [x] Связь **1** ↔ unarmed через `ShooterHandPoseState`; **T** убрана
- [x] Input Actions: `WeaponSlot1`…`WeaponSlot6` (клавиши 1–6)
- [x] Editor: **Shooter → Project → Add Weapon System** (или **Phase 2 → Add Weapon System**)
- [x] Prefabs на `WeaponManager` + `hasGun1…3` для Mk18/AK12/Mk23 (Setup Ranged Weapons)

### 2.2 — Ranged MVP (~6–8 ч)

- [x] `RangedWeapon`: fire semi/auto, reload, ammo + `AmmoType`, recoil, hitscan, camera shake
- [x] Prefabs: **Mk18** (slot 2), **AK12** (slot 3), **Mk23** pistol (slot 4) via **Shooter → Project → Setup Ranged Weapons**
- [x] Attach на `IK WeaponBone` + local offsets (см. [WEAPON_SETUP.md](WEAPON_SETUP.md))
- [ ] Equip/Unequip polish (per-weapon overlay pose)
- [ ] Muzzle flash + shell prefabs (slots ready, assign VFX assets)
- [ ] Tactical reload variant

### 2.3 — Melee MVP (~3–4 ч)

- [ ] `MeleeWeapon` на demo **Knife**
- [ ] Combo chain (2–3 удара)
- [ ] Durability + break

### 2.4 — Ограничения движения (~3–4 ч)

- [x] `WeaponManager` gates: sprint / jump (в воздухе) — блок fire/reload/swap
- [x] Лестница: auto unequip при входе, restore при выходе (`ShooterLadderFpsBridge` → `NotifyLadderEnter/Exit`)
- [x] Блок reload / weapon swap в воздухе

### 2.5 — Стены и FPS AF (~2–3 ч)

- [ ] `CollisionLayer` в profile персонажа / оружия
- [ ] Raycast mask, poses из demo profile
- [ ] Проверка у стены в тестовой сцене

### 2.6 — Inspect, CheckAmmo, break (~2–3 ч)

- [ ] Inspect animation
- [ ] CheckAmmo (анимация + debug UI)
- [ ] `OnBreak()` flow

### 2.7 — Подбор патронов (~2 ч)

- [ ] Pickup trigger + `AmmoPickup` (type + count)
- [ ] Расширяемый список типов на оружии

### 2.8 — Тестовая сцена оружия (~2 ч)

- [ ] Слоты 2–3 с разным оружием, стена для collision, pickup патронов
- [ ] Чеклист по ТЗ (таблица ниже)

**Не в Задаче 2:** Motion Warping (mantle/vault) — по-прежнему вне scope.

---

## 11. Demo-ассеты (стартовый набор)

| Назначение | Путь в проекте |
|------------|----------------|
| Автомат | `Assets/Demo/Meshes/Weapons/MK18/`, `AK12/` |
| Пистолет | `Assets/Demo/Meshes/Weapons/Mk23Mod0/` |
| Нож | `Assets/Demo/Meshes/Weapons/Knife/` |
| Анимации / AA_* | `Assets/Demo/Animations/Weapons/` |
| Референс кода | `Assets/Demo/Scripts/Runtime/Item/Weapon.cs`, `FPSController.cs` |
| Collision layer | `Assets/Demo/AnimatorProfiles/AnimatorProfile_Mk18.asset` (и др.) |

**Retarget Pro:** перенос клипов с demo-скелета на `Character_model` — по видео/доку KINEMATION; учесть риск из [TASKS.md](TASKS.md) (Humanoid avatar уже настроен).

---

## 12. Чеклист приёмки (из ТЗ)

| # | Критерий |
|---|----------|
| 1 | Клавиши 1–6: holster + 5 слотов, respect `hasGunX` |
| 2 | Ranged: fire, reload, inspect, check ammo, VFX/SFX |
| 3 | Melee: combo, durability |
| 4 | Прочность 0 → нельзя достать; break in hands → SFX + unequip |
| 5 | Лестница: auto holster/restore; no shoot/swap on climb |
| 6 | Прыжок: no shoot/reload/swap |
| 7 | Бег: no shoot |
| 8 | Walk/crouch/idle: всё разрешено |
| 9 | Стена: оружие не клиппит (CollisionLayer) |
| 10 | Архитектура: `WeaponManager`, `IWeapon`, `MeleeWeapon`, `RangedWeapon` |

---

## 13. Риски и связь с уже сделанным

| Риск | Митигация |
|------|-----------|
| Armed/unarmed overlay ([FPS_CAMERA_AND_HANDS.md](FPS_CAMERA_AND_HANDS.md)) | Equip оружия = `SetArmed()` + weapon mesh на **IK WeaponBone**; клавиша **1** = unarmed. **Не** `LinkAnimatorProfile` demo-оружия |
| Demo `FPSController` ≠ CCP | Брать только weapon/playables/recoil; движение — `ShooterCharacterController` |
| Лестница + оружие | Расширить bridge, не отключать FPS AF целиком |
| Клавиша T vs 1 | **Решено 31.08:** только **1–6**; T и `ToggleHandPose` удалены | — |
| Только руки в demo | Retarget + full-body locomotion уже работает на Humanoid |

---

## См. также

- [TASKS.md](TASKS.md) — Задача 1 (движение, закрыта), **Задача 2** (статус и нюансы)
- [WEAPON_SETUP.md](WEAPON_SETUP.md) — практический гайд (меню, крепление, клавиши)
- [CLIENT_STATUS.md](CLIENT_STATUS.md) — статус для заказчика
- [FPS_CAMERA_AND_HANDS.md](FPS_CAMERA_AND_HANDS.md) — не ломать при equip/unarmed
- [PHASE2_SETUP.md](PHASE2_SETUP.md) — FPS AF setup
- [PHASE5_SETUP.md](PHASE5_SETUP.md) — legacy Phase 5 (не использовать)
