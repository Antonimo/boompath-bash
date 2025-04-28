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
            unit.ownerPlayer = this;
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

}