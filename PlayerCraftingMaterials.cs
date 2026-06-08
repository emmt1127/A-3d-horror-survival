using UnityEngine;

public class PlayerCraftingMaterials : MonoBehaviour
{
    public int Scraps { get; private set; }

    public bool TryAddScraps(int amount)
    {
        if (amount <= 0)
            return false;

        Scraps += amount;
        return true;
    }

    public bool TrySpendScraps(int amount)
    {
        if (amount <= 0)
            return true;

        if (Scraps < amount)
            return false;

        Scraps -= amount;
        return true;
    }

    public void SetScraps(int amount)
    {
        Scraps = Mathf.Max(0, amount);
    }
}
