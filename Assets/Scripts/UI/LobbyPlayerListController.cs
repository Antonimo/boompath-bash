using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class LobbyPlayerListController : MonoBehaviour
{
    public List<PlayerEntryUI> playerEntries; // Assign these in the Inspector (e.g., size 2 for 1v1)
    public Button readyToggleButton;
    public TextMeshProUGUI readyButtonText;
    public TextMeshProUGUI overallStatusText; // For messages like "Waiting for opponent", "All Ready"

    private bool localPlayerIsReady = false; // Internal state, updated from LobbyService
    private string localPlayerId; // Set by LobbyService

    private void ValidateDependencies()
    {
        if (LobbyManager.Instance == null)
        {
            Debug.LogError("LobbyPlayerListController: LobbyService instance not found!");
            this.enabled = false;
            return;
        }

        if (playerEntries == null || playerEntries.Count == 0)
        {
            Debug.LogError("LobbyPlayerListController: PlayerEntries not assigned or empty in Inspector!");
            this.enabled = false;
            return;
        }

        if (readyToggleButton == null || readyButtonText == null || overallStatusText == null)
        {
            Debug.LogError("LobbyPlayerListController: One or more UI references are not assigned in Inspector!");
            this.enabled = false;
            return;
        }
    }

    void Awake()
    {
        ValidateDependencies();
    }

    void OnEnable()
    {
        ValidateDependencies();
        if (!this.enabled) return;

        InitializePlayerEntries();

        // Subscribe to lobby events from the centralized service
        LobbyManager.OnLobbyStateBroadcast += HandleLobbyStateRefreshed;

        // Initial UI state
        localPlayerIsReady = false; // Assume not ready until State syncs
        UpdateReadyButtonAppearance();
        overallStatusText.text = "Fetching lobby details...";

        // Request current lobby state without polling - the service maintains up-to-date state
        // through events, so this just triggers a broadcast of the current state
        LobbyManager.Instance.RequestLobbyStateBroadcast();

        readyToggleButton.onClick.AddListener(OnReadyToggleButtonClicked);
    }

    void OnDisable()
    {
        if (LobbyManager.Instance != null)
        {
            LobbyManager.OnLobbyStateBroadcast -= HandleLobbyStateRefreshed;
        }
    }

    private void InitializePlayerEntries()
    {
        foreach (var entry in playerEntries)
        {
            entry?.SetVacant();
        }
    }

    // TODO: be able to kick players if host
    public void HandleLobbyStateRefreshed(List<LobbyPlayerData> players, string localPlayerId, bool isHost)
    {
        this.localPlayerId = localPlayerId;

        for (int i = 0; i < playerEntries.Count; i++)
        {
            if (i < players.Count)
            {
                LobbyPlayerData pData = players[i];
                string statusText = pData.IsReady ? "Ready" : "Not Ready";

                bool isLocalPlayerEntry = !string.IsNullOrEmpty(localPlayerId) && pData.PlayerId == localPlayerId;
                if (isLocalPlayerEntry)
                {
                    localPlayerIsReady = pData.IsReady;
                }
                playerEntries[i].Setup(pData.DisplayName, statusText);
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
        readyButtonText.text = localPlayerIsReady ? "Cancel Ready" : "I'm Ready";

        readyToggleButton.interactable = !string.IsNullOrEmpty(localPlayerId);
    }

    private void OnReadyToggleButtonClicked()
    {
        Debug.Log($"LobbyPlayerListController: Ready button clicked. Current local ready: {localPlayerIsReady}. Toggling now.");

        // Use the centralized service to toggle ready state
        _ = LobbyManager.Instance.ToggleLocalPlayerReadyState();

        readyToggleButton.gameObject.SetActive(false);
    }
}