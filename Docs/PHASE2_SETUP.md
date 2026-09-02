# Phase 2 Setup — FPS Animation Framework

## Быстрый старт

1. Убедись, что **Phase 0** и **Phase 1** выполнены (demo content + префаб + сцена).
2. В Unity: **Shooter → Phase 2 → Run Full Phase 2 Setup**
3. Открой `Assets/_Project/Scenes/PlayerTest.unity` и нажми **Play**.

## Что делает setup

- Добавляет на `Graphics/Character_model`:
  - `FPSAnimator`, `FPSBoneController`, `UserInputController`, `FPSPlayablesController`, `RecoilAnimation`
  - Animator Controller: `Assets/Demo/Animations/Locomotion/FPSAnimator_Humanoid.controller`
- Создаёт IK-цели и Rig/Profile в `Assets/_Project/FPS/`
- Ставит **FPS Camera** на кость `head` с `FPSCameraController` (в Play отвязывается на корень — см. [FPS_CAMERA_AND_HANDS.md](FPS_CAMERA_AND_HANDS.md))
- Добавляет **ShooterCharacterController** на корень игрока (мост CCP ↔ FPS AF)
- Отключает third-person камеру сцены, включает FPS-камеру
- CCP: `External Reference` → корень игрока, авто-поворот тела CCP выключен

## Управление

| Клавиша | Действие |
|---------|----------|
| WASD | Движение |
| Мышь | FPS-обзор (yaw на корне, pitch через FPS AF) |
| Space | Прыжок |
| Left Shift | Бег (снижает Sway/Stabilization) |
| C | Присед |

## Меню Phase 2

| Пункт | Когда использовать |
|-------|-------------------|
| **Setup FPS on Player Prefab** | Только обновить префаб без открытия сцены |
| **Apply FPS to Test Scene** | Настроить игрока в сцене + сохранить префаб |
| **Run Full Phase 2 Setup** | Рекомендуется — полный цикл |

## Если что-то не работает

**NullReference в PoseSamplerLayer**  
Нужен `poseToSample` в Profile. Запусти Phase 2 setup заново — назначается  
`Assets/Demo/Prefabs/Humanoid/AA_Rifle_OverlayPose_Humanoid.asset`.

**NullReference / ArgumentNullException (mask) в FPSPlayablesController**  
Запусти Phase 2 setup заново — нужен `UpperBody_Humanoid.mask` на `FPSPlayablesController`.  
Или вручную: `Character_model → FPS Playables Controller → Upper Body Mask` =  
`Assets/Demo/Animations/Masks/UpperBody_Humanoid.mask`

**NullReference в FPSBoneController**  
Запусти Phase 2 setup заново. Не включай FPS-компоненты вручную до назначения Profile/Rig.

**Персонаж не двигается**  
`Shooter → Phase 1 → Fix Movement Reference (current scene)`  
После Phase 2 reference должен быть **корень PlayerCharacter**, не Main Camera.

**Кости не найдены**  
Проверь скелет `Character_model`: нужны `root`, `pelvis`, `spine_01`, `head`, `hand_l/r`, `foot_l/r`.  
Старые ассеты в `Assets/Character_model_FPSAnimator/` можно удалить — Phase 2 создаёт свои в `_Project/FPS/`.

**Двойной поворот камеры**  
Убедись, что на корне есть `ShooterCharacterController`, а CCP `NormalMovement → Change Looking Direction` = **false**.
