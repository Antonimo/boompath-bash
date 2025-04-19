using UnityEngine;

public enum GameState
{
    PlayersTurn,  // Current player can tap pending units
    PathDrawing,  // Player is drawing a path for a selected unit
    GameOver      // Game has ended
}