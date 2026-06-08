using UnityEngine;

[RequireComponent(typeof(Collider))]
public class WeaponPickup : MonoBehaviour
{
    public Weapon weaponData;
    public bool useTriggerPickup = true;

    void Reset()
    {
        weaponData = GetComponent<Weapon>();
        Collider c = GetComponent<Collider>();
        if (c != null)
            c.isTrigger = true;
    }

    /// <summary>
    /// Adds the weapon to inventory and disables pickup colliders without destroying the weapon model.
    /// </summary>
    public bool TryPickupInto(PlayerInventory inv)
    {
        if (inv == null || weaponData == null)
            return false;
        GunWeapon gun = weaponData.GetComponent<GunWeapon>();
        if (gun != null)
            gun.PrepareWeaponData();

        if (!inv.AddWeapon(weaponData))
            return false;

        if (gun != null)
            gun.Initialize(inv.playerCamera, weaponData, inv.transform.root);

        foreach (Collider col in GetComponentsInChildren<Collider>())
            Destroy(col);

        foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>())
            Destroy(rb);

        Destroy(this);
        transform.SetParent(inv.transform);
        gameObject.SetActive(false);
        return true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!useTriggerPickup)
            return;

        PlayerInventory inv = other.GetComponent<PlayerInventory>();
        if (inv != null)
            TryPickupInto(inv);
    }
}
