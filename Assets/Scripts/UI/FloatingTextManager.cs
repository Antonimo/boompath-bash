using UnityEngine;
using System.Collections.Generic;

public class FloatingTextManager : MonoBehaviour
{
    [SerializeField] private GameObject floatingTextPrefab;
    [SerializeField] private int initialPoolSize = 10;
    [SerializeField] private Color hitColor = Color.red;
    [SerializeField] private Color missColor = Color.gray;
    [SerializeField] private float textHeightOffset = 3f; // Height above unit where text appears

    private Queue<GameObject> textPool = new Queue<GameObject>();
    private Transform poolParent;

    private void Awake()
    {
        poolParent = new GameObject("FloatingTextPool").transform;
        poolParent.SetParent(transform);
        InitializePool();
    }

    private void InitializePool()
    {
        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateNewTextObject();
        }
    }

    private GameObject CreateNewTextObject()
    {
        GameObject textObj = Instantiate(floatingTextPrefab, poolParent);
        textObj.SetActive(false);
        textPool.Enqueue(textObj);
        return textObj;
    }

    private GameObject GetTextObject()
    {
        if (textPool.Count == 0)
        {
            return CreateNewTextObject();
        }

        return textPool.Dequeue();
    }

    public void ShowText(Vector3 position, string text, bool isHit)
    {
        GameObject textObj = GetTextObject();
        textObj.transform.position = position + Vector3.up * textHeightOffset;
        textObj.SetActive(true);

        FloatingText floatingText = textObj.GetComponent<FloatingText>();
        if (floatingText != null)
        {
            floatingText.Initialize(text, isHit ? hitColor : missColor);
        }

        // Return to pool after animation
        StartCoroutine(ReturnToPool(textObj));
    }

    private System.Collections.IEnumerator ReturnToPool(GameObject textObj)
    {
        yield return new WaitForSeconds(1.5f); // Wait for animation to complete
        textObj.SetActive(false);
        textPool.Enqueue(textObj);
    }
}