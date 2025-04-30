using System.Collections.Generic;
using UnityEngine;
using System.Collections; // Required for Coroutines
using Unity.Netcode; // Added for Netcode

public class Player : NetworkBehaviour
{
    public string playerName;

    // Use NetworkVariables for synchronized state
    // Server sets these, clients automatically receive updates.
    // ReadPermission.Everyone allows clients (and server) to read the value.
    // WritePermission.Server ensures only the server can change the value.
    public NetworkVariable<Color> PlayerColor = new NetworkVariable<Color>(Color.white, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public Color CurrentPlayerColor => PlayerColor.Value;

    public NetworkVariable<int> TeamId = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public int CurrentTeamId => TeamId.Value;

    public bool IsBot = false;

    [SerializeField] private List<Unit> ownedUnits = new List<Unit>();
    public List<Unit> OwnedUnits => ownedUnits;

    // Reference to player's base
    // public Transform playerBase;

    // Called by the server (PlayerAssignmentManager) after spawn to initialize state
    public void Setup(Color initialColor, int initialTeamId)
    {
        if (!IsServer) return; // Only server should execute this

        PlayerColor.Value = initialColor;
        TeamId.Value = initialTeamId;

        Debug.Log($"Player {OwnerClientId} Setup complete. Color: {PlayerColor.Value}, TeamId: {TeamId.Value}");

        // You could potentially also trigger initial visual updates here if needed immediately on the server
    }


    /* Example handlers for NetworkVariable changes (optional)
    private void OnColorChanged(Color previousValue, Color newValue)
    {
        Debug.Log($"Player {OwnerClientId} color changed to {newValue}");
        // Update player visuals, materials, etc.
    }

    private void OnTeamIdChanged(int previousValue, int newValue)
    {
        Debug.Log($"Player {OwnerClientId} team ID changed to {newValue}");
        // Update team-related logic or visuals
    }
    */

    /* Remember to unsubscribe from OnValueChanged events in OnNetworkDespawn or OnDestroy
    public override void OnNetworkDespawn()
    {
        // Example: PlayerColor.OnValueChanged -= OnColorChanged;
        // Example: TeamId.OnValueChanged -= OnTeamIdChanged;
        base.OnNetworkDespawn();
    }
    */

    private void Start()
    {
        // Find all units that belong to this player if not assigned
        if (ownedUnits.Count == 0)
        {
            // UnitController[] allUnits = FindObjectsOfType<UnitController>();
            // foreach (UnitController unit in allUnits)
            // {
            //     if (unit.ownerPlayer == this)
            //     {
            //         ownedUnits.Add(unit);
            //     }
            // }
        }
    }

    // Check if this player has any pending units
    public bool HasPendingUnits()
    {
        foreach (Unit unit in ownedUnits)
        {
            // Ensure unit is not null before checking IsPending
            if (unit != null && unit.IsPending)
            {
                return true;
            }
        }
        return false;
    }

    // Method to add a new unit to this player
    public void AddUnit(Unit unit)
    {
        if (!ownedUnits.Contains(unit))
        {
            ownedUnits.Add(unit);
        }
    }

    // Method to get the current count of units owned by this player
    public int GetUnitCount()
    {
        // Clean up any null references (destroyed units) before counting
        ownedUnits.RemoveAll(unit => unit == null);
        return ownedUnits.Count;
    }

    // --- Path Assignment RPC Flow ---

    /// <summary>
    /// Client-side method called by GameManager when the player confirms a path for a specific unit.
    /// </summary>
    /// <param name="unitNetworkId">The NetworkObjectId of the unit to assign the path to.</param>
    /// <param name="pathPoints">The list of Vector3 points defining the path.</param>
    public void RequestUnitPathAssignment(ulong unitNetworkId, List<Vector3> pathPoints)
    {
        // This method executes on the *local* client instance of the Player.
        if (!IsOwner)
        {
            Debug.LogError($"RequestUnitPathAssignment called on Player object that is not the owner! OwnerClientId: {OwnerClientId}, LocalClientId: {NetworkManager.Singleton.LocalClientId}");
            return;
        }

        if (unitNetworkId == 0)
        {
            Debug.LogWarning($"[Player {OwnerClientId} Client] RequestUnitPathAssignment called with invalid unitNetworkId (0).");
            return;
        }

        if (pathPoints == null || pathPoints.Count < 2) // Need at least start and one point
        {
            Debug.LogWarning($"[Player {OwnerClientId} Client] RequestUnitPathAssignment called for Unit {unitNetworkId} with invalid path (null or < 2 points).");
            return;
        }

        Debug.Log($"[Player {OwnerClientId} Client] Calling AssignPathToServerRpc for Unit {unitNetworkId} with {pathPoints.Count} points.");
        // Call the ServerRpc, passing the ID of the unit and the path points.
        AssignPathToServerRpc(unitNetworkId, pathPoints.ToArray());
    }

    [ServerRpc]
    private void AssignPathToServerRpc(ulong unitNetworkId, Vector3[] pathArray)
    {
        // This code executes ONLY on the server instance of this Player object.
        Debug.Log($"[Player {OwnerClientId} Server] Received AssignPathToServerRpc for Unit {unitNetworkId}.");

        // Find the target Unit's NetworkObject on the server
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(unitNetworkId, out NetworkObject unitNetworkObject))
        {
            Unit targetUnit = unitNetworkObject.GetComponent<Unit>();
            if (targetUnit != null)
            {
                // --- Security/Ownership Check ---
                // Verify that the Player sending this RPC actually owns the target unit.
                if (targetUnit.OwnerPlayerId.Value == this.NetworkObjectId)
                {
                    Debug.Log($"[Player {OwnerClientId} Server] Ownership confirmed. Calling FollowPath on Unit {unitNetworkId}.");
                    // Call the Unit's method to process the path (this will run server-side)
                    targetUnit.FollowPath(new List<Vector3>(pathArray));
                }
                else
                {
                    Debug.LogError($"[Player {OwnerClientId} Server] Security violation: Tried to submit path for Unit {unitNetworkId}, but unit owner is {targetUnit.OwnerPlayerId.Value}, not {this.NetworkObjectId}. Ignoring.");
                }
            }
            else
            {
                Debug.LogError($"[Player {OwnerClientId} Server] Found NetworkObject for Unit {unitNetworkId}, but it has no Unit component. Ignoring path.");
            }
        }
        else
        {
            Debug.LogWarning($"[Player {OwnerClientId} Server] Could not find spawned NetworkObject for Unit {unitNetworkId}. Unit might have been destroyed before path arrived. Ignoring path.");
        }
    }
    // --- End Path Assignment RPC Flow ---

}