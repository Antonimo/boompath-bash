using UnityEngine;

public class IdleState : UnitState
{
    public IdleState(Unit unit) : base(unit) { }

    public override void Enter()
    {
        // Debug.Log($"[{ (unit.IsServer ? "Server" : "Client") }] Unit {unit.NetworkObjectId} Entering IdleState");
        // TODO: Play Idle animation?
        // TODO: Show selectable indicator or glow (client-side visual)
    }

    // Idle units might still check for targets periodically on the server
    public override void Update()
    {
        if (unit.IsServer)
        {
            // Example: Periodically check for targets?
            // Or maybe this logic belongs in a different "Guard" state?
            // if (Time.frameCount % 30 == 0) { // Check every half-second approx
            //    if (unit.CheckForTargetsInRange(out Component target)) {
            //        unit.ChangeState(new AttackState(unit, target, this));
            //    }
            // }
        }
    }

    public override void FixedUpdate()
    {
        // Ensure velocity is zero if we somehow entered Idle with momentum
        if (unit.IsServer)
        {
            Rigidbody rb = unit.GetComponent<Rigidbody>();
            if (rb != null && rb.linearVelocity != Vector3.zero)
            {
                rb.linearVelocity = Vector3.zero;
            }
        }
    }

    public override void Exit()
    {
        // Debug.Log($"[{ (unit.IsServer ? "Server" : "Client") }] Unit {unit.NetworkObjectId} Exiting IdleState");
        // TODO: Hide selectable indicator?
    }
}
