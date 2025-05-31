using UnityEngine;

namespace GameState
{
    public class GameOverState : BaseGameState
    {
        public GameOverState(GameManager gameManager) : base(gameManager) { }

        public override void Enter()
        {
            if (gameManager.EnableDebugLogs) Debug.Log("Entering game over state");

            // Disable player input
            if (gameManager.PlayerTurn != null)
            {
                gameManager.PlayerTurn.enabled = false;
            }

            // TODO: disable all UI controllers (path drawing, player turn, etc.)

            // TODO: update all units state

            // TODO: switch to "Game Over" camera? (same as main menu camera?)

            // TODO: disable bots?

            // ------
            // TODO: Show game over UI
            // TODO: Display winner
            // TODO: Add restart/quit options
        }

        public override void HandleInput()
        {
            /*
            // Handle input for restart/quit options
            if (Input.GetKeyDown(KeyCode.R))
            {
                // Restart game
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                );
            }
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                // Quit game
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
            */
        }
    }
}