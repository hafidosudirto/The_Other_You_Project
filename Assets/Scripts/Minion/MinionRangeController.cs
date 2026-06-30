using System.Collections;
using UnityEngine;

/// <summary>
/// Controller Minion Range (busur) — musuh jarak jauh.
///
/// Mengikuti pola MinionMeleeController untuk state machine utama
/// (Idle/Walk/Attack/Damaged) + manajemen token attack dari StageManager,
/// tetapi dengan movement sub-state machine (Idle/Aligning/Chase/Retreat)
/// mengikuti pola boss Bow (EnemyMovementFSM.CombatMovementMode.Bow):
///
///   - distanceToPlayer dihitung dari Vector2.Distance (VEKTOR, bukan horizontal).
///   - Jika terlalu jauh (dist > desiredRange + tolerance): Chase.
///   - Jika terlalu dekat (dist < minimumCombatRange): Retreat.
///   - Jika selisih Y > verticalTolerance: Aligning (koreksi vertikal dulu).
///   - Selain itu: Idle (diam, animator IsMoving=false).
///
/// Damage tidak diberikan langsung dari controller. Saat Attack, trigger
/// animator `Attack` → Animation Event memanggil
/// BowAnimationEventRelay_MinionRange.AE_BowQuickShot_Release() →
/// MinionRange_Projectile.ReleaseFromAnimationEvent() → spawn panah.
///
/// StageManager token tetap dipakai agar konsisten dengan minion melee
/// (global concurrent-attack-cap per stage).
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody2D))]
public class MinionRangeController : Enemy
{
    public enum MinionState { Idle, Walk, Attack, Damaged }

    public enum MovementSubState { Idle, Aligning, Chase, Retreat }

    [Header("Minion Current State")]
    public MinionState currentState = MinionState.Idle;
    public MovementSubState movementSubState = MovementSubState.Idle;

    [Header("Minion AI Settings")]
    [Tooltip("Jarak deteksi: player masuk AI aktif bila dalam jarak ini.")]
    public float detectionRange = 15f;

    [Header("Combat Range (Boss Bow pattern)")]
    [Tooltip("Jarak ideal serangan. Minion berusaha menjaga jarak ini dari player.")]
    public float desiredRange = 5f;

    [Tooltip("Jarak minimum. Jika player lebih dekat, minion mundur (anti-melee).")]
    public float minimumCombatRange = 2f;

    [Tooltip("Hysteresis untuk mencegah perpindahan state yang terlalu cepat.")]
    public float rangeTolerance = 0.25f;

    [Header("Vertical Alignment")]
    [Tooltip("Selisih Y maksimum agar dianggap sudah align (tidak perlu koreksi vertikal).")]
    public float verticalTolerance = 0.45f;

    [Header("Attack Cooldown")]
    [Tooltip("Cooldown antar tembakan (detik).")]
    public float attackCooldown = 2f;

    [Tooltip("Durasi state Attack (animasi Attack). Dirancang ≈ 0.55–0.65s untuk animation event release.")]
    public float attackStateDuration = 0.65f;

    [Header("Attack Movement Lock")]
    [Tooltip("Jika aktif, minion benar-benar DIAM selama menyerang (tidak chase/retreat/align). " +
             "Mencegah minion 'menggeser' saat animasi menembak masih berjalan.")]
    public bool lockMovementWhileAttacking = true;

    [Tooltip("Tambahan waktu kunci pergerakan SETELAH state Attack selesai, untuk menutupi sisa " +
             "(recovery) animasi tembak agar minion tidak langsung bergerak begitu panah lepas.")]
    [Min(0f)]
    public float attackMovementLockTail = 0.15f;

    [Header("Damaged State")]
    [Tooltip("Durasi minion diam ketika terkena damage (animasi Damaged).")]
    public float damagedDuration = 1.0f;

    [Tooltip("Nama lengkap state animasi damage di Animator.")]
    [SerializeField] private string damagedStateFullPath = "MinionRange_Damaged";

    [Header("Orbit & Anti-Stacking (saat cooldown)")]
    public float hordeRadius = 2.5f;
    public float separationRadius = 1.5f;
    public float separationWeight = 2f;

    [Header("Stage Y Bounds")]
    public float minY = -4f;
    public float maxY = 1.6f;

    [Header("Ranged Attack")]
    [Tooltip("Prefab projectile (panah). Auto-forward ke MinionRange_Projectile.arrowPrefab saat Start.")]
    public GameObject projectilePrefab;

