using UnityEngine;
using System.Collections.Generic;

public class Unit : MonoBehaviour
{
    private UnitState currentState;
    // TODO: dynamically get current state and display in Inspector
    // TODO: UI to change current state
    [SerializeField, ReadOnly] private string _currentStateName = "None";

    public bool IsPending = true;
    public bool IsAlive = true;
    //
    //
    public float moveSpeed = 1f;
    // TODO: consider unit size?
    public float enemyDetectionRange = 3f;
    //
    //
    public Player ownerPlayer;

    // TODO: UnitPath type?
    public List<Vector3> path = new List<Vector3>();
    public int currentPathIndex = 0;
    //
    //
    public bool isMoving = false;

    //
    //
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

        // ChangeState(new IdleState(this));
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
        ChangeState(new GoToLocationState(this, spawnToPos));
    }

    void FixedUpdate()
    {
        // DebugLogFixedUpdate($"Unit {gameObject.name} FixedUpdate position: {transform.position}, isMovingToSpawn: {isMovingToSpawn}, isMoving: {isMoving}, currentPathIndex: {currentPathIndex}, pathCount: {path.Count}");

        // Let the current state handle physics updates
        UpdateStatePhysics();
    }

    // Method to be called by FixedUpdate in Unit
    public void UpdateStatePhysics()
    {
        if (currentState != null)
        {
            currentState.FixedUpdate();
        }
    }

    void Update()
    {
        DebugLogUpdate($"Unit {gameObject.name} position: {transform.position}, currentPathIndex: {currentPathIndex}, isMoving: {isMoving}, pathCount: {path.Count}");

        currentState?.Update();
    }

    public void ChangeState(UnitState newState)
    {
        DebugLog($"Unit {gameObject.name} changing state from {_currentStateName} to {newState.GetType().Name}");
        currentState?.Exit();
        currentState = newState;
        _currentStateName = newState.GetType().Name;
        DebugLog($"Unit {gameObject.name} changed state to {_currentStateName}");
        currentState.Enter();
    }

    public void FollowPath(List<Vector3> newPath)
    {
        if (newPath.Count > 0)
        {
            path = new List<Vector3>(newPath); // Create a copy of the path
            currentPathIndex = 0;
            IsPending = false; // Unit is no longer pending once given a path

            ChangeState(new FollowPathState(this));

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

    public void MoveTo(Vector3 targetPosition)
    {
        // Get current position
        Vector3 currentPosition = transform.position;

        // Calculate direction to target (ignore Y for ground movement)
        Vector3 targetWithSameY = new Vector3(targetPosition.x, currentPosition.y, targetPosition.z);
        Vector3 direction = (targetWithSameY - currentPosition).normalized;

        // Calculate next position
        Vector3 nextPosition = currentPosition + direction * moveSpeed * Time.fixedDeltaTime;

        // Move using Rigidbody
        rb.MovePosition(nextPosition);

        // Rotate to face movement direction
        if (direction != Vector3.zero)
        {
            RotateTowards(direction);
        }
    }

    // Rotate the unit to face a specific direction
    public void RotateTowards(Vector3 direction)
    {
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                5.0f * Time.fixedDeltaTime);
        }
    }

    // Check if the unit has reached a position (within stopping distance)
    public bool HasReachedPosition(Vector3 targetPosition, float stoppingDistance = 0.1f)
    {
        Vector3 currentPosition = transform.position;
        float distanceToTarget = Vector2.Distance(
            new Vector2(currentPosition.x, currentPosition.z),
            new Vector2(targetPosition.x, targetPosition.z));

        DebugLog($"Unit {gameObject.name} position: {transform.position}, distance to target: {distanceToTarget}, target: {targetPosition}, stopping distance: {stoppingDistance}");

        return distanceToTarget < stoppingDistance;
    }

    // TODO: check player team is an opponent team
    public bool CheckForEnemiesInRange(out Unit enemy)
    {
        enemy = null;

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, enemyDetectionRange);
        foreach (var hitCollider in hitColliders)
        {
            Unit unit = hitCollider.GetComponent<Unit>();
            if (unit != null && unit.ownerPlayer != ownerPlayer)
            {
                enemy = unit;
                return true;
            }
        }

        return false;
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