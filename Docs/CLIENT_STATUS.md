# Статус для заказчика (Shooter — персонаж и FPS-тело)

**Дата:** август 2026  
**Стек на текущем этапе:** только **Character Controller Pro (CCP)** + **FPS Animation Framework (FPS AF)**.  
Motion Warping (mantle/vault) — **не в scope** до отдельного согласования.

---

## Что уже сделано

### Инфраструктура проекта
- Unity-проект (URP, Input System)
- Документация и план фаз: `Docs/TASKS.md`
- Авто-setup через меню **Shooter → Phase 0–4** в редакторе

### Фаза 0–1 — Движение (CCP)
- Персонаж на базе **Demo Character 3D** + модель **Character_model**
- Префаб `PlayerCharacter`, тестовая сцена `PlayerTest` (пол, склон)
- **ShooterInputHandler** — связка Unity Input System → CCP (WASD, прыжок, бег, присед, Interact)
- CCP: ходьба, бег, прыжок, движение по склону

### Фаза 2–3 — FPS-тело (FPS Animation Framework)
- На модели: **FPSAnimator**, Rig, Animator Profile, IK-цели
- **FPSAnimator_Humanoid** — locomotion (ходьба/бег/стрейф)
- FPS-камера на голове, процедурные слои: **Turn, Look, Sway, IK**
- **ShooterCharacterController** — мост CCP ↔ FPS AF (input, поворот, параметры аниматора)
- Модель переведена на **Humanoid** Avatar (обязательно для demo-анимаций)

### Фаза 4 — Лестницы (CCP, в работе / setup готов)
- Editor: **Shooter → Phase 4 → Run Full Phase 4 Setup**
- CCP state **LadderClimbing** + тестовая лестница в сцене
- **ShooterLadderFpsBridge** — на время лазания отключает FPS sway/turn, после — восстанавливает locomotion

---

## Что работает в Play Mode (сейчас)

| Функция | Статус |
|---------|--------|
| WASD + мышь (FPS-обзор) | ✅ |
| Бег (Shift), присед (C), прыжок (Space) | ✅ (прыжок без отдельной anim — в плане полировки) |
| Locomotion анимации (ходьба/бег/стрейф) | ✅ (Humanoid) |
| Процедурное тело (sway / turn) | ✅ базово, полировка позже |
| Лестница (Interact + climb) | 🔧 после Phase 4 setup |
| Оружие | ❌ следующий этап (Задача 2) |
| Mantle / Vault (Motion Warping) | ❌ **вне scope** на данном этапе |

---

## Следующие шаги (план)

1. **Полировка** Phase 3: прыжок/приземление, crouch, чувствительность камеры
2. **Phase 4:** тест лестницы в `PlayerTest`
3. **Задача 2 — оружие:** Weapon Layer, ADS, recoil, стрельба (FPS AF)
4. **Motion Warping (mantle/vault)** — только после отдельного согласования; на текущем этапе используем **только CCP + FPS AF**

---

## Краткий текст для сообщения заказчику

> Реализован базовый FPS-персонаж: движение через Character Controller Pro, процедурное тело и locomotion-анимации через FPS Animation Framework. Есть тестовая сцена, auto-setup в Unity. Сейчас подключаем лестницы (CCP). Оружие — следующий этап. Motion Warping (перепрыгивание/перелезание) на этом этапе не используем — работаем только с CCP и FPS Animator Framework, как договорились.
