using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Top-right minimap: terrain from above, player heading, camp marker, distance.
/// If you leave UI references empty, the whole HUD block is created at runtime (no prefabs).
/// Add <see cref="CampLocator"/> on your campfire for the camp dot and distance.
/// </summary>
public class ForestMinimapUI : MonoBehaviour
{
    public Transform player;
    public bool autoFindPlayer = true;

    [Header("Optional overrides (leave empty for auto-build)")]
    public RawImage minimapDisplay;
    public RectTransform iconsRoot;
    public RectTransform playerMarker;
    public RectTransform campMarker;
    public Text distanceLabel;
    public Text campfireFuelLabel;
    public Text dayNightTimerLabel;
    public Text personalRecordLabel;
    
    [Header("Day/Night Timer")]
    public bool showDayNightTimer = true;
    public Vector2 timerOffset = Vector2.zero;
    
    private DayNightCycle _dayNightCycle;

    [Header("Layout (used only when auto-building UI)")]
    public Vector2 panelSize = new Vector2(340f, 430f);
    public Vector2 anchorOffsetFromCorner = new Vector2(-24f, -24f);
    public int canvasSortOrder = 320;
    public float panelPadding = 12f;
    public Color panelColor = new Color(0.055f, 0.065f, 0.055f, 0.88f);
    public Color panelBorderColor = new Color(0.38f, 0.34f, 0.22f, 1f);
    public Color mapBackgroundColor = new Color(0.11f, 0.13f, 0.10f, 1f);
    public Color playerIconColor = new Color(0.93f, 0.96f, 0.90f, 1f);
    public Color campIconColor = new Color(1f, 0.55f, 0.18f, 1f);
    public Color labelColor = new Color(0.86f, 0.82f, 0.68f, 1f);
    public Color gridColor = new Color(0.58f, 0.52f, 0.34f, 0.24f);
    public Color mapTint = new Color(0.76f, 0.86f, 0.68f, 1f);
    public bool showCompassMarks = true;
    public bool showMapGrid = true;

    [Header("Minimap camera")]
    public Camera minimapCamera;
    public bool createCameraIfMissing = true;
    public float cameraHeight = 240f;
    public float mapHalfExtentWorld = 110f;
    public LayerMask minimapCullingMask = ~0;
    [Range(64, 1024)]
    public int renderTextureSize = 512;
    public Color minimapClearColor = new Color(0.04f, 0.06f, 0.05f, 1f);

    RenderTexture _rt;
    GameObject _createdCameraObject;
    GameObject _builtCanvas;
    GameObject _builtPanel;
    Texture2D _playerMarkerTexture;
    Texture2D _campMarkerTexture;
    Texture2D _gridTexture;
    Texture2D _vignetteTexture;
    Sprite _playerMarkerSprite;
    Sprite _campMarkerSprite;
    Sprite _gridSprite;
    Sprite _vignetteSprite;

    RawImage _display;
    RectTransform _iconsRoot;
    RectTransform _playerMarkerRt;
    RectTransform _campMarkerRt;
    Text _distanceText;
    Text _fuelText;
    Text _personalRecordText;
    Campfire _campfire;

