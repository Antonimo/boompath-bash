using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class UnitController : MonoBehaviour
{
    public Player ownerPlayer;
    public bool isPending = true;
    public float moveSpeed = 1f;
    private Vector3 spawnToPosition;
    private Vector3 spawnTargetPosition;
    private bool isMovingToSpawn = false;
    
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
    private bool isMoving = false;
    
    private Rigidbody rb;
    private Renderer unitRenderer;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        unitRenderer = GetComponentInChildren<Renderer>();
    }

    void Start()
    {
        Debug.Log($"Unit {gameObject.name} spawned at {transform.position}");
        
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
                Debug.Log($"Unit {gameObject.name} reached spawn target at {spawnTargetPosition}");
            }
        }

        if (isMoving && path.Count > 0)
        {
            Vector3 target = path[currentPathIndex];
            Vector3 direction = (target - transform.position).normalized;
            Vector3 movePos = transform.position + direction * moveSpeed * Time.fixedDeltaTime;
            movePos.y = transform.position.y; // Respect gravity
            rb.MovePosition(movePos);

            if (Vector3.Distance(transform.position, target) < 0.1f)
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
        // Move along the path if there are points to follow
        if (isMoving && currentPathIndex < path.Count)
        {
            Vector3 target = path[currentPathIndex];
            Vector3 direction = (target - transform.position).normalized;
            transform.position += direction * moveSpeed * Time.deltaTime;

            // Check if close enough to the current point
            if (Vector3.Distance(transform.position, target) < 0.1f)
            {
                currentPathIndex++;
                if (currentPathIndex >= path.Count)
                {
                    // Reached the end of the path
                    isMoving = false;
                }
            }
        }
    }

    // We don't need OnMouseDown anymore as InputManager handles this
    // void OnMouseDown() has been removed

    public void FollowPath(List<Vector3> newPath)
    {
        if (newPath.Count > 0)
        {
            path = new List<Vector3>(newPath); // Create a copy of the path
            currentPathIndex = 0;
            isMoving = true;
            isPending = false; // Unit is no longer pending once given a path
            
            // Face the first point in the path
            Vector3 direction = (path[0] - transform.position).normalized;
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
            
            Debug.Log($"Unit {gameObject.name} following path with {path.Count} points");
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
}