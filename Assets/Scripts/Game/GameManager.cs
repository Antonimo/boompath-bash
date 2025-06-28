using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Unified GameManager that combines network synchronization with game logic.
/// 
/// Key Responsibilities:
/// - Network synchronization of global game states across all clients
/// - Core game mechanics and local player interactions
/// - Game flow coordination in networked multiplayer
/// - Unity object management (Bases, Units, Camera)
/// 
/// Architecture Philosophy:
/// - Global game states (WaitingForPlayers, Playing, GameOver) are network-synced via server
/// - Player NetworkObjects, bases, and units are server-controlled and network-synced
/// - UI, interactions, camera, and local turn management are client-only (but react to network state)
/// - Each client handles their own initialization when detecting network state changes
/// - Local co-op support via currentPlayerIndex (not network-synced, per-client only)
/// </summary>
public class GameManager : NetworkBehaviour
{
    #region Singleton Pattern
    private static GameManager instance;

    /// <summary>
    /// Singleton instance of the GameManager.
    /// </summary>
    public static GameManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = UnityEngine.Object.FindFirstObjectByType<GameManager>();
                if (instance == null)
                {
                    Debug.LogError("GameManager: No GameManager instance found in scene!");
                }
            }
            return instance;
        }
    }
    #endregion

    #region Network State Synchronization (Server-controlled)
    /// <summary>
    /// Network-synchronized game state that all clients observe.
    /// Server-controlled, automatically synced to all clients.
    /// </summary>
    private NetworkVariable<GameState> networkGameState = new NetworkVariable<GameState>(
        GameState.WaitingForPlayers,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>
    /// Current game state (read-only access).
    /// </summary>
    public GameState CurrentState => networkGameState.Value;

    /// <summary>
    /// Event fired when the game state changes on any client.
    /// </summary>
    public event Action<GameState, GameState> OnGameStateChanged;
    #endregion

    // TODO: fix region names, remove the refactoring flow comments.
    #region Client-Only Fields (Not Network-Synced)
    // Initial game state (used by server only for initialization)
    [SerializeField] private GameState initialGameState = GameState.WaitingForPlayers;

    // Local player management (for co-op: multiple players sharing same screen)
    // NOT network-synced - each client manages their own local turn order
    [SerializeField] private GameObject playersParent;
    [SerializeField] private List<Player> players = new List<Player>();
    [SerializeField] private int currentPlayerIndex = 0; // Client-only: local co-op turn management

    private PlayerTurnPhase currentPlayerTurnPhase = PlayerTurnPhase.PlayerTurn; // Client-only: local player turn phase management

    // public accessor
    public Player CurrentPlayer => players[currentPlayerIndex];

    // Selected unit for path drawing (client-only UI state)
    [SerializeField] private Unit selectedUnit;
    public Unit SelectedUnit => selectedUnit;

    // Client-only UI components
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private SimplePathDrawing pathDrawing;
    [SerializeField] private PlayerTurn playerTurn;
    [SerializeField] private MenuManager menuManager;
    [SerializeField] private MenuPanel gameOverPanel;

    // TODO: is this needed?
    // Public accessors for components
    public CameraManager CameraManager => cameraManager;
    public SimplePathDrawing PathDrawing => pathDrawing;
    public PlayerTurn PlayerTurn => playerTurn;

    // Winner tracking
    // TODO: use network variable?
    private Player winnerPlayer = null;
    public Player WinnerPlayer => winnerPlayer;

    // Debugging (should be always last)
    [SerializeField] private bool enableDebugLogs = true;
    public bool EnableDebugLogs => enableDebugLogs;
    #endregion

    #region Network Setup and State
    [SerializeField] private PlayerSpawnManager playerSpawnManager; // Server-controlled player spawning
    [SerializeField] private int requiredPlayerCount = 2;

    // TODO: no need because now we have WinnerPlayer?
    // Authoritative winner information from host
    // TODO: refactor with WinnerPlayer
    private ulong winnerClientId = 0; // 0 means no winner/draw
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // Ensure singleton pattern
        if (instance == null)
        {
            instance = this;
            Debug.Log("[GameManager] Singleton instance set in Awake");
        }
        else if (instance != this)
        {
            Debug.LogError("[GameManager] Multiple GameManager instances detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        ValidateDependencies();

        Debug.Log("[GameManager] Awake completed");
    }

    private void Start()
    {
        Debug.Log("[GameManager] Start called");

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
        // Update playing state on all clients (local player interactions)
        UpdatePlayingState();

        // Handle input during GameOver state
        if (CurrentState == GameState.GameOver)
        {
            HandleGameOverInput();
        }
    }

    private void LateUpdate()
    {
        // Only server handles game over checks for authoritative decisions
        if (IsServer)
        {
            CheckGameOverConditions();
        }
    }

    public override void OnDestroy()
    {
        // Call base implementation first
        // TODO: verify base.OnDestroy is called on all network objects
        base.OnDestroy();

        // Cleanup singleton reference
        if (instance == this)
        {
            instance = null;
        }

        Debug.Log("[GameManager] OnDestroy called");
    }
    #endregion

    #region NetworkBehaviour Lifecycle
    public override void OnNetworkSpawn()
    {
        Debug.Log($"[GameManager] OnNetworkSpawn: IsClient: {IsClient}, IsServer: {IsServer}, IsHost: {IsHost}");

        // Subscribe to network state changes on all clients
        networkGameState.OnValueChanged += OnNetworkGameStateChanged;

        if (IsServer)
        {
            Debug.Log("[GameManager] Server spawned.");
            // Server waits for all players to be ready before setting up and launching the game
            // TODO: Server-specific setup - StartCoroutine(WaitForPlayersAndSetupGame()) will be added in next steps
        }

        if (IsClient)
        {
            Debug.Log("[GameManager] Client-specific initialization");
            // TODO: Client-specific setup will be added when methods are migrated
        }
    }

    public override void OnNetworkDespawn()
    {
        // Unsubscribe to prevent memory leaks
        if (networkGameState != null)
        {
            networkGameState.OnValueChanged -= OnNetworkGameStateChanged;
        }

        Debug.Log("[GameManager] OnNetworkDespawn called");
    }
    #endregion

    #region Network State Management
    /// <summary>
    /// Changes the game state (server only).
    /// </summary>
    /// <param name="newState">The new game state to transition to</param>
    public void ChangeGameState(GameState newState)
    {
        if (!IsServer)
        {
            Debug.LogWarning("[GameManager] ChangeGameState called on non-server. Only server can change game state.");
            return;
        }

        GameState previousState = networkGameState.Value;
        networkGameState.Value = newState;

        Debug.Log($"[GameManager] Server changed game state: {previousState} -> {newState}");
    }

    /// <summary>
    /// Handles network game state changes on all clients.
    /// </summary>
    private void OnNetworkGameStateChanged(GameState previousState, GameState newState)
    {
        Debug.Log($"[GameManager] Game state changed: {previousState} -> {newState}");

        HandleGameStateTransition(previousState, newState);

        // TODO: where used?
        OnGameStateChanged?.Invoke(previousState, newState);
    }

    /// <summary>
    /// Handles game state transition logic.
    /// </summary>
    private void HandleGameStateTransition(GameState fromState, GameState toState)
    {
        Debug.Log($"[GameManager] Handling state transition: {fromState} -> {toState}");

        HandleGameStateExit(fromState);

        // Handle entries to new states
        switch (toState)
        {
            case GameState.Playing:
                if (EnableDebugLogs) Debug.Log("[GameManager] Entering Playing state");

                // Client-side initialization when entering Playing state
                // This handles both new games and returning players automatically
                InitializeClientForPlayingState();

                // Initialize local player interaction management
                InitializePlayingState();
                break;

            case GameState.GameOver:
                if (EnableDebugLogs) Debug.Log("[GameManager] Entering GameOver state");

                // Network-specific logic adapted from OnNetworkRelevantGameStateChanged
                // TODO: refactor to server only stuff and all clients cleanup stuff
                HandleGameOver();

                // TODO: why?
                ResetSelectedUnit();

                // Show game over UI
                ShowGameOverUI();
                break;
        }
    }

    /// <summary>
    /// Handles cleanup when exiting game states.
    /// </summary>
    private void HandleGameStateExit(GameState exitingState)
    {
        switch (exitingState)
        {
            case GameState.Playing:
                // When leaving Playing state, reset all player turn phases
                // This ensures clean state when game ends or is interrupted
                ResetAllPlayerTurnPhases();
                break;
        }
    }

    /// <summary>
    /// Resets all client-side player turn phases and associated components.
    /// Call this when entering Playing state or when leaving Playing state to ensure clean state.
    /// </summary>
    private void ResetAllPlayerTurnPhases()
    {
        if (EnableDebugLogs) Debug.Log("[GameManager] Resetting all player turn phases");

        // Reset local player turn phase to default
        currentPlayerTurnPhase = PlayerTurnPhase.PlayerTurn;

        // Reset selected unit
        ResetSelectedUnit();

        // Disable all turn-related components
        if (PlayerTurn != null)
        {
            PlayerTurn.enabled = false;
        }

        if (PathDrawing != null)
        {
            PathDrawing.enabled = false;
        }

        // TODO: disable all UI controllers (path drawing, player turn, etc.)
        // TODO: reset any other turn-related state
    }

    /// <summary>
    /// Handles cleanup when exiting player turn phases.
    /// </summary>
    private void HandlePlayerTurnPhaseExit(PlayerTurnPhase exitingPhase)
    {
        switch (exitingPhase)
        {
            case PlayerTurnPhase.PlayerTurn:
                if (PlayerTurn != null)
                {
                    PlayerTurn.enabled = false;
                }
                break;

            case PlayerTurnPhase.DrawingPath:
                if (PathDrawing != null)
                {
                    PathDrawing.enabled = false;
                }
                break;

            case PlayerTurnPhase.PlayerTurnEnd:
                // Nothing specific to disable for turn end phase
                break;
        }
    }

    /// <summary>
    /// Changes the current player turn phase with proper exit/enter handling.
    /// </summary>
    private void ChangePlayerTurnPhase(PlayerTurnPhase newPhase)
    {
        if (CurrentState != GameState.Playing) return;

        PlayerTurnPhase previousPhase = currentPlayerTurnPhase;

        if (EnableDebugLogs) Debug.Log($"[GameManager] Changing player turn phase: {previousPhase} -> {newPhase}");

        // Handle exit from previous phase
        HandlePlayerTurnPhaseExit(previousPhase);

        // Update current phase
        currentPlayerTurnPhase = newPhase;

        if (EnableDebugLogs) Debug.Log($"[GameManager] Player turn phase changed: {previousPhase} -> {newPhase}");

        // Handle entry to new phase
        switch (newPhase)
        {
            case PlayerTurnPhase.PlayerTurn:
                StartPlayerTurnPhase();
                break;

            case PlayerTurnPhase.DrawingPath:
                StartPathDrawingPhase();
                break;

            case PlayerTurnPhase.PlayerTurnEnd:
                StartPlayerTurnEndPhase();
                break;
        }
    }

    #endregion

    #region Initialization Methods
    // TODO: move up
    private void ValidateDependencies()
    {
        // Validate fields from OldGameManager
        if (playersParent == null)
        {
            Debug.LogError("GameManager: playersParent is not assigned. Please assign a parent object for players.");
            this.enabled = false;
        }

        if (cameraManager == null)
        {
            Debug.LogError("GameManager: cameraManager is not assigned.");
            this.enabled = false;
        }

        if (pathDrawing == null)
        {
            Debug.LogError("GameManager: pathDrawing is not assigned.");
            this.enabled = false;
        }

        if (playerTurn == null)
        {
            Debug.LogError("GameManager: playerTurn is not assigned.");
            this.enabled = false;
        }

        // Validate fields from OldNetworkGameManager
        if (menuManager == null)
        {
            Debug.LogError("GameManager: menuManager is not assigned.");
            this.enabled = false;
        }

        if (gameOverPanel == null)
        {
            Debug.LogError("GameManager: gameOverPanel is not assigned.");
            this.enabled = false;
        }

        if (playerSpawnManager == null)
        {
            Debug.LogError("GameManager: playerSpawnManager is not assigned.");
            this.enabled = false;
        }
    }

    // TODO: used where and how?
    private void InitializePlayers()
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

    private IEnumerator DelayedInitialization()
    {
        yield return new WaitForEndOfFrame();

        // Initialize to the specified initial state using new NetworkVariable system
        if (IsServer)
        {
            // TODO: how does this work with default value already set? Loading -> Loading?
            ChangeGameState(initialGameState);
        }
    }

    private void CheckGameOverConditions()
    {
        // Only check game over conditions from states that allow ending the game
        // TODO: is this still needed?
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

            ChangeGameState(GameState.GameOver);
        }
    }

    private static bool CanEndGameFromState(GameState state)
    {
        switch (state)
        {
            case GameState.Playing:
                return true;
            default:
                return false;
        }
    }
    #endregion

    #region Player Management
    public void ClearPlayers()
    {
        players.Clear();
    }

    public void ResetToFirstPlayer()
    {
        currentPlayerIndex = 0;
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
                // TODO: This should trigger LOCAL player turn state (PlayerTurn), not global game state change
                // Local player interaction states (PlayerTurn, PathDrawing, TurnComplete) should be handled
                // per-client during the global "Playing" state. This will be addressed in Phase 4 with
                // PlayerInteractionManager creation.
                return; // Found next non-bot player
            }
        } while (nextIndex != initialIndex); // Loop until we've checked all players

        // If we reach here, it means all players are bots or there's only one player who is a bot.
        // Handle this case as needed (e.g., keep the current bot player's turn, end the game, etc.)
        Debug.Log("StartNextPlayerTurn: Could not find a non-bot player. Staying on the current player or check game logic.");
    }

    public void SelectUnit(Unit unit)
    {
        if (unit.ownerPlayer == CurrentPlayer && unit.IsPending)
        {
            selectedUnit = unit;
            // Trigger local path drawing phase within global Playing state
            ChangePlayerTurnPhase(PlayerTurnPhase.DrawingPath);
        }
    }

    public void ResetSelectedUnit()
    {
        selectedUnit = null;
    }

    public void SetupLocalPlayer(Player localPlayer)
    {
        if (!players.Contains(localPlayer))
        {
            players.Add(localPlayer);
        }

        currentPlayerIndex = players.IndexOf(localPlayer);

        Debug.Log($"[GameManager] Set up local player: {localPlayer.playerName} at index {currentPlayerIndex} (total players: {players.Count})");
    }
    #endregion

    #region Game Flow Control Methods

    // TODO: make sure these are used instead of direct ChangeGameState calls

    /// <summary>
    /// Starts the playing state.
    /// </summary>
    public void StartPlaying()
    {
        ChangeGameState(GameState.Playing);
    }

    /// <summary>
    /// Triggers game over state.
    /// </summary>
    public void GameOver()
    {
        ChangeGameState(GameState.GameOver);
    }


    /// <summary>
    /// Enables all base controllers in the game.
    /// </summary>
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

    /// <summary>
    /// Checks if a point is inside an enemy base relative to the current player.
    /// </summary>
    /// <param name="point">The world position to check</param>
    /// <returns>True if the point is inside an enemy base</returns>
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

    /// <summary>
    /// Called when path drawing is completed by the current player.
    /// </summary>
    /// <param name="path">The path points drawn by the player</param>
    public void ConfirmPath(List<Vector3> path)
    {
        if (selectedUnit != null && CurrentPlayer != null)
        {
            // Request path assignment through the current player (triggers ServerRpc flow)
            Debug.Log($"[GameManager] ConfirmPath: Requesting path assignment for Unit {selectedUnit.NetworkObjectId} via CurrentPlayer {CurrentPlayer.OwnerClientId}.");
            CurrentPlayer.RequestUnitPathAssignment(selectedUnit.NetworkObjectId, path);
        }
        else
        {
            if (selectedUnit == null) Debug.LogWarning("[GameManager] ConfirmPath called but selectedUnit is null.");
            if (CurrentPlayer == null) Debug.LogWarning("[GameManager] ConfirmPath called but CurrentPlayer is null.");
        }

        // Trigger local player turn end phase within global Playing state
        ChangePlayerTurnPhase(PlayerTurnPhase.PlayerTurnEnd);
        Debug.Log("[GameManager] Path confirmed, player turn ending.");
    }

    /// <summary>
    /// Switches the camera to focus on the current player's base.
    /// </summary>
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

    /// <summary>
    /// Client-side initialization when entering Playing state.
    /// Handles setup needed for both new games and returning players.
    /// Called automatically when clients detect transition to Playing state.
    /// </summary>
    private void InitializeClientForPlayingState()
    {
        if (EnableDebugLogs) Debug.Log("[GameManager] Initializing client for Playing state");

        // Get the local player (should be spawned by now)
        Player localPlayer = GetLocalPlayer();

        if (localPlayer == null)
        {
            Debug.LogError("Local player not found when initializing Playing state!");
            return;
        }

        // Setup local player reference and reset local co-op turn index
        // (Safe for returning players - we can't know previous local state anyway)
        SetupLocalPlayer(localPlayer);
        ResetToFirstPlayer(); // Reset local co-op turn management

        ResetAllPlayerTurnPhases();

        // Enable bases (script enabled state is not network-synced, so each client must do this)
        EnableBases();

        // Set up camera to initial playing position
        // (Appropriate for both new games and returning players)
        if (cameraManager != null)
        {
            cameraManager.SwitchToMainCamera();
        }

        // Clear lobby ready state (appropriate - we don't want stale ready states)
        if (LobbyManager.Instance != null)
        {
            _ = LobbyManager.Instance.ClearLocalPlayerReadyState();
        }

        if (EnableDebugLogs) Debug.Log("[GameManager] Client Playing state initialization complete");
    }
    #endregion

    #region Playing State Management
    /// <summary>
    /// Initialize the playing state - starts first player turn
    /// </summary>
    private void InitializePlayingState()
    {
        if (EnableDebugLogs) Debug.Log("[GameManager] Initializing Playing state");

        // Start with the first player's turn
        ChangePlayerTurnPhase(PlayerTurnPhase.PlayerTurn);
    }

    /// <summary>
    /// Start player turn phase - consolidated from PlayerTurnState.Enter()
    /// </summary>
    private void StartPlayerTurnPhase()
    {
        if (EnableDebugLogs) Debug.Log($"Starting {CurrentPlayer.playerName}'s turn");

        // Reset turn variables
        ResetSelectedUnit();

        // Switch camera to the current player's base
        SwitchCameraToCurrentPlayerBase();

        // Enable player turn component
        // TODO: gameManager.EnablePlayerTurn() ?
        if (PlayerTurn != null)
        {
            PlayerTurn.player = CurrentPlayer;
            PlayerTurn.enabled = true;
        }
    }

    /// <summary>
    /// Start path drawing phase - consolidated from PathDrawingState.Enter()
    /// </summary>
    public void StartPathDrawingPhase()
    {
        if (EnableDebugLogs) Debug.Log("Entering path drawing mode");

        if (CameraManager != null)
        {
            CameraManager.SwitchToPathDrawCamera();
        }

        if (PathDrawing != null)
        {
            if (SelectedUnit?.transform == null)
            {
                Debug.LogError("Selected unit or its transform is null. Cannot enable path drawing.");
                CancelPathDrawing();
                return;
            }

            PathDrawing.pathStartPosition = SelectedUnit.transform.position;
            PathDrawing.enabled = true;
        }
    }

    /// <summary>
    /// Cancel path drawing and return to player turn phase
    /// </summary>
    public void CancelPathDrawing()
    {
        ResetSelectedUnit();

        if (CurrentState == GameState.Playing)
        {
            ChangePlayerTurnPhase(PlayerTurnPhase.PlayerTurn);
        }
    }

    /// <summary>
    /// Start player turn end phase - consolidated from PlayerTurnEndState.Enter()
    /// </summary>
    public void StartPlayerTurnEndPhase()
    {
        if (EnableDebugLogs) Debug.Log("Entering player turn end state");

        // Start the transition sequence as a coroutine
        StartCoroutine(PlayerTurnEndTransitionSequence());
    }

    /// <summary>
    /// Player turn end transition sequence - consolidated from PlayerTurnEndState.TransitionSequence()
    /// </summary>
    private IEnumerator PlayerTurnEndTransitionSequence()
    {
        SwitchCameraToCurrentPlayerBase();

        if (CameraManager != null)
        {
            while (CameraManager.IsTransitioning)
            {
                yield return null;
            }
        }

        // Wait additional 2 seconds
        yield return new WaitForSeconds(2f);

        // Start the next player's turn
        StartNextPlayerTurn();

        // Return to player turn phase for the new current player
        // TODO: Shouldn't this be done in StartNextPlayerTurn()?
        ChangePlayerTurnPhase(PlayerTurnPhase.PlayerTurn);
    }

    /// <summary>
    /// Update method called during Playing state for player turn phase management
    /// </summary>
    private void UpdatePlayingState()
    {
        if (CurrentState != GameState.Playing) return;

        HandlePlayingStateInput();

        // Additional playing state update logic can be added here
    }

    /// <summary>
    /// Handle input during Playing state
    /// </summary>
    private void HandlePlayingStateInput()
    {
        if (CurrentState != GameState.Playing) return;

        // TODO: switch case?
        // Handle escape key during path drawing to cancel
        if (currentPlayerTurnPhase == PlayerTurnPhase.DrawingPath && Input.GetKeyDown(KeyCode.Escape))
        {
            CancelPathDrawing();
        }
    }

    /// <summary>
    /// Show the game over UI panel
    /// </summary>
    private void ShowGameOverUI()
    {
        if (EnableDebugLogs) Debug.Log("[GameManager] Showing GameOver UI");

        if (menuManager != null && gameOverPanel != null)
        {
            menuManager.OpenMenuPanel(gameOverPanel);
        }
        else
        {
            Debug.LogError("[GameManager] Cannot show GameOver UI - menuManager or gameOverPanel is null");
        }
    }

    /// <summary>
    /// Handle input during GameOver state
    /// </summary>
    private void HandleGameOverInput()
    {
        // Handle input for restart/quit options
        if (Input.GetKeyDown(KeyCode.R))
        {
            // Restart game
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            );
        }
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Quit game
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
    #endregion

    #region Network Event Handling Methods

    /// <summary>
    /// Handles GameOver state logic adapted from OldNetworkGameManager.
    /// Server determines winner and broadcasts to clients.
    /// </summary>
    // TODO: refactor, now we have synced game state and winnerClientId, so we can just use that
    private void HandleGameOver()
    {
        Debug.Log("[GameManager] Handling GameOver state");

        if (IsServer)
        {
            // Host determines the winner authoritatively
            ulong winnerClientId = DetermineWinnerClientId();
            Debug.Log($"[GameManager] Host determined winner client ID: {winnerClientId}");

            // Broadcast game over with winner client ID to all clients
            GameOverClientRpc(winnerClientId);
        }

        // TODO: maybe this should happen in GameOverClientRpc when the winner is set?
        // Note: ShowGameOverUI() is called separately in HandleGameStateTransition
    }

    /// <summary>
    /// Host-only method to authoritatively determine the winner by client ID.
    /// Adapted from OldNetworkGameManager.
    /// </summary>
    /// <returns>Winner player's client ID or 0 if no winner/draw</returns>
    private ulong DetermineWinnerClientId()
    {
        if (!IsServer)
        {
            Debug.LogError("[GameManager] DetermineWinnerClientId called on non-server!");
            return 0;
        }

        // TODO: maybe this should be a network variable?
        return WinnerPlayer?.OwnerClientId ?? 0;
    }

    /// <summary>
    /// ServerRpc to handle player spawn requests from clients.
    /// Adapted from OldNetworkGameManager.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void RequestPlayerSpawnServerRpc(ServerRpcParams serverRpcParams = default)
    {
        if (!IsServer) return;

        ulong clientId = serverRpcParams.Receive.SenderClientId;
        Debug.Log($"[GameManager] Host received player spawn request from client {clientId}");

        // Spawn/respawn the player for this specific client
        playerSpawnManager.RespawnPlayer(clientId);

        // After spawning, check if all players are now ready to start the game
        if (AreAllPlayersSpawned())
        {
            Debug.Log("[GameManager] All players spawned! Transitioning directly to Playing state.");
            ChangeGameState(GameState.Playing);
        }
    }

    /// <summary>
    /// Checks if all required players are spawned.
    /// Adapted from OldNetworkGameManager.
    /// </summary>
    private bool AreAllPlayersSpawned()
    {
        if (playersParent == null) return false;

        Player[] players = playersParent.GetComponentsInChildren<Player>();
        bool hasEnoughPlayers = players.Length >= requiredPlayerCount;

        if (hasEnoughPlayers)
        {
            Debug.Log($"[GameManager] All players spawned. Found {players.Length} players.");
        }

        return hasEnoughPlayers;
    }



    /// <summary>
    /// ClientRpc to handle game over notification with winner information.
    /// Adapted from OldNetworkGameManager.
    /// </summary>
    // TODO: refactor, now we have synced game state and winnerClientId, so we can just use that
    [ClientRpc]
    private void GameOverClientRpc(ulong winnerClientId)
    {
        if (!IsClient) return;

        Debug.Log($"[GameManager] Received GameOver from server with winner client ID: {winnerClientId}");

        // Store the authoritative winner information
        this.winnerClientId = winnerClientId;

        // TODO: is this needed if we already sync game state?
        // Force all clients to enter GameOver state if they haven't already
        if (CurrentState != GameState.GameOver)
        {
            GameOver();
        }
    }

    /// <summary>
    /// Gets the authoritative winner player name resolved from the host-determined client ID.
    /// Adapted from OldNetworkGameManager.
    /// </summary>
    /// <returns>Winner player name or empty string if no winner/draw</returns>
    public string GetWinnerPlayerName()
    {
        if (winnerClientId == 0)
        {
            return ""; // No winner / draw
        }

        // Resolve the player name from the authoritative client ID
        Player winnerPlayer = GetPlayerByClientId(winnerClientId);
        if (winnerPlayer != null)
        {
            return winnerPlayer.playerName;
        }

        Debug.LogWarning($"[GameManager] Could not resolve winner player name for client ID: {winnerClientId}");
        return "Unknown Player";
    }

    /// <summary>
    /// Helper method to find a Player by their client ID.
    /// Adapted from OldNetworkGameManager.
    /// </summary>
    /// <param name="clientId">The client ID to search for</param>
    /// <returns>Player component or null if not found</returns>
    private Player GetPlayerByClientId(ulong clientId)
    {
        if (playersParent == null) return null;

        Player[] players = playersParent.GetComponentsInChildren<Player>();
        foreach (Player player in players)
        {
            if (player.OwnerClientId == clientId)
            {
                return player;
            }
        }

        return null;
    }

    #endregion

    // TODO: refactor regions and functions to be located in order of usage as much as possible.
    #region Game Setup and Launch Methods

    /// <summary>
    /// Server-only method to set up and launch the game for all clients.
    /// Uses a staged approach to ensure proper cleanup before proceeding:
    /// Phase 1: Server cleanup (despawn all players)
    /// Phase 2: Server verification (wait for hierarchy to be clean)
    /// Phase 3: Client setup (notify clients to reset and request spawning)
    /// </summary>
    public void SetupAndLaunchGame()
    {
        if (IsServer)
        {
            Debug.Log("[GameManager] SetupAndLaunchGame - Starting staged server setup");
            StartCoroutine(ServerGameSetupSequence());
        }
    }

    /// <summary>
    /// Staged server game setup sequence to avoid race conditions.
    /// Ensures network despawn operations complete before proceeding.
    /// </summary>
    private IEnumerator ServerGameSetupSequence()
    {
        // Phase 1: Server Cleanup - despawn all existing players
        Debug.Log("[GameManager] Phase 1: Server cleanup - despawning all players");

        // TODO: do not despawn/respawn if its a new game and not a rematch
        playerSpawnManager.DespawnAllPlayers();
        ChangeGameState(GameState.WaitingForPlayers);

        // Phase 2: Server Verification - wait for cleanup to complete with timeout protection
        Debug.Log("[GameManager] Phase 2: Waiting for cleanup completion and hierarchy verification");

        float timeoutDuration = 5f; // 5 second timeout
        float elapsedTime = 0f;

        while (!VerifyGameHierarchyClean() && elapsedTime < timeoutDuration)
        {
            yield return new WaitForSeconds(0.1f); // Check every 100ms
            elapsedTime += 0.1f;
        }

        if (elapsedTime >= timeoutDuration)
        {
            Debug.LogError("[GameManager] Phase 2: TIMEOUT waiting for hierarchy cleanup - aborting game setup");
            // Log remaining objects for debugging
            if (playersParent != null)
            {
                Player[] remainingPlayers = playersParent.GetComponentsInChildren<Player>();
                Debug.LogError($"[GameManager] TIMEOUT: {remainingPlayers.Length} players still remain in hierarchy");
            }
            // Abort the setup process
            Debug.LogError("[GameManager] Game setup aborted due to cleanup timeout. Manual intervention may be required.");
            yield break;
        }

        // Phase 3: Client Setup - notify clients to setup and request spawning
        Debug.Log("[GameManager] Phase 3: Cleanup completed successfully, notifying clients to setup");
        SetupAndLaunchGameClientRpc();
    }

    /// <summary>
    /// Verifies that the game hierarchy is clean and ready for new game setup.
    /// </summary>
    /// <returns>True if hierarchy is clean (no player objects remaining)</returns>
    private bool VerifyGameHierarchyClean()
    {
        if (playersParent == null)
        {
            Debug.LogWarning("[GameManager] Cannot verify hierarchy - playersParent is null");
            return false;
        }

        Player[] remainingPlayers = playersParent.GetComponentsInChildren<Player>();
        bool isClean = remainingPlayers.Length == 0;

        if (!isClean)
        {
            Debug.Log($"[GameManager] Hierarchy not clean yet - {remainingPlayers.Length} players remaining");
        }
        else
        {
            Debug.Log("[GameManager] Hierarchy verified clean - ready for client setup");
        }

        return isClean;
    }

    /// <summary>
    /// Coroutine that waits for all players to be ready before launching the game.
    /// Used by server to coordinate game start timing.
    /// </summary>
    // TODO: remove unused?
    private IEnumerator WaitForPlayersAndSetupGame()
    {
        // Wait until all expected players are connected and spawned
        while (!AreAllPlayersReady())
        {
            yield return new WaitForSeconds(0.5f);
        }

        // All players are ready, setup and launch the game on all clients
        SetupAndLaunchGameClientRpc();
    }

    /// <summary>
    /// Checks if all required players are connected and ready to play.
    /// Used by server to determine when to start the game.
    /// </summary>
    // TODO: player ready state is not what we are checking for here
    private bool AreAllPlayersReady()
    {
        if (playersParent == null) return false;

        Player[] networkPlayers = playersParent.GetComponentsInChildren<Player>();
        bool hasEnoughPlayers = networkPlayers.Length >= requiredPlayerCount;

        if (hasEnoughPlayers)
        {
            Debug.Log($"[GameManager] All players ready. Found {networkPlayers.Length} players.");
        }

        return hasEnoughPlayers;
    }

    /// <summary>
    /// ClientRpc that sets up game managers and initiates game loading on all clients.
    /// This is Phase 3 of the staged setup process - called by server after cleanup verification.
    /// Clients reset their local state and request new player spawning from the server.
    /// </summary>
    [ClientRpc]
    private void SetupAndLaunchGameClientRpc()
    {
        // TODO: review all the flows with this check, I was confused that IsHost is also IsClient
        if (!IsClient) return;

        Debug.Log("[GameManager] SetupAndLaunchGameClientRpc - Phase 3: Beginning client setup and spawn request");

        // Reset local non-network state
        ClearPlayers();
        menuManager.ClearAll();

        // Request server to spawn new player objects
        RequestPlayerSpawnServerRpc();
    }

    /// <summary>
    /// Finds and returns the local player owned by this client.
    /// Used to identify which player object belongs to the current client.
    /// </summary>
    private Player GetLocalPlayer()
    {
        if (playersParent == null) return null;

        Player[] networkPlayers = playersParent.GetComponentsInChildren<Player>();
        foreach (Player player in networkPlayers)
        {
            // Check if this player object is owned by the local client
            NetworkObject networkObject = player.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsOwner)
            {
                return player;
            }
        }

        return null;
    }

    #endregion
}