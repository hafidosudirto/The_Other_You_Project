using UnityEngine;

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

    private void Awake()
    {
        ai = GetComponent<EnemyAI>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        if (player == null) player = GameObject.FindGameObjectWithTag("Player")?.transform;
        currentState = MoveState.Idle;
    }

    public void Tick()
    {
        if (player == null || rb == null) return;
        UpdateState();
        RunState();
    }

    public void SetDesiredRange(float range) { desiredRange = Mathf.Max(0f, range); }
    public void SetMinimumCombatRange(float range) { minimumCombatRange = Mathf.Max(0f, range); }

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
        if (player == null) return;

        Vector2 toPlayer = player.position - transform.position;
        float absDistX = Mathf.Abs(toPlayer.x);
        float absDistY = Mathf.Abs(toPlayer.y);

        // [PERBAIKAN JITTER]: HYSTERESIS
        // Mencegah FSM bolak-balik state jika jarak berada persis di garis batas (threshold)
        bool isRetreating = (currentState == MoveState.Retreat);
        bool isChasing = (currentState == MoveState.Chase);
        bool isAligning = (currentState == MoveState.Aligning);

        float retreatThreshold = isRetreating ? minimumCombatRange : (minimumCombatRange - rangeTolerance);
        float chaseThreshold = isChasing ? desiredRange : (desiredRange + rangeTolerance);
        float alignThreshold = isAligning ? (verticalTolerance * 0.5f) : verticalTolerance;

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
        else // SWORD
        {
            if (useVerticalAlign && absDistY > alignThreshold)
            {
                SwitchState(MoveState.Aligning);
                return;
            }
            if (absDistX > chaseThreshold)
            {
                SwitchState(MoveState.Chase);
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
                rb.velocity = Vector2.Lerp(rb.velocity, Vector2.zero, Time.deltaTime * accel);
                break;
            case MoveState.Aligning:
                DoAlignManeuver();
                break;
            case MoveState.Chase:
                if (movementMode == CombatMovementMode.Bow) DoChaseDiagonal();
                else DoChaseHorizontalOnly();
                break;
            case MoveState.Retreat:
                if (movementMode == CombatMovementMode.Bow) DoRetreatDiagonal();
                else DoRetreatHorizontalOnly();
                break;
        }
    }

    private void SwitchState(MoveState newState)
    {
        if (newState == currentState) return;
        currentState = newState;
    }

    public void StopImmediately()
    {
        if (rb != null) rb.velocity = Vector2.zero;
        SwitchState(MoveState.Idle);
    }

    // ==========================================
    // GERAK SWORD (Horizontal Only)
    // ==========================================
    private void DoAlignManeuver()
    {
        float diffY = player.position.y - transform.position.y;

        // [PERBAIKAN JITTER]: Smooth Braking. Melambat perlahan saat mendekati target Y (mencegah bablas/overshoot)
        float smoothMult = Mathf.Clamp01(Mathf.Abs(diffY) / (verticalTolerance * 2f));
        float dirY = Mathf.Sign(diffY) * smoothMult;

        rb.velocity = Vector2.Lerp(rb.velocity, new Vector2(0f, dirY * moveSpeed), Time.deltaTime * accel);
    }

    private void DoChaseHorizontalOnly()
    {
        float dirX = Mathf.Sign(player.position.x - transform.position.x);
        rb.velocity = Vector2.Lerp(rb.velocity, new Vector2(dirX * moveSpeed, 0f), Time.deltaTime * accel);
    }

    private void DoRetreatHorizontalOnly()
    {
        float dirX = -Mathf.Sign(player.position.x - transform.position.x);
        rb.velocity = Vector2.Lerp(rb.velocity, new Vector2(dirX * moveSpeed, 0f), Time.deltaTime * accel);
    }

    // ==========================================
    // GERAK DIAGONAL BOW (Henry LF2 Style)
    // ==========================================
    private void DoChaseDiagonal()
    {
        float diffX = player.position.x - transform.position.x;
        float diffY = player.position.y - transform.position.y;

        float dirX = Mathf.Sign(diffX);

        // [PERBAIKAN JITTER]: Pengereman halus sumbu Y saat bergerak diagonal
        float smoothMultY = Mathf.Clamp01(Mathf.Abs(diffY) / (verticalTolerance * 2f));
        float dirY = Mathf.Sign(diffY) * smoothMultY;

        Vector2 targetVel = new Vector2(dirX, dirY).normalized * moveSpeed;
        rb.velocity = Vector2.Lerp(rb.velocity, targetVel, Time.deltaTime * accel);
    }

    private void DoRetreatDiagonal()
    {
        float diffX = player.position.x - transform.position.x;
        float diffY = player.position.y - transform.position.y;

        float dirX = -Mathf.Sign(diffX);
        float smoothMultY = Mathf.Clamp01(Mathf.Abs(diffY) / (verticalTolerance * 2f));
        float dirY = Mathf.Sign(diffY) * smoothMultY;

        Vector2 targetVel = new Vector2(dirX, dirY).normalized * moveSpeed;
        rb.velocity = Vector2.Lerp(rb.velocity, targetVel, Time.deltaTime * accel);
    }
}