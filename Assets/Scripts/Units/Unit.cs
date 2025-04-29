using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;

[RequireComponent(typeof(Health))] // Ensure Health component exists
[RequireComponent(typeof(NetworkObject))] // Ensure NetworkObject exists
[RequireComponent(typeof(Collider))]
public class Unit : NetworkBehaviour
{
    private UnitState currentState;
    // TODO: dynamically get current state and display in Inspector
    // TODO: UI to change current state
    [SerializeField] private string _currentStateName = "None";

    public Vector3 spawnToPos;

    public bool IsPending = true;
    // TODO: should be part of the Health component?
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

    // Combat properties
    [SerializeField] private float hitChance = 0.75f; // 70% chance to hit
    // TODO: damage dice? properties? modifiers? buff/debuff? etc...
    [SerializeField] private int minDamage = 10;
    [SerializeField] private int maxDamage = 34;
    [SerializeField] private Health health;

    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody>();
        unitRenderer = GetComponentInChildren<Renderer>();
        health = GetComponent<Health>();

        // Disable physics initially
        if (rb != null)
        {
            rb.isKinematic = true;
        }
        else
        {
            Debug.LogError($"Unit {NetworkObjectId} is missing Rigidbody component on Spawn!", this);
        }

        // Subscribe to health event early
        if (health != null)
        {
            health.OnHealthDepleted += HandleDeath;
        }
        else
        {
            Debug.LogError($"Unit {NetworkObjectId} is missing Health component on Spawn!", this);
            return;
        }

        DebugLog($"Unit {NetworkObjectId} Network Spawned. Starting Parent Wait Coroutine.");

        // Start the coroutine to wait for the parent and finish initialization
        StartCoroutine(WaitForParentAndInitialize());
    }

    // Coroutine to wait until the parent Player component is found
    private IEnumerator WaitForParentAndInitialize()
    {
        DebugLog($"Unit {NetworkObjectId} Coroutine: Waiting for parent Player...");
        while (true) // Keep checking until found
        {
            Player parentPlayer = GetComponentInParent<Player>();
            if (parentPlayer != null)
            {
                DebugLog($"Unit {NetworkObjectId} Coroutine: Found parent Player {parentPlayer.OwnerClientId}.");
                ownerPlayer = parentPlayer;
                Initialize();
                yield break;
            }

            yield return new WaitForSeconds(0.5f);
        }
    }

    // Contains initialization logic that depends on ownerPlayer or parent being set
    private void Initialize()
    {
        DebugLog($"Unit {NetworkObjectId} Initializing with Owner Player {ownerPlayer?.OwnerClientId ?? ulong.MaxValue}.");

        if (ownerPlayer == null)
        {
            Debug.LogError($"Unit {NetworkObjectId} Initialize called but ownerPlayer is null! Should not happen if WaitForParentAndInitialize worked.", this);
            return; // Should not proceed without an owner
        }

        // Set the unit's color now that the owner is confirmed
        ApplyPlayerColor();

        // Setup Physics Ignore Collision with sibling Base
        Collider ownCollider = GetComponent<Collider>();
        if (ownCollider != null)
        {
            BaseController siblingBase = ownerPlayer.GetComponentInChildren<BaseController>(true); // include inactive
            if (siblingBase != null)
            {
                Collider baseCollider = siblingBase.GetComponent<Collider>();
                if (baseCollider != null)
                {
                    Physics.IgnoreCollision(ownCollider, baseCollider, true);
                    DebugLog($"Unit {NetworkObjectId} ignoring collision with sibling base {siblingBase.name} locally (in Initialize).");
                }
                else
                {
                    Debug.LogWarning($"Unit {NetworkObjectId} found sibling BaseController {siblingBase.name} but it has no Collider (in Initialize).", siblingBase);
                }
            }
            else
            {
                // This case might still occur if the parent Player object doesn't have a BaseController child
                Debug.LogWarning($"Unit {NetworkObjectId} did not find sibling BaseController during Initialize. Check hierarchy.", this);
            }
        }
        else
        {
            Debug.LogError($"Unit {NetworkObjectId} is missing a Collider component during Initialize!", this);
        }

        DebugLog($"Unit {NetworkObjectId} Initialization complete. Enabling physics.");

        // Enable physics now that initialization is complete
        if (rb != null)
        {
            rb.isKinematic = false;
        }
        else
        {
            Debug.LogError($"Unit {NetworkObjectId} is missing Rigidbody component during Initialize!", this);
        }

        ChangeState(new GoToLocationState(this, spawnToPos));
    }

    public override void OnDestroy()
    {
        // Unsubscribe from the health depleted event to prevent memory leaks
        if (health != null)
        {
            health.OnHealthDepleted -= HandleDeath;
        }
    }

    // Apply the owner's color to this unit
    private void ApplyPlayerColor()
    {
        if (unitRenderer != null && ownerPlayer != null)
        {
            // Create a new material instance to avoid affecting other units
            Material mat = new Material(unitRenderer.material);
            mat.color = ownerPlayer.CurrentPlayerColor;
            unitRenderer.material = mat;
        }
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
                ownerPlayer.CurrentPlayerColor * 1.5f : // Brighter when highlighted
                ownerPlayer.CurrentPlayerColor;         // Normal color otherwise

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
    // Returns true if an enemy Unit or Base is within range, providing the target component
    public bool CheckForTargetsInRange(out Component target)
    {
        target = null;

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, enemyDetectionRange);
        foreach (var hitCollider in hitColliders)
        {
            // Check for enemy Unit first
            Unit unit = hitCollider.GetComponent<Unit>();
            if (unit != null && unit.ownerPlayer != ownerPlayer && unit.IsAlive) // Check if the unit is alive
            {
                target = unit;
                return true;
            }

            // Check for enemy BaseController
            BaseController baseController = hitCollider.GetComponent<BaseController>();
            // TODO: Add health check for BaseController if it has one
            if (baseController != null && baseController.OwnerPlayer != ownerPlayer)
            {
                target = baseController;
                return true;
            }
        }

        return false;
    }

    public void TakeDamage(int damage)
    {
        health.TakeDamage(damage);
        // The death check is now handled by the HandleDeath method via the OnHealthDepleted event
        // if (health.CurrentHealth <= 0)
        // {
        //     ChangeState(new DeadState(this));
        // }
    }

    private void HandleDeath()
    {
        // Prevent trying to change state if already dead or dying
        if (currentState is not DeadState)
        {
            DebugLog($"Unit {gameObject.name} health depleted. Changing to DeadState.");
            ChangeState(new DeadState(this));
            IsAlive = false; // Mark as not alive
        }
    }

    public int CalculateDamage()
    {
        return Random.Range(minDamage, maxDamage + 1);
    }

    public bool TryHit()
    {
        return Random.value <= hitChance;
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