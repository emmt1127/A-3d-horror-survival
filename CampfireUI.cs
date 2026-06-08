using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen-space campfire UI showing fuel level near the minimap.
/// Auto-creates at runtime if UI references are left empty.
/// </summary>
public class CampfireUI : MonoBehaviour
{
    [Header("Optional overrides (leave empty for auto-build)")]
    public Text fuelLabel;
    public Image fuelBar;
    public RectTransform uiRoot;

    [Header("Layout (used only when auto-building UI)")]
    public Vector2 panelSize = new Vector2(230f, 70f);
    public Vector2 topRightOffset = new Vector2(-18f, -210f);
    public int canvasSortOrder = 10000;
    public Color panelColor = new Color(0f, 0f, 0f, 0.78f);
    public Color panelBorderColor = new Color(0.12f, 0.18f, 0.12f, 1f);
    public Color fuelBarColor = new Color(0.1f, 0.8f, 0.1f, 0.9f);
    public Color labelColor = new Color(0.94f, 0.96f, 0.92f, 1f);

    Campfire _campfire;
    GameObject _builtCanvas;
    Text _builtLabel;
    Image _builtBar;
    Text _builtPercentText;
    RectTransform _builtRoot;

    void Start()
    {
        if (FindObjectOfType<ForestMinimapUI>() != null)
        {
            enabled = false;
            return;
        }

        _campfire = FindObjectOfType<Campfire>();

        if (_campfire == null)
        {
            StartCoroutine(DelayedStart());
            return;
        }

        if (fuelLabel != null && fuelBar != null)
        {
            _builtLabel = fuelLabel;
            _builtBar = fuelBar;
            _builtRoot = uiRoot ?? fuelLabel.transform.parent as RectTransform;
        }
        else
        {
            BuildUI();
        }
    }

    System.Collections.IEnumerator DelayedStart()
    {
        yield return null;
        _campfire = FindObjectOfType<Campfire>();
        if (_campfire == null)
        {
            Debug.LogWarning("CampfireUI: No Campfire found in scene.");
            yield break;
        }

        BuildUI();
    }

