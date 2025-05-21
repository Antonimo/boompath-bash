using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class MenuManager : MonoBehaviour
{
    [SerializeField] private MenuPanel initialMenuPanel;

    [SerializeField] private List<MenuPanel> currentMenuStack = new List<MenuPanel>();

    private Stack<MenuPanel> menuStack = new Stack<MenuPanel>();

    private void Start()
    {
        MenuPanel[] menuPanels = GetComponentsInChildren<MenuPanel>(true);
        foreach (MenuPanel panel in menuPanels)
        {
            panel.gameObject.SetActive(false);
        }

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
        UpdateStackDebugView();
    }

    public void CloseMenuPanel()
    {
        if (menuStack.Count > 1)
        {
            menuStack.Pop().gameObject.SetActive(false);

            menuStack.Peek().gameObject.SetActive(true);
            UpdateStackDebugView();
        }
        else
        {
            Debug.LogWarning("No menu panel to close");
        }
    }

    public void ClearStackHistory()
    {
        while (menuStack.Count > 1)
        {
            menuStack.Pop().gameObject.SetActive(false);
        }
        UpdateStackDebugView();
    }

    public void ClearAll()
    {
        while (menuStack.Count > 0)
        {
            menuStack.Pop().gameObject.SetActive(false);
        }
        UpdateStackDebugView();
    }

    private void UpdateStackDebugView()
    {
        currentMenuStack.Clear();
        // Stack is LIFO, so we need to reverse it to show the stack order in the Inspector
        foreach (var panel in menuStack.Reverse())
        {
            currentMenuStack.Add(panel);
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