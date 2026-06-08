using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SettingsMenu : MonoBehaviour
{
    [Header("Scenes")]
    public string menuSceneName = "MainMenu";

    [Header("UI")]
    public Button backButton;
    public CanvasGroup fadeCanvasGroup;
    public float fadeDuration = 0.4f;

    [Header("Sensitivity")]
    public Slider sensitivitySlider;
    public Text sensitivityValueText;
    public float defaultSensitivity = 200f;
    public float minSensitivity = 50f;
    public float maxSensitivity = 600f;
    const string PREF_SENSITIVITY = "player_sensitivity";

    [Header("World streaming (chunk radius around player)")]
    public Slider worldStreamQualitySlider;
    [Tooltip("Shows preset name and approximate chunk grid size.")]
    public Text worldStreamQualityLabel;

    [Header("Keybinds")]
    public KeyBindButton[] keyBindButtons;

    void Awake()
    {
        ValidateMenuSceneInBuild();
        AutoFindBackButton();
        WireBackButton();
        InitFadeOverlay();
        InitSensitivity();
        InitWorldStreamQuality();
        InitKeybindButtons();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void ValidateMenuSceneInBuild()
    {
        var scenes = Enumerable.Range(0, SceneManager.sceneCountInBuildSettings)
            .Select(i => System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i)))
            .ToArray();

        if (!scenes.Contains(menuSceneName))
            Debug.LogError($"SettingsMenu: Menu scene '{menuSceneName}' is NOT in Build Settings.");
    }

    void AutoFindBackButton()
    {
        if (backButton == null)
        {
            GameObject go = GameObject.Find("BackButton");
            if (go != null)
                backButton = go.GetComponent<Button>();
        }
    }

    void WireBackButton()
    {
        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(OnBackPressed);
        }
        else
        {
            Debug.LogWarning("SettingsMenu: BackButton not assigned or found.");
        }
    }

    void InitFadeOverlay()
    {
        if (fadeCanvasGroup == null)
            return;
        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;
        fadeCanvasGroup.interactable = false;
    }

    void InitSensitivity()
    {
        float saved = NormalizeSavedSensitivity(PlayerPrefs.GetFloat(PREF_SENSITIVITY, defaultSensitivity));
        if (sensitivitySlider != null)
        {
            sensitivitySlider.minValue = minSensitivity;
            sensitivitySlider.maxValue = maxSensitivity;
            sensitivitySlider.value = saved;
            sensitivitySlider.onValueChanged.RemoveAllListeners();
            sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        }

        UpdateSensitivityUI(saved);
        ApplySensitivity(saved);
    }

    void OnSensitivityChanged(float value)
    {
        UpdateSensitivityUI(value);
        ApplySensitivity(value);
        PlayerPrefs.SetFloat(PREF_SENSITIVITY, value);
        PlayerPrefs.Save();
    }

    void UpdateSensitivityUI(float value)
    {
        if (sensitivityValueText != null)
            sensitivityValueText.text = Mathf.RoundToInt(value).ToString();
    }

    void ApplySensitivity(float value)
    {
        SettingsState.MouseSensitivity = value;
    }

    float NormalizeSavedSensitivity(float value)
    {
        if (value <= 5f)
            return Mathf.Lerp(minSensitivity, maxSensitivity, Mathf.Clamp01(value));

        return Mathf.Clamp(value, minSensitivity, maxSensitivity);
    }

    void InitWorldStreamQuality()
    {
        if (worldStreamQualitySlider == null)
            return;

        worldStreamQualitySlider.wholeNumbers = true;
        worldStreamQualitySlider.minValue = 0f;
        worldStreamQualitySlider.maxValue = 3f;
        int q = WorldStreamSettings.LoadQualityIndex();
        worldStreamQualitySlider.SetValueWithoutNotify(q);
        worldStreamQualitySlider.onValueChanged.RemoveAllListeners();
        worldStreamQualitySlider.onValueChanged.AddListener(OnWorldStreamQualityChanged);
        ApplyWorldStreamQuality(q);
    }

    void OnWorldStreamQualityChanged(float value)
    {
        int q = Mathf.RoundToInt(value);
        ApplyWorldStreamQuality(q);
        WorldStreamSettings.SetQualityIndex(q);
    }

    void ApplyWorldStreamQuality(int q)
    {
        q = Mathf.Clamp(q, 0, 3);
        SettingsState.WorldStreamQualityIndex = q;
        if (worldStreamQualityLabel != null)
        {
            int side = 2 * WorldStreamSettings.ChunkRadiusForQuality(q) + 1;
            worldStreamQualityLabel.text = $"{WorldStreamSettings.QualityLabel(q)} (~{side}×{side} chunks)";
        }
    }

    void InitKeybindButtons()
    {
        if (keyBindButtons == null || keyBindButtons.Length == 0)
            return;
        foreach (KeyBindButton kb in keyBindButtons)
            kb.Init();
    }

    public void OnBackPressed()
    {
        if (backButton != null)
            backButton.interactable = false;

        string ret = PlayerPrefs.GetString(RunStatePersistence.KeyReturnScene, "");
        if (!string.IsNullOrEmpty(ret))
            StartCoroutine(FadeThenLoadScene(ret));
        else
            StartCoroutine(FadeThenLoadMenu());
    }

    bool SceneIsInBuild(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return false;
        return Enumerable.Range(0, SceneManager.sceneCountInBuildSettings)
            .Select(i => System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i)))
            .Contains(sceneName);
    }

    IEnumerator FadeThenLoadScene(string sceneName)
    {
        if (!SceneIsInBuild(sceneName))
        {
            Debug.LogError($"SettingsMenu: Scene '{sceneName}' is not in Build Settings.");
            if (backButton != null)
                backButton.interactable = true;
            yield break;
        }

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.blocksRaycasts = true;
            fadeCanvasGroup.interactable = true;
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                fadeCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t / fadeDuration);
                yield return null;
            }

            fadeCanvasGroup.alpha = 1f;
        }

        Time.timeScale = 1f;
        PlayerPrefs.DeleteKey(RunStatePersistence.KeyReturnScene);
        PlayerPrefs.Save();
        SceneManager.LoadScene(sceneName);
    }

    IEnumerator FadeThenLoadMenu()
    {
        PlayerPrefs.SetInt(RunStatePersistence.KeyResumePending, 0);
        PlayerPrefs.DeleteKey(RunStatePersistence.KeyReturnScene);
        PlayerPrefs.Save();

        if (string.IsNullOrEmpty(menuSceneName))
        {
            Debug.LogWarning("SettingsMenu: menuSceneName is empty.");
            yield break;
        }

        if (!SceneIsInBuild(menuSceneName))
        {
            Debug.LogError($"SettingsMenu: Menu scene '{menuSceneName}' not in Build Settings.");
            yield break;
        }

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.blocksRaycasts = true;
            fadeCanvasGroup.interactable = true;
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                fadeCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t / fadeDuration);
                yield return null;
            }

            fadeCanvasGroup.alpha = 1f;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(menuSceneName);
    }

    public void ResetFade()
    {
        if (fadeCanvasGroup == null)
            return;
        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;
        fadeCanvasGroup.interactable = false;
    }
}
