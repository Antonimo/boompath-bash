using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameState
{
    public class GameStateMachine
    {
        private Dictionary<GameStateType, IGameState> states = new Dictionary<GameStateType, IGameState>();
        private IGameState currentState;
        public GameStateType CurrentStateType { get; private set; }

        // Event for broadcasting state changes
        public event Action<GameStateType, GameStateType> OnStateChanged; // (fromState, toState)

        // Network-relevant states that should be communicated across clients
        private static readonly HashSet<GameStateType> NetworkRelevantStates = new HashSet<GameStateType>
        {
            GameStateType.WaitingForPlayers,
            GameStateType.Paused,
            GameStateType.GameOver
        };

        // Event specifically for network-relevant state changes
        public event Action<GameStateType, GameStateType> OnNetworkRelevantGameStateChanged; // (fromState, toState)

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
            Debug.Log($"GameStateMachine: ChangeState: {newStateType}");

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

            GameStateType previousState = CurrentStateType;

            currentState.Exit();
            CurrentStateType = newStateType;
            currentState = newState;
            currentState.Enter();

            // Broadcast the state change
            OnStateChanged?.Invoke(previousState, newStateType);

            Debug.Log($"GameStateMachine: ChangeState: {newStateType}: NetworkRelevantStates.Contains(newStateType): {NetworkRelevantStates.Contains(newStateType)}");

            // Broadcast network-relevant state changes
            if (NetworkRelevantStates.Contains(newStateType))
            {
                OnNetworkRelevantGameStateChanged?.Invoke(previousState, newStateType);
            }
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