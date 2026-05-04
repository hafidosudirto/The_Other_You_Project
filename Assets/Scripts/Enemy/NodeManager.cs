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

    // [PERBAIKAN RANGE]: Nilai default dinaikkan drastis agar tidak ada lagi area deadzone
    [Tooltip("Radius maksimal enemy bow mulai merespons pemain.")]
    [SerializeField] private float bowSenseRange = 15f;

    [Tooltip("Jarak default bow jika bag sedang kosong atau menggunakan DDA.")]
    [SerializeField] private float bowDesiredRange = 10f;

    [Tooltip("Kalau player terlalu dekat dari ini, enemy bow akan mundur.")]
    [SerializeField] private float bowMinimumRange = 5f;

    [Header("Sword Range Preset")]
    [SerializeField] private float swordDesiredRange = 1.8f;

    [Tooltip("Radius deteksi ofensif sword.")]
    [SerializeField] private float swordSenseRange = 3.2f;

    [Header("Action Lock")]
    public bool isPerformingAction = false;

    private WeightedRandomSelector offensiveTree;
    private int cachedProfileVersion = -1;

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

        if (isPerformingAction) return;

        float velocityX = rb2d != null ? rb2d.velocity.x : 0f;

        if (Mathf.Abs(velocityX) > 0.1f)
        {
            desiredFacingRight = velocityX > 0f;
        }
        else
        {
            float dx = playerTransform.position.x - transform.position.x;
            desiredFacingRight = dx >= 0f;
        }
    }

    private void ApplyFacing()
    {
        if (spriteRenderer == null) return;
        spriteRenderer.flipX = invertFlipX ? desiredFacingRight : !desiredFacingRight;
    }

    public void OnActionStart()
    {
        isPerformingAction = true;

        if (playerTransform != null)
        {
            float dx = playerTransform.position.x - transform.position.x;
            desiredFacingRight = dx >= 0f;
            ApplyFacing();
        }

        if (Movement != null)
            Movement.enabled = false;
    }

    public void OnActionEnd()
    {
        isPerformingAction = false;

        if (Movement != null)
            Movement.enabled = true;
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
            if (Combat != null && Combat.stats != null)
                return Combat.stats.attack;

            return 10f;
        }
    }

    private EnemyWeaponMode ResolveWeaponMode()
    {
        if (weaponMode != EnemyWeaponMode.Auto)
            return weaponMode;

        bool hasBowSkills =
            Combat != null &&
            (
                Combat.quickShotBow != null ||
                Combat.fullDrawBow != null ||
                Combat.piercingBow != null ||
                Combat.concussiveBow != null
            );

        if (hasBowSkills)
            return EnemyWeaponMode.Bow;

        Transform skillRootBow = FindChildRecursive(transform, "SkillRoot_Bow");
        if (skillRootBow != null)
            return EnemyWeaponMode.Bow;

        return EnemyWeaponMode.Sword;
    }

    private Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root == null) return null;

        if (root.name == targetName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), targetName);

            if (found != null)
                return found;
        }

        return null;
    }

    private void ApplyMovementPresetForCurrentWeapon()
    {
        if (Movement == null) return;

        if (ResolveWeaponMode() == EnemyWeaponMode.Bow)
        {
            Movement.SetMovementMode(EnemyMovementFSM.CombatMovementMode.Bow);

            // [PERBAIKAN RANGE DINAMIS]: 
            // Ambil batas jarak terjauh dan terdekat secara otomatis dari skill yang sedang dipakai
            // Ini akan mencegah FSM terjebak di area deadlock
            float dynamicDesiredRange = bowDesiredRange;
            float dynamicMinRange = bowMinimumRange;

            if (Combat != null)
            {
                if (Combat.quickShotBow != null)
                {
                    dynamicDesiredRange = Mathf.Max(dynamicDesiredRange, Combat.quickShotBow.skillRange);
                    dynamicMinRange = Mathf.Max(dynamicMinRange, Combat.quickShotBow.minRange);
                }
                if (Combat.piercingBow != null)
                {
                    dynamicDesiredRange = Mathf.Max(dynamicDesiredRange, Combat.piercingBow.skillRange);
                    dynamicMinRange = Mathf.Max(dynamicMinRange, Combat.piercingBow.minRange);
                }
                if (Combat.fullDrawBow != null)
                {
                    dynamicDesiredRange = Mathf.Max(dynamicDesiredRange, Combat.fullDrawBow.skillRange);
                    dynamicMinRange = Mathf.Max(dynamicMinRange, Combat.fullDrawBow.minRange);
                }
            }

            Movement.SetDesiredRange(dynamicDesiredRange);
            Movement.SetMinimumCombatRange(dynamicMinRange);
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

        float distToPlayer = Vector2.Distance(transform.position, playerTransform.position);

        if (ResolveWeaponMode() == EnemyWeaponMode.Bow)
            return distToPlayer <= bowSenseRange;

        return distToPlayer <= swordSenseRange;
    }

    private void BuildAttackTree()
    {
        if (ResolveWeaponMode() == EnemyWeaponMode.Bow)
        {
            var bowNodes = new List<Node>
            {
                new BowQuickShotNode(this),
                new BowPiercingShotNode(this),
                new BowFullDrawNode(this)
            };

            offensiveTree = new WeightedRandomSelector(
                bowNodes,
                new List<float> { 34f, 33f, 33f }
            );
            return;
        }

        var swordNodes = new List<Node>
        {
            new SwordSlashComboNode(this),
            new SwordWhirlwindNode(this),
            new SwordChargedStrikeNode(this)
        };

        offensiveTree = new WeightedRandomSelector(
            swordNodes,
            new List<float> { 34f, 33f, 33f }
        );
    }

    private void RefreshAdaptiveWeights(bool force = false)
    {
        var dda = DDAController.Instance;

        if (dda == null || adaptiveProfile == null || offensiveTree == null)
            return;

        if (!force && cachedProfileVersion == dda.ProfileVersion)
            return;

        cachedProfileVersion = dda.ProfileVersion;
        adaptiveProfile.RefreshFromDDA();

        if (ResolveWeaponMode() == EnemyWeaponMode.Sword)
        {
            IReadOnlyList<float> w = adaptiveProfile.GetSwordSkillWeights();
            if (w != null && w.Count >= 3)
            {
                float total = Mathf.Max(0f, w[0]) + Mathf.Max(0f, w[1]) + Mathf.Max(0f, w[2]);
                if (total > 0f)
                {
                    offensiveTree.SetWeights(new float[] { (w[0] / total) * 100f, (w[1] / total) * 100f, (w[2] / total) * 100f });
                }
            }
        }
        else if (ResolveWeaponMode() == EnemyWeaponMode.Bow)
        {
            IReadOnlyList<float> w = adaptiveProfile.GetBowSkillWeights();
            if (w != null && w.Count >= 3)
            {
                float total = Mathf.Max(0f, w[0]) + Mathf.Max(0f, w[1]) + Mathf.Max(0f, w[2]);
                if (total > 0f)
                {
                    offensiveTree.SetWeights(new float[] { (w[0] / total) * 100f, (w[1] / total) * 100f, (w[2] / total) * 100f });
                }
            }
        }
    }

    private void RefillBowSkillBag()
    {
        bowSkillBag.Clear();
        bowSkillBag.Add(0);
        bowSkillBag.Add(1);
        bowSkillBag.Add(2);

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

    private bool CanUseBowSkillId(int skillId, float dist)
    {
        switch (skillId)
        {
            case 0: return Combat != null && Combat.quickShotBow != null && Combat.quickShotBow.CanTrigger(dist);
            case 1: return Combat != null && Combat.piercingBow != null && Combat.piercingBow.CanTrigger(dist);
            case 2: return Combat != null && Combat.fullDrawBow != null && Combat.fullDrawBow.CanTrigger(dist);
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
        }
    }

    private bool TryExecuteBalancedBowSkill()
    {
        if (Combat == null || playerTransform == null)
            return false;

        if (bowSkillBag.Count == 0)
            RefillBowSkillBag();

        int nextSkillId = bowSkillBag[0];
        float dist = Vector2.Distance(transform.position, playerTransform.position);

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

        return dda.currentPlayerPlaystyle == PlayerPlaystyle.DefensiveDominant &&
               dda.GetCurrentDefenseRiposteWeight() >= 99f;
    }

    private void EvaluateAttackTree()
    {
        if (isPerformingAction || playerTransform == null)
            return;

        if (ResolveWeaponMode() == EnemyWeaponMode.Bow)
        {
            if (Combat != null && Combat.CanConcussiveCurrentSituation)
            {
                if (Combat.TryExecuteBowDefenseReaction())
                    return;
            }
        }

        if (ShouldEnterReactiveOnlyMode() || !IsInsideOffenseSenseRange())
            return;

        if (Movement != null && Movement.useVerticalAlign)
        {
            float absDistY = Mathf.Abs(playerTransform.position.y - transform.position.y);
            if (absDistY > Movement.verticalTolerance)
                return;
        }

        if (ResolveWeaponMode() == EnemyWeaponMode.Bow && useBowBalancedBag)
        {
            TryExecuteBalancedBowSkill();
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

        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, swordSenseRange);

        Gizmos.color = Color.cyan;
        Vector3 arrowEnd = (Vector2)transform.position + ForwardDir * 0.5f;
        Gizmos.DrawLine(transform.position, arrowEnd);
        Gizmos.DrawSphere(arrowEnd, 0.1f);
    }
}