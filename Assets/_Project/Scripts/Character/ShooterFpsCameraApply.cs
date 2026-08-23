using System.Reflection;
using KINEMATION.FPSAnimationFramework.Runtime.Camera;
using UnityEngine;

namespace Shooter.Project.Character
{
    /// <summary>
    /// Stabilizes FPS camera after FPS AF (FPSCameraController + FPSAnimator LateUpdate at order 0).
    /// Camera Local Offset on this component is the source of truth — edits on FPS Camera transform are overridden every frame.
    /// On ladders the camera stays in the head (not the capsule). Yaw locks to the character instantly;
    /// only vertical bob is lightly damped so climb anim does not shake the view.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(ShooterCharacterController))]
    public class ShooterFpsCameraApply : MonoBehaviour
    {
        [Tooltip("Eye offset from the head bone (local space). Edit here, not on FPS Camera transform.")]
        [SerializeField] Vector3 cameraLocalOffset = new Vector3(0f, 0.06f, 0.04f);
        [Tooltip("Default FPS field of view. Demo humanoid uses 80; 60 feels too zoomed in.")]
        [SerializeField] float defaultFieldOfView = 80f;

        public const float DefaultLadderLookSmoothTime = 0.4f;
        public const float DefaultLadderLookPitch = -18f;
        public const float DefaultLadderBobSmoothTime = 0.05f;
        public const float DefaultLadderExitSmoothTime = 0.12f;

        [Header("Ladder")]
        [Tooltip("How quickly pitch eases toward the climb look. Yaw is always instant (no body peek).")]
        [SerializeField] float ladderLookSmoothTime = DefaultLadderLookSmoothTime;
        [Tooltip("Pitch after mounting (negative = look up the rungs).")]
        [SerializeField] float ladderLookPitch = DefaultLadderLookPitch;
        [Tooltip("Damps only head bob on climb. Keep low. Does not move the camera behind the body.")]
        [SerializeField] float ladderBobSmoothTime = DefaultLadderBobSmoothTime;
        [Tooltip("How quickly the camera returns to the head after leaving the ladder.")]
        [SerializeField] float ladderExitSmoothTime = DefaultLadderExitSmoothTime;

        const float ExitBlendFinishDistance = 0.012f;
        const float ExitBlendFinishAngle = 1.25f;

        static FieldInfo DefaultPositionField;

        ShooterCharacterController _character;
        ShooterLadderFpsBridge _ladderBridge;
        FPSCameraController _fpsCamera;

        bool _ladderCameraActive;
        bool _exitBlendActive;
        Vector3 _smoothedPosition;
        Vector3 _positionVelocity;

        public float LadderLookSmoothTime
        {
            get => ladderLookSmoothTime;
            set => ladderLookSmoothTime = Mathf.Clamp(value, 0.04f, 0.8f);
        }

        public float LadderLookPitch
        {
            get => ladderLookPitch;
            set => ladderLookPitch = Mathf.Clamp(value, -40f, 20f);
        }

        public float LadderBobSmoothTime
        {
            get => ladderBobSmoothTime;
            set => ladderBobSmoothTime = Mathf.Clamp(value, 0.01f, 0.25f);
        }

        /// <summary>Kept for F8 panel compatibility; ladder camera no longer uses forward offset.</summary>
        public float LadderForwardOffset
        {
            get => 0f;
            set { }
        }

        /// <summary>Kept for F8 panel compatibility; maps to bob damp.</summary>
        public float LadderPositionSmoothTime
        {
            get => ladderBobSmoothTime;
            set => LadderBobSmoothTime = value;
        }

        public void ResetLadderCameraDefaults()
        {
            ladderLookSmoothTime = DefaultLadderLookSmoothTime;
            ladderLookPitch = DefaultLadderLookPitch;
            ladderBobSmoothTime = DefaultLadderBobSmoothTime;
            ladderExitSmoothTime = DefaultLadderExitSmoothTime;
        }

        void Awake()
        {
            _character = GetComponent<ShooterCharacterController>();
            _ladderBridge = GetComponent<ShooterLadderFpsBridge>();
        }

        void Start()
        {
            if (_character != null)
                _fpsCamera = _character.FpsCamera;

            ApplyDefaultFieldOfView();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (Application.isPlaying)
                return;

            var fpsCamera = FindFpsCamera();
            if (fpsCamera == null)
                return;

            fpsCamera.localPosition = cameraLocalOffset;
            fpsCamera.localRotation = Quaternion.identity;
        }

