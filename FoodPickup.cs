using UnityEngine;

/// <summary>
/// Walking into the pickup or pressing E (via controller raycast) restores hunger.
/// Tag "Food" is optional if the object has this component.
/// </summary>
[RequireComponent(typeof(Collider))]
public class FoodPickup : MonoBehaviour
{
    [Tooltip("If <= 1: fraction of max hunger (0.35 = 35%). If > 1: adds this many hunger points (same units as PlayerHunger.maxHunger).")]
    public float hungerRestore = 0.35f;
    public AudioClip pickupSound;

    [Header("Inventory pickup (optional)")]
    [Tooltip("When true, this food can be stored in a PlayerInventory slot by pressing F.")]
    public bool allowInventoryPickup = false;
    [Tooltip("Displayed item name when stored in inventory.")]
    public string inventoryItemName = "Meat";
    [Tooltip("Optional icon shown in inventory slot.")]
    public Sprite inventoryIcon;
    [Tooltip("Push force applied when dropped from inventory.")]
    public float dropForce = 2f;
    [Header("Grounding safety")]
    [Tooltip("Helps prevent food from clipping through terrain/chunks when first spawned.")]
    public LayerMask groundMask = ~0;
    public float groundProbeUp = 3f;
    public float groundProbeDown = 30f;
    public float groundOffset = 0.08f;

    bool _isStoredInInventory;
    static Sprite _generatedFoodIcon;
    const string MeatVisualRootName = "GeneratedMeatVisual";

    void Reset()
    {
        ConfigurePhysicsForCurrentMode();
        EnsureFoodVisuals();
    }

    void Awake()
    {
        ConfigurePhysicsForCurrentMode();
        EnsureFoodVisuals();
    }

    void OnValidate()
    {
        ConfigurePhysicsForCurrentMode();
        EnsureFoodVisuals();
    }

    void OnTriggerEnter(Collider other)
    {
        if (allowInventoryPickup)
            return;
        TryPickupFromCollider(other);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (allowInventoryPickup)
            return;
        if (collision.collider != null)
            TryPickupFromCollider(collision.collider);
    }

