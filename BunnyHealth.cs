using UnityEngine;

/// <summary>
/// Bunny HP as weapon hits (default 3). Drops food on death. Works with PlayerInventory raycasts.
/// </summary>
public class BunnyHealth : MonoBehaviour
{
    [Header("Hits")]
    public int hitsToKill = 3;

    [Header("Loot")]
    public GameObject foodPrefab;
    public int meatCount = 1;
    public float spawnOffset = 0.5f;

    int _hitsTaken;
    bool _dying;

    public void RegisterWeaponHit(Vector3 hitPoint)
    {
        if (_hitsTaken >= hitsToKill || _dying)
            return;

        Vector3 outward = hitPoint - transform.position;
        BunnyVfx.PlayHit(hitPoint, outward);

        _hitsTaken++;

        BunnyAI ai = GetComponent<BunnyAI>();
        if (ai != null)
            ai.FleeFromHit(hitPoint);

        if (_hitsTaken >= hitsToKill)
            Die();
    }

    public void KillInstantly(Vector3 hitPoint)
    {
        if (_dying)
            return;

        Vector3 outward = hitPoint - transform.position;
        BunnyVfx.PlayHit(hitPoint, outward);
        _hitsTaken = Mathf.Max(hitsToKill, 1);
        Die();
    }

    /// <summary>Legacy / testing: counts as one weapon hit.</summary>
    public void TakeDamage(float amount)
    {
        if (amount <= 0f)
            return;
        RegisterWeaponHit(transform.position + transform.forward);
    }

    void Die()
    {
        if (_dying)
            return;
        _dying = true;

        BunnyVfx.PlayDeath(transform.position);
        PlayerInventory inventory = ResolvePlayerInventory();

        for (int i = 0; i < meatCount; i++)
        {
            if (foodPrefab != null)
            {
                Vector3 pos = transform.position + new Vector3(Random.Range(-0.3f, 0.3f), spawnOffset, Random.Range(-0.3f, 0.3f));
                GameObject meat = Instantiate(foodPrefab, pos, Quaternion.identity);
                FoodPickup pickup = meat.GetComponent<FoodPickup>() ?? meat.AddComponent<FoodPickup>();
                pickup.SetInventoryPickupMode(true);
                if (string.IsNullOrWhiteSpace(pickup.inventoryItemName))
                    pickup.inventoryItemName = "Meat";

                if (inventory != null && pickup.TryStoreInInventory(inventory))
                    continue;

                DropOverflowMeatInFrontOfPlayer(pickup, inventory);
            }
        }

        foreach (Renderer r in GetComponentsInChildren<Renderer>())
            r.enabled = false;
        foreach (Collider c in GetComponentsInChildren<Collider>())
            c.enabled = false;

        BunnyAI ai = GetComponent<BunnyAI>();
        if (ai != null)
            ai.enabled = false;

        Destroy(gameObject, 0.5f);
    }

    PlayerInventory ResolvePlayerInventory()
    {
        if (Camera.main != null)
        {
            PlayerInventory inventory = Camera.main.transform.root.GetComponentInChildren<PlayerInventory>();
            if (inventory != null)
                return inventory;
        }

        controller player = FindObjectOfType<controller>();
        if (player != null)
            return player.GetComponent<PlayerInventory>() ?? player.GetComponentInChildren<PlayerInventory>();

        return FindObjectOfType<PlayerInventory>();
    }

    void DropOverflowMeatInFrontOfPlayer(FoodPickup pickup, PlayerInventory inventory)
    {
        if (pickup == null)
            return;

        Transform dropSource = null;
        if (inventory != null && inventory.playerCamera != null)
            dropSource = inventory.playerCamera.transform;
        else if (Camera.main != null)
            dropSource = Camera.main.transform;

        Vector3 forward = dropSource != null ? dropSource.forward : transform.forward;
        Vector3 position = dropSource != null
            ? dropSource.position + forward * 1.25f + Vector3.up * -0.1f
            : transform.position + forward * 0.8f + Vector3.up * spawnOffset;

        pickup.DropFromInventory(position, forward);
    }

    void OnMouseDown()
    {
        if (!PlayerHasWeapon())
            return;

        RegisterWeaponHit(Camera.main != null ? Camera.main.transform.position : transform.position);
    }

    bool PlayerHasWeapon()
    {
        if (Camera.main == null)
            return false;

        PlayerInventory inv = Camera.main.transform.root.GetComponentInChildren<PlayerInventory>();
        return inv != null && inv.GetActiveWeapon() != null;
    }
}
