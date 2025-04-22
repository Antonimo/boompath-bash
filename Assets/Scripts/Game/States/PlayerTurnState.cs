using UnityEngine;

namespace GameState
{
    public class PlayerTurnState : BaseGameState
    {
        public PlayerTurnState(GameManager gameManager) : base(gameManager) { }

        public override void Enter()
        {
            Debug.Log($"Starting {gameManager.CurrentPlayer.playerName}'s turn");

            // Reset turn variables
            gameManager.ResetSelectedUnit();

            // Switch camera to the current player's base
            gameManager.SwitchCameraToCurrentPlayerBase();

            // TODO: gameManager.EnablePlayerTurn() ?
            if (gameManager.PlayerTurn != null)
            {
                gameManager.PlayerTurn.player = gameManager.CurrentPlayer;
                gameManager.PlayerTurn.enabled = true;
            }
        }

        public override void Exit()
        {
            if (gameManager.PlayerTurn != null)
            {
                gameManager.PlayerTurn.enabled = false;
            }
        }
    }
}