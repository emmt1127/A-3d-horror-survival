using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Creates a dedicated top-center HUD panel that shows elapsed days.
/// Plays an animation when a new day starts (night has ended).
/// Attach to any active object in the gameplay scene (e.g., UIManager).
/// </summary>
public class DaysPassedAutoUI : MonoBehaviour
{
    [Header("Layout")]
    public Vector2 panelSize = new Vector2(380f, 82f);
    public Vector2 topOffset = new Vector2(0f, -28f);
    public int canvasSortOrder = 700;
    public Color panelColor = new Color(0.015f, 0.018f, 0.02f, 0.72f);
    public Color borderColor = new Color(0.95f, 0.74f, 0.28f, 0.95f);
    public Color textColor = new Color(1f, 0.94f, 0.76f, 1f);
    public Color accentColor = new Color(1f, 0.56f, 0.12f, 1f);
    public Color flashColor = new Color(1f, 0.84f, 0.32f, 0.82f);

    [Header("Animation")]
    public float dayAnnouncementDelay = 2f;
    public float introPulseDuration = 0.38f;
    public float numberRevealDuration = 0.72f;
    public float holdAfterPulseDuration = 0.22f;
    public float settleScale = 1f;
    public float pulseScale = 1.55f;
    public Vector2 announcementOffset = new Vector2(0f, -180f);

    DayNightCycle _dayNight;
    Canvas _canvas;
    RectTransform _panel;
    TMP_Text _counterText;
    Image _panelImage;
    Image _borderImage;
    Image _coreImage;
    Image _leftAccent;
    Image _rightAccent;
    Image _flashImage;
    Coroutine _animRoutine;
    int _displayedDays;
    bool _subscribed;
    int _lastKnownDays = -1;
    int _lastAnimatedElapsedDays = -1;

    void Awake()
    {
        BuildDedicatedUI();
        SetCounterText(0);
        SetPanelVisible(false);
    }

