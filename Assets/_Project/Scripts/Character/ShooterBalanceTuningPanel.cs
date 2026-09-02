using UnityEngine;

namespace Shooter.Project.Character
{
    /// <summary>
    /// Dev balance panel removed from UI. Kept so existing references to IsOpen compile.
    /// </summary>
    [DisallowMultipleComponent]
    public class ShooterBalanceTuningPanel : MonoBehaviour
    {
        public static bool IsOpen => false;
    }
}
