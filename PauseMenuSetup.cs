
using UnityEngine;

/// <summary>
/// Automatically sets up the pause menu system when added to the scene.
/// Attach this to a GameObject in your game scene to enable the pause menu.
/// </summary>
public class PauseMenuSetup : MonoBehaviour
{
    [Header("Settings")]
    public string menuSceneName = "MainMenu";
    public float defaultSensitivity = 200f;

    void Awake()
    {
        // Find or create PauseMenuUI_New
        PauseMenuUI_New pauseMenu = FindObjectOfType<PauseMenuUI_New>();

        if (pauseMenu == null)
        {
            GameObject pauseMenuGO = new GameObject("PauseMenuUI");
            pauseMenuGO.transform.SetParent(transform);
            pauseMenu = pauseMenuGO.AddComponent<PauseMenuUI_New>();
        }

        // Configure settings
        pauseMenu.menuSceneName = menuSceneName;
        pauseMenu.defaultSensitivity = defaultSensitivity;

        // Ensure EventSystem exists
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            GameObject eventSystemGO = new GameObject("EventSystem");
            eventSystemGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
    }
}
