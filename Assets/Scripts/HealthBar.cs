using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Health healthComponent;
    [SerializeField] public Vector3 offset = new Vector3(0, 1.5f, 0);
    [SerializeField] public Camera mainCamera;
    public bool managePosition = true;

    private void Awake()
    {
        // Debug.Log("HealthBar Awake called");
    }

    private void Start()
    {
        Debug.Log("HealthBar Start called");

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        healthSlider = GetComponentInChildren<Slider>();
        if (healthSlider == null)
        {
            Debug.LogError("Health slider not found in children of " + gameObject.name);
        }

        healthComponent = GetComponentInParent<Health>();
        if (healthComponent == null)
        {
            Debug.LogError("Health component not found in parent of " + gameObject.name);
        }

        if (healthComponent != null)
        {
            healthComponent.OnHealthChanged += UpdateHealthBar;
            UpdateHealthBar(healthComponent.CurrentHealth, healthComponent.MaxHealth);
        }
        else
        {
            Debug.LogError("Health component is not assigned.");
        }
    }

    void LateUpdate()
    {
        // Position health bar above object
        if (managePosition)
        {
            transform.position = healthComponent.transform.position + offset;
        }

        // Face the main camera
        transform.rotation = mainCamera.transform.rotation;
    }

    void UpdateHealthBar(int currentHealth, int maxHealth)
    {
        if (healthSlider != null)
        {
            healthSlider.value = (float)currentHealth / maxHealth;
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

