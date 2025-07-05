using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class BaseController : NetworkBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private Player ownerPlayer;
    public Player OwnerPlayer => ownerPlayer;
    [SerializeField] private GameObject unitPrefab;
    [SerializeField] private Transform spawnTo; // Where the spawned Unit will go when coming out of the base
    // TODO: what is better? expose the health component or have this component implement an interface of Health, for IsAlive checks and such?
    // TODO: or should the acces be via the game object?
    public Health health;

    [Header("Unit Production")]
    [SerializeField] private float unitSpawnInterval = 5f;  // Time between unit spawns
    [SerializeField] private int maxUnits = 50;             // Maximum number of units player can have

    [Header("Debug Logging")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool enableUpdateDebugLogs = false; // Separate toggle for potentially noisy Update logs

    private float spawnTimer = 0f;        // Timer for next spawn
    private bool isSpawnTimerRunning = false; // Tracks if spawn cooldown is active
    // TODO: If Player script reliably manages the unit list, this local list might be redundant.
    private List<GameObject> producedUnits = new List<GameObject>(); // List of units produced by *this* base
    private Collider baseCollider;

    void Awake() // Use Awake for component fetching
    {
        baseCollider = GetComponent<Collider>();
        if (health == null) // Check if health was already assigned in Inspector
        {
            health = GetComponent<Health>(); // Get the Health component if not
        }

        // Auto-find player if not set through inspector
        if (ownerPlayer == null)
        {
            // Try to find player by parent relationship
            ownerPlayer = GetComponentInParent<Player>();

            if (ownerPlayer == null)
            {
                DebugLogError($"Base {gameObject.name} has no owner player assigned!");
                return; // Cannot operate without an owner
            }
            else
            {
                DebugLog($"Base automatically assigned to player: {ownerPlayer.OwnerClientId}");
            }
        }

        // Subscribe to the health depleted event
        if (health != null)
        {
            health.OnHealthDepleted += HandleDeath;
        }
        else
        {
            DebugLogError("BaseController requires a Health component!");
        }
    }

    void OnEnable()
    {
        DebugLog($"Base {gameObject.name} enabled.");
        if (IsServer)
        {
            SpawnUnit();
            // TODO: Start timer for next unit immediately after the first spawn on enable?
        }
    }

    void Update()
    {
        if (!IsServer) return;
        if (ownerPlayer == null) return; // Need an owner to manage unit counts

        DebugLogUpdate($"Update called, ownerPlayer: {ownerPlayer.OwnerClientId}, spawnTimer: {spawnTimer}, isSpawnTimerRunning: {isSpawnTimerRunning}, ownerPlayer.HasPendingUnits: {ownerPlayer.HasPendingUnits()}");

        // Check if we should start the spawn timer (only if it's not already running)
        if (!isSpawnTimerRunning && !ownerPlayer.HasPendingUnits())
        {
            StartSpawnTimer();
        }

        // Process the spawn timer if it's running
        if (isSpawnTimerRunning)
        {
            spawnTimer -= Time.deltaTime;
            // DebugLogUpdate($"Spawn timer: {spawnTimer:F2}");

            // When timer finishes, check if we can spawn and then spawn
            if (spawnTimer <= 0f)
            {
                isSpawnTimerRunning = false; // Stop timer regardless of spawn success
                DebugLogUpdate("Spawn timer finished.");

                SpawnUnit();
            }
        }
    }

    void StartSpawnTimer()
    {
        spawnTimer = unitSpawnInterval;
        isSpawnTimerRunning = true;
        DebugLogUpdate($"Starting spawn timer ({unitSpawnInterval}s).");
    }

    bool CanProduceMoreUnits()
    {
        if (ownerPlayer == null)
        {
            DebugLogWarning("Cannot check unit production: ownerPlayer is null.");
            return false;
        }

        // Clean up destroyed units from the local list (server-side check primarily)
        if (IsServer)
        {
            producedUnits.RemoveAll(unit => unit == null);
        }

        // Check against max units using the Player's count
        if (maxUnits > 0 && ownerPlayer.GetUnitCount() >= maxUnits)
        {
            DebugLog($"Max units ({maxUnits}) reached for player {ownerPlayer.OwnerClientId}.");
            return false;
        }

        return true;
    }

    void SpawnUnit()
    {
        if (!IsServer)
        {
            DebugLogWarning("SpawnUnit called on non-server client. Ignoring.");
            return; // Spawning should only happen on the server
        }

        // Use DebugLog for server-side logic flow
        DebugLog($"Attempting to spawn unit. Spawn target position: {spawnTo?.position.ToString() ?? "Not Set"}");

        if (ownerPlayer == null)
        {
            DebugLogError("Cannot spawn unit without an ownerPlayer!");
            return;
        }
        if (unitPrefab == null)
        {
            DebugLogError("Unit Prefab is not assigned in the inspector!");
            return;
        }
        if (spawnTo == null)
        {
            DebugLogError("SpawnTo transform is not assigned in the inspector!");
            return;
        }
        if (!CanProduceMoreUnits())
        {
            DebugLog("SpawnUnit called, but cannot produce more units at this moment.");
            return;
        }

        Vector3 spawnPosition = transform.position; // Spawn at base position
        GameObject newUnitObject = null; // Declare outside try-catch
        NetworkObject unitNetworkObject = null;

        try
        {
            // Instantiate the unit locally on the server first
            newUnitObject = Instantiate(unitPrefab, spawnPosition, transform.rotation);
            DebugLog($"Instantiated unit prefab: {newUnitObject.name}");

            // Get the NetworkObject component
            unitNetworkObject = newUnitObject.GetComponent<NetworkObject>();
            if (unitNetworkObject == null)
            {
                DebugLogError("Spawned Unit Prefab is missing a NetworkObject component!", newUnitObject);
                Destroy(newUnitObject); // Clean up the failed instantiation
                return;
            }

            // Spawn the object across the network. This must be done BEFORE setting parent or modifying NetworkBehaviours.
            // Ownership is implicitly assigned to the server here. If clients needed ownership, use SpawnWithOwnership.
            unitNetworkObject.Spawn(true); // Destroy with scene
            DebugLog($"Spawned NetworkObject Unit ID: {unitNetworkObject.NetworkObjectId}");

            // --- Configuration AFTER Spawning ---
            Unit unit = newUnitObject.GetComponent<Unit>();
            if (unit == null)
            {
                DebugLogError("Spawned object is missing Unit component!", newUnitObject);
                // Clean up the failed spawn - Despawn first!
                if (unitNetworkObject.IsSpawned) unitNetworkObject.Despawn(true);
                else Destroy(newUnitObject); // If spawn hadn't happened yet
                return;
            }

            // Initialize owner *immediately* after getting component and before parenting/adding
            unit.InitializeOwnerPlayer(ownerPlayer);
            DebugLog($"Initialized owner for unit {unitNetworkObject.NetworkObjectId} to {ownerPlayer.OwnerClientId}");

            // Set the parent *after* spawning and initializing owner.
            newUnitObject.transform.SetParent(ownerPlayer.transform, worldPositionStays: true);
            DebugLog($"Parented unit {unitNetworkObject.NetworkObjectId} under {ownerPlayer.gameObject.name}");


            // Add to local tracking list (server only)
            producedUnits.Add(newUnitObject);
            DebugLog($"Unit added to base's produced list. Count: {producedUnits.Count}");

            // Register unit with the player (server-side)
            ownerPlayer.AddUnit(unit);
            DebugLog($"Registered unit {unitNetworkObject.NetworkObjectId} with Player {ownerPlayer.OwnerClientId}.");


            // Assign initial state
            unit.ChangeState(new GoToLocationState(unit, spawnTo.position));
            DebugLog($"Set initial state GoToLocationState for unit {unitNetworkObject.NetworkObjectId} towards {spawnTo.position}");

        }
        catch (System.Exception e) // Catch potential errors during instantiation/setup
        {
            // Corrected multiline string formatting
            DebugLogError($"Exception during SpawnUnit: {e.Message}\n{e.StackTrace}");
            // Cleanup potentially partially spawned object
            if (unitNetworkObject != null && unitNetworkObject.IsSpawned)
            {
                unitNetworkObject.Despawn(true);
            }
            else if (newUnitObject != null)
            {
                Destroy(newUnitObject);
            }
        }
    }

    void OnDisable()
    {
        DebugLog($"Base {gameObject.name} disabled.");
        isSpawnTimerRunning = false; // Stop the timer if disabled mid-countdown
    }

    public override void OnDestroy()
    {
        DebugLog($"Base {gameObject.name} being destroyed.");
        if (health != null)
        {
            health.OnHealthDepleted -= HandleDeath;
        }
        base.OnDestroy();
    }

    private void HandleDeath()
    {
        DebugLog($"Base {gameObject.name} belonging to {ownerPlayer?.OwnerClientId.ToString() ?? "Unknown"} has been destroyed!");

        // TODO: Add visual/audio effects for destruction
        // TODO: Notify GameManager about base destruction (e.g., check for win/loss conditions)

        this.enabled = false;
        if (baseCollider != null) baseCollider.enabled = false;

        DebugLog($"Disabled BaseController script and Collider for destroyed base {gameObject.name}.");
    }

    void OnDrawGizmos()
    {
        if (ownerPlayer != null)
        {
            // Draw a colored sphere to identify owner
            Gizmos.color = ownerPlayer.CurrentPlayerColor;
            Gizmos.DrawWireSphere(transform.position, 1.5f);

            // Draw a line to the spawnTo position if assigned
            if (spawnTo != null)
            {
                Gizmos.DrawLine(transform.position, spawnTo.position);
            }
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[BaseController] {message}", this);
        }
    }

    private void DebugLogUpdate(string message)
    {
        if (enableUpdateDebugLogs)
        {
            Debug.Log($"[BaseController Update] {message}", this);
        }
    }

    private void DebugLogWarning(string message)
    {
        // Pass 'this' to allow clicking the log message to highlight the object
        Debug.LogWarning($"[BaseController Warning] {message}", this);
    }

    private void DebugLogError(string message)
    {
        // Pass 'this' to allow clicking the log message to highlight the object
        Debug.LogError($"[BaseController Error] {message}", this);
    }
    private void DebugLogError(string message, Object context)
    {
        Debug.LogError($"[BaseController Error] {message}", context);
    }
}