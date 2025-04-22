using UnityEngine;

public class AttackState : UnitState
{
    private Unit enemy;
    private UnitState returnState;
    // TODO: how to make these modifiedable from the inspector?
    private float attackDuration = 1.0f; // Duration of the attack animation
    private float hitPoint = 0.5f; // When during the animation the hit occurs (0-1)
    private float attackStartTime;
    private bool hasHit;
    private FloatingTextManager floatingTextManager;
    private float deathPushForce = 80f; // Force applied to push dead units

    public AttackState(Unit unit, Unit enemy, UnitState returnTo) : base(unit)
    {
        this.enemy = enemy;
        this.returnState = returnTo;
        floatingTextManager = UnityEngine.Object.FindFirstObjectByType<FloatingTextManager>();
    }

    public override void Enter()
    {
        attackStartTime = Time.time;
        hasHit = false;
        // TODO: Play attack animation
    }

    public override void Update()
    {
        if (enemy == null || !enemy.IsAlive)
        {
            unit.ChangeState(returnState);
            return;
        }

        float elapsedTime = Time.time - attackStartTime;
        float normalizedTime = elapsedTime / attackDuration;

        // Check if we should hit
        if (!hasHit && normalizedTime >= hitPoint)
        {
            hasHit = true;

            // Try to hit
            if (unit.TryHit())
            {
                // Calculate and apply damage
                int damage = unit.CalculateDamage();
                int previousHealth = enemy.GetComponent<Health>().CurrentHealth;
                enemy.TakeDamage(damage);

                // TODO: refactor
                // Check if this hit killed the enemy
                if (previousHealth > 0 && enemy.GetComponent<Health>().CurrentHealth <= 0)
                {
                    // Calculate push direction from attacker to victim with upward component
                    Vector3 pushDirection = (enemy.transform.position - unit.transform.position).normalized;
                    pushDirection += Vector3.up * 1.5f; // Add upward component
                    pushDirection.Normalize(); // Re-normalize the direction

                    // Apply the push force and torque
                    Rigidbody enemyRb = enemy.GetComponent<Rigidbody>();
                    if (enemyRb != null)
                    {
                        enemyRb.AddForce(pushDirection * deathPushForce, ForceMode.Impulse);
                        // Add random rotation torque
                        Vector3 randomTorque = new Vector3(
                            Random.Range(-1f, 1f),
                            Random.Range(-1f, 1f),
                            Random.Range(-1f, 1f)
                        ).normalized * deathPushForce;
                        enemyRb.AddTorque(randomTorque, ForceMode.Impulse);
                    }
                }

                // Show damage floating text
                if (floatingTextManager != null)
                {
                    floatingTextManager.ShowText(enemy.transform.position, damage.ToString(), true);
                }
            }
            else
            {
                // Show miss floating text
                if (floatingTextManager != null)
                {
                    floatingTextManager.ShowText(enemy.transform.position, "MISS", false);
                }
            }
        }

        // Check if attack is complete
        if (normalizedTime >= 1.0f)
        {
            // Start next attack
            Enter();
        }
    }
}
