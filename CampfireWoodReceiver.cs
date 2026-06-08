using UnityEngine;

[RequireComponent(typeof(Collider))]
/// <summary>
/// Put on your fire pit. Player presses E (IInteractable) to dump all carried wood into the fire.
/// Also accepts dropped logs that touch the receiver trigger.
/// </summary>
public class CampfireWoodReceiver : MonoBehaviour, IInteractable
{
    [Tooltip("Abstract fuel added per log (hook up to your fire script later).")]
    public int fuelUnitsPerLog = 10;

    public bool acceptDroppedLogs = true;

    public int StoredFuel { get; private set; }

    Campfire _campfire;

    void Start()
    {
        _campfire = GetComponentInParent<Campfire>();
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    public void OnInteract(GameObject interactor)
    {
        if (interactor == null)
            return;

        PlayerWoodCarry carry = interactor.GetComponent<PlayerWoodCarry>();
        if (carry == null || carry.Current <= 0)
            return;

        int logs = carry.TakeAll();
        StoredFuel += logs * fuelUnitsPerLog;

        if (_campfire != null)
            _campfire.AddWood(logs);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!acceptDroppedLogs)
            return;

        if (other == null)
            return;

        GameObject root = FindWoodRoot(other.gameObject);
        if (!root.CompareTag("Wood"))
            return;

        WoodPickup wood = other.GetComponent<WoodPickup>()
            ?? other.GetComponentInParent<WoodPickup>()
            ?? other.GetComponentInChildren<WoodPickup>();

        if (wood == null)
            return;

        int logs = wood.logsPerPickup;
        StoredFuel += logs * fuelUnitsPerLog;
        if (_campfire != null)
            _campfire.AddWood(logs);

        Destroy(wood.gameObject);
    }

    GameObject FindWoodRoot(GameObject obj)
    {
        Transform t = obj.transform;
        while (t.parent != null && (t.parent.CompareTag("Wood") || t.parent.name.ToLower().Contains("wood") || t.parent.name.ToLower().Contains("log")))
        {
            t = t.parent;
        }
        return t.gameObject;
    }
}
