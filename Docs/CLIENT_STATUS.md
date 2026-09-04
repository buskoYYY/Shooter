# Статус для заказчика (Shooter — персонаж и FPS-тело)

**Дата:** 2 сентября 2026  
**Стек на текущем этапе:** **Character Controller Pro (CCP)** + **FPS Animation Framework (FPS AF)**.  
Motion Warping (mantle/vault) — **не в scope** до отдельного согласования.

**Этап движения:** ✅ **завершён**.  
**Текущий этап:** **Задача 2 — система оружия** (2.1 ✅, 2.2 🟡) → [WEAPON_SYSTEM_TZ.md](WEAPON_SYSTEM_TZ.md), гайд: [WEAPON_SETUP.md](WEAPON_SETUP.md).

---

## Что уже сделано

### Инфраструктура проекта
- Unity-проект (URP, Input System)
- Документация и план фаз: `docs/TASKS.md`
- Авто-setup через меню **Shooter → Phase 0–4** в редакторе
- Dev-панель баланса **F8/F9** — отключена в runtime (см. `ShooterBalanceTuningPanel`)

### Фаза 0–1 — Движение (CCP)
- Персонаж на базе **Demo Character 3D** + модель **Character_model**
- Префаб `PlayerCharacter`, тестовая сцена `PlayerTest` (пол, склон)
- **ShooterInputHandler** — Unity Input System → CCP (WASD, прыжок, бег, присед, Interact)
- CCP: ходьба, бег, прыжок, движение по склону
- **ShooterCcpMovementTuning** — скорости, разгон/торможение (accel 22 / decel 24)
- **ShooterBodySizeTuning** — ширина капсулы **0.72 m** через CharacterBody (не CapsuleCollider)

### Фаза 2–3 — FPS-тело (FPS Animation Framework)
- На модели: **FPSAnimator**, Rig, Animator Profile, IK-цели
- **FPSAnimator_Humanoid** — locomotion (ходьба/бег/стрейф)
- FPS-камера на голове, процедурные слои: **Turn, Look, Sway, IK**
- **ShooterCharacterController** — мост CCP ↔ FPS AF (input, поворот, параметры аниматора)
- **ShooterHandPoseState** — unarmed/armed overlay; управление через **WeaponManager** (1–6), не T
- Модель на **Humanoid** Avatar (обязательно для demo-анимаций)

### Полировка движения (август 2026)
- **Прыжок:** без air-strafe в воздухе; короткая «пружина» (присед ~0.14 с → отрыв) — `ShooterJumpWindup`
- **Разгон:** отзывчивый поворот при ходьбе (CCP + smoothing аниматора 7/8)
- **Разворот на месте:** плавный переход в ходьбу, если нажать W во время перебирания ног
- **Лестница:** плавный подход к стене, камера без дрожи и без «упора в стену»; скрытие куртки на climb

### Фаза 4 — Лестницы (CCP)
- Editor: **Shooter → Phase 4 → Run Full Phase 4 Setup**
- CCP state **LadderClimbing** + тестовая лестница в сцене
- **ShooterLadderFpsBridge** — отключение FPS sway/turn на лестнице, restore после слезания
- **ShooterFpsCameraApply** / **ShooterFpsHeadHide** — камера и вид тела на лестнице

---

## Что работает в Play Mode (сейчас)

| Функция | Статус |
|---------|--------|
| WASD + мышь (FPS-обзор) | ✅ |
| Бег (Shift), присед (C) | ✅ |
| Прыжок (Space) — пружина + без стрейфа в воздухе | ✅ |
| Locomotion (ходьба/бег/стрейф, Humanoid) | ✅ |
| Unarmed / armed (клавиши **1–6**, holster + слоты) | ✅ |
| Ranged: Mk18 / AK12 / Mk23 — fire, reload, ammo HUD | 🟡 2.2 |
| Gates: спринт / прыжок / лестница | ✅ 2.4 |
| Процедурное тело (sway / turn / look / IK) | ✅ |
| Разворот на месте → ходьба | ✅ |
| Лестница (Interact + climb + exit) | ✅ |
| Оружие: melee, CollisionLayer, inspect, pickups | ❌ 2.3–2.8 |
| Mantle / Vault (Motion Warping) | ❌ **вне scope** |

---

## Следующие шаги (план по ТЗ 30.08.2026)

Полный план: **[WEAPON_SYSTEM_TZ.md](WEAPON_SYSTEM_TZ.md)**

| Этап | Содержание | Статус |
|------|------------|--------|
| **2.1** | `IWeapon`, `WeaponManager`, инвентарь, клавиши **1–6** | ✅ |
| **2.2** | Mk18/AK12/Mk23: fire, reload, recoil, ammo HUD, VFX/SFX | 🟡 pose polish |
| **2.3** | Ближний бой: combo, knife, прочность | ❌ |
| **2.4** | Gates: спринт, прыжок, лестница (holster/restore) | ✅ |
| **2.5** | Стены: **CollisionLayer** FPS AF | ❌ |
| **2.6–2.8** | Inspect, check ammo, break, pickups, тест-сцена | ❌ |

**Заказчик доработает сам:** финальный UI, подбор звуков (пока placeholder / demo).

**Вне scope:** Motion Warping (mantle/vault).

---

## Краткий текст для сообщения заказчику

> Блок движения закрыт. По ТЗ от 30.08 реализована **система оружия**: слоты 1–6, Mk18/AK12/пистолет (стрельба, reload, патроны), ограничения при беге/прыжке/лестнице. Дальше — VFX, melee, CollisionLayer у стены, pickups. Гайд: WEAPON_SETUP.md.

---

## Связанные документы

| Документ | Содержание |
|----------|------------|
| `docs/WEAPON_SYSTEM_TZ.md` | **ТЗ заказчика + план Задачи 2** |
| `docs/WEAPON_SETUP.md` | **Практический гайд** (меню, клавиши, IK WeaponBone) |
| `docs/TASKS.md` | Техническая документация, задачи 1.x + **Задача 2** |
| `docs/BALANCE_TUNING_PANEL.md` | Описание F8-панели (отключена) |
| `docs/PHASE1_SETUP.md` … `PHASE4_SETUP.md` | Auto-setup в Unity |
| `docs/PHASE5_SETUP.md` | Legacy weapon setup (не использовать) |
| `docs/README.md` | Оглавление документации |
