using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class InventoryUI : MonoBehaviour
{
    [Header("Slots")]
    public Image[] slotImages;
    public Button[] slotButtons;

    [Header("Visual")]
    public Sprite emptySlotSprite;
    public Color activeColor = Color.white;
    public Color inactiveColor = new Color(1f, 1f, 1f, 0.4f);
    public Vector2 panelSize = new Vector2(560f, 72f);
    public Vector2 panelOffset = new Vector2(0f, 20f);
    public float slotSize = 48f;
    public float slotSpacing = 54f;
    public float slotBottomPadding = 8f;

    PlayerInventory _inventory;
    Canvas _canvas;
    RectTransform _panel;
    Text _woodCountText;
    Text _actionHintText;
    bool _hintPulseEnabled;
    bool _woodSelected;
    int _woodCount;
    int _woodCapacity = 5;
    int _lastWoodSlotIndex = -1;
    float _hintPulseSpeed = 2.2f;
    Color _hintBaseColor = new Color(0.95f, 0.9f, 0.75f, 1f);
    Color _hintPulseColor = new Color(1f, 0.78f, 0.45f, 1f);
    const int WoodPrimarySlotIndex = 5;
    const int WoodSecondarySlotIndex = 6;
    static Sprite _woodSlotIcon;

    void Awake()
    {
        EnsureEventSystem();
        CreateUI();
        RegisterSlotButtons();
    }

    public void Initialize(PlayerInventory inventory)
    {
        _inventory = inventory;
        RegisterSlotButtons();
    }

    void CreateUI()
    {
        if (_canvas != null) return;

        // Create Canvas
        GameObject canvasGO = new GameObject("InventoryCanvas");
        canvasGO.transform.SetParent(transform, false);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 250;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // Create Panel
        GameObject panelGO = new GameObject("InventoryPanel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        _panel = panelGO.AddComponent<RectTransform>();
        _panel.anchorMin = new Vector2(0.5f, 0f);
        _panel.anchorMax = new Vector2(0.5f, 0f);
        _panel.pivot = new Vector2(0.5f, 0f);
        _panel.anchoredPosition = panelOffset;
        _panel.sizeDelta = panelSize;

        Image panelImage = panelGO.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.5f);

        // Wood count display
        GameObject woodCountGO = new GameObject("WoodCount");
        woodCountGO.transform.SetParent(panelGO.transform, false);
        RectTransform woodCountRect = woodCountGO.AddComponent<RectTransform>();
        woodCountRect.anchorMin = new Vector2(1f, 0f);
        woodCountRect.anchorMax = new Vector2(1f, 0f);
        woodCountRect.pivot = new Vector2(1f, 0f);
        woodCountRect.anchoredPosition = new Vector2(-12f, 10f);
        woodCountRect.sizeDelta = new Vector2(150f, 22f);
        _woodCountText = woodCountGO.AddComponent<Text>();
        _woodCountText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
            ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (_woodCountText.font == null)
            _woodCountText.font = Font.CreateDynamicFontFromOSFont("Arial", 12);
        _woodCountText.fontSize = 16;
        _woodCountText.alignment = TextAnchor.LowerRight;
        _woodCountText.color = Color.white;
        _woodCountText.text = $"Wood: 0/{_woodCapacity}";

        // Action hint above inventory (e.g., "Press E to Eat")
        GameObject hintGO = new GameObject("ActionHint");
        hintGO.transform.SetParent(panelGO.transform, false);
        RectTransform hintRect = hintGO.AddComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0.5f, 1f);
        hintRect.anchorMax = new Vector2(0.5f, 1f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.anchoredPosition = new Vector2(0f, 8f);
        hintRect.sizeDelta = new Vector2(420f, 28f);

        _actionHintText = hintGO.AddComponent<Text>();
        _actionHintText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
            ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (_actionHintText.font == null)
            _actionHintText.font = Font.CreateDynamicFontFromOSFont("Arial", 12);
        _actionHintText.fontSize = 18;
        _actionHintText.alignment = TextAnchor.MiddleCenter;
        _actionHintText.color = _hintBaseColor;
        _actionHintText.text = "";

        // Create Slots
        slotImages = new Image[10];
        slotButtons = new Button[10];

        for (int i = 0; i < 10; i++)
        {
            GameObject slotGO = new GameObject($"Slot{i+1}");
            slotGO.transform.SetParent(_panel, false);
            RectTransform slotRect = slotGO.AddComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(0.5f, 0f);
            slotRect.anchorMax = new Vector2(0.5f, 0f);
            slotRect.pivot = new Vector2(0.5f, 0f);
            float totalWidth = slotSpacing * (slotImages.Length - 1) + slotSize;
            float startX = -totalWidth * 0.5f + slotSize * 0.5f;
            slotRect.anchoredPosition = new Vector2(startX + i * slotSpacing, slotBottomPadding);
            slotRect.sizeDelta = new Vector2(slotSize, slotSize);

            // Background Image
            Image bgImage = slotGO.AddComponent<Image>();
            bgImage.sprite = emptySlotSprite;
            bgImage.color = inactiveColor;
            bgImage.raycastTarget = true;
            slotImages[i] = bgImage;

            // Button
            Button button = slotGO.AddComponent<Button>();
            button.targetGraphic = bgImage;
            slotButtons[i] = button;

            // Text for number
            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(slotGO.transform, false);
            Text text = textGO.AddComponent<Text>();
            text.text = (i + 1).ToString();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (text.font == null)
                text.font = Font.CreateDynamicFontFromOSFont("Arial", 12);
            text.fontSize = 14;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            RectTransform textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
        }
    }

    void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        GameObject eventSystemGo = new GameObject("EventSystem");
        eventSystemGo.AddComponent<EventSystem>();
        eventSystemGo.AddComponent<StandaloneInputModule>();
    }

    void RegisterSlotButtons()
    {
        if (slotButtons == null)
            return;

        for (int i = 0; i < slotButtons.Length; i++)
        {
            int index = i;
            if (slotButtons[i] != null)
            {
                slotButtons[i].onClick.RemoveAllListeners();
                slotButtons[i].onClick.AddListener(() => OnSlotClicked(index));
            }
        }
    }

    public void SetInventory(List<Weapon> weapons, int activeIndex)
    {
        if (slotImages == null || slotImages.Length == 0)
            return;

        int slotCount = Mathf.Min(slotImages.Length, Mathf.Max(0, slotButtons != null ? slotButtons.Length : slotImages.Length));
        if (slotCount == 0)
            slotCount = slotImages.Length;

        for (int i = 0; i < slotImages.Length; i++)
        {
            if (slotImages[i] == null)
                continue;

            if (i < weapons.Count && weapons[i] != null && weapons[i].icon != null)
            {
                slotImages[i].sprite = weapons[i].icon;
                slotImages[i].color = (i == activeIndex) ? activeColor : inactiveColor;
            }
            else
            {
                slotImages[i].sprite = emptySlotSprite;
                slotImages[i].color = inactiveColor;
            }

            if (slotButtons != null && i < slotButtons.Length && slotButtons[i] != null)
                slotButtons[i].interactable = i < weapons.Count;
        }

        ApplyWoodSlot();
    }

    void OnSlotClicked(int slotIndex)
    {
        if (_inventory == null)
            return;

        _inventory.SetActiveSlot(slotIndex);
    }

    public void SetWoodCount(int count)
    {
        _woodCount = Mathf.Max(0, count);
        if (_woodCount <= 0)
            _woodSelected = false;
        if (_woodCountText != null)
            _woodCountText.text = $"Wood: {_woodCount}/{Mathf.Max(1, _woodCapacity)}";

        ApplyWoodSlot();
    }

    public void SetWoodCapacity(int capacity)
    {
        _woodCapacity = Mathf.Max(1, capacity);
        if (_woodCountText != null)
            _woodCountText.text = $"Wood: {_woodCount}/{_woodCapacity}";
    }

    void ApplyWoodSlot()
    {
        if (slotImages == null || slotImages.Length == 0)
            return;

        int targetSlot = ResolveWoodSlotIndex();
        if (_lastWoodSlotIndex >= 0 && _lastWoodSlotIndex != targetSlot)
            RestoreInventorySlot(_lastWoodSlotIndex);

        if (_woodCount > 0 && IsValidSlotIndex(targetSlot))
        {
            slotImages[targetSlot].sprite = GetWoodSlotIcon();
            slotImages[targetSlot].color = _woodSelected ? activeColor : new Color(1f, 1f, 1f, 0.78f);
            if (slotButtons != null && slotButtons.Length > targetSlot && slotButtons[targetSlot] != null)
                slotButtons[targetSlot].interactable = false;
            _lastWoodSlotIndex = targetSlot;
            return;
        }

        if (_lastWoodSlotIndex >= 0)
        {
            RestoreInventorySlot(_lastWoodSlotIndex);
            _lastWoodSlotIndex = -1;
        }
    }

    int ResolveWoodSlotIndex()
    {
        if (_woodCount <= 0)
            return _lastWoodSlotIndex >= 0 ? _lastWoodSlotIndex : WoodPrimarySlotIndex;

        return IsInventorySlotOccupied(WoodPrimarySlotIndex) ? WoodSecondarySlotIndex : WoodPrimarySlotIndex;
    }

    public void SetWoodSelected(bool selected)
    {
        _woodSelected = selected && _woodCount > 0;
        ApplyWoodSlot();
    }

    bool IsInventorySlotOccupied(int slotIndex)
    {
        return _inventory != null
            && _inventory.weapons != null
            && slotIndex >= 0
            && slotIndex < _inventory.weapons.Count
            && _inventory.weapons[slotIndex] != null;
    }

    bool IsValidSlotIndex(int slotIndex)
    {
        return slotIndex >= 0 && slotImages != null && slotIndex < slotImages.Length && slotImages[slotIndex] != null;
    }

    void RestoreInventorySlot(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex))
            return;

        if (IsInventorySlotOccupied(slotIndex))
        {
            Weapon weapon = _inventory.weapons[slotIndex];
            slotImages[slotIndex].sprite = weapon.icon != null ? weapon.icon : emptySlotSprite;
            slotImages[slotIndex].color = slotIndex == _inventory.activeIndex ? activeColor : inactiveColor;
            if (slotButtons != null && slotButtons.Length > slotIndex && slotButtons[slotIndex] != null)
                slotButtons[slotIndex].interactable = true;
            return;
        }

        slotImages[slotIndex].sprite = emptySlotSprite;
        slotImages[slotIndex].color = inactiveColor;
        if (slotButtons != null && slotButtons.Length > slotIndex && slotButtons[slotIndex] != null)
            slotButtons[slotIndex].interactable = false;
    }

    static Sprite GetWoodSlotIcon()
    {
        if (_woodSlotIcon != null)
            return _woodSlotIcon;

        Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        texture.name = "GeneratedWoodSlotIcon";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
                texture.SetPixel(x, y, clear);
        }

        DrawLog(texture, new Vector2(0.42f, 0.42f), 0.1f, 0.52f, -0.55f);
        DrawLog(texture, new Vector2(0.52f, 0.55f), 0.1f, 0.5f, -0.55f);
        DrawLog(texture, new Vector2(0.34f, 0.58f), 0.085f, 0.42f, -0.55f);
        texture.Apply();

        _woodSlotIcon = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 64f);
        _woodSlotIcon.name = "Wood Slot Icon";
        return _woodSlotIcon;
    }

    static void DrawLog(Texture2D texture, Vector2 center, float radius, float length, float rotation)
    {
        Color barkDark = new Color(0.18f, 0.1f, 0.045f, 1f);
        Color barkMid = new Color(0.43f, 0.25f, 0.1f, 1f);
        Color barkLight = new Color(0.66f, 0.43f, 0.19f, 1f);
        Color cut = new Color(0.83f, 0.61f, 0.34f, 1f);
        Color ring = new Color(0.36f, 0.2f, 0.08f, 1f);

        float cos = Mathf.Cos(rotation);
        float sin = Mathf.Sin(rotation);

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                float nx = (x + 0.5f) / texture.width;
                float ny = (y + 0.5f) / texture.height;
                Vector2 p = new Vector2(nx - center.x, ny - center.y);
                float u = p.x * cos + p.y * sin;
                float v = -p.x * sin + p.y * cos;

                if (u > -length * 0.5f && u < length * 0.5f && Mathf.Abs(v) < radius)
                {
                    float shade = 1f - Mathf.Abs(v) / radius;
                    Color color = Color.Lerp(barkDark, barkMid, shade);
                    if (v > radius * 0.25f)
                        color = Color.Lerp(color, barkLight, 0.45f);
                    if (Mathf.Abs(Mathf.Sin((u + 0.2f) * 42f)) > 0.88f)
                        color = Color.Lerp(color, barkDark, 0.42f);
                    BlendPixel(texture, x, y, color, 1f);
                }

                float endLeft = Ellipse(u, v, -length * 0.5f, 0f, radius * 0.58f, radius);
                float endRight = Ellipse(u, v, length * 0.5f, 0f, radius * 0.58f, radius);
                float endMask = Mathf.Max(endLeft, endRight);
                if (endMask > 0f)
                {
                    Color color = Color.Lerp(ring, cut, endMask);
                    BlendPixel(texture, x, y, color, endMask);

                    float innerRing = Mathf.Max(
                        Ellipse(u, v, -length * 0.5f, 0f, radius * 0.28f, radius * 0.48f),
                        Ellipse(u, v, length * 0.5f, 0f, radius * 0.28f, radius * 0.48f));
                    if (innerRing > 0f)
                        BlendPixel(texture, x, y, ring, innerRing * 0.55f);
                }
            }
        }
    }

    static float Ellipse(float x, float y, float cx, float cy, float rx, float ry)
    {
        float dx = x - cx;
        float dy = y - cy;
        float d = (dx * dx) / (rx * rx) + (dy * dy) / (ry * ry);
        return Mathf.Clamp01(1f - d);
    }

    static void BlendPixel(Texture2D texture, int x, int y, Color color, float alpha)
    {
        alpha = Mathf.Clamp01(alpha);
        if (alpha <= 0f)
            return;

        Color existing = texture.GetPixel(x, y);
        float outAlpha = alpha + existing.a * (1f - alpha);
        if (outAlpha <= 0f)
            return;

        float existingFactor = existing.a * (1f - alpha);
        Color blended = new Color(
            (color.r * alpha + existing.r * existingFactor) / outAlpha,
            (color.g * alpha + existing.g * existingFactor) / outAlpha,
            (color.b * alpha + existing.b * existingFactor) / outAlpha,
            outAlpha);
        texture.SetPixel(x, y, blended);
    }

    public void SetActionHint(string message)
    {
        if (_actionHintText == null)
            return;

        _actionHintText.text = string.IsNullOrWhiteSpace(message) ? "" : message;
        if (string.IsNullOrWhiteSpace(_actionHintText.text))
            _actionHintText.color = _hintBaseColor;
    }

    public void SetActionHintPulse(bool enabled)
    {
        _hintPulseEnabled = enabled;
        if (!enabled && _actionHintText != null)
            _actionHintText.color = _hintBaseColor;
    }

    void Update()
    {
        if (_actionHintText == null)
            return;

        if (string.IsNullOrWhiteSpace(_actionHintText.text))
            return;

        if (!_hintPulseEnabled)
            return;

        float t = (Mathf.Sin(Time.unscaledTime * _hintPulseSpeed) + 1f) * 0.5f;
        float eased = t * t * (3f - 2f * t);
        _actionHintText.color = Color.Lerp(_hintBaseColor, _hintPulseColor, eased);
    }
}
