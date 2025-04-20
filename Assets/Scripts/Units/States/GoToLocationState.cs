using UnityEngine;
using System.Collections.Generic;

public class GoToLocationState : UnitState
{
    private Vector3 _targetPosition;
    private float _stoppingDistance = 0.1f;
    private bool _hasReachedTarget = false;

    // Constructor to set the target position
    public GoToLocationState(Unit unit, Vector3 targetPosition) : base(unit)
    {
        _targetPosition = targetPosition;
    }

    public override void Enter()
    {
        // Initialize movement parameters
        unit.isMoving = true;
        _hasReachedTarget = false;
    }

    public override void Update()
    {
        // State logic happens in Update (decision making)
        if (_hasReachedTarget)
        {
            unit.ChangeState(new IdleState(unit));
        }
    }

    public override void FixedUpdate()
    {
        // Physics-based movement happens in FixedUpdate
        if (!_hasReachedTarget)
        {
            // Use the Unit's movement methods instead of implementing movement here
            unit.MoveTo(_targetPosition);

            // Check if we've reached the target
            if (unit.HasReachedPosition(_targetPosition, _stoppingDistance))
            {
                _hasReachedTarget = true;
                unit.isMoving = false;
            }
        }
    }

    public override void Exit()
    {
        // Clean up after movement is complete
        unit.isMoving = false;
    }
}
