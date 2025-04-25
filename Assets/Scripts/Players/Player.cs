using System.Collections.Generic;
using UnityEngine;
using System.Collections; // Required for Coroutines

public class Player : MonoBehaviour
{
    public string playerName;
    // Red: #FF0000
    // Blue: #00A1FF
    // Green: #00FF00
    // Yellow: #FFFF00
    // Purple: #A100A1
    // Orange: #FF7F00

    // Crimson Dusk: #8B1E3F
    // Steel Teal: #2B6A6E
    // Amber Glow: #D98736
    // Midnight Sapphire: #1F2A5E
    // Jade Mirage: #4A7043
    // Violet Nebula: #5C3A7D
    // Slate Phantom: #4B5357
    // Coral Flame: #E86A5B
    // Ochre Vanguard: #A67B2F
    // Frost Lilac: #8A7A9B
    // Saffron Blaze: #E8B923
    // Rose Nova: #D4608A

    public Color playerColor = Color.white;

    // Team
    public int teamId = 0;

    public bool IsBot = false;

    [SerializeField] private List<Unit> ownedUnits = new List<Unit>();
    public List<Unit> OwnedUnits => ownedUnits;

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