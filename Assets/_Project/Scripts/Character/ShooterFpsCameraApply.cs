using System.Reflection;
using KINEMATION.FPSAnimationFramework.Runtime.Camera;
using UnityEngine;

namespace Shooter.Project.Character
{
    /// <summary>
    /// Drives FPS camera after KINEMATION (FPSAnimator LateUpdate order 0).
    /// Camera is unparented from the head bone so Animator rotation does not affect Camera.transform
    /// (wild euler values break world/occlusion culling). Each LateUpdate: follow head position,
    /// apply look rotation from character yaw + pitch only.
    /// </summary>
    [DefaultExecutionOrder(500)]
    [RequireComponent(typeof(ShooterCharacterController))]
    public class ShooterFpsCameraApply : MonoBehaviour
    {
        [Tooltip("Eye offset from the head bone (character-aligned). Edit here, not on FPS Camera transform.")]
        [SerializeField] Vector3 cameraLocalOffset = new Vector3(0f, 0.06f, 0.1f);
        [Tooltip("Default FPS field of view. Demo humanoid uses 80; 60 feels too zoomed in.")]
        [SerializeField] float defaultFieldOfView = 80f;
        [Tooltip("If true, camera is detached from head and follows it in LateUpdate (fixes culling).")]
        [SerializeField] bool detachFromHeadAnimation = true;

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
        static FieldInfo CameraBoneField;

        ShooterCharacterController _character;
        ShooterLadderFpsBridge _ladderBridge;
        FPSCameraController _fpsCamera;
        Transform _head;
        bool _detached;

        bool _ladderCameraActive;
        bool _exitBlendActive;
        Vector3 _smoothedPosition;
        Vector3 _positionVelocity;
        Vector2 _weaponCameraPunch;
        const float WeaponPunchDecay = 12f;

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

        /// <summary>Temporary pitch/yaw punch for melee (and later fire). Decays each frame.</summary>
        public void AddWeaponCameraPunch(Vector2 pitchYaw)
        {
            _weaponCameraPunch += pitchYaw;
        }

        void Awake()
        {
            ResolveRefs();
        }

        void Start()
        {
            ResolveRefs();
            ClearCameraBoneBinding();
            EnsureDetachedFromHead();
            ApplyDefaultFieldOfView();
            ApplyCamera();
        }

