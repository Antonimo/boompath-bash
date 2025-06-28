/// <summary>
/// Simplified game state enumeration for NetworkedGameManager.
/// This replaces the complex state machine with a simple enum-based system.
/// </summary>
public enum GameState
{
    /// <summary>
    /// Waiting for all players to connect before starting.
    /// This is where the current player will request to spawn from host.
    /// </summary>
    WaitingForPlayers,

    /// <summary>
    /// Game is actively being played.
    /// </summary>
    Playing,

    /// <summary>
    /// Game has ended with a winner or draw.
    /// </summary>
    GameOver
}

/// <summary>
/// Player turn phases within the Playing state.
/// </summary>
public enum PlayerTurnPhase
{
    /// <summary>
    /// Player's turn is active and they can look around and select a unit.
    /// </summary>
    PlayerTurn,

    /// <summary>
    /// Player is drawing a path for the selected unit.
    /// </summary>
    DrawingPath,

    /// <summary>
    /// Player's turn is ending, transitioning to next player.
    /// </summary>
    PlayerTurnEnd
}