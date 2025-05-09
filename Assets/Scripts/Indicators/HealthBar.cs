using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [SerializeField] private Health healthComponent;
    [SerializeField] private Canvas canvas;
    [SerializeField] private Image fillImage;
    [SerializeField] private Camera targetCamera;

    private void Start()
    {
        if (healthComponent == null)
        {
            healthComponent = GetComponentInParent<Health>();
            if (healthComponent == null)
            {
                Debug.LogError("Health component not found in parent of " + gameObject.name);
                return;
            }
        }

        if (canvas == null)
        {
            canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("Canvas component not found on " + gameObject.name);
                return;
            }
        }

        if (fillImage == null)
        {
            Debug.LogError("Fill Image reference not set on " + gameObject.name);
            return;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                Debug.LogError("No camera assigned to HealthBar and Camera.main is null");
                return;
            }
        }

        // Subscribe to health changes
        healthComponent.OnHealthChanged += UpdateHealthBar;
        UpdateHealthBar(healthComponent.CurrentHealth, healthComponent.MaxHealth);
    }

    void UpdateHealthBar(int currentHealth, int maxHealth)
    {
        if (fillImage == null) return;

        float healthPercentage = (float)currentHealth / maxHealth;
        fillImage.fillAmount = healthPercentage;
    }

    void LateUpdate()
    {
        // Keep the health bar facing the camera
        if (targetCamera != null)
        {
            transform.rotation = targetCamera.transform.rotation;
        }
    }

    void OnDestroy()
    {
        if (healthComponent != null)
        {
            healthComponent.OnHealthChanged -= UpdateHealthBar;
        }
    }
}

