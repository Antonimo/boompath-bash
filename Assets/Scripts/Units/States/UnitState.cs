public abstract class UnitState
{
    protected Unit unit;

    public UnitState(Unit unit)
    {
        this.unit = unit;
    }

    public virtual void Enter() { }
    public virtual void Update() { }
    public virtual void FixedUpdate() { }  // for physics updates
    public virtual void Exit() { }
}
