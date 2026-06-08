using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persists across loads. Ensures the game-over UI root survives scene changes and is shown on player death.
/// Assign the panel in the inspector, or use a root object named exactly "GameOverPanel".
/// </summary>
public class GameOverManager : MonoBehaviour
{
    public static GameOverManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        Instance = null;
    }

    [Tooltip("The panel to enable on death (often a child of a Canvas).")]
    public GameObject gameOverPanel;

    [Tooltip("If true, unlocks cursor when game over is shown.")]
    public bool unlockCursorOnGameOver = true;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;

        ResolvePanelReference();
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Always rebind to the current scene's panel to avoid carrying old UI roots.
        ResolvePanelReference(forceRefresh: true);
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    void ResolvePanelReference(bool forceRefresh = false)
    {
        if (gameOverPanel != null && !forceRefresh)
            return;

        if (forceRefresh)
            gameOverPanel = null;

        var named = GameObject.Find("GameOverPanel");
        if (named != null)
        {
            gameOverPanel = named;
            return;
        }

        // GameObject.Find does not find inactive objects. Search loaded scene objects too.
        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t.gameObject == null)
                continue;

            if (!t.gameObject.scene.IsValid() || !t.gameObject.scene.isLoaded)
                continue;

            if (t.hideFlags != HideFlags.None)
                continue;

            if (t.name == "GameOverPanel")
            {
                gameOverPanel = t.gameObject;
                return;
            }
        }
    }

    public void ShowGameOver()
    {
        ResolvePanelReference();

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            Transform t = gameOverPanel.transform;
            t.SetAsLastSibling();
        }
        else
            Debug.LogError("GameOverManager: No game over panel assigned or found. Assign it in the inspector or name the panel root GameObject 'GameOverPanel'.");

        Time.timeScale = 0f;

        if (unlockCursorOnGameOver)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void HideGameOver()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        Time.timeScale = 1f;
    }

    public void Retry()
    {
        HideGameOver();
        
        // Ensure player is re-enabled before retrying
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            // Fallback: find by controller component
            controller ctrl = FindObjectOfType<controller>();
            if (ctrl != null)
                player = ctrl.gameObject;
        }
        
        if (player != null)
        {
            Debug.Log($"GameOverManager.Retry: Found player, activeSelf={player.activeSelf}");
            player.SetActive(true);
            controller ctrl = player.GetComponent<controller>();
            if (ctrl != null)
            {
                ctrl.enabled = true;
                Debug.Log("GameOverManager.Retry: Enabled controller component");
            }
            Health h = player.GetComponent<Health>();
            if (h != null)
            {
                h.enabled = true;
                Debug.Log("GameOverManager.Retry: Enabled Health component");
            }
        }
        else
        {
            Debug.LogError("GameOverManager.Retry: No GameObject with tag 'Player' or controller component found!");
        }
        
        Scene current = SceneManager.GetActiveScene();
        SceneManager.LoadScene(current.name);
    }

    public void BackToMenu()
    {
        BackToMenu("MainMenu");
    }

    public void BackToMenu(string menuSceneName = "MainMenu")
    {
        HideGameOver();
        string resolved = ResolveMenuSceneName(menuSceneName);
        if (string.IsNullOrEmpty(resolved))
        {
            Debug.LogError("GameOverManager.BackToMenu: No valid menu scene found in Build Settings.");
            return;
        }

        SceneManager.LoadScene(resolved);
    }

    string ResolveMenuSceneName(string requested)
    {
        if (!string.IsNullOrWhiteSpace(requested) && SceneIsInBuild(requested))
            return requested;

        if (SceneIsInBuild("MainMenu"))
            return "MainMenu";

        if (SceneIsInBuild("Main Menu"))
            return "Main Menu";

        return null;
    }

    bool SceneIsInBuild(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return false;

        int total = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < total; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (string.IsNullOrEmpty(path))
                continue;

            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (name == sceneName)
                return true;
        }

        return false;
    }
}
