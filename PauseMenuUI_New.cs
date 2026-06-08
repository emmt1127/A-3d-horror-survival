
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// In-game pause menu that appears when ESC is pressed.
/// This is a UI overlay, NOT a separate scene.
/// </summary>
public class PauseMenuUI_New : MonoBehaviour
{
    [Header("Settings")]
    public float defaultSensitivity = 200f;
    public string menuSceneName = "MainMenu";
    
    // Same PlayerPrefs key as SettingsMenu and controller
    private const string PREF_SENSITIVITY = "player_sensitivity";
    private const string PREF_QUALITY = "render_quality";
    private const string PREF_GRAPHICS_QUALITY = "graphics_quality_mode";

    private GameObject pausePanel;
    private Canvas pauseCanvas;
    private Slider sensitivitySlider;
    private Text sensitivityValueText;
    private Slider qualitySlider;
    private Text qualityValueText;
    private Slider graphicsQualitySlider;
    private Text graphicsQualityValueText;
    private Image graphicsQualityFillImage;
    private Button resumeButton;
    private Button menuButton;

    private bool isPaused = false;
    private controller playerController;
    private int currentGraphicsMode;
    private float fpsTimer;
    private int fpsFrames;
    private float measuredFps = 60f;
    private int lastAppliedGraphicsPreset = -1;
    private int lastAppliedGameQuality = -1;

    void Awake()
    {
        // Find player controller
        playerController = FindObjectOfType<controller>();

        // Create the pause menu UI
        CreatePauseMenu();

        // Hide it initially
        if (pausePanel != null)
            pausePanel.SetActive(false);
    }

