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
    private bool localPlayerIsReady = false; // Internal state, updated from PrivateMatchManager
    private string localPlayerId; // Set by PrivateMatchManager

    private void ValidateDependencies()
    {
        privateMatchManager = FindFirstObjectByType<PrivateMatchManager>();
        if (privateMatchManager == null)
        {
            Debug.LogError("LobbyPlayerListController: PrivateMatchManager not found!");
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

        PrivateMatchManager.OnLobbyStateBroadcast += HandleLobbyStateRefreshed;

        // Initial UI state
        localPlayerIsReady = false; // Assume not ready until State syncs
        UpdateReadyButtonAppearance();
        overallStatusText.text = "Fetching lobby details...";

        privateMatchManager.RequestLobbyStateRefresh("LobbyPlayerListController OnEnable");

        readyToggleButton.onClick.AddListener(OnReadyToggleButtonClicked);
    }

    void OnDisable()
    {
        PrivateMatchManager.OnLobbyStateBroadcast -= HandleLobbyStateRefreshed;
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
        _ = privateMatchManager.ToggleLocalPlayerReadyState();

        readyToggleButton.gameObject.SetActive(false);
    }

}