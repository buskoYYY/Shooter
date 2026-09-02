# FPS-камера, culling и unarmed-руки

**Дата:** август 2026  
**Статус:** рабочее решение перенесено из `D:\Unity Games\Shooter\` (коммит `fa5b438` + локальные правки на диске).

Если «ломаются руки» или «камера крутится в Transform / culling мигает» — почти всегда затронуты **две разные системы**. Чинить только одну бессмысленно: симптом уедет в другую.

---

## Симптомы

| Что видишь | Вероятная причина |
|------------|------------------|
| В Game view руки в рукавах / грудь / перевёрнутый мир | Камера: offset не в look space или плохой override в сцене |
| В Inspector у **FPS Camera** «бегают» euler, culling мигает | Камера дочерняя к `head`, Animator крутит Transform |
| Руки в Game view норм, но поза idle — руки вверх / «сдаюсь» | `ShooterHandPoseState` старый: целиком меняет Animator Controller |
| Руки деревянные при ходьбе unarmed | `unarmedLocomotionOverride` не подключён на префабе |
| **Armed (T):** руки скручены, вспышка rifle-позы, «деревянный» idle | См. раздел **«Armed state — что ломает руки»** ниже |
| В консоли: *2 audio listeners* | Две камеры с `AudioListener` (сценовая + FPS) — на позу не влияет |

---

## Что помогло (кратко)

### 1. Камера — `ShooterFpsCameraApply`

База: коммит **`Detach FPS camera from head bone to stabilize culling`** (`fa5b438` в репо Shooter).

**Плюс обязательные правки поверх коммита** (были на диске Shooter, в git не всегда закоммичены):

1. **Отвязка от `head` в Play Mode** — камера живёт на **корне игрока** (`detachFromHeadAnimation = true`).
2. **LateUpdate (order 500)** — после KINEMATION выставляет world position + rotation (yaw тела + pitch мыши).
3. **Offset в look space**, не в осях тела:
   ```csharp
   rotation = bodyYaw * pitch;
   position = head.position + rotation * cameraLocalOffset;
   ```
   Иначе при взгляде вниз камера уезжает **в грудь / рукава**.
4. **`ClearCameraBoneBinding()`** в `Update()` — `FPSCameraController` не умножает rotation анимированной головы.
5. **`cameraLocalOffset.z = 0.1`** на префабе (не `0.04`).
6. **`ShooterCharacterController`**: fallback `GetComponentInChildren<FPSCameraController>` на **корне** — камера уже не под `Graphics/Character_model`.

**Файл:** `Assets/_Project/Scripts/Character/ShooterFpsCameraApply.cs`  
**Meta:** `executionOrder: 500`

### 2. Руки и поза — `ShooterHandPoseState`

На префабе включён **`unarmedLocomotionOverride`** → `Assets/_Project/FPS/FPSAnimator_Unarmed_Humanoid.overrideController`.

**Нельзя** просто делать:
```csharp
_animator.runtimeAnimatorController = unarmedLocomotionOverride; // ломает позу
```

**Нужна** версия скрипта с **`EnsureRuntimeLocomotionOverride()`**:
- создаётся runtime `AnimatorOverrideController` поверх базового Humanoid-контроллера;
- подменяются **только клипы** (unarmed jog / idle), база не сбрасывается;
- armed/unarmed переключаются через `ApplyOverrides`, без полной смены controller.

**Файл:** `Assets/_Project/Scripts/Character/ShooterHandPoseState.cs`  
**Префаб:** `PlayerCharacter` → `ShooterHandPoseState`:
- `armedLocomotionController` = `FPSAnimator_Humanoid`
- `unarmedLocomotionOverride` = `FPSAnimator_Unarmed_Humanoid`
- `startUnarmed: 1`

### 3. Armed state — что ломает руки (повторяющаяся ошибка)

KINEMATION **из коробки всегда armed**: overlay с винтовочной позой, rifle locomotion-клипы, IK на WeaponBone. Ломается это часто — обычно не потому что «сломался armed», а потому что **поверх armed наложили правку для unarmed или камеры** и задели общую цепочку.

**Типичные симптомы armed:**
- руки скручены / «сломанные» после **T** (unarmed → armed);
- на кадр вспыхивает rifle idle, потом оседает;
- в Game view руки в рукавах (часто путают с armed — это **камера**, см. §1);
- после правок unarmed idle «сдаюсь» — на самом деле сломан **Animator Controller**, armed тоже поедет.

| Что делают | Почему ломает armed |
|------------|---------------------|
| **`runtimeAnimatorController = unarmedLocomotionOverride`** (старый `ShooterHandPoseState`) | Сбрасывается базовый Humanoid-контроллер → ломаются **и** unarmed idle, **и** armed overlay / IK |
| **Locomotion remap сразу при T→armed** (до settle overlay) | Rifle-клипы включаются, пока в mixer ещё unarmed-поза → вспышка / скрученные руки |
| **`Play("Standing", 0)` при возврате в armed** | Быстрый restart idle → мигание rifle-позы |
| **Не вызывать `ClearSlotAnimations()`** между toggles | Старый slot/override motion **накапливается** → twisted hands при следующем armed |
| **Чинить «руки в рукавах» камерой на `head`** | Armed визуально «ломается» (рукава), плюс culling; откат ломает и то, что уже работало |
| **Offset камеры в body space или `y = -0.15` в сцене** | Камера в модели — кажется, что armed-руки не на месте |
| **Подключить `unarmedLocomotionOverride` без `EnsureRuntimeLocomotionOverride()`** | Fallback идёт в legacy swap controller → armed-переход через `ApplyLocomotionController(false)` становится нестабильным |

**Как должно быть (рабочий armed path):**

1. **T → armed:** сначала overlay blend / equip, **`ApplyLocomotionController(false)` только после** `ForceOverlayPoseFullWeight` (конец `TransitionToPose`).
2. Locomotion — **runtime** `AnimatorOverrideController.ApplyOverrides(_armedClipOverrides)`, не полная смена controller.
3. При armed — только **`ClearStuckInAirLocomotion()`**, без рестарта Standing.
4. Камера отдельно: look-space offset + detach от `head` — **не откатывать** «ради armed-рук».

**Быстрая проверка armed:** Play → **T** (armed) → стоять → спринт → снова idle. Руки держат винтовочную позу без скрутки и без вспышки на втором цикле.

### 4. Сцена `3D Scene` — опасные overrides

На инстансе префаба **не должно** быть:
- `cameraLocalOffset.y = -0.15` — камера внутри черепа
- `cameraLocalOffset.z = -0.02`

**Должно быть:** `cameraLocalOffset.z = 0.1` (или дефолт префаба).

Путь: `Assets/Character Controller Pro/Demo/Scenes/3D Scene.unity` → PlayerCharacter → Overrides → **Revert** плохие поля или править вручную.

### 5. Иерархия префаба

В **Edit Mode** на префабе **FPS Camera** может висеть под `head` — это нормально для setup.

В **Play Mode** после `ShooterFpsCameraApply`:
- **FPS Camera** → дочерняя **корню PlayerCharacter**, **не** к `head`
- **Camera Local Offset** править на **`Shooter Fps Camera Apply`**, не на Transform камеры в Play

---

## Чеклист после правок

1. **Stop → Play** (перезапуск обязателен).
2. Hierarchy: FPS Camera под корнем игрока.
3. Game view: unarmed idle — **руки вниз**, не «сдаюсь».
4. Взгляд вниз — не видно изнутри рукава.
5. Ходьба без оружия — **мах рук** (unarmed locomotion).
6. **T → armed:** винтовочная поза без скрутки и без вспышки.
7. Inspector камеры: local euler может прыгать — **ок**, если Game view стабилен.

---

## Чего не делать

| Действие | Почему плохо |
|----------|--------------|
| Откатить только git `fa5b438` без look-space offset | Руки снова в рукавах |
| Вернуть камеру на `head` «ради рук» | Culling / Transform ломаются; armed кажется «кривым» |
| Подключить `unarmedLocomotionOverride` со **старым** `ShooterHandPoseState` | Ломается idle; armed после **T** тоже нестабилен |
| **T→armed:** менять locomotion до settle overlay | Вспышка rifle-рук / скрутка |
| **`Play("Standing")` при equip** | Мигание armed idle |
| Крутить Transform **FPS Camera** в Play | Перезаписывается каждый кадр |
| Override `cameraLocalOffset.y` отрицательным в сцене | Камера в модели |

---

## Связанные файлы

| Файл | Роль |
|------|------|
| `ShooterFpsCameraApply.cs` | Отвязка камеры, look-space offset, culling |
| `ShooterHandPoseState.cs` | Unarmed overlay + runtime locomotion override |
| `ShooterCharacterController.cs` | Мост CCP ↔ FPS AF, поиск камеры на корне |
| `PlayerCharacter.prefab` | `detachFromHeadAnimation`, offset, locomotion override |
| `FPSAnimator_Unarmed_Humanoid.overrideController` | Unarmed walk/jog клипы |

---

## Источник правды

Рабочая связка собрана из **`D:\Unity Games\Shooter\`** (состояние на диске, август 2026):

- Git: `fa5b438` — Detach FPS camera from head bone to stabilize culling
- Поверх коммита: look-space offset, `Update()` + `ClearCameraBoneBinding`, `z=0.1`, runtime locomotion в `ShooterHandPoseState`

При переносе в новый проект копировать **оба** скрипта (`ShooterFpsCameraApply` + `ShooterHandPoseState`) **и** настройки префаба/сцены, а не только один коммит.

---

## См. также

- [TASKS.md](TASKS.md) — Задача 1.1 (unarmed overlay), риски FPS init
- [PHASE2_SETUP.md](PHASE2_SETUP.md) — первичный setup FPS AF
- [BALANCE_TUNING_PANEL.md](BALANCE_TUNING_PANEL.md) — F8: offset камеры, ladder pitch
