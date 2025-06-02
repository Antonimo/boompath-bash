using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameState;

/// <summary>
/// GameManager is a focused, low-level component responsible for core in-game mechanics and local game flow.
/// 
/// Key Responsibilities:
/// - Manages game state transitions (waiting, player turns, path drawing, game over, etc.)
/// - Handles local player interactions (unit selection, path drawing confirmation)
/// - Manages in-game objects: Bases, Units, Camera positioning
/// - Controls game flow and turn management for the current client
/// 
/// Architecture Position:
/// - This is a LOWER-LAYER component with a single, well-defined responsibility
/// - Operates independently without knowledge of higher-level networking or match orchestration systems
/// - Designed to be controlled by higher-level managers that handle network coordination
/// - This is NOT a NetworkBehaviour - it focuses purely on local game mechanics
/// 
/// Separation of Concerns:
/// - GameManager handles "what happens in the game" (game rules, player actions, object interactions)
/// - Higher layers handle "how the game is coordinated" (networking, lobbies, match setup)
/// - This component should remain unaware of network topology, match management, or lobby systems
/// - Receives commands from above but doesn't initiate network operations or match-level decisions
/// 
/// Network vs Local Gameplay:
/// - In networked games: Manages local player experience while higher layers coordinate multiplayer
///   (the turn flow repeats for the same player in network mode)
/// - In local games: Would manage multiple players on the same screen, switching turns between them
/// 
/// </summary>
public class GameManager : MonoBehaviour
{
    private GameStateMachine stateMachine;
    public GameStateMachine StateMachine => stateMachine;

    [SerializeField] private GameStateType initialGameState = GameStateType.Loading;

    // TODO: Expose current state in inspector - This does not work..
    // Current state accessor
    public GameStateType CurrentState => stateMachine?.CurrentStateType ?? GameStateType.Loading;

    public bool IsHost = true;

    // Player management
    [SerializeField] private GameObject playersParent;
    [SerializeField] private List<Player> players = new List<Player>();
    [SerializeField] private int currentPlayerIndex = 0;
    public Player CurrentPlayer => players[currentPlayerIndex];

    // Selected unit for path drawing
    [SerializeField] private Unit selectedUnit;
    public Unit SelectedUnit => selectedUnit;

    // Components
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private SimplePathDrawing pathDrawing;
    [SerializeField] private PlayerTurn playerTurn;

    // Public accessors for states
    public CameraManager CameraManager => cameraManager;
    public SimplePathDrawing PathDrawing => pathDrawing;
    public PlayerTurn PlayerTurn => playerTurn;

    // Winner tracking
    private Player winnerPlayer = null;
    public Player WinnerPlayer => winnerPlayer;

    // Debugging (should be always last)
    [SerializeField] private bool enableDebugLogs = true;
    public bool EnableDebugLogs => enableDebugLogs;

    private void ValidateDependencies()
    {
        if (playersParent == null)
        {
            Debug.LogError("GameManager: playersParent is not assigned. Please assign a parent object for players.");
        }
    }

    private void Awake()
    {
        ValidateDependencies();
        InitializeStateMachine();
    }

    private void Start()
    {
        InitializePlayers();
        // if (players.Count == 0)
        // {
        //     Debug.LogError("No players found in the scene. Please assign players or a players parent.");
        //     return;
        // }

        StartCoroutine(DelayedInitialization());
    }

    private void Update()
    {
        stateMachine?.Update();
    }

    private void LateUpdate()
    {
        stateMachine?.HandleInput();

        if (IsHost)
        {
            CheckGameOverConditions();
        }
    }

