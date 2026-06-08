using UnityEngine;

[RequireComponent(typeof(PlayerInventory))]
public class PlayerFlashlightController : MonoBehaviour
{
    [Header("Inventory")]
    public KeyCode toggleKey = KeyCode.R;
    public GameObject flashlightPrefab;
    public bool addFlashlightOnStart = true;

    [Header("Flashlight Settings")]
    [Tooltip("How far the flashlight lights the world.")]
    public float flashlightRange = 28f;
    [Tooltip("How bright the flashlight beam is.")]
    public float flashlightIntensity = 4.5f;
    public float flashlightSpotAngle = 58f;

    [Header("Campfire Brightness")]
    [Tooltip("Extra light around the player when standing in a lit campfire zone.")]
    public float campfireZoneBrightness = 2.8f;
    [Tooltip("How far the extra campfire visibility reaches around the player.")]
    public float campfireZoneLightRange = 13f;
    public Color campfireZoneLightColor = new Color(1f, 0.66f, 0.34f, 1f);

    PlayerInventory _inventory;
    FlashlightItem _flashlight;
    Light _campfireVisibilityLight;

    void Awake()
    {
        _inventory = GetComponent<PlayerInventory>();
    }

    void Start()
    {
        if (addFlashlightOnStart)
            EnsureFlashlightInInventory();

        EnsureCampfireVisibilityLight();
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey) && !IsGunEquipped())
            ToggleFlashlight();

        UpdateCampfireVisibility();
    }

    bool IsGunEquipped()
    {
        if (_inventory == null)
            _inventory = GetComponent<PlayerInventory>();

        Weapon active = _inventory != null ? _inventory.GetActiveWeapon() : null;
        return active != null && active.GetComponent<GunWeapon>() != null;
    }

    void ToggleFlashlight()
    {
        EnsureFlashlightInInventory();
        if (_flashlight == null || _inventory == null)
            return;

        int slot = FindFlashlightSlot();
        if (slot < 0)
            return;

        if (_inventory.activeIndex != slot)
        {
            _inventory.SetActiveSlot(slot);
            _flashlight.SetOn(true);
        }
        else
        {
            _flashlight.Toggle();
        }
    }

    void EnsureFlashlightInInventory()
    {
        if (_inventory == null)
            _inventory = GetComponent<PlayerInventory>();

        if (_inventory == null)
            return;

        int existingSlot = FindFlashlightSlot();
        if (existingSlot >= 0)
        {
            _flashlight = _inventory.weapons[existingSlot].GetComponent<FlashlightItem>();
            ApplyFlashlightInspectorSettings();
            return;
        }

        GameObject go = flashlightPrefab != null
            ? Instantiate(flashlightPrefab, transform)
            : FlashlightItem.CreateRuntimeObject();

        go.name = "Flashlight";
        FlashlightItem item = go.GetComponent<FlashlightItem>();
        if (item == null)
            item = go.AddComponent<FlashlightItem>();

        Weapon weapon = go.GetComponent<Weapon>();
        if (weapon == null)
            weapon = go.AddComponent<Weapon>();

        _flashlight = item;
        ApplyFlashlightInspectorSettings();

        if (!_inventory.AddWeapon(weapon))
        {
            Destroy(go);
            _flashlight = null;
        }
    }

    int FindFlashlightSlot()
    {
        if (_inventory == null || _inventory.weapons == null)
            return -1;

        for (int i = 0; i < _inventory.weapons.Count; i++)
        {
            Weapon weapon = _inventory.weapons[i];
            if (weapon != null && weapon.GetComponent<FlashlightItem>() != null)
                return i;
        }

        return -1;
    }

    void ApplyFlashlightInspectorSettings()
    {
        if (_flashlight == null)
            return;

        _flashlight.lightRange = flashlightRange;
        _flashlight.lightIntensity = flashlightIntensity;
        _flashlight.spotAngle = flashlightSpotAngle;
        _flashlight.ApplyLightSettings();
    }

    void EnsureCampfireVisibilityLight()
    {
        if (_campfireVisibilityLight != null)
            return;

        GameObject go = new GameObject("CampfireVisibilityLight");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        _campfireVisibilityLight = go.AddComponent<Light>();
        _campfireVisibilityLight.type = LightType.Point;
        _campfireVisibilityLight.shadows = LightShadows.None;
        _campfireVisibilityLight.enabled = false;
    }

    void UpdateCampfireVisibility()
    {
        EnsureCampfireVisibilityLight();
        if (_campfireVisibilityLight == null)
            return;

        bool insideLitCampfire = false;
        Campfire[] campfires = FindObjectsOfType<Campfire>();
        foreach (Campfire campfire in campfires)
        {
            if (campfire == null || !campfire.IsLit)
                continue;

            float radius = Mathf.Max(0f, campfire.safeRadius);
            Vector3 camp = campfire.transform.position;
            Vector3 player = transform.position;
            camp.y = 0f;
            player.y = 0f;

            if (Vector3.Distance(player, camp) <= radius)
            {
                insideLitCampfire = true;
                break;
            }
        }

        _campfireVisibilityLight.enabled = insideLitCampfire;
        _campfireVisibilityLight.range = Mathf.Max(1f, campfireZoneLightRange);
        _campfireVisibilityLight.intensity = Mathf.Max(0f, campfireZoneBrightness);
        _campfireVisibilityLight.color = campfireZoneLightColor;
    }
}
