using UnityEngine;

namespace GameState
{
    public class PausedState : BaseGameState
    {
        private GameStateType previousState;

        public PausedState(GameManager gameManager) : base(gameManager) { }

        public override void Enter()
        {
            if (gameManager.EnableDebugLogs) Debug.Log("Entering paused state");

            // Store the previous state
            previousState = gameManager.StateMachine.CurrentStateType;

            // Pause the game
            Time.timeScale = 0f;

            // TODO: Show pause menu UI
        }

        public override void Exit()
        {
            // Resume the game
            Time.timeScale = 1f;

            // TODO: Hide pause menu UI
        }

        public override void HandleInput()
        {
            // Handle pause menu input
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                // Resume game
                gameManager.StateMachine.ChangeState(previousState);
            }
            else if (Input.GetKeyDown(KeyCode.R))
            {
                // Restart game
                Time.timeScale = 1f; // Reset time scale before reloading
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                );
            }
            else if (Input.GetKeyDown(KeyCode.Q))
            {
                // Quit game
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }
    }
}