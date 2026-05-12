using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody2D))]
public class MinionMeleeController : Enemy
{
    public enum MinionState { Idle, Walk, Attack, Damaged }

    [Header("Minion Current State")]
    public MinionState currentState = MinionState.Idle;

    [Header("Minion AI Settings")]
    public float detectionRange = 15f;
    public float attackRange = 1.2f;
    public float attackCooldown = 2f;

    [Tooltip("Durasi minion diam ketika terkena damage. Untuk testing, gunakan 1.0 agar animasi terlihat jelas.")]
    public float damagedDuration = 1.0f;

    [Header("Animator State Name")]
    [Tooltip("Nama lengkap state animasi damage di Animator. Sesuaikan jika nama state berbeda.")]
    [SerializeField] private string damagedStateFullPath = "MinionMelee_Damaged";

    [Header("Orbit & Anti-Stacking")]
    public float hordeRadius = 2.5f;
    public float separationRadius = 1.5f;
    public float separationWeight = 2f;

    [Header("Horizontal Attack & Stage Bounds")]
    [Tooltip("Jika aktif, minion hanya boleh melakukan pendekatan dan hit serangan secara horizontal ketika siap menyerang.")]
    public bool horizontalAttackOnly = true;

    [Tooltip("Selisih Y maksimum antara minion dan player agar serangan horizontal dianggap valid.")]
    public float attackYTolerance = 0.45f;

    [Tooltip("Batas Y terendah jalur minion.")]
    public float minY = -4f;

    [Tooltip("Batas Y tertinggi jalur minion.")]
    public float maxY = 1.6f;

    [Tooltip("Tinggi area serangan horizontal. Naikkan jika serangan terlalu sulit mengenai player pada jalur yang sama.")]
    public float horizontalAttackHeight = 0.75f;

    [Tooltip("Lebar area serangan horizontal. Jika bernilai 0 atau kurang, script memakai attackRange.")]
    public float horizontalAttackWidth = 0f;

    [Tooltip("Jarak pusat area serangan dari posisi minion. Jika bernilai 0 atau kurang, script memakai setengah horizontalAttackWidth.")]
    public float horizontalAttackOffset = 0f;

    [Header("Stage Settings (DDA)")]
    public int attackTokens = 3;
    public bool isBoss = false;

    [Header("Stage Manager Attack Token Integration")]
    [Tooltip("Jika aktif, token dikonsumsi melalui StageManager ketika minion memasuki state Attack.")]
    public bool useStageManagerAttackToken = true;

    [Tooltip("Jika aktif, setiap percobaan menyerang menghabiskan 1 token, meskipun hitbox tidak mengenai player.")]
    public bool consumeTokenOnAttackAttempt = true;

    private Transform player;
    private Animator animator;
    private MinionDeathHandler deathHandler;
    private StageManager stageManager;

    private float stateTimer;
    private float cooldownTimer;

    private float assignedOrbitAngle;

    private bool isDead = false;
    private bool canAttackByToken = true;
    private int damagedStateHash;

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int DamagedHash = Animator.StringToHash("Damaged");

    protected override void Awake()
    {
        base.Awake();

        animator = GetComponent<Animator>();
        deathHandler = GetComponent<MinionDeathHandler>();

        damagedStateHash = Animator.StringToHash(damagedStateFullPath);

        FindPlayer();
        FindStageManager();

        assignedOrbitAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        ClampPositionToStageY();
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");

        if (playerObj != null)
        {
            player = playerObj.transform;
        }
    }

    private void FindStageManager()
    {
        if (stageManager == null)
        {
            stageManager = FindObjectOfType<StageManager>();
        }
    }

