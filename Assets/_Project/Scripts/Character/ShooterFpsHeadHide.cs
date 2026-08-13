using System.Collections.Generic;
using UnityEngine;

namespace Shooter.Project.Character
{
    /// <summary>
    /// Hides first-person head/face submeshes by swapping their materials for an invisible one.
    /// Keeps head bone scale intact so IK weapon bones and FPS camera parenting stay valid.
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

        HashSet<string> _hiddenNameSet;
        bool _applied;

        void Awake()
        {
            if (characterRoot == null)
            {
                var graphics = transform.Find("Graphics");
                if (graphics != null && graphics.childCount > 0)
                    characterRoot = graphics.GetChild(0);
            }

            _hiddenNameSet = new HashSet<string>(hiddenMaterialNames);
        }

        void Start()
        {
            ApplyHiddenMaterials();
        }

        void ApplyHiddenMaterials()
        {
            if (_applied || characterRoot == null || hiddenMaterial == null)
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

            _applied = true;
        }
    }
}
