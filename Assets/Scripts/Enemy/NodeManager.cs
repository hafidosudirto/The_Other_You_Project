using System.Collections.Generic;
using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    [Header("References")]
    public Transform playerTransform;

    public EnemyCombatController Combat { get; private set; }
    public EnemyMovementFSM Movement { get; private set; }
    public EnemyAnimation Animation { get; private set; }

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

    [Header("Ranges")]
    public float attackRange = 1.8f;
    [SerializeField] private Vector2 attackRangeOffset = new Vector2(1f, 0f);

    [Header("Action Lock")]
    public bool isPerformingAction = false;

    private WeightedRandomSelector offensiveTree;
    private int cachedProfileVersion = -1;

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

        if (!isPerformingAction && Movement != null)
        {
            Movement.Update();
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
            speed = Mathf.Abs(rb2d.velocity.x);
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

        if (invertFlipX)
            spriteRenderer.flipX = desiredFacingRight;
        else
            spriteRenderer.flipX = !desiredFacingRight;
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
            if (Combat != null && Combat.stats != null)
                return Combat.stats.attack;
            return 10f;
        }
    }

    private void BuildAttackTree()
    {
        var nodes = new List<Node>
        {
            new SwordSlashComboNode(this),
            new SwordWhirlwindNode(this),
            new SwordChargedStrikeNode(this)
            // new SwordRiposteNode(this)
        };

        var weights = new List<float> { 34f, 33f, 33f };
        offensiveTree = new WeightedRandomSelector(nodes, weights);
    }

    private void RefreshAdaptiveWeights(bool force = false)
    {
        var dda = DDAController.Instance;
        if (adaptiveProfile == null || offensiveTree == null)
            return;

        if (dda == null)
        {
            offensiveTree.SetWeights(new float[] { 34f, 33f, 33f });
            return;
        }

        if (!force && cachedProfileVersion == dda.ProfileVersion)
            return;

        cachedProfileVersion = dda.ProfileVersion;
        adaptiveProfile.RefreshFromDDA();

        // Ambil bobot sword dari DDA, tetapi hanya untuk 3 skill ofensif.
        IReadOnlyList<float> allWeights = adaptiveProfile.GetSwordSkillWeights();

        if (allWeights == null || allWeights.Count < 3)
        {
            offensiveTree.SetWeights(new float[] { 34f, 33f, 33f });
            return;
        }

        float slash = Mathf.Max(0f, allWeights[0]);
        float whirlwind = Mathf.Max(0f, allWeights[1]);
        float charged = Mathf.Max(0f, allWeights[2]);

        float total = slash + whirlwind + charged;
        if (total <= 0f)
        {
            offensiveTree.SetWeights(new float[] { 34f, 33f, 33f });
            return;
        }

        offensiveTree.SetWeights(new float[]
        {
            (slash / total) * 100f,
            (whirlwind / total) * 100f,
            (charged / total) * 100f
        });
    }

    private bool ShouldEnterReactiveOnlyMode()
    {
        var dda = DDAController.Instance;
        if (dda == null)
            return false;

        // Mode ini aktif ketika player sebelumnya dominan defensif
        // dan profil defense menunjukkan riposte sangat dominan.
        return dda.currentPlayerPlaystyle == PlayerPlaystyle.DefensiveDominant
            && dda.GetCurrentDefenseRiposteWeight() >= 99f;
    }

    private void EvaluateAttackTree()
    {
        // KUNCI UTAMA:
        // Jika mode reaktif penuh aktif, enemy tidak menyerang proaktif.
        if (ShouldEnterReactiveOnlyMode())
            return;

        if (isPerformingAction) return;
        if (playerTransform == null) return;
        if (!IsInAttackRange()) return;

        offensiveTree?.Evaluate();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector2 attackCenter = GetAttackRangeCenter();
        Gizmos.DrawWireSphere(attackCenter, attackRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, attackCenter);

        Gizmos.color = Color.cyan;
        Vector3 arrowEnd = (Vector2)transform.position + ForwardDir * 0.5f;
        Gizmos.DrawLine(transform.position, arrowEnd);
        Gizmos.DrawSphere(arrowEnd, 0.1f);
    }
}