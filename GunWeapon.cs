using System.Collections;
using UnityEngine;

public class GunWeapon : MonoBehaviour
{
    [Header("References")]
    public Camera playerCamera;
    public Transform muzzle;
    public ParticleSystem muzzleFlash;
    public AudioSource audioSource;
    public AudioClip fireSound;
    public GameObject impactEffectPrefab;

    [Header("Shooting")]
    public float damage = 35f;
    public float range = 160f;
    public float fireCooldown = 0.22f;
    public float bulletSpeed = 95f;
    public float bulletLifetime = 3f;
    public float bulletRadius = 0.055f;
    public float bulletVisualLength = 0.28f;
    public LayerMask hitMask = ~0;
    public bool infiniteAmmo = false;
    public float hipBulletSpreadDegrees = 4f;
    public float focusBulletSpreadDegrees = 0.25f;
    public float focusedDamageMultiplier = 1.15f;

    [Header("Ammo / Reload")]
    public KeyCode reloadKey = KeyCode.R;
    public int magazineSize = 8;
    public int currentAmmoInMagazine = 8;
    public int reserveAmmo = 24;
    public float reloadTime = 1.35f;

    [Header("Focus")]
    public float hipFov = 60f;
    public float focusFov = 42f;
    public float focusSpeed = 12f;
    public Vector3 hipHoldLocalPosition = new Vector3(0.36f, -0.24f, 0.72f);
    public Vector3 hipHoldLocalEuler = new Vector3(0f, -90f, 0f);
    public Vector3 focusHoldLocalPosition = new Vector3(0.02f, -0.13f, 0.68f);
    public Vector3 focusHoldLocalEuler = new Vector3(0f, -90f, 0f);

    [Header("Inventory")]
    public Sprite inventoryIcon;

    Weapon _weapon;
    Transform _playerRoot;
    float _nextFireTime;
    bool _focus;
    bool _isEquipped;
    bool _isReloading;
    bool _hasCapturedFov;
    float _capturedDefaultFov;

    void Awake()
    {
        _weapon = GetComponent<Weapon>();
        if (audioSource == null)
            audioSource = GetComponentInChildren<AudioSource>(true);
        if (muzzle == null)
            muzzle = FindMuzzle();
    }

    void OnEnable()
    {
        CaptureCameraFov();
    }

    void OnDisable()
    {
        _focus = false;
        if (playerCamera != null && _hasCapturedFov)
            playerCamera.fieldOfView = _capturedDefaultFov;
    }

    void Update()
    {
        if (!_isEquipped)
            return;

        if (Input.GetKeyDown(reloadKey))
            TryReload();

        UpdateFocusPose();
    }

    public void Initialize(Camera camera, Weapon weapon, Transform playerRoot)
    {
        playerCamera = camera != null ? camera : playerCamera;
        _weapon = weapon != null ? weapon : GetComponent<Weapon>();
        _playerRoot = playerRoot;
        _isEquipped = true;
        PrepareWeaponData();
        CaptureCameraFov();
    }

    public void SetEquipped(bool equipped)
    {
        _isEquipped = equipped;
        if (!equipped)
        {
            _focus = false;
            if (playerCamera != null && _hasCapturedFov)
                playerCamera.fieldOfView = _capturedDefaultFov;
        }
    }

    public void SetFocus(bool focus)
    {
        _focus = focus;
    }

    public bool IsFocusing => _focus;

    public bool TryShoot()
    {
        if (Time.time < _nextFireTime)
            return false;

        if (_isReloading)
            return false;

        if (!infiniteAmmo && currentAmmoInMagazine <= 0)
        {
            TryReload();
            return false;
        }

        if (playerCamera == null)
            playerCamera = Camera.main;
        if (playerCamera == null)
            return false;

        _nextFireTime = Time.time + Mathf.Max(0.02f, fireCooldown);
        if (!infiniteAmmo)
            currentAmmoInMagazine = Mathf.Max(0, currentAmmoInMagazine - 1);

        PlayFireEffects();

        float spread = _focus ? focusBulletSpreadDegrees : hipBulletSpreadDegrees;
        float damageMultiplier = _focus ? focusedDamageMultiplier : 1f;
        Vector3 shotDirection = ApplyBulletSpread(playerCamera.transform.forward, spread);
        SpawnRuntimeBullet(shotDirection, damageMultiplier);
        return true;
    }

