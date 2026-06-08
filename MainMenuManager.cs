using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    struct ClassShopItem
    {
        public string Name;
        public string Advantage;
        public string Downside;
        public bool Available;
        public int RequiredPersonalBestDays;
        public int GemCost;
        public int StarRating;

        public ClassShopItem(string name, string advantage, string downside, bool available = true, int requiredPersonalBestDays = 0, int gemCost = 0, int starRating = 1)
        {
            Name = name;
            Advantage = advantage;
            Downside = downside;
            Available = available;
            RequiredPersonalBestDays = requiredPersonalBestDays;
            GemCost = gemCost;
            StarRating = starRating;
        }
    }

    static readonly ClassShopItem[] ClassShopItems =
    {
        new ClassShopItem("Noob", "No advantages.", "No disadvantages.", true, 0, 0, 1),
        new ClassShopItem("Camper", "Spawns with a flashlight.", "No downside listed yet.", true, 0, 10, 1),
        new ClassShopItem("Lumberjack", "Spawns with a good axe and gets more materials from trees.", "Slower movement speed.", true, 0, 20, 2),
        new ClassShopItem("Cook", "Spawns with some good food.", "Lower health.", true, 0, 25, 2),
        new ClassShopItem("Medic", "Spawns with bandages and can revive once automatically.", "Only gets 7 inventory slots instead of 10.", true, 0, 40, 3),
        new ClassShopItem("Farmer", "You get 5 farm plots on spawn.", "Less health, and health regen is slower.", false, 50, 55, 3),
        new ClassShopItem("Mapper", "You get a big map, and your teammates can see where you are.", "Slower movement speed.", false, 50, 70, 4),
        new ClassShopItem("Feline", "You get 9 lives that you can respawn with, and you have faster movement speed.", "Lower health (85% of regular players), smaller inventory space (8 slots), and hunger drains faster.", false, 80, 100, 4),
        new ClassShopItem("Witch", "Spawns with 4 potions (speed, health, hunger, and melee damage) and a cauldron that can be placed around camp. Only the Witch can use the cauldron.", "None.", false, 125, 110, 5),
        new ClassShopItem("Arsonist", "Spawns with grenades, molotovs, flares, and bear traps. Does crazy damage.", "Overall weaker, and gets more hungry than other classes.", false, 160, 125, 5)
    };

    const string PurchasedClassPrefix = "class_purchased_";
    const string EquippedClassKey = "equipped_class";

    [Header("Scenes")]
    [Tooltip("Exact name of the gameplay scene to load")]
    public string gameSceneName = "GameScene";
    [Tooltip("Exact name of the main menu scene")]
    public string menuSceneName = "MainMenu";
    [Tooltip("Exact name of the settings scene")]
    public string settingsSceneName = "Settings";

    [Header("UI References")]
    public GameObject mainMenuPanel;
    public Button playButton;
    public Button settingsButton; // NEW: Settings button reference
    public Button quitButton;
    public CanvasGroup fadeCanvasGroup; // optional full-screen overlay for fade
    public float fadeDuration = 0.5f;

    [Header("Shop Button")]
    public Sprite shopButtonSprite;

    GameObject playOptionsPanel;
    GameObject shopPanel;
    Button soloPlayButton;
    Button multiplayerLobbyButton;
    Button shopButton;
    Button shopBackButton;
    Button[] classActionButtons;
    TextMeshProUGUI[] classActionTexts;
    TextMeshProUGUI gemBalanceText;
    Texture2D[] classIconTextures;
    Sprite[] classIconSprites;
    Texture2D filledStarTexture;
    Texture2D emptyStarTexture;
    Sprite filledStarSprite;
    Sprite emptyStarSprite;
    GameObject titleObject;

    void Awake()
    {
        // Validate scenes in build settings and auto-find UI if needed
        ValidateScenesInBuild();
        AutoFindButtons();
        WireButtonListeners();
        InitFadeOverlay();
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        EnsureShopButton();
        EnsurePlayOptionsPanel();
        EnsureShopPanel();
        ShowMainMenuPanel();

        // Ensure cursor visible in menu
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void ValidateScenesInBuild()
    {
        var scenes = Enumerable.Range(0, SceneManager.sceneCountInBuildSettings)
            .Select(i => System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i)))
            .ToArray();

        if (!scenes.Contains(gameSceneName))
            Debug.LogError($"MainMenuManager: Game scene '{gameSceneName}' is NOT in Build Settings.");
        if (!scenes.Contains(menuSceneName))
            Debug.LogWarning($"MainMenuManager: Menu scene '{menuSceneName}' is NOT in Build Settings.");
        if (!scenes.Contains(settingsSceneName))
            Debug.LogWarning($"MainMenuManager: Settings scene '{settingsSceneName}' is NOT in Build Settings.");
    }

    void AutoFindButtons()
    {
        if (playButton == null)
        {
            var go = GameObject.Find("PlayButton") ?? GameObject.Find("PLAY");
            if (go != null) playButton = go.GetComponent<Button>();
        }
        if (settingsButton == null)
        {
            var go = GameObject.Find("SettingsButton") ?? GameObject.Find("SETTINGS");
            if (go != null) settingsButton = go.GetComponent<Button>();
        }
        if (quitButton == null)
        {
            var go = GameObject.Find("QuitButton") ?? GameObject.Find("QUIT");
            if (go != null) quitButton = go.GetComponent<Button>();
        }

        if (titleObject == null)
        {
            var go = GameObject.Find("Title");
            if (go != null) titleObject = go;
        }
    }

    void WireButtonListeners()
    {
        if (playButton != null)
        {
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(OnPlayPressed);
        }
        else Debug.LogWarning("MainMenuManager: PlayButton not assigned or found.");

        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveAllListeners();
            settingsButton.onClick.AddListener(OnSettingsPressed);
        }
        else Debug.LogWarning("MainMenuManager: SettingsButton not assigned or found.");

        if (quitButton != null)
        {
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(QuitGame);
        }
        else Debug.LogWarning("MainMenuManager: QuitButton not assigned or found.");
    }

    void WireShopButton()
    {
        if (shopButton == null)
            return;

        shopButton.onClick.RemoveAllListeners();
        shopButton.onClick.AddListener(OnShopPressed);
    }

    void InitFadeOverlay()
    {
        if (fadeCanvasGroup == null) return;
        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;
        fadeCanvasGroup.interactable = false;
    }

    bool SceneIsInBuild(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return false;

        return Enumerable.Range(0, SceneManager.sceneCountInBuildSettings)
            .Select(i => System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i)))
            .Contains(sceneName);
    }

    // Called by Play button
    public void OnPlayPressed()
    {
        ShowPlayOptionsPanel();
    }

    public void OnSoloPlayPressed()
    {
        SetMainMenuButtonsInteractable(false);
        SetPlayOptionsButtonsInteractable(false);
        StartCoroutine(FadeThenLoadScene(gameSceneName));
    }

    public void OnMultiplayerLobbyPressed()
    {
        Debug.Log("MainMenuManager: Multiplayer Lobby selected. Lobby screen will be implemented later.");
    }

    public void OnShopPressed()
    {
        ShowShopPanel();
    }

    // Called by Settings button
    public void OnSettingsPressed()
    {
        SetMainMenuButtonsInteractable(false);
        SetPlayOptionsButtonsInteractable(false);
        StartCoroutine(FadeThenLoadScene(settingsSceneName));
    }

    // Public method to return to menu scene
    public void BackToMenu()
    {
        if (SceneManager.GetActiveScene().name == menuSceneName)
        {
            ResetFade();
            ShowMainMenuPanel();
            return;
        }
        StartCoroutine(FadeThenLoadScene(menuSceneName));
    }

    // Quit application, stops Play mode in Editor
    public void QuitGame()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }

    IEnumerator FadeThenLoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("MainMenuManager: Cannot load scene because the scene name is empty. Check the gameSceneName/settingsSceneName fields in the inspector.");
            yield break;
        }

        // If scene not in build settings, abort with clear message
        if (!SceneIsInBuild(sceneName))
        {
            Debug.LogError($"MainMenuManager: Scene '{sceneName}' not in Build Settings. Add it under File -> Build Settings.");
            yield break;
        }

        // If no fade overlay assigned, load immediately
        if (fadeCanvasGroup == null)
        {
            Debug.Log("MainMenuManager: Loading scene without fade: " + sceneName);
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
            yield break;
        }

        // Block input and fade to full
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

        // Load scene asynchronously
        Time.timeScale = 1f;
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;
    }

    // Reset fade overlay so menu is visible again
    public void ResetFade()
    {
        if (fadeCanvasGroup == null) return;
        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;
        fadeCanvasGroup.interactable = false;
    }

    // Safe loader you can call from other scripts or inspector
    public void SafeLoadScene(string sceneName)
    {
        StartCoroutine(SafeLoadCoroutine(sceneName));
    }

    void ShowMainMenuPanel()
    {
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);
        if (playOptionsPanel != null)
            playOptionsPanel.SetActive(false);
        if (shopPanel != null)
            shopPanel.SetActive(false);

        SetMainMenuObjectsVisible(true);
        SetShopButtonBackdropVisible(true);
        SetMainMenuButtonsInteractable(true);
        SetPlayOptionsButtonsInteractable(true);
    }

    void ShowPlayOptionsPanel()
    {
        EnsurePlayOptionsPanel();

        SetMainMenuObjectsVisible(false);
        SetShopButtonBackdropVisible(false);
        if (playOptionsPanel != null)
            playOptionsPanel.SetActive(true);

        SetMainMenuButtonsInteractable(true);
        SetPlayOptionsButtonsInteractable(true);
    }

    void ShowShopPanel()
    {
        EnsureShopPanel();

        SetMainMenuObjectsVisible(false);
        SetShopButtonBackdropVisible(false);
        if (playOptionsPanel != null)
            playOptionsPanel.SetActive(false);
        if (shopPanel != null)
            shopPanel.SetActive(true);

        RefreshClassActionButtons();
        SetMainMenuButtonsInteractable(true);
        SetPlayOptionsButtonsInteractable(true);
    }

    void SetMainMenuButtonsInteractable(bool interactable)
    {
        if (playButton != null) playButton.interactable = interactable;
        if (settingsButton != null) settingsButton.interactable = interactable;
        if (quitButton != null) quitButton.interactable = interactable;
        if (shopButton != null) shopButton.interactable = interactable;
    }

    void SetPlayOptionsButtonsInteractable(bool interactable)
    {
        if (soloPlayButton != null) soloPlayButton.interactable = interactable;
        if (multiplayerLobbyButton != null) multiplayerLobbyButton.interactable = interactable;
    }

    void SetMainMenuObjectsVisible(bool visible)
    {
        SetButtonObjectVisible(playButton, visible);
        SetButtonObjectVisible(settingsButton, visible);
        SetButtonObjectVisible(quitButton, visible);
        SetButtonObjectVisible(shopButton, visible);
        if (titleObject != null)
            titleObject.SetActive(visible);
    }

    void SetButtonObjectVisible(Button button, bool visible)
    {
        if (button != null)
            button.gameObject.SetActive(visible);
    }

    void SetShopButtonBackdropVisible(bool visible)
    {
        Image parentImage = shopButton != null && shopButton.transform.parent != null ? shopButton.transform.parent.GetComponent<Image>() : null;
        if (parentImage != null && parentImage.sprite == null)
            parentImage.enabled = visible;
    }

    void EnsureShopButton()
    {
        if (shopButton != null)
            return;

        shopButton = ResolveExistingShopButton();
        if (shopButton != null)
        {
            WireShopButton();
            return;
        }

        Transform parent = mainMenuPanel != null ? mainMenuPanel.transform : (ResolveMenuCanvas() != null ? ResolveMenuCanvas().transform : transform);
        shopButton = CreateSpriteMenuButton(parent, "ShopButton", shopButtonSprite);

        RectTransform shopRt = shopButton.GetComponent<RectTransform>();
        RectTransform settingsRt = settingsButton != null ? settingsButton.GetComponent<RectTransform>() : null;
        if (settingsRt != null)
        {
            shopRt.anchorMin = settingsRt.anchorMin;
            shopRt.anchorMax = settingsRt.anchorMax;
            shopRt.pivot = settingsRt.pivot;
            shopRt.sizeDelta = settingsRt.sizeDelta;
            shopRt.localScale = settingsRt.localScale;
            shopRt.anchoredPosition = settingsRt.anchoredPosition + new Vector2(0f, 260f);
        }
        else
        {
            shopRt.anchoredPosition = new Vector2(743f, -255f);
        }

        WireShopButton();
    }

    Button ResolveExistingShopButton()
    {
        string[] possibleNames = { "ShopButton", "shop button", "SHOP" };
        foreach (string possibleName in possibleNames)
        {
            GameObject existing = GameObject.Find(possibleName);
            if (existing == null)
                continue;

            Button button = existing.GetComponent<Button>();
            Image image = existing.GetComponent<Image>();
            if (button == null && image != null)
                button = existing.AddComponent<Button>();

            if (button != null)
            {
                Image parentImage = existing.transform.parent != null ? existing.transform.parent.GetComponent<Image>() : null;
                if (parentImage != null && parentImage.sprite == null)
                    parentImage.raycastTarget = false;
                if (image != null)
                    button.targetGraphic = image;
                if (settingsButton != null)
                    button.colors = settingsButton.colors;
                else if (playButton != null)
                    button.colors = playButton.colors;
                return button;
            }
        }

        return null;
    }

    void EnsureShopPanel()
    {
        if (shopPanel != null)
            return;

        Transform parent = mainMenuPanel != null ? mainMenuPanel.transform : (ResolveMenuCanvas() != null ? ResolveMenuCanvas().transform : transform);
        shopPanel = new GameObject("ClassShopPanel");
        shopPanel.transform.SetParent(parent, false);

        RectTransform panelRt = shopPanel.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = Vector2.zero;
        panelRt.sizeDelta = new Vector2(1180f, 800f);

        Image panelImage = shopPanel.AddComponent<Image>();
        panelImage.color = new Color(0.035f, 0.028f, 0.022f, 0.96f);

        CreateShopHeader(shopPanel.transform);
        CreateShopClassList(shopPanel.transform);
        CreateShopBackButton(shopPanel.transform);
        shopPanel.SetActive(false);
    }

    void CreateShopHeader(Transform parent)
    {
        TextMeshProUGUI title = CreateShopText(parent, "ShopTitle", "CLASS SHOP", 34f, FontStyles.Bold, new Color(1f, 0.86f, 0.55f, 1f), TextAlignmentOptions.Center);
        RectTransform titleRt = title.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 1f);
        titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -26f);
        titleRt.sizeDelta = new Vector2(620f, 54f);

        TextMeshProUGUI subtitle = CreateShopText(parent, "ShopSubtitle", "Pick a class for strong advantages, sometimes with a drawback.", 17f, FontStyles.Bold, new Color(0.76f, 0.7f, 0.58f, 1f), TextAlignmentOptions.Center);
        RectTransform subtitleRt = subtitle.GetComponent<RectTransform>();
        subtitleRt.anchorMin = new Vector2(0.5f, 1f);
        subtitleRt.anchorMax = new Vector2(0.5f, 1f);
        subtitleRt.pivot = new Vector2(0.5f, 1f);
        subtitleRt.anchoredPosition = new Vector2(0f, -82f);
        subtitleRt.sizeDelta = new Vector2(760f, 32f);

        gemBalanceText = CreateShopText(parent, "GemBalance", "", 18f, FontStyles.Bold, new Color(0.42f, 0.9f, 1f, 1f), TextAlignmentOptions.Center);
        RectTransform gemRt = gemBalanceText.GetComponent<RectTransform>();
        gemRt.anchorMin = new Vector2(0.5f, 1f);
        gemRt.anchorMax = new Vector2(0.5f, 1f);
        gemRt.pivot = new Vector2(0.5f, 1f);
        gemRt.anchoredPosition = new Vector2(0f, -108f);
        gemRt.sizeDelta = new Vector2(420f, 26f);
        UpdateGemBalanceText();
    }

    void CreateShopClassList(Transform parent)
    {
        GameObject scrollGo = new GameObject("ClassShopScroll");
        scrollGo.transform.SetParent(parent, false);
        RectTransform scrollRt = scrollGo.AddComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0.5f, 0f);
        scrollRt.anchorMax = new Vector2(0.5f, 0f);
        scrollRt.pivot = new Vector2(0.5f, 0f);
        scrollRt.anchoredPosition = new Vector2(0f, 58f);
        scrollRt.sizeDelta = new Vector2(1060f, 610f);

        Image scrollBg = scrollGo.AddComponent<Image>();
        scrollBg.color = new Color(0f, 0f, 0f, 0.22f);

        Mask mask = scrollGo.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        ScrollRect scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.viewport = scrollRt;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 40f;

        GameObject contentGo = new GameObject("Content");
        contentGo.transform.SetParent(scrollGo.transform, false);
        RectTransform contentRt = contentGo.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;

        float slotHeight = 152f;
        float spacing = 18f;
        contentRt.sizeDelta = new Vector2(0f, slotHeight * ClassShopItems.Length + spacing * (ClassShopItems.Length - 1) + 18f);
        scrollRect.content = contentRt;

        classActionButtons = new Button[ClassShopItems.Length];
        classActionTexts = new TextMeshProUGUI[ClassShopItems.Length];

        for (int i = 0; i < ClassShopItems.Length; i++)
            CreateClassShopSlot(contentGo.transform, i, slotHeight, spacing);

        RefreshClassActionButtons();
    }

    void CreateClassShopSlot(Transform parent, int index, float slotHeight, float spacing)
    {
        ClassShopItem item = ClassShopItems[index];

        GameObject slotGo = new GameObject($"ClassSlot{index + 1}");
        slotGo.transform.SetParent(parent, false);
        RectTransform slotRt = slotGo.AddComponent<RectTransform>();
        slotRt.anchorMin = new Vector2(0.5f, 1f);
        slotRt.anchorMax = new Vector2(0.5f, 1f);
        slotRt.pivot = new Vector2(0.5f, 1f);
        slotRt.anchoredPosition = new Vector2(0f, -9f - index * (slotHeight + spacing));
        slotRt.sizeDelta = new Vector2(1000f, slotHeight);

        bool unlocked = IsClassAvailable(index);

        Image slotImage = slotGo.AddComponent<Image>();
        slotImage.color = unlocked ? new Color(0.12f, 0.095f, 0.07f, 0.96f) : new Color(0.07f, 0.065f, 0.06f, 0.92f);

        GameObject innerGo = new GameObject("Inner");
        innerGo.transform.SetParent(slotGo.transform, false);
        RectTransform innerRt = innerGo.AddComponent<RectTransform>();
        innerRt.anchorMin = Vector2.zero;
        innerRt.anchorMax = Vector2.one;
        innerRt.offsetMin = new Vector2(16f, 16f);
        innerRt.offsetMax = new Vector2(-16f, -16f);
        Image innerImage = innerGo.AddComponent<Image>();
        innerImage.color = new Color(0f, 0f, 0f, 0.22f);
        innerImage.raycastTarget = false;

        CreateClassStarRating(slotGo.transform, index, unlocked);
        CreateLockedRequirementLabel(slotGo.transform, index, unlocked);
        CreateClassBadge(slotGo.transform, index);

        TextMeshProUGUI nameText = CreateShopText(slotGo.transform, "ClassName", unlocked ? item.Name.ToUpperInvariant() : "", 32f, FontStyles.Bold, new Color(1f, 0.88f, 0.58f, 1f), TextAlignmentOptions.Left);
        RectTransform nameRt = nameText.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0f, 0.5f);
        nameRt.anchorMax = new Vector2(0f, 0.5f);
        nameRt.pivot = new Vector2(0f, 0.5f);
        nameRt.anchoredPosition = new Vector2(132f, 42f);
        nameRt.sizeDelta = new Vector2(620f, 40f);

        TextMeshProUGUI advantageText = CreateShopText(slotGo.transform, "Advantage", unlocked ? "Advantage: " + item.Advantage : "", 19f, FontStyles.Normal, new Color(0.72f, 0.93f, 0.62f, 1f), TextAlignmentOptions.Left);
        RectTransform advantageRt = advantageText.GetComponent<RectTransform>();
        advantageRt.anchorMin = new Vector2(0f, 0.5f);
        advantageRt.anchorMax = new Vector2(0f, 0.5f);
        advantageRt.pivot = new Vector2(0f, 0.5f);
        advantageRt.anchoredPosition = new Vector2(132f, 2f);
        advantageRt.sizeDelta = new Vector2(680f, 28f);

        TextMeshProUGUI downsideText = CreateShopText(slotGo.transform, "Downside", unlocked ? "Downside: " + item.Downside : "", 19f, FontStyles.Normal, new Color(0.92f, 0.56f, 0.48f, 1f), TextAlignmentOptions.Left);
        RectTransform downsideRt = downsideText.GetComponent<RectTransform>();
        downsideRt.anchorMin = new Vector2(0f, 0.5f);
        downsideRt.anchorMax = new Vector2(0f, 0.5f);
        downsideRt.pivot = new Vector2(0f, 0.5f);
        downsideRt.anchoredPosition = new Vector2(132f, -34f);
        downsideRt.sizeDelta = new Vector2(680f, 28f);

        TextMeshProUGUI costText = CreateShopText(slotGo.transform, "GemCost", unlocked ? GetClassCostLabel(index) : "", 17f, FontStyles.Bold, new Color(0.45f, 0.92f, 1f, 1f), TextAlignmentOptions.Left);
        RectTransform costRt = costText.GetComponent<RectTransform>();
        costRt.anchorMin = new Vector2(0f, 0.5f);
        costRt.anchorMax = new Vector2(0f, 0.5f);
        costRt.pivot = new Vector2(0f, 0.5f);
        costRt.anchoredPosition = new Vector2(132f, -62f);
        costRt.sizeDelta = new Vector2(300f, 24f);

        Button actionButton = CreateSmallShopButton(slotGo.transform, "ClassActionButton", "PURCHASE", new Vector2(-28f, 0f), new Vector2(160f, 58f), unlocked ? new Color(0.18f, 0.58f, 0.22f, 1f) : new Color(0.2f, 0.2f, 0.2f, 1f));
        actionButton.gameObject.SetActive(unlocked);
        TextMeshProUGUI actionText = actionButton.GetComponentInChildren<TextMeshProUGUI>(true);
        int classIndex = index;
        actionButton.onClick.AddListener(() => OnClassActionPressed(classIndex));

        if (classActionButtons != null && index >= 0 && index < classActionButtons.Length)
        {
            classActionButtons[index] = actionButton;
            classActionTexts[index] = actionText;
        }
    }

    void CreateClassBadge(Transform parent, int index)
    {
        if (!IsClassAvailable(index))
            return;

        GameObject badgeGo = new GameObject("ClassBadge");
        badgeGo.transform.SetParent(parent, false);
        RectTransform badgeRt = badgeGo.AddComponent<RectTransform>();
        badgeRt.anchorMin = new Vector2(0f, 0.5f);
        badgeRt.anchorMax = new Vector2(0f, 0.5f);
        badgeRt.pivot = new Vector2(0f, 0.5f);
        badgeRt.anchoredPosition = new Vector2(28f, 0f);
        badgeRt.sizeDelta = new Vector2(64f, 64f);

        Image badgeImage = badgeGo.AddComponent<Image>();
        badgeImage.sprite = GetClassIconSprite(index);
        badgeImage.color = Color.white;
        badgeImage.preserveAspect = true;
        badgeImage.raycastTarget = false;
    }

    void CreateClassStarRating(Transform parent, int index, bool unlocked)
    {
        EnsureStarSprites();

        GameObject starsGo = new GameObject("ClassStarRating");
        starsGo.transform.SetParent(parent, false);
        RectTransform starsRt = starsGo.AddComponent<RectTransform>();
        starsRt.anchorMin = new Vector2(0.5f, 0.5f);
        starsRt.anchorMax = new Vector2(0.5f, 0.5f);
        starsRt.pivot = new Vector2(0.5f, 0.5f);
        starsRt.anchoredPosition = unlocked ? new Vector2(42f, 48f) : new Vector2(42f, 8f);
        starsRt.sizeDelta = unlocked ? new Vector2(190f, 32f) : new Vector2(280f, 50f);

        int rating = GetClassStarRating(index);
        float starSize = unlocked ? 28f : 42f;
        float spacing = unlocked ? 36f : 52f;
        float startX = -spacing * 2f;
        for (int i = 0; i < 5; i++)
        {
            GameObject starGo = new GameObject($"Star{i + 1}");
            starGo.transform.SetParent(starsGo.transform, false);
            RectTransform starRt = starGo.AddComponent<RectTransform>();
            starRt.anchorMin = new Vector2(0.5f, 0.5f);
            starRt.anchorMax = new Vector2(0.5f, 0.5f);
            starRt.pivot = new Vector2(0.5f, 0.5f);
            starRt.anchoredPosition = new Vector2(startX + spacing * i, 0f);
            starRt.sizeDelta = new Vector2(starSize, starSize);

            Image starImage = starGo.AddComponent<Image>();
            starImage.sprite = i < rating ? filledStarSprite : emptyStarSprite;
            starImage.color = Color.white;
            starImage.preserveAspect = true;
            starImage.raycastTarget = false;
        }
    }

    void CreateLockedRequirementLabel(Transform parent, int index, bool unlocked)
    {
        if (unlocked)
            return;

        int requiredDays = GetRequiredPersonalBestDays(index);
        if (requiredDays <= 0)
            return;

        TextMeshProUGUI lockedText = CreateShopText(parent, "LockedRequirement", $"LOCKED UNTIL {requiredDays} DAYS", 20f, FontStyles.Bold, new Color(0.78f, 0.72f, 0.58f, 1f), TextAlignmentOptions.Center);
        RectTransform lockedRt = lockedText.GetComponent<RectTransform>();
        lockedRt.anchorMin = new Vector2(0.5f, 0.5f);
        lockedRt.anchorMax = new Vector2(0.5f, 0.5f);
        lockedRt.pivot = new Vector2(0.5f, 0.5f);
        lockedRt.anchoredPosition = new Vector2(42f, -38f);
        lockedRt.sizeDelta = new Vector2(360f, 30f);
        lockedText.enableWordWrapping = false;
        lockedText.overflowMode = TextOverflowModes.Overflow;
    }

    int GetClassStarRating(int index)
    {
        if (index < 0 || index >= ClassShopItems.Length)
            return 1;

        return Mathf.Clamp(ClassShopItems[index].StarRating, 1, 5);
    }

    void EnsureStarSprites()
    {
        if (filledStarSprite == null)
        {
            filledStarTexture = CreateStarTexture(new Color(1f, 0.78f, 0.12f, 1f), new Color(1f, 0.95f, 0.48f, 1f), new Color(0.58f, 0.32f, 0.03f, 1f));
            filledStarSprite = Sprite.Create(filledStarTexture, new Rect(0f, 0f, filledStarTexture.width, filledStarTexture.height), new Vector2(0.5f, 0.5f), 100f);
        }

        if (emptyStarSprite == null)
        {
            emptyStarTexture = CreateStarTexture(new Color(0.23f, 0.18f, 0.1f, 1f), new Color(0.42f, 0.31f, 0.13f, 1f), new Color(0.12f, 0.09f, 0.05f, 1f));
            emptyStarSprite = Sprite.Create(emptyStarTexture, new Rect(0f, 0f, emptyStarTexture.width, emptyStarTexture.height), new Vector2(0.5f, 0.5f), 100f);
        }
    }

    Texture2D CreateStarTexture(Color fill, Color highlight, Color outline)
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = clear;
        texture.SetPixels(pixels);

        Vector2[] outerStar = BuildStarPoints(new Vector2(32f, 32f), 29f, 12f);
        Vector2[] innerStar = BuildStarPoints(new Vector2(32f, 32f), 24f, 10f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                if (PointInPolygon(p, outerStar))
                    texture.SetPixel(x, y, outline);
                if (PointInPolygon(p, innerStar))
                    texture.SetPixel(x, y, y > 36 ? highlight : fill);
            }
        }

        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.Apply();
        return texture;
    }

    Vector2[] BuildStarPoints(Vector2 center, float outerRadius, float innerRadius)
    {
        Vector2[] points = new Vector2[10];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = (-90f + i * 36f) * Mathf.Deg2Rad;
            float radius = i % 2 == 0 ? outerRadius : innerRadius;
            points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        return points;
    }

    bool PointInPolygon(Vector2 point, Vector2[] polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            if ((polygon[i].y > point.y) != (polygon[j].y > point.y) &&
                point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    Sprite GetClassIconSprite(int index)
    {
        if (classIconSprites == null || classIconSprites.Length != ClassShopItems.Length)
        {
            classIconSprites = new Sprite[ClassShopItems.Length];
            classIconTextures = new Texture2D[ClassShopItems.Length];
        }

        if (index < 0 || index >= classIconSprites.Length)
            return null;

        if (classIconSprites[index] != null)
            return classIconSprites[index];

        Texture2D texture = CreateClassIconTexture(index);
        classIconTextures[index] = texture;
        classIconSprites[index] = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        return classIconSprites[index];
    }

    Texture2D CreateClassIconTexture(int index)
    {
        const int size = 72;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = clear;
        texture.SetPixels(pixels);

        DrawRect(texture, 4, 4, 64, 64, new Color(0.045f, 0.035f, 0.028f, 0.96f));
        DrawRect(texture, 6, 6, 60, 60, new Color(0.16f, 0.12f, 0.08f, 0.95f));

        switch (index)
        {
            case 0: DrawNoobIcon(texture); break;
            case 1: DrawFlashlightIcon(texture); break;
            case 2: DrawAxeIcon(texture); break;
            case 3: DrawStewIcon(texture); break;
            case 4: DrawBandageIcon(texture); break;
            case 5: DrawFarmPlotIcon(texture); break;
            case 6: DrawMapIcon(texture); break;
            case 7: DrawFelineIcon(texture); break;
            case 8: DrawWitchIcon(texture); break;
            case 9: DrawArsonistIcon(texture); break;
            default: DrawNoobIcon(texture); break;
        }

        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.Apply();
        return texture;
    }

    void DrawNoobIcon(Texture2D texture)
    {
        DrawRect(texture, 27, 18, 18, 30, new Color(0.36f, 0.34f, 0.3f, 1f));
        DrawRect(texture, 24, 28, 5, 19, new Color(0.18f, 0.16f, 0.14f, 1f));
        DrawRect(texture, 43, 28, 5, 19, new Color(0.18f, 0.16f, 0.14f, 1f));
        DrawRect(texture, 30, 22, 12, 6, new Color(0.58f, 0.52f, 0.42f, 1f));
        DrawRect(texture, 31, 33, 4, 4, new Color(0.06f, 0.055f, 0.05f, 1f));
        DrawRect(texture, 39, 33, 4, 4, new Color(0.06f, 0.055f, 0.05f, 1f));
    }

    void DrawFlashlightIcon(Texture2D texture)
    {
        DrawRect(texture, 22, 32, 30, 12, new Color(0.22f, 0.23f, 0.24f, 1f));
        DrawRect(texture, 25, 34, 20, 8, new Color(0.42f, 0.45f, 0.47f, 1f));
        DrawRect(texture, 49, 29, 9, 18, new Color(0.1f, 0.11f, 0.12f, 1f));
        DrawRect(texture, 52, 32, 5, 12, new Color(1f, 0.84f, 0.34f, 1f));
        DrawLine(texture, 12, 28, 50, 36, new Color(1f, 0.86f, 0.42f, 0.45f), 3);
        DrawLine(texture, 12, 46, 50, 38, new Color(1f, 0.86f, 0.42f, 0.35f), 3);
    }

    void DrawAxeIcon(Texture2D texture)
    {
        DrawLine(texture, 25, 53, 46, 18, new Color(0.42f, 0.22f, 0.09f, 1f), 6);
        DrawLine(texture, 28, 52, 49, 17, new Color(0.66f, 0.4f, 0.18f, 1f), 2);
        DrawRect(texture, 42, 16, 16, 11, new Color(0.6f, 0.64f, 0.66f, 1f));
        DrawRect(texture, 50, 22, 10, 11, new Color(0.36f, 0.39f, 0.42f, 1f));
        DrawLine(texture, 43, 17, 56, 30, new Color(0.82f, 0.86f, 0.88f, 1f), 2);
    }

    void DrawStewIcon(Texture2D texture)
    {
        DrawRect(texture, 18, 38, 36, 10, new Color(0.54f, 0.35f, 0.22f, 1f));
        DrawRect(texture, 22, 45, 28, 8, new Color(0.28f, 0.16f, 0.1f, 1f));
        DrawRect(texture, 22, 34, 28, 8, new Color(0.8f, 0.38f, 0.12f, 1f));
        DrawCircle(texture, 30, 37, 3, new Color(0.98f, 0.73f, 0.28f, 1f));
        DrawCircle(texture, 42, 37, 3, new Color(0.36f, 0.64f, 0.26f, 1f));
        DrawLine(texture, 29, 27, 27, 17, new Color(0.9f, 0.86f, 0.75f, 0.75f), 2);
        DrawLine(texture, 38, 27, 41, 17, new Color(0.9f, 0.86f, 0.75f, 0.65f), 2);
    }

    void DrawBandageIcon(Texture2D texture)
    {
        DrawRect(texture, 16, 28, 40, 20, new Color(0.84f, 0.76f, 0.61f, 1f));
        DrawRect(texture, 19, 31, 34, 14, new Color(0.96f, 0.88f, 0.72f, 1f));
        DrawRect(texture, 33, 31, 6, 14, new Color(0.7f, 0.08f, 0.08f, 1f));
        DrawRect(texture, 26, 35, 20, 6, new Color(0.7f, 0.08f, 0.08f, 1f));
        DrawRect(texture, 20, 32, 3, 3, new Color(0.62f, 0.52f, 0.4f, 1f));
        DrawRect(texture, 49, 42, 3, 3, new Color(0.62f, 0.52f, 0.4f, 1f));
    }

    void DrawFarmPlotIcon(Texture2D texture)
    {
        DrawRect(texture, 17, 31, 38, 22, new Color(0.28f, 0.14f, 0.06f, 1f));
        DrawLine(texture, 20, 37, 53, 37, new Color(0.48f, 0.26f, 0.11f, 1f), 2);
        DrawLine(texture, 20, 45, 53, 45, new Color(0.48f, 0.26f, 0.11f, 1f), 2);
        DrawLine(texture, 26, 50, 31, 25, new Color(0.12f, 0.58f, 0.18f, 1f), 3);
        DrawLine(texture, 26, 36, 20, 30, new Color(0.18f, 0.74f, 0.24f, 1f), 2);
        DrawLine(texture, 42, 50, 45, 24, new Color(0.12f, 0.58f, 0.18f, 1f), 3);
        DrawLine(texture, 43, 34, 51, 29, new Color(0.18f, 0.74f, 0.24f, 1f), 2);
    }

    void DrawMapIcon(Texture2D texture)
    {
        DrawRect(texture, 17, 18, 38, 44, new Color(0.82f, 0.72f, 0.52f, 1f));
        DrawRect(texture, 21, 22, 30, 36, new Color(0.94f, 0.84f, 0.6f, 1f));
        DrawLine(texture, 26, 25, 45, 52, new Color(0.12f, 0.5f, 0.2f, 1f), 3);
        DrawLine(texture, 28, 50, 49, 34, new Color(0.16f, 0.34f, 0.7f, 1f), 2);
        DrawCircle(texture, 26, 25, 4, new Color(0.85f, 0.16f, 0.12f, 1f));
        DrawCircle(texture, 48, 34, 4, new Color(0.12f, 0.24f, 0.76f, 1f));
    }

    void DrawFelineIcon(Texture2D texture)
    {
        DrawCircle(texture, 36, 39, 9, new Color(0.86f, 0.72f, 0.48f, 1f));
        DrawCircle(texture, 24, 30, 4, new Color(0.86f, 0.72f, 0.48f, 1f));
        DrawCircle(texture, 32, 24, 4, new Color(0.86f, 0.72f, 0.48f, 1f));
        DrawCircle(texture, 44, 24, 4, new Color(0.86f, 0.72f, 0.48f, 1f));
        DrawCircle(texture, 52, 30, 4, new Color(0.86f, 0.72f, 0.48f, 1f));
        DrawRect(texture, 19, 52, 34, 6, new Color(0.22f, 0.18f, 0.12f, 1f));
        DrawRect(texture, 22, 53, 28, 2, new Color(0.96f, 0.86f, 0.46f, 1f));
    }

    void DrawWitchIcon(Texture2D texture)
    {
        DrawRect(texture, 19, 41, 34, 12, new Color(0.08f, 0.07f, 0.08f, 1f));
        DrawRect(texture, 23, 35, 26, 14, new Color(0.16f, 0.13f, 0.16f, 1f));
        DrawCircle(texture, 29, 37, 3, new Color(0.36f, 0.9f, 0.3f, 1f));
        DrawCircle(texture, 39, 36, 3, new Color(0.8f, 0.28f, 0.95f, 1f));
        DrawRect(texture, 14, 20, 8, 18, new Color(0.55f, 0.2f, 0.86f, 1f));
        DrawRect(texture, 48, 18, 8, 20, new Color(0.1f, 0.58f, 0.9f, 1f));
        DrawRect(texture, 16, 16, 4, 5, new Color(0.85f, 0.78f, 0.9f, 1f));
        DrawRect(texture, 50, 14, 4, 5, new Color(0.85f, 0.78f, 0.9f, 1f));
    }

    void DrawArsonistIcon(Texture2D texture)
    {
        DrawLine(texture, 22, 52, 42, 24, new Color(0.24f, 0.62f, 0.22f, 1f), 7);
        DrawLine(texture, 22, 52, 42, 24, new Color(0.62f, 0.28f, 0.12f, 1f), 3);
        DrawCircle(texture, 47, 21, 8, new Color(0.95f, 0.18f, 0.06f, 1f));
        DrawCircle(texture, 47, 24, 6, new Color(1f, 0.58f, 0.08f, 1f));
        DrawCircle(texture, 48, 27, 3, new Color(1f, 0.9f, 0.18f, 1f));
        DrawRect(texture, 22, 24, 18, 5, new Color(0.48f, 0.48f, 0.44f, 1f));
        DrawRect(texture, 27, 19, 8, 15, new Color(0.22f, 0.22f, 0.2f, 1f));
    }

    void DrawRect(Texture2D texture, int x, int y, int width, int height, Color color)
    {
        for (int py = y; py < y + height; py++)
        {
            for (int px = x; px < x + width; px++)
                SetPixelSafe(texture, px, py, color);
        }
    }

    void DrawCircle(Texture2D texture, int cx, int cy, int radius, Color color)
    {
        int r2 = radius * radius;
        for (int y = cy - radius; y <= cy + radius; y++)
        {
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                int dx = x - cx;
                int dy = y - cy;
                if (dx * dx + dy * dy <= r2)
                    SetPixelSafe(texture, x, y, color);
            }
        }
    }

    void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, Color color, int thickness)
    {
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            DrawCircle(texture, x0, y0, Mathf.Max(1, thickness / 2), color);
            if (x0 == x1 && y0 == y1)
                break;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    void SetPixelSafe(Texture2D texture, int x, int y, Color color)
    {
        if (x < 0 || y < 0 || x >= texture.width || y >= texture.height)
            return;

        texture.SetPixel(x, y, color);
    }

    void OnDestroy()
    {
        if (classIconSprites != null)
        {
            for (int i = 0; i < classIconSprites.Length; i++)
            {
                if (classIconSprites[i] != null)
                    Destroy(classIconSprites[i]);
            }
        }

        if (classIconTextures != null)
        {
            for (int i = 0; i < classIconTextures.Length; i++)
            {
                if (classIconTextures[i] != null)
                    Destroy(classIconTextures[i]);
            }
        }

        if (filledStarSprite != null)
            Destroy(filledStarSprite);
        if (emptyStarSprite != null)
            Destroy(emptyStarSprite);
        if (filledStarTexture != null)
            Destroy(filledStarTexture);
        if (emptyStarTexture != null)
            Destroy(emptyStarTexture);
    }

    void OnClassActionPressed(int index)
    {
        if (!IsClassAvailable(index))
            return;

        if (!IsClassPurchased(index))
        {
            int cost = GetClassGemCost(index);
            if (GetGemBalance() < cost)
            {
                RefreshClassActionButtons();
                Debug.Log($"Not enough gems to purchase {ClassShopItems[index].Name}. Need {cost}, have {GetGemBalance()}.");
                return;
            }

            SpendGems(cost);
            SetClassPurchased(index, true);
            RefreshClassActionButtons();
            Debug.Log($"Class purchased: {ClassShopItems[index].Name}. Gameplay effects will be added later.");
            return;
        }

        EquipClass(index);
        RefreshClassActionButtons();
        Debug.Log($"Class selected: {ClassShopItems[index].Name}. Gameplay effects will be added later.");
    }

    void RefreshClassActionButtons()
    {
        if (classActionButtons == null || classActionTexts == null)
            return;

        string equippedClass = GetEquippedClassName();
        for (int i = 0; i < classActionButtons.Length; i++)
        {
            Button button = classActionButtons[i];
            TextMeshProUGUI text = classActionTexts[i];
            if (button == null || text == null)
                continue;

            bool available = IsClassAvailable(i);
            bool purchased = IsClassPurchased(i);
            bool equipped = available && purchased && ClassShopItems[i].Name == equippedClass;
            bool canAfford = available && GetGemBalance() >= GetClassGemCost(i);

            button.interactable = available && (purchased || canAfford);
            text.text = !available ? "LOCKED" : equipped ? "EQUIPPED" : purchased ? "EQUIP" : GetClassPurchaseButtonLabel(i);

            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                if (!available)
                    image.color = new Color(0.2f, 0.2f, 0.2f, 1f);
                else if (equipped)
                    image.color = new Color(0.62f, 0.42f, 0.12f, 1f);
                else if (purchased)
                    image.color = new Color(0.18f, 0.44f, 0.7f, 1f);
                else if (!canAfford)
                    image.color = new Color(0.28f, 0.28f, 0.28f, 1f);
                else
                    image.color = new Color(0.18f, 0.58f, 0.22f, 1f);
            }
        }

        UpdateGemBalanceText();
    }

    int GetClassGemCost(int index)
    {
        if (index < 0 || index >= ClassShopItems.Length)
            return 0;

        return Mathf.Max(0, ClassShopItems[index].GemCost);
    }

    string GetClassCostLabel(int index)
    {
        int cost = GetClassGemCost(index);
        return cost <= 0 ? "FREE" : cost + " GEMS";
    }

    string GetClassPurchaseButtonLabel(int index)
    {
        int cost = GetClassGemCost(index);
        return cost <= 0 ? "CLAIM" : "BUY";
    }

    int GetGemBalance()
    {
        return Mathf.Max(0, PlayerPrefs.GetInt(DayNightCycle.ClassGemBalanceKey, 0));
    }

    void SpendGems(int amount)
    {
        if (amount <= 0)
            return;

        PlayerPrefs.SetInt(DayNightCycle.ClassGemBalanceKey, Mathf.Max(0, GetGemBalance() - amount));
        PlayerPrefs.Save();
    }

    void UpdateGemBalanceText()
    {
        if (gemBalanceText != null)
            gemBalanceText.text = "GEMS  " + GetGemBalance().ToString();
    }

    bool IsClassAvailable(int index)
    {
        if (index < 0 || index >= ClassShopItems.Length)
            return false;

        if (ClassShopItems[index].Available)
            return true;

        int requiredDays = GetRequiredPersonalBestDays(index);
        return requiredDays > 0 && GetPersonalBestDays() >= requiredDays;
    }

    bool IsAdvancedClassSlot(int index)
    {
        return index >= 5 && index < ClassShopItems.Length;
    }

    int GetPersonalBestDays()
    {
        return PlayerPrefs.GetInt(DayNightCycle.PersonalBestDaysKey, 0);
    }

    int GetRequiredPersonalBestDays(int index)
    {
        if (index < 0 || index >= ClassShopItems.Length)
            return 0;

        return Mathf.Max(0, ClassShopItems[index].RequiredPersonalBestDays);
    }

    string GetLockedClassMessage(int index)
    {
        int requiredDays = GetRequiredPersonalBestDays(index);
        if (requiredDays > 0)
            return $"Requires personal best of {requiredDays}+ days.";

        return ClassShopItems[index].Advantage;
    }

    bool IsClassPurchased(int index)
    {
        if (!IsClassAvailable(index))
            return false;

        if (index == 0)
            return true;

        return PlayerPrefs.GetInt(PurchasedClassPrefix + ClassShopItems[index].Name, 0) == 1;
    }

    void SetClassPurchased(int index, bool purchased)
    {
        if (!IsClassAvailable(index))
            return;

        PlayerPrefs.SetInt(PurchasedClassPrefix + ClassShopItems[index].Name, purchased ? 1 : 0);
        PlayerPrefs.Save();
    }

    void EquipClass(int index)
    {
        if (!IsClassAvailable(index))
            return;

        PlayerPrefs.SetString(EquippedClassKey, ClassShopItems[index].Name);
        PlayerPrefs.Save();
    }

    string GetEquippedClassName()
    {
        string equipped = PlayerPrefs.GetString(EquippedClassKey, "");
        if (string.IsNullOrEmpty(equipped))
        {
            equipped = ClassShopItems[0].Name;
            PlayerPrefs.SetString(EquippedClassKey, equipped);
            PlayerPrefs.Save();
        }

        return equipped;
    }

    void CreateShopBackButton(Transform parent)
    {
        shopBackButton = CreateSmallShopButton(parent, "ShopBackButton", "BACK", new Vector2(-34f, -34f), new Vector2(120f, 44f), new Color(0.34f, 0.19f, 0.08f, 1f));
        RectTransform backRt = shopBackButton.GetComponent<RectTransform>();
        backRt.anchorMin = new Vector2(1f, 1f);
        backRt.anchorMax = new Vector2(1f, 1f);
        backRt.pivot = new Vector2(1f, 1f);
        shopBackButton.onClick.AddListener(ShowMainMenuPanel);
    }

    Button CreateSmallShopButton(Transform parent, string name, string label, Vector2 position, Vector2 size, Color color)
    {
        GameObject buttonGo = new GameObject(name);
        buttonGo.transform.SetParent(parent, false);
        RectTransform rt = buttonGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;

        Image image = buttonGo.AddComponent<Image>();
        image.color = color;

        Button button = buttonGo.AddComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI text = CreateShopText(buttonGo.transform, "Text", label, 15f, FontStyles.Bold, new Color(0.92f, 1f, 0.72f, 1f), TextAlignmentOptions.Center);
        RectTransform textRt = text.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        return button;
    }

    TextMeshProUGUI CreateShopText(Transform parent, string name, string value, float fontSize, FontStyles style, Color color, TextAlignmentOptions alignment)
    {
        GameObject textGo = new GameObject(name);
        textGo.transform.SetParent(parent, false);
        RectTransform rt = textGo.AddComponent<RectTransform>();
        rt.sizeDelta = Vector2.zero;

        TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    void EnsurePlayOptionsPanel()
    {
        if (playOptionsPanel != null)
            return;

        Canvas parentCanvas = ResolveMenuCanvas();
        Transform parent = mainMenuPanel != null ? mainMenuPanel.transform : (parentCanvas != null ? parentCanvas.transform : transform);

        playOptionsPanel = new GameObject("PlayOptionsPanel");
        playOptionsPanel.transform.SetParent(parent, false);

        RectTransform panelRt = playOptionsPanel.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = new Vector2(0f, -185f);
        panelRt.sizeDelta = new Vector2(980f, 120f);

        soloPlayButton = CreateMenuStyleButton(playOptionsPanel.transform, "SoloPlayButton", "SOLO PLAY");
        soloPlayButton.onClick.AddListener(OnSoloPlayPressed);
        RectTransform soloRt = soloPlayButton.GetComponent<RectTransform>();
        soloRt.anchoredPosition = new Vector2(-360f, 0f);

        multiplayerLobbyButton = CreateMenuStyleButton(playOptionsPanel.transform, "MultiplayerLobbyButton", "MULTIPLAYER LOBBY");
        multiplayerLobbyButton.onClick.AddListener(OnMultiplayerLobbyPressed);
        RectTransform lobbyRt = multiplayerLobbyButton.GetComponent<RectTransform>();
        lobbyRt.anchoredPosition = new Vector2(460f, 0f);

        playOptionsPanel.SetActive(false);
    }

    Button CreateMenuStyleButton(Transform parent, string objectName, string label)
    {
        GameObject buttonGo = new GameObject(objectName);
        buttonGo.transform.SetParent(parent, false);

        RectTransform rt = buttonGo.AddComponent<RectTransform>();
        Vector2 sourceSize = playButton != null ? playButton.GetComponent<RectTransform>().sizeDelta : new Vector2(300f, 64f);
        rt.sizeDelta = sourceSize;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = playButton != null ? playButton.GetComponent<RectTransform>().localScale : Vector3.one;

        Image image = buttonGo.AddComponent<Image>();
        image.color = new Color(0.08f, 0.42f, 0.12f, 0.92f);
        CreateWoodButtonBorder(buttonGo.transform);

        Button button = buttonGo.AddComponent<Button>();
        button.targetGraphic = image;
        if (playButton != null)
            button.colors = playButton.colors;

        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(buttonGo.transform, false);
        RectTransform textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = label.Length > 12 ? 8.5f : 12f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.92f, 1f, 0.72f, 1f);
        text.characterSpacing = 7f;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.outlineColor = new Color(0.02f, 0.12f, 0.03f, 1f);
        text.outlineWidth = 0.16f;
        text.raycastTarget = false;

        Shadow shadow = textGo.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.88f);
        shadow.effectDistance = new Vector2(2f, -2f);

        Outline outline = textGo.AddComponent<Outline>();
        outline.effectColor = new Color(0.02f, 0.12f, 0.03f, 1f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);

        return button;
    }

    Button CreateSpriteMenuButton(Transform parent, string objectName, Sprite sprite)
    {
        GameObject buttonGo = new GameObject(objectName);
        buttonGo.transform.SetParent(parent, false);

        RectTransform rt = buttonGo.AddComponent<RectTransform>();
        Vector2 sourceSize = settingsButton != null ? settingsButton.GetComponent<RectTransform>().sizeDelta : new Vector2(300f, 64f);
        rt.sizeDelta = sourceSize;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = settingsButton != null ? settingsButton.GetComponent<RectTransform>().localScale : Vector3.one;

        Image image = buttonGo.AddComponent<Image>();
        image.sprite = sprite;
        image.color = sprite != null ? Color.white : new Color(0.08f, 0.42f, 0.12f, 0.92f);
        image.preserveAspect = sprite != null;

        Button button = buttonGo.AddComponent<Button>();
        button.targetGraphic = image;
        if (settingsButton != null)
            button.colors = settingsButton.colors;
        else if (playButton != null)
            button.colors = playButton.colors;

        if (sprite == null)
        {
            Debug.LogWarning("MainMenuManager: Shop button sprite is not assigned. Assign your shop button Sprite in the inspector.");
        }

        return button;
    }

    void CreateWoodButtonBorder(Transform parent)
    {
        Color bark = new Color(0.38f, 0.2f, 0.075f, 1f);
        Color barkDark = new Color(0.14f, 0.07f, 0.026f, 1f);
        Color barkMid = new Color(0.5f, 0.28f, 0.11f, 1f);
        Color barkLight = new Color(0.7f, 0.43f, 0.18f, 1f);

        CreateBorderSegment(parent, "LogTopOuter", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 4f), new Vector2(18f, 12f), barkDark);
        CreateBorderSegment(parent, "LogTopInner", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(2f, 7f), barkMid);
        CreateBorderSegment(parent, "LogBottomOuter", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -4f), new Vector2(18f, 12f), barkDark);
        CreateBorderSegment(parent, "LogBottomInner", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(2f, 7f), bark);

        CreateBorderSegment(parent, "LeftTrunkOuter", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(-4f, 0f), new Vector2(14f, 14f), barkDark);
        CreateBorderSegment(parent, "LeftTrunkInner", new Vector2(0f, 0.08f), new Vector2(0f, 0.92f), new Vector2(0f, 0.5f), new Vector2(1f, 0f), new Vector2(7f, 0f), barkMid);
        CreateBorderSegment(parent, "RightTrunkOuter", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(4f, 0f), new Vector2(14f, 14f), barkDark);
        CreateBorderSegment(parent, "RightTrunkInner", new Vector2(1f, 0.08f), new Vector2(1f, 0.92f), new Vector2(1f, 0.5f), new Vector2(-1f, 0f), new Vector2(7f, 0f), bark);

        CreateBorderSegment(parent, "TopCutHighlight", new Vector2(0.08f, 1f), new Vector2(0.92f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -2.5f), new Vector2(-12f, 2f), barkLight);
        CreateBorderSegment(parent, "BottomBarkShadow", new Vector2(0.08f, 0f), new Vector2(0.92f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 2.5f), new Vector2(-16f, 2f), barkDark);

        CreateBorderSegment(parent, "TopGrainA", new Vector2(0.08f, 1f), new Vector2(0.38f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(-18f, 1.5f), barkDark);
        CreateBorderSegment(parent, "TopGrainB", new Vector2(0.5f, 1f), new Vector2(0.9f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(-28f, 1.5f), barkLight);
        CreateBorderSegment(parent, "BottomGrainA", new Vector2(0.1f, 0f), new Vector2(0.46f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(-22f, 1.5f), barkLight);
        CreateBorderSegment(parent, "BottomGrainB", new Vector2(0.56f, 0f), new Vector2(0.92f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(-18f, 1.5f), barkDark);

        CreateBorderSegment(parent, "LeftBarkCrack", new Vector2(0f, 0.18f), new Vector2(0f, 0.55f), new Vector2(0f, 0.5f), new Vector2(7f, 0f), new Vector2(2f, -8f), barkDark);
        CreateBorderSegment(parent, "RightBarkCrack", new Vector2(1f, 0.42f), new Vector2(1f, 0.82f), new Vector2(1f, 0.5f), new Vector2(-7f, 0f), new Vector2(2f, -10f), barkLight);
    }

    void CreateBorderSegment(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 sizeDelta, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = sizeDelta;

        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    Canvas ResolveMenuCanvas()
    {
        if (mainMenuPanel != null)
            return mainMenuPanel.GetComponentInParent<Canvas>();
        if (playButton != null)
            return playButton.GetComponentInParent<Canvas>();
        return FindObjectOfType<Canvas>();
    }

    IEnumerator SafeLoadCoroutine(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("MainMenuManager: SafeLoadScene failed because the scene name is empty.");
            yield break;
        }

        if (!SceneIsInBuild(sceneName))
        {
            Debug.LogError($"MainMenuManager: SafeLoadScene failed. Scene '{sceneName}' not in Build Settings.");
            yield break;
        }

        SceneManager.LoadScene(sceneName);
        yield break;
    }

}