        void Update()
        {
            // Before FPSAnimator LateUpdate (order 0): avoid multiplying head bone into camera rotation.
            ClearCameraBoneBinding();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (Application.isPlaying)
                return;

            var fpsCamera = FindFpsCamera();
            if (fpsCamera == null)
                return;

            // Prefab still parents camera under head; runtime detach handles Play Mode.
            if (fpsCamera.parent != null && fpsCamera.parent.name.IndexOf("head", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                fpsCamera.localPosition = cameraLocalOffset;
                fpsCamera.localRotation = Quaternion.identity;
            }
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

        void ResolveRefs()
        {
            if (_character == null)
                _character = GetComponent<ShooterCharacterController>();

            if (_ladderBridge == null)
                _ladderBridge = GetComponent<ShooterLadderFpsBridge>();

            if (_fpsCamera == null)
            {
                if (_character != null && _character.FpsCamera != null)
                    _fpsCamera = _character.FpsCamera;
                else
                    _fpsCamera = GetComponentInChildren<FPSCameraController>(true);
            }
        }

        Transform FindFpsCamera()
        {
            ResolveRefs();
            return _fpsCamera != null ? _fpsCamera.transform : null;
        }

        /// <summary>Call before FPSCameraController.Initialize so _defaultPosition matches the offset.</summary>
        public void PrepareCameraBeforeInit()
        {
            ResolveRefs();
            EnsureDetachedFromHead();
            ApplyCamera();
            ApplyDefaultFieldOfView();
            SyncFpsCameraDefaultPosition();
        }

        public void ForceRefresh()
        {
            ResolveRefs();
            _detached = false;
            EnsureDetachedFromHead();
            ApplyCamera();
            ApplyDefaultFieldOfView();
            SyncFpsCameraDefaultPosition();
        }

        void LateUpdate()
        {
            ResolveRefs();

            bool wantLadder = ShouldUseLadderCamera();
            if (wantLadder && !_ladderCameraActive)
                BeginLadderCamera();
            else if (!wantLadder && _ladderCameraActive)
                EndLadderCamera();

            // After FPSAnimator / FPSCameraController (order 0): final pose for rendering + culling.
            EnsureDetachedFromHead();
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
            ResolveRefs();
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
            ResolveRefs();
            if (_fpsCamera == null || _character == null)
                return;

            _weaponCameraPunch = Vector2.Lerp(_weaponCameraPunch, Vector2.zero, Time.deltaTime * WeaponPunchDecay);

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

        void EnsureDetachedFromHead()
        {
            if (!detachFromHeadAnimation)
                return;

            ResolveRefs();
            if (_fpsCamera == null || _character == null)
                return;

            Transform cam = _fpsCamera.transform;
            Transform playerRoot = _character.transform;

            if (_head == null)
            {
                if (cam.parent != null && cam.parent != playerRoot && IsHeadLike(cam.parent))
                    _head = cam.parent;
                else
                    _head = FindHeadBone();
            }

            ClearCameraBoneBinding();

            // Re-check every call: something may reparent, or first Awake call ran before refs were ready.
            if (cam.parent != playerRoot)
            {
                GetHeadCameraPose(out Vector3 worldPos, out Quaternion worldRot);
                cam.SetParent(playerRoot, worldPositionStays: true);
                cam.SetPositionAndRotation(worldPos, worldRot);
            }

            _detached = cam.parent == playerRoot;
            if (_detached)
                SyncFpsCameraDefaultPosition();
        }

        static bool IsHeadLike(Transform t)
        {
            if (t == null)
                return false;

            string n = t.name;
            return n.IndexOf("head", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        Transform FindHeadBone()
        {
            if (_character == null)
                return null;

            Transform root = _character.transform;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (string.Equals(all[i].name, "head", System.StringComparison.OrdinalIgnoreCase))
                    return all[i];
            }

            return null;
        }

        void ClearCameraBoneBinding()
        {
            if (_fpsCamera == null)
                return;

            CameraBoneField ??= typeof(FPSCameraController).GetField(
                "cameraBone",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (CameraBoneField == null)
                return;

            if (CameraBoneField.GetValue(_fpsCamera) != null)
                CameraBoneField.SetValue(_fpsCamera, null);
        }

        void ApplyHeadCamera()
        {
            Transform cam = _fpsCamera.transform;
            GetHeadCameraPose(out Vector3 worldPos, out Quaternion lookRotation);
            cam.SetPositionAndRotation(worldPos, lookRotation);
            // FPSCameraController.Update() resets localPosition to _defaultPosition — keep it in sync
            // so mid-frame transform (and culling) stay at the eye, not at a stale offset.
            SyncFpsCameraDefaultPosition();
        }

        void ApplyLadderCamera()
        {
            Transform cam = _fpsCamera.transform;
            GetHeadCameraPose(out Vector3 headPos, out Quaternion lookRotation);

            _smoothedPosition = Vector3.SmoothDamp(
                _smoothedPosition,
                headPos,
                ref _positionVelocity,
                ladderBobSmoothTime);

            cam.SetPositionAndRotation(_smoothedPosition, lookRotation);
            SyncFpsCameraDefaultPosition();
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

            cam.SetPositionAndRotation(_smoothedPosition, targetRotation);
            SyncFpsCameraDefaultPosition();

            if ((cam.position - targetPosition).sqrMagnitude <= ExitBlendFinishDistance * ExitBlendFinishDistance &&
                Quaternion.Angle(cam.rotation, targetRotation) <= ExitBlendFinishAngle)
            {
                _exitBlendActive = false;
                ApplyHeadCamera();
            }
        }

        void GetHeadCameraPose(out Vector3 position, out Quaternion rotation)
        {
            // Rotation is look only (yaw from body + pitch from mouse) — independent of Animator.
            rotation = _character.transform.rotation * Quaternion.Euler(
                _character.Pitch + _weaponCameraPunch.x,
                _weaponCameraPunch.y,
                0f);

            // Offset in look space so looking up does not bury the eye in the chest/sleeves.
            Vector3 eyeOffset = rotation * cameraLocalOffset;
            if (_head != null)
                position = _head.position + eyeOffset;
            else
                position = _character.transform.position + _character.transform.up * 1.6f + eyeOffset;
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

            DefaultPositionField?.SetValue(_fpsCamera, _fpsCamera.transform.localPosition);
        }
    }
}
