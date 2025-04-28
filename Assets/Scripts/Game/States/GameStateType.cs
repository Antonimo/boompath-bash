namespace GameState
{
    public enum GameStateType
    {
        /// <see cref="WaitingForPlayersState"/>
        WaitingForPlayers,
        /// <see cref="GameStartState"/>
        GameStart,
        /// <see cref="PlayerTurnState"/>
        PlayerTurn,
        /// <see cref="PathDrawingState"/>
        PathDrawing,
        /// <see cref="PlayerTurnEndState"/>
        PlayerTurnEnd,
        /// <see cref="GameOverState"/>
        GameOver,
        /// <see cref="PausedState"/>
        Paused
    }
}