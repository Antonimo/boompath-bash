using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

[RequireComponent(typeof(Health))] // Ensure Health component exists
public class BaseController : NetworkBehaviour
{
    [SerializeField] private Player ownerPlayer;  // Reference to the player who owns this base
    public Player OwnerPlayer => ownerPlayer;

    public GameObject unitPrefab;    // Reference to the Unit Prefab
    public Transform spawnTo;        // Where the spawned Unit will go when coming out of the base

    [Header("Unit Production")]
    public float unitSpawnInterval = 5f;  // Time between unit spawns
    public int maxUnits = 50;             // Maximum number of units player can have

    private float spawnTimer = 0f;        // Timer for next spawn
    private bool isSpawnTimerRunning = false; // Tracks if spawn cooldown is active
    // TODO: If Player script reliably manages the unit list, this local list might be redundant.
    private List<GameObject> producedUnits = new List<GameObject>(); // List of units produced by *this* base

    private Collider baseCollider;
    [SerializeField] private Health health;

    void Awake() // Use Awake for component fetching
    {
        baseCollider = GetComponent<Collider>();
        health = GetComponent<Health>(); // Get the Health component

        // Auto-find player if not set through inspector
        if (ownerPlayer == null)
        {
            // Try to find player by parent relationship
            ownerPlayer = GetComponentInParent<Player>();

            if (ownerPlayer == null)
            {
                Debug.LogError($"Base {gameObject.name} has no owner player assigned!", this);
                return; // Cannot operate without an owner
            }
            else
            {
                Debug.Log($"Base automatically assigned to player: {ownerPlayer.playerName}");
            }
        }

        // Subscribe to the health depleted event
        if (health != null)
        {
            health.OnHealthDepleted += HandleDeath;
        }
        else
        {
            Debug.LogError("BaseController requires a Health component!", this);
        }

    }

    void OnEnable()
    {
        Debug.Log($"Base {gameObject.name} enabled.");

        SpawnUnit();
    }

    void Update()
    {
        if (!IsOwner) return; // Only server/host controls spawning
        if (ownerPlayer == null) return;

        // Check if we should start the spawn timer
        // Assumes Player script has HasPendingUnits() method
        if (!isSpawnTimerRunning && !ownerPlayer.HasPendingUnits())
        {
            spawnTimer = unitSpawnInterval;
            isSpawnTimerRunning = true;
            // Debug.Log($"Starting spawn timer ({unitSpawnInterval}s).");
        }

        if (isSpawnTimerRunning)
        {
            spawnTimer -= Time.deltaTime;
            // Debug.Log($"Spawn timer: {spawnTimer:F2}"); // Optional: Debug timer

            // When timer finishes, check if we can spawn and then spawn
            if (spawnTimer <= 0f)
            {
                isSpawnTimerRunning = false; // Stop timer regardless of spawn success
                // Debug.Log("Spawn timer finished.");

                if (CanProduceMoreUnits())
                {
                    SpawnUnit();
                }
                // else { Debug.Log("Cannot produce more units right now."); }
            }
        }
    }

    bool CanProduceMoreUnits()
    {
        if (ownerPlayer == null) return false; // Should not happen if Start() checks, but good practice

        // Clean up destroyed units from the local list
        producedUnits.RemoveAll(unit => unit == null);

        // Check against max units using the Player's count
        // Assumes Player script has GetUnitCount() method
        if (maxUnits > 0 && ownerPlayer.GetUnitCount() >= maxUnits)
        {
            // Debug.Log($"Max units ({maxUnits}) reached for player {ownerPlayer.playerName}.");
            return false;
        }

        return true;
    }