    public bool TryReload()
    {
        if (infiniteAmmo || _isReloading)
            return false;

        if (currentAmmoInMagazine >= magazineSize || reserveAmmo <= 0)
            return false;

        StartCoroutine(ReloadRoutine());
        return true;
    }

    IEnumerator ReloadRoutine()
    {
        _isReloading = true;
        yield return new WaitForSeconds(Mathf.Max(0.05f, reloadTime));

        int missing = Mathf.Max(0, magazineSize - currentAmmoInMagazine);
        int loaded = Mathf.Min(missing, reserveAmmo);
        currentAmmoInMagazine += loaded;
        reserveAmmo -= loaded;
        _isReloading = false;
    }

    public int AddAmmo(int amount)
    {
        int added = Mathf.Max(0, amount);
        reserveAmmo += added;
        return added;
    }

    public void PrepareWeaponData()
    {
        if (_weapon == null)
            _weapon = GetComponent<Weapon>();
        if (_weapon == null)
            return;

        _weapon.isMelee = false;
        _weapon.damage = damage;
        _weapon.range = range;
        _weapon.hitMask = hitMask;
        _weapon.holdLocalPosition = hipHoldLocalPosition;
        _weapon.holdLocalEuler = hipHoldLocalEuler;
        if (string.IsNullOrWhiteSpace(_weapon.weaponName))
            _weapon.weaponName = gameObject.name.Replace("(Clone)", "").Trim();
        if (_weapon.icon == null)
            _weapon.icon = inventoryIcon != null ? inventoryIcon : CreateGeneratedGunIcon(_weapon.weaponName);

        magazineSize = Mathf.Max(1, magazineSize);
        currentAmmoInMagazine = Mathf.Clamp(currentAmmoInMagazine, 0, magazineSize);
        reserveAmmo = Mathf.Max(0, reserveAmmo);
    }

    void UpdateFocusPose()
    {
        if (_weapon == null)
            _weapon = GetComponent<Weapon>();

        if (playerCamera != null)
        {
            CaptureCameraFov();
            float targetFov = _focus ? focusFov : _capturedDefaultFov;
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFov, Time.deltaTime * Mathf.Max(1f, focusSpeed));
        }

