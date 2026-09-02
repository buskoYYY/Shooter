using UnityEngine;

namespace Shooter.Project.Weapons
{
    public static class WeaponPrefabUtility
    {
        public const string IkWeaponBoneName = "IK WeaponBone";

        public static void StripPhysicsComponents(GameObject root)
        {
            if (root == null)
                return;

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    Object.Destroy(colliders[i]);
            }

            Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i] != null)
                    Object.Destroy(bodies[i]);
            }
        }
    }
}
