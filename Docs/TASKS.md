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

### Три слоя позы (руки — не «просто Animator»)

Руки в кадре складываются из **трёх систем сразу**. Если чинить только одну — проблема уезжает в другую.

| Слой | Что это | Что пишет руки |
|------|---------|----------------|
| **1. Animator / locomotion** | `FPSAnimator_Humanoid` (+ опциональный unarmed override) | Idle / run / sprint клипы. Rifle-клипы держат руки «как с АКМ», даже если overlay уже unarmed. |
| **2. Playables overlay** | `FPSPlayablesController` + `PlayPose` | Armed / unarmed поза, equip / unequip. Вес: `PlayablesWeight`. |
| **3. PoseSampler + IK** | слой в Animator Profile | При init **всегда** `PlayPose(poseToSample)`. `defaultWeaponPose` — винтовочный hold. IK WeaponBone тянет руки к этой кости. |

**Два разных «веса» — их нельзя путать:**

| Параметр | За что отвечает | Unarmed | Armed |
|----------|-----------------|---------|-------|
| `LookLayerWeight` / `StabilizationWeight` | осанка, наклон торса от взгляда (F9 = старая сутулость) | Look **0.3**, Stab **0** | Look **1**, Stab **0** |
| `FullBodyWeight` / `PlayablesWeight` | тело из locomotion vs overlay рук | FBW **1** → Playables **0** | FBW **0** → Playables **1** |

`PlayablesWeight` в коде = `1 - FullBodyWeight`.  
Rifle **sprint-клип** сам пишет `FullBodyWeight = 0` → на кадр включает overlay. Если в overlay-миксере ещё armed-поза — в первом спринте торчат винтовочные руки.

F9 (`UseLegacySlouchedPosture`) — **static-флаг**. Без domain reload он переживает Stop/Play и стартует игру в сутулой «винтовочной» осанке, хотя стейт уже unarmed.

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
> **Авто-setup:** `Shooter → Phase 4 → Run Full Phase 4 Setup` (см. `Assets/_Project/PHASE4_SETUP.md`)

- [x] **ShooterLadderFpsBridge** — отключение Turn/Sway/Look на лестнице, restore FPS controller после
- [x] Editor-меню **Shooter/Phase 4** — LadderClimbing state + TestLadder в сцене
- [ ] Запустить setup в Unity и проверить Interact → climb → exit
- [ ] NormalMovement: `overrideAnimatorController = false` (FPS Humanoid не ломается)
- [ ] Ladder state: `LadderClimbing.controller` при лазании (root motion CCP)
- [ ] Тест: подойти → E → W/S → E слезть

#### Фаза 5 — Motion Warping: mantle/vault (≈2–3 ч) ⏸️ ОТЛОЖЕНО
> **Scope заказчика:** на данном этапе только **CCP + FPS AF**. Motion Warping — после отдельного согласования.

- [ ] (отложено) Motion Warping + адаптер под CCP CharacterBody
- [ ] (отложено) Mantle/vault на препятствиях

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
- [x] Модель персонажа — `Character_model.fbx` (Humanoid)
- [x] **ShooterInputHandler** + Editor setup (`Assets/_Project/Scripts/`, `Assets/_Project/Editor/`)
- [x] Префаб `PlayerCharacter`, сцена `PlayerTest`
- [x] **ShooterCharacterController** — мост CCP ↔ FPS AF
- [x] **ShooterHandPoseState** — T armed/unarmed; старт unarmed без рывка рук (см. Задачу 1.1)
- [x] **ShooterLadderFpsBridge** — лестницы
- [x] Play Mode: WASD, мышь, бег, присед, прыжок, первый спринт без вспышки винтовки

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
6. **KINEMATION из коробки — armed.** Unarmed у нас не отдельный режим пакета, а переключение поверх винтовки (`ShooterHandPoseState`). Чинить «только старт» или «только спринт» по отдельности нельзя — это один mixer.
7. **`[DefaultExecutionOrder]` на скрипте Unity игнорирует**, если в `.meta` другой `executionOrder`. У `ShooterHandPoseState` в meta долго стояло **0**, поэтому FPS init (`ShooterCharacterController` = **-200**) всегда успевал поднять armed overlay до нашего `Start`.
8. **Не выключать `Animator.enabled`**, чтобы «спрятать» руки. `FPSAnimator.Update` при обратном включении вызывает `RebuildPlayables()` → PoseSampler снова `PlayPose` → вспышка рук. `animator.speed = 0` PlayableGraph не останавливает. Прятать меши — мигание камеры (кадр как до Play).