        [ContextMenu("Copy Offset From FPS Camera Transform")]
        void CopyOffsetFromFpsCameraTransform()
        {
            var fpsCamera = FindFpsCamera();
            if (fpsCamera == null)
                return;

            cameraLocalOffset = fpsCamera.localPosition;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        Transform FindFpsCamera()
        {
            if (_fpsCamera != null)
                return _fpsCamera.transform;

            if (_character != null && _character.FpsCamera != null)
                return _character.FpsCamera.transform;

            var fpsCameraController = GetComponentInChildren<FPSCameraController>(true);
            return fpsCameraController != null ? fpsCameraController.transform : null;
        }

        /// <summary>Call before FPSCameraController.Initialize so _defaultPosition matches the offset.</summary>
        public void PrepareCameraBeforeInit()
        {
            if (_fpsCamera == null && _character != null)
                _fpsCamera = _character.FpsCamera;

            ApplyCamera();
            ApplyDefaultFieldOfView();
            SyncFpsCameraDefaultPosition();
        }

        public void ForceRefresh()
        {
            if (_fpsCamera == null && _character != null)
                _fpsCamera = _character.FpsCamera;

            ApplyCamera();
            ApplyDefaultFieldOfView();
            SyncFpsCameraDefaultPosition();
        }

        void LateUpdate()
        {
            bool wantLadder = ShouldUseLadderCamera();
            if (wantLadder && !_ladderCameraActive)
                BeginLadderCamera();
            else if (!wantLadder && _ladderCameraActive)
                EndLadderCamera();

            ApplyCamera();
        }

        bool ShouldUseLadderCamera()
        {
            if (_ladderBridge == null)
                _ladderBridge = GetComponent<ShooterLadderFpsBridge>();

            return _ladderBridge != null && _ladderBridge.ShouldUseLadderCamera;
        }

        void BeginLadderCamera()
        {
            if (_fpsCamera == null && _character != null)
                _fpsCamera = _character.FpsCamera;

            if (_fpsCamera == null || _character == null)
                return;

            GetHeadCameraPose(out Vector3 headPos, out _);

            _ladderCameraActive = true;
            _exitBlendActive = false;
            _smoothedPosition = headPos;
            _positionVelocity = Vector3.zero;

            _character.BeginLadderPitchBlend(ladderLookPitch, ladderLookSmoothTime);
        }

        void EndLadderCamera()
        {
            _ladderCameraActive = false;
            _exitBlendActive = true;
            _character.EndLadderPitchBlend();

            if (_fpsCamera == null)
                return;

            _smoothedPosition = _fpsCamera.transform.position;
            _positionVelocity = Vector3.zero;
        }

        void ApplyCamera()
        {
            if (_fpsCamera == null && _character != null)
                _fpsCamera = _character.FpsCamera;

            if (_fpsCamera == null || _character == null)
                return;

            if (_ladderCameraActive)
            {
                ApplyLadderCamera();
                return;
            }

            if (_exitBlendActive)
            {
                ApplyExitBlendCamera();
                return;
            }

            ApplyHeadCamera();
        }

        void ApplyHeadCamera()
        {
            Transform cam = _fpsCamera.transform;
            cam.localPosition = cameraLocalOffset;
            cam.localRotation = Quaternion.identity;
            cam.rotation = _character.transform.rotation * Quaternion.Euler(_character.Pitch, 0f, 0f);
        }

        void ApplyLadderCamera()
        {
            Transform cam = _fpsCamera.transform;
            GetHeadCameraPose(out Vector3 headPos, out Quaternion lookRotation);

            // Stay inside the head — never use capsule offset (that is what showed the jacket).
            _smoothedPosition = Vector3.SmoothDamp(
                _smoothedPosition,
                headPos,
                ref _positionVelocity,
                ladderBobSmoothTime);

            cam.position = _smoothedPosition;
            // Instant yaw from character — any yaw lag looks at the jacket from outside.
            cam.rotation = lookRotation;
        }

        void ApplyExitBlendCamera()
        {
            Transform cam = _fpsCamera.transform;
            GetHeadCameraPose(out Vector3 targetPosition, out Quaternion targetRotation);

            _smoothedPosition = Vector3.SmoothDamp(
                _smoothedPosition,
                targetPosition,
                ref _positionVelocity,
                ladderExitSmoothTime);

            cam.position = _smoothedPosition;
            cam.rotation = targetRotation;

            if ((cam.position - targetPosition).sqrMagnitude <= ExitBlendFinishDistance * ExitBlendFinishDistance &&
                Quaternion.Angle(cam.rotation, targetRotation) <= ExitBlendFinishAngle)
            {
                _exitBlendActive = false;
                ApplyHeadCamera();
            }
        }

        void GetHeadCameraPose(out Vector3 position, out Quaternion rotation)
        {
            Transform head = _fpsCamera.transform.parent;
            if (head != null)
                position = head.TransformPoint(cameraLocalOffset);
            else
                position = _character.transform.position + _character.transform.up * 1.6f;

            rotation = _character.transform.rotation * Quaternion.Euler(_character.Pitch, 0f, 0f);
        }

        void ApplyDefaultFieldOfView()
        {
            if (_fpsCamera == null || defaultFieldOfView <= 0f)
                return;

            Camera cam = _fpsCamera.GetComponent<Camera>();
            if (cam != null)
                cam.fieldOfView = defaultFieldOfView;
        }

        void SyncFpsCameraDefaultPosition()
        {
            if (_fpsCamera == null)
                return;

            DefaultPositionField ??= typeof(FPSCameraController).GetField(
                "_defaultPosition",
                BindingFlags.Instance | BindingFlags.NonPublic);

            DefaultPositionField?.SetValue(_fpsCamera, cameraLocalOffset);
        }
    }
}
