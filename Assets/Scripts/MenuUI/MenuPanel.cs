using UnityEngine;
using UnityEngine.UI;

public class MenuPanel : MonoBehaviour
{
    [SerializeField] private Button backButton;
    [SerializeField] private bool clearStackHistory = false;

    private void OnEnable()
    {
        if (backButton != null)
        {
            backButton.onClick.AddListener(OnBackButtonClicked);
        }

        // TODO: onOpen that is called from the MenuManager?
        if (clearStackHistory)
        {
            GetMenuManager().ClearStackHistory();
        }
    }

    private void OnBackButtonClicked()
    {
        GetMenuManager().CloseMenuPanel();
    }

    private MenuManager GetMenuManager()
    {
        return GetComponentInParent<MenuManager>();
    }

}