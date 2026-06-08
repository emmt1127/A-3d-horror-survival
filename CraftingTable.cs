using UnityEngine;

public class CraftingTable : MonoBehaviour, IInteractable
{
    [Header("Crafting Table")]
    public string displayName = "Crafting Table";
    public KeyCode closeKey = KeyCode.E;

    public void OnInteract(GameObject interactor)
    {
        CraftingTableUI.Toggle(this, interactor);
    }
}
