using UnityEngine;

namespace GameState
{
    public interface IGameState
    {
        void Enter();
        void Exit();
        void Update();
        void HandleInput();
    }
}