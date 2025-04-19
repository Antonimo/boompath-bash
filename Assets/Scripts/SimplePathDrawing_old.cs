using UnityEngine;
using System.Collections.Generic;

public class SimplePathDrawingOld : MonoBehaviour
{
    private LineRenderer lineRenderer;
    private List<Vector3> pathPoints = new List<Vector3>();
    private bool isDrawing = false;
    private GameObject currentUnit;

    void Start()
    {
        lineRenderer = gameObject.GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startWidth = 0.5f;
        lineRenderer.endWidth = 0.5f;
        lineRenderer.positionCount = 0;
        lineRenderer.startColor = Color.red;
        lineRenderer.endColor = Color.red;
        lineRenderer.textureMode = LineTextureMode.Tile;
        lineRenderer.material.mainTextureScale = new Vector2(2f, 1f);
        lineRenderer.material.renderQueue = 3100;
    }

    void Update()
    {
        if (currentUnit == null) return;

        // Handle clearing path when not drawing
        if (!isDrawing && (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)))
        {
            ClearPath();
            return;
        }

        // Handle drawing
        if (isDrawing)
        {
            // Touch input (mobile)
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Moved)
                {
                    AddPoint(touch.position);
                }
                else if (touch.phase == TouchPhase.Ended)
                {
                    isDrawing = false;
                    CompletePath();
                }
            }
            // Mouse input (testing)
            else
            {
                if (Input.GetMouseButton(0))
                {
                    AddPoint(Input.mousePosition);
                }
                else if (Input.GetMouseButtonUp(0))
                {
                    isDrawing = false;
                    CompletePath();
                }
            }
        }
    }

    void AddPoint(Vector2 screenPosition)
    {
        Ray ray = Camera.main.ScreenPointToRay(screenPosition);
        Plane plane = new Plane(Vector3.up, Vector3.zero);
        float distance;
        if (plane.Raycast(ray, out distance))
        {
            Vector3 worldPos = ray.GetPoint(distance);
            worldPos.y = 0.1f;
            pathPoints.Add(worldPos);
            lineRenderer.positionCount = pathPoints.Count;
            lineRenderer.SetPositions(pathPoints.ToArray());
        }
    }

    public void SetCurrentUnit(GameObject unit)
    {
        ClearPath();
        currentUnit = unit;
    }

    public void StartDrawing()
    {
        isDrawing = true;
        pathPoints.Clear();
        lineRenderer.positionCount = 0;
    }

    public void CompletePath()
    {
        isDrawing = false;
        if (currentUnit != null)
        {
            UnitController unitController = currentUnit.GetComponent<UnitController>();
            if (unitController != null)
            {
                unitController.FollowPath(new List<Vector3>(pathPoints));
            }
            else
            {
                Debug.LogError("UnitController not found on currentUnit!");
            }
        }
    }

    public void ClearPath()
    {
        pathPoints.Clear();
        lineRenderer.positionCount = 0;
        isDrawing = false;
    }
}