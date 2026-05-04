using UnityEngine;

[DisallowMultipleComponent]
public class EnemyMovementFSM : MonoBehaviour
{
    private EnemyAI ai;

    public enum CombatMovementMode
    {
        Sword,
        Bow
    }

    public enum MoveState
    {
        Idle,
        Aligning,
        Chase,
        Retreat
    }

    [Header("Debug Info")]
    [SerializeField] private MoveState currentState;
    [SerializeField] private CombatMovementMode movementMode = CombatMovementMode.Sword;

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Rigidbody2D rb;

    [Header("Movement Modules")]
    [SerializeField] private EnemySwordMovement swordMovement;
    [SerializeField] private EnemyBowMovement bowMovement;

    [Header("Movement Settings")]
    public float moveSpeed = 4f;
    public float accel = 40f;

    [Header("Vertical Logic")]
    public bool useVerticalAlign = true;
    public float verticalTolerance = 0.2f;

    [Header("Dynamic Combat Range")]
    public float desiredRange = 1.5f;
    public float minimumCombatRange = 0f;
    public float rangeTolerance = 0.25f;

    [Header("External Movement Lock")]
    [SerializeField] private bool isExternallyLocked;
    private float externalLockUntil = -999f;

    [Header("Attack Movement Lock")]
    [SerializeField] private bool isAttackMovementLocked;
    [SerializeField] private float defaultAttackLockFailSafe = 0.6f;
    private float attackLockUntil = -999f;

    public bool IsExternallyLocked => isExternallyLocked;
    public bool IsAttackMovementLocked => isAttackMovementLocked;
    public bool IsMovementLocked => isExternallyLocked || isAttackMovementLocked;

    private void Awake()
    {
        ai = GetComponent<EnemyAI>();

        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        swordMovement = GetComponent<EnemySwordMovement>();
        if (swordMovement == null)
            swordMovement = gameObject.AddComponent<EnemySwordMovement>();

        bowMovement = GetComponent<EnemyBowMovement>();
        if (bowMovement == null)
            bowMovement = gameObject.AddComponent<EnemyBowMovement>();
    }

    private void Start()
    {
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player")?.transform;

        currentState = MoveState.Idle;
    }

    public void Tick()
    {
        RefreshMovementLocks();

        if (IsMovementLocked)
        {
            StopMovementVelocity();
            SwitchState(MoveState.Idle);
            return;
        }

        if (player == null || rb == null)
            return;

        UpdateState();
        RunState();
    }

    private void RefreshMovementLocks()
    {
        if (isExternallyLocked && Time.time >= externalLockUntil)
        {
            isExternallyLocked = false;
            externalLockUntil = -999f;
        }

        if (isAttackMovementLocked && attackLockUntil > 0f && Time.time >= attackLockUntil)
        {
            isAttackMovementLocked = false;
            attackLockUntil = -999f;
            StopMovementVelocity();
            SwitchState(MoveState.Idle);
        }
    }

    public void LockExternal(float duration, bool stopVelocity = true)
    {
        isExternallyLocked = true;
        externalLockUntil = Time.time + Mathf.Max(0f, duration);

        if (stopVelocity)
            StopMovementVelocity();

        SwitchState(MoveState.Idle);
    }

    public void UnlockExternal(bool stopVelocity = true)
    {
        isExternallyLocked = false;
        externalLockUntil = -999f;

        if (stopVelocity)
            StopMovementVelocity();

        SwitchState(MoveState.Idle);
    }

    public void LockAttackMovement(float failSafeDuration = -1f, bool stopVelocity = true)
    {
        isAttackMovementLocked = true;

        float safeDuration = failSafeDuration > 0f
            ? failSafeDuration
            : defaultAttackLockFailSafe;

        attackLockUntil = Time.time + safeDuration;

        if (stopVelocity)
            StopMovementVelocity();

        SwitchState(MoveState.Idle);
    }

    public void UnlockAttackMovement(bool stopVelocity = true)
    {
        isAttackMovementLocked = false;
        attackLockUntil = -999f;

        if (stopVelocity)
            StopMovementVelocity();

        SwitchState(MoveState.Idle);
    }

