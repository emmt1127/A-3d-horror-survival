using UnityEngine;

/// <summary>
/// World log pickup. Tag the root or collider "Wood". Player picks up with F (raycast) or optional trigger.
/// </summary>
public class WoodPickup : MonoBehaviour
{
    public int logsPerPickup = 1;
    public bool pickupOnTrigger;
    public AudioClip pickupSound;

    void Reset()
    {
        Collider c = GetComponent<Collider>();
        if (c != null)
            c.isTrigger = pickupOnTrigger;
    }
}
