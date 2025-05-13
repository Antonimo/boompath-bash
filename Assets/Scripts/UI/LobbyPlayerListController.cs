using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
// using System.Linq; // Uncomment if LINQ is needed later

public class LobbyPlayerListController : MonoBehaviour
{
    public List<PlayerEntryUI> playerEntries; // Assign these in the Inspector (e.g., size 2 for 1v1)
    public Button readyToggleButton;
    public TextMeshProUGUI readyButtonText;
    public TextMeshProUGUI overallStatusText; // For messages like "Waiting for opponent", "All Ready"

    private PrivateMatchManager privateMatchManager;
    private bool localPlayerIsActuallyReady = false; // Internal state, updated from PMM
    private string localPlayerId; // Set by PMM

    void Awake()
    {
        privateMatchManager = FindFirstObjectByType<PrivateMatchManager>();
        if (privateMatchManager == null)
        {
            Debug.LogError("LobbyPlayerListController: PrivateMatchManager not found!");
            if (overallStatusText != null) overallStatusText.text = "ERROR: Manager Missing";
            if (readyToggleButton != null) readyToggleButton.interactable = false;
            enabled = false;
            return;
        }

        if (playerEntries == null || playerEntries.Count == 0)
        {
            Debug.LogError("LobbyPlayerListController: PlayerEntries not assigned or empty in Inspector!");
            if (overallStatusText != null) overallStatusText.text = "ERROR: UI Setup Issue";
            if (readyToggleButton != null) readyToggleButton.interactable = false;
            enabled = false;
            return;
        }

        if (readyToggleButton != null)
        {
            readyToggleButton.onClick.AddListener(OnReadyToggleButtonClicked);
        }
        else
        {
            Debug.LogWarning("LobbyPlayerListController: ReadyToggleButton not assigned in Inspector.");
        }
        InitializePlayerEntries();
    }

    private void InitializePlayerEntries()
    {
        foreach (var entry in playerEntries)
        {
            if (entry != null)
            {
                entry.SetVacant(); // Initialize all entries as vacant
            }
        }
    }

    void OnEnable()
    {
        if (privateMatchManager == null || !enabled) return;

        PrivateMatchManager.OnLobbyStateRefreshed += HandleLobbyStateRefreshed;

        // Initial UI state
        localPlayerIsActuallyReady = false; // Assume not ready until PMM confirms
        UpdateReadyButtonAppearance(); // Button should be disabled until localPlayerId is known
        if (overallStatusText != null) overallStatusText.text = "Fetching lobby details...";

        // Request initial data. PMM will call OnLobbyStateRefreshed when data is available.
        privateMatchManager.RequestLobbyStateRefresh();
    }

    void OnDisable()
    {
        if (privateMatchManager == null) return;
        PrivateMatchManager.OnLobbyStateRefreshed -= HandleLobbyStateRefreshed;
    }

    // This method is invoked by an event from PrivateMatchManager
    public void HandleLobbyStateRefreshed(List<LobbyPlayerData> players, string localPlayerPmmId, bool isPmmHost)
    {
        if (playerEntries == null) return; // PMM null check already done in Awake

        localPlayerId = localPlayerPmmId;
        // bool isHost = isPmmHost; // Stored if needed for other logic, but mainly used for player entry status here.

        for (int i = 0; i < playerEntries.Count; i++)
        {
            if (i < players.Count)
            {
                LobbyPlayerData pData = players[i];
                string statusText = pData.IsHost ? "Host" : "";
                statusText += pData.IsReady ? (pData.IsHost ? ", Ready" : "Ready") : (pData.IsHost ? ", Not Ready" : "Not Ready");
                if (string.IsNullOrEmpty(statusText) && !pData.IsHost && !pData.IsReady) statusText = "Joined"; // Added !pData.IsReady
                else if (string.IsNullOrEmpty(statusText) && !pData.IsHost && pData.IsReady) statusText = "Ready"; // Added for non-host ready

                bool isLocalPlayerEntry = !string.IsNullOrEmpty(localPlayerId) && pData.PlayerId == localPlayerId;
                if (isLocalPlayerEntry)
                {
                    localPlayerIsActuallyReady = pData.IsReady;
                }
                playerEntries[i].Setup(pData.DisplayName, statusText.TrimStart(',', ' '), false, isLocalPlayerEntry);
            }
            else
            {
                playerEntries[i].SetVacant();
            }
        }
        UpdateOverallStatusText(players);
        UpdateReadyButtonAppearance();
    }

    private void UpdateOverallStatusText(List<LobbyPlayerData> players)
    {
        if (overallStatusText == null || privateMatchManager == null) return;

        // bool isHost = privateMatchManager.IsHost; // TODO: PMM needs to provide IsHost
        int expectedPlayerCount = playerEntries.Count;

        if (players.Count < expectedPlayerCount)
        {
            overallStatusText.text = $"Waiting for {expectedPlayerCount - players.Count} more player(s)...";
        }
        else // All player slots are filled
        {
            bool allPlayersReady = true;
            foreach (var pData in players)
            {
                if (!pData.IsReady)
                {
                    allPlayersReady = false;
                    break;
                }
            }

            if (allPlayersReady)
            {
                // overallStatusText.text = isHost ? "All Ready! Press Start Game." : "All Ready! Waiting for Host to start.";
                // TODO: Requires IsHost from PMM and potentially a Start Game button for the host.
                overallStatusText.text = "All players ready!";
            }
            else
            {
                overallStatusText.text = "Waiting for all players to be ready...";
            }
        }
    }

    private void UpdateReadyButtonAppearance()
    {
        if (readyButtonText != null)
        {
            readyButtonText.text = localPlayerIsActuallyReady ? "Cancel Ready" : "I'm Ready";
        }
        if (readyToggleButton != null)
        {
            // Enable button only if local player is identified (localPlayerId is known)
            // and PMM is available.
            readyToggleButton.interactable = !string.IsNullOrEmpty(localPlayerId) && privateMatchManager != null;
        }
    }

    private void OnReadyToggleButtonClicked()
    {
        if (privateMatchManager != null && !string.IsNullOrEmpty(localPlayerId))
        {
            Debug.Log($"LobbyPlayerListController: Ready button clicked. Current local ready: {localPlayerIsActuallyReady}. Toggling now.");
            privateMatchManager.ToggleLocalPlayerReadyState();

            // The UI will update once PMM confirms the change and sends a new OnLobbyStateRefreshed event.
            // Avoid optimistic UI updates here to rely on PMM as the source of truth.
        }
        else
        {
            Debug.LogWarning("LobbyPlayerListController: Cannot toggle ready state. PMM or LocalPlayerId not available.");
        }
    }

    // TODO: Method to be called by PMM when LocalPlayerId is available
    // public void StoreLocalPlayerId(string id)
    // {
    //     localPlayerId = id;
    //     UpdateReadyButtonAppearance();
    //     // Potentially refresh player list if it was waiting for ID
    // }
}