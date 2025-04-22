using System.Collections.Generic;
using UnityEngine;

namespace GameState
{
    public class PathDrawingState : BaseGameState
    {
        public PathDrawingState(GameManager gameManager) : base(gameManager) { }

        public override void Enter()
        {
            if (gameManager.EnableDebugLogs) Debug.Log("Entering path drawing mode");

            if (gameManager.CameraManager != null)
            {
                gameManager.CameraManager.SwitchToPathDrawCamera();
            }

            if (gameManager.PathDrawing != null)
            {
                if (gameManager.SelectedUnit?.transform == null)
                {
                    Debug.LogError("Selected unit or its transform is null. Cannot enable path drawing.");
                    gameManager.StateMachine.ChangeState(GameStateType.PlayerTurn);
                    return;
                }

                gameManager.PathDrawing.pathStartPosition = gameManager.SelectedUnit.transform.position;
                gameManager.PathDrawing.enabled = true;
            }
        }

        public override void Exit()
        {
            if (gameManager.PathDrawing != null)
            {
                gameManager.PathDrawing.enabled = false;
            }
        }

        public override void HandleInput()
        {
            // Handle escape key to cancel path drawing
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelPathDrawing();
            }
        }

        private void CancelPathDrawing()
        {
            gameManager.ResetSelectedUnit();
            gameManager.StateMachine.ChangeState(GameStateType.PlayerTurn);
        }
    }
}