using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Controller AI minion jarak jauh (Bow).
///
/// Perilaku 3-zona sesuai prototype HTML (RANGE_PAWN):
///   dist > rangeMax  → maju ke slot orbit spread formation (hordeRadius di sekitar player)
///   dist < rangeMin  → mundur dari player
///   rangeMin–rangeMax → sejajarkan Y dulu, baru tembak saat Y sejajar + token tersedia
///
/// Token serangan menggunakan StageManager.TryAcquireMinionAttackToken / ReleaseMinionAttackToken,
/// sama persis seperti MinionMeleeController (token dikembalikan setelah tokenReturnDuration detik).
/// </summary>
[RequireComponent(typeof(CharacterBase))]
public class MinionRangedCombatController : MonoBehaviour
{
    // ============================================================
    // INSPECTOR FIELDS
    // ============================================================

    [Header("Core References")]
    [SerializeField] private CharacterBase character;
    [SerializeField] private Transform player;
    [SerializeField] private Animator animator;

    [Header("Skill Root")]
    [SerializeField] private Transform skillRootBow;

    [Header("Single Ranged Skill")]
    [SerializeField] private Enemy_Bow_QuickShot quickShot;

    [Header("Projectile Assignment")]
    [Tooltip("Prefab projectile/arrow untuk Minion Bow. Akan dikirim otomatis ke Enemy_Bow_QuickShot.")]
    [SerializeField] private GameObject projectilePrefab;

    [Tooltip("Jika aktif, mencoba ambil projectile dari Enemy_Bow_QuickShot bila field di atas masih kosong.")]
    [SerializeField] private bool pullProjectileFromQuickShotIfEmpty = true;

    [Header("Movement / Range Zones (Prototype HTML Parity)")]
    [SerializeField] private bool allowMovement = true;

    [Tooltip("Kecepatan gerak default. Jika CharacterBase.moveSpeed > 0, nilai itu yang dipakai.")]
    [SerializeField] private float moveSpeed = 2f;

    [Tooltip("Minion mundur jika lebih dekat dari ini ke player. (Prototype: dist < 200px ≈ 3.4u)")]
    [SerializeField] private float rangeMin = 3.5f;

    [Tooltip("Minion maju ke slot orbit jika lebih jauh dari ini. (Prototype: dist > 350px ≈ 6u)")]
    [SerializeField] private float rangeMax = 6f;

    [Tooltip("Radius slot orbit saat di luar rangeMax — jarak formasi di sekeliling player. (Prototype: 160px ≈ 2.75u)")]
    [SerializeField] private float hordeRadius = 2.75f;

    [Header("Y Alignment Gate (Prototype HTML Parity)")]
    [Tooltip("Selisih Y maksimum agar minion dianggap sudah sejajar dan boleh menembak.\n" +
             "Selama belum sejajar, minion hanya gerak Y menuju jalur pribadi.")]
    [SerializeField] private float yAlignTolerance = 0.4f;

    [Tooltip("Sebaran 'lane' Y pribadi masing-masing minion agar tidak semua menembak dari baris Y yang sama.\n" +
             "0 = semua sejajar, 1 = sebaran penuh dalam yAlignTolerance.")]
    [Range(0f, 1f)]
    [SerializeField] private float attackLaneSpread = 0.6f;

    [Header("Stage Bounds")]
    [Tooltip("Batas Y terendah. Posisi minion diclamp tiap frame.")]
    [SerializeField] private float minY = -4f;

    [Tooltip("Batas Y tertinggi.")]
    [SerializeField] private float maxY = 1.6f;

    [Header("Anti-Stacking / Separation")]
    [Tooltip("Radius cek tetangga sesama musuh untuk gaya tolak.")]
    [SerializeField] private float separationRadius = 1.5f;

    [Tooltip("Kekuatan gaya tolak dari tetangga.")]
    [SerializeField] private float separationWeight = 2f;

    [Tooltip("Layer musuh untuk cek separation. Set ke layer Enemy.")]
    [SerializeField] private LayerMask enemyLayer;

    [Header("Attack Settings")]
    [Tooltip("Jeda antar tembakan (dalam detik).")]
    [SerializeField] private float attackCooldown = 2.5f;

