using Lightbug.CharacterControllerPro.Core;
using UnityEngine;

namespace Shooter.Project.Character
{
    /// <summary>
    /// CCP collision size comes from CharacterBody (Width / Height), not the CapsuleCollider.
    /// Editing CapsuleCollider alone is overwritten at runtime — use this instead.
    /// </summary>
    [DefaultExecutionOrder(-250)]
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
            _characterBody = GetComponent<CharacterBody>();
            _characterActor = GetComponent<CharacterActor>();
            ApplyTuning();
        }

        void Start()
        {
            // CapsuleCollider Size is synced from BodySize after CharacterActor init.
            ApplyTuning();
        }

        void OnEnable()
        {
            ApplyTuning();
        }

        public void ResetDefaults()
        {
            width = DefaultWidth;
            height = DefaultHeight;
            ApplyTuning();
        }

        public void ApplyTuning()
        {
            if (_characterBody == null)
                _characterBody = GetComponent<CharacterBody>();

            if (_characterActor == null)
                _characterActor = GetComponent<CharacterActor>();

            if (_characterBody == null)
                return;

            width = Mathf.Clamp(width, 0.35f, 1.2f);
            height = Mathf.Max(height, width + CharacterConstants.ColliderMinBottomOffset);

            Vector2 size = new Vector2(width, height);
            _characterBody.BodySize = size;

            if (_characterActor != null && Application.isPlaying)
                _characterActor.SetSize(size, CharacterActor.SizeReferenceType.Bottom);
        }
    }
}
