using KINEMATION.FPSAnimationFramework.Runtime.Camera;
using UnityEngine;

namespace Shooter.Project.Character
{
    /// <summary>
    /// Applies FPS camera pitch after FPS AF LateUpdate (FPSCameraController runs at order 0).
    /// </summary>
    [DefaultExecutionOrder(50)]
    [RequireComponent(typeof(ShooterCharacterController))]
    public class ShooterFpsCameraApply : MonoBehaviour
    {
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
        }

        void LateUpdate()
        {
            if (_fpsCamera == null || _character == null)
                return;

            Transform cam = _fpsCamera.transform;
            cam.rotation = _character.transform.rotation * Quaternion.Euler(_character.Pitch, 0f, 0f);
        }
    }

}
