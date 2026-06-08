
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// In-game pause menu that appears when ESC is pressed.
/// Allows adjusting sensitivity, render quality (chunk count), and provides resume/menu options.
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject pausePanel;
    public Button resumeButton;
    public Button menuButton;
    public Slider sensitivitySlider;
    public Text sensitivityValueText;
    public Slider qualitySlider;
    public Text qualityValueText;

    [Header("Settings")]
    public float defaultSensitivity = 200f;
    public string menuSceneName = "MainMenu";

    [Header("Day/Night Timer")]
    public Text dayNightTimerText;

    private bool isPaused = false;
    private controller playerController;
    private DayNightCycle dayNightCycle;
    private Canvas pauseCanvas;

    // PlayerPrefs keys
    private const string PREF_SENSITIVITY = "player_sensitivity";
    private const string PREF_QUALITY = "render_quality";

    void Start()
    {
        // Find references
        playerController = FindObjectOfType<controller>();
        dayNightCycle = FindObjectOfType<DayNightCycle>();

        // Create canvas if needed
        CreatePauseCanvas();

        // Initialize UI
        InitializeUI();

        // Hide pause menu initially
        HidePauseMenu();
    }

    void Update()
    {
        // Toggle pause menu with ESC
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
                ResumeGame();
            else
                PauseGame();
        }

        // Update day/night timer if visible
        if (isPaused && dayNightTimerText != null && dayNightCycle != null)
        {
            UpdateDayNightTimer();
        }
    }

    void CreatePauseCanvas()
    {
        if (pausePanel != null)
            return;

        // Create canvas
        GameObject canvasGO = new GameObject("PauseMenuCanvas");
        canvasGO.transform.SetParent(transform);
        pauseCanvas = canvasGO.AddComponent<Canvas>();
        pauseCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        pauseCanvas.sortingOrder = 1000;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasGO.AddComponent<GraphicRaycaster>();

        // Create background panel
        pausePanel = new GameObject("PausePanel");
        pausePanel.transform.SetParent(canvasGO.transform, false);
        RectTransform panelRT = pausePanel.AddComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.sizeDelta = Vector2.zero;

        Image panelImage = pausePanel.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.85f);

        // Create content container
        GameObject contentGO = new GameObject("Content");
        contentGO.transform.SetParent(pausePanel.transform, false);
        RectTransform contentRT = contentGO.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0.5f, 0.5f);
        contentRT.anchorMax = new Vector2(0.5f, 0.5f);
        contentRT.pivot = new Vector2(0.5f, 0.5f);
        contentRT.anchoredPosition = Vector2.zero;
        contentRT.sizeDelta = new Vector2(500f, 600f);

        // Create title
        CreateTitle(contentGO);

        // Create sensitivity slider
        CreateSensitivitySlider(contentGO);

        // Create quality slider
        CreateQualitySlider(contentGO);

        // Create buttons
        CreateButtons(contentGO);
    }

    void CreateTitle(GameObject parent)
    {
        GameObject titleGO = new GameObject("Title");
        titleGO.transform.SetParent(parent.transform, false);
        RectTransform titleRT = titleGO.AddComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 1f);
        titleRT.anchorMax = new Vector2(0.5f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0f, -30f);
        titleRT.sizeDelta = new Vector2(400f, 60f);

        Text titleText = titleGO.AddComponent<Text>();
        titleText.text = "SETTINGS";
        titleText.font = GetDefaultFont();
        titleText.fontSize = 36;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.white;
        titleText.raycastTarget = false;
    }

    void CreateSensitivitySlider(GameObject parent)
    {
        // Container
        GameObject container = new GameObject("SensitivityContainer");
        container.transform.SetParent(parent.transform, false);
        RectTransform containerRT = container.AddComponent<RectTransform>();
        containerRT.anchorMin = new Vector2(0.5f, 1f);
        containerRT.anchorMax = new Vector2(0.5f, 1f);
        containerRT.pivot = new Vector2(0.5f, 1f);
        containerRT.anchoredPosition = new Vector2(0f, -120f);
        containerRT.sizeDelta = new Vector2(400f, 80f);

        // Label
        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(container.transform, false);
        RectTransform labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0f, 1f);
        labelRT.anchorMax = new Vector2(1f, 1f);
        labelRT.pivot = new Vector2(0.5f, 1f);
        labelRT.anchoredPosition = Vector2.zero;
        labelRT.sizeDelta = new Vector2(0f, 30f);

        Text labelText = labelGO.AddComponent<Text>();
        labelText.text = "Mouse Sensitivity";
        labelText.font = GetDefaultFont();
        labelText.fontSize = 20;
        labelText.alignment = TextAnchor.MiddleLeft;
        labelText.color = Color.white;
        labelText.raycastTarget = false;

        // Value text
        GameObject valueGO = new GameObject("ValueText");
        valueGO.transform.SetParent(container.transform, false);
        RectTransform valueRT = valueGO.AddComponent<RectTransform>();
        valueRT.anchorMin = new Vector2(1f, 1f);
        valueRT.anchorMax = new Vector2(1f, 1f);
        valueRT.pivot = new Vector2(1f, 1f);
        valueRT.anchoredPosition = new Vector2(0f, 0f);
        valueRT.sizeDelta = new Vector2(80f, 30f);

        sensitivityValueText = valueGO.AddComponent<Text>();
        sensitivityValueText.text = "100";
        sensitivityValueText.font = GetDefaultFont();
        sensitivityValueText.fontSize = 18;
        sensitivityValueText.alignment = TextAnchor.MiddleRight;
        sensitivityValueText.color = new Color(0.8f, 0.8f, 0.8f);
        sensitivityValueText.raycastTarget = false;

        // Slider
        GameObject sliderGO = new GameObject("Slider");
        sliderGO.transform.SetParent(container.transform, false);
        RectTransform sliderRT = sliderGO.AddComponent<RectTransform>();
        sliderRT.anchorMin = new Vector2(0f, 0f);
        sliderRT.anchorMax = new Vector2(1f, 0f);
        sliderRT.pivot = new Vector2(0.5f, 0f);
        sliderRT.anchoredPosition = new Vector2(0f, 10f);
        sliderRT.sizeDelta = new Vector2(0f, 20f);

        sensitivitySlider = sliderGO.AddComponent<Slider>();
        sensitivitySlider.minValue = 10f;
        sensitivitySlider.maxValue = 600f;
        sensitivitySlider.value = PlayerPrefs.GetFloat(PREF_SENSITIVITY, defaultSensitivity);

        // Background
        GameObject backgroundGO = new GameObject("Background");
        backgroundGO.transform.SetParent(sliderGO.transform, false);
        RectTransform backgroundRT = backgroundGO.AddComponent<RectTransform>();
        backgroundRT.anchorMin = Vector2.zero;
        backgroundRT.anchorMax = Vector2.one;
        backgroundRT.sizeDelta = new Vector2(0f, -10f);
        backgroundRT.anchoredPosition = new Vector2(0f, 5f);

        Image backgroundImage = backgroundGO.AddComponent<Image>();
        backgroundImage.color = new Color(0.3f, 0.3f, 0.3f);

        // Fill area
        GameObject fillAreaGO = new GameObject("Fill Area");
        fillAreaGO.transform.SetParent(sliderGO.transform, false);
        RectTransform fillAreaRT = fillAreaGO.AddComponent<RectTransform>();
        fillAreaRT.anchorMin = new Vector2(0f, 0f);
        fillAreaRT.anchorMax = new Vector2(1f, 1f);
        fillAreaRT.sizeDelta = new Vector2(-20f, 0f);

        // Fill
        GameObject fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        RectTransform fillRT = fillGO.AddComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.sizeDelta = Vector2.zero;

        Image fillImage = fillGO.AddComponent<Image>();
        fillImage.color = new Color(0.2f, 0.6f, 1f);
        sensitivitySlider.fillRect = fillRT;

        // Handle
        GameObject handleGO = new GameObject("Handle");
        handleGO.transform.SetParent(fillAreaGO.transform, false);
        RectTransform handleRT = handleGO.AddComponent<RectTransform>();
        handleRT.sizeDelta = new Vector2(20f, 30f);

        Image handleImage = handleGO.AddComponent<Image>();
        handleImage.color = Color.white;
        sensitivitySlider.handleRect = handleRT;
        sensitivitySlider.targetGraphic = handleImage;
    }

    void CreateQualitySlider(GameObject parent)
    {
        // Container
        GameObject container = new GameObject("QualityContainer");
        container.transform.SetParent(parent.transform, false);
        RectTransform containerRT = container.AddComponent<RectTransform>();
        containerRT.anchorMin = new Vector2(0.5f, 1f);
        containerRT.anchorMax = new Vector2(0.5f, 1f);
        containerRT.pivot = new Vector2(0.5f, 1f);
        containerRT.anchoredPosition = new Vector2(0f, -220f);
        containerRT.sizeDelta = new Vector2(400f, 80f);

        // Label
        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(container.transform, false);
        RectTransform labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0f, 1f);
        labelRT.anchorMax = new Vector2(1f, 1f);
        labelRT.pivot = new Vector2(0.5f, 1f);
        labelRT.anchoredPosition = Vector2.zero;
        labelRT.sizeDelta = new Vector2(0f, 30f);

        Text labelText = labelGO.AddComponent<Text>();
        labelText.text = "Render Quality (Chunks)";
        labelText.font = GetDefaultFont();
        labelText.fontSize = 20;
        labelText.alignment = TextAnchor.MiddleLeft;
        labelText.color = Color.white;
        labelText.raycastTarget = false;

        // Value text
        GameObject valueGO = new GameObject("ValueText");
        valueGO.transform.SetParent(container.transform, false);
        RectTransform valueRT = valueGO.AddComponent<RectTransform>();
        valueRT.anchorMin = new Vector2(1f, 1f);
        valueRT.anchorMax = new Vector2(1f, 1f);
        valueRT.pivot = new Vector2(1f, 1f);
        valueRT.anchoredPosition = new Vector2(0f, 0f);
        valueRT.sizeDelta = new Vector2(80f, 30f);

        qualityValueText = valueGO.AddComponent<Text>();
        qualityValueText.text = "Low";
        qualityValueText.font = GetDefaultFont();
        qualityValueText.fontSize = 18;
        qualityValueText.alignment = TextAnchor.MiddleRight;
        qualityValueText.color = new Color(0.8f, 0.8f, 0.8f);
        qualityValueText.raycastTarget = false;

        // Slider
        GameObject sliderGO = new GameObject("Slider");
        sliderGO.transform.SetParent(container.transform, false);
        RectTransform sliderRT = sliderGO.AddComponent<RectTransform>();
        sliderRT.anchorMin = new Vector2(0f, 0f);
        sliderRT.anchorMax = new Vector2(1f, 0f);
        sliderRT.pivot = new Vector2(0.5f, 0f);
        sliderRT.anchoredPosition = new Vector2(0f, 10f);
        sliderRT.sizeDelta = new Vector2(0f, 20f);

        qualitySlider = sliderGO.AddComponent<Slider>();
        qualitySlider.minValue = 0f;
        qualitySlider.maxValue = 3f;
        qualitySlider.wholeNumbers = true;
        qualitySlider.value = PlayerPrefs.GetInt(PREF_QUALITY, 0);

        // Background
        GameObject backgroundGO = new GameObject("Background");
        backgroundGO.transform.SetParent(sliderGO.transform, false);
        RectTransform backgroundRT = backgroundGO.AddComponent<RectTransform>();
        backgroundRT.anchorMin = Vector2.zero;
        backgroundRT.anchorMax = Vector2.one;
        backgroundRT.sizeDelta = new Vector2(0f, -10f);
        backgroundRT.anchoredPosition = new Vector2(0f, 5f);

        Image backgroundImage = backgroundGO.AddComponent<Image>();
        backgroundImage.color = new Color(0.3f, 0.3f, 0.3f);

        // Fill area
        GameObject fillAreaGO = new GameObject("Fill Area");
        fillAreaGO.transform.SetParent(sliderGO.transform, false);
        RectTransform fillAreaRT = fillAreaGO.AddComponent<RectTransform>();
        fillAreaRT.anchorMin = new Vector2(0f, 0f);
        fillAreaRT.anchorMax = new Vector2(1f, 1f);
        fillAreaRT.sizeDelta = new Vector2(-20f, 0f);

        // Fill
        GameObject fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        RectTransform fillRT = fillGO.AddComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.sizeDelta = Vector2.zero;

        Image fillImage = fillGO.AddComponent<Image>();
        fillImage.color = new Color(0.2f, 0.6f, 1f);
        qualitySlider.fillRect = fillRT;

        // Handle
        GameObject handleGO = new GameObject("Handle");
        handleGO.transform.SetParent(fillAreaGO.transform, false);
        RectTransform handleRT = handleGO.AddComponent<RectTransform>();
        handleRT.sizeDelta = new Vector2(20f, 30f);

        Image handleImage = handleGO.AddComponent<Image>();
        handleImage.color = Color.white;
        qualitySlider.handleRect = handleRT;
        qualitySlider.targetGraphic = handleImage;
    }

    void CreateButtons(GameObject parent)
    {
        float buttonY = -320f;
        float buttonHeight = 50f;
        float buttonSpacing = 15f;

        // Resume button
        CreateButton(parent, "RESUME", buttonY, buttonHeight, OnResumeClicked);
        buttonY -= buttonHeight + buttonSpacing;

        // Menu button
        CreateButton(parent, "GO TO MENU", buttonY, buttonHeight, OnMenuClicked);
    }

    void CreateButton(GameObject parent, string text, float y, float height, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonGO = new GameObject(text.Replace(" ", "") + "Button");
        buttonGO.transform.SetParent(parent.transform, false);
        RectTransform buttonRT = buttonGO.AddComponent<RectTransform>();
        buttonRT.anchorMin = new Vector2(0.5f, 1f);
        buttonRT.anchorMax = new Vector2(0.5f, 1f);
        buttonRT.pivot = new Vector2(0.5f, 1f);
        buttonRT.anchoredPosition = new Vector2(0f, y);
        buttonRT.sizeDelta = new Vector2(300f, height);

        Image buttonImage = buttonGO.AddComponent<Image>();
        buttonImage.color = new Color(0.2f, 0.2f, 0.2f);

        Button button = buttonGO.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(onClick);

        // Button text
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(buttonGO.transform, false);
        RectTransform textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;

        Text buttonText = textGO.AddComponent<Text>();
        buttonText.text = text;
        buttonText.font = GetDefaultFont();
        buttonText.fontSize = 18;
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.color = Color.white;
        buttonText.raycastTarget = false;
    }

    Font GetDefaultFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null)
            f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return f;
    }

    void InitializeUI()
    {
        // Initialize sensitivity
        if (sensitivitySlider != null)
        {
            sensitivitySlider.value = PlayerPrefs.GetFloat(PREF_SENSITIVITY, defaultSensitivity);
            sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
            UpdateSensitivityText(sensitivitySlider.value);
        }

        // Initialize quality
        if (qualitySlider != null)
        {
            qualitySlider.value = PlayerPrefs.GetInt(PREF_QUALITY, 0);
            qualitySlider.onValueChanged.AddListener(OnQualityChanged);
            UpdateQualityText(Mathf.RoundToInt(qualitySlider.value));
        }

        // Initialize buttons
        if (resumeButton != null)
            resumeButton.onClick.AddListener(OnResumeClicked);

        if (menuButton != null)
            menuButton.onClick.AddListener(OnMenuClicked);
    }

    void OnSensitivityChanged(float value)
    {
        UpdateSensitivityText(value);
        PlayerPrefs.SetFloat(PREF_SENSITIVITY, value);
        PlayerPrefs.Save();

        // Apply to player controller
        if (playerController != null)
            playerController.mouseSensitivity = value;
    }

    void UpdateSensitivityText(float value)
    {
        if (sensitivityValueText != null)
            sensitivityValueText.text = Mathf.RoundToInt(value).ToString();
    }

    void OnQualityChanged(float value)
    {
        int quality = Mathf.RoundToInt(value);
        UpdateQualityText(quality);
        PlayerPrefs.SetInt(PREF_QUALITY, quality);
        PlayerPrefs.Save();

        // Apply quality setting to world streaming
        WorldStreamSettings.SetQualityIndex(quality);
    }
    
    void OnQualityChangedInt(int value)
    {
        UpdateQualityText(value);
        PlayerPrefs.SetInt(PREF_QUALITY, value);
        PlayerPrefs.Save();

        // Apply quality setting to world streaming
        WorldStreamSettings.SetQualityIndex(value);
    }

    void UpdateQualityText(int quality)
    {
        if (qualityValueText != null)
        {
            string[] labels = { "Low", "Medium", "High", "Very High" };
            qualityValueText.text = labels[Mathf.Clamp(quality, 0, 3)];
        }
    }

    void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;

        // Unlock cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Show pause menu
        ShowPauseMenu();

        // Update day/night timer
        UpdateDayNightTimer();
    }

    void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;

        // Lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Hide pause menu
        HidePauseMenu();
    }

    void OnResumeClicked()
    {
        ResumeGame();
    }

    void OnMenuClicked()
    {
        // Reset time scale
        Time.timeScale = 1f;

        // Clear saved run state to reset game
        PlayerPrefs.DeleteKey(RunStatePersistence.KeyReturnScene);
        PlayerPrefs.SetInt(RunStatePersistence.KeyResumePending, 0);
        PlayerPrefs.Save();

        // Load menu scene
        SceneManager.LoadScene(menuSceneName);
    }

    void ShowPauseMenu()
    {
        if (pausePanel != null)
            pausePanel.SetActive(true);
    }

    void HidePauseMenu()
    {
        if (pausePanel != null)
            pausePanel.SetActive(false);
    }

    void UpdateDayNightTimer()
    {
        if (dayNightTimerText == null || dayNightCycle == null)
            return;

        float remaining = dayNightCycle.CurrentPhaseRemaining;
        string phase = dayNightCycle.IsDay ? "Day" : "Night";

        int minutes = Mathf.FloorToInt(remaining / 60f);
        int seconds = Mathf.FloorToInt(remaining % 60f);

        dayNightTimerText.text = $"{phase} ends in {minutes:00}:{seconds:00}";
    }
}
