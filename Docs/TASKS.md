# Документация проекта Shooter

Формат: каждая новая задача получает заголовок, список того, что нужно сделать, и список того, что уже сделано.  
Обновляется по мере работы.

---

## Задача 1 — Интеграция Character Controller Pro + полное тело (FPS)

**Цель:** взять готовый Character Controller Pro как основной компонент движения (ходьба, бег, прыжки, слоупы, карабканье по лестнице и т.д.), подключить к нему полное тело персонажа с видом от первого лица. Оружие, стрельба и перезарядка — позже.

### Что нужно сделать

#### Этап A — Подготовка ассетов
- [ ] Убедиться, что Character Controller Pro импортирован и доступен в проекте
- [ ] Скачать и импортировать **Demo Content** для KINEMATION FPS Animation Framework  
  (`KINEMATION → Tools → FPS Animation Framework → Download Demo`) — анимации, Animator Controller, примеры
- [ ] Разместить персонажа (`Character_model.fbx`) и префаб CCP на сцене

#### Этап B — Character Controller Pro (движение)
- [ ] Настроить префаб/сцену с контроллером CCP по его документации
- [ ] Проверить базовое движение: ходьба, бег, прыжок, приземление
- [ ] Проверить слоупы (наклонные поверхности)
- [ ] Проверить карабканье по лестнице вверх
- [ ] Настроить камеру от первого лица (FPS view)

#### Этап C — KINEMATION FPS Animation Framework (тело)
- [ ] Запустить **FPS ANIMATOR Wizard** (`GameObject → FPS ANIMATOR Wizard`) на персонаже:
  - Root, Head, Pelvis, Spine Root
  - Right/Left Hand, Right/Left Foot
  - Animator Controller из демо
  - Input Config: `Assets/KINEMATION/FPSAnimationFramework/Assets/InputConfig_FPSAnimationFramework.asset`
- [ ] Создать **Animator Profile** через **FPS PROFILE Wizard** (`Assets → FPS PROFILE Wizard` на Rig-ассете)
- [ ] Подключить слои из демо-профиля:
  - **Turn Layer** — поворот тела (hip/root)
  - **View Layer** — наклон камеры/вида
  - **IK Layer** — IK для ног и рук
  - **Sway Layer** — процедурный разброс
  - **Look Layer**, **Pose Sampler**, **Additive**, **Ik Motion**, **ADS** (по необходимости без оружия)
- [ ] Связать параметры аниматора с состоянием CCP (скорость, grounded, прыжок, лестница и т.д.)
- [ ] Настроить `UserInputController` / Input System под управление проекта

#### Этап D — Интеграция CCP ↔ анимация
- [ ] Передавать скорость и состояние движения из CCP в `FPSAnimator` / Animator
- [ ] Синхронизировать поворот камеры с `FPSCameraController` и Turn Layer
- [ ] Проверить IK ног на неровной поверхности и на лестнице
- [ ] Убедиться, что тело видно в FPS (руки, ноги при движении) без артефактов

#### Этап E — Тестирование
- [ ] Прогнать тестовую сцену: плоскость, слоуп, лестница
- [ ] Зафиксировать известные проблемы и отложить оружие на следующую задачу

### Что уже сделано

- [x] Создан Unity-проект Shooter (URP, Input System)
- [x] Импортирован **KINEMATION FPS Animation Framework** (`Assets/KINEMATION/`)
- [x] Добавлена 3D-модель персонажа (`Assets/_Project/Packages/Models/Character_model.fbx`)
- [x] Пользователь добавил Character Controller Pro и персонажа в проект *(в репозитории CCP пока не отслеживается git — проверить локально в Unity)*
- [ ] Demo Content KINEMATION — **не импортирован** (нужно скачать через Wizard)
- [ ] Интеграция CCP + анимация — **не начата**
- [ ] Сцена `SampleScene` — дефолтная (камера + свет), персонаж не настроен

### Используемые ассеты

| Ассет | Путь / заметки |
|-------|----------------|
| Character Controller Pro | Документация внутри пакета (PDF / Online Docs) |
| KINEMATION FPS Animation Framework | `Assets/KINEMATION/FPSAnimationFramework/` |
| Документация KINEMATION | `Offline Documentation.pdf`, `Online Documentation.url` |
| Demo Content | Скачать: GitHub kinemation/demoes → FPSAnimationFramework_Demo.unitypackage |
| Модель персонажа | `Assets/_Project/Packages/Models/Character_model.fbx` |

### Следующая задача (запланировано)

**Задача 2 — Оружие:** добавление оружия, перезарядка, стрельба, Weapon Layer, Recoil.

---

*Последнее обновление: 4 августа 2026*
