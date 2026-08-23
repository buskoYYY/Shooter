using Lightbug.CharacterControllerPro.Core;
using UnityEngine;

namespace Shooter.Project.Character
{
    /// <summary>
    /// CCP collision size comes from CharacterBody (Width / Height), not the CapsuleCollider.
    /// Editing CapsuleCollider alone is overwritten at runtime — use this instead.
    /// </summary>
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterBody))]
    public class ShooterBodySizeTuning : MonoBehaviour
    {
        /// <summary>Demo default is 0.5 (radius 0.25). Thicker avoids mesh clipping into walls.</summary>
        public const float DefaultWidth = 0.72f;
        public const float DefaultHeight = 1.9f;

        [Tooltip("CharacterBody Width (= capsule diameter). Demo was 0.5.")]
        [SerializeField] float width = DefaultWidth;
        [Tooltip("CharacterBody Height. Keep >= width.")]
        [SerializeField] float height = DefaultHeight;

        CharacterBody _characterBody;
        CharacterActor _characterActor;
        bool _actorReady;

        public float Width
        {
            get => width;
            set
            {
                width = Mathf.Clamp(value, 0.35f, 1.2f);
                height = Mathf.Max(height, width + 0.2f);
                ApplyTuning();
            }
        }

        public float Height
        {
            get => height;
            set
            {
                height = Mathf.Max(value, width + 0.2f);
                ApplyTuning();
            }
        }

        void Awake()
        {
            CacheRefs();
            // Only write BodySize here — CharacterActor.Rigidbody is not ready yet if we were
            // added from another Awake (EnsureBodySizeTuningOnSelf).
            ApplyBodySizeOnly();
        }

        void Start()
        {
            _actorReady = true;
            ApplyTuning();
        }

        void OnEnable()
        {
            if (_actorReady)
                ApplyTuning();
            else
                ApplyBodySizeOnly();
        }

        public void ResetDefaults()
        {
            width = DefaultWidth;
            height = DefaultHeight;
            ApplyTuning();
        }

        public void ApplyTuning()
        {
            ApplyBodySizeOnly();

            if (!Application.isPlaying || !_actorReady)
                return;

            CacheRefs();
            if (_characterActor == null || _characterActor.RigidbodyComponent == null)
                return;

            _characterActor.SetSize(new Vector2(width, height), CharacterActor.SizeReferenceType.Bottom);
        }

        void ApplyBodySizeOnly()
        {
            CacheRefs();
            if (_characterBody == null)
                return;

            width = Mathf.Clamp(width, 0.35f, 1.2f);
            height = Mathf.Max(height, width + CharacterConstants.ColliderMinBottomOffset);
            _characterBody.BodySize = new Vector2(width, height);
        }

        void CacheRefs()
        {
            if (_characterBody == null)
                _characterBody = GetComponent<CharacterBody>();

            if (_characterActor == null)
                _characterActor = GetComponent<CharacterActor>();
        }
    }
}
