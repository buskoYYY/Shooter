# Документация проекта Shooter

Формат: каждая новая задача получает заголовок, список того, что нужно сделать, и список того, что уже сделано.  
Обновляется по мере работы.

---

## Справка: Как устроен FPS Animation Framework

### Зачем вообще эта сложность?

**Обычный Animator Controller** отлично проигрывает заранее записанные клипы (Idle, Walk, Run).  
**FPS Animation Framework** нужен, когда одних клипов мало — в FPS с видимым телом каждый кадр нужно *досчитывать* позу поверх базовой анимации.

#### Что делает обычный Animator

```
Idle клип → кости двигаются по ключам → готово
```

Персонаж стоит в одном Idle, пока не переключишь state. Для third person или FPS **без тела** (только руки) этого часто хватает.

#### Что ломается в FPS с полным телом

| Проблема | Почему Idle/Walk не решают |
|----------|---------------------------|
| Смотришь вверх/вниз | Нужно наклонить позвоночник, голову, плечи — на каждый градус мыши |
| Смотришь влево/вправо | Бёдра и торс должны догонять камеру с задержкой (Turn Layer) |
| Идёшь по склону | Ноги должны вставать на наклонную — IK |
| Бежишь, дёргаешь мышью | Тело слегка качается (Sway) — это не отдельный клип на каждый угол |
| Перезарядка / mantle | Нужно наложить клип *поверх* текущей позы, не сбрасывая всё |

Записать отдельный клип на каждую комбинацию — combinatorial explosion. Процедурные слои решают это **математикой в runtime**, а не сотнями клипов.

#### Когда система НЕ нужна

- Вид от первого лица **только руки + оружие** (классический FPS)
- Простой third person: Idle / Walk / Run / Jump — и всё
- Нет процедурного sway, IK, поворота тела от камеры

#### Когда система НУЖНА (наш случай)

CCP двигает капсулу, а тело должно выглядеть живым в FPS — поворот, IK ног, sway, look. FPS AF **не заменяет** анимации, а **дополняет** их:

```
Базовые клипы (Idle, Walk, Run)     ← обычный Animator
        ↓
Процедурные слои (Look, Turn, IK…)  ← FPS AF поверх, каждый кадр
        ↓
Итоговая поза костей
```

> **Idle/Walk/Run** — «что делает персонаж».  
> **FPS AF** — «как тело реагирует на камеру, рельеф и движение в каждый кадр».

---

### Wizard: первичная настройка персонажа

`ПКМ по герою → FPS ANIMATOR Wizard`

| Что создаётся | Зачем |
|---------------|-------|
| **Компоненты на игроке** | `FPSAnimator`, `FPSBoneController`, `UserInputController`, `FPSPlayablesController`, `RecoilAnimation` |
| **Rig asset** (ScriptableObject) | «Паспорт» скелета: иерархия костей, chains (руки, ноги, pelvis), curves |
| **InputConfig** | Мост между **нашим кодом** (CCP, input) и **фреймворком**. Пишем `_userInput.SetValue("MouseDelta", ...)` — слои читают это |
| **Камера + FPSCameraController** | FPS-камера (FOV, shake и т.д.) |
| **IK targets** (виртуальные кости) | Точки для IK рук/ног |

**InputConfig** из пакета — это **не** Unity Input System. Это **внутренний словарь свойств** фреймворка (мышь, движение, aiming, weights слоёв).  
Путь: `Assets/KINEMATION/FPSAnimationFramework/Assets/InputConfig_FPSAnimationFramework.asset`

---

### Profile Wizard: процедурные фичи

`ПКМ по Rig asset → FPS PROFILE Wizard`

Создаёт **Animator Profile** — набор процедурных слоёв. Profile **ссылается на Rig** — слои знают, *какие кости* крутить.

```
AnimatorProfile
├── PoseSamplerLayer   — базовая поза pelvis/spine
├── ViewLayer          — наклон «вида» при движении
├── TurnLayer          — поворот бёдер относительно камеры
├── LookLayer          — наклон костей при взгляде вверх/вниз/в стороны
├── IkLayer            — ноги на склонах, руки
├── SwayLayer          — качание при движении
├── AdditiveLayer      — доп. наложения
├── IkMotionLayer      — IK при root motion анимациях
└── … (ADS, Weapon — позже, в Задаче 2)
```

#### Look Layer

