using UnityEngine;
using System.Collections;

namespace GameState
{
    /// <summary>
    /// LoadingState handles the initial loading phase after match initialization:
    /// - Transitions to WaitingForPlayers when loading is complete
    /// </summary>
    public class LoadingState : BaseGameState
    {
        private bool loadingComplete = false;

        public LoadingState(GameManager gameManager) : base(gameManager) { }

        public override void Enter()
        {
            if (gameManager.EnableDebugLogs) Debug.Log("[LoadingState] Entering Loading state");

            loadingComplete = false;

            gameManager.ClearPlayers();

            // Start the loading sequence
            gameManager.StartCoroutine(LoadingSequence());
        }

        public override void Update()
        {
            if (loadingComplete)
            {
                gameManager.StateMachine.ChangeState(GameStateType.WaitingForPlayers);
            }
        }

        public override void Exit()
        {
            if (gameManager.EnableDebugLogs) Debug.Log("[LoadingState] Exiting Loading state");
        }

        private IEnumerator LoadingSequence()
        {
            if (gameManager.EnableDebugLogs) Debug.Log("[LoadingState] Starting loading sequence...");

            // TODO: Future enhancements can add:
            // - Loading map geometry and assets
            // - Preloading audio clips and materials
            // - Setting up terrain or procedural content
            // - Initializing game-specific systems

            // Simple loading delay to simulate loading time
            yield return new WaitForSeconds(4f);

            loadingComplete = true;
            if (gameManager.EnableDebugLogs) Debug.Log("[LoadingState] Loading complete, transitioning to WaitingForPlayers");
        }

        /// <summary>
        /// Force complete loading (for testing purposes)
        /// </summary>
        public void ForceCompleteLoading()
        {
            loadingComplete = true;
            if (gameManager.EnableDebugLogs) Debug.Log("[LoadingState] ForceCompleteLoading called");
        }
    }
}