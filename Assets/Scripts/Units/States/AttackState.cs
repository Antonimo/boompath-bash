using UnityEngine;

public class AttackState : UnitState
{
    // --- Server-Side Data ---
    private Component _serverTargetComponent;
    private Health _serverTargetHealth;
    private Transform _serverTargetTransform;
    private UnitState _serverReturnState; // State to return to when finished/target lost (Server only)
    private float _attackStartTime;
    private bool _hasHit; // Flag to ensure damage is applied only once per cycle (Server only)

    // --- Client-Side Data (or shared) ---
    // TODO: Sync target via NetworkVariable<ulong> AttackTargetId on Unit
    private ulong _syncedTargetId = 0; // Populated from NV on client
    private Transform _clientTargetTransform; // Cached from synced ID on client
    private FloatingTextManager _floatingTextManager;

    // Attack timing parameters
    private float attackDuration = 1.0f; // Duration of the attack animation/cycle
    private float hitPoint = 0.5f;       // Point in the cycle where damage is applied (0-1)

    // Physics parameters for dead units
    private float deathPushForce = 1342f; // Force applied to push dead units

    // Server-side constructor
    public AttackState(Unit unit, Component target, UnitState returnTo) : base(unit)
    {
        // This constructor is primarily for the SERVER
        if (!unit.IsServer)
        {
            Debug.LogError($"Server AttackState constructor called on client!", unit);
            // Fallback or error handling?
        }

        this._serverTargetComponent = target;
        this._serverReturnState = returnTo;

        if (target != null)
        {
            this._serverTargetHealth = target.GetComponent<Health>();
            this._serverTargetTransform = target.transform; // Get the transform
        }

        FindFloatingTextManager(); // Can be found by both

        // Server logs errors
        if (unit.IsServer)
        {
            if (this._serverTargetComponent == null) Debug.LogError($"[Server] AttackState initialized with a null target.", unit);
            if (this._serverTargetHealth == null) Debug.LogError($"[Server] AttackState target '{target?.name}' does not have a Health component.", target);
            if (this._serverTargetTransform == null) Debug.LogError($"[Server] AttackState target '{target?.name}' does not have a Transform component.", target);
        }
    }

    // Client-side constructor (Placeholder - needs target ID sync)
    // Called via OnNetworkStateChanged
    public AttackState(Unit unit) : base(unit)
    {
        if (unit.IsServer)
        {
            Debug.LogError($"Client AttackState constructor called on Server!", unit);
        }
        FindFloatingTextManager();
        // TODO: Get synced _syncedTargetId from Unit NetworkVariable
        // TODO: Find _clientTargetTransform using _syncedTargetId
    }

    private void FindFloatingTextManager()
    {
        this._floatingTextManager = UnityEngine.Object.FindFirstObjectByType<FloatingTextManager>();
    }

    public override void Enter()
    {
        // Debug.Log($"[{ (unit.IsServer ? "Server" : "Client") }] Unit {unit.NetworkObjectId} Entering AttackState");
        if (unit.IsServer)
        {
            // Server validates target and starts attack cycle
            if (_serverTargetComponent == null || _serverTargetHealth == null || _serverTargetHealth.CurrentHealth <= 0)
            {
                Debug.LogWarning($"[Server] AttackState: Target '{_serverTargetComponent?.name}' is invalid or already dead on Enter. Returning to previous state: {_serverReturnState?.GetType().Name ?? "null"}.", unit);
                unit.ChangeState(_serverReturnState ?? new IdleState(unit)); // Return or Idle if null
                return;
            }

            _attackStartTime = Time.time;
            _hasHit = false;
            Debug.Log($"[Server] Unit {unit.name} entering AttackState against {_serverTargetComponent.name}", unit);

            // TODO: Set Unit's AttackTargetId NetworkVariable
            // if (_serverTargetComponent.TryGetComponent<NetworkObject>(out var targetNetObj)){
            //     unit.AttackTargetId.Value = targetNetObj.NetworkObjectId;
            // }

            // Orient the unit towards the target (server authoritative rotation)
            RotateTowardsTarget(_serverTargetTransform);
        }
        else
        {
            // Client tries to find target based on (future) synced ID
            // TODO: Find _clientTargetTransform using _syncedTargetId
            // TODO: Play attack animation specific to the attacker (unit)
        }
    }

    // Helper for server rotation
    private void RotateTowardsTarget(Transform target)
    {
        if (target != null)
        {
            Vector3 directionToTarget = (target.position - unit.transform.position);
            directionToTarget.y = 0; // Keep rotation horizontal
            if (directionToTarget != Vector3.zero)
            {
                unit.RotateTowards(directionToTarget.normalized);
            }
        }
    }