    void Update()
    {
        TrackGraphicsAutoQuality();

        // Toggle pause menu with ESC
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
                ResumeGame();
            else
                PauseGame();
        }
    }

    void CreatePauseMenu()
    {
        // Find or create canvas
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("PauseMenuCanvas");
            canvasGO.transform.SetParent(transform);
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            canvasGO.AddComponent<GraphicRaycaster>();
        }

        // Create background panel
        pausePanel = new GameObject("PausePanel");
        pausePanel.transform.SetParent(canvas.transform, false);
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
        contentRT.sizeDelta = new Vector2(500f, 700f);

        // Create title
        CreateTitle(contentGO);

        // Create sensitivity slider
        CreateSensitivitySlider(contentGO);

        // Create one quality slider that controls both graphics pixels and world detail.
        CreateGraphicsQualitySlider(contentGO);

        // Create buttons
        CreateButtons(contentGO);

        // Initialize values
        InitializeValues();
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

        // Add listener
        sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
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

        // Add listener
        qualitySlider.onValueChanged.AddListener(OnQualityChanged);
    }

    void CreateGraphicsQualitySlider(GameObject parent)
    {
        GameObject container = new GameObject("GraphicsQualityContainer");
        container.transform.SetParent(parent.transform, false);
        RectTransform containerRT = container.AddComponent<RectTransform>();
        containerRT.anchorMin = new Vector2(0.5f, 1f);
        containerRT.anchorMax = new Vector2(0.5f, 1f);
        containerRT.pivot = new Vector2(0.5f, 1f);
        containerRT.anchoredPosition = new Vector2(0f, -220f);
        containerRT.sizeDelta = new Vector2(400f, 80f);

        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(container.transform, false);
        RectTransform labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0f, 1f);
        labelRT.anchorMax = new Vector2(1f, 1f);
        labelRT.pivot = new Vector2(0.5f, 1f);
        labelRT.anchoredPosition = Vector2.zero;
        labelRT.sizeDelta = new Vector2(0f, 30f);

        Text labelText = labelGO.AddComponent<Text>();
        labelText.text = "Graphics / Pixels";
        labelText.font = GetDefaultFont();
        labelText.fontSize = 20;
        labelText.alignment = TextAnchor.MiddleLeft;
        labelText.color = Color.white;
        labelText.raycastTarget = false;

        GameObject valueGO = new GameObject("ValueText");
        valueGO.transform.SetParent(container.transform, false);
        RectTransform valueRT = valueGO.AddComponent<RectTransform>();
        valueRT.anchorMin = new Vector2(1f, 1f);
        valueRT.anchorMax = new Vector2(1f, 1f);
        valueRT.pivot = new Vector2(1f, 1f);
        valueRT.anchoredPosition = new Vector2(0f, 0f);
        valueRT.sizeDelta = new Vector2(140f, 30f);

        graphicsQualityValueText = valueGO.AddComponent<Text>();
        graphicsQualityValueText.text = "Auto";
        graphicsQualityValueText.font = GetDefaultFont();
        graphicsQualityValueText.fontSize = 18;
        graphicsQualityValueText.alignment = TextAnchor.MiddleRight;
        graphicsQualityValueText.color = new Color(0.8f, 0.8f, 0.8f);
        graphicsQualityValueText.raycastTarget = false;

        GameObject sliderGO = new GameObject("Slider");
        sliderGO.transform.SetParent(container.transform, false);
        RectTransform sliderRT = sliderGO.AddComponent<RectTransform>();
        sliderRT.anchorMin = new Vector2(0f, 0f);
        sliderRT.anchorMax = new Vector2(1f, 0f);
        sliderRT.pivot = new Vector2(0.5f, 0f);
        sliderRT.anchoredPosition = new Vector2(0f, 10f);
        sliderRT.sizeDelta = new Vector2(0f, 20f);

        graphicsQualitySlider = sliderGO.AddComponent<Slider>();
        graphicsQualitySlider.minValue = 0f;
        graphicsQualitySlider.maxValue = 3f;
        graphicsQualitySlider.wholeNumbers = true;
        graphicsQualitySlider.value = PlayerPrefs.GetInt(PREF_GRAPHICS_QUALITY, 0);

        GameObject backgroundGO = new GameObject("Background");
        backgroundGO.transform.SetParent(sliderGO.transform, false);
        RectTransform backgroundRT = backgroundGO.AddComponent<RectTransform>();
        backgroundRT.anchorMin = Vector2.zero;
        backgroundRT.anchorMax = Vector2.one;
        backgroundRT.sizeDelta = new Vector2(0f, -10f);
        backgroundRT.anchoredPosition = new Vector2(0f, 5f);

        Image backgroundImage = backgroundGO.AddComponent<Image>();
        backgroundImage.color = new Color(0.3f, 0.3f, 0.3f);

        GameObject fillAreaGO = new GameObject("Fill Area");
        fillAreaGO.transform.SetParent(sliderGO.transform, false);
        RectTransform fillAreaRT = fillAreaGO.AddComponent<RectTransform>();
        fillAreaRT.anchorMin = new Vector2(0f, 0f);
        fillAreaRT.anchorMax = new Vector2(1f, 1f);
        fillAreaRT.sizeDelta = new Vector2(-20f, 0f);

        GameObject fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        RectTransform fillRT = fillGO.AddComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.sizeDelta = Vector2.zero;

        graphicsQualityFillImage = fillGO.AddComponent<Image>();
        graphicsQualityFillImage.color = new Color(0.2f, 0.6f, 1f);
        graphicsQualitySlider.fillRect = fillRT;

        GameObject handleGO = new GameObject("Handle");
        handleGO.transform.SetParent(fillAreaGO.transform, false);
        RectTransform handleRT = handleGO.AddComponent<RectTransform>();
        handleRT.sizeDelta = new Vector2(20f, 30f);

        Image handleImage = handleGO.AddComponent<Image>();
        handleImage.color = Color.white;
        graphicsQualitySlider.handleRect = handleRT;
        graphicsQualitySlider.targetGraphic = handleImage;

        graphicsQualitySlider.onValueChanged.AddListener(OnGraphicsQualityChanged);
    }

    void CreateButtons(GameObject parent)
    {
        float buttonY = -330f;
        float buttonHeight = 50f;
        float buttonSpacing = 15f;

        // Resume button
        resumeButton = CreateButton(parent, "RESUME", buttonY, buttonHeight);
        resumeButton.onClick.AddListener(OnResumeClicked);
        buttonY -= buttonHeight + buttonSpacing;

        // Menu button
        menuButton = CreateButton(parent, "GO TO MENU", buttonY, buttonHeight);
        menuButton.onClick.AddListener(OnMenuClicked);
    }

    Button CreateButton(GameObject parent, string text, float y, float height)
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

        return button;
    }

    Font GetDefaultFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null)
            f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return f;
    }

    void InitializeValues()
    {
        // Initialize sensitivity
        if (sensitivitySlider != null)
        {
            float savedSensitivity = PlayerPrefs.GetFloat(PREF_SENSITIVITY, defaultSensitivity);
            sensitivitySlider.value = savedSensitivity;
            UpdateSensitivityText(savedSensitivity);

            // Apply to player controller
            if (playerController != null)
                playerController.mouseSensitivity = savedSensitivity;
        }

        if (graphicsQualitySlider != null)
        {
            currentGraphicsMode = Mathf.Clamp(PlayerPrefs.GetInt(PREF_GRAPHICS_QUALITY, 0), 0, 3);
            graphicsQualitySlider.value = currentGraphicsMode;
            ApplyGraphicsQualityMode(currentGraphicsMode);
        }
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

    void UpdateQualityText(int quality)
    {
        if (qualityValueText != null)
        {
            string[] labels = { "Low", "Medium", "High", "Very High" };
            qualityValueText.text = labels[Mathf.Clamp(quality, 0, 3)];
        }
    }

    void OnGraphicsQualityChanged(float value)
    {
        currentGraphicsMode = Mathf.RoundToInt(value);
        PlayerPrefs.SetInt(PREF_GRAPHICS_QUALITY, currentGraphicsMode);
        PlayerPrefs.Save();
        ApplyGraphicsQualityMode(currentGraphicsMode);
    }

    void TrackGraphicsAutoQuality()
    {
        fpsTimer += Time.unscaledDeltaTime;
        fpsFrames++;

        if (fpsTimer < 0.5f)
            return;

        measuredFps = fpsFrames / Mathf.Max(0.0001f, fpsTimer);
        fpsTimer = 0f;
        fpsFrames = 0;

        if (currentGraphicsMode == 0)
            ApplyGraphicsQualityMode(0);
    }

    void ApplyGraphicsQualityMode(int mode)
    {
        mode = Mathf.Clamp(mode, 0, 3);
        int preset = mode == 0 ? GraphicsPresetFromFps(measuredFps) : mode - 1;
        ApplyGraphicsPreset(preset);
        UpdateGraphicsQualityText(mode, preset);
    }

    int GraphicsPresetFromFps(float fps)
    {
        if (fps < 30f)
            return 0;
        if (fps < 55f)
            return 1;
        return 2;
    }

    void ApplyGraphicsPreset(int preset)
    {
        preset = Mathf.Clamp(preset, 0, 2);
        if (preset != lastAppliedGraphicsPreset)
        {
            int qualityCount = QualitySettings.names != null ? QualitySettings.names.Length : 0;
            if (qualityCount > 0)
            {
                int qualityIndex = preset == 0 ? 0 : (preset == 1 ? Mathf.Max(0, qualityCount / 2) : qualityCount - 1);
                QualitySettings.SetQualityLevel(qualityIndex, true);
            }

            switch (preset)
            {
                case 0:
                    QualitySettings.shadows = ShadowQuality.Disable;
                    QualitySettings.shadowDistance = 12f;
                    QualitySettings.lodBias = 0.35f;
                    QualitySettings.globalTextureMipmapLimit = 2;
                    QualitySettings.antiAliasing = 0;
                    ApplyRenderScale(0.62f);
                    break;
                case 1:
                    QualitySettings.shadows = ShadowQuality.HardOnly;
                    QualitySettings.shadowDistance = 35f;
                    QualitySettings.lodBias = 0.75f;
                    QualitySettings.globalTextureMipmapLimit = 0;
                    QualitySettings.antiAliasing = 2;
                    ApplyRenderScale(0.82f);
                    break;
                default:
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowDistance = 75f;
                    QualitySettings.lodBias = 1.25f;
                    QualitySettings.globalTextureMipmapLimit = 0;
                    QualitySettings.antiAliasing = 4;
                    ApplyRenderScale(1f);
                    break;
            }

            lastAppliedGraphicsPreset = preset;
        }

        ApplyGameQualityForGraphicsPreset(preset);
    }

    void ApplyRenderScale(float scale)
    {
        scale = Mathf.Clamp(scale, 0.5f, 1f);
        ScalableBufferManager.ResizeBuffers(scale, scale);

        foreach (Camera cam in Camera.allCameras)
        {
            if (cam == null)
                continue;
            cam.allowHDR = scale >= 0.8f;
            cam.allowMSAA = scale >= 0.75f;
        }
    }

    void ApplyGameQualityForGraphicsPreset(int preset)
    {
        int gameQuality = Mathf.Clamp(preset, 0, 2);
        if (lastAppliedGameQuality == gameQuality)
            return;

        PlayerPrefs.SetInt(PREF_QUALITY, gameQuality);
        PlayerPrefs.Save();
        WorldStreamSettings.SetQualityIndex(gameQuality);
        lastAppliedGameQuality = gameQuality;
    }

    void UpdateGraphicsQualityText(int mode, int preset)
    {
        string[] presetLabels = { "Low", "Medium", "High" };
        Color[] presetColors =
        {
            new Color(0.95f, 0.24f, 0.18f),
            new Color(0.95f, 0.72f, 0.22f),
            new Color(0.32f, 0.82f, 0.42f)
        };

        preset = Mathf.Clamp(preset, 0, 2);
        if (graphicsQualityValueText != null)
        {
            graphicsQualityValueText.text = mode == 0
                ? $"Auto {presetLabels[preset]}"
                : presetLabels[preset];
            graphicsQualityValueText.color = presetColors[preset];
        }

        if (graphicsQualityFillImage != null)
            graphicsQualityFillImage.color = presetColors[preset];
    }

    void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;

        // Unlock cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Show pause menu
        if (pausePanel != null)
            pausePanel.SetActive(true);
    }

    void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;

        // Lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Hide pause menu
        if (pausePanel != null)
            pausePanel.SetActive(false);
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
}
