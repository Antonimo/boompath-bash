using UnityEngine;

public class DeadState : UnitState
{
    public DeadState(Unit unit) : base(unit) { }

    public override void Enter()
    {
        Debug.Log($"[{(unit.IsServer ? "Server" : "Client")}] Unit {unit.NetworkObjectId} entering DeadState");

        // unit.IsAlive = false; // Moved to Unit.HandleDeath (server) and Unit.OnNetworkStateChanged (client)

        // Server potentially modifies physics state for death effects
        if (unit.IsServer)
        {
            // Unfreeze X and Z rotation constraints to allow tumbling
            Rigidbody rb = unit.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Note: Ensure NetworkRigidbody is configured to sync constraints if clients need this authoritatively,
                // otherwise, clients just observe the resulting motion.
                rb.constraints &= ~(RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ);
            }
        }

        // Client-side effects
        if (!unit.IsServer) // Execute on clients and host
        {
            // TODO: Play death animation
            // TODO: Disable other components like colliders locally? (See comment in Unit.HandleDeath)
            // Example:
            // unit.GetComponent<Collider>().enabled = false;
        }

    }

    // Dead units typically don't do anything in Update/FixedUpdate
    public override void Update() { }
    public override void FixedUpdate() { }

    public override void Exit()
    {
        // Typically no exit logic needed from DeadState, but added for completeness
        Debug.Log($"[{(unit.IsServer ? "Server" : "Client")}] Unit {unit.NetworkObjectId} exiting DeadState (Should not happen usually)");
    }
}