    public void NotifyAttackStarted(float failSafeDuration = -1f)
    {
        LockAttackMovement(failSafeDuration, true);
    }

    public void NotifyAttackEnded()
    {
        UnlockAttackMovement(true);
    }

    public void SetDesiredRange(float range)
    {
        desiredRange = Mathf.Max(0f, range);
    }

    public void SetMinimumCombatRange(float range)
    {
        minimumCombatRange = Mathf.Max(0f, range);
    }

    public void SetMovementMode(CombatMovementMode mode)
    {
        movementMode = mode;
        useVerticalAlign = true;
    }

    public void SetPlayer(Transform target)
    {
        player = target;
    }

    private void UpdateState()
    {
        if (player == null)
            return;

        Vector2 toPlayer = player.position - transform.position;
        float absDistX = Mathf.Abs(toPlayer.x);
        float absDistY = Mathf.Abs(toPlayer.y);

        bool isRetreating = currentState == MoveState.Retreat;
        bool isChasing = currentState == MoveState.Chase;
        bool isAligning = currentState == MoveState.Aligning;

        float retreatThreshold = isRetreating ? minimumCombatRange : minimumCombatRange - rangeTolerance;
        float chaseThreshold = isChasing ? desiredRange : desiredRange + rangeTolerance;
        float alignThreshold = isAligning ? verticalTolerance * 0.5f : verticalTolerance;

        if (movementMode == CombatMovementMode.Bow)
        {
            if (absDistX < retreatThreshold)
            {
                SwitchState(MoveState.Retreat);
                return;
            }

            if (absDistX > chaseThreshold)
            {
                SwitchState(MoveState.Chase);
                return;
            }

            if (useVerticalAlign && absDistY > alignThreshold)
            {
                SwitchState(MoveState.Aligning);
                return;
            }
        }
        else
        {
            if (absDistX > chaseThreshold)
            {
                SwitchState(MoveState.Chase);
                return;
            }

            if (useVerticalAlign && absDistY > alignThreshold)
            {
                SwitchState(MoveState.Aligning);
                return;
            }
        }

        SwitchState(MoveState.Idle);
    }

    private void RunState()
    {
        switch (currentState)
        {
            case MoveState.Idle:
                rb.velocity = Vector2.Lerp(
                    rb.velocity,
                    Vector2.zero,
                    Time.deltaTime * accel
                );
                break;

            case MoveState.Aligning:
                if (movementMode == CombatMovementMode.Bow)
                {
                    bowMovement.Align(
                        player,
                        rb,
                        moveSpeed,
                        accel,
                        verticalTolerance
                    );
                }
                else
                {
                    swordMovement.Align(
                        player,
                        rb,
                        moveSpeed,
                        accel,
                        verticalTolerance
                    );
                }
                break;

            case MoveState.Chase:
                if (movementMode == CombatMovementMode.Bow)
                {
                    bowMovement.Chase(
                        player,
                        rb,
                        moveSpeed,
                        accel,
                        verticalTolerance
                    );
                }
                else
                {
                    swordMovement.Chase(
                        player,
                        rb,
                        moveSpeed,
                        accel
                    );
                }
                break;

            case MoveState.Retreat:
                if (movementMode == CombatMovementMode.Bow)
                {
                    bowMovement.Retreat(
                        player,
                        rb,
                        moveSpeed,
                        accel,
                        verticalTolerance
                    );
                }
                else
                {
                    swordMovement.Retreat(
                        player,
                        rb,
                        moveSpeed,
                        accel
                    );
                }
                break;
        }
    }

    private void SwitchState(MoveState newState)
    {
        if (newState == currentState)
            return;

        currentState = newState;
    }

    private void StopMovementVelocity()
    {
        if (rb != null)
            rb.velocity = Vector2.zero;
    }

    public void StopImmediately()
    {
        StopMovementVelocity();
        SwitchState(MoveState.Idle);
    }

    public void ForceClearAllMovementLocks(bool stopVelocity = true)
    {
        isExternallyLocked = false;
        isAttackMovementLocked = false;

        externalLockUntil = -999f;
        attackLockUntil = -999f;

        if (stopVelocity)
            StopMovementVelocity();

        SwitchState(MoveState.Idle);
    }
}