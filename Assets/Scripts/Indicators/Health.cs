using UnityEngine;
using System;
using Unity.Netcode;

public class Health : NetworkBehaviour
{
    // TODO: NetworkVariables in Inspector ReadOnly Serialized??
    [SerializeField] private int startingMaxHealth = 100;
    public NetworkVariable<int> maxHealth = new NetworkVariable<int>(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public int MaxHealth => maxHealth.Value;

    [SerializeField] private int startingHealth = 100;
    public NetworkVariable<int> currentHealth = new NetworkVariable<int>(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public int CurrentHealth => currentHealth.Value;

    public bool IsAlive => currentHealth.Value > 0;

    /// <summary>
    /// Event triggered when health changes. (updatedHealth, maxHealth)
    /// </summary>
    public event Action<int, int> OnHealthChanged;
    public event Action OnHealthDepleted;

    private void OnValidate()
    {
        if (startingMaxHealth < 0) startingMaxHealth = 0;
        startingHealth = Mathf.Clamp(startingHealth, 0, startingMaxHealth);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            maxHealth.Value = startingMaxHealth;
            currentHealth.Value = startingHealth;
        }

        currentHealth.OnValueChanged += HandleHealthChanged;
        maxHealth.OnValueChanged += HandleMaxHealthChanged;

        HandleHealthChanged(currentHealth.Value, currentHealth.Value);
    }

    public override void OnNetworkDespawn()
    {
        currentHealth.OnValueChanged -= HandleHealthChanged;
        maxHealth.OnValueChanged -= HandleMaxHealthChanged;
    }

    // Handles changes from the currentHealth NetworkVariable
    private void HandleHealthChanged(int previousValue, int newValue)
    {
        // Determine alive status before and after
        bool wasAlive = previousValue > 0;
        bool isNowAlive = newValue > 0;

        // Invoke local event with the new value and the current synchronized maxHealth
        OnHealthChanged?.Invoke(newValue, maxHealth.Value);

        // Trigger OnHealthDepleted only when transitioning from alive to not alive
        if (wasAlive && !isNowAlive)
        {
            OnHealthDepleted?.Invoke();
        }
        // Optional: Add OnRevived logic here if needed in the future
        // else if (!wasAlive && isNowAlive) { /* Invoke OnRevived */ }
    }

    // Handles changes from the maxHealth NetworkVariable
    private void HandleMaxHealthChanged(int previousValue, int newValue)
    {
        OnHealthChanged?.Invoke(currentHealth.Value, newValue);
    }

    // Server authoritative method to set health directly
    public void SetHealth(int health)
    {
        if (!IsServer) return;
        currentHealth.Value = Mathf.Clamp(health, 0, maxHealth.Value);
    }

    // Server authoritative method to change max health
    public void SetMaxHealth(int newMaxHealth)
    {
        if (!IsServer) return;

        int clampedNewMax = Mathf.Max(0, newMaxHealth);
        if (maxHealth.Value == clampedNewMax) return;

        maxHealth.Value = clampedNewMax;

        int clampedCurrentHealth = Mathf.Clamp(currentHealth.Value, 0, maxHealth.Value);
        if (currentHealth.Value != clampedCurrentHealth)
        {
            currentHealth.Value = clampedCurrentHealth;
        }
    }

    // Server authoritative method to apply damage
    public void TakeDamage(int damage)
    {
        // Only server applies damage, and only if the object is currently alive
        if (!IsServer || !IsAlive) return;
        if (damage <= 0) return;

        currentHealth.Value = Mathf.Max(0, currentHealth.Value - damage);
    }

    // Server authoritative method to apply healing
    public void Heal(int amount)
    {
        // Only server applies healing, and only if the object is currently alive
        // Note: You might allow healing a dead object if reviving is intended,
        // but typically healing implies the target is already alive.
        if (!IsServer || !IsAlive) return;
        if (amount <= 0) return;

        currentHealth.Value = Mathf.Min(currentHealth.Value + amount, maxHealth.Value);
    }
}