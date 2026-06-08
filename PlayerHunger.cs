using UnityEngine;

public class PlayerHunger : MonoBehaviour
{
    [Header("Hunger")]
    public float maxHunger = 100f;
    public float hungerDrainPerSecond = 1f;
    public float foodRestoreAmount = 35f; // in same units as maxHunger
    public bool takeDamageWhenStarving = true;
    public float starvationDamagePerSecond = 5f;

    [Header("UI")]
    public HungerUI hungerUI; // assign HungerUI component

    [Header("Health link")]
    [Tooltip("Leave empty to auto-find Health on this object, parent, or children (including inactive).")]
    public Health healthOverride;

    float currentHunger;
    Health _resolvedHealth;
    bool _loggedMissingHealth;

    void Awake()
    {
        ResolveHealthReference();
        ResolveHungerUI();
        currentHunger = maxHunger;
    }

    void Start()
    {
        UpdateUI();
    }

    void ResolveHungerUI()
    {
        if (hungerUI != null)
            return;

        hungerUI = GetComponentInChildren<HungerUI>(true)
            ?? GetComponentInParent<HungerUI>()
            ?? transform.root.GetComponentInChildren<HungerUI>(true);
    }

    void Update()
    {
        currentHunger -= hungerDrainPerSecond * Time.deltaTime;
        currentHunger = Mathf.Clamp(currentHunger, 0f, maxHunger);

        if (currentHunger <= 0f && takeDamageWhenStarving)
        {
            if (_resolvedHealth == null || !_resolvedHealth.gameObject.activeInHierarchy)
                ResolveHealthReference();

            if (_resolvedHealth != null)
            {
                float dmg = starvationDamagePerSecond * Time.deltaTime;
                if (dmg > 0f)
                    _resolvedHealth.TakeDamage(dmg);
            }
            else if (!_loggedMissingHealth)
            {
                _loggedMissingHealth = true;
                Debug.LogWarning(
                    "PlayerHunger: No Health found while starving. Add Health to the player (or parent/child), or assign Health Override.",
                    this);
            }
        }

        UpdateUI();
    }

    void ResolveHealthReference()
    {
        if (healthOverride != null)
        {
            _resolvedHealth = healthOverride;
            return;
        }

        // Same object / ancestors / own descendants, then any Health under the same root (siblings).
        _resolvedHealth = GetComponent<Health>()
            ?? GetComponentInParent<Health>()
            ?? GetComponentInChildren<Health>(true)
            ?? transform.root.GetComponentInChildren<Health>(true);
    }

    public void Eat(float fractionOrAmount)
    {
        if (fractionOrAmount <= 0f)
            return;

        // 0 < value <= 1  => fraction of max (e.g. 0.35 = 35% of bar)
        // value > 1       => add that many hunger units (same as maxHunger scale)
        if (fractionOrAmount <= 1f)
            currentHunger += fractionOrAmount * maxHunger;
        else
            currentHunger += fractionOrAmount;

        currentHunger = Mathf.Clamp(currentHunger, 0f, maxHunger);
        UpdateUI();
    }

    public void EatAbsolute(float amount)
    {
        currentHunger = Mathf.Clamp(currentHunger + amount, 0f, maxHunger);
        UpdateUI();
    }

    void UpdateUI()
    {
        if (hungerUI != null)
            hungerUI.SetHunger(currentHunger / maxHunger);
    }

    // Expose for other scripts
    public float GetHungerNormalized() => maxHunger > 0f ? currentHunger / maxHunger : 0f;

    public void SetHungerNormalized(float normalized)
    {
        normalized = Mathf.Clamp01(normalized);
        currentHunger = maxHunger * normalized;
        UpdateUI();
    }
}


