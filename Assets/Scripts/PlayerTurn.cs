using UnityEngine;
using UnityEngine.EventSystems;

public class PlayerTurn : MonoBehaviour
{
    public Player player;
    public Camera mainCamera;
    public GameManager gameManager;

    [SerializeField] private LayerMask unitLayerMask;
    [SerializeField] private float raycastDistance = 100f;

    // Debugging
    [SerializeField] private bool enableDebugLogs = true;

    void OnEnable()
    {
        DebugLog("Enabled");

    }

    void OnDisable()
    {
        DebugLog("Disabled");

    }

    void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("Main camera not found!");
        }
    }

    void Update()
    {
        if (IsPointerOverUI())
        {
            return;
        }

        ProcessPlayerTurnInput();
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
                    if (unit.ownerPlayer == player && unit.isPending)
                    {
                        DebugLog("Valid unit selected, invoking OnTapUnit");
                        gameManager.SelectUnit(unit);
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
            // else if (Physics.Raycast(ray, out hit, raycastDistance))
            // {
            //     // Hit something else (like the ground)
            //     DebugLog($"Hit ground or other object: {hit.collider.gameObject.name} at position {hit.point}");
            // }
            else
            {
                DebugLog("Ray did not hit anything");
            }
        }
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

    public void HighlightSelectableUnits(bool highlight)
    {
        if (player != null)
        {
            foreach (UnitController unit in player.OwnedUnits)
            {
                if (unit != null && unit.isPending)
                {
                    unit.HighlightAsSelectable(highlight);
                }
            }
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerTurn] {message}");
        }
    }
}
