using Lightbug.CharacterControllerPro.Core;
using Lightbug.CharacterControllerPro.Demo;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Shooter.Project.Character
{
    /// <summary>
    /// Plays full-body humanoid jump clips (Start / Loop / End) via Playables.
    /// Works without an InAir layer on the animator controller.
    /// </summary>
    [DefaultExecutionOrder(-180)]
    [DisallowMultipleComponent]
    public class ShooterJumpBodyAnimation : MonoBehaviour
    {
        enum JumpPhase
        {
            None,
            Start,
            Loop,
            End
        }

        [SerializeField] Transform fpsCharacterRoot;
        [SerializeField] AnimationClip jumpStart;
        [SerializeField] AnimationClip jumpLoop;
        [SerializeField] AnimationClip jumpEnd;
        [SerializeField] float startSpeed = 1.3f;
        [SerializeField] float endSpeed = 1.3f;

        Animator _animator;
        CharacterActor _characterActor;
        NormalMovement _normalMovement;
        PlayableGraph _graph;
        AnimationClipPlayable _activePlayable;
        JumpPhase _phase = JumpPhase.None;
        bool _playedSinceLand;

        void Awake()
        {
            _characterActor = GetComponent<CharacterActor>();
            _normalMovement = GetComponentInChildren<NormalMovement>();

            if (fpsCharacterRoot == null)
            {
                Transform graphics = transform.Find("Graphics");
                if (graphics != null && graphics.childCount > 0)
                    fpsCharacterRoot = graphics.GetChild(0);
            }

            if (fpsCharacterRoot != null)
                _animator = fpsCharacterRoot.GetComponent<Animator>();

            EnsureDefaultClips();
        }

        void EnsureDefaultClips()
        {
            if (jumpStart != null && jumpLoop != null && jumpEnd != null)
                return;

#if UNITY_EDITOR
            jumpStart ??= LoadEditorClip(
                "Assets/Demo/Animations/Locomotion/Humanoid/InAir/C_JumpStart_Humanoid.fbx",
                "C_JumpStart_Humanoid");
            jumpLoop ??= LoadEditorClip(
                "Assets/Demo/Animations/Locomotion/Humanoid/InAir/C_JumpLoop_Humanoid.fbx",
                "C_JumpLoop_Humanoid");
            jumpEnd ??= LoadEditorClip(
                "Assets/Demo/Animations/Locomotion/Humanoid/InAir/C_JumpEnd_Humanoid.fbx",
                "C_JumpEnd_Humanoid");
#endif
        }

#if UNITY_EDITOR
        static AnimationClip LoadEditorClip(string assetPath, string clipName)
        {
            foreach (Object asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (asset is AnimationClip clip && clip.name == clipName)
                    return clip;
            }

            return null;
        }
#endif

        void OnEnable()
        {
            if (_normalMovement != null)
                _normalMovement.OnJumpPerformed += HandleJumpPerformed;

            if (_characterActor != null)
                _characterActor.OnGroundedStateEnter += HandleLanded;
        }

        void OnDisable()
        {
            if (_normalMovement != null)
                _normalMovement.OnJumpPerformed -= HandleJumpPerformed;

            if (_characterActor != null)
                _characterActor.OnGroundedStateEnter -= HandleLanded;

            StopGraph();
        }

        void Update()
        {
            if (_phase == JumpPhase.None || !_graph.IsValid())
                return;

            if (_characterActor == null || _animator == null)
            {
                StopGraph();
                return;
            }

            switch (_phase)
            {
                case JumpPhase.Start:
                    if (GetNormalizedTime() >= 0.98f)
                        BeginLoop();
                    break;

                case JumpPhase.Loop:
                    if (_characterActor.IsGrounded)
                        BeginEnd();
                    break;

                case JumpPhase.End:
                    if (GetNormalizedTime() >= 0.98f)
                        StopGraph();
                    break;
            }
        }

        void HandleJumpPerformed()
        {
            if (jumpStart == null || _animator == null)
                return;

            _playedSinceLand = true;
            PlayClip(jumpStart, startSpeed, JumpPhase.Start);
        }

        void HandleLanded(Vector3 _)
        {
            if (!_playedSinceLand || jumpEnd == null || _animator == null)
                return;

            _playedSinceLand = false;

            if (_phase == JumpPhase.Loop || _phase == JumpPhase.Start)
                BeginEnd();
        }

        void BeginLoop()
        {
            if (jumpLoop == null)
                return;

            PlayClip(jumpLoop, 1f, JumpPhase.Loop);
        }

        void BeginEnd()
        {
            PlayClip(jumpEnd, endSpeed, JumpPhase.End);
        }

        void PlayClip(AnimationClip clip, float speed, JumpPhase phase)
        {
            if (clip == null || _animator == null)
                return;

            StopGraph();

            _graph = PlayableGraph.Create("ShooterJumpBody");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            _activePlayable = AnimationClipPlayable.Create(_graph, clip);
            _activePlayable.SetSpeed(speed);
            _activePlayable.SetDuration(clip.length / Mathf.Max(speed, 0.01f));

            var output = AnimationPlayableOutput.Create(_graph, "ShooterJumpBodyOutput", _animator);
            output.SetSourcePlayable(_activePlayable);

            _graph.Play();
            _phase = phase;
        }

        float GetNormalizedTime()
        {
            if (!_activePlayable.IsValid())
                return 1f;

            double duration = _activePlayable.GetDuration();
            if (duration <= 0d)
                return 1f;

            return (float)(_activePlayable.GetTime() / duration);
        }

        void StopGraph()
        {
            _phase = JumpPhase.None;

            if (!_graph.IsValid())
                return;

            _graph.Destroy();
        }
    }

}