    [Tooltip("FirePoint Transform. Boleh dikosongkan; akan di-auto-find 'FirePoint'/'ArrowSpawnPoint'.")]
    public Transform firePoint;

    [Tooltip("Koefisien damage projectile terhadap stat attack Minion. " +
             "Damage yang dikirim ke MinionArrowDamage = attack × multiplier ini. " +
             "Mis. 1.0 = damage = attack; 0.6 = damage = 60% attack (panah lebih lemah dari melee).")]
    [Range(0f, 2f)]
    public float projectileDamageMultiplier = 1.0f;

    [Header("MinionRange_Projectile (auto-assigned)")]
    [Tooltip("Komponen skill spawn-projectile khusus Minion Range. Auto-find di children.")]
    public MinionRange_Projectile projectileSkill;

    [Header("Stage Settings (DDA)")]
    [Tooltip("Batas jumlah minion yang boleh menyerang bersamaan pada stage ini. Dikirim oleh StageManager.")]
    public int attackTokens = 2;
    public bool isBoss = false;

    [Header("Concurrent Attack Token Gate")]
    [SerializeField] private float tokenDeniedRetryDelay = 0.2f;
    [SerializeField] private bool allowAttackWithoutStageManager = true;

    [Header("Concurrent Attack Token Runtime Debug")]
    public bool hasActiveAttackToken = false;
    public int activeAttackersInStage = 0;
    public int attackTokenCapacityInStage = 0;

    private StageManager stageManager;
    private bool hasReservedAttackToken = false;

    private Transform player;
    private Animator animator;
    private MinionDeathHandler deathHandler;

    private float stateTimer;
    private float cooldownTimer;

    // Sisa waktu kunci pergerakan akibat menyerang (state Attack + tail recovery).
    private float attackMoveLockTimer;

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
        FindStageManager();

        assignedOrbitAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;

