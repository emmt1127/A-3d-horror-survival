using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InteractableClassMenu : MonoBehaviour, IInteractable
{
    struct ClassInfo
    {
        public string Name;
        public string Advantage;
        public string Downside;
        public int StarRating;

        public ClassInfo(string name, string advantage, string downside, int starRating = 1)
        {
            Name = name;
            Advantage = advantage;
            Downside = downside;
            StarRating = starRating;
        }
    }

    static readonly ClassInfo[] Classes =
    {
        new ClassInfo("Noob", "No advantages.", "No disadvantages.", 1),
        new ClassInfo("Camper", "Spawns with a flashlight.", "No downside listed yet.", 1),
        new ClassInfo("Lumberjack", "Spawns with a good axe and gets more materials from trees.", "Slower movement speed.", 2),
        new ClassInfo("Cook", "Spawns with some good food.", "Lower health.", 2),
        new ClassInfo("Medic", "Spawns with bandages and can revive once automatically.", "Only gets 7 inventory slots instead of 10.", 3),
        new ClassInfo("Farmer", "You get 5 farm plots on spawn.", "Less health, and health regen is slower.", 3),
        new ClassInfo("Mapper", "You get a big map, and your teammates can see where you are.", "Slower movement speed.", 4),
        new ClassInfo("Feline", "You get 9 lives that you can respawn with, and you have faster movement speed.", "Lower health, smaller inventory space, and hunger drains faster.", 4),
        new ClassInfo("Witch", "Spawns with 4 potions and a cauldron. Only the Witch can use the cauldron.", "None.", 5),
        new ClassInfo("Arsonist", "Spawns with grenades, molotovs, flares, and bear traps.", "Overall weaker, and gets more hungry than other classes.", 5)
    };

    [Header("Menu Setup")]
    public Canvas menuCanvasPrefab; // optional: if null, will create one
    public RectTransform contentPanelPrefab; // optional content area

    [Header("Colors")]
    public Color selectedClassColor = new Color(0.2f, 0.8f, 0.2f, 1f);
    public Color normalClassColor = Color.white;

    Canvas _menuCanvas;
    GameObject _menuPanel;
    int _currentClassIndex = 0;
    bool _menuActive = false;

    void OnDestroy()
    {
        if (_menuCanvas != null)
            Destroy(_menuCanvas.gameObject);
    }

    public void OnInteract(GameObject interactor)
    {
        Debug.Log("InteractableClassMenu interacted with!");
        ToggleMenu();
    }

    void ToggleMenu()
    {
        if (!_menuActive)
            ShowMenu();
        else
            HideMenu();
    }

    void ShowMenu()
    {
        if (_menuCanvas == null)
            CreateMenuUI();

        _menuActive = true;
        if (_menuCanvas != null)
            _menuCanvas.gameObject.SetActive(true);

        UpdateMenuDisplay();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void HideMenu()
    {
        _menuActive = false;
        if (_menuCanvas != null)
            _menuCanvas.gameObject.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void CreateMenuUI()
    {
        // Create a canvas
        GameObject canvasObj = new GameObject("ClassMenuCanvas");
        _menuCanvas = canvasObj.AddComponent<Canvas>();
        _menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _menuCanvas.sortingOrder = 100;

        GraphicRaycaster raycaster = canvasObj.AddComponent<GraphicRaycaster>();

        // Create background panel
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObj.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0.7f);

        // Close on background click
        Button bgButton = bgObj.AddComponent<Button>();
        bgButton.onClick.AddListener(HideMenu);

        // Create main panel
        GameObject mainPanelObj = new GameObject("MainPanel");
        mainPanelObj.transform.SetParent(canvasObj.transform, false);
        RectTransform mainRect = mainPanelObj.AddComponent<RectTransform>();
        mainRect.anchoredPosition = Vector2.zero;
        mainRect.sizeDelta = new Vector2(800, 600);
        Image mainImage = mainPanelObj.AddComponent<Image>();
        mainImage.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);

        // Title
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(mainPanelObj.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchoredPosition = new Vector2(0, 250);
        titleRect.sizeDelta = new Vector2(700, 60);
        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "SELECT YOUR CLASS";
        titleText.fontSize = 40;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = new Color(1, 0.84f, 0);

        // Class info display
        GameObject classNameObj = new GameObject("ClassName");
        classNameObj.transform.SetParent(mainPanelObj.transform, false);
        RectTransform classNameRect = classNameObj.AddComponent<RectTransform>();
        classNameRect.anchoredPosition = new Vector2(0, 180);
        classNameRect.sizeDelta = new Vector2(700, 40);
        TextMeshProUGUI classNameText = classNameObj.AddComponent<TextMeshProUGUI>();
        classNameText.text = Classes[0].Name;
        classNameText.fontSize = 32;
        classNameText.alignment = TextAlignmentOptions.Center;
        classNameText.color = selectedClassColor;
        classNameText.name = "ClassNameText";

        // Advantage
        GameObject advObj = new GameObject("Advantage");
        advObj.transform.SetParent(mainPanelObj.transform, false);
        RectTransform advRect = advObj.AddComponent<RectTransform>();
        advRect.anchoredPosition = new Vector2(0, 110);
        advRect.sizeDelta = new Vector2(700, 50);
        TextMeshProUGUI advText = advObj.AddComponent<TextMeshProUGUI>();
        advText.text = "Advantage: " + Classes[0].Advantage;
        advText.fontSize = 18;
        advText.alignment = TextAlignmentOptions.Center;
        advText.color = Color.green;
        advText.name = "AdvantageText";

        // Downside
        GameObject downObj = new GameObject("Downside");
        downObj.transform.SetParent(mainPanelObj.transform, false);
        RectTransform downRect = downObj.AddComponent<RectTransform>();
        downRect.anchoredPosition = new Vector2(0, 50);
        downRect.sizeDelta = new Vector2(700, 50);
        TextMeshProUGUI downText = downObj.AddComponent<TextMeshProUGUI>();
        downText.text = "Downside: " + Classes[0].Downside;
        downText.fontSize = 18;
        downText.alignment = TextAlignmentOptions.Center;
        downText.color = Color.red;
        downText.name = "DownsideText";

        // Navigation buttons panel
        GameObject navPanel = new GameObject("NavigationPanel");
        navPanel.transform.SetParent(mainPanelObj.transform, false);
        RectTransform navRect = navPanel.AddComponent<RectTransform>();
        navRect.anchoredPosition = new Vector2(0, -80);
        navRect.sizeDelta = new Vector2(700, 80);

        // Left button
        GameObject leftBtnObj = new GameObject("LeftButton");
        leftBtnObj.transform.SetParent(navPanel.transform, false);
        RectTransform leftRect = leftBtnObj.AddComponent<RectTransform>();
        leftRect.anchoredPosition = new Vector2(-150, 0);
        leftRect.sizeDelta = new Vector2(120, 50);
        Image leftImage = leftBtnObj.AddComponent<Image>();
        leftImage.color = new Color(0.3f, 0.3f, 0.3f);
        TextMeshProUGUI leftText = leftBtnObj.AddComponent<TextMeshProUGUI>();
        leftText.text = "< PREV";
        leftText.fontSize = 20;
        leftText.alignment = TextAlignmentOptions.Center;
        Button leftBtn = leftBtnObj.AddComponent<Button>();
        leftBtn.onClick.AddListener(PrevClass);

        // Right button
        GameObject rightBtnObj = new GameObject("RightButton");
        rightBtnObj.transform.SetParent(navPanel.transform, false);
        RectTransform rightRect = rightBtnObj.AddComponent<RectTransform>();
        rightRect.anchoredPosition = new Vector2(150, 0);
        rightRect.sizeDelta = new Vector2(120, 50);
        Image rightImage = rightBtnObj.AddComponent<Image>();
        rightImage.color = new Color(0.3f, 0.3f, 0.3f);
        TextMeshProUGUI rightText = rightBtnObj.AddComponent<TextMeshProUGUI>();
        rightText.text = "NEXT >";
        rightText.fontSize = 20;
        rightText.alignment = TextAlignmentOptions.Center;
        Button rightBtn = rightBtnObj.AddComponent<Button>();
        rightBtn.onClick.AddListener(NextClass);

        // Select button
        GameObject selectBtnObj = new GameObject("SelectButton");
        selectBtnObj.transform.SetParent(navPanel.transform, false);
        RectTransform selectRect = selectBtnObj.AddComponent<RectTransform>();
        selectRect.anchoredPosition = new Vector2(0, 0);
        selectRect.sizeDelta = new Vector2(100, 50);
        Image selectImage = selectBtnObj.AddComponent<Image>();
        selectImage.color = new Color(0.2f, 0.7f, 0.2f);
        TextMeshProUGUI selectText = selectBtnObj.AddComponent<TextMeshProUGUI>();
        selectText.text = "SELECT";
        selectText.fontSize = 18;
        selectText.alignment = TextAlignmentOptions.Center;
        Button selectBtn = selectBtnObj.AddComponent<Button>();
        selectBtn.onClick.AddListener(SelectClass);

        _menuPanel = mainPanelObj;

        // Setup layout
        LayoutElement mainLayout = mainPanelObj.AddComponent<LayoutElement>();
        mainLayout.preferredWidth = 800;
        mainLayout.preferredHeight = 600;

        // Handle keyboard input in update
    }

    void UpdateMenuDisplay()
    {
        if (_menuPanel == null) return;

        ClassInfo currentClass = Classes[_currentClassIndex];
        
        TextMeshProUGUI classNameText = _menuPanel.transform.Find("ClassName/ClassNameText")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI advText = _menuPanel.transform.Find("Advantage/AdvantageText")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI downText = _menuPanel.transform.Find("Downside/DownsideText")?.GetComponent<TextMeshProUGUI>();

        if (classNameText != null) classNameText.text = currentClass.Name;
        if (advText != null) advText.text = "Advantage: " + currentClass.Advantage;
        if (downText != null) downText.text = "Downside: " + currentClass.Downside;
    }

    void NextClass()
    {
        _currentClassIndex = (_currentClassIndex + 1) % Classes.Length;
        UpdateMenuDisplay();
    }

    void PrevClass()
    {
        _currentClassIndex = (_currentClassIndex - 1 + Classes.Length) % Classes.Length;
        UpdateMenuDisplay();
    }

    void SelectClass()
    {
        Debug.Log("Selected class: " + Classes[_currentClassIndex].Name);
        // You can add logic here to actually select the class
        HideMenu();
    }

    void Update()
    {
        if (!_menuActive) return;

        // Arrow keys to navigate
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            NextClass();
        else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            PrevClass();
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            SelectClass();
        else if (Input.GetKeyDown(KeyCode.Escape))
            HideMenu();
    }
}
