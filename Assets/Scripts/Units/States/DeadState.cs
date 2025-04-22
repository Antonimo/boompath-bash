using UnityEngine;

public class DeadState : UnitState
{
    public DeadState(Unit unit) : base(unit) { }

    public override void Enter()
    {
        unit.IsAlive = false;

        // Unfreeze X and Z rotation constraints
        Rigidbody rb = unit.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Remove FreezeRotationX and FreezeRotationZ from constraints
            rb.constraints &= ~(RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ);
        }

        // unit.Die();
        // Play death animation
    }

    public override void Update() { } // No-op
}