        ClampPositionToStageY();
    }

    private void Start()
    {
        EnsureProjectileConfigured();
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

    private void EnsureProjectileConfigured()
    {
        if (projectileSkill == null)
        {
            projectileSkill = GetComponentInChildren<MinionRange_Projectile>(true);

            if (projectileSkill == null)
                projectileSkill = transform.root.GetComponentInChildren<MinionRange_Projectile>(true);
        }

        if (projectileSkill != null)
        {
            // Forward field inspector ke komponen skill agar tidak double-setting.
            if (projectilePrefab != null)
                projectileSkill.arrowPrefab = projectilePrefab;

            if (firePoint != null)
                projectileSkill.firePoint = firePoint;

            // Set owner = komponen CharacterBase pada root prefab (MinionRangeController : Enemy : CharacterBase).
            if (projectileSkill.GetType().GetField("owner",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    is System.Reflection.FieldInfo ownerField)
            {
                CharacterBase self = this;
                ownerField.SetValue(projectileSkill, self);
            }
        }
    }

    public void InitializeStageEnemy(CharacterBase character, int tokens, bool bossStatus)
    {
        attackTokens = Mathf.Max(0, tokens);
        attackTokenCapacityInStage = Mathf.Max(0, tokens);
        activeAttackersInStage = 0;
        hasActiveAttackToken = false;
        hasReservedAttackToken = false;
        isBoss = bossStatus;

        FindStageManager();

        if (character != null)
        {
            maxHP = character.maxHP;
            currentHP = character.currentHP;
            attack = character.attack;
            moveSpeed = character.moveSpeed;
            defense = character.defense;
        }

        // Forward scaled attack stat ke MinionRange_Projectile agar damage
        // projectile ikut naik ketika stage naik (stat multiplier).
        ApplyProjectileDamageFromAttack();

        Debug.Log($"<color=orange>[VISUALISASI STAT STAGE]</color> {gameObject.name} Spawned! " +
                  $"HP: {currentHP} | ATK: {attack} | SPD: {moveSpeed} | " +
                  $"DEF: {defense} | ProjectileDMG: {(projectileSkill != null ? projectileSkill.damage : 0f):F1} | " +
                  $"Batas Minion Menyerang Bersamaan: {attackTokenCapacityInStage}");
    }

    /// <summary>
    /// Hitung damage projectile = attack × projectileDamageMultiplier dan
    /// forward ke MinionRange_Projectile.damage. Dipanggil setiap kali
    /// attack stat berubah (InitializeStageEnemy).
    /// </summary>
    private void ApplyProjectileDamageFromAttack()
    {
        if (projectileSkill == null)
            return;

        float scaledDamage = Mathf.Max(0f, attack * Mathf.Max(0f, projectileDamageMultiplier));
        projectileSkill.damage = scaledDamage;

        // Knockback & stun tetap pakai nilai default MinionRange_Projectile
        // (tidak di-scale oleh stat multiplier — agar feel konsisten tiap stage).
        // Jika ingin ikut scale, assign di sini dari attack ratio.
    }

    public void SetStageAttackTokenRuntime(bool isUsingToken, int activeTokens, int tokenCapacity)
    {
        hasActiveAttackToken = isUsingToken;
        hasReservedAttackToken = isUsingToken;
        activeAttackersInStage = Mathf.Max(0, activeTokens);
        attackTokenCapacityInStage = Mathf.Max(0, tokenCapacity);
        attackTokens = attackTokenCapacityInStage;
    }

    public void SetConcurrentAttackTokenRuntime(bool isUsingToken, int activeTokens, int tokenCapacity)
    {
        SetStageAttackTokenRuntime(isUsingToken, activeTokens, tokenCapacity);
    }

    public void SetRemainingAttackTokens(int tokens)
    {
        attackTokenCapacityInStage = Mathf.Max(0, tokens);
        attackTokens = attackTokenCapacityInStage;
    }

    public void SetAttackTokens(int tokens)
    {
        SetRemainingAttackTokens(tokens);
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

        if (attackMoveLockTimer > 0f)
            attackMoveLockTimer -= Time.deltaTime;

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

    /// <summary>
    /// Logika movement versi ranged.
    /// - Update movement sub-state (Idle/Aligning/Chase/Retreat) berdasarkan desiredRange.
    /// - Terapkan movement + animator IsMoving sesuai sub-state.
    /// - Jika dalam zona serang & cooldown habis → trigger Attack.
    /// </summary>
    private void HandleWalk()
    {
        if (player == null)
        {
            ChangeState(MinionState.Idle);
            return;
        }

        Vector2 minionPosition = transform.position;
        Vector2 playerPosition = player.position;
        Vector2 toPlayer = playerPosition - minionPosition;

        float distToPlayer = toPlayer.magnitude;
        float absDeltaY = Mathf.Abs(toPlayer.y);

        if (distToPlayer > detectionRange)
        {
            ChangeState(MinionState.Idle);
            return;
        }

        // ----- Kunci pergerakan saat/seusai menyerang -----
        // Menutupi sisa (tail) animasi tembak setelah state Attack berakhir agar minion
        // tidak langsung chase/retreat/align selagi animasi menembak masih terlihat.
        if (lockMovementWhileAttacking && attackMoveLockTimer > 0f)
        {
            if (rb != null)
                rb.velocity = Vector2.zero;

            animator.SetBool(IsMovingHash, false);
            FacePlayerHorizontally();
            return;
        }

        // ----- Tentukan movement sub-state -----
        bool isReadyToAttack = cooldownTimer <= 0f;

        // Jika cooldown habis & dalam zone serang → tetap Idle movement (diam & aim).
        // Zone serang: minimumCombatRange <= dist <= desiredRange + tolerance.
        bool isInShootingZone =
            distToPlayer >= minimumCombatRange &&
            distToPlayer <= desiredRange + rangeTolerance;

        if (isReadyToAttack && isInShootingZone)
        {
            UpdateMovementSubState(MovementSubState.Idle, distToPlayer, absDeltaY, lockChase: true);
        }
        else
        {
            UpdateMovementSubState(MovementSubState.Idle, distToPlayer, absDeltaY, lockChase: false);
        }

        // ----- Terapkan movement sesuai sub-state -----
        Vector2 moveDirection = Vector2.zero;

        switch (movementSubState)
        {
            case MovementSubState.Idle:
                // Diam, animator IsMoving=false. Tetap face player.
                break;

            case MovementSubState.Aligning:
                // Koreksi vertikal dulu (jangan diagonal).
                moveDirection = new Vector2(0f, Mathf.Sign(toPlayer.y));
                break;

            case MovementSubState.Chase:
                // Jalan ke player (vektor, sesuai boss Bow).
                moveDirection = toPlayer.normalized;
                break;

            case MovementSubState.Retreat:
                // Mundur dari player — horizontal saja (abaikan DeltaY).
                // Mundur vertikal (naik/turun) terasa janggal untuk busur & membuat
                // sprite kelihatan moonwalk saat hadap player.
                // Edge case: kalau player tepat di atas/bawah (toPlayer.x == 0),
                // pakai hadap sprite terakhir sebagai arah mundur.
                float retreatX;
                if (Mathf.Abs(toPlayer.x) > 0.0001f)
                {
                    retreatX = -Mathf.Sign(toPlayer.x);
                }
                else
                {
                    retreatX = isFacingRight ? 1f : -1f;
                }
                moveDirection = new Vector2(retreatX, 0f);
                break;
        }

        // Anti-stacking dengan minion lain (selama movement).
        Vector2 separation = Vector2.zero;
        if (movementSubState != MovementSubState.Idle)
        {
            Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, separationRadius);

            foreach (Collider2D hit in hitColliders)
            {
                if (hit.gameObject == gameObject)
                    continue;

                if (hit.GetComponent<MinionRangeController>() == null &&
                    hit.GetComponent<MinionMeleeController>() == null)
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
        }

        Vector2 finalMoveDir = (moveDirection + separation * separationWeight).normalized;

        if (moveDirection.sqrMagnitude > 0.01f || separation.sqrMagnitude > 0.01f)
        {
            transform.position += (Vector3)(finalMoveDir * moveSpeed * Time.deltaTime);
            ClampPositionToStageY();
        }

        // ----- Update animator IsMoving -----
        bool isMoving = movementSubState != MovementSubState.Idle;
        animator.SetBool(IsMovingHash, isMoving);

        // ----- Face player hanya bila BUKAN sedang Retreat -----
        // Saat Retreat, sprite tetap hadap ke arah terakhir (tidak flip ke player)
        // agar gerakan mundur tidak terlihat seperti moonwalk.
        bool isRetreating = movementSubState == MovementSubState.Retreat;

        // ----- Trigger Attack jika siap -----
        if (isReadyToAttack && isInShootingZone && absDeltaY <= verticalTolerance)
        {
            if (!isRetreating)
                FacePlayerHorizontally();
            TryChangeToAttackState();
        }
        else
        {
            if (!isRetreating)
                FacePlayerHorizontally();
        }
    }

    /// <summary>
    /// Tentukan movement sub-state berdasarkan jarak & offset vertikal.
    /// lockChase: jika true (sedang cooldown habis & dalam shooting zone), paksa Idle
    /// meskipun distToPlayer > desiredRange sedikit (tetap diam & aim).
    /// </summary>
    private void UpdateMovementSubState(MovementSubState current, float distToPlayer, float absDeltaY, bool lockChase)
    {
        // Prioritas: Retreat > Aligning > Chase > Idle.

        if (distToPlayer < minimumCombatRange)
        {
            SwitchMovementSubState(MovementSubState.Retreat);
            return;
        }

        if (absDeltaY > verticalTolerance)
        {
            SwitchMovementSubState(MovementSubState.Aligning);
            return;
        }

        float chaseThreshold = lockChase
            ? Mathf.Max(0f, desiredRange - rangeTolerance)
            : desiredRange + rangeTolerance;

        if (distToPlayer > chaseThreshold)
        {
            SwitchMovementSubState(MovementSubState.Chase);
            return;
        }

        SwitchMovementSubState(MovementSubState.Idle);
    }

    private void SwitchMovementSubState(MovementSubState newSubState)
    {
        if (movementSubState == newSubState)
            return;

        movementSubState = newSubState;
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

    private bool TryChangeToAttackState()
    {
        if (!TryReserveAttackToken())
        {
            cooldownTimer = Mathf.Max(cooldownTimer, tokenDeniedRetryDelay);
            FacePlayerHorizontally();
            return false;
        }

        ChangeState(MinionState.Attack);
        return true;
    }

    private bool TryReserveAttackToken()
    {
        if (isBoss)
            return true;

        if (hasReservedAttackToken)
            return true;

        FindStageManager();

        if (stageManager == null)
        {
            if (!allowAttackWithoutStageManager)
                return false;

            hasReservedAttackToken = true;
            hasActiveAttackToken = true;
            return true;
        }

        bool acquired = stageManager.TryAcquireMinionAttackToken(
            gameObject,
            "MinionRangeController.ChangeState(Attack)"
        );

        if (acquired)
        {
            hasReservedAttackToken = true;
            hasActiveAttackToken = true;
        }

        return acquired;
    }

    private void ReleaseAttackToken(string reason)
    {
        if (isBoss)
            return;

        if (!hasReservedAttackToken && !hasActiveAttackToken)
            return;

        hasReservedAttackToken = false;
        hasActiveAttackToken = false;

        if (stageManager == null)
        {
            FindStageManager();
        }

        if (stageManager != null)
        {
            stageManager.ReleaseMinionAttackToken(gameObject, reason);
        }
    }

    public void ChangeState(MinionState newState)
    {
        if (isDead)
            return;

        if (currentHP <= 0f && newState != MinionState.Damaged)
            return;

        if (newState == MinionState.Attack && !TryReserveAttackToken())
        {
            currentState = MinionState.Walk;
            animator.SetBool(IsMovingHash, true);
            cooldownTimer = Mathf.Max(cooldownTimer, tokenDeniedRetryDelay);
            return;
        }

        if (currentState == MinionState.Attack && newState != MinionState.Attack)
        {
            ReleaseAttackToken($"Keluar dari state Attack menuju {newState}");
        }

        currentState = newState;

        switch (newState)
        {
            case MinionState.Idle:
                animator.SetBool(IsMovingHash, false);
                break;

            case MinionState.Walk:
                animator.ResetTrigger(AttackHash);
                animator.ResetTrigger(DamagedHash);
                // IsMoving di-handle oleh HandleWalk (tergantung movementSubState).
                // Default true agar animator transisi ke Walk saat baru masuk state.
                animator.SetBool(IsMovingHash, true);
                break;

            case MinionState.Attack:
                animator.SetBool(IsMovingHash, false);
                animator.ResetTrigger(DamagedHash);
                animator.SetTrigger(AttackHash);

                stateTimer = Mathf.Max(0.01f, attackStateDuration);
                cooldownTimer = attackCooldown + stateTimer;

                // Kunci pergerakan selama menyerang + sisa recovery animasi tembak.
                attackMoveLockTimer = stateTimer + Mathf.Max(0f, attackMovementLockTail);

                // Hentikan sisa kecepatan (mis. dari knockback) agar minion benar-benar diam saat menembak.
                if (rb != null)
                    rb.velocity = Vector2.zero;

                // Damage akan ditangani oleh Animation Event →
                // BowAnimationEventRelay_MinionRange → MinionRange_Projectile → ArrowDamage.
                FacePlayerHorizontally();
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

        ReleaseAttackToken("Minion mati");

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

    /// <summary>
    /// Flip sprite ke arah MENJAUHI player (punggung ke player) sehingga
    /// gerakan mundur kelihatan seperti "lari menjauh", bukan "moonwalk".
    /// Dipanggil saat sub-state Retreat, menggantikan FacePlayerHorizontally.
    /// </summary>
    private void FaceAwayFromPlayer()
    {
        if (player == null)
            return;

        // Arah menjauhi player: kalau player di kanan, kita harus hadap kiri.
        float faceDirection = transform.position.x - player.position.x;

        if (Mathf.Abs(faceDirection) > 0.1f)
        {
            // faceDirection > 0 → player di kiri → kita harus hadap kanan
            // faceDirection < 0 → player di kanan → kita harus hadap kiri
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

    private void OnDisable()
    {
        ReleaseAttackToken("Minion dinonaktifkan atau dihancurkan");
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // desiredRange ring (hijau)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, desiredRange);

        // minimumCombatRange ring (merah)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, minimumCombatRange);

        // verticalTolerance band
        Gizmos.color = Color.yellow;
        Vector3 leftBoundStart = new Vector3(transform.position.x - 2f, minY, transform.position.z);
        Vector3 leftBoundEnd = new Vector3(transform.position.x + 2f, minY, transform.position.z);
        Vector3 rightBoundStart = new Vector3(transform.position.x - 2f, maxY, transform.position.z);
        Vector3 rightBoundEnd = new Vector3(transform.position.x + 2f, maxY, transform.position.z);

        Gizmos.DrawLine(leftBoundStart, leftBoundEnd);
        Gizmos.DrawLine(rightBoundStart, rightBoundEnd);

        // FirePoint gizmo
        if (firePoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(firePoint.position, 0.15f);
        }
    }
#endif
}