    /// <summary>Used by player controller raycast (E key).</summary>
    public bool TryPickupForPlayer(PlayerHunger hunger)
    {
        if (hunger == null)
            return false;
        ApplyEat(hunger);
        if (pickupSound != null)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);
        Destroy(gameObject);
        return true;
    }

    /// <summary>Consumes this stored inventory food and applies hunger restore.</summary>
    public bool TryConsumeFromInventory(PlayerHunger hunger, Vector3 soundPosition)
    {
        if (!allowInventoryPickup || hunger == null)
            return false;

        ApplyEat(hunger);
        if (pickupSound != null)
            AudioSource.PlayClipAtPoint(pickupSound, soundPosition);
        return true;
    }

    /// <summary>Stores this food in an inventory slot (used for bunny meat).</summary>
    public bool TryStoreInInventory(PlayerInventory inv)
    {
        if (!allowInventoryPickup || inv == null)
            return false;

        Weapon weaponItem = GetComponent<Weapon>();
        if (weaponItem == null)
            weaponItem = gameObject.AddComponent<Weapon>();

        weaponItem.weaponName = string.IsNullOrWhiteSpace(inventoryItemName) ? "Meat" : inventoryItemName;
        weaponItem.icon = inventoryIcon != null ? inventoryIcon : GetDefaultFoodIcon();
        weaponItem.damage = 0f;
        weaponItem.range = 0f;
        weaponItem.isMelee = false;
        weaponItem.meleeRadius = 0.05f;

        if (!inv.AddWeapon(weaponItem))
            return false;

        foreach (Collider c in GetComponentsInChildren<Collider>())
        {
            if (c != null)
                c.enabled = false;
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.detectCollisions = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        _isStoredInInventory = true;
        transform.SetParent(inv.transform, false);
        gameObject.SetActive(false);
        return true;
    }

    /// <summary>Drops this stored food item back into the world.</summary>
    public void DropFromInventory(Vector3 worldPosition, Vector3 forwardDirection)
    {
        _isStoredInInventory = false;
        transform.SetParent(null, true);
        transform.position = worldPosition;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        gameObject.SetActive(true);

        foreach (Collider c in GetComponentsInChildren<Collider>())
        {
            if (c != null)
                c.enabled = true;
        }

        Collider ownCollider = GetComponent<Collider>();
        if (ownCollider != null)
            ownCollider.isTrigger = false;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.detectCollisions = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        SnapToGround();
        rb.AddForce((forwardDirection.normalized + Vector3.up * 0.25f) * dropForce, ForceMode.Impulse);
    }

    public void SetInventoryPickupMode(bool enabled)
    {
        allowInventoryPickup = enabled;
        ConfigurePhysicsForCurrentMode();
    }

    void ConfigurePhysicsForCurrentMode()
    {
        Collider c = GetComponent<Collider>();
        Rigidbody rb = GetComponent<Rigidbody>();

        if (_isStoredInInventory)
        {
            if (c != null)
                c.enabled = false;

            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.detectCollisions = false;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                rb.interpolation = RigidbodyInterpolation.None;
            }
            return;
        }

        if (allowInventoryPickup)
        {
            if (c != null)
                c.isTrigger = false;

            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.detectCollisions = true;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            }
            SnapToGround();
            return;
        }

        if (c != null)
            c.isTrigger = true;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.detectCollisions = false;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb.interpolation = RigidbodyInterpolation.None;
        }
    }

    void SnapToGround()
    {
        Vector3 start = transform.position + Vector3.up * groundProbeUp;
        if (!Physics.Raycast(start, Vector3.down, out RaycastHit hit, groundProbeUp + groundProbeDown, groundMask, QueryTriggerInteraction.Ignore))
            return;

        transform.position = hit.point + Vector3.up * groundOffset;
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null && rb.velocity.y < 0f)
            rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
    }

    void TryPickupFromCollider(Collider other)
    {
        if (other == null)
            return;

        PlayerHunger hunger = other.GetComponentInParent<PlayerHunger>()
            ?? other.GetComponent<PlayerHunger>();

        if (hunger == null)
            return;

        ApplyEat(hunger);
        if (pickupSound != null)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);
        Destroy(gameObject);
    }

    void ApplyEat(PlayerHunger hunger)
    {
        hunger.Eat(hungerRestore);
    }

    void EnsureFoodVisuals()
    {
        HideOriginalRenderers();

        Transform root = transform.Find(MeatVisualRootName);
        if (root == null)
        {
            GameObject rootGo = new GameObject(MeatVisualRootName);
            rootGo.transform.SetParent(transform, false);
            root = rootGo.transform;
        }

        SetVisualPart(root, "MeatChunk", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.26f, 0.17f, 0.19f), Quaternion.Euler(0f, 0f, -18f), new Color(0.58f, 0.08f, 0.055f, 1f));
        SetVisualPart(root, "MeatHighlight", PrimitiveType.Sphere, new Vector3(-0.035f, 0.035f, 0.035f), new Vector3(0.18f, 0.08f, 0.1f), Quaternion.Euler(0f, 0f, -20f), new Color(0.86f, 0.2f, 0.14f, 1f));
        SetVisualPart(root, "FatStripe", PrimitiveType.Capsule, new Vector3(-0.035f, 0.08f, 0f), new Vector3(0.032f, 0.16f, 0.032f), Quaternion.Euler(0f, 0f, 78f), new Color(1f, 0.78f, 0.56f, 1f));
        SetVisualPart(root, "Bone", PrimitiveType.Cylinder, new Vector3(0.22f, 0f, 0f), new Vector3(0.045f, 0.18f, 0.045f), Quaternion.Euler(0f, 0f, 90f), new Color(0.88f, 0.78f, 0.6f, 1f));
        SetVisualPart(root, "BoneEndA", PrimitiveType.Sphere, new Vector3(0.38f, 0.035f, 0f), new Vector3(0.09f, 0.075f, 0.075f), Quaternion.identity, new Color(0.9f, 0.8f, 0.62f, 1f));
        SetVisualPart(root, "BoneEndB", PrimitiveType.Sphere, new Vector3(0.39f, -0.04f, 0f), new Vector3(0.075f, 0.065f, 0.065f), Quaternion.identity, new Color(0.84f, 0.74f, 0.56f, 1f));
    }

    void HideOriginalRenderers()
    {
        Transform visualRoot = transform.Find(MeatVisualRootName);
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null)
                continue;
            if (visualRoot != null && renderer.transform.IsChildOf(visualRoot))
                continue;

            renderer.enabled = false;
        }
    }

    void SetVisualPart(Transform root, string name, PrimitiveType primitive, Vector3 position, Vector3 scale, Quaternion rotation, Color color)
    {
        Transform part = root.Find(name);
        if (part == null)
        {
            GameObject go = GameObject.CreatePrimitive(primitive);
            go.name = name;
            go.transform.SetParent(root, false);
            part = go.transform;
        }

        part.localPosition = position;
        part.localRotation = rotation;
        part.localScale = scale;

        Collider col = part.GetComponent<Collider>();
        if (col != null)
            col.enabled = false;

        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer == null)
            return;

        renderer.enabled = true;
        Material mat = renderer.sharedMaterial;
        if (mat == null || mat.name == "Default-Material")
            mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        renderer.sharedMaterial = mat;
    }

    static Sprite GetDefaultFoodIcon()
    {
        if (_generatedFoodIcon != null)
            return _generatedFoodIcon;

        Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        texture.name = "GeneratedFoodIcon";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
                texture.SetPixel(x, y, clear);
        }

        DrawMeatIcon(texture);
        texture.Apply();

        _generatedFoodIcon = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 64f);
        _generatedFoodIcon.name = "Food Meat Icon";
        return _generatedFoodIcon;
    }

    static void DrawMeatIcon(Texture2D texture)
    {
        Color shadow = new Color(0.12f, 0.025f, 0.018f, 0.45f);
        Color crust = new Color(0.28f, 0.075f, 0.045f, 1f);
        Color meatDark = new Color(0.48f, 0.085f, 0.07f, 1f);
        Color meatMid = new Color(0.72f, 0.16f, 0.12f, 1f);
        Color meatLight = new Color(0.96f, 0.34f, 0.24f, 1f);
        Color fat = new Color(1f, 0.79f, 0.58f, 1f);
        Color bone = new Color(0.92f, 0.82f, 0.65f, 1f);
        Color boneShade = new Color(0.72f, 0.62f, 0.48f, 1f);

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                float nx = (x + 0.5f) / texture.width;
                float ny = (y + 0.5f) / texture.height;

                float boneA = Ellipse(nx, ny, 0.68f, 0.56f, 0.26f, 0.08f, -0.45f);
                float knobA = Ellipse(nx, ny, 0.82f, 0.65f, 0.09f, 0.075f, -0.45f);
                float knobB = Ellipse(nx, ny, 0.86f, 0.53f, 0.075f, 0.065f, -0.45f);
                float boneMask = Mathf.Max(boneA, Mathf.Max(knobA, knobB));
                if (boneMask > 0f)
                    BlendPixel(texture, x, y, Color.Lerp(boneShade, bone, boneMask), boneMask);

                float meatMask = Ellipse(nx, ny, 0.38f, 0.42f, 0.33f, 0.24f, -0.45f);
                if (meatMask > 0f)
                {
                    float shade = Mathf.Clamp01((ny - 0.18f) * 1.55f + meatMask * 0.35f);
                    Color color = Color.Lerp(meatDark, meatMid, shade);
                    if (ny > 0.49f)
                        color = Color.Lerp(color, meatLight, 0.5f);
                    if (meatMask < 0.22f)
                        color = Color.Lerp(crust, color, meatMask * 2.8f);

                    BlendPixel(texture, x, y, color, Mathf.Clamp01(meatMask * 1.35f));
                }

                float inner = Ellipse(nx, ny, 0.4f, 0.43f, 0.18f, 0.115f, -0.45f);
                if (inner > 0f)
                    BlendPixel(texture, x, y, Color.Lerp(meatMid, meatLight, inner), inner * 0.7f);

                float fatLine = Ellipse(nx, ny, 0.3f, 0.5f, 0.17f, 0.025f, -0.45f);
                if (fatLine > 0f)
                    BlendPixel(texture, x, y, fat, fatLine * 0.9f);

                float fatSpot = Ellipse(nx, ny, 0.46f, 0.33f, 0.06f, 0.035f, -0.45f);
                if (fatSpot > 0f)
                    BlendPixel(texture, x, y, fat, fatSpot * 0.85f);

                float shadowMask = Ellipse(nx, ny, 0.42f, 0.31f, 0.36f, 0.09f, -0.25f);
                if (shadowMask > 0f && ny < 0.34f)
                    BlendPixel(texture, x, y, shadow, shadowMask * 0.45f);
            }
        }
    }

    static float Ellipse(float x, float y, float cx, float cy, float rx, float ry, float rotation)
    {
        float dx = x - cx;
        float dy = y - cy;
        float cos = Mathf.Cos(rotation);
        float sin = Mathf.Sin(rotation);
        float u = dx * cos + dy * sin;
        float v = -dx * sin + dy * cos;
        float d = (u * u) / (rx * rx) + (v * v) / (ry * ry);
        return Mathf.Clamp01(1f - d);
    }

    static void BlendPixel(Texture2D texture, int x, int y, Color color, float alpha)
    {
        alpha = Mathf.Clamp01(alpha);
        if (alpha <= 0f)
            return;

        Color existing = texture.GetPixel(x, y);
        float outAlpha = alpha + existing.a * (1f - alpha);
        if (outAlpha <= 0f)
            return;

        float existingFactor = existing.a * (1f - alpha);
        Color blended = new Color(
            (color.r * alpha + existing.r * existingFactor) / outAlpha,
            (color.g * alpha + existing.g * existingFactor) / outAlpha,
            (color.b * alpha + existing.b * existingFactor) / outAlpha,
            outAlpha);
        texture.SetPixel(x, y, blended);
    }
}
