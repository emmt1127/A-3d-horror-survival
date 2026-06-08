using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Add to the player. ESC opens the Settings scene, saves run state, and returns here via Settings Back.
/// </summary>
public class GameplayPauseSettings : MonoBehaviour
{
    [Tooltip("Scene to load when pressing ESC during gameplay.")]
    public string settingsSceneName = "Settings";

    [Tooltip("Scenes where ESC should NOT open settings (main menu, etc.).")]
    public string[] blockInScenes = { "MainMenu", "Main Menu", "Settings" };

    public bool ShouldPauseToSettings()
    {
        string n = SceneManager.GetActiveScene().name;
        if (blockInScenes == null)
            return true;
        foreach (string s in blockInScenes)
        {
            if (!string.IsNullOrEmpty(s) && s == n)
                return false;
        }

        return true;
    }

    public void OpenSettingsAndSave()
    {
        if (string.IsNullOrEmpty(settingsSceneName))
        {
            Debug.LogWarning("GameplayPauseSettings: settings scene name is empty.", this);
            return;
        }

        RunStatePersistence.SaveFromPlayer(transform.root.gameObject);
        Time.timeScale = 1f;
        SceneManager.LoadScene(settingsSceneName);
    }
}
