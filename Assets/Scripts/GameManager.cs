using System;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    // TODO: Is this required?
    // Singleton instance
    public static GameManager Instance { get; private set; }
    
    // Game state properties
    [SerializeField] private GameState currentState = GameState.PlayersTurn;
    public GameState CurrentState => currentState;
    
    // Player management
    [SerializeField] private Transform playersParent; // Reference to the Players parent GameObject
    [SerializeField] private List<Player> players = new List<Player>();
    [SerializeField] private int currentPlayerIndex = 0;
    public Player CurrentPlayer => players[currentPlayerIndex];
    
    // Selected unit for path drawing
    private UnitController selectedUnit;
    public UnitController SelectedUnit => selectedUnit;
    
    // Camera management
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private SimplePathDrawing pathDrawingSystem;
    
    // Events
    public static event Action<GameState> OnGameStateChanged;
    public static event Action<Player> OnPlayerTurnChanged;
    public static event Action<UnitController> OnUnitSelected;

    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        // Make sure GameManager persists throughout the game
        DontDestroyOnLoad(gameObject);
    }
    
    private void Start()
    {
        // Get players from playersParent if provided
        if (playersParent != null && players.Count == 0)
        {
            players.AddRange(playersParent.GetComponentsInChildren<Player>());
        }
        // Fallback to finding all players in scene
        else if (players.Count == 0)
        {
            players.AddRange(FindObjectsOfType<Player>());
        }
        
        if (players.Count == 0)
        {
            Debug.LogError("No players found in the scene. Please assign players or a players parent.");
            return;
        }
        
        // Start the game with first player's turn
        StartPlayerTurn(0);
        
        // Find camera manager if not assigned
        if (cameraManager == null)
        {
            cameraManager = FindObjectOfType<CameraManager>();
            if (cameraManager == null)
            {
                Debug.LogWarning("No CameraManager found in scene. Camera transitions will be disabled.");
            }
        }

        // Subscribe to our own events
        OnPlayerTurnChanged += (player) => HighlightSelectableUnits(true);
        OnGameStateChanged += (state) => {
            if (state == GameState.PlayersTurn)
                HighlightSelectableUnits(true);
            else
                HighlightSelectableUnits(false);
        };
    }
    
    // Change to a new game state
    public void ChangeState(GameState newState)
    {
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
        
        // Invoke event
        OnGameStateChanged?.Invoke(currentState);
    }
    
    // Start a specific player's turn
    public void StartPlayerTurn(int playerIndex)
    {
        currentPlayerIndex = playerIndex;
        currentState = GameState.PlayersTurn;
        
        // Reset turn variables
        selectedUnit = null;
        
        // Switch to main game camera
        if (cameraManager != null)
        {
            cameraManager.SwitchToMainCamera();
        }
        
        // Invoke event
        OnPlayerTurnChanged?.Invoke(CurrentPlayer);
        OnGameStateChanged?.Invoke(currentState);
        
        Debug.Log($"Starting {players[currentPlayerIndex].playerName}'s turn");
    }
    
    // Switch to next player's turn
    public void NextPlayerTurn()
    {
        currentPlayerIndex = (currentPlayerIndex + 1) % players.Count;
        StartPlayerTurn(currentPlayerIndex);
    }
    
    // Select a unit and enter path drawing mode
    public void SelectUnit(UnitController unit)
    {
        // Only allow selection if it's player's turn and unit belongs to current player
        if (currentState == GameState.PlayersTurn && 
            unit.ownerPlayer == CurrentPlayer && 
            unit.isPending)
        {
            selectedUnit = unit;
            OnUnitSelected?.Invoke(selectedUnit);
            
            // Change to path drawing state
            ChangeState(GameState.PathDrawing);
        }
    }
    
    // Enable path drawing mode
    private void EnablePathDrawing()
    {
        Debug.Log("Enabling path drawing mode");
        
        // Transition to path drawing camera
        if (cameraManager != null)
        {
            cameraManager.SwitchToPathDrawCamera();
        }
        
        // Enable path drawing system using serialized reference if available
        if (pathDrawingSystem != null)
        {
            pathDrawingSystem.enabled = true;
            pathDrawingSystem.StartDrawing();
        }
        else
        {
            // Fallback to finding the component if not assigned
            SimplePathDrawing pathDrawing = FindObjectOfType<SimplePathDrawing>();
            if (pathDrawing != null)
            {
                pathDrawing.enabled = true;
                pathDrawing.StartDrawing();
            }
            else
            {
                Debug.LogWarning("No SimplePathDrawing component found in the scene!");
            }
        }
        
        Debug.Log("Path drawing mode enabled");
    }
    
    // Disable path drawing mode
    private void DisablePathDrawing()
    {
        SimplePathDrawing pathDrawing = FindObjectOfType<SimplePathDrawing>();
        if (pathDrawing != null)
        {
            pathDrawing.enabled = false;
        }
        
        Debug.Log("Path drawing mode disabled");
    }
    
    // Called when path drawing is completed
    public void ConfirmPath(List<Vector3> path)
    {
        if (selectedUnit != null)
        {
            // Assign path to selected unit
            selectedUnit.FollowPath(path);
            selectedUnit.isPending = false;
            
            // Return to player turn state
            // TODO: next players turn
            ChangeState(GameState.PlayersTurn);
        }
    }
    
    // Cancel path drawing and return to player turn
    public void CancelPathDrawing()
    {
        selectedUnit = null;
        ChangeState(GameState.PlayersTurn);
    }

    public void HighlightSelectableUnits(bool highlight)
    {
        if (CurrentPlayer != null)
        {
            foreach (UnitController unit in CurrentPlayer.OwnedUnits)
            {
                if (unit != null && unit.isPending)
                {
                    unit.HighlightAsSelectable(highlight);
                }
            }
        }
    }
}