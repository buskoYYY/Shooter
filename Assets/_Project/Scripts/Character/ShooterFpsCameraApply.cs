using System.Reflection;
using KINEMATION.FPSAnimationFramework.Runtime.Camera;
using UnityEngine;

namespace Shooter.Project.Character
{
    /// <summary>
    /// Stabilizes FPS camera after FPS AF (FPSCameraController + FPSAnimator LateUpdate at order 0).
    /// Camera Local Offset on this component is the source of truth — edits on FPS Camera transform are overridden every frame.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(ShooterCharacterController))]
    public class ShooterFpsCameraApply : MonoBehaviour
    {
        [Tooltip("Eye offset from the head bone (local space). Edit here, not on FPS Camera transform.")]
        [SerializeField] Vector3 cameraLocalOffset = new Vector3(0f, 0.06f, 0.04f);
        [Tooltip("Default FPS field of view. Demo humanoid uses 80; 60 feels too zoomed in.")]
        [SerializeField] float defaultFieldOfView = 80f;

        static FieldInfo DefaultPositionField;

        ShooterCharacterController _character;
        FPSCameraController _fpsCamera;

        void Awake()
        {
            _character = GetComponent<ShooterCharacterController>();
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
            ApplyCamera();
        }

        void ApplyCamera()
        {
            if (_fpsCamera == null && _character != null)
                _fpsCamera = _character.FpsCamera;

            if (_fpsCamera == null || _character == null)
                return;

            Transform cam = _fpsCamera.transform;
            cam.localPosition = cameraLocalOffset;
            cam.localRotation = Quaternion.identity;
            cam.rotation = _character.transform.rotation * Quaternion.Euler(_character.Pitch, 0f, 0f);
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
