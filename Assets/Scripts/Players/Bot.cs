using UnityEngine;
using System.Collections;
using System.Collections.Generic; // Added for List

public class Bot : MonoBehaviour
{
    [SerializeField] private Player player; // Reference to the Player component on the parent
    [SerializeField] private BotsManager botsManager; // Reference to the BotsManager
    private bool _isBotThinking = false;

    // TODO: check for IsBot? or set IsBot to true?
    void Awake()
    {
        if (player == null)
        {
            player = GetComponentInParent<Player>();
            if (player == null)
            {
                Debug.LogError("[Bot] Player component not found on parent GameObject.", this.gameObject);
                enabled = false; // Disable this script if Player is not found
                return;
            }
        }

        // TODO: looks like prefabs cannot keep references to objects in the scene?
        if (botsManager == null)
        {
            Debug.LogError("[Bot] BotsManager not found in the scene. Bot cannot function without paths.", this.gameObject);
            enabled = false; // Disable if manager is not found
            return;
        }

        player.IsBot = true;
        Debug.Log($"[Bot] Initialized for player {player.OwnerClientId}");
    }

    private void LateUpdate()
    {
        if (player == null || botsManager == null) return;

        // If bot is not already thinking and its player has pending units, start the decision process
        if (!_isBotThinking && player.HasPendingUnits())
        {
            _isBotThinking = true; // Set flag to prevent starting multiple coroutines
            StartCoroutine(BotAssignPathAfterDelay());
        }
    }

    private IEnumerator BotAssignPathAfterDelay()
    {
        // Simulate thinking delay
        float delay = Random.Range(4.0f, 10.0f); // Wait 4 to 10 seconds
        yield return new WaitForSeconds(delay);

        // TODO: Refactor bot flow with game flow: instead of waiting and then immidiately acting,
        // the bot should not "lock" itself into a Coroutine,
        // but on every (late) update react to the game state.
        // if the bot is "thinking", check if the game state changed - is the thinking still relevant?
        // when done thinking, can the bot act on what he was planning? or did the game state change and 
        // something else needs to happen?

        // Ensure BotsManager is still valid (scene changes, etc.)
        if (botsManager == null)
        {
            Debug.LogError("[Bot] BotsManager reference lost.");
            _isBotThinking = false; // Reset flag
            yield break; // Exit coroutine
        }

        // Find the first pending unit owned by the player
        // TODO: refactor to use method on Player
        Unit pendingUnit = null;
        foreach (Unit unit in player.OwnedUnits) // Use player.OwnedUnits
        {
            // Ensure unit is not null (might have been destroyed)
            if (unit != null && unit.IsPending)
            {
                pendingUnit = unit;
                break; // Found the first pending unit
            }
        }

        if (pendingUnit != null)
        {
            List<Vector3> selectedPath = botsManager.GetRandomPath();

            Debug.Log($"[Bot] Assigning path to unit {pendingUnit.gameObject.name} for player {player.OwnerClientId}");

            pendingUnit.FollowPath(selectedPath);
        }
        else
        {
            // This case should technically not happen if player.HasPendingUnits() returned true,
            // but good to handle just in case.
            Debug.LogWarning($"[Bot] No pending units found after delay for player {player.OwnerClientId}, though HasPendingUnits was true.");
        }

        // Reset the flag after the process is complete
        _isBotThinking = false;
    }
}