Берёт **mouse input** и **раскладывает угол по цепочке костей** (spine → chest → neck → head). У каждой кости — свой лимит (`clampedAngle`):

- `pitchOffsetElements` — вверх/вниз
- `yawOffsetElements` — влево/вправо
- `rollOffsetElements` — наклон

Без этого при взгляде вверх голова уйдёт в плечи, или всё будет крутиться одной костью.

---

### Animation Asset vs Profile vs Animator Controller

| Ассет | Что хранит |
|-------|-----------|
| **Animator Controller** | Locomotion: Idle, Walk, Run, Jump (state machine) |
| **Animator Profile** | Настройки процедурных слоёв (Look, Turn, IK…) |
| **Animation Asset** | Клипы для *динамических* анимаций (перезарядка, throw, mantle overlay). Играется через `PlayablesController.PlayAnimation()` |
| **Rig asset** | Информация о скелете + ссылка на Animator Controller и InputConfig |

Profile **не хранит** locomotion-клипы — только настройки «надстройки» поверх них.

---

### Связка с CCP (как будет работать у нас)

```
Input
  ↓
CharacterBrain (CCP)  →  движение капсулы, velocity
  ↓                           ↓
Animator params          UserInputController (FPS AF)
(PlanarSpeed, Grounded)  (MouseDelta, MoveInput, weights)
  ↓                           ↓
Walk/Run клипы           Look, Turn, IK, Sway
  ↓                           ↓
       ═══ итоговая поза костей ═══
```

CCP отвечает за **где** персонаж. FPS AF — за **как выглядит** тело.

---

## Задача 1 — Полное тело + FPS-движение (CCP + FPS AF + Motion Warping)

**Цель:** Character Controller Pro — физика и логика движения. FPS Animation Framework — процедурное тело (поворот, IK, sway). Motion Warping — интеракты и карабканье на препятствия. Вид от первого лица. Оружие — позже.

### Архитектура (как это стыкуется)

```
[Input System]
      ↓
[CharacterBrain (CCP)] ──→ CharacterActions (move, jump, interact…)
      ↓
[CharacterStateController (CCP)]
   ├── NormalMovement  → ходьба, бег, прыжок, слоупы
   ├── LadderClimbing  → лестницы (root motion + IK)
   └── (будущее) MantleState → Motion Warping (mantle/vault)
      ↓
[CharacterActor (CCP)] — позиция, velocity, root motion
      ↓
[Animator] ← параметры из NormalMovement (PlanarSpeed, Grounded…)
      ↓
[FPSAnimator + Layers (FPS AF)] — Turn, View, IK, Sway, Look…
      ↓
[FPSCameraController] — FPS-камера, pitch/yaw
      ↓
[MotionWarping (LateUpdate)] — warp root motion на препятствиях
```

**Иерархия персонажа (CCP):**
```
Player (Root)
├── CharacterBody + CharacterActor
├── FPSAnimator, UserInputController, CharacterBrain…
├── Graphics/
│   └── Character_model (Animator, KRigComponent, MotionWarping)
├── States/  (NormalMovement, LadderClimbing…)
├── Actions/ (CharacterBrain)
└── Environment/
```

---

### План работ

#### Фаза 0 — Подготовка (≈30 мин)
- [x] Импортировать **Character Controller Pro** (`Assets/Character Controller Pro/`)
- [x] Импортировать **FPS Animation Framework** (`Assets/KINEMATION/FPSAnimationFramework/`)
- [x] Импортировать **Motion Warping** (`Assets/KINEMATION/MotionWarping/`)
- [x] Добавить модель персонажа (`Assets/_Project/Packages/Models/Character_model.fbx`)
- [x] Скачан **Demo Content FPS AF** → `Assets/_Project/Downloads/FPSAnimationFramework_Demo.unitypackage` *(импорт в Unity: Shooter → Phase 0)*
- [ ] **Motion Warping Demo** — отдельного package в releases нет; будет на Фазе 5
- [ ] Изучить демо-сцены CCP: `Demo/Scenes/3D Scene.unity`, `Character Scene.unity`

