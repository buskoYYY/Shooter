using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Shooter.Project.Input
{
    /// <summary>
    /// Replaces legacy StandaloneInputModule on scene EventSystems at load time.
    /// Project uses Input System only; demo scenes ship with the old UI input module.
    /// </summary>
    static class ShooterEventSystemInputFix
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void FixEventSystemsInLoadedScene()
        {
            var eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            foreach (var eventSystem in eventSystems)
                UpgradeEventSystem(eventSystem.gameObject);
        }

        static void UpgradeEventSystem(GameObject eventSystemObject)
        {
            var standalone = eventSystemObject.GetComponent<StandaloneInputModule>();
            if (standalone == null)
                return;

            Object.Destroy(standalone);

            if (eventSystemObject.GetComponent<InputSystemUIInputModule>() == null)
                eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }
    }
}
