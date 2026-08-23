# Статус для заказчика (Shooter — персонаж и FPS-тело)

**Дата:** август 2026  
**Стек на текущем этапе:** **Character Controller Pro (CCP)** + **FPS Animation Framework (FPS AF)**.  
Motion Warping (mantle/vault) — **не в scope** до отдельного согласования.

**Этап движения:** ✅ **завершён** (locomotion, прыжок, лестница, полировка). Следующий крупный блок — **оружие (Задача 2)**.

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
- **ShooterHandPoseState** — unarmed/armed по **T**, старт с опущенными руками без рывка
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
| Unarmed / armed (T), старт unarmed | ✅ |
| Процедурное тело (sway / turn / look / IK) | ✅ |
| Разворот на месте → ходьба | ✅ |
| Лестница (Interact + climb + exit) | ✅ |
| F8 — временная панель баланса | ✅ |
| Оружие (стрельба, ADS, reload) | ❌ **Задача 2** |
| Mantle / Vault (Motion Warping) | ❌ **вне scope** |

---

## Следующие шаги (план)

1. **Задача 2 — оружие:** Weapon Layer, Attach Hand, ADS, recoil, перезарядка, стрельба (FPS AF)
2. Тестовая сцена / полировка окружения (Фаза 6) — по необходимости параллельно
3. **Motion Warping (mantle/vault)** — только после отдельного согласования

---

## Краткий текст для сообщения заказчику

> Реализован FPS-персонаж уровня полноценного locomotion-блока: движение через Character Controller Pro, процедурное тело и анимации через FPS Animation Framework. Работают ходьба, бег, присед, прыжок с пружиной, лестницы, unarmed/armed, разворот на месте и плавные переходы. Есть тестовая сцена и dev-панель F8 для подстройки баланса. **Блок движения закрыт** — следующий этап: **оружие**. Motion Warping (перепрыгивание/перелезание) на этом этапе не используем.

---

## Связанные документы

| Документ | Содержание |
|----------|------------|
| `docs/TASKS.md` | Техническая документация, задачи 1.1–1.4, архитектура |
| `docs/BALANCE_TUNING_PANEL.md` | Описание F8-панели для заказчика |
| `docs/PHASE1_SETUP.md` … `PHASE4_SETUP.md` | Auto-setup в Unity |
| `docs/README.md` | Оглавление документации |
