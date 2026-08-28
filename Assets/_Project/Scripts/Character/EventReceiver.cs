using UnityEngine;
using UnityEngine.Events;

namespace Shooter.Project.Character
{
    /// <summary>
    /// Animation Event target on the Animator GameObject (Character_model).
    /// Clips call Step / Jump / Falldown; wire audio in the UnityEvents.
    /// </summary>
    [DisallowMultipleComponent]
    public class EventReceiver : MonoBehaviour
    {
        [SerializeField] UnityEvent onStep;
        [SerializeField] UnityEvent onJump;
        [SerializeField] UnityEvent onFalldown;

        public UnityEvent OnStep => onStep;
        public UnityEvent OnJump => onJump;
        public UnityEvent OnFalldown => onFalldown;

        public void Step()
        {
            onStep?.Invoke();
        }

        public void Jump()
        {
            onJump?.Invoke();
        }

        public void Falldown()
        {
            onFalldown?.Invoke();
        }
    }
}
