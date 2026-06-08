using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingScreenUI : MonoBehaviour
{
    public GameObject greenLoadingBarPrefab;
    public float minVisibleTime = 0.25f;
    public float hideDelayAfterLoaded = 0.05f;

    Canvas _canvas;
    GameObject _panel;
    RectTransform[] _segmentGreenRects;
    RectTransform[] _segmentRedRects;
    Sprite _solidGreenSprite;
    Sprite _solidRedSprite;
    Text _text;
    InfiniteTerrainStreamer _terrainStreamer;
    InfiniteForestWorld _forestWorld;
    float _shownAt;
    float _loadedAt = -1f;
    float _displayedProgress;
    bool _isVisible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureOnSceneLoad()
    {
        if (FindObjectOfType<LoadingScreenUI>() != null)
            return;

        GameObject go = new GameObject("LoadingScreenUI");
        DontDestroyOnLoad(go);
        go.AddComponent<LoadingScreenUI>();
    }

    void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        BuildUI();
        BindWorldLoaders();
        Show();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindWorldLoaders();
        Show();
    }

    void Update()
    {
        BindWorldLoaders();

        float targetProgress = CurrentProgress01();
        _displayedProgress = Mathf.MoveTowards(_displayedProgress, targetProgress, Time.unscaledDeltaTime * 2.4f);
        UpdateSegmentBars(_displayedProgress);
        if (_text != null)
            _text.text = $"LOADING {Mathf.RoundToInt(_displayedProgress * 100f)}%";

        bool loading = IsWorldLoading();
        if (loading)
        {
            _loadedAt = -1f;
            if (!_isVisible)
                Show();
            return;
        }

        if (_loadedAt < 0f)
            _loadedAt = Time.unscaledTime;

        bool stayedLongEnough = Time.unscaledTime - _shownAt >= minVisibleTime;
        bool finishedDelay = Time.unscaledTime - _loadedAt >= hideDelayAfterLoaded;
        if (_isVisible && stayedLongEnough && finishedDelay)
            Hide();
    }

    void BindWorldLoaders()
    {
        if (_terrainStreamer == null)
            _terrainStreamer = FindObjectOfType<InfiniteTerrainStreamer>();
        if (_forestWorld == null)
            _forestWorld = FindObjectOfType<InfiniteForestWorld>();
    }

    bool IsWorldLoading()
    {
        bool terrainLoading = _terrainStreamer != null && _terrainStreamer.IsLoading;
        bool forestLoading = _forestWorld != null && _forestWorld.IsLoading;
        return terrainLoading || forestLoading;
    }

    float CurrentProgress01()
    {
        float total = 0f;
        int count = 0;

        if (_terrainStreamer != null)
        {
            total += _terrainStreamer.LoadingProgress01;
            count++;
        }

        if (_forestWorld != null)
        {
            total += _forestWorld.LoadingProgress01;
            count++;
        }

        if (count == 0)
            return 1f;

        return Mathf.Clamp01(total / count);
    }

    void Show()
    {
        _shownAt = Time.unscaledTime;
        _loadedAt = -1f;
        if (!_isVisible)
            _displayedProgress = 0f;
        _isVisible = true;
        if (_panel != null)
            _panel.SetActive(true);
    }

    void Hide()
    {
        _isVisible = false;
        if (_panel != null)
            _panel.SetActive(false);
    }

    void BuildUI()
    {
        GameObject canvasGo = new GameObject("LoadingScreenCanvas");
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 5000;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        _panel = new GameObject("LoadingPanel");
        _panel.transform.SetParent(canvasGo.transform, false);
        RectTransform panelRt = _panel.AddComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;

        Image gray = _panel.AddComponent<Image>();
        gray.color = new Color(0.28f, 0.28f, 0.28f, 0.96f);
        gray.raycastTarget = false;

        BuildSegmentedLoadingBar();

        GameObject textGo = new GameObject("LoadingText");
        textGo.transform.SetParent(_panel.transform, false);
        RectTransform textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0.5f, 0.42f);
        textRt.anchorMax = new Vector2(0.5f, 0.42f);
        textRt.pivot = new Vector2(0.5f, 0.5f);
        textRt.anchoredPosition = new Vector2(0f, 66f);
        textRt.sizeDelta = new Vector2(760f, 48f);

        _text = textGo.AddComponent<Text>();
        _text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        _text.text = "LOADING 0%";
        _text.fontSize = 28;
        _text.alignment = TextAnchor.MiddleCenter;
        _text.color = Color.white;
        _text.raycastTarget = false;
    }

    void BuildSegmentedLoadingBar()
    {
        GameObject barRoot = new GameObject("EightGreenLoadingBars");
        barRoot.transform.SetParent(_panel.transform, false);
        RectTransform rootRt = barRoot.AddComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.5f, 0.42f);
        rootRt.anchorMax = new Vector2(0.5f, 0.42f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.sizeDelta = new Vector2(800f, 46f);

        HorizontalLayoutGroup layout = barRoot.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        _segmentGreenRects = new RectTransform[8];
        _segmentRedRects = new RectTransform[8];
        for (int i = 0; i < _segmentGreenRects.Length; i++)
            CreateLoadingSegment(barRoot.transform, i, out _segmentGreenRects[i], out _segmentRedRects[i]);
    }

    void CreateLoadingSegment(Transform parent, int index, out RectTransform greenRect, out RectTransform redRect)
    {
        GameObject shell = new GameObject($"LoadingSegment_{index + 1}");
        shell.transform.SetParent(parent, false);
        RectTransform shellRt = shell.AddComponent<RectTransform>();
        shellRt.sizeDelta = new Vector2(92f, 42f);

        EnsureSolidSprites();

        Image shellImage = shell.AddComponent<Image>();
        shellImage.sprite = _solidRedSprite;
        shellImage.material = null;
        shellImage.type = Image.Type.Simple;
        shellImage.color = new Color(0.95f, 0.12f, 0.08f, 1f);
        shellImage.raycastTarget = false;

        GameObject redGo = new GameObject("RedRemainingFill");
        redGo.transform.SetParent(shell.transform, false);
        redRect = redGo.AddComponent<RectTransform>();
        redRect.anchorMin = Vector2.zero;
        redRect.anchorMax = Vector2.one;
        redRect.offsetMin = new Vector2(4f, 4f);
        redRect.offsetMax = new Vector2(-4f, -4f);

        Image redFill = redGo.AddComponent<Image>();
        redFill.sprite = _solidRedSprite;
        redFill.material = null;
        redFill.type = Image.Type.Simple;
        redFill.color = new Color(0.95f, 0.12f, 0.08f, 1f);
        redFill.raycastTarget = false;

        GameObject fillGo = new GameObject("GreenLoadedFill");
        fillGo.name = "GreenLoadedFill";
        fillGo.transform.SetParent(shell.transform, false);

        greenRect = fillGo.GetComponent<RectTransform>();
        if (greenRect == null)
            greenRect = fillGo.AddComponent<RectTransform>();
        greenRect.anchorMin = Vector2.zero;
        greenRect.anchorMax = Vector2.one;
        greenRect.offsetMin = new Vector2(4f, 4f);
        greenRect.offsetMax = new Vector2(-4f, -4f);

        Image greenFill = fillGo.GetComponent<Image>();
        if (greenFill == null)
            greenFill = fillGo.AddComponent<Image>();

        greenFill.sprite = _solidGreenSprite;
        greenFill.material = null;
        greenFill.type = Image.Type.Simple;
        greenFill.color = new Color(0.1f, 0.95f, 0.22f, 1f);
        greenFill.raycastTarget = false;
    }

    void EnsureSolidSprites()
    {
        if (_solidGreenSprite == null)
            _solidGreenSprite = CreateSolidSprite("LoadingGreenSprite", new Color(0.1f, 0.95f, 0.22f, 1f));
        if (_solidRedSprite == null)
            _solidRedSprite = CreateSolidSprite("LoadingRedSprite", new Color(0.95f, 0.12f, 0.08f, 1f));
    }

    Sprite CreateSolidSprite(string spriteName, Color color)
    {
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.name = spriteName + "Texture";
        texture.SetPixels(new Color[] { color, color, color, color });
        texture.Apply();
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Point;

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 2f);
        sprite.name = spriteName;
        return sprite;
    }

    void UpdateSegmentBars(float progress)
    {
        if (_segmentGreenRects == null || _segmentGreenRects.Length == 0)
            return;

        progress = Mathf.Clamp01(progress);
        float scaled = progress * _segmentGreenRects.Length;
        for (int i = 0; i < _segmentGreenRects.Length; i++)
        {
            float greenAmount = Mathf.Clamp01(scaled - i);

            RectTransform green = _segmentGreenRects[i];
            if (green != null)
            {
                green.anchorMin = new Vector2(0f, 0f);
                green.anchorMax = new Vector2(greenAmount, 1f);
                green.offsetMin = new Vector2(4f, 4f);
                green.offsetMax = new Vector2(-4f, -4f);
            }

            RectTransform red = _segmentRedRects != null && i < _segmentRedRects.Length ? _segmentRedRects[i] : null;
            if (red != null)
            {
                red.anchorMin = new Vector2(greenAmount, 0f);
                red.anchorMax = new Vector2(1f, 1f);
                red.offsetMin = new Vector2(4f, 4f);
                red.offsetMax = new Vector2(-4f, -4f);
            }
        }
    }
}