    public void InitializeStageEnemy(CharacterBase character, int tokens, bool bossStatus)
    {
        attackTokens = Mathf.Max(0, tokens);
        isBoss = bossStatus;
        canAttackByToken = isBoss || attackTokens > 0;
        FindStageManager();

        if (character != null)
        {
            maxHP = character.maxHP;
            currentHP = character.currentHP;
            attack = character.attack;
            moveSpeed = character.moveSpeed;
            defense = character.defense;
        }

        Debug.Log($"<color=orange>[VISUALISASI STAT STAGE]</color> {gameObject.name} Spawned! " +
                  $"HP: {currentHP} | ATK: {attack} | SPD: {moveSpeed} | Token Serang: {attackTokens}");
    }

    public void SetRemainingAttackTokens(int remainingTokens)
    {
        attackTokens = Mathf.Max(0, remainingTokens);
        canAttackByToken = isBoss || attackTokens > 0;
    }

    public void SetAttackTokens(int remainingTokens)
    {
        SetRemainingAttackTokens(remainingTokens);
    }

    public int GetRemainingAttackTokens()
    {
        return isBoss ? int.MaxValue : Mathf.Max(0, attackTokens);
    }

    public bool HasAttackTokens()
    {
        return isBoss || (canAttackByToken && attackTokens > 0);
    }

    public void SetCanAttack(bool value)
    {
        canAttackByToken = isBoss || value;

        if (!canAttackByToken && !isBoss)
        {
            attackTokens = 0;
        }
    }

    public void SetAttackEnabled(bool value)
    {
        SetCanAttack(value);
    }

    public void SetCanUseAttack(bool value)
    {
        SetCanAttack(value);
    }

    public void DisableAttack()
    {
        SetCanAttack(false);
    }

