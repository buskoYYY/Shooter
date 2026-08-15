# Phase 4 Setup — Ladder Climbing (CCP)

## Быстрый старт

1. **Shooter → Phase 4 → Run Full Phase 4 Setup**
2. Открой `PlayerTest` → **Play**
3. Подойди к лестнице справа от спавна → **F (Interact)**
4. **W / S** или стрелки вверх/вниз — карабкаться, **E** — слезть

## Что делает setup

- Включает CCP state **LadderClimbing** на игроке
- **NormalMovement** больше не подменяет Animator (остаётся FPS Humanoid после лестницы)
- Добавляет **ShooterLadderFpsBridge** — отключает Turn/Sway/Look на время лазания
- Создаёт **TestLadder** в сцене (trigger + Top/Bottom references)

## Управление на лестнице

| Клавиша | Действие |
|---------|----------|
| E | Залезть / слезть |
| W / S | Вверх / вниз по лестнице |
| Space | Прыжок со лестницы (отталкивает назад) |

## Почему W / Space могли не работать

FPS setup ставит `applyRootMotion = false` и перехватывает Animator через Playables — CCP-лестница от этого ломается. **ShooterLadderFpsBridge** теперь:
- отключает FPS Playables на время лазания;
- двигает игрока по лестнице **напрямую** (W/S);
- обрабатывает **Space** и **E** (слезть).

## Если W не работает (старый чеклист)

1. **Shooter → Phase 4 → Setup Ladder on Player (current scene)** — на открытой сцене с игроком
2. Убедись, что на **States → LadderClimbing**:
   - компонент **включён**
   - **Override Animator Controller = ON** (анимация залезания/лезания)
3. На лестнице (**Ladder**): **Climbing Animations ≥ 1**, заданы **Top/Bottom Reference**
4. На игроке есть **ShooterLadderFpsBridge** — он отключает FPS-слои на время лазания (без этого root motion лестницы часто не работает)

## Если не работает

- Interact не срабатывает → проверь **ShooterInputHandler** и action **Interact** (F)
- Нет анимации лестницы → Avatar модели **Humanoid**
- После лестницы T-pose → **ShooterLadderFpsBridge** должен быть на корне игрока
