# Logging Guidelines

This document outlines the standard approach for adding debug logging within the project. Consistent logging helps with debugging during development without cluttering the console in builds or during focused testing.

## Core Principles

1.  **Contextual Prefix:** All log messages should start with a prefix indicating the class or component they originate from (e.g., `[Unit]`, `[Player]`, `[GameManager]`).
2.  **Inspector Toggles:** Debug logs (standard, Update, FixedUpdate) should be toggleable via `[SerializeField]` boolean flags in the Unity Inspector. This allows enabling/disabling logs per instance or prefab without code changes.
3.  **Dedicated Helper Methods:** Use private helper methods within each class to handle the conditional logging and prefixing.
4.  **Standard Warning/Error Logging:** Use `Debug.LogWarning` and `Debug.LogError` via helpers for important warnings and errors. These should generally _not_ be toggleable, as they indicate potential problems.

## Guidelines Summary

- Add the `[SerializeField]` boolean flags for the log types you need (`enableDebugLogs`, `enableUpdateDebugLogs`, `enableFixedUpdateDebugLogs`).
- Implement the corresponding private helper methods (`DebugLog`, `DebugLogUpdate`, `DebugLogFixedUpdate`) that check the flags.
- **Crucially:** Update the prefix string (e.g., `[MyComponent]`) in each helper method to match the actual class name.
- Use `DebugLogWarning` and `DebugLogError` for warnings and errors, passing `this` as the context object where appropriate.
- Call the helper methods throughout your class instead of `Debug.Log` directly for toggleable messages.
- **Avoid redundant comments:** Do not add comments like `// Called logging helper` when simply invoking one of the `DebugLog...` methods. The method name itself makes the intent clear.

## Implementation Example

Follow this pattern when adding logging to a `MonoBehaviour` or other classes:

```csharp
using UnityEngine;

public class MyComponent : MonoBehaviour // Or NetworkBehaviour, etc.
{
    // --- Logging Configuration ---
    [Header("Debug Logging")] // Optional: Group in inspector
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool enableUpdateDebugLogs = false; // If Update logs are needed
    [SerializeField] private bool enableFixedUpdateDebugLogs = false; // If FixedUpdate logs are needed

    // --- Unity Methods ---
    void Start()
    {
        DebugLog("MyComponent has started.");
    }

    void Update()
    {
        // Example usage in Update
        DebugLogUpdate("Performing update tasks.");
    }

    void FixedUpdate()
    {
        // Example usage in FixedUpdate
        DebugLogFixedUpdate("Performing physics tasks.");
    }

    // --- Public/Private Methods ---
    public void DoSomethingImportant()
    {
        DebugLog("Executing DoSomethingImportant.");
        if (/* some error condition */)
        {
            DebugLogError("Critical error occurred in DoSomethingImportant!");
        }
    }

    // --- Logging Helper Methods ---

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            // Replace [MyComponent] with the actual class name
            Debug.Log($"[MyComponent] {message}");
        }
    }

    private void DebugLogUpdate(string message)
    {
        if (enableUpdateDebugLogs)
        {
            // Replace [MyComponent Update] with the actual class name + context
            Debug.Log($"[MyComponent Update] {message}");
        }
    }

    private void DebugLogFixedUpdate(string message)
    {
        if (enableFixedUpdateDebugLogs)
        {
            // Replace [MyComponent FixedUpdate] with the actual class name + context
            Debug.Log($"[MyComponent FixedUpdate] {message}");
        }
    }

    // Helper for Warnings (generally not toggleable)
    private void DebugLogWarning(string message)
    {
        // Pass 'this' to allow clicking the log message to highlight the object
        Debug.LogWarning($"[MyComponent Warning] {message}", this);
    }

    // Helper for Errors (not toggleable)
    private void DebugLogError(string message)
    {
        // Pass 'this' to allow clicking the log message to highlight the object
        Debug.LogError($"[MyComponent Error] {message}", this);
    }
}

```