    public void StopAttacking()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator != null)
        {
            animator.ResetTrigger(AttackHash);
        }
    }

    public override void TakeDamage(float dmg, GameObject attacker = null)
    {
        if (isDead || currentHP <= 0f)
            return;

        Debug.Log($"<color=yellow>[CEK HIT]</color> TakeDamage Minion dipanggil! Damage masuk: {dmg}");

        float hpBefore = currentHP;

        base.TakeDamage(dmg, attacker);

        if (isDead || currentHP <= 0f)
            return;

        if (currentHP < hpBefore)
        {
            Debug.Log("<color=green>[CEK HIT]</color> Minion terkena damage. Memainkan animasi Damaged.");
            ChangeState(MinionState.Damaged);
        }
    }

    protected override void Update()
    {
        base.Update();

        if (isDead || currentHP <= 0f)
            return;

        if (stateTimer > 0f)
            stateTimer -= Time.deltaTime;

        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        switch (currentState)
        {
            case MinionState.Idle:
                HandleIdle();
                break;

            case MinionState.Walk:
                HandleWalk();
                break;

            case MinionState.Attack:
                HandleAttack();
                break;

            case MinionState.Damaged:
                HandleDamaged();
                break;
        }

        ClampPositionToStageY();
    }

    private void HandleIdle()
    {
        if (player == null)
        {
            FindPlayer();
            return;
        }

        float distToPlayer = Vector2.Distance(transform.position, player.position);

        if (distToPlayer <= detectionRange)
        {
            ChangeState(MinionState.Walk);
        }
    }

    private void HandleWalk()
    {
        if (player == null)
        {
            ChangeState(MinionState.Idle);
            return;
        }

        Vector2 minionPosition = transform.position;
        Vector2 playerPosition = player.position;

        float distToPlayer = Vector2.Distance(minionPosition, playerPosition);
        float horizontalDistance = Mathf.Abs(playerPosition.x - minionPosition.x);
        float verticalDistance = Mathf.Abs(playerPosition.y - minionPosition.y);

        if (distToPlayer > detectionRange)
        {
            ChangeState(MinionState.Idle);
            return;
        }

        Vector2 targetPosition;
        Vector2 moveDirection = Vector2.zero;

        bool isReadyToAttack = cooldownTimer <= 0f && CanUseAttackTokenForDecision();
        bool isHorizontalAttackMotion = horizontalAttackOnly && isReadyToAttack;

        if (isReadyToAttack)
        {
            if (horizontalAttackOnly)
            {
                bool isAlignedOnY = verticalDistance <= attackYTolerance;

                if (!isAlignedOnY)
                {
                    // Koreksi jalur dilakukan vertikal terlebih dahulu, bukan diagonal.
                    float yDirection = Mathf.Sign(playerPosition.y - minionPosition.y);
                    moveDirection = new Vector2(0f, yDirection);
                }
                else
                {
                    if (horizontalDistance > attackRange * 0.8f)
                    {
                        float xDirection = Mathf.Sign(playerPosition.x - minionPosition.x);
                        moveDirection = new Vector2(xDirection, 0f);
                    }

                    if (horizontalDistance <= attackRange)
                    {
                        FacePlayerHorizontally();
                        ChangeState(MinionState.Attack);
                        return;
                    }
                }
            }
            else
            {
                if (distToPlayer > attackRange * 0.8f)
                {
                    moveDirection = ((Vector2)player.position - (Vector2)transform.position).normalized * 1.2f;
                }

                if (distToPlayer <= attackRange)
                {
                    ChangeState(MinionState.Attack);
                    return;
                }
            }
        }
        else
        {
            Vector2 offset = new Vector2(
                Mathf.Cos(assignedOrbitAngle),
                Mathf.Sin(assignedOrbitAngle)
            ) * hordeRadius;

            targetPosition = new Vector2(
                playerPosition.x + offset.x,
                Mathf.Clamp(playerPosition.y + offset.y, minY, maxY)
            );

            float distToOrbitPoint = Vector2.Distance(transform.position, targetPosition);

            if (distToOrbitPoint > 0.2f)
            {
                moveDirection = (targetPosition - (Vector2)transform.position).normalized;
            }
        }

        Vector2 separation = Vector2.zero;
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, separationRadius);

        foreach (Collider2D hit in hitColliders)
        {
            if (hit.gameObject == gameObject)
                continue;

            if (hit.GetComponent<MinionMeleeController>() == null)
                continue;

            Vector2 diff = (Vector2)transform.position - (Vector2)hit.transform.position;
            float distance = diff.magnitude;

            if (distance < 0.01f)
            {
                separation += new Vector2(
                    Random.Range(-1f, 1f),
                    Random.Range(-1f, 1f)
                ).normalized;
            }
            else
            {
                separation += diff.normalized / distance;
            }
        }

        Vector2 finalMoveDir = (moveDirection + separation * separationWeight).normalized;

        if (isHorizontalAttackMotion && moveDirection.sqrMagnitude > 0.01f)
        {
            finalMoveDir = GetAxisLockedDirection(moveDirection);
        }

        if (moveDirection.sqrMagnitude > 0.01f || separation.sqrMagnitude > 0.01f)
        {
            transform.position += (Vector3)(finalMoveDir * moveSpeed * Time.deltaTime);
            ClampPositionToStageY();
        }

        FacePlayerHorizontally();
    }

    private void HandleAttack()
    {
        if (stateTimer <= 0f)
        {
            cooldownTimer = attackCooldown;
            ChangeState(MinionState.Walk);
        }
    }

    private void HandleDamaged()
    {
        if (stateTimer <= 0f)
        {
            ChangeState(MinionState.Walk);
        }
    }

    public void ChangeState(MinionState newState)
    {
        if (isDead)
            return;

        if (currentHP <= 0f && newState != MinionState.Damaged)
            return;

        currentState = newState;

        switch (newState)
        {
            case MinionState.Idle:
                animator.SetBool(IsMovingHash, false);
                break;

            case MinionState.Walk:
                animator.ResetTrigger(AttackHash);
                animator.ResetTrigger(DamagedHash);
                animator.SetBool(IsMovingHash, true);
                break;

            case MinionState.Attack:
                float damageForThisAttack = Mathf.Max(0f, attack);

                if (!TryConsumeAttackTokenBeforeAttack(damageForThisAttack))
                {
                    currentState = MinionState.Walk;
                    animator.ResetTrigger(AttackHash);
                    animator.SetBool(IsMovingHash, true);
                    cooldownTimer = Mathf.Max(cooldownTimer, 0.25f);
                    return;
                }

                animator.SetBool(IsMovingHash, false);
                animator.ResetTrigger(DamagedHash);
                animator.SetTrigger(AttackHash);

                stateTimer = 1f;
                cooldownTimer = attackCooldown + 1f;

                GiveDamageToPlayer(damageForThisAttack);
                FinalizeAttackTokenAfterAttack();
                break;

            case MinionState.Damaged:
                PlayDamagedAnimation();

                stateTimer = damagedDuration;
                cooldownTimer = Mathf.Max(cooldownTimer, damagedDuration + 0.5f);
                break;
        }
    }

    private void PlayDamagedAnimation()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        animator.SetBool(IsMovingHash, false);
        animator.ResetTrigger(AttackHash);
        animator.ResetTrigger(DamagedHash);

        if (animator.HasState(0, damagedStateHash))
        {
            animator.Play(damagedStateHash, 0, 0f);
            animator.Update(0f);

            Debug.Log($"<color=cyan>[ANIM]</color> Memaksa play state: {damagedStateFullPath}");
        }
        else
        {
            Debug.LogError($"<color=red>[ANIM ERROR]</color> State '{damagedStateFullPath}' tidak ditemukan. " +
                           "Cek nama layer dan nama state pada Animator.");

            animator.SetTrigger(DamagedHash);
        }
    }

    public override void Die()
    {
        if (isDead)
            return;

        isDead = true;
        currentHP = 0f;

        StartCoroutine(DieAfterDamagedAnimation());
    }

    private IEnumerator DieAfterDamagedAnimation()
    {
        currentState = MinionState.Damaged;

        PlayDamagedAnimation();

        Collider2D col = GetComponent<Collider2D>();

        if (col != null)
            col.enabled = false;

        yield return new WaitForSeconds(damagedDuration);

        if (deathHandler != null)
        {
            deathHandler.HandleDeath();
            Debug.Log($"{name} MATI");
            Destroy(gameObject);
        }
        else
        {
            base.Die();
        }
    }

    private bool CanUseAttackTokenForDecision()
    {
        if (isBoss)
            return true;

        if (!canAttackByToken || attackTokens <= 0)
            return false;

        return true;
    }

    private bool TryConsumeAttackTokenBeforeAttack(float expectedDamage)
    {
        if (isBoss || !consumeTokenOnAttackAttempt)
            return true;

        if (!CanUseAttackTokenForDecision())
        {
            Debug.Log($"<color=orange>[MINION TOKEN]</color> {name} gagal menyerang karena token habis.");
            return false;
        }

        if (useStageManagerAttackToken)
        {
            FindStageManager();

            if (stageManager != null)
            {
                bool consumedByStageManager = stageManager.TryConsumeAttackTokenForEnemy(
                    gameObject,
                    expectedDamage,
                    "MinionMeleeController.Attack"
                );

                if (consumedByStageManager)
                {
                    SetRemainingAttackTokens(
                        stageManager.GetRemainingAttackTokensForEnemy(gameObject, attackTokens)
                    );
                    return true;
                }

                if (stageManager.HasAttackTokenRuntimeData(gameObject))
                {
                    SetRemainingAttackTokens(0);
                    return false;
                }
            }
        }

        attackTokens = Mathf.Max(0, attackTokens - 1);
        canAttackByToken = attackTokens > 0;

        Debug.Log($"<color=orange>[MINION TOKEN LOCAL]</color> {name} memakai 1 token lokal. Sisa: {attackTokens}");

        return true;
    }

    private void FinalizeAttackTokenAfterAttack()
    {
        if (isBoss || !consumeTokenOnAttackAttempt || !useStageManagerAttackToken)
            return;

        FindStageManager();

        if (stageManager != null)
        {
            stageManager.FinalizeAttackTokenConsumptionForEnemy(gameObject);
            SetRemainingAttackTokens(
                stageManager.GetRemainingAttackTokensForEnemy(gameObject, attackTokens)
            );
        }
    }

    private void GiveDamageToPlayer(float damageAmount)
    {
        if (player == null)
            FindPlayer();

        if (player == null)
            return;

        FacePlayerHorizontally();

        if (horizontalAttackOnly)
        {
            float attackWidth = horizontalAttackWidth > 0f ? horizontalAttackWidth : attackRange;
            float attackOffset = horizontalAttackOffset > 0f ? horizontalAttackOffset : attackWidth * 0.5f;
            float facingDirection = isFacingRight ? 1f : -1f;

            Vector2 boxCenter = (Vector2)transform.position + Vector2.right * facingDirection * attackOffset;
            Vector2 boxSize = new Vector2(attackWidth, horizontalAttackHeight);

            Collider2D[] hits = Physics2D.OverlapBoxAll(boxCenter, boxSize, 0f);

            foreach (Collider2D hit in hits)
            {
                if (!hit.CompareTag("Player"))
                    continue;

                CharacterBase playerStats = hit.GetComponent<CharacterBase>();

                if (playerStats != null)
                {
                    playerStats.TakeDamage(damageAmount, gameObject);
                    Debug.Log($"<color=red>[MINION ATTACK]</color> Minion hit Player secara horizontal! Damage: {damageAmount} | Sisa Token: {attackTokens}");
                    return;
                }
            }

            return;
        }

        Collider2D playerCol = Physics2D.OverlapCircle(transform.position, attackRange);

        if (playerCol == null)
            return;

        if (!playerCol.CompareTag("Player"))
            return;

        CharacterBase fallbackPlayerStats = playerCol.GetComponent<CharacterBase>();

        if (fallbackPlayerStats != null)
        {
            fallbackPlayerStats.TakeDamage(damageAmount, gameObject);
            Debug.Log($"<color=red>[MINION ATTACK]</color> Minion hit Player! Damage: {damageAmount} | Sisa Token: {attackTokens}");
        }
    }

    private Vector2 GetAxisLockedDirection(Vector2 inputDirection)
    {
        if (Mathf.Abs(inputDirection.x) >= Mathf.Abs(inputDirection.y))
        {
            return new Vector2(Mathf.Sign(inputDirection.x), 0f);
        }

        return new Vector2(0f, Mathf.Sign(inputDirection.y));
    }

    private void FacePlayerHorizontally()
    {
        if (player == null)
            return;

        float faceDirection = player.position.x - transform.position.x;

        if (Mathf.Abs(faceDirection) > 0.1f)
        {
            if ((faceDirection > 0f && !isFacingRight) || (faceDirection < 0f && isFacingRight))
            {
                Flip();
            }
        }
    }

    private void ClampPositionToStageY()
    {
        Vector3 clampedPosition = transform.position;
        clampedPosition.y = Mathf.Clamp(clampedPosition.y, minY, maxY);
        transform.position = clampedPosition;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        float attackWidth = horizontalAttackWidth > 0f ? horizontalAttackWidth : attackRange;
        float attackOffset = horizontalAttackOffset > 0f ? horizontalAttackOffset : attackWidth * 0.5f;
        float facingDirection = isFacingRight ? 1f : -1f;

        Vector2 boxCenter = (Vector2)transform.position + Vector2.right * facingDirection * attackOffset;
        Vector2 boxSize = new Vector2(attackWidth, horizontalAttackHeight);

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(boxCenter, boxSize);

        Gizmos.color = Color.yellow;
        Vector3 leftBoundStart = new Vector3(transform.position.x - 2f, minY, transform.position.z);
        Vector3 leftBoundEnd = new Vector3(transform.position.x + 2f, minY, transform.position.z);
        Vector3 rightBoundStart = new Vector3(transform.position.x - 2f, maxY, transform.position.z);
        Vector3 rightBoundEnd = new Vector3(transform.position.x + 2f, maxY, transform.position.z);

        Gizmos.DrawLine(leftBoundStart, leftBoundEnd);
        Gizmos.DrawLine(rightBoundStart, rightBoundEnd);
    }
#endif
}