    private void InitializeStateMachine()
    {
        stateMachine = new GameStateMachine();

        // TODO: review this:
        // TODO: this means all game states persist across session, is that good?
        // TODO: maybe its better to set a fresh instance of state when switching?
        stateMachine.AddState(GameStateType.Loading, new LoadingState(this));
        stateMachine.AddState(GameStateType.WaitingForPlayers, new WaitingForPlayersState(this));
        stateMachine.AddState(GameStateType.GameStart, new GameStartState(this));
        stateMachine.AddState(GameStateType.PlayerTurn, new PlayerTurnState(this));
        stateMachine.AddState(GameStateType.PathDrawing, new PathDrawingState(this));
        stateMachine.AddState(GameStateType.PlayerTurnEnd, new PlayerTurnEndState(this));
        stateMachine.AddState(GameStateType.GameOver, new GameOverState(this));
        stateMachine.AddState(GameStateType.Paused, new PausedState(this));
    }

    private IEnumerator DelayedInitialization()
    {
        yield return new WaitForEndOfFrame();

        stateMachine.Initialize(initialGameState);
    }

    // Public methods for state management

    public void LoadGame()
    {
        stateMachine.ChangeState(GameStateType.Loading);
    }

    public void ClearPlayers()
    {
        players.Clear();
    }

    public void InitializePlayers()
    {
        if (playersParent != null)
        {
            players.AddRange(playersParent.GetComponentsInChildren<Player>());
            Debug.Log($"[GameManager] Initialized players list. Found {players.Count} players.");
        }
        else
        {
            Debug.LogError("[GameManager] playersParent is not assigned. Cannot initialize players list.");
        }
    }

    public void ResetToFirstPlayer()
    {
        currentPlayerIndex = 0;
    }

    public void EnableBases()
    {
        if (playersParent != null)
        {
            BaseController[] allBases = playersParent.GetComponentsInChildren<BaseController>();
            foreach (var baseController in allBases)
            {
                if (baseController != null)
                {
                    baseController.enabled = true;
                }
            }
        }
        else
        {
            Debug.LogWarning("[GameManager] playersParent is not assigned. Cannot enable bases.");
        }
    }

    public void StartNextPlayerTurn()
    {
        if (players.Count == 0) return; // No players

        int initialIndex = currentPlayerIndex;
        int nextIndex = initialIndex;

        do
        {
            nextIndex = (nextIndex + 1) % players.Count;
            if (players[nextIndex] != null && !players[nextIndex].IsBot)
            {
                currentPlayerIndex = nextIndex;
                Debug.Log($"[GameManager] Starting turn for player: {players[currentPlayerIndex].playerName}");
                stateMachine.ChangeState(GameStateType.PlayerTurn);
                return; // Found next non-bot player
            }
        } while (nextIndex != initialIndex); // Loop until we've checked all players

        // If we reach here, it means all players are bots or there's only one player who is a bot.
        // Handle this case as needed (e.g., keep the current bot player's turn, end the game, etc.)
        Debug.Log("StartNextPlayerTurn: Could not find a non-bot player. Staying on the current player or check game logic.");
        // Optionally, you might still want to change state if the only player is a bot:
        // stateMachine.ChangeState(GameStateType.PlayerTurn);
    }

    public void SelectUnit(Unit unit)
    {
        if (unit.ownerPlayer == CurrentPlayer && unit.IsPending)
        {
            selectedUnit = unit;

            stateMachine.ChangeState(GameStateType.PathDrawing);
        }
    }

    public void ResetSelectedUnit()
    {
        selectedUnit = null;
    }

    public bool IsPointInsideEnemyBase(Vector3 point)
    {
        Collider[] hitColliders = Physics.OverlapSphere(point, 0.1f);
        foreach (var collider in hitColliders)
        {
            BaseController baseComponent = collider.GetComponent<BaseController>();
            if (baseComponent != null && baseComponent.OwnerPlayer != null)
            {
                if (baseComponent.OwnerPlayer.CurrentTeamId != CurrentPlayer.CurrentTeamId)
                {
                    return true;
                }
            }
        }
        return false;
    }

