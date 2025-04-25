# Boompath Bash - Game Architecture

This document outlines the architecture for managing different player types (Human and Bot) within the game.

## Core Principle: Composition over Inheritance

Instead of using inheritance (`HumanPlayer`, `BotPlayer`) or conditional logic within a single `Player` script, we will use **composition**. This means separating the player's data from the player's control logic using distinct components.

## Components and Structure

1.  **`Player` GameObject:**

    - **Purpose:** Represents a player entity in the game scene (e.g., `Player1`, `Player2`).
    - **Scripts:**
      - `Player.cs`: This existing MonoBehaviour remains responsible for holding all **data** associated with the player, such as:
        - `playerName`
        - `playerColor`
        - `teamId`
        - `ownedUnits` (List of `Unit` references)
        - `playerBase` (Reference to the player's base `Transform`)
      - This script should **not** contain control logic (neither human input handling nor AI decisions).

2.  **`BotLogic` Child GameObject (Conditional):**
    - **Purpose:** To house the AI control logic **only for bot players**.
    - **Structure:** This GameObject is added as a **child** of the `Player` GameObject _only_ if that player is intended to be a bot.
    - **Scripts:**
      - `Bot.cs` (New MonoBehaviour):
        - **Responsibility:** Implements the AI decision-making process for the bot.
        - **Functionality:**
          - Gets a reference to the `Player` component on its parent GameObject (`GetComponentInParent<Player>()`).
          - In its `Update()` or via Coroutines, it continuously performs bot actions:
            - Checks for pending units using `player.HasPendingUnits()`.
            - Selects a pending unit from `player.OwnedUnits`.
            - Chooses a predefined path for the unit.
            - Instructs the unit to follow the path.
        - **Operates Continuously:** The bot logic runs independently of the human player turn system.

## Human Player Control

- **Control Flow:** The existing system for human players remains largely unchanged.
  - `GameManager`: Manages game states and likely orchestrates player turns in local multiplayer mode.
  - `PlayerTurn` (or similar): Handles input detection (mouse/touch) for selecting units during a human player's turn.
  - `PathManager` / `SimplePathDrawing` (or similar): Handles input detection for drawing paths during a human player's turn.
- **Interaction:** These systems interact with the specific `Player` instance whose turn it is, but they **do not** need to know about or interact with the `Bot.cs` script. Human players simply won't have the `BotLogic` child GameObject or the active `Bot.cs` component.

## Benefits of this Architecture

- **Separation of Concerns:** Player data (`Player.cs`) is cleanly separated from AI control logic (`Bot.cs`).
- **Composition:** Leverages Unity's preferred component-based design pattern.
- **Minimal Disruption:** Requires minimal changes to the existing `Player.cs` script and the human player control flow (`GameManager`, `PlayerTurn`, `PathManager`).
- **Clarity:** It's explicit in the scene hierarchy which players are controlled by AI.
- **Flexibility:** Easy to enable/disable bot behavior by adding/removing or activating/deactivating the `BotLogic` child GameObject or `Bot.cs` component. Scalable if different bot logic components were needed later.

## Future Considerations (Online Play)

While not the immediate focus, this architecture provides a foundation for online play:

- **Human Players:** A `NetworkPlayerController` component could be added (similar to `Bot.cs`) to handle receiving commands from a remote player and applying them to the local `Player` data.
- **Bots:** Bot logic remains the same, running locally on the server or host.
