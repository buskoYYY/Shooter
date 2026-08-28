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
        // [SerializeField] ShooterHandPoseState handPoseState;
        // [SerializeField] ShooterCharacterController locomotion;
        [SerializeField] ShooterCcpMovementTuning ccpMovement;
        [SerializeField] ShooterLadderApproachTuning ladderApproach;
        [SerializeField] ShooterCharacterController characterController;
        [SerializeField] ShooterFpsCameraApply fpsCamera;
        [SerializeField] ShooterJumpWindup jumpWindup;
        [SerializeField] ShooterBodySizeTuning bodySize;
        [SerializeField] bool visible;

        Rect _windowRect = new Rect(20f, 20f, 380f, 420f);
        Vector2 _scroll;
        // SwayLayerSettings _swayLayer;

        public static bool IsOpen { get; private set; }

        void Awake()
        {
            // if (handPoseState == null)
            //     handPoseState = GetComponent<ShooterHandPoseState>();

            // if (locomotion == null)
            //     locomotion = GetComponent<ShooterCharacterController>();

            if (ccpMovement == null)
                ccpMovement = GetComponent<ShooterCcpMovementTuning>();

            if (ladderApproach == null)
                ladderApproach = GetComponent<ShooterLadderApproachTuning>();

            if (characterController == null)
                characterController = GetComponent<ShooterCharacterController>();

            if (fpsCamera == null)
                fpsCamera = GetComponent<ShooterFpsCameraApply>();

            if (jumpWindup == null)
                jumpWindup = GetComponent<ShooterJumpWindup>();

            if (bodySize == null)
                bodySize = GetComponent<ShooterBodySizeTuning>();

            // CacheSwayLayer();
        }

        void OnDestroy()
        {
            if (IsOpen)
                ApplyCursorState(false);

            IsOpen = false;
        }

        void Update()
        {
            if (WasF9PressedThisFrame())
            {
                ShooterCharacterController.TogglePostureCompareMode();
                characterController?.SyncFpsLayerWeights();
            }

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
            GUI.Box(new Rect(8f, 34f, 320f, 22f), $"F9 — posture: {ShooterCharacterController.PostureCompareLabel}");
        }

        void DrawWindow(int id)
        {
            _scroll = GUILayout.BeginScrollView(_scroll);

            GUILayout.Label("F8 — show/hide this panel");
            GUILayout.Label($"F9 — posture A/B: {ShooterCharacterController.PostureCompareLabel}");
            GUILayout.Space(6f);

            DrawControllerMovementSection();

            GUILayout.Space(8f);
            DrawBodySizeSection();

            GUILayout.Space(8f);
            DrawJumpWindupSection();

            GUILayout.Space(8f);
            DrawLadderApproachSection();

            GUILayout.Space(8f);
            DrawLadderCameraSection();

            // GUILayout.Space(8f);
            // DrawAnimationLocomotionSection();
            // GUILayout.Space(8f);
            // DrawIkMotionSection();
            // GUILayout.Space(8f);
            // DrawOverlaySection();
            // GUILayout.Space(8f);
            // DrawSwaySection();

            GUILayout.Space(10f);
            if (GUILayout.Button("Reset to defaults"))
                ResetAllDefaults();

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        void DrawControllerMovementSection()
        {
            GUILayout.Label("Controller movement (CCP)", GUI.skin.box);

            if (ccpMovement == null)
            {
                GUILayout.Label("ShooterCcpMovementTuning not found.");
                return;
            }

            GUILayout.Label($"Walk speed: {ccpMovement.BaseSpeedLimit:0.0} m/s");
            ccpMovement.BaseSpeedLimit = GUILayout.HorizontalSlider(
                ccpMovement.BaseSpeedLimit, 2f, 8f);

            GUILayout.Label($"Sprint speed: {ccpMovement.BoostSpeedLimit:0.0} m/s");
            ccpMovement.BoostSpeedLimit = GUILayout.HorizontalSlider(
                ccpMovement.BoostSpeedLimit, ccpMovement.BaseSpeedLimit, 12f);

            GUILayout.Label($"Acceleration: {ccpMovement.StableGroundedAcceleration:0.0}");
            ccpMovement.StableGroundedAcceleration = GUILayout.HorizontalSlider(
                ccpMovement.StableGroundedAcceleration, 8f, 50f);

            GUILayout.Label($"Deceleration: {ccpMovement.StableGroundedDeceleration:0.0}");
            ccpMovement.StableGroundedDeceleration = GUILayout.HorizontalSlider(
                ccpMovement.StableGroundedDeceleration, 8f, 50f);
            GUILayout.Label("Higher accel = snappier turn while walking/running.");
        }

        void DrawBodySizeSection()
        {
            GUILayout.Label("Capsule (CharacterBody)", GUI.skin.box);

            if (bodySize == null)
            {
                GUILayout.Label("ShooterBodySizeTuning not found.");
                return;
            }

            GUILayout.Label("Edit Width here — NOT CapsuleCollider (CCP overwrites it).");
            GUILayout.Label($"Width (diameter): {bodySize.Width:0.00} m  → radius {bodySize.Width * 0.5f:0.00}");
            bodySize.Width = GUILayout.HorizontalSlider(bodySize.Width, 0.4f, 1.0f);

            GUILayout.Label($"Height: {bodySize.Height:0.00} m");
            bodySize.Height = GUILayout.HorizontalSlider(bodySize.Height, 1.4f, 2.2f);
        }

        void DrawJumpWindupSection()
        {
            GUILayout.Label("Jump (crouch spring)", GUI.skin.box);

            if (jumpWindup == null)
            {
                GUILayout.Label("ShooterJumpWindup not found.");
                return;
            }

            GUILayout.Label($"Crouch delay: {jumpWindup.CrouchDelay:0.00} s");
            jumpWindup.CrouchDelay = GUILayout.HorizontalSlider(
                jumpWindup.CrouchDelay, 0.05f, 0.35f);
            GUILayout.Label("Air strafe is off (planar air accel = 0).");
        }

        void DrawLadderApproachSection()
        {
            GUILayout.Label("Ladder approach", GUI.skin.box);

            if (ladderApproach == null)
            {
                GUILayout.Label("ShooterLadderApproachTuning not found.");
                return;
            }

            GUILayout.Label($"Approach duration: {ladderApproach.ApproachDuration:0.00} s");
            ladderApproach.ApproachDuration = GUILayout.HorizontalSlider(
                ladderApproach.ApproachDuration, 0.1f, 1.5f);

            GUILayout.Label($"Snap distance: {ladderApproach.ApproachSnapDistance:0.00} m");
            ladderApproach.ApproachSnapDistance = GUILayout.HorizontalSlider(
                ladderApproach.ApproachSnapDistance, 0.01f, 0.25f);
        }

        void DrawLadderCameraSection()
        {
            GUILayout.Label("Ladder camera", GUI.skin.box);

            if (fpsCamera == null)
            {
                GUILayout.Label("ShooterFpsCameraApply not found.");
                return;
            }

            GUILayout.Label("Yaw follows character (smooth approach). Pitch blends separately.");
            GUILayout.Label("Jacket stays hidden — these only soften the wall stare.");

            GUILayout.Label($"Look pitch: {fpsCamera.LadderLookPitch:0} deg");
            fpsCamera.LadderLookPitch = GUILayout.HorizontalSlider(
                fpsCamera.LadderLookPitch, -35f, 10f);

            GUILayout.Label($"Pitch blend: {fpsCamera.LadderLookSmoothTime:0.00} s (higher = softer into wall)");
            fpsCamera.LadderLookSmoothTime = GUILayout.HorizontalSlider(
                fpsCamera.LadderLookSmoothTime, 0.1f, 0.8f);

            GUILayout.Label($"Bob damp: {fpsCamera.LadderBobSmoothTime:0.00} s");
            fpsCamera.LadderBobSmoothTime = GUILayout.HorizontalSlider(
                fpsCamera.LadderBobSmoothTime, 0.01f, 0.2f);

            if (GUILayout.Button("Reset ladder camera defaults"))
                fpsCamera.ResetLadderCameraDefaults();
        }

        /*
        void DrawAnimationLocomotionSection()
        {
            GUILayout.Label("Animation locomotion (legs)", GUI.skin.box);

            if (locomotion == null)
            {
                GUILayout.Label("ShooterCharacterController not found.");
                return;
            }

            GUILayout.Label($"Start smoothing: {locomotion.LocomotionSmoothingStart:0.0}");
            locomotion.LocomotionSmoothingStart = GUILayout.HorizontalSlider(
                locomotion.LocomotionSmoothingStart, 1f, 10f);

            GUILayout.Label($"Stop smoothing: {locomotion.LocomotionSmoothingStop:0.0}");
            locomotion.LocomotionSmoothingStop = GUILayout.HorizontalSlider(
                locomotion.LocomotionSmoothingStop, 1f, 10f);

            GUILayout.Label($"Moving start threshold: {locomotion.MovingStartThreshold:0.00}");
            locomotion.MovingStartThreshold = GUILayout.HorizontalSlider(
                locomotion.MovingStartThreshold, 0.05f, 0.35f);

            GUILayout.Label($"Moving stop threshold: {locomotion.MovingStopThreshold:0.00}");
            locomotion.MovingStopThreshold = GUILayout.HorizontalSlider(
                locomotion.MovingStopThreshold, 0.01f, locomotion.MovingStartThreshold - 0.01f);
        }

        void DrawIkMotionSection()
        {
            GUILayout.Label("Weapon IK motions", GUI.skin.box);

            if (locomotion == null)
            {
                GUILayout.Label("ShooterCharacterController not found.");
                return;
            }

            DrawIkMotion("Jump / Land", locomotion.JumpBlendTime, locomotion.JumpPlayRate,
                v => locomotion.JumpBlendTime = v, v => locomotion.JumpPlayRate = v);
            DrawIkMotion("Stop", locomotion.StopBlendTime, locomotion.StopPlayRate,
                v => locomotion.StopBlendTime = v, v => locomotion.StopPlayRate = v);
            DrawIkMotion("Lean", locomotion.LeanBlendTime, locomotion.LeanPlayRate,
                v => locomotion.LeanBlendTime = v, v => locomotion.LeanPlayRate = v);
            DrawIkMotion("Crouch", locomotion.CrouchBlendTime, locomotion.CrouchPlayRate,
                v => locomotion.CrouchBlendTime = v, v => locomotion.CrouchPlayRate = v);
        }

        void DrawOverlaySection()
        {
            GUILayout.Label("Hand pose overlays", GUI.skin.box);

            if (handPoseState == null)
            {
                GUILayout.Label("ShooterHandPoseState not found.");
                return;
            }

            DrawTransitionAsset("Armed overlay", handPoseState.ArmedOverlayPose);
            DrawTransitionAsset("Unarmed overlay", handPoseState.UnarmedOverlayPose);
            DrawTransitionAsset("Equip (T → armed)", handPoseState.EquipClip);
            DrawTransitionAsset("Unequip (T → unarmed)", handPoseState.UnequipClip);
        }

        void DrawSwaySection()
        {
            GUILayout.Label("Weapon sway springs", GUI.skin.box);

            if (_swayLayer == null)
            {
                GUILayout.Label("SwayLayer not found on FPS profile.");
                return;
            }

            GUILayout.Label($"Move target damping: {_swayLayer.moveSwayTargetDamping:0.0}");
            _swayLayer.moveSwayTargetDamping = GUILayout.HorizontalSlider(
                _swayLayer.moveSwayTargetDamping, 0f, 25f);

            var moveRot = _swayLayer.moveSwayRotationSpring;
            GUILayout.Label($"Move rot stiffness: {moveRot.stiffness.x:0.00}");
            float stiffness = GUILayout.HorizontalSlider(moveRot.stiffness.x, 0.1f, 1.5f);
            moveRot.stiffness = new Vector3(stiffness, stiffness, stiffness);
            GUILayout.Label($"Move rot damping: {moveRot.damping.x:0.00}");
            float damping = GUILayout.HorizontalSlider(moveRot.damping.x, 0.05f, 1f);
            moveRot.damping = new Vector3(damping, damping, damping);
            _swayLayer.moveSwayRotationSpring = moveRot;

            var aimRot = _swayLayer.aimSwayRotationSpring;
            GUILayout.Label($"Aim rot stiffness: {aimRot.stiffness.x:0.00}");
            stiffness = GUILayout.HorizontalSlider(aimRot.stiffness.x, 0.1f, 1.5f);
            aimRot.stiffness = new Vector3(stiffness, stiffness, stiffness);
            GUILayout.Label($"Aim rot damping: {aimRot.damping.x:0.00}");
            damping = GUILayout.HorizontalSlider(aimRot.damping.x, 0.05f, 1f);
            aimRot.damping = new Vector3(damping, damping, damping);
            _swayLayer.aimSwayRotationSpring = aimRot;
        }

        static void DrawIkMotion(string label, float blendTime, float playRate,
            System.Action<float> setBlend, System.Action<float> setPlayRate)
        {
            GUILayout.Label(label, GUI.skin.box);
            GUILayout.Label($"Blend: {blendTime:0.00}s");
            setBlend(GUILayout.HorizontalSlider(blendTime, 0.05f, 1f));
            GUILayout.Label($"Play rate: {playRate:0.00}");
            setPlayRate(GUILayout.HorizontalSlider(playRate, 0.25f, 2f));
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

        void ResetSwayDefaults()
        {
            if (_swayLayer == null)
                return;

            _swayLayer.moveSwayPositionSpring = new VectorSpring
            {
                damping = new Vector3(0.35f, 0.3f, 0.4f),
                stiffness = new Vector3(0.7f, 0.8f, 0.7f),
                speed = Vector3.one,
                scale = Vector3.one
            };
            _swayLayer.moveSwayRotationSpring = new VectorSpring
            {
                damping = new Vector3(0.3f, 0.3f, 0.2f),
                stiffness = new Vector3(0.8f, 0.8f, 0.8f),
                speed = Vector3.one,
                scale = Vector3.one
            };
            _swayLayer.moveSwayTargetDamping = 15f;
            _swayLayer.moveSwaySpace = ESpaceType.ComponentSpace;
            _swayLayer.aimSwayPositionSpring = new VectorSpring
            {
                damping = new Vector3(0.5f, 0.5f, 0.5f),
                stiffness = new Vector3(0.6f, 0.6f, 0.6f),
                speed = Vector3.one,
                scale = Vector3.one
            };
            _swayLayer.aimSwayRotationSpring = new VectorSpring
            {
                damping = new Vector3(0.3f, 0.3f, 0.3f),
                stiffness = new Vector3(0.7f, 0.7f, 0.8f),
                speed = Vector3.one,
                scale = Vector3.one
            };
            _swayLayer.aimSwayTargetDamping = 9f;
            _swayLayer.aimSwaySpace = ESpaceType.ComponentSpace;
        }

        void CacheSwayLayer()
        {
            _swayLayer = null;
            if (handPoseState == null)
                return;

            var profile = handPoseState.FpsAnimatorProfile;
            if (profile?.settings == null)
                return;

            foreach (var layer in profile.settings)
            {
                if (layer is SwayLayerSettings sway)
                {
                    _swayLayer = sway;
                    break;
                }
            }
        }
        */

        void ResetAllDefaults()
        {
            ccpMovement?.ResetDefaults();
            ladderApproach?.ResetDefaults();
            fpsCamera?.ResetLadderCameraDefaults();
            jumpWindup?.ResetDefaults();
            bodySize?.ResetDefaults();
            // locomotion?.ResetMotionDefaults();
            // handPoseState?.ResetTransitionBlendDefaults();
            // ResetSwayDefaults();
        }

        static bool WasF8PressedThisFrame() => WasKeyPressedThisFrame(Key.F8);

        static bool WasF9PressedThisFrame() => WasKeyPressedThisFrame(Key.F9);

        static bool WasKeyPressedThisFrame(Key key)
        {
            if (Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame)
                return true;

            for (int i = 0; i < InputSystem.devices.Count; i++)
            {
                if (InputSystem.devices[i] is not Keyboard keyboard)
                    continue;

                if (keyboard[key].wasPressedThisFrame)
                    return true;
            }

            return false;
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

            if (host.GetComponent<ShooterBalanceTuningPanel>() == null)
                host.AddComponent<ShooterBalanceTuningPanel>();

            if (handPose != null && handPose.GetComponent<ShooterCcpMovementTuning>() == null)
                handPose.gameObject.AddComponent<ShooterCcpMovementTuning>();

            if (handPose != null && handPose.GetComponent<ShooterLadderApproachTuning>() == null)
                handPose.gameObject.AddComponent<ShooterLadderApproachTuning>();

            if (handPose != null && handPose.GetComponent<ShooterJumpWindup>() == null)
                handPose.gameObject.AddComponent<ShooterJumpWindup>();

            if (handPose != null && handPose.GetComponent<ShooterBodySizeTuning>() == null)
                handPose.gameObject.AddComponent<ShooterBodySizeTuning>();
        }
    }
}
