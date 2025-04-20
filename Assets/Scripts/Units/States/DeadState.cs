public class DeadState : UnitState
{
    public DeadState(Unit unit) : base(unit) { }

    public override void Enter()
    {
        unit.IsAlive = false;
        // unit.Die();
        // Play death animation
    }

    public override void Update() { } // No-op
}
