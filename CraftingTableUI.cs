using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CraftingTableUI : MonoBehaviour
{
    struct CraftRecipeView
    {
        public string Name;
        public string Cost;
        public int WoodCost;
        public int ScrapCost;
        public bool IsSackUpgrade;
        public CraftableItemKind ItemKind;

        public CraftRecipeView(string name, string cost, int woodCost, int scrapCost, CraftableItemKind itemKind, bool isSackUpgrade = false)
        {
            Name = name;
            Cost = cost;
            WoodCost = woodCost;
            ScrapCost = scrapCost;
            IsSackUpgrade = isSackUpgrade;
            ItemKind = itemKind;
        }
    }

    static readonly CraftRecipeView[] Recipes =
    {
        new CraftRecipeView("Wooden Barrier", "Cost: 5 wood", 5, 0, CraftableItemKind.WoodenBarrier),
        new CraftRecipeView("Farm Plot", "Cost: 10 wood", 10, 0, CraftableItemKind.FarmPlot),
        new CraftRecipeView("Recipe Unlocking", "Cost: 30 wood, 5 scraps", 30, 5, CraftableItemKind.RecipeBook),
        new CraftRecipeView("Torch", "Cost: 15 wood, 1 scrap", 15, 1, CraftableItemKind.Torch),
        new CraftRecipeView("Crafting Table Level 2", "Cost: 50 wood, 10 scraps", 50, 10, CraftableItemKind.CraftingTableLevel2),
        new CraftRecipeView("Sack Upgrade", "Cost: 5 wood", 5, 0, CraftableItemKind.RecipeBook, true)
    };

    static CraftingTableUI _instance;
    static int _lastClosedFrame = -1;
    static Sprite _panelTextureSprite;
    static Sprite _slotTextureSprite;

    Canvas _canvas;
    GameObject _panel;
    Text _titleText;
    Text _emptyListText;
    CraftingTable _currentTable;
    GameObject _currentInteractor;
    Button[] _craftButtons;
    Image[] _craftButtonImages;
    Text[] _craftButtonTexts;
    Text[] _recipeNameTexts;
    Text[] _recipeCostTexts;
    Coroutine[] _craftButtonFeedbackRoutines;
    bool[] _craftButtonFeedbackActive;
    CursorLockMode _previousCursorLockState;
    bool _previousCursorVisible;
    bool _hasPreviousCursorState;
    int _openedFrame = -1;

    public static bool IsOpen
    {
        get { return _instance != null && _instance._panel != null && _instance._panel.activeSelf; }
    }

    public static bool ClosedThisFrame
    {
        get { return Time.frameCount == _lastClosedFrame; }
    }

    public static void Toggle(CraftingTable table)
    {
        Toggle(table, null);
    }

    public static void Toggle(CraftingTable table, GameObject interactor)
    {
        EnsureInstance();

        if (_instance._panel.activeSelf && _instance._currentTable == table)
            _instance.Hide();
        else
            _instance.Show(table, interactor);
    }

    static void EnsureInstance()
    {
        if (_instance != null)
            return;

        GameObject go = new GameObject("CraftingTableUI");
        _instance = go.AddComponent<CraftingTableUI>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        BuildUI();
        Hide();
    }

    void Update()
    {
        if (_panel != null && _panel.activeSelf && Time.frameCount > _openedFrame && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape)))
            Hide();

        if (_panel != null && _panel.activeSelf)
            UpdateCraftButtonStates();
    }

    void Show(CraftingTable table, GameObject interactor)
    {
        bool wasOpen = _panel != null && _panel.activeSelf;
        _currentTable = table;
        _currentInteractor = interactor;
        if (_titleText != null)
            _titleText.text = table != null ? table.displayName.ToUpperInvariant() : "CRAFTING TABLE";

        if (_emptyListText != null)
            _emptyListText.text = "Available craftable recipes";

        if (!wasOpen)
        {
            _previousCursorLockState = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            _hasPreviousCursorState = true;
        }
        _openedFrame = Time.frameCount;

        _panel.SetActive(true);
        EnsureEventSystem();
        EnsurePlayerCraftingMaterials();
        UpdateCraftButtonStates();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Hide()
    {
        StopCraftButtonFeedback();
        _currentTable = null;
        _currentInteractor = null;
        if (_panel != null)
            _panel.SetActive(false);

        _lastClosedFrame = Time.frameCount;

        if (_hasPreviousCursorState)
        {
            Cursor.lockState = _previousCursorLockState;
            Cursor.visible = _previousCursorVisible;
            _hasPreviousCursorState = false;
        }
    }

    void StopCraftButtonFeedback()
    {
        if (_craftButtonFeedbackRoutines == null)
            return;

        for (int i = 0; i < _craftButtonFeedbackRoutines.Length; i++)
        {
            if (_craftButtonFeedbackRoutines[i] != null)
            {
                StopCoroutine(_craftButtonFeedbackRoutines[i]);
                _craftButtonFeedbackRoutines[i] = null;
            }

            if (_craftButtonFeedbackActive != null && i < _craftButtonFeedbackActive.Length)
                _craftButtonFeedbackActive[i] = false;
        }
    }

    void BuildUI()
    {
        if (_canvas != null)
            return;

        GameObject canvasGo = new GameObject("CraftingTableCanvas");
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 850;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        _panel = new GameObject("CraftingTablePanel");
        _panel.transform.SetParent(canvasGo.transform, false);
        RectTransform panelRt = _panel.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = Vector2.zero;
        panelRt.sizeDelta = new Vector2(680f, 520f);

        Image panelImage = _panel.AddComponent<Image>();
        panelImage.sprite = GetGrainSprite(ref _panelTextureSprite, "CraftingPanelTexture", new Color(0.045f, 0.035f, 0.026f, 1f), new Color(0.09f, 0.064f, 0.038f, 1f));
        panelImage.color = new Color(0.035f, 0.028f, 0.022f, 0.94f);

        CreateHeader(_panel);
        CreateEmptyRecipeList(_panel);
        CreateSlots(_panel);
    }

    void CreateHeader(GameObject parent)
    {
        GameObject titleGo = new GameObject("Title");
        titleGo.transform.SetParent(parent.transform, false);
        RectTransform rt = titleGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -26f);
        rt.sizeDelta = new Vector2(520f, 42f);

        _titleText = titleGo.AddComponent<Text>();
        _titleText.font = GetDefaultFont();
        _titleText.fontSize = 30;
        _titleText.fontStyle = FontStyle.Bold;
        _titleText.alignment = TextAnchor.MiddleCenter;
        _titleText.color = new Color(1f, 0.86f, 0.55f, 1f);
        _titleText.raycastTarget = false;
    }

    void CreateEmptyRecipeList(GameObject parent)
    {
        GameObject listGo = new GameObject("RecipeList");
        listGo.transform.SetParent(parent.transform, false);
        RectTransform rt = listGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -82f);
        rt.sizeDelta = new Vector2(560f, 66f);

        Image bg = listGo.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.28f);

        GameObject textGo = new GameObject("EmptyText");
        textGo.transform.SetParent(listGo.transform, false);
        RectTransform textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(16f, 8f);
        textRt.offsetMax = new Vector2(-16f, -8f);

        _emptyListText = textGo.AddComponent<Text>();
        _emptyListText.font = GetDefaultFont();
        _emptyListText.fontSize = 20;
        _emptyListText.alignment = TextAnchor.MiddleCenter;
        _emptyListText.color = new Color(0.78f, 0.72f, 0.62f, 1f);
        _emptyListText.raycastTarget = false;
    }

    void CreateSlots(GameObject parent)
    {
        GameObject scrollGo = new GameObject("CraftSlotScroll");
        scrollGo.transform.SetParent(parent.transform, false);
        RectTransform scrollRt = scrollGo.AddComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0.5f, 0f);
        scrollRt.anchorMax = new Vector2(0.5f, 0f);
        scrollRt.pivot = new Vector2(0.5f, 0f);
        scrollRt.anchoredPosition = new Vector2(0f, 42f);
        scrollRt.sizeDelta = new Vector2(560f, 300f);

        Image scrollBg = scrollGo.AddComponent<Image>();
        scrollBg.color = new Color(0f, 0f, 0f, 0.18f);

        Mask mask = scrollGo.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        ScrollRect scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.viewport = scrollRt;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 34f;

        GameObject contentGo = new GameObject("Content");
        contentGo.transform.SetParent(scrollGo.transform, false);
        RectTransform contentRt = contentGo.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;

        float slotWidth = 520f;
        float slotHeight = 96f;
        float slotSpacing = 14f;
        float contentHeight = slotHeight * Recipes.Length + slotSpacing * (Recipes.Length - 1) + 18f;
        contentRt.sizeDelta = new Vector2(0f, contentHeight);
        scrollRect.content = contentRt;
        _craftButtons = new Button[Recipes.Length];
        _craftButtonImages = new Image[Recipes.Length];
        _craftButtonTexts = new Text[Recipes.Length];
        _recipeNameTexts = new Text[Recipes.Length];
        _recipeCostTexts = new Text[Recipes.Length];
        _craftButtonFeedbackRoutines = new Coroutine[Recipes.Length];
        _craftButtonFeedbackActive = new bool[Recipes.Length];

        for (int i = 0; i < Recipes.Length; i++)
        {
            CreateRecipeSlot(contentGo, i, slotWidth, slotHeight, slotSpacing);
        }
    }

    void CreateRecipeSlot(GameObject parent, int index, float slotWidth, float slotHeight, float slotSpacing)
    {
        GameObject slotGo = new GameObject($"CraftSlot{index + 1}");
        slotGo.transform.SetParent(parent.transform, false);
        RectTransform rt = slotGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -9f - index * (slotHeight + slotSpacing));
        rt.sizeDelta = new Vector2(slotWidth, slotHeight);

        Image slotImage = slotGo.AddComponent<Image>();
        slotImage.sprite = GetGrainSprite(ref _slotTextureSprite, "CraftingSlotTexture", new Color(0.09f, 0.066f, 0.04f, 1f), new Color(0.22f, 0.14f, 0.075f, 1f));
        slotImage.color = new Color(0.12f, 0.095f, 0.07f, 0.96f);

        GameObject innerGo = CreateRect("Inner", slotGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        RectTransform innerRt = innerGo.GetComponent<RectTransform>();
        innerRt.offsetMin = new Vector2(10f, 10f);
        innerRt.offsetMax = new Vector2(-10f, -10f);
        Image innerImage = innerGo.AddComponent<Image>();
        innerImage.color = new Color(0f, 0f, 0f, 0.22f);
        innerImage.raycastTarget = false;

        GameObject iconFrame = CreateRect("RecipeIcon", slotGo.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(54f, 0f));
        RectTransform iconFrameRt = iconFrame.GetComponent<RectTransform>();
        iconFrameRt.sizeDelta = new Vector2(70f, 70f);
        Image iconFrameImage = iconFrame.AddComponent<Image>();
        iconFrameImage.color = new Color(0.055f, 0.045f, 0.035f, 1f);

        CreateRecipeIcon(iconFrame.transform, index);

        Text nameText = CreateText("RecipeName", slotGo.transform, GetRecipeName(index), 24, FontStyle.Bold, new Color(1f, 0.88f, 0.58f, 1f), TextAnchor.MiddleLeft);
        RectTransform nameRt = nameText.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0f, 0.5f);
        nameRt.anchorMax = new Vector2(0f, 0.5f);
        nameRt.pivot = new Vector2(0f, 0.5f);
        nameRt.anchoredPosition = new Vector2(104f, 18f);
        nameRt.sizeDelta = new Vector2(330f, 30f);

        Text costText = CreateText("RecipeCost", slotGo.transform, GetRecipeCostText(index), 18, FontStyle.Normal, new Color(0.77f, 0.7f, 0.58f, 1f), TextAnchor.MiddleLeft);
        RectTransform costRt = costText.GetComponent<RectTransform>();
        costRt.anchorMin = new Vector2(0f, 0.5f);
        costRt.anchorMax = new Vector2(0f, 0.5f);
        costRt.pivot = new Vector2(0f, 0.5f);
        costRt.anchoredPosition = new Vector2(104f, -16f);
        costRt.sizeDelta = new Vector2(330f, 24f);

        if (_recipeNameTexts != null && index >= 0 && index < _recipeNameTexts.Length)
            _recipeNameTexts[index] = nameText;
        if (_recipeCostTexts != null && index >= 0 && index < _recipeCostTexts.Length)
            _recipeCostTexts[index] = costText;

        CreateAddMaterialsButton(slotGo.transform, index);
        CreateCraftButton(slotGo.transform, index);
    }

    void CreateCraftButton(Transform parent, int index)
    {
        GameObject buttonGo = CreateRect("CraftButton", parent, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, -19f));
        RectTransform buttonRt = buttonGo.GetComponent<RectTransform>();
        buttonRt.sizeDelta = new Vector2(98f, 34f);

        Image buttonImage = buttonGo.AddComponent<Image>();
        buttonImage.color = new Color(1f, 0.76f, 0.18f, 1f);

        Button button = buttonGo.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        int recipeIndex = index;
        button.onClick.AddListener(() => OnCraftButtonPressed(recipeIndex));

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.disabledColor = Color.white;
        colors.colorMultiplier = 1f;
        button.colors = colors;

        Text buttonText = CreateText("CraftButtonText", buttonGo.transform, "CRAFT", 17, FontStyle.Bold, new Color(0.12f, 0.085f, 0.025f, 1f), TextAnchor.MiddleCenter);
        RectTransform textRt = buttonText.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        if (_craftButtons != null && index >= 0 && index < _craftButtons.Length)
        {
            _craftButtons[index] = button;
            _craftButtonImages[index] = buttonImage;
            _craftButtonTexts[index] = buttonText;
        }
    }

    void CreateAddMaterialsButton(Transform parent, int index)
    {
        GameObject buttonGo = CreateRect("AddMaterialsButton", parent, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, 23f));
        RectTransform buttonRt = buttonGo.GetComponent<RectTransform>();
        buttonRt.sizeDelta = new Vector2(98f, 24f);

        Image buttonImage = buttonGo.AddComponent<Image>();
        buttonImage.color = new Color(0.22f, 0.72f, 0.28f, 1f);

        Button button = buttonGo.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        int recipeIndex = index;
        button.onClick.AddListener(() => OnAddMaterialsButtonPressed(recipeIndex));

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.9f, 1f, 0.9f, 1f);
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = Color.white;
        colors.colorMultiplier = 1f;
        button.colors = colors;

        Text buttonText = CreateText("AddMaterialsButtonText", buttonGo.transform, "ADD MATERIALS", 9, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        RectTransform textRt = buttonText.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(3f, 0f);
        textRt.offsetMax = new Vector2(-3f, 0f);
    }

    void OnAddMaterialsButtonPressed(int recipeIndex)
    {
        if (recipeIndex < 0 || recipeIndex >= Recipes.Length)
            return;
    }

    void OnCraftButtonPressed(int recipeIndex)
    {
        if (recipeIndex < 0 || recipeIndex >= Recipes.Length)
            return;

        if (_craftButtonFeedbackRoutines != null && _craftButtonFeedbackRoutines[recipeIndex] != null)
            StopCoroutine(_craftButtonFeedbackRoutines[recipeIndex]);

        _craftButtonFeedbackRoutines[recipeIndex] = StartCoroutine(CraftButtonFeedbackRoutine(recipeIndex, CanCraft(recipeIndex)));
    }

    IEnumerator CraftButtonFeedbackRoutine(int recipeIndex, bool hadMaterialsOnClick)
    {
        SetCraftButtonFeedbackActive(recipeIndex, true);

        if (hadMaterialsOnClick)
            SetCraftButtonVisual(recipeIndex, new Color(0.77f, 0.48f, 0.05f, 1f), new Color(0.08f, 0.055f, 0.018f, 1f));
        else
            SetCraftButtonVisual(recipeIndex, new Color(0.2f, 0.2f, 0.2f, 0.96f), new Color(0.66f, 0.66f, 0.66f, 1f));

        yield return new WaitForSeconds(0.3f);

        if (hadMaterialsOnClick && CanCraft(recipeIndex))
            CompleteCraft(recipeIndex);

        SetCraftButtonFeedbackActive(recipeIndex, false);
        _craftButtonFeedbackRoutines[recipeIndex] = null;
        UpdateCraftButtonStates();
    }

    void CompleteCraft(int recipeIndex)
    {
        if (Recipes[recipeIndex].IsSackUpgrade)
        {
            PlayerWoodCarry carry = ResolvePlayerWoodCarry();
            if (carry != null)
                carry.TryUpgradeSack();
            return;
        }

        PlayerWoodCarry woodCarry = ResolvePlayerWoodCarry();
        PlayerCraftingMaterials materials = ResolvePlayerCraftingMaterials();
        if (woodCarry == null || woodCarry.Current < Recipes[recipeIndex].WoodCost)
            return;

        if (Recipes[recipeIndex].ScrapCost > 0 && (materials == null || materials.Scraps < Recipes[recipeIndex].ScrapCost))
            return;

        if (!woodCarry.TrySpendWood(Recipes[recipeIndex].WoodCost))
            return;

        if (materials != null)
            materials.TrySpendScraps(Recipes[recipeIndex].ScrapCost);

        SpawnCraftedItem(recipeIndex);
    }

    void UpdateCraftButtonStates()
    {
        if (_craftButtons == null)
            return;

        for (int i = 0; i < _craftButtons.Length; i++)
        {
            bool canCraft = CanCraft(i);
            UpdateRecipeText(i);

            if (_craftButtonFeedbackActive != null && _craftButtonFeedbackActive[i])
                continue;

            if (_craftButtons[i] != null)
                _craftButtons[i].interactable = true;

            SetCraftButtonVisual(
                i,
                canCraft ? new Color(1f, 0.76f, 0.18f, 1f) : new Color(0.34f, 0.34f, 0.34f, 0.92f),
                canCraft ? new Color(0.12f, 0.085f, 0.025f, 1f) : new Color(0.78f, 0.78f, 0.78f, 1f));
        }
    }

    void SetCraftButtonFeedbackActive(int recipeIndex, bool active)
    {
        if (_craftButtonFeedbackActive == null || recipeIndex < 0 || recipeIndex >= _craftButtonFeedbackActive.Length)
            return;

        _craftButtonFeedbackActive[recipeIndex] = active;
    }

    void SetCraftButtonVisual(int recipeIndex, Color buttonColor, Color textColor)
    {
        if (_craftButtonImages != null && recipeIndex >= 0 && recipeIndex < _craftButtonImages.Length && _craftButtonImages[recipeIndex] != null)
            _craftButtonImages[recipeIndex].color = buttonColor;

        if (_craftButtonTexts != null && recipeIndex >= 0 && recipeIndex < _craftButtonTexts.Length && _craftButtonTexts[recipeIndex] != null)
            _craftButtonTexts[recipeIndex].color = textColor;
    }

    bool CanCraft(int recipeIndex)
    {
        if (recipeIndex < 0 || recipeIndex >= Recipes.Length)
            return false;

        if (Recipes[recipeIndex].IsSackUpgrade)
        {
            PlayerWoodCarry carry = ResolvePlayerWoodCarry();
            return carry != null && !carry.IsSackMaxLevel && carry.Current >= carry.NextSackUpgradeCost;
        }

        return GetWoodCount() >= Recipes[recipeIndex].WoodCost && GetScrapCount() >= Recipes[recipeIndex].ScrapCost;
    }

    string GetRecipeName(int recipeIndex)
    {
        if (recipeIndex < 0 || recipeIndex >= Recipes.Length)
            return "";

        if (!Recipes[recipeIndex].IsSackUpgrade)
            return Recipes[recipeIndex].Name;

        PlayerWoodCarry carry = ResolvePlayerWoodCarry();
        if (carry == null)
            return "Sack Upgrade";
        if (carry.IsSackMaxLevel)
            return $"Sack Level {carry.SackLevel} (Max)";

        return $"Sack Upgrade Lv {carry.SackLevel} -> {carry.SackLevel + 1}";
    }

    string GetRecipeCostText(int recipeIndex)
    {
        if (recipeIndex < 0 || recipeIndex >= Recipes.Length)
            return "";

        if (!Recipes[recipeIndex].IsSackUpgrade)
            return Recipes[recipeIndex].Cost;

        PlayerWoodCarry carry = ResolvePlayerWoodCarry();
        if (carry == null)
            return "Cost: wood";
        if (carry.IsSackMaxLevel)
            return $"Max capacity: {carry.maxCarry} wood";

        int nextLevel = carry.SackLevel + 1;
        int nextCapacity = GetSackCapacityForLevel(carry, nextLevel);
        return $"Cost: {carry.NextSackUpgradeCost} wood  |  Capacity: {nextCapacity}";
    }

    int GetSackCapacityForLevel(PlayerWoodCarry carry, int level)
    {
        if (carry == null || carry.sackLevelCapacities == null || carry.sackLevelCapacities.Length == 0)
            return 0;

        int index = Mathf.Clamp(level - 1, 0, carry.sackLevelCapacities.Length - 1);
        return carry.sackLevelCapacities[index];
    }

    void UpdateRecipeText(int recipeIndex)
    {
        if (_recipeNameTexts != null && recipeIndex >= 0 && recipeIndex < _recipeNameTexts.Length && _recipeNameTexts[recipeIndex] != null)
            _recipeNameTexts[recipeIndex].text = GetRecipeName(recipeIndex);

        if (_recipeCostTexts != null && recipeIndex >= 0 && recipeIndex < _recipeCostTexts.Length && _recipeCostTexts[recipeIndex] != null)
            _recipeCostTexts[recipeIndex].text = GetRecipeCostText(recipeIndex);
    }

    int GetWoodCount()
    {
        PlayerWoodCarry woodCarry = ResolvePlayerWoodCarry();
        return woodCarry != null ? woodCarry.Current : 0;
    }

    int GetScrapCount()
    {
        PlayerCraftingMaterials materials = ResolvePlayerCraftingMaterials();
        return materials != null ? materials.Scraps : 0;
    }

    PlayerWoodCarry ResolvePlayerWoodCarry()
    {
        if (_currentInteractor != null)
        {
            PlayerWoodCarry carry = _currentInteractor.GetComponent<PlayerWoodCarry>()
                ?? _currentInteractor.GetComponentInParent<PlayerWoodCarry>()
                ?? _currentInteractor.GetComponentInChildren<PlayerWoodCarry>();
            if (carry != null)
                return carry;
        }

        return FindObjectOfType<PlayerWoodCarry>();
    }

    PlayerCraftingMaterials ResolvePlayerCraftingMaterials()
    {
        if (_currentInteractor != null)
        {
            PlayerCraftingMaterials materials = _currentInteractor.GetComponent<PlayerCraftingMaterials>()
                ?? _currentInteractor.GetComponentInParent<PlayerCraftingMaterials>()
                ?? _currentInteractor.GetComponentInChildren<PlayerCraftingMaterials>();
            if (materials != null)
                return materials;
        }

        return FindObjectOfType<PlayerCraftingMaterials>();
    }

    void EnsurePlayerCraftingMaterials()
    {
        if (_currentInteractor == null || ResolvePlayerCraftingMaterials() != null)
            return;

        _currentInteractor.AddComponent<PlayerCraftingMaterials>();
    }

    void SpawnCraftedItem(int recipeIndex)
    {
        if (recipeIndex < 0 || recipeIndex >= Recipes.Length || Recipes[recipeIndex].IsSackUpgrade)
            return;

        Vector3 spawnPosition;
        Quaternion spawnRotation;
        ResolveCraftedSpawnPose(out spawnPosition, out spawnRotation);
        CraftedItemPrefabCatalog.Instance.Spawn(Recipes[recipeIndex].ItemKind, spawnPosition, spawnRotation);
    }

    void ResolveCraftedSpawnPose(out Vector3 position, out Quaternion rotation)
    {
        Transform origin = null;
        if (_currentInteractor != null)
        {
            Camera camera = _currentInteractor.GetComponentInChildren<Camera>(true);
            if (camera != null)
                origin = camera.transform;
            else
                origin = _currentInteractor.transform;
        }

        if (origin == null && _currentTable != null)
            origin = _currentTable.transform;

        if (origin == null)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            return;
        }

        Vector3 forward = origin.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;
        forward.Normalize();

        position = origin.position + forward * 2.1f + Vector3.up * 0.15f;
        rotation = Quaternion.LookRotation(forward, Vector3.up);

        if (Physics.Raycast(position + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 8f, ~0, QueryTriggerInteraction.Ignore))
            position.y = hit.point.y + 0.04f;
    }

    void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        GameObject eventSystemGo = new GameObject("EventSystem");
        eventSystemGo.AddComponent<EventSystem>();
        eventSystemGo.AddComponent<StandaloneInputModule>();
    }

    void CreateRecipeIcon(Transform parent, int index)
    {
        switch (index)
        {
            case 0:
                CreateWoodenBarrierIcon(parent);
                break;
            case 1:
                CreateFarmPlotIcon(parent);
                break;
            case 2:
                CreateRecipeBookIcon(parent);
                break;
            case 3:
                CreateTorchIcon(parent);
                break;
            case 4:
                CreateTableUpgradeIcon(parent);
                break;
            default:
                CreateSackUpgradeIcon(parent);
                break;
        }
    }

    void CreateWoodenBarrierIcon(Transform parent)
    {
        Color plank = new Color(0.55f, 0.31f, 0.13f, 1f);
        Color edge = new Color(0.28f, 0.15f, 0.07f, 1f);
        CreateImageRect("BackPlank", parent, new Vector2(0.5f, 0.5f), new Vector2(50f, 12f), new Vector2(0f, 0f), plank, -28f);
        CreateImageRect("TopPlank", parent, new Vector2(0.5f, 0.5f), new Vector2(50f, 12f), new Vector2(0f, 13f), plank, -10f);
        CreateImageRect("BottomPlank", parent, new Vector2(0.5f, 0.5f), new Vector2(50f, 12f), new Vector2(0f, -13f), plank, 10f);
        CreateImageRect("LeftPost", parent, new Vector2(0.5f, 0.5f), new Vector2(9f, 54f), new Vector2(-19f, 0f), edge, 0f);
        CreateImageRect("RightPost", parent, new Vector2(0.5f, 0.5f), new Vector2(9f, 54f), new Vector2(19f, 0f), edge, 0f);
    }

    void CreateFarmPlotIcon(Transform parent)
    {
        CreateImageRect("Soil", parent, new Vector2(0.5f, 0.5f), new Vector2(52f, 38f), new Vector2(0f, -4f), new Color(0.24f, 0.13f, 0.06f, 1f), 0f);
        CreateImageRect("SoilRim", parent, new Vector2(0.5f, 0.5f), new Vector2(56f, 8f), new Vector2(0f, 17f), new Color(0.44f, 0.25f, 0.11f, 1f), 0f);
        CreateImageRect("SproutA", parent, new Vector2(0.5f, 0.5f), new Vector2(7f, 22f), new Vector2(-15f, 4f), new Color(0.23f, 0.65f, 0.22f, 1f), -18f);
        CreateImageRect("SproutB", parent, new Vector2(0.5f, 0.5f), new Vector2(7f, 26f), new Vector2(0f, 6f), new Color(0.28f, 0.78f, 0.26f, 1f), 8f);
        CreateImageRect("SproutC", parent, new Vector2(0.5f, 0.5f), new Vector2(7f, 20f), new Vector2(15f, 3f), new Color(0.2f, 0.58f, 0.2f, 1f), 18f);
    }

    void CreateRecipeBookIcon(Transform parent)
    {
        CreateImageRect("BookBack", parent, new Vector2(0.5f, 0.5f), new Vector2(46f, 54f), new Vector2(2f, 0f), new Color(0.3f, 0.08f, 0.06f, 1f), 0f);
        CreateImageRect("BookPage", parent, new Vector2(0.5f, 0.5f), new Vector2(36f, 46f), new Vector2(7f, 0f), new Color(0.86f, 0.76f, 0.56f, 1f), 0f);
        CreateImageRect("BookSpine", parent, new Vector2(0.5f, 0.5f), new Vector2(10f, 54f), new Vector2(-18f, 0f), new Color(0.58f, 0.14f, 0.09f, 1f), 0f);
        CreateImageRect("LineA", parent, new Vector2(0.5f, 0.5f), new Vector2(20f, 3f), new Vector2(8f, 9f), new Color(0.42f, 0.31f, 0.2f, 1f), 0f);
        CreateImageRect("LineB", parent, new Vector2(0.5f, 0.5f), new Vector2(18f, 3f), new Vector2(7f, -1f), new Color(0.42f, 0.31f, 0.2f, 1f), 0f);
        CreateImageRect("LineC", parent, new Vector2(0.5f, 0.5f), new Vector2(14f, 3f), new Vector2(5f, -11f), new Color(0.42f, 0.31f, 0.2f, 1f), 0f);
    }

    void CreateTorchIcon(Transform parent)
    {
        CreateImageRect("TorchHandle", parent, new Vector2(0.5f, 0.5f), new Vector2(10f, 50f), new Vector2(0f, -6f), new Color(0.45f, 0.25f, 0.11f, 1f), -18f);
        CreateImageRect("TorchWrap", parent, new Vector2(0.5f, 0.5f), new Vector2(22f, 12f), new Vector2(0f, 10f), new Color(0.24f, 0.14f, 0.08f, 1f), -18f);
        CreateImageRect("FlameOuter", parent, new Vector2(0.5f, 0.5f), new Vector2(24f, 34f), new Vector2(5f, 23f), new Color(1f, 0.33f, 0.08f, 1f), -12f);
        CreateImageRect("FlameInner", parent, new Vector2(0.5f, 0.5f), new Vector2(14f, 22f), new Vector2(5f, 22f), new Color(1f, 0.84f, 0.23f, 1f), -12f);
    }

    void CreateTableUpgradeIcon(Transform parent)
    {
        CreateImageRect("TableTop", parent, new Vector2(0.5f, 0.5f), new Vector2(52f, 15f), new Vector2(0f, 6f), new Color(0.49f, 0.29f, 0.13f, 1f), 0f);
        CreateImageRect("TableFace", parent, new Vector2(0.5f, 0.5f), new Vector2(46f, 20f), new Vector2(0f, -9f), new Color(0.34f, 0.19f, 0.09f, 1f), 0f);
        CreateImageRect("LeftLeg", parent, new Vector2(0.5f, 0.5f), new Vector2(8f, 24f), new Vector2(-18f, -22f), new Color(0.24f, 0.13f, 0.07f, 1f), 0f);
        CreateImageRect("RightLeg", parent, new Vector2(0.5f, 0.5f), new Vector2(8f, 24f), new Vector2(18f, -22f), new Color(0.24f, 0.13f, 0.07f, 1f), 0f);

        Text levelText = CreateText("LevelText", parent, "II", 22, FontStyle.Bold, new Color(1f, 0.78f, 0.28f, 1f), TextAnchor.MiddleCenter);
        RectTransform rt = levelText.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -8f);
        rt.sizeDelta = new Vector2(40f, 26f);
    }

    void CreateSackUpgradeIcon(Transform parent)
    {
        CreateImageRect("SackBody", parent, new Vector2(0.5f, 0.5f), new Vector2(46f, 48f), new Vector2(0f, -4f), new Color(0.46f, 0.31f, 0.17f, 1f), 0f);
        CreateImageRect("SackShadow", parent, new Vector2(0.5f, 0.5f), new Vector2(38f, 40f), new Vector2(3f, -8f), new Color(0.24f, 0.16f, 0.09f, 0.75f), 0f);
        CreateImageRect("SackTop", parent, new Vector2(0.5f, 0.5f), new Vector2(34f, 12f), new Vector2(0f, 20f), new Color(0.33f, 0.22f, 0.12f, 1f), 0f);
        CreateImageRect("Tie", parent, new Vector2(0.5f, 0.5f), new Vector2(42f, 5f), new Vector2(0f, 14f), new Color(0.75f, 0.56f, 0.28f, 1f), 0f);
        CreateImageRect("Patch", parent, new Vector2(0.5f, 0.5f), new Vector2(15f, 12f), new Vector2(-9f, -6f), new Color(0.58f, 0.4f, 0.22f, 1f), -8f);
        CreateImageRect("StitchA", parent, new Vector2(0.5f, 0.5f), new Vector2(18f, 3f), new Vector2(10f, 2f), new Color(0.9f, 0.72f, 0.42f, 1f), 20f);
        CreateImageRect("StitchB", parent, new Vector2(0.5f, 0.5f), new Vector2(14f, 3f), new Vector2(7f, -10f), new Color(0.9f, 0.72f, 0.42f, 1f), -18f);
    }

    GameObject CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPosition;
        return go;
    }

    Image CreateImageRect(string name, Transform parent, Vector2 anchor, Vector2 size, Vector2 position, Color color, float rotation)
    {
        GameObject go = CreateRect(name, parent, anchor, anchor, new Vector2(0.5f, 0.5f), position);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.localRotation = Quaternion.Euler(0f, 0f, rotation);
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    Text CreateText(string name, Transform parent, string value, int fontSize, FontStyle fontStyle, Color color, TextAnchor alignment)
    {
        GameObject textGo = new GameObject(name);
        textGo.transform.SetParent(parent, false);
        RectTransform rt = textGo.AddComponent<RectTransform>();
        rt.sizeDelta = Vector2.zero;

        Text text = textGo.AddComponent<Text>();
        text.font = GetDefaultFont();
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    Sprite GetGrainSprite(ref Sprite cache, string textureName, Color low, Color high)
    {
        if (cache != null)
            return cache;

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = textureName;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Repeat;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float grain = Mathf.PerlinNoise(x * 0.18f, y * 0.05f);
                float scratch = Mathf.PerlinNoise((x + 37f) * 0.55f, (y + 11f) * 0.18f) * 0.25f;
                texture.SetPixel(x, y, Color.Lerp(low, high, Mathf.Clamp01(grain * 0.75f + scratch)));
            }
        }

        texture.Apply();
        cache = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        cache.name = textureName + "Sprite";
        return cache;
    }

    Font GetDefaultFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return font;
    }
}
