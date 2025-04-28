using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class OnlineMultiplayerUI : MonoBehaviour
{
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private HostStartupManager hostStartupManager;

    private void Awake()
    {
        hostButton.onClick.AddListener(HostButtonClicked);
        clientButton.onClick.AddListener(ClientButtonClicked);

        if (hostStartupManager == null)
        {
            Debug.LogError("OnlineMultiplayerUI: HostStartupManager reference is not set in the Inspector!");
            hostButton.interactable = false;
        }
    }

    private void HostButtonClicked()
    {
        if (hostStartupManager != null)
        {
            hostStartupManager.StartHostWithApprovalSetup();
        }
        else
        {
            Debug.LogError("Cannot start host: HostStartupManager reference is missing.");
        }
    }

    private void ClientButtonClicked()
    {
        NetworkManager.Singleton.StartClient();
    }
}
