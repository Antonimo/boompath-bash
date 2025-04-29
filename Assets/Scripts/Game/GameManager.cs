using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameState;

public class GameManager : MonoBehaviour
{
    private GameStateMachine stateMachine;
    public GameStateMachine StateMachine => stateMachine;

    [SerializeField] private GameStateType initialGameState = GameStateType.WaitingForPlayers;

    // Expose current state in inspector
    public GameStateType CurrentState => stateMachine?.CurrentStateType ?? GameStateType.GameStart;

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

    // Debugging
    [SerializeField] private bool enableDebugLogs = true;
    public bool EnableDebugLogs => enableDebugLogs;

    private void Awake()
    {
        InitializeStateMachine();
    }

    private void Start()
    {
        // Get players from playersParent if provided
        if (playersParent != null && players.Count == 0)
        {
            players.AddRange(playersParent.GetComponentsInChildren<Player>());
        }

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
    }

    private void InitializeStateMachine()
    {
        stateMachine = new GameStateMachine();

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
        if (selectedUnit != null)
        {
            selectedUnit.FollowPath(path);
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

        Debug.Log($"[GameManager] Set up local player: {localPlayer.playerName} at index {currentPlayerIndex}");
    }

    public void StartGame()
    {
        stateMachine.ChangeState(GameStateType.GameStart);
    }
}