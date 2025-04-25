using UnityEngine;

public class AttackState : UnitState
{
    // Store the target component and its relevant sub-components
    private Component targetComponent;
    private Health targetHealth;
    private Transform targetTransform; // For positioning effects like floating text

    private UnitState returnState; // State to return to when finished/target lost
    private FloatingTextManager floatingTextManager;

    // Attack timing parameters
    private float attackDuration = 1.0f; // Duration of the attack animation/cycle
    private float hitPoint = 0.5f;       // Point in the cycle where damage is applied (0-1)
    private float attackStartTime;
    private bool hasHit; // Flag to ensure damage is applied only once per cycle

    // Physics parameters for dead units
    // 842f
    private float deathPushForce = 1342f; // Force applied to push dead units

    // Constructor accepts a Component target
    public AttackState(Unit unit, Component target, UnitState returnTo) : base(unit)
    {
        this.targetComponent = target;
        this.returnState = returnTo;

        // Try to get essential components from the target
        if (target != null)
        {
            this.targetHealth = target.GetComponent<Health>();
            this.targetTransform = target.transform; // Get the transform
        }

        // Find the FloatingTextManager instance in the scene
        // Consider using a singleton pattern or dependency injection for better management
        this.floatingTextManager = UnityEngine.Object.FindFirstObjectByType<FloatingTextManager>();

        // Log errors if essential components are missing
        if (this.targetComponent == null)
        {
            Debug.LogError($"AttackState initialized with a null target.", unit);
        }
        if (this.targetHealth == null)
        {
            Debug.LogError($"AttackState target '{target?.name}' does not have a Health component.", target);
        }
        if (this.targetTransform == null)
        {
            Debug.LogError($"AttackState target '{target?.name}' does not have a Transform component.", target);
        }
    }

    public override void Enter()
    {
        // Check if the target is valid before starting the attack
        if (targetComponent == null || targetHealth == null || targetHealth.CurrentHealth <= 0)
        {
            Debug.LogWarning($"AttackState: Target '{targetComponent?.name}' is invalid or already dead on Enter. Returning to previous state.", unit);
            unit.ChangeState(returnState);
            return;
        }

        attackStartTime = Time.time;
        hasHit = false;
        // TODO: Play attack animation specific to the attacker (unit)
        Debug.Log($"Unit {unit.name} entering AttackState against {targetComponent.name}", unit);

        // Orient the unit towards the target
        if (targetTransform != null)
        {
            Vector3 directionToTarget = (targetTransform.position - unit.transform.position);
            directionToTarget.y = 0; // Keep rotation horizontal
            if (directionToTarget != Vector3.zero)
            {
                unit.RotateTowards(directionToTarget.normalized);
            }
        }
    }

    public override void Update()
    {
        // Constantly check if the target is still valid and alive
        if (targetComponent == null || targetHealth == null || targetHealth.CurrentHealth <= 0)
        {
            Debug.Log($"AttackState: Target '{targetComponent?.name}' lost or dead. Returning to state: {returnState.GetType().Name}", unit);
            unit.ChangeState(returnState);
            return;
        }

        // Keep facing the target
        if (targetTransform != null)
        {
            Vector3 directionToTarget = (targetTransform.position - unit.transform.position);
            directionToTarget.y = 0;
            if (directionToTarget != Vector3.zero)
            {
                unit.RotateTowards(directionToTarget.normalized);
            }
        }


        float elapsedTime = Time.time - attackStartTime;
        float normalizedTime = elapsedTime / attackDuration;

        // Apply damage at the hit point in the attack cycle
        if (!hasHit && normalizedTime >= hitPoint)
        {
            AttemptAttack();
            hasHit = true; // Mark that damage has been applied for this cycle
        }

        // Check if the attack cycle is complete
        if (normalizedTime >= 1.0f)
        {
            // If target is still alive, restart the attack cycle
            if (targetHealth.CurrentHealth > 0)
            {
                Enter(); // Re-enter state to reset timers and attack again
            }
            else
            {
                // Target died during this cycle, return to the previous state
                unit.ChangeState(returnState);
            }
        }
    }

    private void AttemptAttack()
    {
        if (targetHealth == null || targetTransform == null) return; // Should not happen if Enter checks passed

        // Try to hit based on unit's hit chance
        if (unit.TryHit())
        {
            // Calculate damage and record health before applying
            int damage = unit.CalculateDamage();
            int previousHealth = targetHealth.CurrentHealth;
            targetHealth.TakeDamage(damage);
            Debug.Log($"Unit {unit.name} HIT {targetComponent.name} for {damage} damage. Health: {targetHealth.CurrentHealth}/{targetHealth.MaxHealth}", unit);

            // Show damage floating text
            if (floatingTextManager != null)
            {
                floatingTextManager.ShowText(targetTransform.position, damage.ToString(), true);
            }

            // Check if this hit killed the target *and* if the target is a Unit
            if (previousHealth > 0 && targetHealth.CurrentHealth <= 0 && targetComponent is Unit deadUnit)
            {
                ApplyDeathPush(deadUnit);
            }
        }
        else
        {
            Debug.Log($"Unit {unit.name} MISSED {targetComponent.name}", unit);
            // Show miss floating text
            if (floatingTextManager != null)
            {
                floatingTextManager.ShowText(targetTransform.position, "MISS", false);
            }
        }
    }

    // Apply physics push effect only to Units upon death
    private void ApplyDeathPush(Unit deadUnit)
    {
        Rigidbody targetRb = deadUnit.GetComponent<Rigidbody>();
        if (targetRb != null)
        {
            // Calculate horizontal direction (XZ only)
            Vector3 horizontal = deadUnit.transform.position - unit.transform.position;
            horizontal.y = 0;
            horizontal.Normalize();

            // Calculate push direction with upward angle
            Vector3 pushDirection = (horizontal + Vector3.up * 2f).normalized;

            // Apply the push force and torque
            targetRb.AddForce(pushDirection * deathPushForce, ForceMode.Impulse);

            // Add random rotation torque
            Vector3 randomTorque = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f)
            ).normalized * deathPushForce * 0.5f; // Reduce torque magnitude slightly
            targetRb.AddTorque(randomTorque, ForceMode.Impulse);
            Debug.Log($"Applied death push to {deadUnit.name}", unit);
        }
    }

    public override void Exit()
    {
        Debug.Log($"Unit {unit.name} exiting AttackState. Was attacking {targetComponent?.name}", unit);
        // TODO: Stop attack animation
    }
}
