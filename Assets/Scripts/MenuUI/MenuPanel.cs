using UnityEngine;
using UnityEngine.UI;

public class MenuPanel : MonoBehaviour
{
    [SerializeField] private Button backButton;

    private void OnEnable()
    {
        if (backButton != null)
        {
            backButton.onClick.AddListener(OnBackButtonClicked);
        }
    }

    private void OnBackButtonClicked()
    {
        MenuManager menuManager = GetComponentInParent<MenuManager>();
        menuManager.CloseMenuPanel();
    }

}