### Следующая задача (запланировано)

**Задача 2 — Оружие:** Weapon Layer, Attach Hand, ADS, Recoil, перезарядка, стрельба.

---

## Задача 1.3 — Прыжок: без стрейфа в воздухе + пружина

**Цель:** в воздухе нельзя докручивать направление WASD. Прыжок с короткой «пружиной» (присед → отрыв), не мгновенный импульс.

### Что нужно сделать

- [x] Обнулить CCP `notGroundedAcceleration` / `notGroundedDeceleration` — planar velocity как при отрыве
- [x] Задержка ~0.14 с перед импульсом; сразу играть JumpStart + crouch IK
- [x] Не ломать crouch-jump-down и лестницу

### Что уже сделано

- [x] `ShooterCcpMovementTuning` — air accel/decel = 0
- [x] `ShooterJumpWindup` + `ShooterInputHandler` — Space → wind-up → Jump в CCP
- [x] F8: **Crouch delay**

---

## Задача 1.2 — Камера на лестнице (без взгляда в стену и дрожи)

**Цель:** вход на лестницу не дёргает камеру в стену. Во время лазания камера не трясётся от анимации головы.

### Что нужно сделать

- [x] Плавно доворачивать камеру к лестнице, без мгновенного pitch/yaw snap
- [x] Смотреть чуть вверх по перекладинам, а не в упор в стену
- [x] Отвязать камеру от кости головы на время climb — позиция от капсулы, с демпфом
- [x] После слезания вернуть камеру к голове без резкого скачка

### Что уже сделано

- [x] `ShooterFpsCameraApply` — ladder-камера в голове (не на капсуле); yaw мгновенный; оффсет вперёд убран (из‑за него была видна куртка)
- [x] `ShooterFpsHeadHide` — на лестнице прячет `Jacket1` / `Backpack2`, иначе entry-анимация показывает грудь
- [x] Плавный взгляд в стену: approach крутит yaw от текущего forward (больше не snap на Interact); pitch blend ~0.4 с
- [x] `ShooterCharacterController` — pitch к лестнице blend’ится; мышь вверх/вниз по-прежнему работает; yaw на лестнице не конфликтует с approach
- [x] `ShooterLadderFpsBridge` — больше не сбрасывает pitch в 0 и не форсит камеру в момент входа

Код: `Assets/_Project/Scripts/Character/ShooterFpsCameraApply.cs`.

---

## Задача 1.1 — Unarmed как визуальный старт (без ломания спринта)

**Цель:** игра начинается с опущенными руками, без анимации «снял винтовку». Первый спринт / прыжок / атака не должны вспыхивать armed-позой.

**Почему это было сложно:** пакет стартует как **armed** (overlay `PlayablesWeight = 1`, `FullBodyWeight = 0`, PoseSampler `PlayPose`, `defaultWeaponPose` = rifle hold). Настоящий **T** чинит спринт, потому что кладёт unarmed-позу в overlay-mixer и делает `ForceOverlayPoseFullWeight`. Цена — на старте виден blend/рывок рук.

### Что нужно сделать

- [x] Переключение armed / unarmed по **T** (`ShooterHandPoseState`)
- [x] Старт визуально unarmed, без unequip-анимации
- [x] Первый спринт без винтовочных рук (тот же resync, что у T)
- [x] Не ломать прыжок, атаку, look/осанку
- [ ] На префабе по желанию прописать `unarmedLocomotionOverride` — сейчас поле часто пустое, `ApplyLocomotionController` no-op; не трогать, пока спринт ок

### Что уже сделано

- [x] `ShooterHandPoseState` — overlay, equip/unequip, locomotion swap, TurnInPlace
- [x] `startUnarmed` на префабе
- [x] Старт: внутренне как нажатие **T** (`SimulateToggleHandPosePress`), но overlay **snap** (`_snapStartOverlay`), без blend ~0.45 с
- [x] Look: unarmed 0.3 / armed 1.0; Stabilization 0; F9 сбрасывается (не залипает между Play)
- [x] `PlayablesWeight = 1 - FullBodyWeight`; в unarmed скрипт держит FBW = 1, в прыжке тоже 1

