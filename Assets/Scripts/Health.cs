using UnityEngine;
using System;

public class Health : MonoBehaviour
{
    [SerializeField] private int maxHealth = 100;
    public int MaxHealth => maxHealth;
    [SerializeField] private int currentHealth = 100;
    public int CurrentHealth => currentHealth;

    // Event to notify when health changes
    public event Action<int, int> OnHealthChanged;
    public event Action OnHealthDepleted;

    private void OnValidate()
    {
        if (maxHealth < 0) maxHealth = 0;
        if (currentHealth < 0) currentHealth = 0;
        // if (currentHealth > maxHealth) currentHealth = maxHealth;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Start()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void SetHealth(int health)
    {
        currentHealth = health;
        if (currentHealth > maxHealth) currentHealth = maxHealth;
        if (currentHealth < 0) currentHealth = 0;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void SetMaxHealth(int health)
    {
        maxHealth = health;
        if (currentHealth > maxHealth) currentHealth = maxHealth;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        bool healthDepleted = false;
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            healthDepleted = true;
        }

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (healthDepleted)
        {
            OnHealthDepleted?.Invoke();
        }
    }

    public void Heal(int amount)
    {
        currentHealth += amount;
        if (currentHealth > maxHealth) currentHealth = maxHealth;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
}