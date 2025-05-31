using UnityEngine;
using TMPro;
using System.Collections; // For IEnumerator

public class PrivateMatchLobbyController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI lobbyCodeText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI copiedMessageText;
    [SerializeField] private float copiedMessageDuration = 1.5f;
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private GameObject countdownPanel;

    [Header("Menu References")]
    [SerializeField] private MenuManager menuManager;

    private void ValidateDependencies()
    {
        if (LobbyManager.Instance == null)
        {
            Debug.LogError("PrivateMatchLobbyController: LobbyService instance not found!");
            enabled = false;
            return;
        }

        if (lobbyCodeText == null || statusText == null || copiedMessageText == null)
        {
            Debug.LogError("PrivateMatchLobbyController: One or more UI references are not assigned in the Inspector for PrivateMatchLobbyController.");
            enabled = false;
            return;
        }

        if (menuManager == null)
        {
            Debug.LogError("PrivateMatchLobbyController: MenuManager not found in the scene!");
            enabled = false;
        }
    }

    void Awake()
    {
        ValidateDependencies();
    }

    void OnEnable()
    {
        Debug.Log("PrivateMatchLobbyController: OnEnable - START");

        ValidateDependencies();
        if (!this.enabled) return;

        // TODO: does it matter if this is done here or in Awake?
        copiedMessageText.gameObject.SetActive(false); // Hide "Copied!" message initially

        // Subscribe to countdown events
        LobbyManager.OnCountdownTick += HandleCountdownTick;
        LobbyManager.OnCountdownComplete += HandleCountdownComplete;

        // Hide countdown UI initially
        countdownPanel.SetActive(false);

        GetLobbyCode();

        statusText.text = "Share join code";

        // Player list and its specific statuses are handled by LobbyPlayerListController.
        // This controller focuses on lobby code and high-level status not covered by LobbyPlayerListController.

        // Request lobby state broadcast to update UI components
        LobbyManager.Instance.RequestLobbyStateBroadcast();

        Debug.Log("PrivateMatchLobbyController: OnEnable - END");
    }

    void OnDisable()
    {
        // Unsubscribe from countdown events
        LobbyManager.OnCountdownTick -= HandleCountdownTick;
        LobbyManager.OnCountdownComplete -= HandleCountdownComplete;

        Debug.Log("PrivateMatchLobbyController: OnDisable");
        StopAllCoroutines(); // Stop any running coroutines, like ShowCopiedMessage
    }

    private void HandleCountdownTick(float remainingTime)
    {
        // Make countdown visible
        if (!countdownPanel.activeSelf)
            countdownPanel.SetActive(true);

        int seconds = Mathf.CeilToInt(remainingTime);
        countdownText.text = $"Game starting in {seconds}...";
    }

    private void HandleCountdownComplete()
    {
        countdownText.text = "Starting game...";
    }

    private void GetLobbyCode()
    {
        string currentLobbyCode = LobbyManager.Instance.LobbyCode;
        lobbyCodeText.text = !string.IsNullOrEmpty(currentLobbyCode) ? currentLobbyCode : "----";
        Debug.Log($"PrivateMatchLobbyController: Got current lobby code: {currentLobbyCode}");
    }

    public void CopyLobbyCodeToClipboard()
    {
        string codeToCopy = lobbyCodeText.text;

        if (string.IsNullOrWhiteSpace(codeToCopy) || codeToCopy == "----" || codeToCopy == "ERROR")
        {
            Debug.LogWarning($"No valid lobby code to copy. Current displayed text: '{codeToCopy ?? "(lobbyCodeText is null)"}'");
            if (copiedMessageText != null)
            {
                StartCoroutine(ShowTemporaryMessage("No code to copy!", 2f));
            }
            return;
        }

        GUIUtility.systemCopyBuffer = codeToCopy;
        Debug.Log($"Copied to clipboard: {codeToCopy}");

        if (copiedMessageText != null)
        {
            StartCoroutine(ShowTemporaryMessage("Copied!", copiedMessageDuration));
        }
    }

    private IEnumerator ShowTemporaryMessage(string message, float duration)
    {
        if (copiedMessageText == null) yield break;

        copiedMessageText.text = message;
        copiedMessageText.gameObject.SetActive(true);
        yield return new WaitForSeconds(duration);
        copiedMessageText.gameObject.SetActive(false);
    }
}