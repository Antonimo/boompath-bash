using UnityEngine;
using System.Collections.Generic;

public class FollowPathState : UnitState
{
    // The unit is holding the path and the current index
    private float _stoppingDistance = 0.1f;

    public FollowPathState(Unit unit) : base(unit) { }

    public override void Enter()
    {
        // Initialize movement parameters
        unit.isMoving = true;
    }

    public override void Update()
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

    public override void FixedUpdate()
    {
        // Physics-based movement happens in FixedUpdate

        Vector3 targetPosition = unit.path[unit.currentPathIndex];

        unit.MoveTo(targetPosition);

        // Check if we've reached the target
        if (unit.HasReachedPosition(targetPosition, _stoppingDistance))
        {
            unit.currentPathIndex++;
            if (unit.currentPathIndex >= unit.path.Count)
            {
                // If we've reached the end of the path, change state to Idle
                unit.ChangeState(new IdleState(unit));
            }

        }
    }

    public override void Exit()
    {
        // Clean up after movement is complete
        unit.isMoving = false;
    }
}