    void SpawnUnit()
    {
        if (!IsServer) return; // Spawning should only happen on the server

        // Log spawnTo.position
        Debug.Log($"SpawnUnit: spawnTo.position: {spawnTo.position}");

        if (ownerPlayer == null)
        {
            Debug.LogError("Cannot spawn unit without an ownerPlayer!", this);
            return;
        }
        if (unitPrefab == null)
        {
            Debug.LogError("Unit Prefab is not assigned in the inspector!", this);
            return;
        }
        if (!CanProduceMoreUnits()) // Added check here before instantiation
        {
            // Debug.Log("SpawnUnit called, but cannot produce more units.");
            return;
        }

        Vector3 spawnPosition = transform.position;
        // Instantiate the unit locally on the server first
        GameObject newUnitObject = Instantiate(unitPrefab, spawnPosition, transform.rotation);

        Debug.Log($"SpawnUnit: newUnitObject: {newUnitObject}");

        // Get the NetworkObject component from the instantiated object
        NetworkObject unitNetworkObject = newUnitObject.GetComponent<NetworkObject>();
        if (unitNetworkObject == null)
        {
            Debug.LogError("Spawned Unit Prefab is missing a NetworkObject component!", newUnitObject);
            Destroy(newUnitObject); // Clean up the failed instantiation
            return;
        }

        // Spawn the object across the network. This must be done BEFORE setting parent or modifying NetworkBehaviours.
        // Ownership is implicitly assigned to the server here. If clients needed ownership, use SpawnWithOwnership.
        unitNetworkObject.Spawn(true); // Pass true to automatically destroy with the scene if the server stops

        Debug.Log($"Spawned Unit {unitNetworkObject.NetworkObjectId}");

        // --- Configuration AFTER Spawning ---

        // Get the Unit component AFTER spawning
        Unit unit = newUnitObject.GetComponent<Unit>();
        if (unit == null)
        {
            Debug.LogError("Spawned object is missing Unit component!", newUnitObject);
            // Clean up the failed spawn - Despawn first!
            if (unitNetworkObject.IsSpawned)
            {
                unitNetworkObject.Despawn(true);
            }
            else // If spawn failed before the network call
            {
                Destroy(newUnitObject);
            }
            return; // Stop further processing for this unit
        }

        // *** Call InitializeOwnerPlayer immediately after getting the component ***
        // This sets the local owner and replicates the OwnerPlayerId NetworkVariable.
        unit.InitializeOwnerPlayer(ownerPlayer);

        // Set the parent *after* spawning and initializing owner.
        // Consider if parenting is strictly necessary or if owner reference is enough.
        // Parenting networked objects can sometimes have implications. Let's keep it for now.
        newUnitObject.transform.SetParent(ownerPlayer.transform, worldPositionStays: true);

        // Add to local tracking list (consider removing if Player manages list solely)
        // This list should probably only exist on the server if it's needed at all.
        producedUnits.Add(newUnitObject);
        // Debug.Log($"Unit spawned at {spawnPosition}. Base produced units: {producedUnits.Count}");

        // Register unit with the player (likely server-side logic)
        if (ownerPlayer != null)
        {
            ownerPlayer.AddUnit(unit);
        }

        unit.ChangeState(new GoToLocationState(unit, spawnTo.position));
    }

    void OnDisable()
    {
        // Optional: Add logic here if something needs to happen when the base is disabled
        // For example, stopping timers or coroutines explicitly.
        Debug.Log($"Base {gameObject.name} disabled.");
        isSpawnTimerRunning = false; // Stop the timer if disabled mid-countdown
    }

    public override void OnDestroy()
    {
        // Unsubscribe from the health depleted event to prevent memory leaks
        if (health != null)
        {
            health.OnHealthDepleted -= HandleDeath;
        }
        // TODO: is this needed?
        base.OnDestroy(); // Call base implementation
    }

    // Method to handle base destruction when health is depleted
    private void HandleDeath()
    {
        Debug.Log($"Base {gameObject.name} belonging to {ownerPlayer?.playerName ?? "Unknown"} has been destroyed!");

        // TODO: Add visual/audio effects for destruction
        // TODO: Notify GameManager about base destruction (e.g., check for win/loss conditions)

        // Disable the base object or destroy it
        // Destroy(gameObject);
        // For now, just disable components to stop functionality like spawning
        this.enabled = false; // Disable this script
        if (baseCollider != null) baseCollider.enabled = false; // Disable collider
        // Optionally disable renderer etc.
    }

    // Used for game state visualization
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
}