using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInventory))]
[RequireComponent(typeof(PlayerHunger))]
[RequireComponent(typeof(PlayerWoodCarry))]
public class SoloLobbyController : MonoBehaviour
{
    [Header("Components")]
    public CharacterController controllerComp;
    public Transform cameraTransform;

    [Header("Movement")]
    public float walkSpeed = 6f;
    public float sprintSpeed = 12f;
    public float crouchSpeed = 3.5f;
    [Tooltip("Vertical acceleration while airborne. More negative = stronger gravity.")]
    public float gravity = -28f;
    [Tooltip("Approximate jump apex height in meters (paired with Gravity).")]
    public float jumpHeight = 0.5f;

    [Header("Stamina")]
    public float maxStamina = 5f;
    public float staminaDrainRate = 1f;
    public float staminaRegenRate = 0.75f;
    public float staminaRegenDelay = 1.0f;

    [Header("Camera")]
    public float mouseSensitivity = 200f;
    public float standingCameraY = 0.8f;
    public float crouchCameraY = 0.45f;

    [Header("Heights")]
    public float standingHeight = 1.8f;
    public float crouchHeight = 1.0f;
    public float heightTransitionSpeed = 8f;

    [Header("Jumping")]
    [Tooltip("Allow a short window after leaving ground to still jump (makes jumping feel responsive).")]
    public float coyoteTime = 0.12f;
    [Tooltip("Horizontal control while airborne (1 = full walk speed in air).")]
    [Range(0.05f, 1f)]
    public float airControlMultiplier = 0.28f;
    [Tooltip("When grounded, snap downward velocity so isGrounded stays stable on slopes.")]
    public float groundedDownwardSnap = -2f;
    [Tooltip("Multiplies gravity while falling (velocity.y < 0). Larger values make landings feel heavier.")]
    public float fallGravityMultiplier = 1.8f;

    [Header("Interaction")]
    public float interactDistance = 5f;
    public KeyCode interactKey = KeyCode.E;
    public KeyCode woodPickupKey = KeyCode.F;
    public KeyCode dropWoodKey = KeyCode.Q;

    [Header("Wood Drop")]
    public GameObject woodDropPrefab; // WoodPickup prefab to instantiate when dropping
    [Tooltip("Prefab shown in the player's hand when selecting carried wood. Falls back to Wood Drop Prefab when empty.")]
    public GameObject heldWoodPrefab;
    public Vector3 heldWoodLocalPosition = new Vector3(0.3f, -0.28f, 0.9f);
    public Vector3 heldWoodLocalEuler = new Vector3(8f, -24f, -10f);
    public float heldWoodLocalScale = 0.35f;
    public float dropDistance = 2f; // how far in front of player to drop

    [Header("UI (assign your existing StaminaUI)")]
    public StaminaUI staminaUI;

    [Header("Starter / Pickup")]
    public GameObject starterWeaponPrefab; // optional: assign a weapon prefab (has Weapon component)
    [Range(0f, 1f)]
    public float starterHungerFillFraction = 1f; // 1 = full hunger; 0.5 = 50%
    [Range(0f, 1f)]
    [Tooltip("When hunger is at or below this value, inventory eat hint will pulse.")]
    public float lowHungerHintThreshold = 0.35f;

    // internal state
    float xRotation = 0f;
    Vector3 velocity;
    bool isCrouching = false;      // visual/height state
    bool crouchHeld = false;       // actual key state

    // sliding state removed

    // stamina state
    float currentStamina;
    float lastSprintTime = -999f;

    // grounding / coyote
    float lastGroundedTime = -999f;

    // inventory / hunger caches
    PlayerInventory _inventory;
    PlayerHunger _hunger;
    PlayerWoodCarry _woodCarry;
    GameplayPauseSettings _pauseSettings;
    GameObject _heldWoodVisual;
    bool _showHeldWood;

    void Start()
    {
        if (controllerComp == null) controllerComp = GetComponent<CharacterController>();
        ResolveCamera();

        // Ensure this player object stays active
        Debug.Log($"controller.Start: gameObject.activeSelf={gameObject.activeSelf}");
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            Debug.Log("controller.Start: Enabled player object");
        }

