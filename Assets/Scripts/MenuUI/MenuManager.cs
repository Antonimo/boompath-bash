using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{
    [SerializeField] private MenuPanel initialMenuPanel;

    private Stack<MenuPanel> menuStack = new Stack<MenuPanel>();

    private void Start()
    {
        if (initialMenuPanel != null)
        {
            OpenMenuPanel(initialMenuPanel);
        }
    }

    public void OpenMenuPanel(MenuPanel menuPanel)
    {
        menuPanel.gameObject.SetActive(true);

        if (menuStack.Count > 0)
        {
            menuStack.Peek().gameObject.SetActive(false);
        }

        menuStack.Push(menuPanel);
    }

    public void CloseMenuPanel()
    {
        if (menuStack.Count > 1)
        {
            menuStack.Pop().gameObject.SetActive(false);

            menuStack.Peek().gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning("No menu panel to close");
        }
    }


    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // Stop play mode in Editor
#endif
    }
}