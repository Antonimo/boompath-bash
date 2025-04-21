using UnityEngine;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class SimplePathDrawing : MonoBehaviour
{
    // References to Components
    [SerializeField] private GameManager gameManager; // check intersection with enemy base
    [SerializeField] private Camera pathCamera; // path drawing ray casting from
    [SerializeField] private GameObject plane; // ray casting to

    // Input Properties
    [SerializeField] public Vector3 pathStartPosition = Vector3.zero;

    // Path Drawing Properties
    [SerializeField] private LineRenderer pathLine;
    [SerializeField] private int maxPoints = 500;
    [SerializeField] private float minPointDistance = 0.5f;
    [SerializeField] private float maxPathDistance = 150f;
    [SerializeField] private Color pathColor = Color.red;
    [SerializeField] private Color maxDistanceReachedColor = new Color(0.8f, 0.2f, 0.2f, 1f);

    // Path visual feedback
    [SerializeField] private Health health;
    [SerializeField] private GameObject healthBarCanvas;
    [SerializeField] private Vector3 healthBarOffset = new Vector3(0, 0, 3f); // Offset above path point

    // Debugging
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool enableUpdateDebugLogs = true;

    // Path Drawing State
    public List<Vector3> pathPoints { get; private set; } = new List<Vector3>();
    private bool isDrawing = false;
    private float totalPathDistance = 0f;
    public float TotalPathDistance => totalPathDistance;

    void OnEnable()
    {
        InitializeLineRenderer();

        // Set initial state
        ClearPath();

        DisableHealthBarCanvas();
    }

    void OnDisable()
    {
        ClearPath();
        DisableHealthBarCanvas();
    }

    // TODO: setup the line renderer in editor and just use it without creating it here
    private void InitializeLineRenderer()
    {
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
            pathLine.startColor = pathColor;
            pathLine.endColor = pathColor;
            pathLine.material.renderQueue = 2000;
        }
    }

    void Update()
    {
        if (enableUpdateDebugLogs)
        {
            Debug.Log($"[SimplePathDrawing] Input: MouseDown: {Input.GetMouseButtonDown(0)}, Mouse: {Input.GetMouseButton(0)}, TouchCount: {Input.touchCount}, TouchPhase: {(Input.touchCount > 0 ? Input.GetTouch(0).phase.ToString() : "N/A")}, isDrawing: {isDrawing}, PointerOverUI: {IsPointerOverUI()}");
            Debug.Log($"[SimplePathDrawing] PathPoints.Count: {pathPoints.Count}, TotalPathDistance: {totalPathDistance}");
        }

        // Process input
        if (!isDrawing && !IsPointerOverUI())
        {
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);

                if (touch.phase == TouchPhase.Began)
                {
                    if (enableDebugLogs) Debug.Log("[SimplePathDrawing] Touch began detected");
                }

                StartPath(GetWorldPositionFromPointer(touch.position));

                isDrawing = true;

                if (enableDebugLogs) Debug.Log($"[SimplePathDrawing] Path drawing started ({isDrawing})");
            }
            else if (Input.GetMouseButtonDown(0))
            {
                if (enableDebugLogs) Debug.Log("[SimplePathDrawing] Mouse down detected");

                StartPath(GetWorldPositionFromPointer(Input.mousePosition));

                isDrawing = true;

                if (enableDebugLogs) Debug.Log($"[SimplePathDrawing] Path drawing started ({isDrawing})");
            }
            else
            {
                if (enableDebugLogs) Debug.Log("[SimplePathDrawing] No input detected");
            }

            return;
        }

        if (isDrawing)
        {
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);

                AddPathPoint(GetWorldPositionFromPointer(touch.position));

                if (touch.phase == TouchPhase.Ended)
                {
                    ClearPath();
                    isDrawing = false;
                }
            }
            else
            {
                if (Input.GetMouseButton(0))
                {
                    AddPathPoint(GetWorldPositionFromPointer(Input.mousePosition));
                }
                else if (Input.GetMouseButtonUp(0))
                {
                    if (enableDebugLogs) Debug.Log("[SimplePathDrawing] Mouse up detected");
                    AddPathPoint(GetWorldPositionFromPointer(Input.mousePosition));
                    ClearPath();
                    isDrawing = false;
                }
            }
        }
    }

    private Vector3 GetWorldPositionFromPointer(Vector3 screenPosition)
    {
        Ray ray = pathCamera.ScreenPointToRay(screenPosition);

        if (plane == null || plane.transform == null)
        {
            Debug.LogError("[SimplePathDrawing] Plane or Plane Transform is null");
            return screenPosition;
        }

        Plane groundPlane = new Plane(plane.transform.up, plane.transform.position);
        if (groundPlane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }

        return screenPosition;
    }

    private bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void StartPath(Vector3 position)
    {
        if (enableDebugLogs) Debug.Log("[SimplePathDrawing] StartPath called");

        ClearPath();
        AddPathPoint(pathStartPosition);
        AddPathPoint(position);

        EnableHealthBarCanvas();
        SetHealthBarPosition(position);
    }

    private void AddPathPoint(Vector3 position)
    {
        // Ensure Y position is consistent for all points (on the ground)
        position.y = 0.1f;

        if (pathPoints.Count >= maxPoints)
        {
            return;
        }

        UpdatePathHealthBar(position);

        if (pathPoints.Count > 0)
        {
            float distance = Vector3.Distance(position, pathPoints[pathPoints.Count - 1]);
            if (distance < minPointDistance)
            {
                return;
            }

            // Check if adding this point would exceed the max path distance
            float newTotalDistance = totalPathDistance + distance;
            if (newTotalDistance > maxPathDistance)
            {
                if (pathLine != null)
                {
                    pathLine.startColor = maxDistanceReachedColor;
                    pathLine.endColor = maxDistanceReachedColor;
                }

                // Clamp the point to max distance
                Vector3 direction = (position - pathPoints[pathPoints.Count - 1]).normalized;
                float remainingDistance = maxPathDistance - totalPathDistance;
                position = pathPoints[pathPoints.Count - 1] + direction * remainingDistance;
                distance = remainingDistance;
            }

            totalPathDistance += distance;
        }

        // Add the point
        pathPoints.Add(position);

        UpdatePathVisual();

        if (gameManager != null)
        {
            if (gameManager.IsPointInsideEnemyBase(position))
            {
                if (enableDebugLogs) Debug.Log("[SimplePathDrawing] Last point is inside enemy base");

                FinishPath();
            }
            Debug.Log("[SimplePathDrawing] Done checking point inside enemy base");
        }

        // Debug log count points
        if (enableDebugLogs) Debug.Log($"[SimplePathDrawing] Points count: {pathPoints.Count}");
    }

    private void FinishPath()
    {
        if (enableDebugLogs) Debug.Log("[SimplePathDrawing] FinishPath called");

        isDrawing = false;

        // Clean up the health bar when path is finished
        DisableHealthBarCanvas();

        // Notify the GameManager that the path is complete
        if (gameManager != null && pathPoints.Count > 0)
        {
            if (enableDebugLogs) Debug.Log("[SimplePathDrawing] Path completed, notifying GameManager");

            // Create a new list with a copy of all path points
            List<Vector3> pathPointsCopy = new List<Vector3>(pathPoints);
            gameManager.ConfirmPath(pathPoints);
        }
        else if (gameManager == null)
        {
            Debug.LogError("[SimplePathDrawing] GameManager reference not assigned");
        }
    }

    private void UpdatePathVisual()
    {
        if (pathLine != null)
        {
            pathLine.positionCount = pathPoints.Count;
            pathLine.SetPositions(pathPoints.ToArray());
        }
    }

    public void ClearPath()
    {
        DisableHealthBarCanvas();

        // Clear points
        pathPoints.Clear();

        totalPathDistance = 0f;

        // Reset line renderer
        if (pathLine != null)
        {
            pathLine.positionCount = 0;
        }
    }

    #region Health Bar Canvas

    private void EnableHealthBarCanvas()
    {
        if (healthBarCanvas != null)
        {
            Canvas canvas = healthBarCanvas.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.sortingOrder = 10; // Set a higher sorting order to ensure it renders on top
            }
            healthBarCanvas.SetActive(true);
        }
    }

    private void DisableHealthBarCanvas()
    {
        if (healthBarCanvas != null)
        {
            healthBarCanvas.SetActive(false);
        }
    }

    private void SetHealthBarPosition(Vector3 position)
    {
        if (healthBarCanvas != null && healthBarCanvas.activeSelf)
        {
            Vector3 newPosition = position + healthBarOffset;
            newPosition.y = healthBarCanvas.transform.position.y;
            healthBarCanvas.transform.position = newPosition;
        }
    }

    private void UpdatePathHealthBar(Vector3 position)
    {
        if (healthBarCanvas == null) return;

        if (health == null)
        {
            Debug.LogError("[SimplePathDrawing] Health component is not assigned");
            return;
        }

        health.SetMaxHealth((int)maxPathDistance);
        health.SetHealth((int)(maxPathDistance - totalPathDistance));

        SetHealthBarPosition(position);
    }

    #endregion
}