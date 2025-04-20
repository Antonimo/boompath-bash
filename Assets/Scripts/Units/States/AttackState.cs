public class AttackState : UnitState
{
    private Unit enemy;
    private UnitState returnState;
    private float lastAttackTime;
    private float attackCooldown = 1.0f;

    public AttackState(Unit unit, Unit enemy, UnitState returnTo) : base(unit)
    {
        this.enemy = enemy;
        this.returnState = returnTo;
    }

    public override void Enter()
    {
        // Play attack animation
    }

    public override void Update()
    {
        // if (enemy == null || !enemy.IsAlive)
        // {
        //     unit.ChangeState(returnState);
        //     return;
        // }

        // if (Time.time - lastAttackTime >= attackCooldown)
        // {
        //     lastAttackTime = Time.time;
        //     enemy.TakeDamage(unit.AttackPower);
        // }
    }
}