    void OnEnable()
    {
        StartCoroutine(BindWhenReady());
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    IEnumerator BindWhenReady()
    {
        Unsubscribe();

        while (_dayNight == null)
        {
            _dayNight = FindObjectOfType<DayNightCycle>();
            if (_dayNight == null)
                yield return new WaitForSeconds(0.25f);
        }

        _dayNight.OnDayStarted -= HandleDayStarted;
        _dayNight.OnDayStarted += HandleDayStarted;
        _subscribed = true;

        _displayedDays = Mathf.Max(0, _dayNight.DaysElapsed);
        _lastKnownDays = _displayedDays;
        SetCounterText(CurrentDayNumber(_displayedDays));
        ApplyBaseStyle(1f);
        SetPanelVisible(false);

        if (_dayNight.IsDay)
        {
            StartDayAnimation(_displayedDays);
        }
    }

    void Unsubscribe()
    {
        if (_dayNight != null && _subscribed)
            _dayNight.OnDayStarted -= HandleDayStarted;

        _subscribed = false;
        _dayNight = null;
    }

    void HandleDayStarted(int completedDays)
    {
        if (_counterText == null || _panel == null)
            return;

        int elapsedDays = Mathf.Max(0, completedDays);
        if (elapsedDays <= _lastAnimatedElapsedDays)
            return;

        StartDayAnimation(elapsedDays);
    }

    void Update()
    {
        if (_dayNight == null || _counterText == null)
            return;

        // Safety net in case events are missed during scene/hot reload edge cases.
        int days = Mathf.Max(0, _dayNight.DaysElapsed);
        if (days > _lastKnownDays && days > _lastAnimatedElapsedDays)
            StartDayAnimation(days);
    }

    void StartDayAnimation(int elapsedDays)
    {
        if (_animRoutine != null)
            StopCoroutine(_animRoutine);

        _lastAnimatedElapsedDays = Mathf.Max(0, elapsedDays);
        _animRoutine = StartCoroutine(PlayDayAnnouncement(_lastAnimatedElapsedDays));
    }

    IEnumerator PlayDayAnnouncement(int elapsedDays)
    {
        if (dayAnnouncementDelay > 0f)
            yield return new WaitForSecondsRealtime(dayAnnouncementDelay);

        _lastKnownDays = Mathf.Max(0, elapsedDays);
        _displayedDays = CurrentDayNumber(_lastKnownDays);
        SetCounterText(_displayedDays);
        SetPanelVisible(true);
        _panel.localScale = Vector3.one * pulseScale;
        _panel.anchoredPosition = topOffset + announcementOffset;
        SetTextStyle(flashColor, 1.45f);
        SetAccentAlpha(1f);
        SetFlashAlpha(0.72f);

        float t = 0f;
        while (t < introPulseDuration)
        {
            t += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(t / introPulseDuration);
            float eased = EaseOutCubic(n);
            float scale = Mathf.Lerp(pulseScale * 0.88f, pulseScale, eased);
            float flash = Mathf.Lerp(0.72f, 0.18f, eased);
            float accent = Mathf.Lerp(1f, 0.82f, eased);
            _panel.localScale = Vector3.one * scale;
            _panel.anchoredPosition = topOffset + announcementOffset;
            SetFlashAlpha(flash);
            SetAccentAlpha(accent);
            yield return null;
        }

        _panel.localScale = Vector3.one * pulseScale;
        if (holdAfterPulseDuration > 0f)
            yield return new WaitForSecondsRealtime(holdAfterPulseDuration);

        t = 0f;
        while (t < numberRevealDuration)
        {
            t += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(t / numberRevealDuration);
            float eased = EaseInOutSine(n);
            float shimmer = Mathf.Sin(n * Mathf.PI * 5f) * 0.5f + 0.5f;
            float scale = Mathf.Lerp(pulseScale, settleScale, eased);
            _panel.localScale = Vector3.one * scale;
            _panel.anchoredPosition = Vector2.Lerp(topOffset + announcementOffset, topOffset, eased);
            SetTextStyle(Color.Lerp(accentColor, textColor, eased), Mathf.Lerp(1.45f, 1f, eased));
            SetAccentAlpha(Mathf.Lerp(0.82f, 0.38f + shimmer * 0.18f, eased));
            SetFlashAlpha(Mathf.Lerp(0.18f, 0f, eased));
            yield return null;
        }

        ApplyBaseStyle(1f);
        _panel.localScale = Vector3.one * settleScale;
        _panel.anchoredPosition = topOffset;
        SetAccentAlpha(0.36f);
        SetFlashAlpha(0f);
        _animRoutine = null;
    }

    float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    float EaseInOutSine(float t)
    {
        t = Mathf.Clamp01(t);
        return -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;
    }

    void BuildDedicatedUI()
    {
        GameObject canvasGo = new GameObject("DaysPassedCanvas");
        canvasGo.transform.SetParent(transform, false);

        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.overrideSorting = true;
        _canvas.sortingOrder = canvasSortOrder;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        GameObject panelGo = new GameObject("DaysPassedPanel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        _panel = panelGo.AddComponent<RectTransform>();
        _panel.anchorMin = new Vector2(0.5f, 1f);
        _panel.anchorMax = new Vector2(0.5f, 1f);
        _panel.pivot = new Vector2(0.5f, 1f);
        _panel.anchoredPosition = topOffset;
        _panel.sizeDelta = panelSize;
        _panel.localScale = Vector3.one * settleScale;

        _panelImage = panelGo.AddComponent<Image>();
        _panelImage.color = panelColor;
        _panelImage.raycastTarget = false;

        GameObject flashGo = new GameObject("Flash");
        flashGo.transform.SetParent(panelGo.transform, false);
        RectTransform flashRt = flashGo.AddComponent<RectTransform>();
        flashRt.anchorMin = Vector2.zero;
        flashRt.anchorMax = Vector2.one;
        flashRt.offsetMin = Vector2.zero;
        flashRt.offsetMax = Vector2.zero;
        _flashImage = flashGo.AddComponent<Image>();
        _flashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
        _flashImage.raycastTarget = false;

        GameObject borderGo = new GameObject("Border");
        borderGo.transform.SetParent(panelGo.transform, false);
        RectTransform borderRt = borderGo.AddComponent<RectTransform>();
        borderRt.anchorMin = Vector2.zero;
        borderRt.anchorMax = Vector2.one;
        borderRt.offsetMin = new Vector2(2f, 2f);
        borderRt.offsetMax = new Vector2(-2f, -2f);
        _borderImage = borderGo.AddComponent<Image>();
        _borderImage.color = borderColor;
        _borderImage.raycastTarget = false;

        GameObject coreGo = new GameObject("Core");
        coreGo.transform.SetParent(panelGo.transform, false);
        RectTransform coreRt = coreGo.AddComponent<RectTransform>();
        coreRt.anchorMin = Vector2.zero;
        coreRt.anchorMax = Vector2.one;
        coreRt.offsetMin = new Vector2(4f, 4f);
        coreRt.offsetMax = new Vector2(-4f, -4f);
        _coreImage = coreGo.AddComponent<Image>();
        _coreImage.color = panelColor;
        _coreImage.raycastTarget = false;

        _leftAccent = CreateAccentBar(coreGo, "LeftAccent", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 54f), new Vector2(12f, 0f));
        _rightAccent = CreateAccentBar(coreGo, "RightAccent", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(8f, 54f), new Vector2(-12f, 0f));

        GameObject textGo = new GameObject("DaysPassedText");
        textGo.transform.SetParent(coreGo.transform, false);
        RectTransform textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(12f, 6f);
        textRt.offsetMax = new Vector2(-12f, -6f);

        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 48f;
        tmp.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.color = textColor;
        tmp.raycastTarget = false;
        tmp.characterSpacing = 8f;
        tmp.fontWeight = FontWeight.Black;
        tmp.outlineWidth = 0.24f;
        tmp.outlineColor = new Color(0f, 0f, 0f, 0.95f);
        tmp.enableVertexGradient = true;
        tmp.colorGradient = new VertexGradient(textColor, textColor, accentColor, accentColor);
        _counterText = tmp;
        SetAccentAlpha(0.36f);
        SetFlashAlpha(0f);
    }

    Image CreateAccentBar(GameObject parent, string name, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 position)
    {
        GameObject barGo = new GameObject(name);
        barGo.transform.SetParent(parent.transform, false);
        RectTransform barRt = barGo.AddComponent<RectTransform>();
        barRt.anchorMin = anchor;
        barRt.anchorMax = anchor;
        barRt.pivot = pivot;
        barRt.anchoredPosition = position;
        barRt.sizeDelta = size;

        Image image = barGo.AddComponent<Image>();
        image.color = accentColor;
        image.raycastTarget = false;
        return image;
    }

    void SetCounterText(int daysPassed)
    {
        if (_counterText == null)
            return;

        _counterText.text = "DAY " + Mathf.Max(1, daysPassed).ToString("000");
    }

    int CurrentDayNumber(int elapsedDays)
    {
        return Mathf.Max(1, elapsedDays + 1);
    }

    void ApplyBaseStyle(float outlineMultiplier)
    {
        SetTextStyle(textColor, outlineMultiplier);
    }

    void SetTextStyle(Color color, float outlineMultiplier)
    {
        if (_counterText != null)
        {
            _counterText.color = color;
            _counterText.outlineWidth = 0.2f * Mathf.Max(0.6f, outlineMultiplier);
            _counterText.colorGradient = new VertexGradient(Color.Lerp(color, Color.white, 0.08f), color, accentColor, Color.Lerp(accentColor, color, 0.35f));
        }
    }

    void SetAccentAlpha(float alpha)
    {
        alpha = Mathf.Clamp01(alpha);
        SetImageAlpha(_leftAccent, alpha);
        SetImageAlpha(_rightAccent, alpha);
        SetImageAlpha(_borderImage, Mathf.Lerp(0.46f, 1f, alpha));
    }

    void SetFlashAlpha(float alpha)
    {
        SetImageAlpha(_flashImage, Mathf.Clamp01(alpha));
    }

    void SetImageAlpha(Image image, float alpha)
    {
        if (image == null)
            return;

        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }

    void SetPanelVisible(bool visible)
    {
        if (_panel != null)
            _panel.gameObject.SetActive(visible);
    }
}
