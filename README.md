# Shooter

FPS-проект на Unity (URP): персонаж с **Character Controller Pro** + **FPS Animation Framework**, вид от первого лица с полным телом.

## Быстрый старт

1. Открыть сцену `Assets/_Project/Scenes/PlayerTest.unity` или CCP demo `Assets/Character Controller Pro/Demo/Scenes/3D Scene.unity`.
2. Play — WASD, мышь, Shift (бег), C (присед), Space (прыжок), E (interact / лестница), T (unarmed/armed).
3. **F8** — dev-панель баланса (временная). **F9** — сравнение осанки.

## Документация

Вся проектная документация: **[docs/](docs/README.md)**

| Файл | Описание |
|------|----------|
| [docs/TASKS.md](docs/TASKS.md) | Архитектура, фазы, задачи 1.1–1.4 (движение) |
| [docs/CLIENT_STATUS.md](docs/CLIENT_STATUS.md) | Статус для заказчика |
| [docs/BALANCE_TUNING_PANEL.md](docs/BALANCE_TUNING_PANEL.md) | F8-панель |
| [docs/PHASE1_SETUP.md](docs/PHASE1_SETUP.md) | Auto-setup Phase 0–1 |
| [docs/PHASE2_SETUP.md](docs/PHASE2_SETUP.md) | Auto-setup Phase 2 |
| [docs/PHASE4_SETUP.md](docs/PHASE4_SETUP.md) | Auto-setup Phase 4 (лестницы) |

## Текущий статус

- **Движение:** ✅ locomotion, прыжок, лестница, turn-in-place, unarmed/armed
- **Оружие:** 🔜 Задача 2 (Weapon Layer, ADS, recoil, стрельба)
- **Motion Warping:** вне scope до отдельного согласования

## Стек

- Unity URP 17.x, Input System
- [Character Controller Pro](https://lightbug14.gitbook.io/ccp/)
- [FPS Animation Framework](https://kinemation.gitbook.io/scriptable-animation-system/) (KINEMATION)
