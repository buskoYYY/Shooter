using System.Collections.Generic;
using UnityEngine;

namespace Shooter.Project.Character
{
    /// <summary>
    /// Hides first-person head/face submeshes by swapping their materials for an invisible one.
    /// Keeps head bone scale intact so IK weapon bones and FPS camera parenting stay valid.
    /// Can also hide jacket/backpack while climbing so entry anim never shows the chest from outside.
    /// </summary>
    [RequireComponent(typeof(ShooterCharacterController))]
    public class ShooterFpsHeadHide : MonoBehaviour
    {
        [SerializeField] Transform characterRoot;
        [SerializeField] Material hiddenMaterial;
        [SerializeField] string[] hiddenMaterialNames =
        {
            "Head",
            "Hair3",
            "Brows_Leashes",
            "Mouth",
            "Body_Arkit_Eye"
        };

        [Tooltip("Hidden only while on a ladder (entry/climb often peeks the chest).")]
        [SerializeField] string[] ladderHiddenMaterialNames =
        {
            "Jacket1",
            "Backpack2"
        };

        HashSet<string> _hiddenNameSet;
        HashSet<string> _ladderHiddenNameSet;
        bool _ladderBodyHidden;
        readonly Dictionary<Renderer, Material[]> _ladderOriginalMaterials = new Dictionary<Renderer, Material[]>();

        void Awake()
        {
            if (characterRoot == null)
            {
                var graphics = transform.Find("Graphics");
                if (graphics != null && graphics.childCount > 0)
                    characterRoot = graphics.GetChild(0);
            }

            _hiddenNameSet = new HashSet<string>(hiddenMaterialNames);
            _ladderHiddenNameSet = new HashSet<string>(ladderHiddenMaterialNames);
        }

        void Start()
        {
            RefreshHeadHide();
        }

        void OnDisable()
        {
            SetLadderBodyHidden(false);
        }

        /// <summary>
        /// Re-applies hidden head materials. Needed after animator Rebind / ladder FPS restore.
        /// </summary>
        public void RefreshHeadHide()
        {
            ApplyHiddenMaterials();
            if (_ladderBodyHidden)
                ApplyLadderHiddenMaterials();
        }

        public void SetLadderBodyHidden(bool hidden)
        {
            if (hidden == _ladderBodyHidden)
                return;

            if (hidden)
            {
                CacheAndHideLadderMaterials();
                _ladderBodyHidden = true;
                return;
            }

            RestoreLadderMaterials();
            _ladderBodyHidden = false;
            ApplyHiddenMaterials();
        }

        void ApplyHiddenMaterials()
        {
            if (characterRoot == null || hiddenMaterial == null)
                return;

            foreach (var renderer in characterRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var sharedMaterials = renderer.sharedMaterials;
                var materials = renderer.materials;
                bool changed = false;

                for (int i = 0; i < sharedMaterials.Length; i++)
                {
                    var source = sharedMaterials[i];
                    if (source == null || !_hiddenNameSet.Contains(source.name))
                        continue;

                    materials[i] = hiddenMaterial;
                    changed = true;
                }

                if (changed)
                    renderer.materials = materials;
            }
        }

        void CacheAndHideLadderMaterials()
        {
            if (characterRoot == null || hiddenMaterial == null || _ladderHiddenNameSet.Count == 0)
                return;

            _ladderOriginalMaterials.Clear();

            foreach (var renderer in characterRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var sharedMaterials = renderer.sharedMaterials;
                bool needsHide = false;
                for (int i = 0; i < sharedMaterials.Length; i++)
                {
                    var source = sharedMaterials[i];
                    if (source != null && _ladderHiddenNameSet.Contains(source.name))
                    {
                        needsHide = true;
                        break;
                    }
                }

                if (!needsHide)
                    continue;

                Material[] original = renderer.materials;
                _ladderOriginalMaterials[renderer] = original;

                Material[] hidden = (Material[])original.Clone();
                for (int i = 0; i < sharedMaterials.Length; i++)
                {
                    var source = sharedMaterials[i];
                    if (source != null && _ladderHiddenNameSet.Contains(source.name))
                        hidden[i] = hiddenMaterial;
                }

                renderer.materials = hidden;
            }
        }

        void ApplyLadderHiddenMaterials()
        {
            if (characterRoot == null || hiddenMaterial == null)
                return;

            foreach (var renderer in characterRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var sharedMaterials = renderer.sharedMaterials;
                var materials = renderer.materials;
                bool changed = false;

                for (int i = 0; i < sharedMaterials.Length; i++)
                {
                    var source = sharedMaterials[i];
                    if (source == null || !_ladderHiddenNameSet.Contains(source.name))
                        continue;

                    materials[i] = hiddenMaterial;
                    changed = true;
                }

                if (changed)
                    renderer.materials = materials;
            }
        }

        void RestoreLadderMaterials()
        {
            foreach (var pair in _ladderOriginalMaterials)
            {
                if (pair.Key != null)
                    pair.Key.materials = pair.Value;
            }

            _ladderOriginalMaterials.Clear();
        }
    }
}
