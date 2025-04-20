using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    // Current game state with event notification
    [SerializeField] private GameState currentState = GameState.PlayersTurn;
    public GameState CurrentState => currentState;

    // Player management
    [SerializeField] private Transform playersParent; // Reference to the Players parent GameObject
    [SerializeField] private List<Player> players = new List<Player>();
    [SerializeField] private int currentPlayerIndex = 0;
    public Player CurrentPlayer => players[currentPlayerIndex];

    // Selected unit for path drawing
    [SerializeField] private Unit selectedUnit;
    public Unit SelectedUnit => selectedUnit;

    // Other components
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private SimplePathDrawing pathDrawing;
    [SerializeField] private PlayerTurn playerTurn;

    // Debugging
    [SerializeField] private bool enableDebugLogs = true;

    private void Start()
    {
        // Get players from playersParent if provided
        if (playersParent != null && players.Count == 0)
        {
            players.AddRange(playersParent.GetComponentsInChildren<Player>());
        }

        if (players.Count == 0)
        {
            Debug.LogError("No players found in the scene. Please assign players or a players parent.");
            return;
        }

        StartCoroutine(DelayedInitialization());
    }

    private IEnumerator DelayedInitialization()
    {
        // Wait until the end of the frame when all Start methods have completed
        yield return new WaitForEndOfFrame();

        InitGame();

        SetInitialGameState(currentState);
    }

    private void InitGame()
    {
        if (enableDebugLogs) Debug.Log("[GameManager] Initializing game...");

        currentPlayerIndex = 0;

        // Reset turn variables
        // selectedUnit = null;

        if (enableDebugLogs) Debug.Log("[GameManager] Game initialized");
    }

    public void SetInitialGameState(GameState initialState)
    {
        currentState = initialState;

        switch (currentState)
        {
            case GameState.PlayersTurn:
                // Start the game with first player's turn
                StartPlayerTurn(0);
                break;

            case GameState.PathDrawing:
                cameraManager.SetActiveCameraToPathDraw();
                EnablePathDrawing();
                break;

            case GameState.GameOver:
                // Handle game over
                break;
        }
    }

    // Change to a new game state
    public void ChangeGameState(GameState newState)
    {
        if (currentState == newState) return;

        if (enableDebugLogs) Debug.Log($"[GameManager] State changing from {currentState} to {newState}");

        // Exit current state
        switch (currentState)
        {
            case GameState.PathDrawing:
                // Disable path drawing UI/components
                DisablePathDrawing();
                break;
        }

        // Set new state
        currentState = newState;

        // Enter new state
        switch (newState)
        {
            case GameState.PlayersTurn:
                // Set up player turn
                break;

            case GameState.PathDrawing:
                // Enable path drawing
                EnablePathDrawing();
                break;

            case GameState.GameOver:
                // Handle game over
                break;
        }
    }

    // Start a specific player's turn
    public void StartPlayerTurn(int playerIndex)
    {
        if (enableDebugLogs) Debug.Log($"Starting {players[currentPlayerIndex].playerName}'s turn");

        currentPlayerIndex = playerIndex;
        currentState = GameState.PlayersTurn;

        // Reset turn variables
        selectedUnit = null;

        // Switch to main game camera
        if (cameraManager != null)
        {
            cameraManager.SwitchToMainCamera();
        }

        if (playerTurn != null)
        {
            playerTurn.player = CurrentPlayer;
            playerTurn.enabled = true;
        }
    }

    // Switch to next player's turn
    public void NextPlayerTurn()
    {
        currentPlayerIndex = (currentPlayerIndex + 1) % players.Count;
        StartPlayerTurn(currentPlayerIndex);
    }

    // Select a unit and enter path drawing mode
    public void SelectUnit(Unit unit)
    {
        // Only allow selection if it's player's turn and unit belongs to current player
        if (currentState == GameState.PlayersTurn &&
            unit.ownerPlayer == CurrentPlayer &&
            unit.IsPending)
        {
            selectedUnit = unit;

            // Change to path drawing state
            ChangeGameState(GameState.PathDrawing);
        }
    }

    // Enable path drawing mode
    private void EnablePathDrawing()
    {
        if (enableDebugLogs) Debug.Log("Enabling path drawing mode");

        // Transition to path drawing camera
        if (cameraManager != null)
        {
            cameraManager.SwitchToPathDrawCamera();
        }

        if (pathDrawing != null)
        {
            if (selectedUnit?.transform == null)
            {
                Debug.LogError("Selected unit or its transform is null. Cannot enable path drawing.");
                return;
            }
            Debug.Log("Enabling path drawing component");
            pathDrawing.pathStartPosition = selectedUnit.transform.position;
            pathDrawing.enabled = true;
        }

        if (enableDebugLogs) Debug.Log("Path drawing mode enabled");
    }

    private void DisablePathDrawing()
    {
        Debug.Log("Disabling path drawing component");

        if (pathDrawing != null)
        {
            pathDrawing.enabled = false;
        }

        if (enableDebugLogs) Debug.Log("Path drawing mode disabled");
    }

    // TODO: is this fine or should the teamId be set on the Base? should I use tag instead of GetComponent?
    public bool IsPointInsideEnemyBase(Vector3 point)
    {
        // Check if the point is inside the enemy base
        Collider[] hitColliders = Physics.OverlapSphere(point, 0.1f);
        foreach (var collider in hitColliders)
        {
            Debug.Log($"Collider: {collider.name}");
            Debug.Log($"Collider tag: {collider.tag}");
            Debug.Log($"Collider teamId: {collider.GetComponent<BaseController>()?.OwnerPlayer.teamId}");

            BaseController baseComponent = collider.GetComponent<BaseController>();
            if (baseComponent != null && baseComponent.OwnerPlayer != null)
            {
                if (baseComponent.OwnerPlayer.teamId != CurrentPlayer.teamId)
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

            // TODO: next players turn
            // ChangeGameState(GameState.PlayersTurn);
        }

        DisablePathDrawing();
    }

    // Cancel path drawing and return to player turn
    public void CancelPathDrawing()
    {
        selectedUnit = null;
        ChangeGameState(GameState.PlayersTurn);
    }

    public void EndTurn()
    {
        if (enableDebugLogs) Debug.Log("[GameManager] Player turn ended");

        // Transition to enemy turn
        // ChangeState(GameState.EnemyTurn);

        // Start enemy turn logic
        // ...
    }
}