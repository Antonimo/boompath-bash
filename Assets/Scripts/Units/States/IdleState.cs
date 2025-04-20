public class IdleState : UnitState
{
    public IdleState(Unit unit) : base(unit) { }

    public override void Enter()
    {
        // Show selectable indicator or glow
    }
}
