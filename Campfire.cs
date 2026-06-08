using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class Campfire : MonoBehaviour
{
    [Header("Fuel")]
    public float maxFuel = 100f;
    public float currentFuel = 0f;
    [Tooltip("Fire loses this percent of max fuel every second.")]
    public float fuelPercentDrainPerSecond = 0.002f; // 1% every 5 seconds
    public float woodFuelValue = 25f; // fuel added per wood

    [Header("UI")]
    public Slider fuelBar;
    [Tooltip("Optional world-space UI root for the floating campfire fuel display.")]
    public GameObject fuelBarUI;
    [Tooltip("Shows the world-space campfire UI above the fire when enabled.")]
    public bool showWorldFuelBarUI = false;

    [Header("Safe Zone")]
    public float safeRadius = 10f;
    [Tooltip("Light range for the safe zone glow.")]
    public float safeZoneLightRange = 25f;
    [Tooltip("Light intensity for the safe zone glow.")]
    public float safeZoneLightIntensity = 2f;

    [Header("Fire FX")]
    [Tooltip("Assign your fire FX prefab here. It will be enabled when lit and disabled when empty.")]
    public GameObject fireFXPrefab;

    [Header("Day/Night Audio")]
    [Tooltip("Music that plays during the day.")]
    public AudioClip dayMusic;
    [Tooltip("Music that plays during the night.")]
    public AudioClip nightMusic;
    [Tooltip("Enable looping for short music tracks.")]
    public bool loopAudio = true;

    [Header("Wood Receiver")]
    public CampfireWoodReceiver woodReceiver;

    DayNightCycle _dayNight;
    bool _isLit = false;
    SphereCollider _safeZoneCollider;
    GameObject _safeZoneIndicator;
    GameObject _fireFXInstance;
    AudioSource _audioSource;
    Material _safeZoneMaterial;

    GameObject _worldFuelBarCanvas;
    Slider _worldFuelSlider;
    Image _fuelFillImage;
    Text _fuelPercentText;
    const float FuelBarWidth = 200f;
    const float FuelBarHeight = 24f;

    public bool IsLit => _isLit;
    public float CurrentFuel => currentFuel;
    public float MaxFuel => maxFuel;

    void Start()
    {
        _dayNight = FindObjectOfType<DayNightCycle>();
        _isLit = currentFuel > 0f;
        if (fuelBar != null)
            fuelBar.maxValue = MaxFuel;

        if (woodReceiver == null)
            woodReceiver = GetComponentInChildren<CampfireWoodReceiver>();

        CampfireUI existingCampfireUI = FindObjectOfType<CampfireUI>();
        if (existingCampfireUI == null)
        {
            GameObject campfireUIObj = new GameObject("CampfireUI");
            campfireUIObj.AddComponent<CampfireUI>();
        }
        else if (!existingCampfireUI.gameObject.activeInHierarchy)
        {
            existingCampfireUI.gameObject.SetActive(true);
        }

        SetupFuelBarUI();
        UpdateUI();
        SetupSafeZone();
        SetupSafeZoneIndicator();
        SetupFireFX();
        SetupAudioSource();
        UpdateSafeZone();
    }

    void SetupSafeZone()
    {
        // Create a child object for the trigger (avoids concave collider issues)
        GameObject safeZoneTrigger = new GameObject("SafeZoneTrigger");
        safeZoneTrigger.transform.SetParent(transform, false);
        safeZoneTrigger.transform.localPosition = Vector3.zero;

        _safeZoneCollider = safeZoneTrigger.AddComponent<SphereCollider>();
        _safeZoneCollider.radius = safeRadius;
        _safeZoneCollider.isTrigger = true;
    }

    void SetupSafeZoneIndicator()
    {
        if (_safeZoneIndicator != null)
            return;

        GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        indicator.name = "SafeZoneIndicator";
        Destroy(indicator.GetComponent<Collider>());
        indicator.transform.SetParent(transform, false);
        indicator.transform.localPosition = new Vector3(0, 0.01f, 0); // Just above ground to avoid clipping
        float diameter = safeRadius * 2f;
        indicator.transform.localScale = new Vector3(diameter, 0.01f, diameter);

        Renderer rend = indicator.GetComponent<Renderer>();
        if (rend != null)
        {
            Shader shader = Shader.Find("Standard");
            Material mat = new Material(shader);
            // Outline only: transparent with just edge visibility
            mat.color = new Color(1f, 0.5f, 0.0f, 0.15f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Glossiness", 0f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = 3000;
            rend.sharedMaterial = mat;
            _safeZoneMaterial = mat;
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }

        _safeZoneIndicator = indicator;
        _safeZoneIndicator.SetActive(false); // Start hidden
    }

    void SetupFireFX()
    {
        if (fireFXPrefab == null)
            return;

        _fireFXInstance = Instantiate(fireFXPrefab, transform);
        _fireFXInstance.name = "FireFX";
        _fireFXInstance.transform.localPosition = new Vector3(0f, 0.5f, 0f); // Lower position
        _fireFXInstance.transform.localRotation = Quaternion.identity;
        
        // Destroy any lights on the fire FX to remove light bulb gizmos
        Light[] lights = _fireFXInstance.GetComponentsInChildren<Light>();
        foreach (Light light in lights)
        {
            Destroy(light);
        }
        
        _fireFXInstance.SetActive(false); // Start disabled
    }

    void SetupAudioSource()
    {
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 0f; // 2D audio
        _audioSource.loop = loopAudio;
    }





    void SetupFuelBarUI()
    {
        if (fuelBar != null)
            return;

        if (!showWorldFuelBarUI)
            return;

        if (fuelBarUI == null)
            return;

        if (_worldFuelBarCanvas != null)
            return;

        if (!fuelBarUI.scene.IsValid())
        {
            Debug.Log("Campfire: fuelBarUI is a prefab asset; instantiating a scene copy.");
            _worldFuelBarCanvas = Instantiate(fuelBarUI);
        }
        else
        {
            _worldFuelBarCanvas = fuelBarUI;
        }

        _worldFuelBarCanvas.transform.SetParent(transform, false);

        Canvas worldCanvas = _worldFuelBarCanvas.GetComponent<Canvas>();
        if (worldCanvas == null)
            worldCanvas = _worldFuelBarCanvas.AddComponent<Canvas>();

        worldCanvas.renderMode = RenderMode.WorldSpace;
        if (Camera.main != null)
            worldCanvas.worldCamera = Camera.main;

        if (_worldFuelBarCanvas.GetComponent<CanvasScaler>() == null)
        {
            CanvasScaler worldScaler = _worldFuelBarCanvas.AddComponent<CanvasScaler>();
            worldScaler.dynamicPixelsPerUnit = 10f;
        }

        _worldFuelBarCanvas.transform.localPosition = _worldFuelBarCanvas.transform.localPosition == Vector3.zero
            ? new Vector3(0f, 2.0f, 0f)
            : _worldFuelBarCanvas.transform.localPosition;
        _worldFuelBarCanvas.transform.localRotation = Quaternion.identity;
        _worldFuelBarCanvas.transform.localScale = Vector3.one * 0.02f;

        _worldFuelSlider = _worldFuelBarCanvas.GetComponentInChildren<Slider>();
        _fuelFillImage = FindFuelFillImage(_worldFuelBarCanvas);
        _fuelPercentText = FindFuelText(_worldFuelBarCanvas);
    }

    Image FindFuelFillImage(GameObject root)
    {
        if (root == null)
            return null;

        Image[] images = root.GetComponentsInChildren<Image>(true);
        foreach (Image image in images)
        {
            if (image.gameObject.name.ToLower().Contains("fuelfill") || image.gameObject.name.ToLower().Contains("fill"))
                return image;
        }

        return null;
    }

    Text FindFuelText(GameObject root)
    {
        if (root == null)
            return null;

        Text[] texts = root.GetComponentsInChildren<Text>(true);
        foreach (Text text in texts)
        {
            if (text.gameObject.name.ToLower().Contains("fuel") || text.gameObject.name.ToLower().Contains("percent"))
                return text;
        }

        return texts.Length > 0 ? texts[0] : null;
    }

    void LateUpdate()
    {
        if (_worldFuelBarCanvas == null)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        _worldFuelBarCanvas.transform.rotation = Quaternion.LookRotation(_worldFuelBarCanvas.transform.position - cam.transform.position);
    }

    void Update()
    {
        // Drain fuel whenever the fire is lit
        if (IsLit)
        {
            float drainAmount = Mathf.Max(0f, maxFuel) * fuelPercentDrainPerSecond * Time.deltaTime;
            currentFuel -= drainAmount;
            currentFuel = Mathf.Max(0f, currentFuel);
            if (currentFuel <= 0f)
                _isLit = false;
            UpdateUI();
        }

        // Always update safe zone visibility (day/night changes or fire status)
        UpdateSafeZone();
    }

    public void AddWood(int amount)
    {
        if (amount <= 0)
            return;

        currentFuel += amount * woodFuelValue;
        currentFuel = Mathf.Min(maxFuel, currentFuel);
        _isLit = currentFuel > 0f;

        UpdateUI();
        UpdateSafeZone();
    }

    void UpdateUI()
    {
        float ratio = MaxFuel > 0f ? CurrentFuel / MaxFuel : 0f;
        ratio = Mathf.Clamp01(ratio);

        if (fuelBar != null)
        {
            fuelBar.maxValue = MaxFuel;
            fuelBar.value = CurrentFuel;
        }

        if (_worldFuelSlider != null)
        {
            _worldFuelSlider.maxValue = MaxFuel;
            _worldFuelSlider.value = CurrentFuel;
        }

        if (_fuelFillImage != null)
        {
            float fillWidth = (FuelBarWidth - 2f) * ratio;
            RectTransform fillRect = _fuelFillImage.rectTransform;
            fillRect.sizeDelta = new Vector2(fillWidth, FuelBarHeight - 2f);
        }

        if (_fuelPercentText != null)
        {
            _fuelPercentText.text = Mathf.RoundToInt(ratio * 100f) + "%";
        }
        UpdateFireFX();
    }

    void UpdateSafeZone()
    {
        if (_dayNight == null)
            return;

        bool active = IsLit && !_dayNight.IsDay;

        if (_safeZoneIndicator != null)
            _safeZoneIndicator.SetActive(active);

        if (_safeZoneCollider != null)
            _safeZoneCollider.enabled = active;

        UpdateFireFX();
    }

    void UpdateFireFX()
    {
        if (_fireFXInstance != null)
            _fireFXInstance.SetActive(IsLit);
    }

    public bool IsSafeZoneActive => IsLit && _dayNight != null && !_dayNight.IsDay;

    public void OnDayNightChanged(bool isDay)
    {
        UpdateSafeZone();
        UpdateDayNightAudio(isDay);
    }

    void UpdateDayNightAudio(bool isDay)
    {
        if (_audioSource == null)
            return;

        _audioSource.Stop();

        AudioClip clipToPlay = isDay ? dayMusic : nightMusic;
        if (clipToPlay == null)
            return;

        float cycleDuration = isDay ? _dayNight.dayDuration : _dayNight.nightDuration;
        float trackDuration = clipToPlay.length;
        
        // Loop if track is shorter than cycle duration, otherwise play once
        bool shouldLoop = loopAudio || (trackDuration < cycleDuration);
        _audioSource.loop = shouldLoop;
        
        _audioSource.clip = clipToPlay;
        _audioSource.Play();
    }

    void OnDestroy()
    {
        if (_safeZoneMaterial != null)
            Destroy(_safeZoneMaterial);
    }
}
 
