using UnityEngine;
using System.Collections.Generic;

public class GoToLocationState : UnitState
{
    private Vector3 _targetPosition; // Only correct/relevant on the server
    private float _stoppingDistance = 0.1f;
    private bool _hasReachedTarget = false;

    // Constructor to set the target position
    public GoToLocationState(Unit unit, Vector3 targetPosition) : base(unit)
    {
        // Target position is primarily used by the server for movement calculation.
        // Clients receive Vector3.zero here via OnNetworkStateChanged.
        _targetPosition = targetPosition;
    }

    public override void Enter()
    {
        // Reset flag on enter - fine for both server & client representation
        _hasReachedTarget = false;
        Debug.Log($"[{(unit.IsServer ? "Server" : "Client")}] Unit {unit.NetworkObjectId} Entering GoToLocationState towards {_targetPosition}");
    }

    public override void Update()
    {
        // State change decisions only happen on the server
        if (unit.IsServer)
        {
            if (_hasReachedTarget)
            {
                unit.ChangeState(new IdleState(unit));
            }
        }
        // Client-side Update logic (if any) could go here
    }

    public override void FixedUpdate()
    {
        // Authoritative movement and arrival check only happen on the server
        if (unit.IsServer)
        {
            Debug.Log($"[Unit FixedUpdate] Unit {unit.NetworkObjectId} in GoToLocationState _targetPosition: {_targetPosition}");

            if (!_hasReachedTarget)
            {
                // Server calculates desired velocity and applies it
                Vector3 currentPosition = unit.transform.position;
                Vector3 direction = (_targetPosition - currentPosition).normalized;
                // Ignore Y for velocity calculation to keep movement planar
                direction.y = 0;

                // Apply velocity directly
                // Note: Ensure Rigidbody settings (drag, mass) are appropriate
                Rigidbody rb = unit.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = direction * unit.moveSpeed;

                    // Rotate towards movement direction (server authoritative)
                    if (direction != Vector3.zero)
                    {
                        unit.RotateTowards(direction);
                    }
                }
                else
                {
                    // Log error if Rigidbody is missing, should not happen
                    Debug.LogError($"[Server] Unit {unit.NetworkObjectId} missing Rigidbody in GoToLocationState.FixedUpdate()", unit);
                }

                // unit.MoveTo(_targetPosition); // Replaced with velocity control

                // Server checks if target is reached
                if (unit.HasReachedPosition(_targetPosition, _stoppingDistance))
                {
                    _hasReachedTarget = true;
                    // Stop movement when reached
                    if (rb != null) rb.linearVelocity = Vector3.zero;
                }
            }
            else // Ensure velocity is zero if target was reached in a previous frame
            {
                Rigidbody rb = unit.GetComponent<Rigidbody>();
                if (rb != null && rb.linearVelocity != Vector3.zero)
                {
                    rb.linearVelocity = Vector3.zero;
                }
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
            if (rb != null && !_hasReachedTarget) // Stop movement if exiting before reaching target
            {
                rb.linearVelocity = Vector3.zero;
            }
        }
        // Debug.Log($"[{ (unit.IsServer ? "Server" : "Client") }] Unit {unit.NetworkObjectId} Exiting GoToLocationState");
    }
}
