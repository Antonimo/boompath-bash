using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    public string playerName;
    public Color playerColor = Color.white;

    // Team
    public int teamId = 0;

    [SerializeField] private List<UnitController> ownedUnits = new List<UnitController>();
    public List<UnitController> OwnedUnits => ownedUnits;

    // Reference to player's base
    public Transform playerBase;

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
        foreach (UnitController unit in ownedUnits)
        {
            if (unit.isPending)
            {
                return true;
            }
        }
        return false;
    }

    // Method to add a new unit to this player
    public void AddUnit(UnitController unit)
    {
        if (!ownedUnits.Contains(unit))
        {
            unit.ownerPlayer = this;
            ownedUnits.Add(unit);
        }
    }
}