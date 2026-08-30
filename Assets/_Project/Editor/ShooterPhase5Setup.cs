using Shooter.Project.Character;
using Shooter.Project.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Shooter.Project.Editor
{
    public static class ShooterPhase5Setup
    {
        const string TestScenePath = "Assets/_Project/Scenes/PlayerTest.unity";
        const string PlayerPrefabPath = "Assets/_Project/Prefabs/PlayerCharacter.prefab";
        const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        const string WeaponsFolder = "Assets/_Project/Weapons";

        const string PistolPrefabPath = "Assets/Demo/Prefabs/Humanoid/Mk23Mod0_Scriptable_Humanoid.prefab";
        const string RiflePrefabPath = "Assets/Demo/Prefabs/Humanoid/AK12_Scriptable_Humanoid.prefab";
        const string Rifle2PrefabPath = "Assets/Demo/Prefabs/Mk18/Mk18_Scriptable.prefab";
        const string KnifePrefabPath = "Assets/Demo/Prefabs/Knife/Knife.prefab";

        [MenuItem("Shooter/Phase 5/Create Weapon Definitions")]
        public static void CreateWeaponDefinitions()
        {
            EnsureDefinitions();
            AssetDatabase.SaveAssets();
            Debug.Log("Weapon definitions saved to " + WeaponsFolder);
        }

        [MenuItem("Shooter/Phase 5/Setup Weapons on Player")]
        public static void SetupWeaponsOnPlayer()
        {
            var player = FindPlayerInSceneOrPrefab();
            if (player == null)
            {
                EditorUtility.DisplayDialog("Player not found", "Open PlayerTest or the player prefab.", "OK");
                return;
            }

            if (!ConfigurePlayerWeapons(player, out string error))
            {
                EditorUtility.DisplayDialog("Weapon setup failed", error, "OK");
                return;
            }

            EditorUtility.SetDirty(player);
            if (player.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(player.scene);

            Debug.Log("Weapon system enabled on " + player.name);
        }

        [MenuItem("Shooter/Phase 5/Add Test Targets To Scene")]
        public static void AddTestTargetsToScene()
        {
            if (!System.IO.File.Exists(TestScenePath))
            {
                EditorUtility.DisplayDialog("Scene missing", "Run Phase 1 Create Test Scene first.", "OK");
                return;
            }

            var scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
            CreateTestProps();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Weapon test props added to " + TestScenePath);
        }

        [MenuItem("Shooter/Phase 5/Run Full Weapon Setup")]
        public static void RunFullWeaponSetup()
        {
            if (!System.IO.File.Exists(TestScenePath))
            {
                EditorUtility.DisplayDialog("Scene missing", "Run Phase 1 Create Test Scene first.", "OK");
                return;
            }

            EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
            var player = GameObject.Find("PlayerCharacter");
            if (player == null)
            {
                EditorUtility.DisplayDialog("Player missing", "PlayerCharacter not found in scene.", "OK");
                return;
            }

            if (!ConfigurePlayerWeapons(player, out string error))
            {
                EditorUtility.DisplayDialog("Weapon setup failed", error, "OK");
                return;
            }

            CreateTestProps();
            SavePlayerToPrefab(player);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

            EditorUtility.DisplayDialog(
                "Phase 5 setup",
                "Weapon scaffold is on the player.\n\n" +
                "T — draw / holster\n" +
                "LMB — fire\nR — reload\n1/2/3 — slots\nG — drop\n" +
                "Blocked while sprinting, jumping, or on a ladder.",
                "OK");
        }

        static GameObject FindPlayerInSceneOrPrefab()
        {
            var player = GameObject.Find("PlayerCharacter") ?? GameObject.Find("Player Character");
            if (player != null)
                return player;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            return prefab;
        }

        static bool ConfigurePlayerWeapons(GameObject playerRoot, out string error)
        {
            error = null;

            if (playerRoot.GetComponent<ShooterCharacterController>() == null)
            {
                error = "ShooterCharacterController missing. Run Phase 2 first.";
                return false;
            }

            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (input == null)
            {
                error = "InputSystem_Actions.inputactions not found.";
                return false;
            }

            var pistol = EnsureDefinitions();

            if (playerRoot.GetComponent<ShooterWeaponActionGate>() == null)
                playerRoot.AddComponent<ShooterWeaponActionGate>();
            if (playerRoot.GetComponent<ShooterWeaponInventory>() == null)
                playerRoot.AddComponent<ShooterWeaponInventory>();

            var controller = playerRoot.GetComponent<ShooterWeaponController>();
            if (controller == null)
                controller = playerRoot.AddComponent<ShooterWeaponController>();

            var so = new SerializedObject(controller);
            so.FindProperty("inputActions").objectReferenceValue = input;
            var starting = so.FindProperty("startingWeapons");
            starting.arraySize = pistol != null ? 1 : 0;
            if (pistol != null)
                starting.GetArrayElementAtIndex(0).objectReferenceValue = pistol;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        static ShooterWeaponDefinition EnsureDefinitions()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project"))
                AssetDatabase.CreateFolder("Assets", "_Project");
            if (!AssetDatabase.IsValidFolder(WeaponsFolder))
                AssetDatabase.CreateFolder("Assets/_Project", "Weapons");

            var pistol = UpsertDefinition(
                "Pistol_Mk23.asset",
                "Mk23",
                ShooterWeaponKind.Pistol,
                PistolPrefabPath,
                22f, 80f, 400f, ShooterFireMode.Semi, 12, 36, 1.6f, 0f, 0f);

            UpsertDefinition(
                "Rifle_AK12.asset",
                "AK12",
                ShooterWeaponKind.Rifle,
                RiflePrefabPath,
                25f, 150f, 650f, ShooterFireMode.Auto, 30, 90, 2.2f, 0f, 0f);

            UpsertDefinition(
                "Rifle_Mk18.asset",
                "Mk18",
                ShooterWeaponKind.Rifle,
                Rifle2PrefabPath,
                24f, 140f, 750f, ShooterFireMode.Auto, 30, 90, 2.1f, 0f, 0f);

            UpsertDefinition(
                "Melee_Knife.asset",
                "Knife",
                ShooterWeaponKind.Melee,
                KnifePrefabPath,
                40f, 1.8f, 0f, ShooterFireMode.Semi, 0, 0, 0f, 0.45f, 0.35f);

            AssetDatabase.SaveAssets();
            return pistol;
        }

        static ShooterWeaponDefinition UpsertDefinition(
            string fileName,
            string displayName,
            ShooterWeaponKind kind,
            string prefabPath,
            float damage,
            float range,
            float rpm,
            ShooterFireMode fireMode,
            int mag,
            int reserve,
            float reload,
            float meleeDelay,
            float meleeRadius)
        {
            string path = WeaponsFolder + "/" + fileName;
            var def = AssetDatabase.LoadAssetAtPath<ShooterWeaponDefinition>(path);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<ShooterWeaponDefinition>();
                AssetDatabase.CreateAsset(def, path);
            }

            def.displayName = displayName;
            def.kind = kind;
            def.viewPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            def.damage = damage;
            def.range = range;
            def.fireRateRpm = rpm;
            def.fireMode = fireMode;
            def.magazineSize = mag;
            def.startReserveAmmo = reserve;
            def.reloadSeconds = reload;
            def.meleeDelay = meleeDelay;
            def.meleeRadius = meleeRadius;
            def.meleeCameraPunch = new Vector2(-2.2f, 0.6f);
            EditorUtility.SetDirty(def);
            return def;
        }

        static void CreateTestProps()
        {
            Transform root = GetOrCreate("WeaponTest").transform;

            if (root.Find("DummyTarget") == null)
            {
                var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
                target.name = "DummyTarget";
                target.transform.SetParent(root, false);
                target.transform.position = new Vector3(4f, 1f, 8f);
                target.transform.localScale = new Vector3(0.8f, 2f, 0.4f);
                target.AddComponent<ShooterDummyDamageable>();
                Tint(target, new Color(0.75f, 0.45f, 0.2f));
            }

            CreatePickup(root, "Pickup_AK12", new Vector3(2f, 0.35f, 3f), "Rifle_AK12.asset", Color.green);
            CreatePickup(root, "Pickup_Mk18", new Vector3(3.2f, 0.35f, 3f), "Rifle_Mk18.asset", new Color(0.2f, 0.5f, 0.9f));
            CreatePickup(root, "Pickup_Knife", new Vector3(4.4f, 0.35f, 3f), "Melee_Knife.asset", Color.gray);
            CreateAmmoPickup(root, new Vector3(2f, 0.25f, 4.2f), "Pistol_Mk23.asset", 24);
        }

        static void CreatePickup(Transform root, string name, Vector3 position, string assetFile, Color color)
        {
            if (root.Find(name) != null)
                return;

            var def = AssetDatabase.LoadAssetAtPath<ShooterWeaponDefinition>(WeaponsFolder + "/" + assetFile);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(root, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.28f;
            var col = go.GetComponent<BoxCollider>();
            col.isTrigger = true;
            go.AddComponent<ShooterWorldPickup>().ConfigureWeapon(def);
            Tint(go, color);
        }

        static void CreateAmmoPickup(Transform root, Vector3 position, string assetFile, int amount)
        {
            const string name = "Pickup_Ammo";
            if (root.Find(name) != null)
                return;

            var def = AssetDatabase.LoadAssetAtPath<ShooterWeaponDefinition>(WeaponsFolder + "/" + assetFile);
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(root, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.22f;
            var col = go.GetComponent<SphereCollider>();
            col.isTrigger = true;
            go.AddComponent<ShooterWorldPickup>().ConfigureAmmo(def, amount);
            Tint(go, Color.yellow);
        }

        static void Tint(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null || renderer.sharedMaterial == null)
                return;

            renderer.sharedMaterial = new Material(renderer.sharedMaterial);
            renderer.sharedMaterial.color = color;
        }

        static GameObject GetOrCreate(string name)
        {
            var existing = GameObject.Find(name);
            if (existing != null)
                return existing;
            return new GameObject(name);
        }

        static void SavePlayerToPrefab(GameObject playerRoot)
        {
            var prefabRoot = PrefabUtility.GetCorrespondingObjectFromSource(playerRoot);
            if (prefabRoot != null)
                PrefabUtility.SaveAsPrefabAssetAndConnect(playerRoot, PlayerPrefabPath, InteractionMode.AutomatedAction);
            else
                PrefabUtility.SaveAsPrefabAsset(playerRoot, PlayerPrefabPath);
        }
    }
}
