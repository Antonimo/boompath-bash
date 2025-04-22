using UnityEngine;

namespace GameState
{
    public abstract class BaseGameState : IGameState
    {
        protected GameManager gameManager;

        public BaseGameState(GameManager gameManager)
        {
            this.gameManager = gameManager;
        }

        public virtual void Enter() { }
        public virtual void Exit() { }
        public virtual void Update() { }
        public virtual void HandleInput() { }
    }
}