        Transform t = transform;
        Vector3 targetPosition = _focus ? focusHoldLocalPosition : hipHoldLocalPosition;
        Vector3 targetEuler = _focus ? focusHoldLocalEuler : hipHoldLocalEuler;
        t.localPosition = Vector3.Lerp(t.localPosition, targetPosition, Time.deltaTime * Mathf.Max(1f, focusSpeed));
        t.localRotation = Quaternion.Slerp(t.localRotation, Quaternion.Euler(targetEuler), Time.deltaTime * Mathf.Max(1f, focusSpeed));
    }

    void CaptureCameraFov()
    {
        if (playerCamera == null)
            return;

        if (!_hasCapturedFov)
        {
            _capturedDefaultFov = playerCamera.fieldOfView;
            hipFov = _capturedDefaultFov;
            _hasCapturedFov = true;
        }
    }

    void PlayFireEffects()
    {
        if (muzzleFlash != null)
            muzzleFlash.Play();

        if (audioSource != null && fireSound != null)
            audioSource.PlayOneShot(fireSound);
    }

    void SpawnImpact(Vector3 point, Vector3 normal)
    {
        if (impactEffectPrefab == null)
            return;

        Quaternion rotation = Quaternion.LookRotation(normal);
        Destroy(Instantiate(impactEffectPrefab, point + normal * 0.01f, rotation), 2f);
    }

    Vector3 ApplyBulletSpread(Vector3 direction, float spreadDegrees)
    {
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        spreadDegrees = Mathf.Max(0f, spreadDegrees);
        if (spreadDegrees <= 0.001f)
            return direction;

        Quaternion spreadRotation = Quaternion.Euler(
            Random.Range(-spreadDegrees, spreadDegrees),
            Random.Range(-spreadDegrees, spreadDegrees),
            0f);
        return (playerCamera.transform.rotation * spreadRotation * Vector3.forward).normalized;
    }

    void SpawnRuntimeBullet(Vector3 direction, float damageMultiplier)
    {
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        Vector3 origin = playerCamera.transform.position + direction * 0.45f;
        if (muzzle != null)
            origin = Vector3.Lerp(origin, muzzle.position, 0.35f);

        GameObject bullet = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        bullet.name = "Runtime Bullet";
        bullet.transform.position = origin;
        bullet.transform.rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(90f, 0f, 0f);
        bullet.transform.localScale = new Vector3(
            Mathf.Max(0.01f, bulletRadius),
            Mathf.Max(0.03f, bulletVisualLength),
            Mathf.Max(0.01f, bulletRadius));

        Collider bulletCollider = bullet.GetComponent<Collider>();
        if (bulletCollider != null)
            bulletCollider.isTrigger = false;

        Rigidbody rb = bullet.AddComponent<Rigidbody>();
        rb.mass = 0.04f;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.velocity = direction * Mathf.Max(5f, bulletSpeed);

        BulletProjectile projectile = bullet.AddComponent<BulletProjectile>();
        projectile.Initialize(damage * Mathf.Max(0.01f, damageMultiplier), range, bulletLifetime, impactEffectPrefab, _playerRoot);
    }

    Transform FindMuzzle()
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            string n = child.name.ToLowerInvariant();
            if (n.Contains("muzzle") || n.Contains("barrel") || n.Contains("firepoint"))
                return child;
        }
        return null;
    }

    public static Sprite CreateGeneratedGunIcon(string itemName)
    {
        string key = string.IsNullOrWhiteSpace(itemName) ? "gun" : itemName.Trim().ToLowerInvariant();
        Texture2D texture = new Texture2D(96, 96, TextureFormat.RGBA32, false);
        texture.name = $"GeneratedGunIcon_{key.Replace(' ', '_')}";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
                texture.SetPixel(x, y, clear);
        }

        DrawGeneratedGun(texture);
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 96f);
        sprite.name = $"{key} Inventory Icon";
        return sprite;
    }

    static void DrawGeneratedGun(Texture2D texture)
    {
        Color shadow = new Color(0f, 0f, 0f, 0.3f);
        Color bodyDark = new Color(0.05f, 0.055f, 0.06f, 1f);
        Color body = new Color(0.16f, 0.17f, 0.18f, 1f);
        Color metal = new Color(0.44f, 0.48f, 0.5f, 1f);
        Color highlight = new Color(0.86f, 0.88f, 0.86f, 1f);
        Color grip = new Color(0.12f, 0.075f, 0.045f, 1f);
        Color accent = new Color(0.95f, 0.58f, 0.12f, 1f);

        FillRect(texture, 18, 48, 78, 58, shadow);
        FillRect(texture, 16, 42, 74, 52, body);
        FillRect(texture, 20, 38, 60, 45, metal);
        FillRect(texture, 60, 40, 84, 45, bodyDark);
        FillRect(texture, 80, 41, 90, 44, metal);
        FillRect(texture, 27, 53, 42, 76, grip);
        FillRect(texture, 43, 53, 53, 61, bodyDark);
        FillRect(texture, 50, 51, 58, 56, accent);
        FillRect(texture, 24, 39, 55, 41, highlight);
        FillRect(texture, 64, 42, 78, 43, highlight);
        FillRect(texture, 30, 57, 35, 74, new Color(0.2f, 0.13f, 0.08f, 1f));
    }

    static void FillRect(Texture2D texture, int xMin, int yMin, int xMax, int yMax, Color color)
    {
        for (int y = Mathf.Max(0, yMin); y < Mathf.Min(texture.height, yMax); y++)
        {
            for (int x = Mathf.Max(0, xMin); x < Mathf.Min(texture.width, xMax); x++)
                texture.SetPixel(x, y, color);
        }
    }
}
