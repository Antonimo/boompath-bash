using UnityEngine;
using System.Collections.Generic;

public class SimplePathDrawing : MonoBehaviour
{
    [SerializeField] private LineRenderer pathLine;
    [SerializeField] private float minPointDistance = 0.5f;
    [SerializeField] private int maxPoints = 100;
    [SerializeField] private GameObject pathMarkerPrefab;
    
    public List<Vector3> PathPoints { get; private set; } = new List<Vector3>();
    private List<GameObject> pathMarkers = new List<GameObject>();
    
    private bool isDrawing = false;
    
    void OnEnable()
    {
        Debug.Log("[SimplePathDrawing] Enabled");
        // log if InputManager.Instance exists
        if (InputManager.Instance != null)
        {
            Debug.Log("[SimplePathDrawing] InputManager instance found");
        }
        else
        {
            Debug.LogWarning("[SimplePathDrawing] InputManager instance not found");
        }

        // Subscribe to input events when enabled
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnDragStart += StartPath;
            InputManager.Instance.OnDrag += AddPathPoint;
            InputManager.Instance.OnDragEnd += FinishPath;
        }
        
        // Initialize line renderer if needed
        if (pathLine == null)
        {
            pathLine = GetComponent<LineRenderer>();
            if (pathLine == null)
            {
                pathLine = gameObject.AddComponent<LineRenderer>();
            }

            pathLine.startWidth = 0.5f;
            pathLine.endWidth = 0.5f;
            pathLine.material = new Material(Shader.Find("Sprites/Default"));
            pathLine.startColor = Color.red;
            pathLine.endColor = Color.red;
            pathLine.material.renderQueue = 3100;
        }
        
        // Set initial state
        ClearPath();
    }
    
    void OnDisable()
    {
        // Unsubscribe from input events when disabled
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnDragStart -= StartPath;
            InputManager.Instance.OnDrag -= AddPathPoint;
            InputManager.Instance.OnDragEnd -= FinishPath;
        }
    }
    
    public void StartDrawing()
    {
        isDrawing = true;
        ClearPath();
    }
    
    private void StartPath(Vector3 position)
    {
        if (!isDrawing) return;
        
        ClearPath();
        AddPathPoint(position);
    }
    
    private void AddPathPoint(Vector3 position)
    {
        if (!isDrawing) return;
        
        // Ensure Y position is consistent for all points (on the ground)
        position.y = 10f;
        
        // Don't add if too close to the last point
        if (PathPoints.Count > 0)
        {
            float distance = Vector3.Distance(position, PathPoints[PathPoints.Count - 1]);
            if (distance < minPointDistance)
            {
                return;
            }
        }
        
        // Don't exceed max points
        if (PathPoints.Count >= maxPoints)
        {
            return;
        }
        
        // Add the point
        PathPoints.Add(position);
        
        // Create a visual marker
        if (pathMarkerPrefab != null)
        {
            GameObject marker = Instantiate(pathMarkerPrefab, position, Quaternion.identity);
            pathMarkers.Add(marker);
        }
        
        // Update line renderer
        UpdatePathVisual();
    }
    
    private void FinishPath(Vector3 position)
    {
        if (!isDrawing) return;
        
        // Add final point if needed
        AddPathPoint(position);
        
        // Notify the GameManager that the path is complete
        if (GameManager.Instance != null && PathPoints.Count > 0)
        {
            GameManager.Instance.ConfirmPath(PathPoints);
        }
        
        isDrawing = false;
    }
    
    private void UpdatePathVisual()
    {
        if (pathLine != null)
        {
            pathLine.positionCount = PathPoints.Count;
            pathLine.SetPositions(PathPoints.ToArray());
        }
    }
    
    public void ClearPath()
    {
        // Clear points
        PathPoints.Clear();
        
        // Clear visual markers
        foreach (GameObject marker in pathMarkers)
        {
            Destroy(marker);
        }
        pathMarkers.Clear();
        
        // Reset line renderer
        if (pathLine != null)
        {
            pathLine.positionCount = 0;
        }
    }
}