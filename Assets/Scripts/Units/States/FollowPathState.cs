using UnityEngine;
using System.Collections.Generic;

public class FollowPathState : UnitState
{
    // The unit holds the path and the current index (server-side)
    private float _stoppingDistance = 0.1f;

    public FollowPathState(Unit unit) : base(unit) { }

    public override void Enter()
    {
        Debug.Log($"[{(unit.IsServer ? "Server" : "Client")}] Unit {unit.NetworkObjectId} Entering FollowPathState");
    }

    public override void Update()
    {
        // Target checking and state changes only happen on the server
        if (unit.IsServer)
        {
            // Check for enemy Units or Bases
            if (unit.CheckForTargetsInRange(out Component target))
            {
                // Transition to AttackState regardless of whether it's a Unit or Base
                // Note: AttackState needs to handle Component target (or be adapted)
                unit.ChangeState(new AttackState(unit, target, this));
                return;
            }
        }
        // Client-side Update logic (if any) could go here
    }

    public override void FixedUpdate()
    {
        // Path following, movement, arrival checks, and state changes only happen on the server
        if (unit.IsServer)
        {
            // Ensure path data is valid (should be, as only server enters this state with a path)
            if (unit.path == null || unit.path.Count == 0 || unit.currentPathIndex >= unit.path.Count)
            {
                Debug.LogError($"[Server] Unit {unit.NetworkObjectId} in FollowPathState with invalid path data. Path count: {unit.path?.Count ?? -1}, Index: {unit.currentPathIndex}. Switching to Idle.", unit);
                unit.ChangeState(new IdleState(unit));
                return;
            }

            Vector3 targetPosition = unit.path[unit.currentPathIndex];

            // Server calculates desired velocity and applies it
            // Vector3 currentPosition = unit.transform.position;
            // Vector3 direction = (targetPosition - currentPosition).normalized;
            // direction.y = 0; // Keep movement planar

            // Rigidbody rb = unit.GetComponent<Rigidbody>();
            // if (rb != null)
            // {
            //     rb.linearVelocity = direction * unit.moveSpeed;

            //     // Rotate towards movement direction (server authoritative)
            //     if (direction != Vector3.zero)
            //     {
            //         unit.RotateTowards(direction);
            //     }
            // }
            // else
            // {
            //     Debug.LogError($"[Server] Unit {unit.NetworkObjectId} missing Rigidbody in FollowPathState.FixedUpdate()", unit);
            // }

            unit.MoveTo(targetPosition); // Replaced with velocity control

            // Server checks arrival at the current waypoint
            if (unit.HasReachedPosition(targetPosition, _stoppingDistance))
            {
                unit.currentPathIndex++;
                // Check if end of path is reached
                if (unit.currentPathIndex >= unit.path.Count)
                {
                    unit.ChangeState(new IdleState(unit)); // Reached end
                    // if (rb != null) rb.linearVelocity = Vector3.zero; // Stop movement
                }
                // Else: Continue to the next waypoint in the next FixedUpdate
            }
        }
        // Client-side FixedUpdate logic (e.g., visual interpolation) could go here
    }

    public override void Exit()
    {
        // Clean up server-side movement on exit
        if (unit.IsServer)
        {
            Rigidbody rb = unit.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
            }
        }
        // Debug.Log($"[{ (unit.IsServer ? "Server" : "Client") }] Unit {unit.NetworkObjectId} Exiting FollowPathState");
    }
}
