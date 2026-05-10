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

    [Header("Stage Settings (DDA)")]
    public int attackTokens = 3;
    public bool isBoss = false;

    private Transform player;
    private Animator animator;
    private MinionDeathHandler deathHandler;

    private float stateTimer;
    private float cooldownTimer;

    private float assignedOrbitAngle;

    private bool isDead = false;
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

        assignedOrbitAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");

        if (playerObj != null)
        {
            player = playerObj.transform;
        }
    }

    public void InitializeStageEnemy(CharacterBase character, int tokens, bool bossStatus)
    {
        attackTokens = tokens;
        isBoss = bossStatus;

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

        float distToPlayer = Vector2.Distance(transform.position, player.position);

        if (distToPlayer > detectionRange)
        {
            ChangeState(MinionState.Idle);
            return;
        }

        Vector2 targetPosition;
        Vector2 moveDirection = Vector2.zero;

        bool isReadyToAttack = cooldownTimer <= 0f && attackTokens > 0;

        if (isReadyToAttack)
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
        else
        {
            Vector2 offset = new Vector2(
                Mathf.Cos(assignedOrbitAngle),
                Mathf.Sin(assignedOrbitAngle)
            ) * hordeRadius;

            targetPosition = (Vector2)player.position + offset;

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

        if (moveDirection.sqrMagnitude > 0.01f || separation.sqrMagnitude > 0.01f)
        {
            transform.position += (Vector3)(finalMoveDir * moveSpeed * Time.deltaTime);
        }

        float faceDirection = player.position.x - transform.position.x;

        if (Mathf.Abs(faceDirection) > 0.1f)
        {
            if ((faceDirection > 0f && !isFacingRight) || (faceDirection < 0f && isFacingRight))
            {
                Flip();
            }
        }
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
                animator.SetBool(IsMovingHash, false);
                animator.ResetTrigger(DamagedHash);
                animator.SetTrigger(AttackHash);

                stateTimer = 1f;
                cooldownTimer = attackCooldown + 1f;

                GiveDamageToPlayer();
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

    private void GiveDamageToPlayer()
    {
        Collider2D playerCol = Physics2D.OverlapCircle(transform.position, attackRange);

        if (playerCol == null)
            return;

        if (!playerCol.CompareTag("Player"))
            return;

        CharacterBase playerStats = playerCol.GetComponent<CharacterBase>();

        if (playerStats != null)
        {
            playerStats.TakeDamage(attack, gameObject);
            Debug.Log($"<color=red>[MINION ATTACK]</color> Minion hit Player! Damage: {attack}");
        }
    }
}