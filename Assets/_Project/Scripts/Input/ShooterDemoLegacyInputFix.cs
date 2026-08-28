using Lightbug.CharacterControllerPro.Demo;
using UnityEngine;

namespace Shooter.Project.Input
{
    /// <summary>
    /// Disables CCP demo helpers that conflict with Shooter FPS player (legacy Input, demo camera, etc.).
    /// </summary>
    static class ShooterDemoLegacyInputFix
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void DisableLegacyDemoComponents()
        {
            foreach (var manager in Object.FindObjectsByType<DemoSceneManager>(FindObjectsSortMode.None))
                manager.enabled = false;

            foreach (var menu in Object.FindObjectsByType<MainMenuManager>(FindObjectsSortMode.None))
                menu.enabled = false;

            foreach (var camera3D in Object.FindObjectsByType<Camera3D>(FindObjectsSortMode.None))
            {
                camera3D.enabled = false;
                var cam = camera3D.GetComponent<Camera>();
                if (cam != null)
                    cam.enabled = false;
                var listener = camera3D.GetComponent<AudioListener>();
                if (listener != null)
                    listener.enabled = false;
            }
        }
    }
}