using System;
using UnityEngine;

public class DayNightCycle : MonoBehaviour
{
    public const string PersonalBestDaysKey = "personal_best_days";
    public const string ClassGemBalanceKey = "class_gems";
    public const int GemRewardMinimumDays = 50;

    [Header("Timing")]
    public Light sun;
    public float dayDuration = 150f; // 2 minutes 30 seconds
    public float nightDuration = 90f; // 1 minute 30 seconds

    [Header("Sky Animation")]
    public float daySunIntensity = 1.15f;
    public float nightSunIntensity = 0.08f;
    public Color dayAmbientSky = new Color(0.48f, 0.56f, 0.66f, 1f);
    public Color dayAmbientEquator = new Color(0.32f, 0.36f, 0.34f, 1f);
    public Color dayAmbientGround = new Color(0.16f, 0.14f, 0.10f, 1f);
    public Color nightAmbientSky = new Color(0.035f, 0.045f, 0.075f, 1f);
    public Color nightAmbientEquator = new Color(0.025f, 0.03f, 0.045f, 1f);
    public Color nightAmbientGround = new Color(0.012f, 0.012f, 0.018f, 1f);
    public Color dayFogColor = new Color(0.62f, 0.70f, 0.76f, 1f);
    public Color nightFogColor = new Color(0.02f, 0.025f, 0.04f, 1f);

    float timer = 0f;
    bool _isDay = true;
    int _daysElapsed = 0;

    public bool IsDay => _isDay;
    public int DaysElapsed => _daysElapsed;
    public float CurrentPhaseElapsed => Mathf.Clamp(timer, 0f, CurrentPhaseDuration);
    public float CurrentPhaseDuration => Mathf.Max(0.01f, _isDay ? dayDuration : nightDuration);
    public float CurrentPhaseRemaining => Mathf.Max(0f, CurrentPhaseDuration - timer);
    public event Action<int> OnDayStarted;

    void Awake()
    {
        timer = 0f;
        _isDay = true;
        _daysElapsed = 0;
    }

    void Start()
    {
        ApplySkyState();
        OnDayNightChanged();
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (_isDay)
        {
            float safeDayDuration = Mathf.Max(0.01f, dayDuration);
            ApplySkyState();

            if (timer >= safeDayDuration)
            {
                timer = 0f;
                _isDay = false;
                ApplySkyState();
                OnDayNightChanged();
            }
        }
        else
        {
            float safeNightDuration = Mathf.Max(0.01f, nightDuration);
            ApplySkyState();

            if (timer >= safeNightDuration)
            {
                timer = 0f;
                _isDay = true;
                _daysElapsed++;
                SavePersonalBestDays();
                ApplySkyState();
                OnDayNightChanged();
                OnDayStarted?.Invoke(_daysElapsed);
            }
        }
    }

    void SavePersonalBestDays()
    {
        int previousBest = PlayerPrefs.GetInt(PersonalBestDaysKey, 0);
        if (_daysElapsed <= previousBest)
            return;

        if (_daysElapsed >= GemRewardMinimumDays)
            PlayerPrefs.SetInt(ClassGemBalanceKey, PlayerPrefs.GetInt(ClassGemBalanceKey, 0) + 1);

        PlayerPrefs.SetInt(PersonalBestDaysKey, _daysElapsed);
        PlayerPrefs.Save();
    }

    void ApplySkyState()
    {
        float progress = CurrentPhaseDuration > 0f ? Mathf.Clamp01(timer / CurrentPhaseDuration) : 0f;
        float skyBlend = _isDay ? DayBrightness(progress) : NightBrightness(progress);

        if (sun != null)
        {
            float sunPitch = _isDay
                ? Mathf.Lerp(78f, 18f, progress)
                : Mathf.Lerp(-18f, -78f, progress);
            sun.transform.rotation = Quaternion.Euler(sunPitch, 30f, 0f);
            sun.intensity = Mathf.Lerp(nightSunIntensity, daySunIntensity, skyBlend);
            sun.color = Color.Lerp(new Color(0.42f, 0.52f, 0.82f, 1f), new Color(1f, 0.92f, 0.76f, 1f), skyBlend);
        }

        RenderSettings.ambientSkyColor = Color.Lerp(nightAmbientSky, dayAmbientSky, skyBlend);
        RenderSettings.ambientEquatorColor = Color.Lerp(nightAmbientEquator, dayAmbientEquator, skyBlend);
        RenderSettings.ambientGroundColor = Color.Lerp(nightAmbientGround, dayAmbientGround, skyBlend);
        RenderSettings.fogColor = Color.Lerp(nightFogColor, dayFogColor, skyBlend);
        RenderSettings.ambientIntensity = Mathf.Lerp(0.35f, 1f, skyBlend);
    }

    float DayBrightness(float progress)
    {
        return 1f;
    }

    float NightBrightness(float progress)
    {
        return 0f;
    }

    void OnDayNightChanged()
    {
        // Notify other systems
        Campfire[] campfires = FindObjectsOfType<Campfire>();
        foreach (Campfire c in campfires)
            c.OnDayNightChanged(_isDay);

        // MonsterAI is managed by MonsterManager, so no direct notification needed.
    }
}