        if (controllerComp != null) controllerComp.height = standingHeight;
        if (cameraTransform != null)
        {
            // Reset camera rotation to identity at start
            xRotation = 0f;
            cameraTransform.localRotation = Quaternion.identity;
            Debug.Log($"controller.Start: Reset camera rotation to identity");
            
            Vector3 camLocal = cameraTransform.localPosition;
            camLocal.y = standingCameraY;
            cameraTransform.localPosition = camLocal;

            // Ensure camera object is active and Camera component is enabled
            Debug.Log($"controller.Start: cameraTransform.gameObject.activeSelf={cameraTransform.gameObject.activeSelf}");
            if (!cameraTransform.gameObject.activeSelf)
            {
                cameraTransform.gameObject.SetActive(true);
                Debug.Log("controller.Start: Enabled camera object");
            }
            
            Camera cam = cameraTransform.GetComponent<Camera>();
            if (cam != null)
            {
                Debug.Log($"controller.Start: cam.enabled={cam.enabled}");
                if (!cam.enabled)
                {
                    cam.enabled = true;
                    Debug.Log("controller.Start: Enabled Camera component");
                }
                
                // Ensure camera renders to screen, not to a render texture
                if (cam != null && cam.targetTexture != null)
                    cam.targetTexture = null;

                cam.targetDisplay = 0;
            }
        }

        currentStamina = maxStamina;

        _inventory = GetComponent<PlayerInventory>();
        _hunger = GetComponent<PlayerHunger>();
        _woodCarry = GetComponent<PlayerWoodCarry>();
        if (_woodCarry != null)
            _woodCarry.OnWoodChanged += OnWoodCarryChanged;
        _pauseSettings = GetComponent<GameplayPauseSettings>();

        if (GetComponent<FPSBarUI>() == null)
            gameObject.AddComponent<FPSBarUI>();
        
        // Apply saved sensitivity setting (same key as SettingsMenu)
        const string PREF_SENSITIVITY = "player_sensitivity";
        float savedSensitivity = PlayerPrefs.GetFloat(PREF_SENSITIVITY, 200f);
        mouseSensitivity = savedSensitivity;
        Debug.Log($"Applied sensitivity from settings: {savedSensitivity}");

        if (_inventory != null && _inventory.inventoryUI == null)
        {
            InventoryUI foundInventoryUI = FindObjectOfType<InventoryUI>();
            if (foundInventoryUI == null)
            {
                GameObject inventoryUIGO = new GameObject("InventoryUI");
                foundInventoryUI = inventoryUIGO.AddComponent<InventoryUI>();
            }

            _inventory.inventoryUI = foundInventoryUI;
            _inventory.inventoryUI.Initialize(_inventory);
        }

        bool resumed = RunStatePersistence.TryApplyToPlayer(transform.root.gameObject);

        if (!resumed && _hunger != null && _hunger.GetHungerNormalized() < starterHungerFillFraction)
            _hunger.Eat(starterHungerFillFraction);