    void Awake()
    {
        if (player == null && autoFindPlayer)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p == null)
            {
                // Fallback: find by controller component
                controller ctrl = FindObjectOfType<controller>();
                if (ctrl != null)
                    p = ctrl.gameObject;
            }
            if (p != null)
                player = p.transform;
        }
    }

    void Start()
    {
        if (minimapDisplay == null)
            BuildRuntimeUI();
        else
            WireManualReferences();

        _display = minimapDisplay;
        _iconsRoot = iconsRoot;
        _playerMarkerRt = playerMarker;
        _campMarkerRt = campMarker;
        _distanceText = distanceLabel;
        _fuelText = campfireFuelLabel;
        
        // Find day/night cycle
        _dayNightCycle = FindObjectOfType<DayNightCycle>();
        if (_dayNightCycle != null)
        {
            _dayNightCycle.OnDayStarted -= HandleDayStarted;
            _dayNightCycle.OnDayStarted += HandleDayStarted;
        }
        
        // Create day/night timer UI if needed
        if (showDayNightTimer && dayNightTimerLabel == null)
            CreateDayNightTimerUI();
        if (personalRecordLabel == null)
            CreatePersonalRecordUI();

        if (_display == null)
        {
            Debug.LogError("ForestMinimapUI: Failed to create or assign minimap display.", this);
            enabled = false;
            return;
        }

        _rt = new RenderTexture(renderTextureSize, renderTextureSize, 16, RenderTextureFormat.ARGB32)
        {
            name = "ForestMinimapRT",
            antiAliasing = 1,
            filterMode = FilterMode.Bilinear
        };
        _rt.Create();
        _display.texture = _rt;
        _personalRecordText = personalRecordLabel;
        UpdatePersonalRecordLabel();

        _campfire = FindObjectOfType<Campfire>();
        if (minimapCamera == null && createCameraIfMissing)
        {
            _createdCameraObject = new GameObject("MinimapWorldCamera");
            _createdCameraObject.transform.SetParent(null);
            minimapCamera = _createdCameraObject.AddComponent<Camera>();
            minimapCamera.enabled = true;
        }

        if (minimapCamera != null)
        {
            minimapCamera.targetTexture = _rt;
            minimapCamera.cullingMask = minimapCullingMask;
            minimapCamera.orthographic = true;
            minimapCamera.orthographicSize = mapHalfExtentWorld;
            minimapCamera.nearClipPlane = 0.3f;
            minimapCamera.farClipPlane = Mathf.Max(400f, cameraHeight + 200f);
            minimapCamera.clearFlags = CameraClearFlags.SolidColor;
            minimapCamera.backgroundColor = minimapClearColor;
            minimapCamera.allowMSAA = false;
        }

        UpdateDayNightTimer();
    }

    void WireManualReferences()
    {
        _display = minimapDisplay;
        _iconsRoot = iconsRoot;
        _playerMarkerRt = playerMarker;
        _campMarkerRt = campMarker;
        _distanceText = distanceLabel;
        _fuelText = campfireFuelLabel;
        _personalRecordText = personalRecordLabel;
    }

    void BuildRuntimeUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindAnyCanvasInScene();

        if (canvas == null)
        {
            _builtCanvas = new GameObject("ForestMinimap_Canvas");
            _builtCanvas.transform.SetParent(transform, false);
            canvas = _builtCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = canvasSortOrder;
            CanvasScaler scaler = _builtCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _builtCanvas.AddComponent<GraphicRaycaster>();
        }

        _builtPanel = new GameObject("ForestMinimap_Panel");
        _builtPanel.transform.SetParent(canvas.transform, false);
        RectTransform panelRt = _builtPanel.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(1f, 1f);
        panelRt.anchorMax = new Vector2(1f, 1f);
        panelRt.pivot = new Vector2(1f, 1f);
        panelRt.anchoredPosition = anchorOffsetFromCorner;
        panelRt.sizeDelta = panelSize;

        Image frame = _builtPanel.AddComponent<Image>();
        frame.color = panelColor;
        frame.raycastTarget = false;

        GameObject borderGo = new GameObject("Border");
        borderGo.transform.SetParent(_builtPanel.transform, false);
        RectTransform borderRt = borderGo.AddComponent<RectTransform>();
        StretchFull(borderRt, 0f);
        Image borderImg = borderGo.AddComponent<Image>();
        borderImg.color = panelBorderColor;
        borderImg.raycastTarget = false;

        GameObject innerGo = new GameObject("Inner");
        innerGo.transform.SetParent(_builtPanel.transform, false);
        RectTransform innerRt = innerGo.AddComponent<RectTransform>();
        StretchFull(innerRt, panelPadding);
        Image innerImg = innerGo.AddComponent<Image>();
        innerImg.color = new Color(0.02f, 0.026f, 0.022f, 0.55f);
        innerImg.raycastTarget = false;

        const float labelHeight = 24f;
        float bottomArea = labelHeight * 3f + panelPadding * 2f + 8f;
        float mapSide = Mathf.Min(panelSize.x - panelPadding * 2f, panelSize.y - panelPadding - labelHeight - bottomArea);

        GameObject titleGo = new GameObject("MinimapTitle");
        titleGo.transform.SetParent(_builtPanel.transform, false);
        RectTransform titleRt = titleGo.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -panelPadding);
        titleRt.sizeDelta = new Vector2(0f, labelHeight);
        Text titleText = titleGo.AddComponent<Text>();
        titleText.font = GetDefaultUIFont();
        titleText.text = "FIELD MAP";
        titleText.fontSize = 15;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = labelColor;
        titleText.raycastTarget = false;

        GameObject mapBlock = new GameObject("MapBlock");
        mapBlock.transform.SetParent(_builtPanel.transform, false);
        RectTransform mapBlockRt = mapBlock.AddComponent<RectTransform>();
        mapBlockRt.anchorMin = new Vector2(0.5f, 1f);
        mapBlockRt.anchorMax = new Vector2(0.5f, 1f);
        mapBlockRt.pivot = new Vector2(0.5f, 1f);
        mapBlockRt.anchoredPosition = new Vector2(0f, -(panelPadding * 2f + labelHeight));
        mapBlockRt.sizeDelta = new Vector2(mapSide, mapSide);

        Image mapBorder = mapBlock.AddComponent<Image>();
        mapBorder.color = mapBackgroundColor;
        mapBorder.raycastTarget = false;

        GameObject bevelGo = new GameObject("MapInnerFrame");
        bevelGo.transform.SetParent(mapBlock.transform, false);
        RectTransform bevelRt = bevelGo.AddComponent<RectTransform>();
        StretchFull(bevelRt, 3f);
        Image bevelImg = bevelGo.AddComponent<Image>();
        bevelImg.color = new Color(panelBorderColor.r, panelBorderColor.g, panelBorderColor.b, 0.38f);
        bevelImg.raycastTarget = false;

        GameObject rawGo = new GameObject("MinimapRaw");
        rawGo.transform.SetParent(mapBlock.transform, false);
        RectTransform rawRt = rawGo.AddComponent<RectTransform>();
        StretchFull(rawRt, 7f);
        RawImage raw = rawGo.AddComponent<RawImage>();
        raw.color = mapTint;
        raw.raycastTarget = false;
        _display = raw;
        minimapDisplay = raw;

        if (showMapGrid)
        {
            GameObject gridGo = new GameObject("MapGrid");
            gridGo.transform.SetParent(mapBlock.transform, false);
            RectTransform gridRt = gridGo.AddComponent<RectTransform>();
            StretchFull(gridRt, 7f);
            Image gridImg = gridGo.AddComponent<Image>();
            _gridTexture = CreateGridTexture(gridColor, 128, 8);
            _gridSprite = SpriteFromTexture(_gridTexture);
            gridImg.sprite = _gridSprite;
            gridImg.type = Image.Type.Tiled;
            gridImg.color = Color.white;
            gridImg.raycastTarget = false;
        }

        GameObject iconsGo = new GameObject("Icons");
        iconsGo.transform.SetParent(mapBlock.transform, false);
        RectTransform iconsRt = iconsGo.AddComponent<RectTransform>();
        StretchFull(iconsRt, 7f);
        _iconsRoot = iconsRt;
        iconsRoot = iconsRt;

        GameObject vignetteGo = new GameObject("MapVignette");
        vignetteGo.transform.SetParent(mapBlock.transform, false);
        RectTransform vignetteRt = vignetteGo.AddComponent<RectTransform>();
        StretchFull(vignetteRt, 7f);
        Image vignetteImg = vignetteGo.AddComponent<Image>();
        _vignetteTexture = CreateVignetteTexture(128);
        _vignetteSprite = SpriteFromTexture(_vignetteTexture);
        vignetteImg.sprite = _vignetteSprite;
        vignetteImg.raycastTarget = false;

        if (showCompassMarks)
            BuildCompassMarks(mapBlock.transform);

        GameObject playerGo = new GameObject("PlayerMarker");
        playerGo.transform.SetParent(iconsRt, false);
        RectTransform prt = playerGo.AddComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(20f, 24f);
        prt.anchoredPosition = Vector2.zero;
        Image pi = playerGo.AddComponent<Image>();
        _playerMarkerTexture = CreateArrowTexture();
        _playerMarkerSprite = SpriteFromTexture(_playerMarkerTexture);
        pi.sprite = _playerMarkerSprite;
        pi.color = playerIconColor;
        pi.raycastTarget = false;
        pi.type = Image.Type.Simple;
        pi.preserveAspect = true;
        _playerMarkerRt = prt;
        playerMarker = prt;

        GameObject campGo = new GameObject("CampMarker");
        campGo.transform.SetParent(iconsRt, false);
        RectTransform crt = campGo.AddComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(18f, 18f);
        crt.anchoredPosition = Vector2.zero;
        Image ci = campGo.AddComponent<Image>();
        _campMarkerTexture = CreateDiamondTexture(24, campIconColor);
        _campMarkerSprite = SpriteFromTexture(_campMarkerTexture);
        ci.sprite = _campMarkerSprite;
        ci.color = campIconColor;
        ci.raycastTarget = false;
        _campMarkerRt = crt;
        campMarker = crt;

        GameObject fuelGo = new GameObject("CampfireFuel");
        fuelGo.transform.SetParent(_builtPanel.transform, false);
        RectTransform frt = fuelGo.AddComponent<RectTransform>();
        frt.anchorMin = new Vector2(0f, 0f);
        frt.anchorMax = new Vector2(1f, 0f);
        frt.pivot = new Vector2(0.5f, 0f);
        frt.anchoredPosition = new Vector2(0f, panelPadding + labelHeight + 6f);
        frt.sizeDelta = new Vector2(0f, labelHeight);
        Text ftxt = fuelGo.AddComponent<Text>();
        ftxt.font = GetDefaultUIFont();
        ftxt.text = "FIRE  --";
        ftxt.fontSize = 13;
        ftxt.alignment = TextAnchor.MiddleCenter;
        ftxt.color = labelColor;
        ftxt.horizontalOverflow = HorizontalWrapMode.Overflow;
        ftxt.verticalOverflow = VerticalWrapMode.Truncate;
        ftxt.raycastTarget = false;
        _fuelText = ftxt;
        campfireFuelLabel = ftxt;

        GameObject textGo = new GameObject("Distance");
        textGo.transform.SetParent(_builtPanel.transform, false);
        RectTransform trt = textGo.AddComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 0f);
        trt.anchorMax = new Vector2(1f, 0f);
        trt.pivot = new Vector2(0.5f, 0f);
        trt.anchoredPosition = new Vector2(0f, panelPadding);
        trt.sizeDelta = new Vector2(0f, labelHeight);
        Text txt = textGo.AddComponent<Text>();
        txt.font = GetDefaultUIFont();
        txt.text = "CAMP  --";
        txt.fontSize = 13;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = labelColor;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Truncate;
        txt.raycastTarget = false;
        _distanceText = txt;
        distanceLabel = txt;
    }

    void BuildCompassMarks(Transform mapBlock)
    {
        AddCompassLabel(mapBlock, "N", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f));
        AddCompassLabel(mapBlock, "S", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f));
        AddCompassLabel(mapBlock, "W", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f));
        AddCompassLabel(mapBlock, "E", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f));
    }

    void AddCompassLabel(Transform parent, string label, Vector2 anchor, Vector2 pivot, Vector2 offset)
    {
        GameObject go = new GameObject("Compass_" + label);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = offset;
        rt.sizeDelta = new Vector2(22f, 18f);
        Text text = go.AddComponent<Text>();
        text.font = GetDefaultUIFont();
        text.text = label;
        text.fontSize = 12;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(labelColor.r, labelColor.g, labelColor.b, 0.9f);
        text.raycastTarget = false;
    }

    static void StretchFull(RectTransform rt, float margin)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(margin, margin);
        rt.offsetMax = new Vector2(-margin, -margin);
    }

    static Texture2D CreateArrowTexture()
    {
        const int w = 24;
        const int h = 28;
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color col = Color.white;
        for (int y = 0; y < h; y++)
        {
            float ty = y / (float)(h - 1);
            float halfW = (w * 0.5f - 1f) * (1f - ty);
            for (int x = 0; x < w; x++)
            {
                float dx = Mathf.Abs(x - (w - 1) * 0.5f);
                bool inside = dx <= halfW + 0.25f;
                t.SetPixel(x, y, inside ? col : clear);
            }
        }

        t.Apply();
        t.filterMode = FilterMode.Bilinear;
        t.wrapMode = TextureWrapMode.Clamp;
        return t;
    }

    static Texture2D CreateSolidTexture(Color c, int w, int h)
    {
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] px = new Color[w * h];
        for (int i = 0; i < px.Length; i++)
            px[i] = c;
        t.SetPixels(px);
        t.Apply();
        t.filterMode = FilterMode.Point;
        t.wrapMode = TextureWrapMode.Clamp;
        return t;
    }

    static Texture2D CreateDiamondTexture(int size, Color c)
    {
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);
        float center = (size - 1) * 0.5f;
        float radius = center - 1f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Abs(x - center) + Mathf.Abs(y - center);
                t.SetPixel(x, y, d <= radius ? c : clear);
            }
        }
        t.Apply();
        t.filterMode = FilterMode.Bilinear;
        t.wrapMode = TextureWrapMode.Clamp;
        return t;
    }

    static Texture2D CreateGridTexture(Color c, int size, int spacing)
    {
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool major = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                bool grid = x % spacing == 0 || y % spacing == 0;
                Color px = major ? new Color(c.r, c.g, c.b, Mathf.Min(0.5f, c.a * 1.6f)) : (grid ? c : clear);
                t.SetPixel(x, y, px);
            }
        }
        t.Apply();
        t.filterMode = FilterMode.Point;
        t.wrapMode = TextureWrapMode.Repeat;
        return t;
    }

    static Texture2D CreateVignetteTexture(int size)
    {
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxDist = center.magnitude;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                float a = Mathf.SmoothStep(0f, 0.48f, d);
                t.SetPixel(x, y, new Color(0f, 0f, 0f, a));
            }
        }
        t.Apply();
        t.filterMode = FilterMode.Bilinear;
        t.wrapMode = TextureWrapMode.Clamp;
        return t;
    }

    static Sprite SpriteFromTexture(Texture2D tex, float pixelsPerUnit = 100f)
    {
        return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
    }

    static Canvas FindAnyCanvasInScene()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Exclude);