    [Header("Stage Token (DDA)")]
    [Tooltip("Batas jumlah minion bow yang boleh menembak bersamaan. Nilai ini dikirim oleh StageManager.")]
    public int attackTokens = 2;

    [Tooltip("True jika ini unit boss; token diabaikan dan serangan selalu diizinkan.")]
    public bool isBoss = false;

    [Tooltip("Jika aktif, minion boleh menembak walau StageManager tidak ditemukan.")]
    [SerializeField] private bool allowAttackWithoutStageManager = true;

    [Tooltip("Jeda singkat sebelum mencoba minta token lagi setelah ditolak.")]
    [SerializeField] private float tokenDeniedRetryDelay = 0.3f;

    [Tooltip("Durasi sebelum token dikembalikan ke pool setelah menembak. (Prototype: 2s)")]
    [SerializeField] private float tokenReturnDuration = 2f;

    [Header("Stage Token Runtime Debug (read-only)")]
    public bool hasActiveAttackToken = false;
    public int activeAttackersInStage = 0;
    public int attackTokenCapacityInStage = 0;

    [Header("Animation Parameters")]
    [SerializeField] private string moveSpeedFloat = "MoveSpeed";
    [SerializeField] private string quickShotBool  = "QuickShot";

    [Header("Animation Timing")]
    [Tooltip("Durasi animasi QuickShot. Bool quickShotBool dimatikan setelah durasi ini.")]
    [SerializeField] private float quickShotAnimDuration = 0.45f;

    [Header("Busy Lock")]
    [Tooltip("Fallback: berapa lama minion dianggap busy setelah tembak (mencegah spam tembak).")]
    [SerializeField] private float fallbackBusyDuration = 0.45f;

