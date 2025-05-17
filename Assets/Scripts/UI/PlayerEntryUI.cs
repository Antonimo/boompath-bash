using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerEntryUI : MonoBehaviour
{
    public TextMeshProUGUI playerNameText;
    public TextMeshProUGUI playerStatusText; // e.g. "Ready", "Not Ready"

    public void Setup(string playerName, string status)
    {
        gameObject.SetActive(true);

        playerNameText.text = playerName;
        playerStatusText.text = status;
    }

    public void SetVacant()
    {
        gameObject.SetActive(true);

        playerNameText.text = "Waiting for Player...";
        playerStatusText.text = string.Empty;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}