#else
        return Object.FindObjectOfType<Canvas>();
#endif
    }

    static Font GetDefaultUIFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null)
            f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return f;
    }

    void LateUpdate()
    {
        if (player == null || minimapCamera == null)
            return;

        Vector3 p = player.position;
        minimapCamera.transform.position = p + Vector3.up * cameraHeight;
        minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        minimapCamera.orthographicSize = mapHalfExtentWorld;

        UpdatePlayerMarker();
        UpdateCampMarker();
        UpdateDistanceLabel();
        UpdateDayNightTimer();
        UpdatePersonalRecordLabel();
    }

    void HandleDayStarted(int completedDays)
    {
        int savedBest = PlayerPrefs.GetInt(DayNightCycle.PersonalBestDaysKey, 0);
        if (completedDays >= savedBest)
        {
            PlayerPrefs.SetInt(DayNightCycle.PersonalBestDaysKey, completedDays);
            PlayerPrefs.Save();
        }

        UpdatePersonalRecordLabel();
    }

    void UpdatePlayerMarker()
    {
        if (_playerMarkerRt == null)
            return;

        _playerMarkerRt.anchoredPosition = Vector2.zero;
        float yaw = player.eulerAngles.y;
        _playerMarkerRt.localEulerAngles = new Vector3(0f, 0f, -yaw);
    }

    void UpdateCampMarker()
    {
        if (_campMarkerRt == null || _iconsRoot == null)
            return;

        if (!CampLocator.HasCamp)
        {
            _campMarkerRt.gameObject.SetActive(false);
            return;
        }

        _campMarkerRt.gameObject.SetActive(true);

        Vector3 delta = CampLocator.CampPosition - player.position;
        Vector2 half = _iconsRoot.rect.size * 0.5f * 0.88f;
        if (half.x < 4f || half.y < 4f)
            half = new Vector2(80f, 80f);

        float extent = Mathf.Max(0.01f, mapHalfExtentWorld);
        Vector2 norm = new Vector2(delta.x, delta.z) / extent;
        Vector2 local = new Vector2(norm.x * half.x, norm.y * half.y);

        local.x = Mathf.Clamp(local.x, -half.x, half.x);
        local.y = Mathf.Clamp(local.y, -half.y, half.y);

        _campMarkerRt.anchoredPosition = local;
    }

    void UpdateDistanceLabel()
    {
        if (_distanceText == null || player == null)
            return;

        if (!CampLocator.HasCamp)
        {
            _distanceText.text = "CAMP  --";
            UpdateFuelLabel();
            return;
        }

        float d = Vector3.Distance(player.position, CampLocator.CampPosition);
        _distanceText.text = d >= 1000f ? $"CAMP  {d / 1000f:F1} km" : $"CAMP  {d:F0} m";
        UpdateFuelLabel();
    }

    void UpdateFuelLabel()
    {
        if (_fuelText == null)
            return;

        if (_campfire == null)
            _campfire = FindObjectOfType<Campfire>();

        if (_campfire == null)
        {
            _fuelText.text = "FIRE  --";
            return;
        }

        float ratio = _campfire.MaxFuel > 0f ? _campfire.CurrentFuel / _campfire.MaxFuel : 0f;
        _fuelText.text = _campfire.IsLit ? $"FIRE  {Mathf.RoundToInt(ratio * 100f)}%" : "FIRE  0%";
    }
    
    void CreateDayNightTimerUI()
    {
        if (_builtPanel == null)
            return;
            
        GameObject timerGO = new GameObject("DayNightTimer");
        timerGO.transform.SetParent(_builtPanel.transform, false);
        RectTransform timerRT = timerGO.AddComponent<RectTransform>();
        timerRT.anchorMin = new Vector2(0f, 0f);
        timerRT.anchorMax = new Vector2(1f, 0f);
        timerRT.pivot = new Vector2(0.5f, 0f);
        timerRT.anchoredPosition = new Vector2(0f, panelPadding + 58f + timerOffset.y);
        timerRT.sizeDelta = new Vector2(0f, 26f);
        
        Text timerText = timerGO.AddComponent<Text>();
        timerText.font = GetDefaultUIFont();
        timerText.text = "NIGHT IN  --:--";
        timerText.fontSize = 13;
        timerText.fontStyle = FontStyle.Bold;
        timerText.alignment = TextAnchor.MiddleCenter;
        timerText.color = new Color(0.94f, 0.96f, 0.92f, 1f);
        timerText.horizontalOverflow = HorizontalWrapMode.Overflow;
        timerText.verticalOverflow = VerticalWrapMode.Truncate;
        timerText.raycastTarget = false;
        
        dayNightTimerLabel = timerText;
    }

    void CreatePersonalRecordUI()
    {
        Transform parent = _builtPanel != null ? _builtPanel.transform : null;
        if (parent == null && dayNightTimerLabel != null)
            parent = dayNightTimerLabel.transform.parent;
        if (parent == null && minimapDisplay != null)
            parent = minimapDisplay.transform.parent;
        if (parent == null)
            return;

        GameObject recordGO = new GameObject("PersonalRecordDays");
        recordGO.transform.SetParent(parent, false);
        RectTransform recordRT = recordGO.AddComponent<RectTransform>();
        recordRT.anchorMin = new Vector2(0f, 0f);
        recordRT.anchorMax = new Vector2(1f, 0f);
        recordRT.pivot = new Vector2(0.5f, 0f);
        recordRT.anchoredPosition = new Vector2(0f, panelPadding + 84f);
        recordRT.sizeDelta = new Vector2(0f, 24f);

        Text recordText = recordGO.AddComponent<Text>();
        recordText.font = GetDefaultUIFont();
        recordText.text = "PR DAY 001";
        recordText.fontSize = 13;
        recordText.fontStyle = FontStyle.Bold;
        recordText.alignment = TextAnchor.MiddleCenter;
        recordText.color = new Color(1f, 0.86f, 0.45f, 1f);
        recordText.horizontalOverflow = HorizontalWrapMode.Overflow;
        recordText.verticalOverflow = VerticalWrapMode.Truncate;
        recordText.raycastTarget = false;

        personalRecordLabel = recordText;
        _personalRecordText = recordText;
    }
    
    void UpdateDayNightTimer()
    {
        if (dayNightTimerLabel == null || _dayNightCycle == null)
            return;
            
        float remaining = _dayNightCycle.CurrentPhaseRemaining;
        string nextPhase = _dayNightCycle.IsDay ? "NIGHT" : "DAY";
        
        int minutes = Mathf.FloorToInt(remaining / 60f);
        int seconds = Mathf.FloorToInt(remaining % 60f);
        
        dayNightTimerLabel.text = $"{nextPhase} IN  {minutes:00}:{seconds:00}";
    }

    void UpdatePersonalRecordLabel()
    {
        if (_personalRecordText == null)
            _personalRecordText = personalRecordLabel;
        if (_personalRecordText == null)
            return;

        int savedBest = PlayerPrefs.GetInt(DayNightCycle.PersonalBestDaysKey, 0);
        int currentRunDays = _dayNightCycle != null ? Mathf.Max(0, _dayNightCycle.DaysElapsed) : 0;
        int displayDays = currentRunDays >= savedBest ? currentRunDays : savedBest;
        _personalRecordText.text = "PR DAY " + Mathf.Max(1, displayDays).ToString("000");
    }

    void OnDestroy()
    {
        if (_dayNightCycle != null)
            _dayNightCycle.OnDayStarted -= HandleDayStarted;

        if (_rt != null)
        {
            _rt.Release();
            Destroy(_rt);
        }

        if (_playerMarkerSprite != null)
            Destroy(_playerMarkerSprite);
        if (_campMarkerSprite != null)
            Destroy(_campMarkerSprite);
        if (_gridSprite != null)
            Destroy(_gridSprite);
        if (_vignetteSprite != null)
            Destroy(_vignetteSprite);
        if (_playerMarkerTexture != null)
            Destroy(_playerMarkerTexture);
        if (_campMarkerTexture != null)
            Destroy(_campMarkerTexture);
        if (_gridTexture != null)
            Destroy(_gridTexture);
        if (_vignetteTexture != null)
            Destroy(_vignetteTexture);

        if (_createdCameraObject != null)
            Destroy(_createdCameraObject);

        if (_builtCanvas != null)
            Destroy(_builtCanvas);
        else if (_builtPanel != null)
            Destroy(_builtPanel);
    }
}
