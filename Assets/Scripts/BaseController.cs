using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Health))] // Ensure Health component exists
public class BaseController : MonoBehaviour
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

    void Start()
    {
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

        // Spawn the first unit immediately
        SpawnUnit();
    }

    void Update()
    {
        if (ownerPlayer == null) return; // Need an owner to function

        // Check if we should start the spawn timer
        // Assumes Player script has HasPendingUnits() method
        if (!isSpawnTimerRunning && !ownerPlayer.HasPendingUnits())
        {
            spawnTimer = unitSpawnInterval;
            isSpawnTimerRunning = true;
            Debug.Log($"Starting spawn timer ({unitSpawnInterval}s).");
        }

        // If the timer is running, decrement it
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

        Vector3 spawnPosition = transform.position;
        GameObject newUnitObject = Instantiate(unitPrefab, spawnPosition, transform.rotation);

        // Set the parent to the owner player's transform
        newUnitObject.transform.SetParent(ownerPlayer.transform, worldPositionStays: true);

        // Add to local tracking list (consider removing if Player manages list solely)
        producedUnits.Add(newUnitObject);
        // Debug.Log($"Unit spawned at {spawnPosition}. Base produced units: {producedUnits.Count}");

        Unit unit = newUnitObject.GetComponent<Unit>();
        if (unit != null)
        {
            // Set the owner player on the unit
            unit.ownerPlayer = ownerPlayer;

            // Register unit with the player
            // Assumes Player script has AddUnit(Unit unit) method
            if (ownerPlayer != null)
            {
                ownerPlayer.AddUnit(unit);
            }

            // Initialize unit's state and movement (this should set IsPending = true)
            if (spawnTo != null)
            {
                unit.Initialize(spawnTo.position);
            }
            else
            {
                Debug.LogWarning($"SpawnTo transform not set for base {gameObject.name}. Unit initialized at base position.", this);
                unit.Initialize(transform.position); // Initialize at base if spawnTo is missing
            }

            // Update collision *after* Initialize has potentially set IsPending state
            UpdateUnitCollision(newUnitObject);
        }
        else
        {
            Debug.LogError("Spawned object is missing Unit component!", newUnitObject);
            // Clean up the failed spawn
            Destroy(newUnitObject);
            producedUnits.Remove(newUnitObject); // Remove from local list too
        }
    }

    // Check and update collision based on unit's isPending state
    public void UpdateUnitCollision(GameObject unitObject)
    {
        Unit unit = unitObject.GetComponent<Unit>();
        Collider unitCollider = unitObject.GetComponent<Collider>();

        // log to see if not null
        // Debug.Log($"UpdateUnitCollision: BaseCollider: {baseCollider}, UnitCollider: {unitCollider}, Unit: {unit}, IsPending: {unit?.IsPending}");

        if (baseCollider != null && unitCollider != null && unit != null)
        {
            // Ignore collision if the unit is pending (i.e., still inside/exiting the base)
            Physics.IgnoreCollision(baseCollider, unitCollider, unit.IsPending);
        }
    }

    void OnDestroy()
    {
        // Unsubscribe from the health depleted event to prevent memory leaks
        if (health != null)
        {
            health.OnHealthDepleted -= HandleDeath;
        }
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
            Gizmos.color = ownerPlayer.playerColor;
            Gizmos.DrawWireSphere(transform.position, 1.5f);

            // Draw a line to the spawnTo position if assigned
            if (spawnTo != null)
            {
                Gizmos.DrawLine(transform.position, spawnTo.position);
            }
        }
    }
}