        if (_inventory != null && _inventory.weapons != null && _inventory.weapons.Count == 0 && starterWeaponPrefab != null)
        {
            GameObject go = Instantiate(starterWeaponPrefab, transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            Weapon w = go.GetComponent<Weapon>();
            if (w != null)
            {
                bool added = _inventory.AddWeapon(w);
                if (!added)
                    Destroy(go);
            }
            else
            {
                Destroy(go);
            }
        }

        UpdateWoodUI();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // ESC is now handled by PauseMenuUI, so we don't need to handle it here
        if (CraftingTableUI.IsOpen || CraftingTableUI.ClosedThisFrame)
        {
            UpdateUI();
            return;
        }

        // Only handle cursor locking when clicking
        if (Cursor.lockState == CursorLockMode.None && Input.GetMouseButtonDown(0))
        {
            // Check if we're not over any UI elements
            if (!UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        MouseLook();

        // read crouch key
        crouchHeld = Input.GetKey(KeyCode.LeftControl);
        isCrouching = crouchHeld;

        // update grounded time for coyote
        if (controllerComp != null && controllerComp.isGrounded)
            lastGroundedTime = Time.time;

        HandleStamina();
        Movement();
        if (TryConsumeActiveInventoryFood())
        {
            SmoothHeightAndCamera();
            UpdateUI();
            return;
        }
        Interaction(); // enhanced interaction/pickup logic
        DropWoodIfPressed();
        HandleHeldWoodInput();
        SmoothHeightAndCamera();
        UpdateUI();
    }

    // CAMERA
    void MouseLook()
    {
        if (cameraTransform == null)
        {
            ResolveCamera();
            if (cameraTransform == null) return;
        }

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // Debug camera rotation
        if (Mathf.Abs(mouseY) > 0.01f)
        {
            Debug.Log($"MouseY input: {mouseY}, xRotation before: {xRotation}");
        }

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        // Ensure camera local rotation is properly set (full rotation, not just pitch)
        cameraTransform.localEulerAngles = new Vector3(xRotation, cameraTransform.localEulerAngles.y, cameraTransform.localEulerAngles.z);
        
        if (Mathf.Abs(mouseX) > 0.01f || Mathf.Abs(mouseY) > 0.01f)
        {
            Debug.Log($"Camera rotation - X: {xRotation}, Position: {cameraTransform.localPosition}");
        }
        
        transform.Rotate(Vector3.up * mouseX);
    }


    // STAMINA
    bool IsTryingToSprint()
    {
        return Input.GetKey(KeyCode.LeftShift) && (Mathf.Abs(Input.GetAxis("Vertical")) > 0.01f || Mathf.Abs(Input.GetAxis("Horizontal")) > 0.01f);
    }

    void HandleStamina()
    {
        bool sprinting = IsTryingToSprint() && !crouchHeld && controllerComp != null && controllerComp.isGrounded && currentStamina > 0f;

        if (sprinting)
        {
            currentStamina -= staminaDrainRate * Time.deltaTime;
            currentStamina = Mathf.Max(0f, currentStamina);
            lastSprintTime = Time.time;
        }
        else if (Time.time - lastSprintTime >= staminaRegenDelay)
        {
            currentStamina += staminaRegenRate * Time.deltaTime;
            currentStamina = Mathf.Min(maxStamina, currentStamina);
        }
    }

    // MOVEMENT + JUMP
    void Movement()
    {
        if (controllerComp == null) return;

        if (controllerComp.isGrounded && velocity.y < 0f)
            velocity.y = groundedDownwardSnap;

        // Normal movement
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        float targetSpeed = isCrouching ? crouchSpeed : (Input.GetKey(KeyCode.LeftShift) && currentStamina > 0f ? sprintSpeed : walkSpeed);

        Vector3 move = transform.right * x + transform.forward * z;
        float horizontalScale = controllerComp.isGrounded ? 1f : airControlMultiplier;
        if (move.sqrMagnitude > 1f)
            move.Normalize();
        controllerComp.Move(move * targetSpeed * horizontalScale * Time.deltaTime);

        // Jump: allow whenever grounded or within coyote time (no cooldown)
        if (Input.GetButtonDown("Jump") && (controllerComp.isGrounded || Time.time - lastGroundedTime <= coyoteTime))
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        ApplyVerticalGravity();
        controllerComp.Move(velocity * Time.deltaTime);
    }

    void ApplyVerticalGravity()
    {
        float g = gravity;
        if (velocity.y < 0f)
            g *= fallGravityMultiplier;
        velocity.y += g * Time.deltaTime;
    }

    // HEIGHT & CAMERA
    void SmoothHeightAndCamera()
    {
        if (controllerComp == null || cameraTransform == null) return;

        float desiredHeight = isCrouching ? crouchHeight : standingHeight;
        float desiredCamY = isCrouching ? crouchCameraY : standingCameraY;

        float newHeight = Mathf.Lerp(controllerComp.height, desiredHeight, Time.deltaTime * heightTransitionSpeed);
        float heightDiff = newHeight - controllerComp.height;
        Vector3 newCenter = controllerComp.center;
        newCenter.y += heightDiff * 0.5f;

        controllerComp.height = newHeight;
        controllerComp.center = newCenter;

        Vector3 camLocal = cameraTransform.localPosition;
        camLocal.y = Mathf.Lerp(camLocal.y, desiredCamY, Time.deltaTime * heightTransitionSpeed);
        cameraTransform.localPosition = camLocal;
    }

    // INTERACTION (enhanced: E pickup + debug)
    void Interaction()
    {
        if (cameraTransform == null)
            return;

        Camera cam = ResolveCamera();
        if (cam == null)
        {
            Debug.LogWarning("Interaction: Camera is disabled or missing, cannot interact.");
            return;
        }

        bool useInteract = Input.GetKeyDown(interactKey);
        bool woodOnly = Input.GetKeyDown(woodPickupKey);
        if (!useInteract && !woodOnly)
            return;

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, interactDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
            return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        RaycastHit? validHit = null;
        foreach (RaycastHit candidate in hits)
        {
            if (candidate.collider == null)
                continue;

            if (candidate.collider.transform.IsChildOf(transform) || candidate.collider.GetComponentInParent<controller>() == this)
                continue;

            validHit = candidate;
            break;
        }

        if (validHit == null)
            return;

        RaycastHit hit = validHit.Value;

        if (woodOnly)
        {
            if (TryPickupWood(hit))
                return;

            FoodPickup foodToStore = hit.collider.GetComponent<FoodPickup>()
                ?? hit.collider.GetComponentInParent<FoodPickup>()
                ?? hit.collider.GetComponentInChildren<FoodPickup>();
            if (foodToStore != null)
            {
                if (_inventory == null)
                {
                    Debug.Log("No inventory found, cannot store food.");
                    return;
                }

                if (foodToStore.TryStoreInInventory(_inventory))
                {
                    Debug.Log($"Stored '{foodToStore.inventoryItemName}' in inventory.");
                }
                else
                {
                    Debug.Log("Cannot store food item (inventory full or item is not inventory-storable).");
                }

                return;
            }

            if (hit.collider.CompareTag("Wood"))
                Debug.Log("Pointed at wood, but the object is missing a WoodPickup component.");
            else
                Debug.Log("No wood or storable food target in front of player.");
            return;
        }

        WeaponPickup wp = hit.collider.GetComponentInParent<WeaponPickup>()
            ?? hit.collider.GetComponentInChildren<WeaponPickup>();
        if (wp != null && _inventory != null)
        {
            if (!wp.TryPickupInto(_inventory))
                Debug.Log("Inventory full, cannot pick up weapon.");
            return;
        }

        FoodPickup food = hit.collider.GetComponent<FoodPickup>()
            ?? hit.collider.GetComponentInParent<FoodPickup>()
            ?? hit.collider.GetComponentInChildren<FoodPickup>();
        if (food == null)
        {
            string name = hit.collider.name.ToLower();
            if (hit.collider.CompareTag("Food") || name.Contains("food") || name.Contains("meat"))
            {
                GameObject target = hit.collider.gameObject;
                food = target.AddComponent<FoodPickup>();
                Collider foodCollider = target.GetComponent<Collider>();
                if (foodCollider != null)
                    foodCollider.isTrigger = true;
            }
        }
        if (food != null)
        {
            if (_hunger != null)
                food.TryPickupForPlayer(_hunger);
            return;
        }

        if (hit.collider.CompareTag("Food"))
        {
            Debug.Log("Pointed at food, but the object is missing a FoodPickup component.");
            return;
        }

        IInteractable interactable = hit.collider.GetComponent<IInteractable>()
            ?? hit.collider.GetComponentInParent<IInteractable>()
            ?? hit.collider.GetComponentInChildren<IInteractable>();
        if (interactable != null)
        {
            interactable.OnInteract(gameObject);
            return;
        }

        CraftingTable table = ResolveCraftingTableFromHit(hit);
        if (table != null)
        {
            table.OnInteract(gameObject);
            return;
        }

        Debug.Log("Interact key pressed, but no valid target was under the crosshair.");
    }

    CraftingTable ResolveCraftingTableFromHit(RaycastHit hit)
    {
        if (hit.collider == null)
            return null;

        Transform search = hit.collider.transform;
        while (search != null)
        {
            if (search.GetComponentInParent<controller>() == this)
                break;

            if (LooksLikeCraftingTable(search.name))
            {
                CraftingTable table = search.GetComponent<CraftingTable>();
                if (table == null)
                    table = search.gameObject.AddComponent<CraftingTable>();

                Collider tableCollider = search.GetComponent<Collider>();
                if (tableCollider == null && search.GetComponentInChildren<Collider>() == null)
                {
                    BoxCollider box = search.gameObject.AddComponent<BoxCollider>();
                    box.size = new Vector3(2f, 1f, 1.2f);
                    box.center = new Vector3(0f, 0.5f, 0f);
                }

                return table;
            }

            search = search.parent;
        }

        return null;
    }

    bool LooksLikeCraftingTable(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return false;

        string lower = objectName.ToLowerInvariant();
        return lower.Contains("crafting table")
            || lower.Contains("craftingtable")
            || lower.Contains("craft table")
            || lower.Contains("workbench")
            || lower == "table"
            || lower.Contains(" table");
    }

    bool TryPickupWood(RaycastHit hit)
    {
        WoodPickup wood = hit.collider.GetComponent<WoodPickup>()
            ?? hit.collider.GetComponentInParent<WoodPickup>()
            ?? hit.collider.GetComponentInChildren<WoodPickup>();

        if (wood == null)
        {
            Transform search = hit.collider.transform;
            while (search != null)
            {
                if (search.GetComponentInParent<controller>() == this)
                    break;

                if (IsWoodRoot(search.gameObject))
                {
                    Collider targetCollider = search.GetComponent<Collider>();
                    if (targetCollider != null)
                        targetCollider.isTrigger = false;

                    wood = search.GetComponent<WoodPickup>() ?? search.gameObject.AddComponent<WoodPickup>();
                    wood.logsPerPickup = 1;
                    wood.pickupOnTrigger = false;
                    break;
                }
                search = search.parent;
            }
        }

        if (wood == null)
        {
            Debug.Log("No WoodPickup component found on hit collider.");
            return false;
        }

        PlayerWoodCarry carry = GetComponent<PlayerWoodCarry>();
        if (carry == null || !carry.TryAdd(wood.logsPerPickup))
        {
            Debug.Log("Cannot add wood to carry: carry is null or inventory full.");
            return false;
        }

        if (wood.pickupSound != null)
            AudioSource.PlayClipAtPoint(wood.pickupSound, transform.position);

        GameObject toDestroy = FindWoodRoot(wood.gameObject);
        if (toDestroy == null)
        {
            Debug.LogWarning($"Picked up wood, but no safe wood root was found for '{wood.gameObject.name}'. Skipping destroy.");
        }
        else if (toDestroy == gameObject || toDestroy.GetComponentInParent<controller>() != null)
        {
            Debug.LogWarning($"Picked up wood, but skipping destroy because target appears to be player-related: {toDestroy.name}");
        }
        else
        {
            Debug.Log($"Picked up wood from: {toDestroy.name}");
            Destroy(toDestroy);
        }

        UpdateWoodUI();
        return true;
    }

    GameObject FindWoodRoot(GameObject start)
    {
        if (start == null)
            return null;

        GameObject root = start;
        Transform current = start.transform;
        while (current.parent != null)
        {
            Transform parent = current.parent;
            GameObject parentGo = parent.gameObject;

            if (parentGo == gameObject || parentGo.GetComponentInParent<controller>() != null)
                break;

            if (!IsWoodRoot(parentGo))
                break;

            root = parentGo;
            current = parent;
        }

        return root;
    }

    bool IsWoodRoot(GameObject go)
    {
        if (go == null)
            return false;

        if (go.CompareTag("Wood"))
            return true;

        string name = go.name.ToLower();
        if (name.Contains("log") || name.Contains("wood"))
            return true;

        if (go.GetComponent<WoodPickup>() != null)
            return true;

        return false;
    }

    void DropWoodIfPressed()
    {
        if (!Input.GetKeyDown(dropWoodKey))
            return;

        if (TryDropActiveStoredFood())
            return;

        PlayerWoodCarry carry = GetComponent<PlayerWoodCarry>();
        if (carry == null || carry.Current <= 0)
        {
            Debug.Log("No wood to drop.");
            return;
        }

        if (woodDropPrefab == null)
        {
            Debug.LogWarning("Wood drop prefab not assigned. Cannot drop wood.");
            return;
        }

        Vector3 dropPos = cameraTransform != null
            ? cameraTransform.position + cameraTransform.forward * dropDistance
            : transform.position + transform.forward * dropDistance;

        GameObject dropped = Instantiate(woodDropPrefab, dropPos, Quaternion.identity);

        Collider[] colliders = dropped.GetComponentsInChildren<Collider>();
        foreach (Collider childCollider in colliders)
        {
            childCollider.isTrigger = false;
        }

        Rigidbody[] rigidbodies = dropped.GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody childRb in rigidbodies)
        {
            childRb.isKinematic = false;
            childRb.useGravity = true;
        }

        WoodPickup wp = dropped.GetComponentInChildren<WoodPickup>();
        if (wp == null)
            wp = dropped.AddComponent<WoodPickup>();

        wp.logsPerPickup = 1;
        wp.pickupOnTrigger = false; // Ensure component config matches

        carry.TakeOne();
        UpdateWoodUI();
    }

    bool TryDropActiveStoredFood()
    {
        if (_inventory == null)
            _inventory = GetComponent<PlayerInventory>();

        if (_inventory == null || _inventory.weapons == null || _inventory.weapons.Count == 0)
            return false;

        int index = _inventory.activeIndex;
        if (index < 0 || index >= _inventory.weapons.Count)
            return false;

        Weapon active = _inventory.weapons[index];
        if (active == null)
            return false;

        FoodPickup storedFood = active.GetComponent<FoodPickup>();
        if (storedFood == null || !storedFood.allowInventoryPickup)
            return false;

        Vector3 dropPos = cameraTransform != null
            ? cameraTransform.position + cameraTransform.forward * dropDistance
            : transform.position + transform.forward * dropDistance;
        Vector3 dropForward = cameraTransform != null ? cameraTransform.forward : transform.forward;

        _inventory.RemoveWeaponAt(index);
        storedFood.DropFromInventory(dropPos, dropForward);
        Debug.Log($"Dropped inventory food item '{storedFood.inventoryItemName}'.");
        return true;
    }

    void UpdateWoodUI()
    {
        if (_inventory == null)
            _inventory = GetComponent<PlayerInventory>();

        if (_inventory != null && _inventory.inventoryUI == null)
        {
            InventoryUI foundInventoryUI = FindObjectOfType<InventoryUI>();
            if (foundInventoryUI == null)
            {
                GameObject inventoryUIGO = new GameObject("InventoryUI");
                foundInventoryUI = inventoryUIGO.AddComponent<InventoryUI>();
            }

            _inventory.inventoryUI = foundInventoryUI;
            _inventory.inventoryUI.Initialize(_inventory);
        }

        if (_inventory == null || _inventory.inventoryUI == null)
            return;

        PlayerWoodCarry carry = GetComponent<PlayerWoodCarry>();
        if (carry != null)
            _inventory.inventoryUI.SetWoodCount(carry.Current);
    }

    void OnWoodCarryChanged(int currentWood)
    {
        UpdateWoodUI();
        if (currentWood <= 0)
            HideHeldWood();
    }

    void OnDestroy()
    {
        if (_woodCarry != null)
            _woodCarry.OnWoodChanged -= OnWoodCarryChanged;
    }

    void HandleHeldWoodInput()
    {
        if (WoodSlotKeyPressed())
        {
            PlayerWoodCarry carry = _woodCarry != null ? _woodCarry : GetComponent<PlayerWoodCarry>();
            if (carry != null && carry.Current > 0)
            {
                ShowHeldWood();
                return;
            }
        }

        if (_showHeldWood && AnyNonWoodNumberKeyPressed())
            HideHeldWood();
    }

    bool AnyNonWoodNumberKeyPressed()
    {
        for (int i = 0; i < 10; i++)
        {
            KeyCode key = GetSlotKeyCode(i);
            if (Input.GetKeyDown(key) && !IsWoodSlotIndex(i))
                return true;
        }

        return false;
    }

    bool WoodSlotKeyPressed()
    {
        return (IsWoodSlotIndex(5) && Input.GetKeyDown(KeyCode.Alpha6))
            || (IsWoodSlotIndex(6) && Input.GetKeyDown(KeyCode.Alpha7));
    }

    bool IsWoodSlotIndex(int slotIndex)
    {
        PlayerWoodCarry carry = _woodCarry != null ? _woodCarry : GetComponent<PlayerWoodCarry>();
        if (carry == null || carry.Current <= 0)
            return false;

        if (_inventory == null)
            _inventory = GetComponent<PlayerInventory>();

        bool slotSixOccupied = _inventory != null
            && _inventory.weapons != null
            && _inventory.weapons.Count > 5
            && _inventory.weapons[5] != null;
        int woodSlotIndex = slotSixOccupied ? 6 : 5;
        return slotIndex == woodSlotIndex;
    }

    KeyCode GetSlotKeyCode(int slotIndex)
    {
        return slotIndex == 9 ? KeyCode.Alpha0 : (KeyCode)((int)KeyCode.Alpha1 + slotIndex);
    }

    void ShowHeldWood()
    {
        Transform holdPoint = ResolveWoodHoldPoint();
        if (holdPoint == null)
            return;

        EnsureHeldWoodVisual(holdPoint);
        HideInventoryHeldObjects();

        _heldWoodVisual.transform.SetParent(holdPoint, false);
        _heldWoodVisual.transform.localPosition = heldWoodLocalPosition;
        _heldWoodVisual.transform.localRotation = Quaternion.Euler(heldWoodLocalEuler);
        _heldWoodVisual.transform.localScale = Vector3.one * Mathf.Max(0.01f, heldWoodLocalScale);
        _heldWoodVisual.SetActive(true);
        _showHeldWood = true;
        if (_inventory != null && _inventory.inventoryUI != null)
            _inventory.inventoryUI.SetWoodSelected(true);
    }

    void HideHeldWood()
    {
        _showHeldWood = false;
        if (_heldWoodVisual != null)
            _heldWoodVisual.SetActive(false);
        if (_inventory != null && _inventory.inventoryUI != null)
            _inventory.inventoryUI.SetWoodSelected(false);
    }

    Transform ResolveWoodHoldPoint()
    {
        if (_inventory == null)
            _inventory = GetComponent<PlayerInventory>();

        if (_inventory != null && _inventory.weaponHoldPoint != null)
            return _inventory.weaponHoldPoint;

        if (cameraTransform == null)
            ResolveCamera();
        if (cameraTransform == null)
            return null;

        Transform existing = cameraTransform.Find("WeaponHold");
        if (existing != null)
            return existing;

        GameObject hold = new GameObject("WeaponHold");
        hold.transform.SetParent(cameraTransform, false);
        hold.transform.localPosition = new Vector3(0.35f, -0.22f, 0.48f);
        hold.transform.localRotation = Quaternion.identity;
        if (_inventory != null)
            _inventory.weaponHoldPoint = hold.transform;
        return hold.transform;
    }

    void HideInventoryHeldObjects()
    {
        if (_inventory == null || _inventory.weapons == null)
            return;

        foreach (Weapon weapon in _inventory.weapons)
        {
            if (weapon != null && weapon.gameObject.activeSelf)
                weapon.gameObject.SetActive(false);
        }
    }

    void EnsureHeldWoodVisual(Transform parent)
    {
        if (_heldWoodVisual != null)
            return;

        GameObject prefab = heldWoodPrefab != null ? heldWoodPrefab : woodDropPrefab;
        if (prefab != null)
        {
            _heldWoodVisual = Instantiate(prefab, parent);
            _heldWoodVisual.name = "HeldWoodVisual";
            PrepareHeldWoodPrefabVisual(_heldWoodVisual);
        }
        else
        {
            _heldWoodVisual = new GameObject("HeldWoodVisual");
            _heldWoodVisual.transform.SetParent(parent, false);
            CreateHeldWoodLog("HeldWoodLogA", new Vector3(-0.04f, 0.02f, 0f), Quaternion.Euler(90f, 0f, 82f), new Vector3(0.055f, 0.34f, 0.055f));
            CreateHeldWoodLog("HeldWoodLogB", new Vector3(0.04f, -0.025f, 0.02f), Quaternion.Euler(90f, 0f, 98f), new Vector3(0.052f, 0.32f, 0.052f));
            CreateHeldWoodLog("HeldWoodLogC", new Vector3(0.02f, 0.045f, -0.035f), Quaternion.Euler(90f, 0f, 88f), new Vector3(0.046f, 0.28f, 0.046f));
        }

        _heldWoodVisual.SetActive(false);
    }

    void PrepareHeldWoodPrefabVisual(GameObject visual)
    {
        foreach (Collider col in visual.GetComponentsInChildren<Collider>(true))
        {
            if (col != null)
                col.enabled = false;
        }

        foreach (Rigidbody rb in visual.GetComponentsInChildren<Rigidbody>(true))
        {
            if (rb == null)
                continue;
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.detectCollisions = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        foreach (WoodPickup pickup in visual.GetComponentsInChildren<WoodPickup>(true))
        {
            if (pickup != null)
                pickup.enabled = false;
        }
    }

    void CreateHeldWoodLog(string name, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
    {
        GameObject log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        log.name = name;
        log.transform.SetParent(_heldWoodVisual.transform, false);
        log.transform.localPosition = localPosition;
        log.transform.localRotation = localRotation;
        log.transform.localScale = localScale;

        Collider col = log.GetComponent<Collider>();
        if (col != null)
            col.enabled = false;

        Renderer renderer = log.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(0.36f, 0.19f, 0.075f, 1f);
            renderer.sharedMaterial = mat;
        }
    }

    bool TryConsumeActiveInventoryFood()
    {
        if (!Input.GetKeyDown(interactKey))
            return false;

        if (_inventory == null)
            _inventory = GetComponent<PlayerInventory>();

        if (_inventory == null || _hunger == null)
            return false;

        int index = _inventory.activeIndex;
        if (index < 0 || index >= _inventory.weapons.Count)
            return false;

        Weapon active = _inventory.weapons[index];
        if (active == null)
            return false;

        FoodPickup storedFood = active.GetComponent<FoodPickup>();
        if (storedFood == null || !storedFood.allowInventoryPickup)
            return false;

        if (!storedFood.TryConsumeFromInventory(_hunger, transform.position))
            return false;

        _inventory.RemoveWeaponAt(index);
        Destroy(storedFood.gameObject);
        Debug.Log($"Consumed inventory food item '{storedFood.inventoryItemName}'.");
        return true;
    }

    // Also support trigger pickups (walk over)
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Wood"))
        {
            WoodPickup wood = other.GetComponentInParent<WoodPickup>();
            PlayerWoodCarry carry = GetComponent<PlayerWoodCarry>();
            if (wood != null && wood.pickupOnTrigger && carry != null && carry.TryAdd(wood.logsPerPickup))
            {
                if (wood.pickupSound != null)
                    AudioSource.PlayClipAtPoint(wood.pickupSound, transform.position);

                GameObject destroyTarget = wood.gameObject;
                Transform t = destroyTarget.transform;
                while (t.parent != null && t.parent.GetComponent<WoodPickup>() != null)
                    t = t.parent;

                while (t.parent != null && (t.parent.CompareTag("Wood") || t.parent.name.ToLower().Contains("wood") || t.parent.name.ToLower().Contains("log")))
                    t = t.parent;

                destroyTarget = t.gameObject;

                if (destroyTarget == gameObject || destroyTarget.GetComponentInParent<controller>() != null)
                {
                    Debug.LogWarning($"Skip destroying object '{destroyTarget.name}' because it appears to belong to the player.");
                }
                else
                {
                    Destroy(destroyTarget);
                }

                UpdateWoodUI();
            }

        }
    }

    Camera ResolveCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
            cam = GetComponentInChildren<Camera>(true) ?? transform.root.GetComponentInChildren<Camera>(true);

        if (cam == null)
        {
            Camera[] cameras = FindObjectsOfType<Camera>(true);
            foreach (Camera candidate in cameras)
            {
                if (candidate != null && candidate.enabled)
                {
                    cam = candidate;
                    break;
                }
            }
        }

        if (cam == null)
            return null;

        if (!cam.gameObject.activeInHierarchy)
            cam.gameObject.SetActive(true);

        if (cameraTransform == null || cameraTransform.GetComponent<Camera>() != cam)
            cameraTransform = cam.transform;

        if (!cam.enabled)
            cam.enabled = true;

        if (cam.targetTexture != null)
            cam.targetTexture = null;

        cam.targetDisplay = 0;

        return cam;
    }

