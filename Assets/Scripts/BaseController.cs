using UnityEngine;
using System.Collections.Generic;

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
    private bool producingUnit = false;   // Whether the base is ready to produce a unit
    private List<GameObject> producedUnits = new List<GameObject>(); // List of all units produced
    
    private Collider baseCollider;

    void Start()
    {
        baseCollider = GetComponent<Collider>();
        
        // Auto-find player if not set through inspector
        if (ownerPlayer == null) 
        {
            // Try to find player by parent relationship
            ownerPlayer = GetComponentInParent<Player>();
            
            if (ownerPlayer == null)
            {
                Debug.LogError($"Base {gameObject.name} has no owner player assigned!", this);
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
        if (!producingUnit) return; // Wait until ready to produce

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f && CanProduceMoreUnits())
        {
            producingUnit = false; // Prevent spawning until the current unit moves
            SpawnUnit();
        }
    }

    bool CanProduceMoreUnits()
    {
        // Check if we've reached the maximum unit count
        if (maxUnits > 0 && producedUnits.Count >= maxUnits)
        {
            return false;
        }
        
        // Clean up destroyed units from the list
        producedUnits.RemoveAll(unit => unit == null);
        
        return true;
    }

    void SpawnUnit()
    {
        Vector3 spawnPosition = transform.position;
        GameObject newUnit = Instantiate(unitPrefab, spawnPosition, transform.rotation);
        
        // Set the parent while preserving world position, rotation and scale
        newUnit.transform.SetParent(transform, worldPositionStays: true);
        
        producedUnits.Add(newUnit);
        Debug.Log($"Unit spawned at {spawnPosition}. Total units: {producedUnits.Count}");

        // Disable collision if unit is pending
        UpdateUnitCollision(newUnit);

        UnitController unitController = newUnit.GetComponent<UnitController>();
        if (unitController != null)
        {
            // Set the owner player
            unitController.ownerPlayer = ownerPlayer;

            // Register with player if available
            // if (ownerPlayer != null)
            // {
            //     ownerPlayer.AddUnit(unitController);
            // }
            
            // Initialize movement to spawnTo position
            unitController.Initialize(spawnTo.position);
        }
    }

    // Check and update collision based on unit's isPending state
    public void UpdateUnitCollision(GameObject unit)
    {
        UnitController unitController = unit.GetComponent<UnitController>();
        Collider unitCollider = unit.GetComponent<Collider>();

        // log to see if not null
        Debug.Log($"BaseCollider: {baseCollider}, UnitCollider: {unitCollider}, UnitController: {unitController}");
        if (baseCollider != null && unitCollider != null && unitController != null)
        {
            Physics.IgnoreCollision(baseCollider, unitCollider, unitController.isPending);
        }
    }

    // Called by the unit when it starts moving
    public void UnitStartedMoving()
    {
        producingUnit = true; // Allow spawning the next unit
        spawnTimer = unitSpawnInterval; // Start the timer
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