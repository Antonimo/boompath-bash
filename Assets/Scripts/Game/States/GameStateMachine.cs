using System.Collections.Generic;
using UnityEngine;

namespace GameState
{
    public class GameStateMachine
    {
        private Dictionary<GameStateType, IGameState> states = new Dictionary<GameStateType, IGameState>();
        private IGameState currentState;
        public GameStateType CurrentStateType { get; private set; }

        public void AddState(GameStateType stateType, IGameState state)
        {
            states[stateType] = state;
        }

        public void Initialize(GameStateType initialState)
        {
            if (states.TryGetValue(initialState, out IGameState state))
            {
                CurrentStateType = initialState;
                currentState = state;
                currentState.Enter();
            }
            else
            {
                Debug.LogError($"Failed to initialize state machine: State {initialState} not found");
            }
        }

        public void ChangeState(GameStateType newStateType)
        {
            if (currentState == null)
            {
                Debug.LogError("Cannot change state: No current state set");
                return;
            }

            if (!states.TryGetValue(newStateType, out IGameState newState))
            {
                Debug.LogError($"State {newStateType} not found");
                return;
            }

            currentState.Exit();
            CurrentStateType = newStateType;
            currentState = newState;
            currentState.Enter();
        }

        public void Update()
        {
            currentState?.Update();
        }

        public void HandleInput()
        {
            currentState?.HandleInput();
        }
    }
}