using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;

[RequireComponent(typeof(Health))]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(NetworkTransform))]
public class Unit : NetworkBehaviour
{
    // TODO: freeze unit movement when below -500 Y?
    // TODO: disable/reduce network sync when unit is dead?
    public enum UnitStateEnum
    {
        Idle,
        GoToLocation,
        FollowingPath,
        Attacking,
        Dead
        // TODO: Game Over State, celebrate win or be sad
    }

    // --- Networked Variables ---
    // Tracks the NetworkObjectId of the owner Player object
    public NetworkVariable<ulong> OwnerPlayerId = new NetworkVariable<ulong>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server); // Default to 0 or an invalid ID

    // This is the local reference to the owning Player component.
    // It's set directly on the server via InitializeOwnerPlayer,
    /// <see cref="InitializeOwnerPlayer"/>
    // and set on clients via the OnOwnerChanged callback when OwnerPlayerId replicates.
    public Player ownerPlayer { get; private set; }

    // Tracks the current state across the network
    public NetworkVariable<UnitStateEnum> NetworkCurrentState = new NetworkVariable<UnitStateEnum>(UnitStateEnum.Idle, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    // TODO: for syncinc state data:
    // NetworkVariable<StateData>
    // https://grok.com/share/bGVnYWN5_a5861f4a-b711-4dd8-b2b6-5110548899c6

    private UnitState currentState;
    // TODO: dynamically get current state and display in Inspector -> Now use NetworkCurrentState
    // TODO: UI to change current state
    // [SerializeField] private string _currentStateName = "None"; // Replaced by NetworkCurrentState

    public bool IsPending = true;
    // TODO: should be part of the Health component? Or derived from state?
    public bool IsAlive = true;
    //
    //
    public float moveSpeed = 1f;
    // TODO: consider unit size?
    public float enemyDetectionRange = 3f;
    //
    //

    // TODO: UnitPath type?
    // TODO: This path will likely only exist on the server
    public List<Vector3> path = new List<Vector3>();
    public int currentPathIndex = 0;
    //
    //

    //
    //
    private Rigidbody rb;
    private Renderer unitRenderer;

    // Combat properties
    [SerializeField] private float hitChance = 0.75f; // 70% chance to hit
    // TODO: damage dice? properties? modifiers? buff/debuff? etc...
    [SerializeField] private int minDamage = 10;
    [SerializeField] private int maxDamage = 34;
    [SerializeField] private Health health;

    // Debugging
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool enableUpdateDebugLogs = false;
    [SerializeField] private bool enableFixedUpdateDebugLogs = false;

    public override void OnNetworkSpawn()
    {
        DebugLog($"{NetworkManager.Singleton.LocalClientId}: Unit {NetworkObjectId} Network Spawned. OwnerClientId: {OwnerClientId}");

        // --- Component Fetching ---
        // Fetch components essential for the unit's operation.
        rb = GetComponent<Rigidbody>();
        unitRenderer = GetComponentInChildren<Renderer>();
        health = GetComponent<Health>();

        // Log errors if essential components are missing
        if (rb == null) Debug.LogError($"Unit {NetworkObjectId} is missing Rigidbody component on Spawn!", this);
        if (unitRenderer == null) Debug.LogError($"Unit {NetworkObjectId} is missing Renderer in children on Spawn!", this);
        if (health == null) Debug.LogError($"Unit {NetworkObjectId} is missing Health component on Spawn!", this);

        // Subscribe to health event - needed by both server (for state change) and clients (potentially for effects)
        if (health != null)
        {
            health.OnHealthDepleted += HandleDeath;
        }

        // Call OnOwnerChanged immediately with the current value to set initial state if owner is already known
        // OnOwnerChanged(0, OwnerPlayerId.Value); // Pass 0 as previous, doesn't matter here

        // Subscribe to owner changes to update visuals like color
        OwnerPlayerId.OnValueChanged += OnOwnerChanged;
        // TODO: test how this works with late joining / reconnecing clients,
        // Do we need to call OnOwnerChanged() here?

        // Subscribe to Network state changes
        NetworkCurrentState.OnValueChanged += OnNetworkStateChanged;
        // Immediately update local state based on initial network state
        OnNetworkStateChanged(default, NetworkCurrentState.Value); // Use default as previous

        // --- Server-Side Initialization ---
        // Server side specific setup is now handled by the spawner calling InitializeOwnerPlayer
        // and other logic remaining in InitializeServerSide if needed for non-owner things.
        if (IsServer)
        {
            InitializeServerSideNonOwnerTasks(); // Renamed or refactored from InitializeServerSide
        }
    }

    public override void OnDestroy()
    {
        // Unsubscribe from the health depleted event to prevent memory leaks
        if (health != null)
        {
            health.OnHealthDepleted -= HandleDeath;
        }

        // Unsubscribe from network variable event
        // Check if NetworkManager exists as it might be destroyed before this object
        if (NetworkManager.Singleton != null)
        {
            OwnerPlayerId.OnValueChanged -= OnOwnerChanged;
            NetworkCurrentState.OnValueChanged -= OnNetworkStateChanged; // Unsubscribe from state changes
        }
    }

    /// <summary>
    /// Performs server-side initialization tasks that DON'T depend on the owner being set immediately.
    /// Owner-specific initialization is now handled by InitializeOwnerPlayer.
    /// </summary>
    private void InitializeServerSideNonOwnerTasks()
    {
        if (!IsServer) return; // Double check just in case

        DebugLog($"Unit {NetworkObjectId} Initializing Server Side Non-Owner Tasks.");
    }

    /// <summary>
    /// Initializes the Unit with its owning Player. MUST be called by the server immediately after spawning.
    /// Sets the local owner reference and the networked OwnerPlayerId.
    /// </summary>
    /// <param name="owner">The Player object that owns this unit.</param>
    public void InitializeOwnerPlayer(Player owner)
    {
        if (!IsServer)
        {
            DebugLogError($"InitializeOwnerPlayer called on client for Unit {NetworkObjectId}. This should only be called on the server.");
            return;
        }
        if (owner == null)
        {
            DebugLogError($"InitializeOwnerPlayer called with null owner for Unit {NetworkObjectId}.");
            return;
        }

        DebugLog($"Unit {NetworkObjectId} InitializeOwnerPlayer called by server. Setting owner to Player {owner.NetworkObjectId} ({owner.name}).");
        ownerPlayer = owner;
        OwnerPlayerId.Value = owner.NetworkObjectId; // This will replicate to clients

        // Apply color immediately on the server
        ApplyPlayerColor();
    }

    // Callback for when the OwnerPlayerId NetworkVariable changes
    private void OnOwnerChanged(ulong previousOwnerNetId, ulong newOwnerNetId)
    {
        DebugLog($"Unit {NetworkObjectId} OwnerPlayerId changed from {previousOwnerNetId} to {newOwnerNetId}. Finding Player object.");

        if (newOwnerNetId == 0) // Handle case where owner might be reset or invalid
        {
            DebugLogWarning($"Unit {NetworkObjectId} received invalid owner NetworkObjectId (0). Clearing local owner.");
            ownerPlayer = null;
            // Optionally reset color or other owner-specific visuals
            // ResetColor();
            return;
        }

        // Find the NetworkObject associated with the newOwnerNetId
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.SpawnManager != null &&
            NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(newOwnerNetId, out NetworkObject ownerNetworkObject))
        {
            Player foundPlayer = ownerNetworkObject.GetComponent<Player>();
            if (foundPlayer != null)
            {
                DebugLog($"Unit {NetworkObjectId} Found owner Player object: {foundPlayer.name} (NetId: {newOwnerNetId})");
                ownerPlayer = foundPlayer;

                // TODO: move to some kind of OnOwnerPlayerSet() method?
                ApplyPlayerColor(); // Update color based on the new owner

                // Now that ownerPlayer is set, setup local physics if we are a client
                if (IsClient)
                {
                    SetupLocalCollisionIgnores();
                    // TODO: re-enable collision with previous base (all other bases)
                }
            }
            else
            {
                DebugLogError($"Unit {NetworkObjectId} Found NetworkObject for owner ID {newOwnerNetId}, but it has no Player component.");
                ownerPlayer = null;
            }
        }
        else
        {
            // This might happen temporarily during network setup or if the owner object hasn't spawned yet on this client
            DebugLogWarning($"Unit {NetworkObjectId} could not find spawned NetworkObject for owner ID {newOwnerNetId}. Owner might not be spawned locally yet.");
            ownerPlayer = null; // Clear local owner if lookup fails
        }
    }

    // Apply the owner's color to this unit
    // Relies on this.ownerPlayer being set correctly.
    private void ApplyPlayerColor()
    {
        if (unitRenderer != null && ownerPlayer != null)
        {
            DebugLog($"Unit {NetworkObjectId} applying color for owner {ownerPlayer.NetworkObjectId}");
            // Create a new material instance to avoid affecting other units using the same base material
            // Check if we already have an instance material maybe? For now, new one each time.
            Material mat = new Material(unitRenderer.material); // Consider using sharedMaterial and MaterialPropertyBlock for performance
            mat.color = ownerPlayer.CurrentPlayerColor;
            unitRenderer.material = mat;
        }
        else
        {
            if (unitRenderer == null) DebugLogWarning($"Unit {NetworkObjectId} cannot apply color - unitRenderer is null.");
            if (ownerPlayer == null) DebugLogWarning($"Unit {NetworkObjectId} cannot apply color - ownerPlayer is null. Waiting for owner update.");
            // Optionally reset to a default color if ownerPlayer is null
        }
    }

    // Called on clients in OnNetworkSpawn to handle local physics settings
    // This should ideally run *after* ownerPlayer is confirmed locally.
    private void SetupLocalCollisionIgnores()
    {
        if (!IsClient) return;
        if (ownerPlayer == null)
        {
            DebugLogWarning($"Unit {NetworkObjectId} cannot setup collision ignores yet: ownerPlayer is null. Will retry on owner change.");
            return;
        }

        DebugLog($"Unit {NetworkObjectId} owner is Player NetworkObjectId {ownerPlayer.NetworkObjectId}. Setting up collision ignores on local client {NetworkManager.Singleton.LocalClientId}.");

        BaseController ownerBase = ownerPlayer.GetComponentInChildren<BaseController>();

        if (ownerBase != null)
        {
            Collider unitCollider = GetComponent<Collider>();
            Collider baseCollider = ownerBase.GetComponent<Collider>();

            if (unitCollider != null && baseCollider != null)
            {
                Physics.IgnoreCollision(unitCollider, baseCollider, true);
                // TODO: fix logs, ether dont use ownerBase.name or set the base names to something identifiable
                DebugLog($"Unit {NetworkObjectId} ignoring collision with owner ({ownerPlayer.NetworkObjectId}) base {ownerBase.name} locally.");
            }
            else
            {
                if (unitCollider == null) Debug.LogError($"Unit {NetworkObjectId} cannot ignore collision: Unit collider is missing.", this);
                if (baseCollider == null) Debug.LogError($"Unit {NetworkObjectId} cannot ignore collision: Owner {ownerPlayer.NetworkObjectId}'s Base collider is missing.", ownerBase);
            }
        }
        else
        {
            Debug.LogWarning($"Unit {NetworkObjectId} could not find BaseController child on owner {ownerPlayer.NetworkObjectId}'s player object {ownerPlayer.name} for collision ignore.", ownerPlayer);
        }
    }

    void FixedUpdate()
    {
        // DebugLogFixedUpdate($"Unit {gameObject.name} FixedUpdate position: {transform.position}, isMovingToSpawn: {isMovingToSpawn}, isMoving: {isMoving}, currentPathIndex: {currentPathIndex}, pathCount: {path.Count}");

        // Let the current state handle physics updates.
        // State classes must guard authoritative logic (e.g., movement) with if(IsServer).
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
        // DebugLogUpdate($"Unit {gameObject.name} position: {transform.position}, currentPathIndex: {currentPathIndex}, pathCount: {path.Count}"); // Removed: Too noisy

        // Let the current state handle non-physics updates.
        // State classes must ensure Update logic is client-safe (visuals, non-authoritative actions).
        currentState?.Update();
    }

    public void ChangeState(UnitState newState)
    {
        // Use the type name of the new state for logging
        string newStateName = newState != null ? newState.GetType().Name : "null";
        // Use the type name of the current state, or "None" if null
        string previousStateName = currentState != null ? currentState.GetType().Name : "None";

        // Core state transition logic ONLY runs on the server
        if (IsServer)
        {
            DebugLog($"[Server] Unit {gameObject.name} changing state from {previousStateName} to {newStateName}");

            currentState?.Exit();
            currentState = newState;
            // _currentStateName = newState.GetType().Name; // Removed

            // Update the NetworkVariable to sync state to clients
            NetworkCurrentState.Value = StateToEnum(newState);
            DebugLog($"[Server] Unit {gameObject.name} set NetworkCurrentState to {NetworkCurrentState.Value}");

            currentState.Enter();
        }
        else
        {
            // Clients do not change state directly. They react to NetworkCurrentState changes.
            // Log if a client attempts to change state directly (shouldn't happen in correct flow)
            DebugLogWarning($"[Client] Attempted to ChangeState directly to {newStateName}. State change is server-authoritative.");
        }
    }

    // Helper method to convert a UnitState instance to its corresponding enum value
    private UnitStateEnum StateToEnum(UnitState state)
    {
        return state switch
        {
            GoToLocationState _ => UnitStateEnum.GoToLocation,
            FollowPathState _ => UnitStateEnum.FollowingPath,
            AttackState _ => UnitStateEnum.Attacking, // Assuming AttackState exists
            DeadState _ => UnitStateEnum.Dead,
            _ => UnitStateEnum.Idle, // Default or handle null/unknown state
        };
    }

    // Called on clients when the NetworkCurrentState variable changes
    private void OnNetworkStateChanged(UnitStateEnum previousStateEnum, UnitStateEnum newStateEnum)
    {
        DebugLog($"[Client] Network State Changed from {previousStateEnum} to {newStateEnum} for Unit {NetworkObjectId}");

        // Avoid state setup on server side via callback, as it's handled directly in ChangeState
        if (IsHost)
        {
            DebugLog($"[Server] Skipping client state setup for {newStateEnum}");
            return;
        }

        // Update the local state representation for visualization/non-authoritative logic
        // Note: State constructors might need refactoring if they expect server-only data.
        UnitState newLocalState = newStateEnum switch
        {
            UnitStateEnum.GoToLocation => new GoToLocationState(this, Vector3.zero), // TODO: Client doesn't have spawnToPos. Movement driven by NetworkTransform.
            UnitStateEnum.FollowingPath => new FollowPathState(this), // Path data exists only on server, client state needs adaptation
            UnitStateEnum.Attacking => new AttackState(this, null, null), // TODO: Client needs target synced separately? Passing null for target and return state.
            UnitStateEnum.Dead => new DeadState(this),
            UnitStateEnum.Idle => null, // Or new IdleState(this) if it exists
            _ => null,
        };

        // Directly set the client's current state representation
        // Do NOT call Exit/Enter here, they are server-authoritative.
        // Client-side states should handle their visual setup in constructor or Update.
        currentState = newLocalState;
        DebugLog($"[Client] Set local currentState representation to {newLocalState?.GetType().Name ?? "null"}");

        // Update IsAlive based on DeadState transition (clients need this too)
        if (newStateEnum == UnitStateEnum.Dead)
        {
            IsAlive = false;
        }
    }

    /// <summary>
    /// Public method called to assign a path to this unit.
    /// If called on a client, it sends the path to the server via RPC.
    /// If called on the server, it directly processes the path.
    /// </summary>
    /// <param name="newPath">The list of waypoints for the unit to follow.</param>
    public void FollowPath(List<Vector3> newPath)
    {
        if (newPath == null || newPath.Count == 0)
        {
            DebugLogWarning($"Unit {NetworkObjectId} FollowPath called with null or empty path.");
            return;
        }

        // Logic now assumes this is ONLY called on the server.
        // The client initiates the path assignment via Player.RequestUnitPathAssignment,
        // which triggers Player.AssignPathToServerRpc, which then calls this method on the server.
        if (IsServer)
        {
            // If already on the server, process directly
            ProcessFollowPath(newPath);
        }
        else
        {
            // FollowPath should no longer be called directly on non-server instances.
            DebugLogError($"[Client] FollowPath called directly on Unit {NetworkObjectId}. This should only happen via Server RPC. Ignoring.");
        }
    }

    // Processes the path following logic - only runs on server.
    private void ProcessFollowPath(List<Vector3> newPath)
    {
        if (!IsServer) return;

        path = new List<Vector3>(newPath); // Store a copy of the path (server-side)
        currentPathIndex = 0;
        IsPending = false; // Unit is no longer pending once given a path

        ChangeState(new FollowPathState(this)); // Server changes state

        DebugLog($"[Server] Unit {NetworkObjectId} processed path with {path.Count} points. Changed state to FollowingPath.");
    }

    // Visual feedback when the unit is selectable
    public void HighlightAsSelectable(bool highlight)
    {
        if (unitRenderer != null && ownerPlayer != null) // Check ownerPlayer exists
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
            new Vector2(targetPosition.x, targetPosition.z)
        );

        // DebugLog($"Unit {gameObject.name} position: {transform.position}, distance to target: {distanceToTarget}, target: {targetPosition}, stopping distance: {stoppingDistance}");

        return distanceToTarget < stoppingDistance;
    }

    // TODO: check player team is an opponent team
    // Returns true if an enemy Unit or Base is within range, providing the target component
    // NOTE: This method should ONLY be called by server-authoritative code (e.g., server-side state updates).
    public bool CheckForTargetsInRange(out Component target)
    {
        target = null;

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, enemyDetectionRange);
        foreach (var hitCollider in hitColliders)
        {
            // Check for enemy Unit first
            Unit unit = hitCollider.GetComponent<Unit>();
            if (unit != null && unit.ownerPlayer != ownerPlayer && unit.ownerPlayer != null && ownerPlayer != null && unit.IsAlive) // Check if the unit is alive AND owners are different and not null
            {
                target = unit;
                return true;
            }

            // Check for enemy BaseController
            BaseController baseController = hitCollider.GetComponent<BaseController>();
            // TODO: Add health check for BaseController if it has one
            if (baseController != null && baseController.OwnerPlayer != ownerPlayer && ownerPlayer != null) // Check owner is not null before comparing
            {
                target = baseController;
                return true;
            }
        }

        return false;
    }

    // NOTE: This method should ONLY be called by server-authoritative code.
    // Damage synchronization is handled by the Health component's NetworkVariable.
    public void TakeDamage(int damage)
    {
        // Ensure health component exists before trying to apply damage
        if (health != null)
        {
            health.TakeDamage(damage);
        }
        else
        {
            DebugLogError($"Unit {NetworkObjectId} cannot take damage - Health component is null.");
        }
        // The death check is now handled by the HandleDeath method via the OnHealthDepleted event
    }

    // Called by the Health component's OnHealthDepleted event on both server and clients.
    private void HandleDeath()
    {
        // Server handles the authoritative state change and marks IsAlive.
        // Clients will update IsAlive via OnNetworkStateChanged when Dead state syncs.
        if (IsServer)
        {
            // Prevent trying to change state if already dead or dying
            if (currentState is not DeadState)
            {
                DebugLog($"[Server] Unit {gameObject.name} health depleted. Changing to DeadState.");
                ChangeState(new DeadState(this));
                IsAlive = false; // Mark as not alive authoritatively

                // TODO: Consider a ClientRpc for client-side death effects (particles, sound, ragdoll)?
                // PlayDeathEffectsClientRpc();
            }
        }

        // Client-side actions on death (like disabling local components immediately)
        // could potentially go here, outside the IsServer check, if needed before state sync.
        // Example:
        // if (IsClient) {
        //     GetComponent<Collider>().enabled = false; // Prevent further physics interactions locally
        // }

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
            Debug.Log($"[Unit] {message}");
        }
    }

    private void DebugLogUpdate(string message)
    {
        if (enableUpdateDebugLogs)
        {
            Debug.Log($"[Unit Update] {message}");
        }
    }

    private void DebugLogFixedUpdate(string message)
    {
        if (enableFixedUpdateDebugLogs)
        {
            Debug.Log($"[Unit FixedUpdate] {message}");
        }
    }

    private void DebugLogWarning(string message)
    {
        if (enableDebugLogs) // Or use a separate warning flag?
        {
            Debug.LogWarning($"[Unit Warning] {message}", this);
        }
    }

    private void DebugLogError(string message)
    {
        Debug.LogError($"[Unit Error] {message}", this);
    }
}