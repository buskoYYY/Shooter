# Панель Balance Tuning (временная)

Документ для заказчика: что можно крутить в игре через панель настройки баланса.

## Как открыть

- Запусти Play Mode в сцене с игроком (например `PlayerTest` или `3D Scene`).
- Нажми **F8** — панель откроется.
- Повторное **F8** — закрыть.
- **F9** — A/B сравнение осанки (legacy slouched vs текущая straight).
- Когда панель закрыта, в левом верхнем углу видны подсказки: `F8 — balance tuning`, `F9 — posture`.

Пока панель открыта:

- управление персонажем отключено;
- курсор разблокирован (можно двигать ползунки мышью);
- окно можно перетаскивать за заголовок.

Панель помечена как **TEMP** — перед релизом её уберут из билда.

---

## 1. Controller movement (CCP) — скорость самого персонажа

Это **реальная скорость Character Controller Pro**, а не анимация ног.

**Walk speed** (дефолт: 5 m/s, диапазон: 2–8)

- Максимальная скорость **ходьбы** (WASD без Shift).

**Sprint speed** (дефолт: 7.5 m/s, диапазон: walk–12)

- Максимальная скорость **бега** (Shift).

**Acceleration** (дефолт: **22**, диапазон: 8–50)

- Как быстро набирается скорость при нажатии WASD.
- Меньше — медленный разгон. Больше — резкий старт и быстрее реакция при смене направления в движении.

**Deceleration** (дефолт: **24**, диапазон: 8–50)

- Как быстро теряет скорость после отпускания клавиш.

Подсказка в панели: *Higher accel = snappier turn while walking.*

---

## 2. Capsule (CharacterBody) — размер капсулы

CCP **не** использует Unity `CapsuleCollider` для ширины — каждый кадр берёт **CharacterBody → Width / Height**.

**Width (diameter)** (дефолт: **0.72 m**, диапазон: 0.4–1.0)

- Диаметр капсулы. Демо CCP было 0.5 — при 0.72 меньше застреваний в углах и клип в стены.

**Height** (дефолт: **1.9 m**, диапазон: 1.4–2.2)

- Высота капсулы под рост модели.

> Править `CapsuleCollider` в инспекторе бесполезно — CCP перезапишет значения.

---

## 3. Jump (crouch spring) — пружина перед прыжком

**Crouch delay** (дефолт: **0.14 s**, диапазон: 0.05–0.35)

- Задержка между Space и фактическим импульсом прыжка.
- В это время играет присед / JumpStart — ощущение «пружины», не мгновенный отрыв.

**Air strafe:** отключён на уровне CCP (`notGroundedAcceleration` / `notGroundedDeceleration` = 0). В воздухе нельзя докручивать направление WASD.

---

## 4. Ladder approach — подход к лестнице

**Approach duration** (дефолт: **0.55 s**, диапазон: 0.1–1.5)

- Как долго персонаж плавно доворачивается к лестнице перед началом climb (yaw от текущего forward, не snap).

**Snap distance** (дефолт: из CCP demo, диапазон: 0.01–0.25 m)

- Дистанция финального прилипания к точке входа.

---

## 5. Ladder camera — камера на лестнице

Камера остаётся на **голове** (не на капсуле). Куртка скрывается на время лазания — эти ползунки только смягчают «взгляд в стену».

**Look pitch** (дефолт: **−18°**, диапазон: −35…10)

- Насколько камера смотрит вверх по перекладинам, а не в упор в стену.

**Pitch blend** (дефолт: **0.40 s**, диапазон: 0.1–0.8)

- Плавность перехода pitch при входе на лестницу.

**Bob damp** (дефолт: **0.05 s**, диапазон: 0.01–0.2)

- Демпфирование дрожи камеры от анимации головы при climb.

Кнопка **Reset ladder camera defaults** — сброс только этой секции.

---

## 6. Reset to defaults

Кнопка внизу панели сбрасывает активные секции:

- Controller movement (CCP);
- Capsule (CharacterBody);
- Jump wind-up;
- Ladder approach;
- Ladder camera.

---

## Параметры в коде (не в F8 сейчас)

Следующие значения заданы на префабе / в `ShooterCharacterController`, секции в F8 **закомментированы**, но дефолты актуальны:

| Параметр | Дефолт | Где |
|----------|--------|-----|
| Animation start smoothing | **7** | `ShooterCharacterController` |
| Animation stop smoothing | **8** | то же |
| Moving start threshold | **0.18** | то же |
| Turn-in-place fade-out | **~0.35 s** | `ShooterHandPoseState` |
| Turn-in-place locomotion ramp | **2.5** | `ShooterCharacterController` |

Если понадобится крутить их в Play Mode — можно вернуть секцию «Animation locomotion» в панель.

---

## Что крутить в первую очередь (подсказка)

**Старт с места слишком резкий (скорость персонажа)**

- Уменьши **Acceleration** (например 15–18).
- Можно чуть снизить **Walk speed**.

**Старт с места резкий только визуально (ноги)**

- На префабе: `locomotionSmoothingStart` (сейчас 7; попробовать 4–5).

**Застревает в углах / клип в стены**

- Увеличь **Width** в Capsule (0.72 → 0.78), не CapsuleCollider.

**Прыжок слишком мгновенный**

- Увеличь **Crouch delay** (0.14 → 0.18–0.22).

**На лестнице резко смотрит в стену**

- **Look pitch** −22…−25, **Pitch blend** 0.5–0.6.

**Разворот на месте → W режет анимацию**

- Уже смягчено в коде (fade 0.35 s). Если всё ещё резко — править константы в `ShooterHandPoseState` / `turnInPlaceLocomotionSmoothing` на префабе.

---

## Важно

- Панель только для **временной** настройки баланса в редакторе / dev-билде.
- `OnGUI` панели даёт GC Alloc — в Profiler не путать с игровой логикой.
- Значения на префабе: `Assets/_Project/Prefabs/PlayerCharacter.prefab`.

---

## Связанные файлы в проекте

- `Assets/_Project/Scripts/Character/ShooterBalanceTuningPanel.cs` — панель F8/F9
- `Assets/_Project/Scripts/Character/ShooterCcpMovementTuning.cs` — разгон/торможение CCP
- `Assets/_Project/Scripts/Character/ShooterBodySizeTuning.cs` — капсула CharacterBody
- `Assets/_Project/Scripts/Character/ShooterJumpWindup.cs` — пружина прыжка
- `Assets/_Project/Scripts/Character/ShooterLadderApproachTuning.cs` — подход к лестнице
- `Assets/_Project/Scripts/Character/ShooterFpsCameraApply.cs` — камера на лестнице
- `Assets/_Project/Scripts/Character/ShooterCharacterController.cs` — анимация ног, turn-in-place
- `Assets/_Project/Scripts/Character/ShooterHandPoseState.cs` — armed / unarmed (T), TurnInPlace layer
- [TASKS.md](TASKS.md) — полная техническая документация (Задачи 1.1–1.4)