### Проблемы, с которыми столкнулись, и как решили

Круг был один: **чиним старт unarmed → в первом спринте торчат руки; чиним спринт фейковым T → на старте рывок/unequip.** Стопка патчей (FBW, override, freeze Animator, прятать меши) ломала ноги/камеру/атаку. Рабочая база — фейковый T; последняя правка только убрала **видимый blend**.

| Симптом | Почему так | Что не сработало | Что сработало |
|---------|------------|------------------|---------------|
| Спавн как armed, потом «снял оружие» | `ShooterCharacterController.Awake` (−200) вызывает `FPSAnimator.Initialize()` → PoseSampler `PlayPose`. `HandPose.Start` (meta order **0**) потом имитирует **T** с blend 0.45 с | Ставить `_isUnarmed` в Awake и вызывать `SetHandPose(true)` — early-out, resync mixer не происходит | Оставить внутренний путь **T** (`_isUnarmed` сначала false), не делать early-out |
| Рывок рук в начале | `PlayPose(unarmed overlay)` с `blendInTime` 0.45–0.5 с: mixer едет из rifle idle в руки вниз. Клип overlay — `Unarmed_Idle`, не unequip; выглядит как убирание оружия | `animator.enabled = false` на 2 с; `speed = 0`; прятать рендереры; `Play("Standing")`; нулить PlayablesWeight | Тот же coroutine, что у T (`ClearSlot` → кадр → `ClearSlot` → snap → `ForceOverlayPoseFullWeight`), но **без** `ApplyOverlayBlend`. Флаг `_snapStartOverlay` |
| Первый спринт — руки «как с АКМ», второй раз нормально | Sprint-клип пишет `FullBodyWeight = 0` → `PlayablesWeight = 1`. Overlay включается. Если mixer ещё с init-позой armed — вспышка. После настоящего T в mixer уже unarmed | Жёстко `PlayablesWeight = 0` в unarmed (ломает T); смена locomotion override на префабе (носки при атаке); skip fake T | Не пропускать resync T. Snap на старте **после** тех же `ClearSlot` + `ForceOverlay`, что и у клавиши T |
| Поза «как F9» на старте / в конце бега | F9 = `LookLayerWeight`/`StabilizationWeight` = 1 (static). `stopMotion` — IK на WeaponBone при остановке | Крутить offset камеры (`ShooterFpsCameraApply`) — на armed-кадр не влияет | Look 0.3 unarmed / 1 armed; Stab 0; в unarmed не запускать `stopMotion`; сбрасывать F9 при Play |
| Руки на мгновение после включения Animator | `FPSAnimator.Update`: animator был выкл → вкл → **`RebuildPlayables()`** → PoseSampler `Initialize` → снова `PlayPose` с blend. Graph живёт отдельно от `Animator.enabled` | Считать, что Animator «запомнил клип» | Не выключать Animator. Freeze/hide не использовать для старта |
| Мигание камеры | Скрытие mesh на старте: первый кадр как Scene view / pre-Play camera | — | Не трогать рендереры и `ShooterFpsCameraApply` ради рук |
| Носки при атаке + рывок рук | Подключили `FPSAnimator_Unarmed_Humanoid.overrideController`; Standing/sprint мапы пересеклись с rifle-клипами | Чинить старт через замену locomotion controller | Откат. Override на префабе **не** заполнять, пока нет отдельной задачи |
| Instant `SetHandPose(true)` без coroutine | Snap в `Start` раньше, чем PoseSampler доигрывает init-blend; либо `_isUnarmed` уже true → early-out. Mixer не фиксирует unarmed | Одна строка `instant: true` без `ClearSlot`/ожидания кадра | Snap **внутри** того же `TransitionToPose`, с yield на кадр и повторным `ForceOverlay` |

**Итог (август 2026):** не делать unarmed «настоящей базой KINEMATION» и не выключать анимацию. Внутри старт = переход armed → unarmed как **T**, визуально = мгновенный overlay snap. Прыжок этот путь не трогает.

Код: `Assets/_Project/Scripts/Character/ShooterHandPoseState.cs` (`Start`, `_snapStartOverlay`, `TransitionToPose`). Веса: `ShooterCharacterController.UpdatePlayablesWeight` / `ApplyFpsLayerWeights`.

---

*Последнее обновление: 23 августа 2026 — прыжок: без air-strafe, crouch wind-up ~0.14 с*
