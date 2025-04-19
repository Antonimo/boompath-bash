using UnityEngine;
using UnityEngine.EventSystems;
using System;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }
    
    [SerializeField] private LayerMask unitLayerMask; // Layer for units
    [SerializeField] private LayerMask uiLayerMask;   // Layer for UI elements
    [SerializeField] private float raycastDistance = 100f;
    [SerializeField] private bool enableDebugLogs = true; // Toggle for debug logs
    [SerializeField] private bool enablePathDrawingDebugLogs = false; // Toggle for path drawing debug logs

    // Events that other systems can subscribe to
    public event Action<Vector3> OnTapGround;
    public event Action<UnitController> OnTapUnit;
    public event Action<Vector3> OnDragStart;
    public event Action<Vector3> OnDrag;
    public event Action<Vector3> OnDragEnd;
    
    private Camera mainCamera;
    private bool isDragging = false;
    private Vector3 dragStartPosition;
    
    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Debug.Log("[InputManager] Initialized");
    }
    
    private void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("[InputManager] Main camera not found!");
        }
        else
        {
            DebugLog("Main camera reference set");
        }
        
        // Debug layer masks
        DebugLog($"Unit Layer Mask: {unitLayerMask.value}, UI Layer Mask: {uiLayerMask.value}");
    }
    
    private void Update()
    {
        // Skip input processing if over UI
        if (IsPointerOverUI())
        {
            DebugLog("Pointer is over UI - ignoring input");
            return;
        }
        
        // Get the current game state
        GameState currentState = GameManager.Instance.CurrentState;
        // DebugLog($"Current game state: {currentState}");
        
        // Process input based on current game state
        switch (currentState)
        {
            case GameState.PlayersTurn:
                ProcessPlayerTurnInput();
                break;
                
            case GameState.PathDrawing:
                ProcessPathDrawingInput();
                break;
                
            // Add more states as needed
            default:
                DebugLog($"No input handling for state: {currentState}");
                break;
        }
    }
    
    private void ProcessPlayerTurnInput()
    {
        // Handle click/tap during player turn
        if (Input.GetMouseButtonDown(0)) // Left click or touch
        {
            DebugLog("Mouse button down detected in PlayersTurn state");
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            DebugLog($"Casting ray from: {ray.origin} in direction: {ray.direction}");
            
            if (Physics.Raycast(ray, out hit, raycastDistance, unitLayerMask))
            {
                DebugLog($"Hit object on unitLayerMask: {hit.collider.gameObject.name} at position {hit.point}");
                // Hit a unit - check if it belongs to current player
                UnitController unit = hit.collider.GetComponent<UnitController>();
                if (unit != null)
                {
                    DebugLog($"UnitController found, Owner: {unit.ownerPlayer}, isPending: {unit.isPending}");
                    // Only process taps on units belonging to current player
                    if (unit.ownerPlayer == GameManager.Instance.CurrentPlayer && unit.isPending)
                    {
                        DebugLog("Valid unit selected, invoking OnTapUnit");
                        OnTapUnit?.Invoke(unit);
                        GameManager.Instance.SelectUnit(unit);
                    }
                    else
                    {
                        Debug.Log("Unit belongs to a different player or is not pending");
                    }
                }
                else
                {
                    DebugLog("No UnitController component found on hit object");
                }
            }
            else if (Physics.Raycast(ray, out hit, raycastDistance))
            {
                // Hit something else (like the ground)
                DebugLog($"Hit ground or other object: {hit.collider.gameObject.name} at position {hit.point}");
                OnTapGround?.Invoke(hit.point);
                DebugLog($"OnTapGround event invoked at {hit.point}");
            }
            else
            {
                DebugLog("Ray did not hit anything");
            }
        }
    }
    
    private void ProcessPathDrawingInput()
    {
        // Handle path drawing input
        if (Input.GetMouseButtonDown(0)) // Start drawing
        {
            isDragging = true;
            dragStartPosition = GetMouseWorldPosition();
            PathDrawingDebugLog($"Starting drag at position: {dragStartPosition}");
            OnDragStart?.Invoke(dragStartPosition);
        }
        else if (Input.GetMouseButton(0) && isDragging) // Continue drawing
        {
            Vector3 currentPos = GetMouseWorldPosition();
            PathDrawingDebugLog($"Dragging at position: {currentPos}");
            OnDrag?.Invoke(currentPos);
        }
        else if (Input.GetMouseButtonUp(0) && isDragging) // End drawing
        {
            Vector3 endPos = GetMouseWorldPosition();
            isDragging = false;
            PathDrawingDebugLog($"Ending drag at position: {endPos}");
            OnDragEnd?.Invoke(endPos);
        }
    }
    
    private Vector3 GetMouseWorldPosition()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        // Use a layermask for the ground/terrain
        if (Physics.Raycast(ray, out hit, raycastDistance))
        {
            DebugLog($"Mouse position raycast hit: {hit.collider.gameObject.name} at {hit.point}");
            return hit.point;
        }
        
        // Fallback to a plane at y=0 if no hit
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        float rayDistance;
        
        if (groundPlane.Raycast(ray, out rayDistance))
        {
            Vector3 point = ray.GetPoint(rayDistance);
            DebugLog($"Mouse position using groundPlane: {point}");
            return point;
        }
        
        DebugLog("Failed to get mouse world position");
        return Vector3.zero;
    }
    
    private bool IsPointerOverUI()
    {
        // Check if the pointer is over a UI element
        bool isOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (isOverUI)
        {
            DebugLog("Pointer is over UI element");
        }
        return isOverUI;
    }
    
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[InputManager] {message}");
        }
    }

    private void PathDrawingDebugLog(string message)
    {
        if (enablePathDrawingDebugLogs)
        {
            Debug.Log($"[InputManager] {message}");
        }
    }
}