#### Фаза 1 — Базовый персонаж CCP (≈1–2 ч)
> Документация: [Organize the character hierarchy](https://lightbug14.gitbook.io/ccp/how-to.../implementation/organize-the-character-hierarchy.md)  
> **Авто-setup:** `Shooter → Phase 1 → Run Full Setup` (см. `Assets/_Project/PHASE1_SETUP.md`)

- [x] Скрипт **ShooterInputHandler** — мост Unity Input System → CCP
- [x] Editor-меню **Shooter/Phase 1** — создание префаба и тестовой сцены
- [ ] Запустить setup в Unity и проверить префаб `Assets/_Project/Prefabs/PlayerCharacter.prefab`
- [ ] Создать префаб на основе `Demo Character 3D`, заменить меш на `Character_model`
- [ ] Сохранить иерархию: Root → Graphics / States / Actions / Environment
- [ ] Настроить `CharacterBody` (капсула под рост модели)
- [x] Подключить Input System к `CharacterBrain` (Custom + ShooterInputHandler)
- [ ] Проверить `NormalMovement`: ходьба, бег, прыжок, приземление, слоупы
- [ ] Убедиться, что аниматор получает параметры: `Grounded`, `PlanarSpeed`, `VerticalSpeed`, `HorizontalAxis`, `VerticalAxis`

#### Фаза 2 — FPS Animation Framework: настройка тела (≈2–3 ч)
> Документация: [Character Rig](https://kinemation.gitbook.io/scriptable-animation-system/workflow/character-rig.md) → [Profiles and Layers](https://kinemation.gitbook.io/scriptable-animation-system/workflow/profiles-and-layers.md)  
> **Авто-setup:** `Shooter → Phase 2 → Run Full Phase 2 Setup` (см. `Assets/_Project/PHASE2_SETUP.md`)

- [x] Editor-меню **Shooter/Phase 2** — Rig, Profile, IK, FPS-компоненты, камера
- [x] **ShooterCharacterController** — мост CCP ↔ FPS AF (input, yaw, animator sync)
- [ ] Запустить setup в Unity и проверить Play в `PlayerTest`
- [ ] Проверить кости: Root, Head, Pelvis, Spine Root, Hands, Feet
- [ ] Animator Controller — `FPSAnimator_Humanoid` из демо
- [ ] Input Config: `Assets/KINEMATION/FPSAnimationFramework/Assets/InputConfig_FPSAnimationFramework.asset`
- [ ] Слои Profile (Pose Sampler, View, Turn, Look, IK, Sway, Ik Motion, Additive)
- [ ] Камера: `FPSCameraController` на голове, third-person камера сцены отключена
- [ ] FPS-режим: тело видно, sway/turn/IK на склоне

#### Фаза 3 — Мост CCP ↔ FPS AF (≈2–3 ч) ⭐ ключевая фаза
> Документация: [Integration](https://kinemation.gitbook.io/scriptable-animation-system/workflow/integration.md)  
> **Базовый мост уже в `ShooterCharacterController` (Phase 2). Фаза 3 — полировка и edge cases.**

- [x] Создать `ShooterCharacterController.cs`
- [x] Инициализация FPSAnimator, UserInputController
- [x] Прокинуть mouse delta / move input → `UserInputController`
- [x] FPS-поворот: yaw на корне, pitch через FPS AF
- [x] CCP `changeLookingDirection = false`, External Reference → корень игрока
- [x] При спринте: `StabilizationWeight = 0`, `PlayablesWeight = 0`
- [ ] Проверить в Play: движение + поворот тела + sway + IK ног на склоне
- [ ] Полировка: прыжок/приземление, crouch transitions, чувствительность мыши

#### Фаза 4 — Лестницы через CCP (≈1–2 ч)
> CCP Demo: `LadderClimbing.cs` — готовый state с root motion + IK

- [ ] Добавить `Ladder` компоненты на лестницы в тестовой сцене
- [ ] Добавить state `LadderClimbing` в `CharacterStateController`
- [ ] Настроить Animator Controller: триггеры `BottomUp`, `TopDown`, `Up`, `Down`, `BottomDown`, `TopUp`
- [ ] Root motion: `CharacterActor.SetUpRootMotion(true, SetVelocity, false)` — CCP делает сам
- [ ] IK на лестнице: `useIKOffsetValues` в LadderClimbing или IK Layer FPS AF
- [ ] При входе на лестницу: отключить Turn Layer / Sway (через UserInputController weights)
- [ ] При выходе: восстановить веса слоёв
- [ ] Тест: подойти → Interact → карабкаться вверх/вниз → слезть

#### Фаза 5 — Motion Warping: mantle/vault (≈2–3 ч)
> Документация: [How this asset works](https://kinemation.gitbook.io/motion-warping-for-unity/concept/how-this-asset-works.md)

- [ ] Добавить `MotionWarping` + `MotionWarpingIk` на персонажа (Graphics)
- [ ] Импортировать demo MotionWarpingAsset (mantle high/low, vault)
- [ ] Добавить `MantleComponent` / `VaultComponent` на препятствия или через raycast
- [ ] Создать CCP state `MotionWarpState`:
  - Detect obstacle → call `MotionWarping.Play(asset, warpPoints)`
  - `CharacterActor.IsKinematic = true`, `UseRootMotion = true`
  - Motion Warping двигает root в `LateUpdate`
  - По `onWarpEnded` → вернуть NormalMovement
- [ ] ⚠️ Motion Warping ожидает `CharacterController`/`Rigidbody` — CCP использует свой `CharacterBody`. Нужен адаптер: warp двигает `CharacterActor.Position` напрямую
- [ ] Animator parameter `WarpRate` — для play rate scale
- [ ] Тест: подбежать к низкому препятствию → mantle, к высокому → vault

#### Фаза 6 — Тестовая сцена и полировка (≈1–2 ч)
- [ ] Сцена: плоскость + слoп + лестница + 2–3 препятствия для mantle
- [ ] Прогнать все режимы: walk, run, jump, slope, ladder, mantle
- [ ] Проверить артефакты: twisted feet, clipping, camera jitter
- [ ] Зафиксировать баги → отложить оружие на Задачу 2

---

### Что уже сделано

- [x] Unity-проект Shooter (URP 17.3, Input System 1.18)
- [x] **Character Controller Pro** — импортирован (`Assets/Character Controller Pro/`)
- [x] **FPS Animation Framework** — импортирован (`Assets/KINEMATION/FPSAnimationFramework/`)
- [x] **Motion Warping** — импортирован (`Assets/KINEMATION/MotionWarping/`)
- [x] Модель персонажа — `Character_model.fbx`
- [x] **ShooterInputHandler** + Editor setup (`Assets/_Project/Scripts/`, `Assets/_Project/Editor/`)
- [x] FPS AF Demo package скачан в `Assets/_Project/Downloads/`
- [ ] Demo Content FPS AF — **импортировать в Unity** (Phase 0 menu)
- [ ] Префаб `PlayerCharacter` — **создать через Run Full Setup**
- [ ] Тестовая сцена `PlayerTest` — **создать через Run Full Setup**
- [ ] Play Mode тест движения — **не проверен**

### Документация ассетов

| Ассет | Онлайн | Локально |
|-------|--------|----------|
| Character Controller Pro | [User Manual](https://lightbug14.gitbook.io/ccp/) · [API](https://lightbug14.github.io/lightbug-web/character-controller-pro/Documentation/html/index.html) | `Assets/Character Controller Pro/Documentation/` |
| FPS Animation Framework | [GitBook](https://kinemation.gitbook.io/scriptable-animation-system/) | `Offline Documentation.pdf` |
| Motion Warping | [GitBook](https://kinemation.gitbook.io/motion-warping-for-unity/) | `Offline Documentation.pdf` |

### Риски и нюансы

1. **CCP vs Motion Warping** — MW заточен под Unity CharacterController/Rigidbody; CCP использует свой CharacterBody. Нужен адаптер, который двигает `CharacterActor.Position` в LateUpdate.
2. **FPS-поворот vs CCP-поворот** — в FPS камера задаёт направление, а не движение. Нужно отключить auto-rotation CCP и передать yaw камеры в Turn Layer.
3. **Root motion конфликт** — и CCP LadderClimbing, и Motion Warping используют root motion. Важно не включать оба одновременно.
4. **Retarget анимаций** — demo FPS AF использует Mixamo-скелет; `Character_model` может иметь другую иерархию костей. Wizard поможет, но анимации могут потребовать retarget.
5. **Execution Order** — CCP (FixedUpdate) → Animator → FPS AF layers → Motion Warping (LateUpdate).

### Следующая задача (запланировано)

**Задача 2 — Оружие:** Weapon Layer, Attach Hand, ADS, Recoil, перезарядка, стрельба.

---

*Последнее обновление: 6 августа 2026 — Фаза 0–1 (код + setup menu)*
