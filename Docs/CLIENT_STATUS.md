# Статус для заказчика (Shooter — персонаж и FPS-тело)

**Дата:** 31 августа 2026  
**Стек на текущем этапе:** **Character Controller Pro (CCP)** + **FPS Animation Framework (FPS AF)**.  
Motion Warping (mantle/vault) — **не в scope** до отдельного согласования.

**Этап движения:** ✅ **завершён**.  
**Следующий этап:** **Задача 2 — система оружия** по ТЗ Robert (30.08.2026) → [WEAPON_SYSTEM_TZ.md](WEAPON_SYSTEM_TZ.md).

---

## Что уже сделано

### Инфраструктура проекта
- Unity-проект (URP, Input System)
- Документация и план фаз: `docs/TASKS.md`
- Авто-setup через меню **Shooter → Phase 0–4** в редакторе
- Dev-панель баланса **F8** (временная, см. `docs/BALANCE_TUNING_PANEL.md`)

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
| Unarmed / armed (клавиши **1–6**, holster + слоты) | ✅ каркас 2.1 |
| Процедурное тело (sway / turn / look / IK) | ✅ |
| Разворот на месте → ходьба | ✅ |
| Лестница (Interact + climb + exit) | ✅ |
| F8 — временная панель баланса | ✅ |
| Оружие: каркас 1–6 + Mk18/AK12/Pistol ranged MVP | 🟡 2.1–2.2 |
| Оружие: melee, CollisionLayer, inspect, pickups | ❌ дальше по ТЗ |
| Mantle / Vault (Motion Warping) | ❌ **вне scope** |

---

## Следующие шаги (план по ТЗ 30.08.2026)

Полный план: **[WEAPON_SYSTEM_TZ.md](WEAPON_SYSTEM_TZ.md)**

| Этап | Содержание |
|------|------------|
| **2.1** | `IWeapon`, `WeaponManager`, инвентарь `hasGun1…5`, клавиши **1–6** |
| **2.2** | Дальний бой: стрельба, reload, recoil, VFX (demo Mk18/AK12) |
| **2.3** | Ближний бой: combo, knife, прочность |
| **2.4** | Ограничения: лестница (auto holster), прыжок, бег |
| **2.5** | Стены: **CollisionLayer** FPS AF |
| **2.6–2.8** | Inspect, check ammo, break, pickups, тест-сцена |

**Заказчик доработает сам:** финальный UI, подбор звуков (пока placeholder / demo).

**Вне scope:** Motion Warping (mantle/vault).

---

## Краткий текст для сообщения заказчику

> Блок движения закрыт. По ТЗ от 30.08 начинаем **систему оружия**: слоты 1–6, инвентарь, дальний/ближний бой, прочность, патроны, ограничения при беге/прыжке/лестнице, поднятие оружия у стены (FPS AF CollisionLayer). За основу — demo-оружия и анимации из FPS Animation Framework; звуки и UI — простые заглушки.

---

## Связанные документы

| Документ | Содержание |
|----------|------------|
| `docs/WEAPON_SYSTEM_TZ.md` | **ТЗ заказчика + план Задачи 2** (оружие, инвентарь, ограничения) |
| `docs/TASKS.md` | Техническая документация, задачи 1.1–1.4, архитектура |
| `docs/BALANCE_TUNING_PANEL.md` | Описание F8-панели для заказчика |
| `docs/PHASE1_SETUP.md` … `PHASE4_SETUP.md` | Auto-setup в Unity |
| `docs/README.md` | Оглавление документации |