    // Called when path drawing is completed
    public void ConfirmPath(List<Vector3> path)
    {
        if (selectedUnit != null && CurrentPlayer != null)
        {
            // selectedUnit.FollowPath(path); // Old direct call

            // NEW: Call the method on the CurrentPlayer (which must be the local player)
            // This will trigger the ServerRpc flow.
            Debug.Log($"[GameManager] ConfirmPath: Requesting path assignment for Unit {selectedUnit.NetworkObjectId} via CurrentPlayer {CurrentPlayer.OwnerClientId}.");
            CurrentPlayer.RequestUnitPathAssignment(selectedUnit.NetworkObjectId, path);
        }
        else
        {
            if (selectedUnit == null) Debug.LogWarning("[GameManager] ConfirmPath called but selectedUnit is null.");
            if (CurrentPlayer == null) Debug.LogWarning("[GameManager] ConfirmPath called but CurrentPlayer is null.");
        }

        stateMachine.ChangeState(GameStateType.PlayerTurnEnd);
    }

    // Refactored camera switching logic
    public void SwitchCameraToCurrentPlayerBase()
    {
        if (CurrentPlayer == null)
        {
            Debug.LogError("Cannot switch camera, CurrentPlayer is null.");
            return;
        }

        // Find the current player's base camera position
        BaseController currentBase = CurrentPlayer.GetComponentInChildren<BaseController>();
        Transform cameraPosition = currentBase?.transform.Find("CameraPosition");

        if (cameraPosition == null)
        {
            Debug.LogWarning($"No CameraPosition found for player {CurrentPlayer.playerName}'s base. Using default camera position.");
        }

        if (CameraManager != null)
        {
            CameraManager.SwitchToMainCamera(cameraPosition); // Pass null if not found to use default
        }
        else
        {
            Debug.LogError("CameraManager is not assigned in GameManager.");
        }
    }

    // New methods for network integration
    public void SetupLocalPlayer(Player localPlayer)
    {
        if (!players.Contains(localPlayer))
        {
            players.Add(localPlayer);
        }

        currentPlayerIndex = players.IndexOf(localPlayer);

        Debug.Log($"[GameManager] Set up local player: {localPlayer.playerName} at index {currentPlayerIndex} (total players: {players.Count})");
    }

    public void StartGame()
    {
        stateMachine.ChangeState(GameStateType.GameStart);
    }

    public void GameOver()
    {
        stateMachine.ChangeState(GameStateType.GameOver);
    }

    public void Pause()
    {
        stateMachine.ChangeState(GameStateType.Paused);
    }

    private static bool CanEndGameFromState(GameStateType state)
    {
        switch (state)
        {
            case GameStateType.PlayerTurn:
            case GameStateType.PathDrawing:
            case GameStateType.PlayerTurnEnd:
            case GameStateType.Paused:
                return true;
            default:
                return false;
        }
    }

    private void CheckGameOverConditions()
    {
        // Debug.Log($"[GameManager] CheckGameOverConditions: CurrentState: {CurrentState}: CanEndGameFromState: {CanEndGameFromState(CurrentState)}");

        // TODO: host only? How does that work together with the idea that this is not a network object?
        if (!CanEndGameFromState(CurrentState))
        {
            return;
        }

        int aliveBases = 0;
        BaseController aliveBase = null;

        BaseController[] allBases = playersParent.GetComponentsInChildren<BaseController>();
        foreach (var baseController in allBases)
        {
            if (baseController.enabled && baseController.health.IsAlive)
            {
                aliveBases++;
                aliveBase = baseController; // Keep reference to the last alive base
            }
        }

        // Debug.Log($"[GameManager] CheckGameOverConditions: Alive bases: {aliveBases} out of {allBases.Length}");

        if (aliveBases <= 1)
        {
            // Determine the winner
            if (aliveBases == 1 && aliveBase != null)
            {
                winnerPlayer = aliveBase.OwnerPlayer;
                Debug.Log($"[GameManager] Game over - Winner: {winnerPlayer?.playerName ?? "Unknown"}");
            }
            else
            {
                winnerPlayer = null; // Draw/no winner
                Debug.Log("[GameManager] Game over - No winner (draw)");
            }

            // Debug.Log($"[GameManager] CheckGameOverConditions: Game over condition met. Changing state to GameOver.");
            stateMachine.ChangeState(GameStateType.GameOver);
        }
    }
}