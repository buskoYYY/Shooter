using KINEMATION.FPSAnimationFramework.Runtime.Playables;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Project.Character
{
    /// <summary>
    /// Temporary in-game balance panel for client tuning. Remove before release.
    /// Toggle: F8
    /// </summary>
    [DisallowMultipleComponent]
    public class ShooterBalanceTuningPanel : MonoBehaviour
    {
        [SerializeField] ShooterHandPoseState handPoseState;
        [SerializeField] bool visible;

        Rect _windowRect = new Rect(20f, 20f, 360f, 240f);
        Vector2 _scroll;

        public static bool IsOpen { get; private set; }

        void Awake()
        {
            if (handPoseState == null)
                handPoseState = GetComponent<ShooterHandPoseState>();
        }

        void OnDestroy()
        {
            if (IsOpen)
                ApplyCursorState(false);

            IsOpen = false;
        }

        void Update()
        {
            if (WasF8PressedThisFrame())
                SetVisible(!visible);
        }

        void OnGUI()
        {
            if (!visible)
            {
                DrawClosedHint();
                return;
            }

            _windowRect = GUILayout.Window(
                GetInstanceID(),
                _windowRect,
                DrawWindow,
                "Balance Tuning (TEMP)");
        }

        void DrawClosedHint()
        {
            GUI.Box(new Rect(8f, 8f, 180f, 22f), "F8 — balance tuning");
        }

        void DrawWindow(int id)
        {
            _scroll = GUILayout.BeginScrollView(_scroll);

            GUILayout.Label("F8 — show/hide this panel");
            GUILayout.Space(6f);

            if (handPoseState == null)
            {
                GUILayout.Label("ShooterHandPoseState not found.");
                GUILayout.EndScrollView();
                GUI.DragWindow();
                return;
            }

            DrawTransitionAsset("Equip (T → armed)", handPoseState.EquipClip);
            GUILayout.Space(8f);
            DrawTransitionAsset("Unequip (T → unarmed)", handPoseState.UnequipClip);

            GUILayout.Space(10f);
            if (GUILayout.Button("Reset to defaults"))
                handPoseState.ResetTransitionBlendDefaults();

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        static bool WasF8PressedThisFrame()
        {
            if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
                return true;

            for (int i = 0; i < InputSystem.devices.Count; i++)
            {
                if (InputSystem.devices[i] is not Keyboard keyboard)
                    continue;

                if (keyboard.f8Key.wasPressedThisFrame)
                    return true;
            }

            return false;
        }

        static void DrawTransitionAsset(string label, FPSAnimationAsset asset)
        {
            GUILayout.Label(label, GUI.skin.box);

            if (asset == null)
            {
                GUILayout.Label("Asset not assigned.");
                return;
            }

            BlendTime blend = asset.blendTime;

            GUILayout.Label($"Blend In:  {blend.blendInTime:0.00}s");
            blend.blendInTime = GUILayout.HorizontalSlider(blend.blendInTime, 0.05f, 1.5f);

            GUILayout.Label($"Blend Out: {blend.blendOutTime:0.00}s");
            blend.blendOutTime = GUILayout.HorizontalSlider(blend.blendOutTime, 0.05f, 1.5f);

            asset.blendTime = blend;
        }

        void SetVisible(bool open)
        {
            visible = open;
            IsOpen = open;
            ApplyCursorState(open);
        }

        static void ApplyCursorState(bool panelOpen)
        {
            if (panelOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureExists()
        {
            if (FindFirstObjectByType<ShooterBalanceTuningPanel>() != null)
                return;

            ShooterHandPoseState handPose = FindFirstObjectByType<ShooterHandPoseState>();
            GameObject host = handPose != null
                ? handPose.gameObject
                : new GameObject("BalanceTuning (TEMP)");

            if (handPose == null)
                DontDestroyOnLoad(host);

            host.AddComponent<ShooterBalanceTuningPanel>();
        }
    }
}
