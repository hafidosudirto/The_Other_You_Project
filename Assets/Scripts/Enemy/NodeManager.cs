using System.Collections.Generic;
using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    public enum EnemyWeaponMode
    {
        Auto,
        Sword,
        Bow
    }

    [Header("References")]
    public Transform playerTransform;

    public EnemyCombatController Combat { get; private set; }
    public EnemyMovementFSM Movement { get; private set; }
    public EnemyAnimation Animation { get; private set; }

    [Header("Combat Mode")]
    [SerializeField] private EnemyWeaponMode weaponMode = EnemyWeaponMode.Auto;

    [Header("Adaptive")]
    [SerializeField] private EnemyAdaptiveProfile adaptiveProfile;

    [Header("Visual Facing")]
    [SerializeField] private Transform spriteVisual;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private bool invertFlipX = false;
    [SerializeField] private float maxMoveSpeed = 3f;
    private Rigidbody2D rb2d;

    private bool desiredFacingRight = true;
    public bool IsFacingRight => desiredFacingRight;
    private Vector3 prevPos;

    public int ForwardSign => desiredFacingRight ? 1 : -1;
    public Vector2 ForwardDir => new Vector2(ForwardSign, 0f);
    public bool VisualFacingRight => desiredFacingRight;

    [Header("Legacy Melee Range (Sword / Debug)")]
    public float attackRange = 1.8f;
    [SerializeField] private Vector2 attackRangeOffset = new Vector2(1f, 0f);

    [Header("Bow Logic Control")]
    [Tooltip("Matikan (False) agar Bow beradaptasi dengan persentase DDA.")]
    [SerializeField] private bool useBowBalancedBag = false;

    // Variabel kunci untuk DDA: Mengunci skill yang terpilih agar tidak terganti saat cooldown
    private int pendingBowSkillId = -1;

    [Tooltip("Radius maksimal enemy bow mulai merespon musuh.")]
    [SerializeField] private float bowSenseRange = 8.5f;

    [Tooltip("Jarak default bow jika bag sedang kosong atau menggunakan DDA.")]
    [SerializeField] private float bowDesiredRange = 5.25f;

    [Tooltip("Kalau player terlalu dekat dari ini, enemy bow akan mundur.")]
    [SerializeField] private float bowMinimumRange = 2.25f;

    [Header("Sword Range Preset")]
    [SerializeField] private float swordDesiredRange = 1.8f;

    [Header("Action Lock")]
    public bool isPerformingAction = false;

    private WeightedRandomSelector offensiveTree;
    private int cachedProfileVersion = -1;

    // Balanced bag untuk bow
    private readonly List<int> bowSkillBag = new List<int>();
    private int lastBowSkillId = -1;

    public EnemyWeaponMode ActiveWeaponMode => ResolveWeaponMode();

    private void Awake()
    {
        Combat = GetComponent<EnemyCombatController>();
        Movement = GetComponent<EnemyMovementFSM>();
        Animation = GetComponentInChildren<EnemyAnimation>(true);

        if (adaptiveProfile == null)
            adaptiveProfile = GetComponent<EnemyAdaptiveProfile>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

        if (spriteVisual == null && spriteRenderer != null)
            spriteVisual = spriteRenderer.transform;

        rb2d = GetComponent<Rigidbody2D>();
        if (rb2d == null)
            rb2d = GetComponentInChildren<Rigidbody2D>(true);
    }

    private void Start()
    {
        if (playerTransform == null)
            playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (Combat != null)
        {
            Combat.OnSkillStart += OnActionStart;
            Combat.OnSkillEnd += OnActionEnd;
        }

        BuildAttackTree();
        RefreshAdaptiveWeights(true);

        prevPos = transform.position;

        if (playerTransform != null)
        {
            UpdateDesiredFacing();
            ApplyFacing();
        }

        if (ResolveWeaponMode() == EnemyWeaponMode.Bow && useBowBalancedBag)
            RefillBowSkillBag();

        ApplyMovementPresetForCurrentWeapon();
    }

    private void OnDestroy()
    {
        if (Combat != null)
        {
            Combat.OnSkillStart -= OnActionStart;
            Combat.OnSkillEnd -= OnActionEnd;
        }
    }

    public void Update()
    {
        if (playerTransform == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) playerTransform = p.transform;
            else return;
        }

        UpdateDesiredFacing();
        RefreshAdaptiveWeights();
        ApplyMovementPresetForCurrentWeapon();

        if (!isPerformingAction && Movement != null)
        {
            Movement.Tick();
            UpdateLocomotionAnim();
        }

        EvaluateAttackTree();
    }

    private void LateUpdate()
    {
        ApplyFacing();
    }

    private void UpdateLocomotionAnim()
    {
        if (Animation == null) return;

        if (isPerformingAction)
        {
            Animation.SetMoveSpeed(0f);
            return;
        }

        float speed = 0f;
        if (rb2d != null)
        {
            speed = rb2d.velocity.magnitude;
        }
        else
        {
            Vector3 delta = transform.position - prevPos;
            speed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        }

        float speed01 = Mathf.Clamp01(speed / Mathf.Max(0.01f, maxMoveSpeed));
        Animation.SetMoveSpeed(speed01);
        prevPos = transform.position;
    }

    private void UpdateDesiredFacing()
    {
        if (!playerTransform) return;
        float dx = playerTransform.position.x - transform.position.x;
        desiredFacingRight = dx >= 0f;
    }

    private void ApplyFacing()
    {
        if (spriteRenderer == null) return;
        spriteRenderer.flipX = invertFlipX ? desiredFacingRight : !desiredFacingRight;
    }

    public void OnActionStart()
    {
        isPerformingAction = true;
        if (Movement != null) Movement.enabled = false;
    }

    public void OnActionEnd()
    {
        isPerformingAction = false;
        if (Movement != null) Movement.enabled = true;
    }

    public bool IsInAttackRange()
    {
        if (!playerTransform) return false;
        Vector2 attackPos = GetAttackRangeCenter();
        return Vector2.Distance(attackPos, playerTransform.position) <= attackRange;
    }

    private Vector2 GetAttackRangeCenter()
    {
        Vector2 basePos = transform.position;
        Vector2 offset = attackRangeOffset;
        offset.x *= ForwardSign;
        return basePos + offset;
    }

    public float AttackPower
    {
        get
        {
            if (Combat != null && Combat.stats != null) return Combat.stats.attack;
            return 10f;
        }
    }

    private EnemyWeaponMode ResolveWeaponMode()
    {
        if (weaponMode != EnemyWeaponMode.Auto) return weaponMode;
        bool hasBowSkills = Combat != null && (Combat.quickShotBow != null || Combat.fullDrawBow != null || Combat.piercingBow != null || Combat.concussiveBow != null);
        if (hasBowSkills) return EnemyWeaponMode.Bow;

        Transform skillRootBow = FindChildRecursive(transform, "SkillRoot_Bow");
        if (skillRootBow != null) return EnemyWeaponMode.Bow;

        return EnemyWeaponMode.Sword;
    }

    private Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root == null) return null;
        if (root.name == targetName) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), targetName);
            if (found != null) return found;
        }
        return null;
    }

    private float GetPendingBowSkillRange()
    {
        int skillToEvaluate = -1;

        if (useBowBalancedBag && bowSkillBag.Count > 0)
        {
            skillToEvaluate = bowSkillBag[0];
        }
        else if (!useBowBalancedBag && pendingBowSkillId != -1)
        {
            skillToEvaluate = pendingBowSkillId;
        }

        switch (skillToEvaluate)
        {
            case 0: return Combat.quickShotBow != null ? Combat.quickShotBow.skillRange : bowDesiredRange;
            case 1: return Combat.piercingBow != null ? Combat.piercingBow.skillRange : bowDesiredRange;
            case 2: return Combat.fullDrawBow != null ? Combat.fullDrawBow.skillRange : bowDesiredRange;
            case 3: return Combat.concussiveBow != null ? Combat.concussiveBow.skillRange : bowDesiredRange;
        }

        return bowDesiredRange;
    }

    private void ApplyMovementPresetForCurrentWeapon()
    {
        if (Movement == null) return;

        if (ResolveWeaponMode() == EnemyWeaponMode.Bow)
        {
            Movement.SetMovementMode(EnemyMovementFSM.CombatMovementMode.Bow);
            Movement.SetMinimumCombatRange(bowMinimumRange);

            // Pergerakan adaptif berdasarkan jarak ideal skill yang akan dikeluarkan
            Movement.SetDesiredRange(GetPendingBowSkillRange());
        }
        else
        {
            Movement.SetMovementMode(EnemyMovementFSM.CombatMovementMode.Sword);
            Movement.SetDesiredRange(swordDesiredRange);
            Movement.SetMinimumCombatRange(0f);
        }
    }

    private bool IsInsideOffenseSenseRange()
    {
        if (!playerTransform) return false;
        if (ResolveWeaponMode() == EnemyWeaponMode.Bow)
            return Vector2.Distance(transform.position, playerTransform.position) <= bowSenseRange;

        return IsInAttackRange();
    }

    private void BuildAttackTree()
    {
        if (ResolveWeaponMode() == EnemyWeaponMode.Bow)
        {
            offensiveTree = null;
            return;
        }

        var swordNodes = new List<Node> { new SwordSlashComboNode(this), new SwordWhirlwindNode(this), new SwordChargedStrikeNode(this) };
        offensiveTree = new WeightedRandomSelector(swordNodes, new List<float> { 34f, 33f, 33f });
    }

    private void RefreshAdaptiveWeights(bool force = false)
    {
        var dda = DDAController.Instance;
        if (dda == null || adaptiveProfile == null) return;

        if (!force && cachedProfileVersion == dda.ProfileVersion) return;
        cachedProfileVersion = dda.ProfileVersion;

        // Refresh Adaptive Profile secara global (Sword & Bow)
        adaptiveProfile.RefreshFromDDA();

        // Update Tree HANYA untuk Sword 
        if (ResolveWeaponMode() == EnemyWeaponMode.Sword && offensiveTree != null)
        {
            IReadOnlyList<float> allWeights = adaptiveProfile.GetSwordSkillWeights();
            if (allWeights != null && allWeights.Count >= 3)
            {
                float total = Mathf.Max(0f, allWeights[0]) + Mathf.Max(0f, allWeights[1]) + Mathf.Max(0f, allWeights[2]);
                if (total > 0f)
                    offensiveTree.SetWeights(new float[] { (allWeights[0] / total) * 100f, (allWeights[1] / total) * 100f, (allWeights[2] / total) * 100f });
            }
        }
    }

    private void RefillBowSkillBag()
    {
        bowSkillBag.Clear();
        bowSkillBag.Add(0); // Quick
        bowSkillBag.Add(1); // Piercing
        bowSkillBag.Add(2); // Full Draw
        bowSkillBag.Add(3); // Concussive

        for (int i = 0; i < bowSkillBag.Count; i++)
        {
            int j = Random.Range(i, bowSkillBag.Count);
            int temp = bowSkillBag[i];
            bowSkillBag[i] = bowSkillBag[j];
            bowSkillBag[j] = temp;
        }

        if (bowSkillBag.Count > 1 && bowSkillBag[0] == lastBowSkillId)
        {
            int temp = bowSkillBag[0];
            bowSkillBag[0] = bowSkillBag[1];
            bowSkillBag[1] = temp;
        }
    }

    // =========================================================
    // MAPPING ID (DDA: 0=Quick, 1=Piercing, 2=FullDraw)
    // =========================================================
    private bool CanUseBowSkillId(int skillId, float dist)
    {
        switch (skillId)
        {
            case 0: return Combat != null && Combat.quickShotBow != null && Combat.quickShotBow.CanTrigger(dist);
            case 1: return Combat != null && Combat.piercingBow != null && Combat.piercingBow.CanTrigger(dist);
            case 2: return Combat != null && Combat.fullDrawBow != null && Combat.fullDrawBow.CanTrigger(dist);
            case 3: return Combat != null && Combat.concussiveBow != null && Combat.concussiveBow.CanTrigger(dist);
        }
        return false;
    }

    private void TriggerBowSkillId(int skillId)
    {
        switch (skillId)
        {
            case 0: Combat?.quickShotBow?.Trigger(); break;
            case 1: Combat?.piercingBow?.Trigger(); break;
            case 2: Combat?.fullDrawBow?.Trigger(); break;
            case 3: Combat?.concussiveBow?.Trigger(); break;
        }
    }

    // =========================================================
    // DDA WEIGHTED BOW LOGIC (FIXED)
    // =========================================================
    private bool TryExecuteWeightedBowSkill()
    {
        if (Combat == null || playerTransform == null) return false;
        float dist = Vector2.Distance(transform.position, playerTransform.position);

        // 1. Roll bobot DDA jika belum ada skill yang mengantre
        if (pendingBowSkillId == -1)
        {
            IReadOnlyList<float> bowWeights = adaptiveProfile != null ? adaptiveProfile.GetBowSkillWeights() : new float[] { 34f, 33f, 33f };
            float totalWeight = bowWeights[0] + bowWeights[1] + bowWeights[2];

            if (totalWeight <= 0f) return false;

            float roll = Random.Range(0f, totalWeight);

            if (roll <= bowWeights[0]) pendingBowSkillId = 0;
            else if (roll <= bowWeights[0] + bowWeights[1]) pendingBowSkillId = 1;
            else pendingBowSkillId = 2;
        }

        // 2. Alignment vertikal
        if (Movement != null && Movement.useVerticalAlign)
        {
            float absDistY = Mathf.Abs(playerTransform.position.y - transform.position.y);
            if (absDistY > Movement.verticalTolerance)
                return false;
        }

        // 3. Tembak hanya jika skill yang terpilih dari DDA sudah siap (Distance/Cooldown)
        if (CanUseBowSkillId(pendingBowSkillId, dist))
        {
            TriggerBowSkillId(pendingBowSkillId);
            pendingBowSkillId = -1; // Reset setelah eksekusi agar bisa roll DDA lagi
            return true;
        }

        // Jika skill terpilih belum siap (misal lagi cooldown), musuh akan menunggu
        return false;
    }

    // =========================================================
    // STRICT BOW BALANCED BAG LOGIC (25% Mutlak)
    // =========================================================
    private bool TryExecuteBalancedBowSkill()
    {
        if (Combat == null || playerTransform == null) return false;
        if (bowSkillBag.Count == 0) RefillBowSkillBag();

        int nextSkillId = bowSkillBag[0];
        float dist = Vector2.Distance(transform.position, playerTransform.position);

        if (Movement != null && Movement.useVerticalAlign)
        {
            float absDistY = Mathf.Abs(playerTransform.position.y - transform.position.y);
            if (absDistY > Movement.verticalTolerance)
                return false;
        }

        if (CanUseBowSkillId(nextSkillId, dist))
        {
            TriggerBowSkillId(nextSkillId);
            lastBowSkillId = nextSkillId;
            bowSkillBag.RemoveAt(0);
            return true;
        }

        return false;
    }

    private bool ShouldEnterReactiveOnlyMode()
    {
        var dda = DDAController.Instance;
        if (dda == null) return false;
        return dda.currentPlayerPlaystyle == PlayerPlaystyle.DefensiveDominant && dda.GetCurrentDefenseRiposteWeight() >= 99f;
    }

    private void EvaluateAttackTree()
    {
        if (ShouldEnterReactiveOnlyMode() || isPerformingAction || playerTransform == null || !IsInsideOffenseSenseRange())
            return;

        if (ResolveWeaponMode() == EnemyWeaponMode.Bow)
        {
            if (useBowBalancedBag) TryExecuteBalancedBowSkill();
            else TryExecuteWeightedBowSkill();
            return;
        }

        offensiveTree?.Evaluate();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector2 attackCenter = GetAttackRangeCenter();
        Gizmos.DrawWireSphere(attackCenter, attackRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, attackCenter);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, bowSenseRange);

        Gizmos.color = Color.cyan;
        Vector3 arrowEnd = (Vector2)transform.position + ForwardDir * 0.5f;
        Gizmos.DrawLine(transform.position, arrowEnd);
        Gizmos.DrawSphere(arrowEnd, 0.1f);
    }
}