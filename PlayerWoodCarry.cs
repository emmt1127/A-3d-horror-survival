using UnityEngine;

/// <summary>
/// Holds logs for the campfire. Sack upgrades increase the carry limit over time.
/// </summary>
public class PlayerWoodCarry : MonoBehaviour
{
    public int maxCarry = 5;
    public int sackLevel = 1;
    public int[] sackLevelCapacities = { 5, 10, 20, 35, 50 };
    public int[] sackUpgradeCosts = { 5, 10, 20, 35 };

    public int Current { get; private set; }
    public int SackLevel => Mathf.Clamp(sackLevel, 1, MaxSackLevel);
    public int MaxSackLevel => sackLevelCapacities != null && sackLevelCapacities.Length > 0 ? sackLevelCapacities.Length : 1;
    public bool IsSackMaxLevel => SackLevel >= MaxSackLevel;
    public int NextSackUpgradeCost => IsSackMaxLevel ? 0 : GetUpgradeCostForLevel(SackLevel + 1);
    public event System.Action<int> OnWoodChanged;

    void Awake()
    {
        ApplySackLevel(false);
    }

    public bool TryAdd(int amount)
    {
        if (amount <= 0)
            return false;
        if (Current >= maxCarry)
            return false;

        int space = maxCarry - Current;
        int take = Mathf.Min(space, amount);
        Current += take;
        NotifyWoodChanged();
        return take > 0;
    }

    /// <summary>Used when adding to fire: empties hands into the fire.</summary>
    public int TakeAll()
    {
        int n = Current;
        Current = 0;
        NotifyWoodChanged();
        return n;
    }

    /// <summary>Remove one log from carry and return if successful.</summary>
    public bool TakeOne()
    {
        if (Current <= 0)
            return false;
        Current--;
        NotifyWoodChanged();
        return true;
    }

    public bool TrySpendWood(int amount)
    {
        if (amount <= 0)
            return true;
        if (Current < amount)
            return false;

        Current -= amount;
        NotifyWoodChanged();
        return true;
    }

    public bool TryUpgradeSack()
    {
        if (IsSackMaxLevel)
            return false;

        int cost = NextSackUpgradeCost;
        if (!TrySpendWood(cost))
            return false;

        sackLevel = Mathf.Min(MaxSackLevel, SackLevel + 1);
        ApplySackLevel(true);
        return true;
    }

    public void SetSackLevel(int level)
    {
        sackLevel = Mathf.Clamp(level, 1, MaxSackLevel);
        ApplySackLevel(true);
    }

    public void SetCarriedWood(int amount)
    {
        ApplySackLevel(false);
        Current = Mathf.Clamp(amount, 0, maxCarry);
        NotifyWoodChanged();
    }

    int GetUpgradeCostForLevel(int targetLevel)
    {
        if (sackUpgradeCosts == null || sackUpgradeCosts.Length == 0)
            return 0;

        int index = Mathf.Clamp(targetLevel - 2, 0, sackUpgradeCosts.Length - 1);
        return Mathf.Max(0, sackUpgradeCosts[index]);
    }

    void ApplySackLevel(bool notify)
    {
        if (sackLevelCapacities == null || sackLevelCapacities.Length == 0)
            sackLevelCapacities = new[] { Mathf.Max(1, maxCarry) };

        sackLevel = Mathf.Clamp(sackLevel, 1, MaxSackLevel);
        int capacityIndex = Mathf.Clamp(sackLevel - 1, 0, sackLevelCapacities.Length - 1);
        maxCarry = Mathf.Max(1, sackLevelCapacities[capacityIndex]);
        Current = Mathf.Clamp(Current, 0, maxCarry);
        if (notify)
            NotifyWoodChanged();
    }

    void NotifyWoodChanged()
    {
        OnWoodChanged?.Invoke(Current);
    }
}
