using UnityEngine;
using System.Collections;

namespace GameState
{
    public class PlayerTurnEndState : BaseGameState
    {
        public PlayerTurnEndState(GameManager gameManager) : base(gameManager) { }

        public override void Enter()
        {
            if (gameManager.EnableDebugLogs) Debug.Log("Entering player turn end state");

            // Start the transition sequence as a coroutine
            gameManager.StartCoroutine(TransitionSequence());
        }

        private IEnumerator TransitionSequence()
        {
            gameManager.SwitchCameraToCurrentPlayerBase();

            if (gameManager.CameraManager != null)
            {
                while (gameManager.CameraManager.IsTransitioning)
                {
                    yield return null;
                }
            }

            // Wait additional 2 seconds
            yield return new WaitForSeconds(2f);

            // Start the next player's turn
            gameManager.StartNextPlayerTurn();
        }
    }
}