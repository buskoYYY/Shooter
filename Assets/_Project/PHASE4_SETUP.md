# Phase 4 Setup — Ladder Climbing (CCP)

## Быстрый старт

1. **Shooter → Phase 4 → Run Full Phase 4 Setup**
2. Открой `PlayerTest` → **Play**
3. Подойди к лестнице справа от спавна → **E (Interact)**
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

## Если не работает

- Interact не срабатывает → проверь **ShooterInputHandler** и action **Interact** (E)
- Нет анимации лестницы → Avatar модели **Humanoid**
- После лестницы T-pose → **ShooterLadderFpsBridge** должен быть на корне игрока
