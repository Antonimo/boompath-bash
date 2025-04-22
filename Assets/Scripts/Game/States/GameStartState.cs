using UnityEngine;

namespace GameState
{
    public class GameStartState : BaseGameState
    {
        public GameStartState(GameManager gameManager) : base(gameManager) { }

        public override void Enter()
        {
            if (gameManager.EnableDebugLogs) Debug.Log("[GameStartState] Entering game start state");

            InitializeGameState();

            // Set up the initial camera position
            if (gameManager.CameraManager != null)
            {
                gameManager.CameraManager.SwitchToMainCamera();
            }

            // TODO: wait for camera transition to complete

            // Transition to the first player's turn
            gameManager.StateMachine.ChangeState(GameStateType.PlayerTurn);
        }

        public override void Exit()
        {
            if (gameManager.EnableDebugLogs) Debug.Log("[GameStartState] Exiting game start state");
        }

        private void InitializeGameState()
        {
            if (gameManager.EnableDebugLogs) Debug.Log("[GameStartState] Initializing game...");

            gameManager.ResetToFirstPlayer();

            if (gameManager.EnableDebugLogs) Debug.Log("[GameStartState] Game initialized");
        }
    }
}