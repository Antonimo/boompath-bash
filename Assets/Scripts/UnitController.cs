using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class UnitController : MonoBehaviour
{
    public Player ownerPlayer;
    public bool isPending = true;
    public float moveSpeed = 1f;
    private Vector3 spawnToPosition;
    public Vector3 spawnTargetPosition;
    public bool isMovingToSpawn = false;

    // TODO: UnitPath type
    private List<Vector3> path = new List<Vector3>();

    private int currentPathIndex = 0;
    // TODO: current Action
    // post-spawn navigation
    // waiting for path 
    // following the path
    // attacking opponent unit
    // collected something
    // attacking opponent base
    // death
    public bool isMoving = false;

    private Rigidbody rb;
    private Renderer unitRenderer;

    // Debugging
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool enableUpdateDebugLogs = false;
    [SerializeField] private bool enableFixedUpdateDebugLogs = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        unitRenderer = GetComponentInChildren<Renderer>();
    }

    void Start()
    {
        DebugLog($"Unit {gameObject.name} spawned at {transform.position}");

        // Auto-assign to player if not set
        if (ownerPlayer == null)
        {
            // Try to find player by parent relationship
            Player parentPlayer = GetComponentInParent<Player>();
            if (parentPlayer != null)
            {
                ownerPlayer = parentPlayer;
                ownerPlayer.AddUnit(this);
            }
        }

        // Set the unit's color based on the owner player's color
        if (ownerPlayer != null && unitRenderer != null)
        {
            // Apply player color to the unit
            ApplyPlayerColor();
        }
    }

    // Apply the owner's color to this unit
    private void ApplyPlayerColor()
    {
        if (unitRenderer != null && ownerPlayer != null)
        {
            // Create a new material instance to avoid affecting other units
            Material mat = new Material(unitRenderer.material);
            mat.color = ownerPlayer.playerColor;
            unitRenderer.material = mat;
        }
    }

    public void Initialize(Vector3 spawnToPos)
    {
        spawnToPosition = spawnToPos;
        StartMoveToSpawn();
    }

    // Start movement to spawnTo position
    private void StartMoveToSpawn()
    {
        // Set target position with current Y to respect gravity
        spawnTargetPosition = new Vector3(spawnToPosition.x, transform.position.y, spawnToPosition.z);

        // Face the target direction
        Vector3 direction = (spawnTargetPosition - transform.position).normalized;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }

        isMovingToSpawn = true;
    }

    void FixedUpdate()
    {
        DebugLogFixedUpdate($"Unit {gameObject.name} FixedUpdate position: {transform.position}, isMovingToSpawn: {isMovingToSpawn}, isMoving: {isMoving}, currentPathIndex: {currentPathIndex}, pathCount: {path.Count}");

        if (isMovingToSpawn)
        {
            // Move toward spawnTargetPosition using Rigidbody
            Vector3 currentPos = transform.position;
            Vector3 direction = (spawnTargetPosition - currentPos).normalized;
            Vector3 targetPos = currentPos + direction * moveSpeed * Time.fixedDeltaTime;

            // Only update X and Z to respect gravity's effect on Y
            targetPos.y = transform.position.y;
            rb.MovePosition(targetPos);

            // Check if close enough to target
            if (Vector3.Distance(new Vector3(currentPos.x, 0, currentPos.z), new Vector3(spawnTargetPosition.x, 0, spawnTargetPosition.z)) < 0.1f)
            {
                isMovingToSpawn = false;
                DebugLogFixedUpdate($"Unit {gameObject.name} reached spawn target at {spawnTargetPosition}");
            }
        }

        if (isMoving && path.Count > 0)
        {
            // TODO: fix names, refactor this whole thing...
            Vector3 target = path[currentPathIndex];
            target.y = transform.position.y;
            DebugLogFixedUpdate($"target: {target}, original target: {path[currentPathIndex]}");
            Vector3 direction = (target - transform.position).normalized;
            Vector3 movePos = transform.position + direction * moveSpeed * Time.fixedDeltaTime;
            // movePos.y = transform.position.y; // Respect gravity

            float distance = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(target.x, 0, target.z));

            rb.MovePosition(movePos);
            DebugLogFixedUpdate($"Unit {gameObject.name} moving to {target}, movePos: {movePos}, distance: {distance}");

            if (distance < 0.1f)
            {
                currentPathIndex++;
                if (currentPathIndex >= path.Count)
                {
                    isMoving = false;
                    path.Clear();
                }
            }
        }
    }

    void Update()
    {
        // TODO: there must be a convinient way to display this data
        // in the inspect view? the script component can show this data somehow?
        // label on the Scene?
        DebugLogUpdate($"Unit {gameObject.name} position: {transform.position}, currentPathIndex: {currentPathIndex}, isMoving: {isMoving}, pathCount: {path.Count}");

        if (isMoving)
        {
            // Move();
        }
    }

    private void Move()
    {
        // Move along the path if there are points to follow
        if (isMoving && currentPathIndex < path.Count)
        {
            // Get the target point while preserving our current Y position
            Vector3 currentPosition = transform.position;
            Vector3 targetPoint = path[currentPathIndex];
            Vector3 targetPosition = new Vector3(targetPoint.x, currentPosition.y, targetPoint.z);

            // Calculate direction to the target (ignoring Y component)
            Vector3 direction = (targetPosition - currentPosition).normalized;

            // Handle rotation - gradually turn to face the direction of movement
            if (direction != Vector3.zero)
            {
                // Create the target rotation
                Quaternion targetRotation = Quaternion.LookRotation(direction);

                // Smoothly rotate towards the target direction
                float rotationSpeed = 5.0f; // Adjust this value to control rotation speed
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
    }

    public void FollowPath(List<Vector3> newPath)
    {
        if (newPath.Count > 0)
        {
            path = new List<Vector3>(newPath); // Create a copy of the path
            currentPathIndex = 0;
            isPending = false; // Unit is no longer pending once given a path
            isMoving = true;

            DebugLog($"Unit {gameObject.name} following path with {path.Count} points");
        }
    }

    // Visual feedback when the unit is selectable
    public void HighlightAsSelectable(bool highlight)
    {
        if (unitRenderer != null)
        {
            // Apply visual highlighting (could be outline, glow, etc.)
            // For now, let's just adjust brightness
            Color baseColor = highlight ?
                ownerPlayer.playerColor * 1.5f : // Brighter when highlighted
                ownerPlayer.playerColor;         // Normal color otherwise

            // Ensure color values don't exceed 1
            baseColor.r = Mathf.Clamp01(baseColor.r);
            baseColor.g = Mathf.Clamp01(baseColor.g);
            baseColor.b = Mathf.Clamp01(baseColor.b);

            unitRenderer.material.color = baseColor;
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerTurn] {message}");
        }
    }

    private void DebugLogUpdate(string message)
    {
        if (enableUpdateDebugLogs)
        {
            Debug.Log($"[PlayerTurn] {message}");
        }
    }

    private void DebugLogFixedUpdate(string message)
    {
        if (enableFixedUpdateDebugLogs)
        {
            Debug.Log($"[PlayerTurn] {message}");
        }
    }
}