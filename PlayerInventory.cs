using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class PlayerInventory : MonoBehaviour
{
    public int maxSlots = 10;
    public List<Weapon> weapons = new List<Weapon>();
    public int activeIndex = -1;
    public Camera playerCamera;
    public InventoryUI inventoryUI;

    [Header("Weapon limits")]
    [Tooltip("Limits actual combat weapons in inventory. Food and flashlight items do not count.")]
    public int maxCombatWeapons = 1;

    [Header("Weapon in hand")]
    [Tooltip("Assign a camera child transform, not a UI element. If empty or invalid, a WeaponHold child is created on the camera.")]
    public Transform weaponHoldPoint;

    [Header("Attack input")]
    [Tooltip("If true, left click only registers when the cursor is locked (FPS mode).")]
    public bool requireCursorLockedToAttack;
    [Tooltip("Ignore attacks while the pointer is over uGUI.")]
    public bool ignoreAttackWhenPointerOverUI = true;

    [Header("Inventory food hold pose")]
    [Tooltip("Stable camera-relative pose for inventory food items (e.g. bunny meat).")]
    public Vector3 foodHoldLocalPosition = new Vector3(0.28f, -0.25f, 0.62f);
    public Vector3 foodHoldLocalEuler = new Vector3(8f, 70f, 0f);
    public float foodHoldScale = 1.45f;
    [Tooltip("Minimum distance kept in front of the camera so held food does not slip behind the near clip plane.")]
    public float foodMinCameraDistance = 0.38f;
    [Tooltip("Extra space kept between the held food and nearby walls.")]
    public float foodWallPadding = 0.08f;
    [Tooltip("Sphere radius used to keep held food from disappearing into walls.")]
    public float foodWallProbeRadius = 0.08f;
    [Tooltip("Layers checked when preventing held food from being hidden by walls.")]
    public LayerMask foodWallMask = ~0;

    bool _visualsDirty = true;
    Transform _playerRoot;
    Coroutine _meleeSwingCo;

    void Awake()
    {
        _playerRoot = transform.root;
    }

    void Start()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true)
                ?? transform.root.GetComponentInChildren<Camera>(true)
                ?? Camera.main;

        if (weaponHoldPoint != null && !IsValidWeaponHoldPoint(weaponHoldPoint))
            weaponHoldPoint = null;

        if (inventoryUI != null)
            inventoryUI.Initialize(this);

        EnsureWeaponHoldPoint();
        EnsureInventoryIcons();
        RefreshUI();
        _visualsDirty = true;
    }

    void LateUpdate()
    {
        if (_visualsDirty)
            ApplyWeaponVisuals();

        UpdateActiveInventoryFoodPose();
    }

    void EnsureWeaponHoldPoint()
    {
        if (weaponHoldPoint != null && IsValidWeaponHoldPoint(weaponHoldPoint))
            return;

        weaponHoldPoint = null;

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>(true)
                ?? transform.root.GetComponentInChildren<Camera>(true)
                ?? Camera.main;
        }

        if (playerCamera == null)
            return;

        Transform cam = playerCamera.transform;
        Transform existing = cam.Find("WeaponHold");
        if (existing != null)
        {
            weaponHoldPoint = existing;
            return;
        }

        GameObject hold = new GameObject("WeaponHold");
        hold.transform.SetParent(cam, false);
        hold.transform.localPosition = new Vector3(0.35f, -0.22f, 0.48f);
        hold.transform.localRotation = Quaternion.identity;
        weaponHoldPoint = hold.transform;
    }

    public bool HasActiveWeapon()
    {
        return GetActiveWeapon() != null;
    }

    public bool SetActiveSlot(int index)
    {
        if (index < 0 || index >= weapons.Count)
            return false;

        if (activeIndex == index)
        {
            RefreshUI();
            _visualsDirty = true;
            return true;
        }

        StopMeleeSwing();
        activeIndex = index;
        RefreshUI();
        _visualsDirty = true;
        return true;
    }

    public bool AddWeapon(Weapon w)
    {
        if (w == null)
            return false;
        if (weapons.Contains(w))
            return false;
        if (weapons.Count >= maxSlots)
            return false;
        if (IsCombatWeapon(w) && CountCombatWeapons() >= Mathf.Max(0, maxCombatWeapons))
            return false;

        CleanupWeaponPhysics(w);
        w.EnsureInventoryIcon();

        weapons.Add(w);
        if (activeIndex == -1)
            activeIndex = 0;

        RefreshUI();
        _visualsDirty = true;
        return true;
    }

    public bool IsCombatWeapon(Weapon w)
    {
        if (w == null)
            return false;

        if (w.GetComponent<FoodPickup>() != null)
            return false;

        if (w.GetComponent<FlashlightItem>() != null)
            return false;

        return true;
    }

    public int CountCombatWeapons()
    {
        int count = 0;
        if (weapons == null)
            return count;

        foreach (Weapon weapon in weapons)
        {
            if (IsCombatWeapon(weapon))
                count++;
        }

        return count;
    }

    public void RemoveAllCombatWeapons(bool destroyObjects)
    {
        if (weapons == null)
            return;

        for (int i = weapons.Count - 1; i >= 0; i--)
        {
            Weapon weapon = weapons[i];
            if (!IsCombatWeapon(weapon))
                continue;

            weapons.RemoveAt(i);
            if (destroyObjects && weapon != null)
                Destroy(weapon.gameObject);
        }

        if (activeIndex >= weapons.Count)
            activeIndex = weapons.Count - 1;
        if (weapons.Count == 0)
            activeIndex = -1;

        RefreshUI();
        _visualsDirty = true;
    }

    void CleanupWeaponPhysics(Weapon w)
    {
        if (w == null)
            return;

        foreach (Collider c in w.GetComponentsInChildren<Collider>())
        {
            if (c != null)
                c.enabled = false;
        }

        foreach (Rigidbody rb in w.GetComponentsInChildren<Rigidbody>())
        {
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }
    }

    public void RemoveWeaponAt(int index)
    {
        if (index < 0 || index >= weapons.Count)
            return;

        weapons.RemoveAt(index);
        if (activeIndex >= weapons.Count)
            activeIndex = weapons.Count - 1;
        if (weapons.Count == 0)
            activeIndex = -1;

        RefreshUI();
        _visualsDirty = true;
    }

    public Weapon GetActiveWeapon()
    {
        if (activeIndex < 0 || activeIndex >= weapons.Count)
            return null;
        return weapons[activeIndex];
    }

    void Update()
    {
        int slotInputLimit = Mathf.Min(maxSlots, 10);
        for (int i = 0; i < slotInputLimit; i++)
        {
            if (Input.GetKeyDown(GetSlotKeyCode(i)))
            {
                if (ShouldReserveSlotForWood(i))
                    continue;

                if (i < weapons.Count)
                {
                    SetActiveSlot(i);
                }
            }
        }

        Weapon activeWeapon = GetActiveWeapon();
        GunWeapon activeGun = activeWeapon != null ? activeWeapon.GetComponent<GunWeapon>() : null;
        if (activeGun != null)
        {
            activeGun.SetFocus(WantsGunFocus());
            if (WantsGunShotThisFrame(activeGun.IsFocusing))
                activeGun.TryShoot();
            return;
        }

        if (!WantsPrimaryAttackThisFrame())
            return;

        Weapon w = activeWeapon;
        if (w == null || playerCamera == null)
            return;

        FoodPickup storedFood = w.GetComponent<FoodPickup>();
        if (storedFood != null && storedFood.allowInventoryPickup)
            return;

        if (w.isMelee)
            BeginMeleeSwing(w);

        FireWeapon(w);
    }

    bool WantsGunFocus()
    {
        if (!Input.GetMouseButton(1))
            return false;

        return AllowsWeaponInput();
    }

    bool WantsGunShotThisFrame(bool aiming)
    {
        if (!Input.GetButtonDown("Fire1") && !Input.GetMouseButtonDown(0))
            return false;

        return AllowsWeaponInput(aiming);
    }

    bool WantsPrimaryAttackThisFrame()
    {
        bool pressed = Input.GetButtonDown("Fire1") || Input.GetMouseButtonDown(0);
        if (!pressed)
            return false;

        return AllowsWeaponInput();
    }

    bool AllowsWeaponInput(bool allowThroughUI = false)
    {
        if (requireCursorLockedToAttack && Cursor.lockState != CursorLockMode.Locked)
            return false;

        if (!allowThroughUI && ignoreAttackWhenPointerOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(-1))
            return false;

        return true;
    }

    KeyCode GetSlotKeyCode(int slotIndex)
    {
        return slotIndex == 9 ? KeyCode.Alpha0 : (KeyCode)((int)KeyCode.Alpha1 + slotIndex);
    }

    bool ShouldReserveSlotForWood(int slotIndex)
    {
        PlayerWoodCarry carry = GetComponent<PlayerWoodCarry>();
        if (carry == null || carry.Current <= 0)
            return false;

        int woodSlotIndex = IsInventorySlotOccupied(5) ? 6 : 5;
        return slotIndex == woodSlotIndex;
    }

    bool IsInventorySlotOccupied(int slotIndex)
    {
        return weapons != null
            && slotIndex >= 0
            && slotIndex < weapons.Count
            && weapons[slotIndex] != null;
    }

    void StopMeleeSwing()
    {
        if (_meleeSwingCo == null)
            return;
        StopCoroutine(_meleeSwingCo);
        _meleeSwingCo = null;
    }

    void BeginMeleeSwing(Weapon w)
    {
        StopMeleeSwing();
        _meleeSwingCo = StartCoroutine(MeleeSwingRoutine(w));
    }

    IEnumerator MeleeSwingRoutine(Weapon w)
    {
        Transform t = w.transform;
        Vector3 axis = w.swingLocalEulerAxis.sqrMagnitude < 0.0001f
            ? Vector3.right
            : w.swingLocalEulerAxis.normalized;

        float peak = w.swingPeakDegrees;
        float outT = Mathf.Max(0.02f, w.swingOutTime);
        float inT = Mathf.Max(0.02f, w.swingInTime);
        Quaternion baseRot = Quaternion.Euler(w.holdLocalEuler);

        float e = 0f;
        while (e < outT)
        {
            e += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / outT));
            float ang = peak * k;
            t.localRotation = baseRot * Quaternion.Euler(axis * ang);
            yield return null;
        }

        e = 0f;
        while (e < inT)
        {
            e += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / inT));
            float ang = peak * (1f - k);
            t.localRotation = baseRot * Quaternion.Euler(axis * ang);
            yield return null;
        }

        w.ApplyHoldPose();
        _meleeSwingCo = null;
    }

    void FireWeapon(Weapon w)
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit hit;

        if (!TryGetWeaponHit(w, ray, out hit))
            return;

        BunnyHealth bunny = hit.collider.GetComponentInParent<BunnyHealth>();
        if (bunny != null)
        {
            bunny.RegisterWeaponHit(hit.point);
            return;
        }

        Health h = hit.collider.GetComponentInParent<Health>();
        if (h != null && !IsOwnHealth(h))
            h.TakeDamage(w.damage);
    }

    bool IsOwnHealth(Health h)
    {
        return h.transform.root == _playerRoot;
    }

    bool TryGetWeaponHit(Weapon w, Ray ray, out RaycastHit bestHit)
    {
        bestHit = default;
        float maxDist = Mathf.Max(0.15f, w.range);
        LayerMask mask = w.hitMask;

        if (w.isMelee)
            return TryMeleeHit(ray, maxDist, w.meleeRadius, mask, out bestHit);

        Vector3 origin = ray.origin + ray.direction * 0.05f;
        float dist = Mathf.Max(0.1f, maxDist - 0.05f);
        if (!Physics.Raycast(origin, ray.direction, out bestHit, dist, mask, QueryTriggerInteraction.Ignore))
            return false;

        if (IsOwnCollider(bestHit.collider))
            return false;

        return true;
    }

    bool TryMeleeHit(Ray ray, float maxDist, float radius, LayerMask mask, out RaycastHit bestHit)
    {
        bestHit = default;
        float r = Mathf.Max(0.05f, radius);
        float inset = 0.35f;
        Vector3 origin = ray.origin + ray.direction * inset;
        float travel = Mathf.Max(0.15f, maxDist - inset);

        RaycastHit[] hits = Physics.SphereCastAll(origin, r, ray.direction, travel, mask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
        {
            if (Physics.Raycast(origin, ray.direction, out bestHit, travel, mask, QueryTriggerInteraction.Ignore))
                return !IsOwnCollider(bestHit.collider);
            return false;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit h in hits)
        {
            if (h.collider == null || IsOwnCollider(h.collider))
                continue;
            bestHit = h;
            return true;
        }

        return false;
    }

    bool IsOwnCollider(Collider c)
    {
        return c.transform.root == _playerRoot;
    }

    void ApplyWeaponVisuals()
    {
        _visualsDirty = false;
        EnsureWeaponHoldPoint();

        for (int i = 0; i < weapons.Count; i++)
        {
            Weapon w = weapons[i];
            if (w == null)
                continue;

            Transform t = w.transform;
            bool equipped = i == activeIndex && activeIndex >= 0;
            FoodPickup storedFood = w.GetComponent<FoodPickup>();
            bool isInventoryFood = storedFood != null && storedFood.allowInventoryPickup;

            if (equipped && weaponHoldPoint != null)
            {
                t.SetParent(weaponHoldPoint, false);
                GunWeapon gun = w.GetComponent<GunWeapon>();
                if (gun != null)
                    gun.SetEquipped(true);

                if (isInventoryFood)
                {
                    t.localPosition = foodHoldLocalPosition;
                    t.localRotation = Quaternion.Euler(foodHoldLocalEuler);
                    t.localScale = Vector3.one * Mathf.Max(0.1f, foodHoldScale);
                }
                else
                {
                    w.ApplyHoldPose();
                }

                // Safety clamp: keep item in front of camera even if a bad pose slips in.
                Vector3 lp = t.localPosition;
                if (lp.z < 0.2f)
                    lp.z = 0.2f;
                t.localPosition = lp;

                if (!t.gameObject.activeSelf)
                    t.gameObject.SetActive(true);
            }
            else
            {
                GunWeapon gun = w.GetComponent<GunWeapon>();
                if (gun != null)
                    gun.SetEquipped(false);

                t.SetParent(transform, false);
                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.identity;
                if (t.gameObject.activeSelf)
                    t.gameObject.SetActive(false);
            }
        }
    }

    void UpdateActiveInventoryFoodPose()
    {
        Weapon active = GetActiveWeapon();
        if (active == null || playerCamera == null)
            return;

        FoodPickup storedFood = active.GetComponent<FoodPickup>();
        if (storedFood == null || !storedFood.allowInventoryPickup)
            return;

        EnsureWeaponHoldPoint();
        if (weaponHoldPoint == null)
            return;

        Transform t = active.transform;
        if (t.parent != weaponHoldPoint)
        {
            _visualsDirty = true;
            return;
        }

        Quaternion desiredLocalRotation = Quaternion.Euler(foodHoldLocalEuler);
        Vector3 desiredWorldPosition = weaponHoldPoint.TransformPoint(foodHoldLocalPosition);
        Vector3 cameraPosition = playerCamera.transform.position;
        Vector3 toDesired = desiredWorldPosition - cameraPosition;
        float desiredDistance = toDesired.magnitude;
        if (desiredDistance <= 0.0001f)
            return;

        Vector3 direction = toDesired / desiredDistance;
        float minVisibleDistance = Mathf.Max(
            foodMinCameraDistance,
            playerCamera.nearClipPlane + Mathf.Max(0.04f, foodWallPadding * 0.5f));
        float adjustedDistance = desiredDistance;

        if (TryGetFoodWallHit(cameraPosition, direction, desiredDistance, out RaycastHit wallHit))
            adjustedDistance = Mathf.Clamp(wallHit.distance - foodWallPadding, minVisibleDistance, desiredDistance);

        Vector3 adjustedWorldPosition = cameraPosition + direction * adjustedDistance;

        t.localRotation = desiredLocalRotation;
        t.localPosition = weaponHoldPoint.InverseTransformPoint(adjustedWorldPosition);
        t.localScale = Vector3.one * Mathf.Max(0.1f, foodHoldScale);
    }

    bool TryGetFoodWallHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit bestHit)
    {
        bestHit = default;

        float castDistance = Mathf.Max(0.01f, distance);
        float radius = Mathf.Max(0.01f, foodWallProbeRadius);
        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            radius,
            direction,
            castDistance,
            foodWallMask,
            QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
            return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || IsOwnCollider(hit.collider))
                continue;

            bestHit = hit;
            return true;
        }

        return false;
    }

    void RefreshUI()
    {
        EnsureInventoryIcons();
        if (inventoryUI != null)
            inventoryUI.SetInventory(weapons, activeIndex);
    }

    void EnsureInventoryIcons()
    {
        if (weapons == null)
            return;

        foreach (Weapon weapon in weapons)
        {
            if (weapon != null)
                weapon.EnsureInventoryIcon();
        }
    }

    public void MarkWeaponVisualsDirty() => _visualsDirty = true;

    bool IsValidWeaponHoldPoint(Transform t)
    {
        if (t == null)
            return false;

        if (t.GetComponent<RectTransform>() != null)
            return false;

        if (playerCamera != null && !t.IsChildOf(playerCamera.transform))
            return false;

        return true;
    }

    public void ClearWeaponsForResume()
    {
        foreach (Weapon w in weapons)
        {
            if (w != null && w.gameObject != null)
                Destroy(w.gameObject);
        }

        weapons.Clear();
        activeIndex = -1;
        RefreshUI();
        _visualsDirty = true;
    }
}