    void BuildUI()
    {
        Canvas screenCanvas = FindExistingCanvas();
        if (screenCanvas == null)
        {
            GameObject canvasGo = new GameObject("CampfireCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            screenCanvas = canvasGo.GetComponent<Canvas>();
            screenCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            screenCanvas.overrideSorting = true;
            screenCanvas.sortingOrder = canvasSortOrder;
            canvasGo.layer = LayerMask.NameToLayer("UI");

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.7f;
        }

        _builtCanvas = new GameObject("CampfireUIPanel");
        _builtRoot = _builtCanvas.AddComponent<RectTransform>();

        RectTransform healthRect = GetHealthUIRect();
        if (healthRect != null)
        {
            RectTransform parentRect = healthRect.parent as RectTransform;
            _builtRoot.SetParent(parentRect != null ? parentRect : screenCanvas.transform, false);
            _builtRoot.anchorMin = healthRect.anchorMin;
            _builtRoot.anchorMax = healthRect.anchorMax;
            _builtRoot.pivot = new Vector2(healthRect.pivot.x, 1f);
            _builtRoot.anchoredPosition = new Vector2(healthRect.anchoredPosition.x, healthRect.anchoredPosition.y - healthRect.rect.height - 8f);
        }
        else
        {
            _builtRoot.SetParent(screenCanvas.transform, false);
            _builtRoot.anchorMin = new Vector2(1f, 1f);
            _builtRoot.anchorMax = new Vector2(1f, 1f);
            _builtRoot.pivot = new Vector2(1f, 1f);
            _builtRoot.anchoredPosition = topRightOffset;
        }

        _builtRoot.SetAsLastSibling();
        _builtRoot.sizeDelta = panelSize;

        Image panelImage = _builtCanvas.AddComponent<Image>();
        panelImage.color = panelColor;
        panelImage.raycastTarget = false;

        // Fuel label
        GameObject labelGo = new GameObject("FuelLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelGo.transform.SetParent(_builtRoot, false);
        RectTransform labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.anchoredPosition = new Vector2(0f, -8f);
        labelRect.sizeDelta = new Vector2(-16f, 20f);

        _builtLabel = labelGo.GetComponent<Text>();
        _builtLabel.alignment = TextAnchor.MiddleCenter;
        _builtLabel.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _builtLabel.fontSize = 16;
        _builtLabel.fontStyle = FontStyle.Bold;
        _builtLabel.color = labelColor;
        _builtLabel.text = "Fire";

        // Fuel bar background
        GameObject barBgGo = new GameObject("BarBg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        barBgGo.transform.SetParent(_builtRoot, false);
        RectTransform barBgRect = barBgGo.GetComponent<RectTransform>();
        barBgRect.anchorMin = new Vector2(0f, 0f);
        barBgRect.anchorMax = new Vector2(1f, 0.6f);
        barBgRect.offsetMin = new Vector2(8f, 8f);
        barBgRect.offsetMax = new Vector2(-8f, -8f);

        Image barBgImage = barBgGo.GetComponent<Image>();
        barBgImage.color = new Color(0f, 0f, 0f, 0.5f);

        // Fuel bar fill
        GameObject barFillGo = new GameObject("BarFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        barFillGo.transform.SetParent(barBgGo.transform, false);
        RectTransform barFillRect = barFillGo.GetComponent<RectTransform>();
        barFillRect.anchorMin = Vector2.zero;
        barFillRect.anchorMax = Vector2.one;
        barFillRect.offsetMin = Vector2.zero;
        barFillRect.offsetMax = Vector2.zero;

        _builtBar = barFillGo.GetComponent<Image>();
        _builtBar.color = fuelBarColor;
        _builtBar.type = Image.Type.Filled;
        _builtBar.fillMethod = Image.FillMethod.Horizontal;
        _builtBar.fillOrigin = (int)Image.OriginHorizontal.Left;
        _builtBar.fillAmount = 1f;

        // Fuel percent text on bar
        GameObject percentGo = new GameObject("Percent", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        percentGo.transform.SetParent(barBgGo.transform, false);
        RectTransform percentRect = percentGo.GetComponent<RectTransform>();
        percentRect.anchorMin = Vector2.zero;
        percentRect.anchorMax = Vector2.one;
        percentRect.offsetMin = Vector2.zero;
        percentRect.offsetMax = Vector2.zero;

        _builtPercentText = percentGo.GetComponent<Text>();
        _builtPercentText.alignment = TextAnchor.MiddleCenter;
        _builtPercentText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _builtPercentText.fontSize = 14;
        _builtPercentText.fontStyle = FontStyle.Bold;
        _builtPercentText.color = Color.white;
        _builtPercentText.text = "100%";

        // Keep bar fill inside its parent so the fill amount updates correctly
        _builtBar.transform.SetParent(barBgGo.transform, false);
    }

    Canvas FindExistingCanvas()
    {
        Canvas[] canvases = Object.FindObjectsOfType<Canvas>();
        return (canvases != null && canvases.Length > 0) ? canvases[0] : null;
    }

    RectTransform GetHealthUIRect()
    {
        HealthUI healthUI = Object.FindObjectOfType<HealthUI>();
        if (healthUI == null)
            return null;
        return healthUI.GetComponent<RectTransform>();
    }

    void Update()
    {
        if (_campfire == null)
            return;

        if (_builtLabel != null)
        {
            if (_campfire.IsLit)
                _builtLabel.text = "🔥 Fire";
            else
                _builtLabel.text = "Fire (Out)";
        }

        if (_builtBar != null)
        {
            float maxFuel = _campfire.maxFuel;
            float currentFuel = _campfire.currentFuel;

            float ratio = maxFuel > 0f ? currentFuel / maxFuel : 0f;
            _builtBar.fillAmount = Mathf.Clamp01(ratio);

            if (_builtPercentText != null)
            {
                _builtPercentText.text = Mathf.RoundToInt(ratio * 100f) + "%";
            }
        }
    }
}
