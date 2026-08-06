# Phase 0–1 Setup

## Быстрый старт в Unity

1. Открой проект в Unity и дождись компиляции скриптов.
2. **Shooter → Phase 0 → Import FPS AF Demo Content**  
   (файл уже лежит в `Assets/_Project/Downloads/FPSAnimationFramework_Demo.unitypackage`)
3. **Shooter → Phase 1 → Run Full Setup**  
   Создаст префаб `PlayerCharacter` и сцену `PlayerTest`.
4. Открой `Assets/_Project/Scenes/PlayerTest.unity` и нажми **Play**.

## Если персонаж прыгает, но не ходит

CCP в demo-режиме двигается **относительно камеры** (`Movement Reference = External`).  
Нужно назначить **External Reference → Main Camera**.

**Быстро:** `Shooter → Phase 1 → Fix Movement Reference (current scene)`

**Вручную:** Player → `States` → `Character State Controller` →  
`Movement Reference Parameters` → **External Reference** = Main Camera

---

## Управление

| Клавиша | Действие |
|---------|----------|
| WASD | Движение |
| Мышь | Камера |
| Space | Прыжок |
| Left Shift | Бег |
| C | Присед |

## Что делает setup

- Копирует `Demo Character 3D` (CCP) и подменяет модель на `Character_model`
- Подключает **ShooterInputHandler** → Unity Input System → CharacterBrain
- Отключает лишние demo-states (Dash, JetPack, Ledge…)
- Создаёт сцену: пол + склон + third-person камера для теста движения

## Motion Warping Demo

Отдельного unitypackage нет в releases. Demo-контент — в репозитории  
https://github.com/kinemation/motion-warping (понадобится на Фазе 5).