    [Header("Retreat Tuning")]
    [Tooltip("Faktor kecepatan saat retreat (0–1). 1 = kecepatan penuh, 0.6 = lebih lambat / kurang panik.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float retreatSpeedMultiplier = 0.7f;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    // ============================================================
    // RUNTIME STATE
    // ============================================================

    private float lastAttackTime   = -999f;
    private float lockedUntil      = -999f;
    private int   skillBusyCounter = 0;

    private bool hasReservedAttackToken = false;

    private float assignedOrbitAngle;
    private float laneOffsetFactor;

    private StageManager stageManager;

    public bool IsBusy    => skillBusyCounter > 0 || Time.time < lockedUntil;
    public bool IsNotBusy => !IsBusy;

    private bool IsAttackReady => !IsBusy && Time.time >= lastAttackTime + attackCooldown;

    // ============================================================
    // REFLECTION MEMBER NAME LIST (PROJECTILE ASSIGNMENT)
    // ============================================================

    private static readonly string[] ProjectileMemberNames =
    {
        "projectilePrefab", "projectile", "arrowPrefab", "arrowProjectile",
        "arrowProjectilePrefab", "projectileObject", "projectileGameObject",
        "prefabProjectile", "quickShotProjectile", "quickShotProjectilePrefab"
    };

    // ============================================================
    // LIFECYCLE
    // ============================================================

    private void Awake()
    {
        assignedOrbitAngle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
        laneOffsetFactor   = UnityEngine.Random.Range(-1f, 1f);

        FindStageManager();
        AutoAssignReferences();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            AutoAssignReferences();
    }
#endif

    private void Update()
    {
        if (player == null)
        {
            AutoAssignPlayer();
            SetMoveAnimation(false);
            return;
        }

        if (character == null || !character.CanAct())
        {
            SetMoveAnimation(false);
            return;
        }

        float dist = Vector2.Distance(transform.position, player.position);

        bool isMoving = false;

        if (allowMovement && !IsBusy)
            isMoving = HandleMovement(dist);
        else
            FacePlayer();  // diam / busy → selalu hadap player (e.g. saat animasi tembak)

        if (!IsBusy)
            TryShoot(dist);

        SetMoveAnimation(isMoving);

        ClampPositionToStageY();
    }

    private void OnDisable()
    {
        ReleaseAttackToken("Minion Ranged dinonaktifkan atau dihancurkan");
        CancelInvoke();
    }

    // ============================================================
    // MOVEMENT — 3 ZONA (PROTOTYPE HTML PARITY)
    // ============================================================

    private bool HandleMovement(float dist)
    {
        float speed = character != null && character.moveSpeed > 0f ? character.moveSpeed : moveSpeed;
        Vector3 before = transform.position;

        Vector2 moveDir    = Vector2.zero;
        bool isRetreating  = false;

        if (dist > rangeMax)
        {
            // Zona 1 — Terlalu jauh: maju ke slot orbit (spread formation di sekeliling player)
            Vector2 slotPos = (Vector2)player.position + new Vector2(
                Mathf.Cos(assignedOrbitAngle),
                Mathf.Sin(assignedOrbitAngle)
            ) * hordeRadius;
            slotPos.y = Mathf.Clamp(slotPos.y, minY, maxY);

            moveDir = ((Vector3)slotPos - transform.position).normalized;
            FacePlayer();
        }
        else if (dist < rangeMin)
        {
            // Zona 2 — Terlalu dekat: retreat X-only (tidak diagonal), hadap arah gerak
            // X-only agar tidak lari diagonal naik/turun seperti di canvas prototype yang top-down.
            // Ini lebih natural untuk side-scroller dan membuat retreat tidak terlalu agresif.
            float xAway = transform.position.x - player.position.x;
            float xDir  = Mathf.Abs(xAway) > 0.01f
                ? Mathf.Sign(xAway)
                : (character != null && !character.isFacingRight ? 1f : -1f);

            moveDir      = new Vector2(xDir, 0f);
            isRetreating = true;

            // Hadap arah gerak (menjauh dari player) — fix moonwalk
            FaceDirection(xDir);
        }
        else
        {
            // Zona 3 — Ideal range: gerak Y menuju lane pribadi, selalu hadap player
            float laneTargetY = player.position.y + laneOffsetFactor * yAlignTolerance * attackLaneSpread;
            float yDiff       = laneTargetY - transform.position.y;

            if (Mathf.Abs(yDiff) > yAlignTolerance * 0.5f)
                moveDir = new Vector2(0f, Mathf.Sign(yDiff));

            FacePlayer();
        }

        if (isRetreating)
        {
            // Retreat: gerak X-only tanpa separation force agar tidak diperparah oleh minion lain
            if (moveDir.sqrMagnitude > 0.01f)
                transform.position += (Vector3)(moveDir * speed * retreatSpeedMultiplier * Time.deltaTime);
        }
        else
        {
            // Gerak normal dengan separation force (anti-stacking)
            Vector2 separation = GetSeparationForce();
            Vector2 finalDir   = (moveDir + separation * separationWeight).normalized;

            bool hasMoveIntent = moveDir.sqrMagnitude > 0.01f || separation.sqrMagnitude > 0.01f;

            if (hasMoveIntent)
                transform.position += (Vector3)(finalDir * speed * Time.deltaTime);
        }

        return Vector3.Distance(before, transform.position) > 0.001f;
    }

    private Vector2 GetSeparationForce()
    {
        Vector2 force = Vector2.zero;
        Collider2D[] allies = Physics2D.OverlapCircleAll(transform.position, separationRadius, enemyLayer);

        foreach (Collider2D ally in allies)
        {
            if (ally.gameObject == gameObject)
                continue;

            Vector2 awayDir = (Vector2)transform.position - (Vector2)ally.transform.position;

            if (awayDir.sqrMagnitude > 0f)
                force += awayDir.normalized / awayDir.magnitude;
        }

        return force;
    }

    // ============================================================
    // SHOOT LOGIC — Y-ALIGN GATE + TOKEN GATE (PROTOTYPE PARITY)
    // ============================================================

    private void TryShoot(float dist)
    {
        if (!IsAttackReady)
            return;

        // Hanya tembak dalam zona ideal (bukan terlalu dekat / terlalu jauh)
        if (dist < rangeMin || dist > rangeMax)
            return;

        // Gate 1 — Y harus sejajar dulu (prototype: tembak baru boleh setelah Y aligned)
        float laneTargetY = player.position.y + laneOffsetFactor * yAlignTolerance * attackLaneSpread;
        if (Mathf.Abs(laneTargetY - transform.position.y) > yAlignTolerance)
            return;

        // Gate 2 — Minta token ke StageManager (seperti MinionMeleeController)
        if (!TryReserveAttackToken())
        {
            lockedUntil = Mathf.Max(lockedUntil, Time.time + tokenDeniedRetryDelay);
            return;
        }

        ExecuteShot();
    }

    private void ExecuteShot()
    {
        if (quickShot == null)
        {
            AutoAssignQuickShot();

            if (quickShot == null)
            {
                Debug.LogWarning($"[MINION RANGED] {name} tidak menemukan Enemy_Bow_QuickShot.", this);
                ReleaseAttackToken("QuickShot null saat ExecuteShot");
                return;
            }
        }

        PrepareQuickShotBeforeTrigger();
        PlayQuickShotAnimation();

        bool triggered = InvokeQuickShot();

        if (!triggered)
        {
            Debug.LogWarning($"[MINION RANGED] {name} gagal memanggil QuickShot. Cek nama method pada Enemy_Bow_QuickShot.", this);
            ResetQuickShotAnimation();
            ReleaseAttackToken("InvokeQuickShot gagal");
            return;
        }

        lastAttackTime = Time.time;
        lockedUntil    = Time.time + fallbackBusyDuration;

        // Kembalikan token setelah tokenReturnDuration (prototype: 2s)
        CancelInvoke(nameof(ReleaseAttackTokenDelayed));
        Invoke(nameof(ReleaseAttackTokenDelayed), tokenReturnDuration);

        if (showDebug)
            Debug.Log($"[MINION RANGED] {name} menembak. Token kembali dalam {tokenReturnDuration}s.", this);
    }

    private void ReleaseAttackTokenDelayed()
    {
        ReleaseAttackToken("tokenReturnDuration habis");
    }

    // ============================================================
    // STAGE MANAGER TOKEN SYSTEM (SAMA PERSIS SEPERTI MELEE)
    // ============================================================

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
            hasActiveAttackToken   = true;
            return true;
        }

        bool acquired = stageManager.TryAcquireMinionAttackToken(
            gameObject,
            "MinionRangedCombatController.TryShoot"
        );

        if (acquired)
        {
            hasReservedAttackToken = true;
            hasActiveAttackToken   = true;
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
        hasActiveAttackToken   = false;

        FindStageManager();

        if (stageManager != null)
            stageManager.ReleaseMinionAttackToken(gameObject, reason);
    }

    private void FindStageManager()
    {
        if (stageManager == null)
            stageManager = FindObjectOfType<StageManager>();
    }

    // ============================================================
    // INITIALIZE — SIGNATURE MATCHING PRODUCTION (CharacterBase, int, bool)
    // ============================================================

    public void InitializeStageEnemy(CharacterBase stageCharacter, int stageAttackTokens, bool bossStatus = false)
    {
        if (stageCharacter != null)
            character = stageCharacter;

        attackTokens             = Mathf.Max(0, stageAttackTokens);
        attackTokenCapacityInStage = Mathf.Max(0, stageAttackTokens);
        activeAttackersInStage   = 0;
        hasActiveAttackToken     = false;
        hasReservedAttackToken   = false;
        isBoss                   = bossStatus;

        FindStageManager();

        if (showDebug)
            Debug.Log(
                $"[MINION RANGED INIT] {name} " +
                $"token: {attackTokens}, " +
                $"attack: {(character != null ? character.attack : 0f)}, " +
                $"isBoss: {isBoss}",
                this
            );
    }

    // Compatibility methods agar StageManager lama tetap bisa update token info via reflection.
    public void SetStageAttackTokenRuntime(bool isUsingToken, int activeTokens, int tokenCapacity)
    {
        hasActiveAttackToken     = isUsingToken;
        hasReservedAttackToken   = isUsingToken;
        activeAttackersInStage   = Mathf.Max(0, activeTokens);
        attackTokenCapacityInStage = Mathf.Max(0, tokenCapacity);
        attackTokens             = attackTokenCapacityInStage;
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

    // ============================================================
    // HELPERS — FACING, ANIMATION, BOUNDS
    // ============================================================

    private void FacePlayer()
    {
        if (player == null || character == null)
            return;

        bool playerOnRight = player.position.x > transform.position.x;

        if (playerOnRight != character.isFacingRight)
            character.Flip();
    }

    // Dipakai saat retreat agar sprite menghadap arah gerak, bukan moonwalk ke belakang.
    private void FaceDirection(float xDir)
    {
        if (character == null)
            return;

        bool shouldFaceRight = xDir > 0f;

        if (shouldFaceRight != character.isFacingRight)
            character.Flip();
    }

    private void ClampPositionToStageY()
    {
        Vector3 pos = transform.position;
        pos.y = Mathf.Clamp(pos.y, minY, maxY);
        transform.position = pos;
    }

    private void SetMoveAnimation(bool isMoving)
    {
        if (animator == null)
            return;

        if (!string.IsNullOrEmpty(moveSpeedFloat))
            animator.SetFloat(moveSpeedFloat, isMoving ? 1f : 0f);
    }

    private void PlayQuickShotAnimation()
    {
        SetMoveAnimation(false);

        if (animator == null)
            return;

        ResetQuickShotAnimation();

        if (!string.IsNullOrEmpty(quickShotBool))
            animator.SetBool(quickShotBool, true);

        CancelInvoke(nameof(ResetQuickShotAnimation));
        Invoke(nameof(ResetQuickShotAnimation), quickShotAnimDuration);
    }

    private void ResetQuickShotAnimation()
    {
        if (animator == null)
            return;

        if (!string.IsNullOrEmpty(quickShotBool))
            animator.SetBool(quickShotBool, false);
    }

    // ============================================================
    // AUTO ASSIGN REFERENCES
    // ============================================================

    [ContextMenu("Auto Assign References")]
    public void AutoAssignReferences()
    {
        if (character == null)
            character = GetComponent<CharacterBase>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        AutoAssignPlayer();
        AutoAssignQuickShot();

        if (pullProjectileFromQuickShotIfEmpty && projectilePrefab == null)
            TryPullProjectileFromQuickShot(false);

        AssignProjectileToQuickShot(false);
    }

    private void AutoAssignPlayer()
    {
        if (player != null)
            return;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");

        if (playerObj != null)
            player = playerObj.transform;
    }

    private void AutoAssignQuickShot()
    {
        if (quickShot != null)
            return;

        Transform root = skillRootBow ?? FindChildRecursive(transform, "SkillRoot_Bow");

        if (root != null)
        {
            skillRootBow = root;
            quickShot    = root.GetComponentInChildren<Enemy_Bow_QuickShot>(true);
        }

        if (quickShot == null)
            quickShot = GetComponentInChildren<Enemy_Bow_QuickShot>(true);
    }

    [ContextMenu("Assign Projectile To QuickShot")]
    public void AssignProjectileToQuickShotFromInspector()
    {
        AutoAssignQuickShot();

        if (pullProjectileFromQuickShotIfEmpty && projectilePrefab == null)
            TryPullProjectileFromQuickShot(true);

        AssignProjectileToQuickShot(true);
    }

    // ============================================================
    // SHOT PREP
    // ============================================================

    private void PrepareQuickShotBeforeTrigger()
    {
        if (quickShot == null || character == null)
            return;

        AssignProjectileToQuickShot(false);
        TryInvokeBoolOrVoid(quickShot, "SetPlayer",      player);
        TryInvokeBoolOrVoid(quickShot, "SetTarget",      player);
        TryInvokeBoolOrVoid(quickShot, "SetOwner",       character);
        TryInvokeBoolOrVoid(quickShot, "SetOwner",       character.gameObject);
        TryInvokeBoolOrVoid(quickShot, "SetDamage",      character.attack);
        TryInvokeBoolOrVoid(quickShot, "SetAttackPower", character.attack);
    }

    private bool InvokeQuickShot()
    {
        if (quickShot == null)
            return false;

        // ForceShoot: bypass NodeManager/range check internal Enemy_Bow_QuickShot.
        // Minion sudah memeriksa jarak sendiri sebelum sampai sini.
        if (TryInvokeBoolOrVoid(quickShot, "ForceShoot"))           return true;
        if (TryInvokeBoolOrVoid(quickShot, "Trigger"))              return true;
        if (TryInvokeBoolOrVoid(quickShot, "TryStartQuickShot"))    return true;
        if (TryInvokeBoolOrVoid(quickShot, "TryStartSkill"))        return true;
        if (TryInvokeBoolOrVoid(quickShot, "StartSkill"))           return true;
        if (TryInvokeBoolOrVoid(quickShot, "Shoot"))                return true;

        return false;
    }

    // ============================================================
    // PROJECTILE ASSIGNMENT (REFLECTION)
    // ============================================================

    private bool AssignProjectileToQuickShot(bool logResult)
    {
        if (quickShot == null)
            AutoAssignQuickShot();

        if (quickShot == null)
        {
            if (logResult)
                Debug.LogWarning($"[MINION RANGED] {name} gagal assign projectile: Enemy_Bow_QuickShot tidak ditemukan.", this);
            return false;
        }

        if (projectilePrefab == null)
        {
            if (logResult)
                Debug.LogWarning($"[MINION RANGED] {name} Projectile Prefab belum diisi.", this);
            return false;
        }

        bool assigned = false;
        assigned |= TryInvokeBoolOrVoid(quickShot, "SetProjectilePrefab",  projectilePrefab);
        assigned |= TryInvokeBoolOrVoid(quickShot, "SetProjectile",        projectilePrefab);
        assigned |= TryInvokeBoolOrVoid(quickShot, "SetArrowPrefab",       projectilePrefab);
        assigned |= TryInvokeBoolOrVoid(quickShot, "SetArrowProjectile",   projectilePrefab);
        assigned |= TryInvokeBoolOrVoid(quickShot, "AssignProjectilePrefab", projectilePrefab);
        assigned |= TryInvokeBoolOrVoid(quickShot, "AssignProjectile",     projectilePrefab);
        assigned |= TrySetProjectileMemberByName(quickShot, projectilePrefab);

        if (!assigned)
            Debug.LogWarning(
                $"[MINION RANGED] {name} gagal assign projectile. " +
                $"Tambahkan SetProjectilePrefab(GameObject) atau field projectilePrefab pada Enemy_Bow_QuickShot.",
                this
            );
        else if (logResult)
            Debug.Log($"[MINION RANGED] {name} berhasil assign projectile '{projectilePrefab.name}'.", this);

        return assigned;
    }

    private bool TryPullProjectileFromQuickShot(bool logResult)
    {
        if (quickShot == null)
            AutoAssignQuickShot();

        if (quickShot == null)
            return false;

        Type         type  = quickShot.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (string memberName in ProjectileMemberNames)
        {
            FieldInfo field = type.GetField(memberName, flags);
            if (field != null)
            {
                GameObject pulled = ExtractGameObject(field.GetValue(quickShot));
                if (pulled != null)
                {
                    projectilePrefab = pulled;
                    if (logResult)
                        Debug.Log($"[MINION RANGED] Projectile dari field '{memberName}': {pulled.name}.", this);
                    return true;
                }
            }

            PropertyInfo property = type.GetProperty(memberName, flags);
            if (property != null && property.CanRead)
            {
                GameObject pulled = ExtractGameObject(property.GetValue(quickShot, null));
                if (pulled != null)
                {
                    projectilePrefab = pulled;
                    if (logResult)
                        Debug.Log($"[MINION RANGED] Projectile dari property '{memberName}': {pulled.name}.", this);
                    return true;
                }
            }
        }

        return false;
    }

    private bool TrySetProjectileMemberByName(MonoBehaviour target, GameObject projectile)
    {
        if (target == null || projectile == null)
            return false;

        bool         assigned = false;
        Type         type     = target.GetType();
        BindingFlags flags    = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (string memberName in ProjectileMemberNames)
        {
            FieldInfo field = type.GetField(memberName, flags);
            if (field != null && !field.IsInitOnly)
            {
                object value = ConvertProjectileValue(field.FieldType, projectile);
                if (value != null)
                {
                    field.SetValue(target, value);
                    assigned = true;
                }
            }

            PropertyInfo property = type.GetProperty(memberName, flags);
            if (property != null && property.CanWrite)
            {
                object value = ConvertProjectileValue(property.PropertyType, projectile);
                if (value != null)
                {
                    property.SetValue(target, value, null);
                    assigned = true;
                }
            }
        }

        return assigned;
    }

    private GameObject ExtractGameObject(object value)
    {
        if (value is GameObject go)   return go;
        if (value is Component comp)  return comp.gameObject;
        return null;
    }

    private object ConvertProjectileValue(Type targetType, GameObject projectile)
    {
        if (targetType.IsAssignableFrom(typeof(GameObject)))    return projectile;
        if (targetType == typeof(Transform))                    return projectile.transform;
        if (typeof(Component).IsAssignableFrom(targetType))     return projectile.GetComponent(targetType);
        return null;
    }

    // ============================================================
    // SKILL INVOCATION HELPERS (REFLECTION)
    // ============================================================

    private bool CanTriggerSkill(MonoBehaviour skill, float distance)
    {
        if (skill == null)
            return false;

        MethodInfo m = skill.GetType().GetMethod(
            "CanTrigger",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, new Type[] { typeof(float) }, null
        );
        if (m != null && m.ReturnType == typeof(bool))
            return (bool)m.Invoke(skill, new object[] { distance });

        m = skill.GetType().GetMethod(
            "CanTrigger",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, Type.EmptyTypes, null
        );
        if (m != null && m.ReturnType == typeof(bool))
            return (bool)m.Invoke(skill, null);

        return true;
    }

    private bool TryInvokeBoolOrVoid(MonoBehaviour target, string methodName, params object[] args)
    {
        if (target == null)
            return false;

        MethodInfo method = FindMethod(target.GetType(), methodName, args);

        if (method == null)
            return false;

        object[] converted = ConvertArguments(method, args);
        object   result    = method.Invoke(target, converted);

        return method.ReturnType == typeof(bool) ? (bool)result : true;
    }

    private MethodInfo FindMethod(Type type, string methodName, object[] args)
    {
        MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        foreach (MethodInfo method in methods)
        {
            if (method.Name != methodName)
                continue;

            ParameterInfo[] parameters = method.GetParameters();

            if (parameters.Length != args.Length)
                continue;

            bool match = true;

            for (int i = 0; i < parameters.Length; i++)
            {
                if (args[i] == null)
                    continue;

                Type pt = parameters[i].ParameterType;
                Type at = args[i].GetType();

                if (!pt.IsAssignableFrom(at) && !(IsNumericType(pt) && IsNumericType(at)))
                {
                    match = false;
                    break;
                }
            }

            if (match)
                return method;
        }

        return null;
    }

    private object[] ConvertArguments(MethodInfo method, object[] args)
    {
        ParameterInfo[] parameters = method.GetParameters();
        object[]        converted  = new object[args.Length];

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == null) { converted[i] = null; continue; }

            Type pt = parameters[i].ParameterType;
            Type at = args[i].GetType();

            if (pt.IsAssignableFrom(at))                           { converted[i] = args[i]; continue; }
            if (IsNumericType(pt) && IsNumericType(at))            { converted[i] = Convert.ChangeType(args[i], pt); continue; }

            converted[i] = args[i];
        }

        return converted;
    }

