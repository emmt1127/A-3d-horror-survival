using System;
using UnityEngine;

public class Health : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth = 100f;
    public bool destroyOnDeath = false;
    public GameObject deathPrefab; // optional VFX prefab to spawn on death

    [Header("Player")]
    public bool isPlayer = false; // set true on the player GameObject

    [Header("UI")]
    [Tooltip("Optional; auto-picked from children if empty.")]
    public HealthUI healthUI;

    public float CurrentHealth { get; private set; }

    // Event fired when this entity dies
    public event Action OnDeath;

    void Awake()
    {
        CurrentHealth = maxHealth;
        if (healthUI == null)
        {
            healthUI = GetComponentInChildren<HealthUI>(true)
                ?? transform.root.GetComponentInChildren<HealthUI>(true);
        }

        PushHealthUI();
    }

    void PushHealthUI()
    {
        if (healthUI == null || maxHealth <= 0f)
            return;
        healthUI.SetHealth(CurrentHealth / maxHealth);
    }

    // Public API
    public void TakeDamage(float amount)
    {
        if (amount <= 0f) return;
        CurrentHealth -= amount;
        CurrentHealth = Mathf.Max(0f, CurrentHealth);
        PushHealthUI();

        if (CurrentHealth <= 0f)
            Die();
    }

    public void Heal(float amount)
    {
        if (amount <= 0f) return;
        CurrentHealth += amount;
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth);
        PushHealthUI();
    }

    public void SetHealthFraction(float fraction)
    {
        fraction = Mathf.Clamp01(fraction);
        CurrentHealth = maxHealth * fraction;
        PushHealthUI();
    }

    void Die()
    {
        // invoke death event for other systems
        OnDeath?.Invoke();

        // optional death VFX
        if (deathPrefab != null)
            Instantiate(deathPrefab, transform.position, Quaternion.identity);

        // show game over if this is the player
        if (isPlayer || CompareTag("Player"))
        {
            if (GameOverManager.Instance != null)
                GameOverManager.Instance.ShowGameOver();

            // Disable only the controller input, keep player and camera active
            controller playerCtrl = GetComponent<controller>();
            if (playerCtrl != null)
                playerCtrl.enabled = false;
            
            // Disable this Health component but keep player object active
            enabled = false;
            return;
        }

        if (destroyOnDeath)
            Destroy(gameObject);
        else
            enabled = false;
    }
}

