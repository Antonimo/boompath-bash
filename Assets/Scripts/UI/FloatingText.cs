using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FloatingText : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI textComponent;
    [SerializeField] private float floatSpeed = 1f;
    [SerializeField] private float fadeSpeed = 1f;
    [SerializeField] private float lifetime = 1f;
    [SerializeField] private Vector3 floatDirection = Vector3.up;

    private float startTime;
    private Color startColor;
    private Vector3 startPosition;

    private void Awake()
    {
        if (textComponent == null)
        {
            textComponent = GetComponent<TextMeshProUGUI>();
        }
        startColor = textComponent.color;
        startPosition = transform.position;
    }

    public void Initialize(string text, Color color)
    {
        textComponent.text = text;
        textComponent.color = color;
        startTime = Time.time;
        startPosition = transform.position;
    }

    private void Update()
    {
        float elapsedTime = Time.time - startTime;

        // Float upward
        transform.position = startPosition + floatDirection * floatSpeed * elapsedTime;

        // Fade out
        float alpha = Mathf.Lerp(1f, 0f, elapsedTime * fadeSpeed);
        Color newColor = textComponent.color;
        newColor.a = alpha;
        textComponent.color = newColor;

        // Destroy after lifetime
        if (elapsedTime >= lifetime)
        {
            Destroy(gameObject);
        }
    }
}