    private bool IsNumericType(Type type) =>
        type == typeof(byte)    || type == typeof(sbyte)   || type == typeof(short)   ||
        type == typeof(ushort)  || type == typeof(int)     || type == typeof(uint)     ||
        type == typeof(long)    || type == typeof(ulong)   || type == typeof(float)    ||
        type == typeof(double)  || type == typeof(decimal);

    private Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root == null)             return null;
        if (root.name == targetName)  return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), targetName);
            if (found != null) return found;
        }

        return null;
    }

    // ============================================================
    // PUBLIC API — SKILL BUSY COUNTER (EXTERNAL CALLERS)
    // ============================================================

    public void InvokeSkillStart()
    {
        skillBusyCounter++;
    }

    public void InvokeSkillEnd()
    {
        if (skillBusyCounter > 0)
            skillBusyCounter--;
    }

    // ============================================================
    // GIZMOS
    // ============================================================

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Zona range (cyan = rangeMin, yellow = rangeMax)
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, rangeMin);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, rangeMax);

        // Slot orbit target
        if (player != null)
        {
            Gizmos.color = Color.green;
            Vector2 slotPos = (Vector2)player.position + new Vector2(
                Mathf.Cos(assignedOrbitAngle),
                Mathf.Sin(assignedOrbitAngle)
            ) * hordeRadius;
            Gizmos.DrawLine(transform.position, slotPos);
            Gizmos.DrawWireSphere(slotPos, 0.12f);
        }

        // Stage bounds
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(
            new Vector3(transform.position.x - 2f, minY, 0f),
            new Vector3(transform.position.x + 2f, minY, 0f)
        );
        Gizmos.DrawLine(
            new Vector3(transform.position.x - 2f, maxY, 0f),
            new Vector3(transform.position.x + 2f, maxY, 0f)
        );
    }
#endif
}
