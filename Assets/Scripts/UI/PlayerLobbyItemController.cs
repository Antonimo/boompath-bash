using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class PlayerLobbyItemController : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI playerNameText;
    public TextMeshProUGUI playerStatusText; // e.g., "Ready", "Not Ready", "Waiting for Player...", "Host"
    public Button readyButton;
    public TextMeshProUGUI readyButtonText;

    private bool isLocalPlayer = false;
    private bool currentPlayerReadyStatus = false;
    private string currentPlayerId = null;

    public event Action<string, bool> OnPlayerReadyButtonToggled; // string: playerId, bool: new isReady state

    void Awake()
    {
        if (readyButton != null)
        {
            readyButton.onClick.AddListener(ToggleReadyStatus);
        }
        else
        {
            Debug.LogError("PlayerLobbyItemController: ReadyButton is not assigned in the Inspector!");
        }
        ClearDisplay(); // Initial state
    }

    public void SetupForPlayer(string playerId, string displayName, bool isReady, bool isHost, bool isLocal)
    {
        gameObject.SetActive(true);
        currentPlayerId = playerId;
        isLocalPlayer = isLocal;
        currentPlayerReadyStatus = isReady;

        if (playerNameText != null) playerNameText.text = displayName;

        string status = "";
        if (isHost)
        {
            status = "Host";
        }
        if (isReady)
        {
            status += (string.IsNullOrEmpty(status) ? "" : " - ") + "Ready";
        }
        else
        {
            status += (string.IsNullOrEmpty(status) ? "" : " - ") + "Not Ready";
        }
        if (playerStatusText != null) playerStatusText.text = status;


        if (readyButton != null)
        {
            readyButton.gameObject.SetActive(isLocalPlayer); // Only local player can click their ready button
            if (isLocalPlayer && readyButtonText != null)
            {
                readyButtonText.text = isReady ? "Cancel Ready" : "I'm Ready";
            }
        }
    }

    public void SetToWaitingSlot()
    {
        gameObject.SetActive(true);
        currentPlayerId = null;
        isLocalPlayer = false;
        if (playerNameText != null) playerNameText.text = "Player Slot";
        if (playerStatusText != null) playerStatusText.text = "Waiting for Player...";
        if (readyButton != null)
        {
            readyButton.gameObject.SetActive(false);
        }
    }

    public void ClearDisplay()
    {
        gameObject.SetActive(false); // Hide the whole item if not in use or empty
        currentPlayerId = null;
        isLocalPlayer = false;
        if (playerNameText != null) playerNameText.text = "";
        if (playerStatusText != null) playerStatusText.text = "";
        if (readyButton != null)
        {
            readyButton.gameObject.SetActive(false);
        }
    }

    private void ToggleReadyStatus()
    {
        if (!isLocalPlayer || string.IsNullOrEmpty(currentPlayerId))
        {
            Debug.LogWarning("PlayerLobbyItemController: ToggleReadyStatus called, but not local player or no player ID.");
            return;
        }

        bool newReadyState = !currentPlayerReadyStatus;
        // The event will trigger PrivateMatchManager to change the state,
        // and the UI will be updated when the manager broadcasts the change.
        OnPlayerReadyButtonToggled?.Invoke(currentPlayerId, newReadyState);
    }

    void OnDestroy()
    {
        if (readyButton != null)
        {
            readyButton.onClick.RemoveListener(ToggleReadyStatus);
        }
    }
}