    // UI
    void UpdateUI()
    {
        UpdateInventoryActionHint();

        if (staminaUI != null)
        {
            float normalized = (maxStamina > 0f) ? currentStamina / maxStamina : 0f;
            staminaUI.SetStamina(Mathf.Clamp01(normalized));
        }
    }

    void UpdateInventoryActionHint()
    {
        if (_inventory == null)
            _inventory = GetComponent<PlayerInventory>();

        if (_inventory == null || _inventory.inventoryUI == null)
            return;

        string hint = "";
        bool pulse = false;
        int idx = _inventory.activeIndex;
        if (idx >= 0 && idx < _inventory.weapons.Count)
        {
            Weapon active = _inventory.weapons[idx];
            if (active != null)
            {
                FoodPickup fp = active.GetComponent<FoodPickup>();
                if (fp != null && fp.allowInventoryPickup)
                {
                    hint = $"Press {interactKey} to Eat";
                    if (_hunger != null && _hunger.GetHungerNormalized() <= lowHungerHintThreshold)
                        pulse = true;
                }
            }
        }

        _inventory.inventoryUI.SetActionHint(hint);
        _inventory.inventoryUI.SetActionHintPulse(pulse);
    }

    // Settings API
    public void SetMouseSensitivity(float newSens)
    {
        mouseSensitivity = newSens;
        PlayerPrefs.SetFloat("player_sensitivity", mouseSensitivity);
    }

    public float GetStaminaNormalized() => maxStamina > 0f ? currentStamina / maxStamina : 0f;

    public void SetStaminaNormalized(float n)
    {
        n = Mathf.Clamp01(n);
        currentStamina = maxStamina * n;
        UpdateUI();
    }
}