    public override void Update()
    {
        // Server manages the attack cycle and state transitions
        if (unit.IsServer)
        {
            // Constantly check if the target is still valid and alive
            if (_serverTargetComponent == null || _serverTargetHealth == null || _serverTargetHealth.CurrentHealth <= 0)
            {
                Debug.Log($"[Server] AttackState: Target '{_serverTargetComponent?.name}' lost or dead. Returning to state: {_serverReturnState?.GetType().Name ?? "null"}", unit);
                unit.ChangeState(_serverReturnState ?? new IdleState(unit));
                return;
            }

            // Keep facing the target (server authoritative rotation)
            RotateTowardsTarget(_serverTargetTransform);

            float elapsedTime = Time.time - _attackStartTime;
            float normalizedTime = elapsedTime / attackDuration;

            // Apply damage at the hit point in the attack cycle
            if (!_hasHit && normalizedTime >= hitPoint)
            {
                AttemptAttackServer();
                _hasHit = true; // Mark that damage has been applied for this cycle
            }

            // Check if the attack cycle is complete
            if (normalizedTime >= 1.0f)
            {
                // If target is still alive, restart the attack cycle
                if (_serverTargetHealth.CurrentHealth > 0)
                {
                    // Re-enter state to reset timers and attack again
                    // Note: Direct re-entry might skip Exit/Enter logic needed elsewhere.
                    // Consider changing state back to AttackState for proper lifecycle?
                    // For now, mimicking original logic:
                    _attackStartTime = Time.time;
                    _hasHit = false;
                    Debug.Log($"[Server] Unit {unit.name} restarting attack cycle against {_serverTargetComponent.name}", unit);

                    // Enter(); // Re-enter state to reset timers and attack again
                }
                else
                {
                    // Target died during this cycle, return to the previous state
                    unit.ChangeState(_serverReturnState ?? new IdleState(unit));
                }
            }
        }
        else // Client-side update
        {
            // TODO: Smoothly rotate towards _clientTargetTransform?
            // TODO: Update animations based on timing?
        }
    }

    // Renamed to clarify it's server-only
    private void AttemptAttackServer()
    {
        if (!unit.IsServer) return;
        if (_serverTargetHealth == null || _serverTargetTransform == null) return; // Should not happen if Enter checks passed

        bool didHit = unit.TryHit();
        string resultText = "MISS";

        if (didHit)
        {
            // Calculate damage and record health before applying
            int damage = unit.CalculateDamage();
            int previousHealth = _serverTargetHealth.CurrentHealth;
            _serverTargetHealth.TakeDamage(damage);
            resultText = damage.ToString();
            Debug.Log($"[Server] Unit {unit.name} HIT {_serverTargetComponent.name} for {damage} damage. Health: {_serverTargetHealth.CurrentHealth}/{_serverTargetHealth.MaxHealth}", unit);

            // Check if this hit killed the target *and* if the target is a Unit
            if (previousHealth > 0 && _serverTargetHealth.CurrentHealth <= 0 && _serverTargetComponent is Unit deadUnit)
            {
                ApplyDeathPushServer(deadUnit);
            }
        }
        else
        {
            Debug.Log($"[Server] Unit {unit.name} MISSED {_serverTargetComponent.name}", unit);
        }

        // TODO: Send ClientRpc to show floating text
        // ShowFloatingTextClientRpc(_serverTargetTransform.position, resultText, didHit);
        // Temporary local call for testing:
        ShowFloatingText(_serverTargetTransform.position, resultText, didHit);

    }

    // Placeholder for ClientRpc
    // [ClientRpc]
    // private void ShowFloatingTextClientRpc(Vector3 position, string text, bool isHit)
    // {
    //     ShowFloatingText(position, text, isHit);
    // }

    // Actual logic to show floating text (runs on clients via RPC, or server locally)
    private void ShowFloatingText(Vector3 position, string text, bool isHit)
    {
        if (_floatingTextManager != null)
        {
            _floatingTextManager.ShowText(position, text, isHit);
        }
    }


    // Renamed to clarify it's server-only
    private void ApplyDeathPushServer(Unit deadUnit)
    {
        if (!unit.IsServer) return;

        Rigidbody targetRb = deadUnit.GetComponent<Rigidbody>();
        if (targetRb != null)
        {
            Vector3 horizontal = deadUnit.transform.position - unit.transform.position;
            horizontal.y = 0;
            horizontal.Normalize();
            Vector3 pushDirection = (horizontal + Vector3.up * 2f).normalized;

            targetRb.AddForce(pushDirection * deathPushForce, ForceMode.Impulse);
            Vector3 randomTorque = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized * deathPushForce * 0.5f;
            targetRb.AddTorque(randomTorque, ForceMode.Impulse);
            Debug.Log($"[Server] Applied death push to {deadUnit.name}", unit);
        }
    }

    public override void Exit()
    {
        Debug.Log($"[{(unit.IsServer ? "Server" : "Client")}] Unit {unit.NetworkObjectId} Exiting AttackState. Was attacking {_serverTargetComponent?.name ?? _syncedTargetId.ToString()}", unit);
        // TODO: Stop attack animation (client-side)
        if (unit.IsServer)
        {
            // TODO: Clear Unit's AttackTargetId NetworkVariable
            // unit.AttackTargetId.Value = 0;
        }
    }
}
