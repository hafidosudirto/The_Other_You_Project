using System.Collections.Generic;
using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    [Header("References")]
    public Transform playerTransform;

    public EnemyCombatController Combat { get; private set; }
    public EnemyMovementFSM Movement { get; private set; }
    public EnemyAnimation Animation { get; private set; }

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

    // Behavior Trees
    private Node offensiveTree;
    private Node defensiveTree;

    // Balanced playstyle
    private bool balancedChosen = false;
    private bool balancedOffensive = true;

    // ============================================================
    // INIT
    // ============================================================
    private void Awake()
    {
        Combat = GetComponent<EnemyCombatController>();
        Movement = GetComponent<EnemyMovementFSM>();
        Animation = GetComponentInChildren<EnemyAnimation>(true);

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        if (spriteVisual == null && spriteRenderer != null)
        {
            spriteVisual = spriteRenderer.transform;
        }

        rb2d = GetComponent<Rigidbody2D>();
        if (rb2d == null) rb2d = GetComponentInChildren<Rigidbody2D>(true);
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
        if (rb2d != null) speed = Mathf.Abs(rb2d.velocity.x);
        else
        {
            Vector3 delta = transform.position - prevPos;
            speed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        }

        float speed01 = Mathf.Clamp01(speed / Mathf.Max(0.01f, maxMoveSpeed));
        Animation.SetMoveSpeed(speed01);
        prevPos = transform.position;
    }

    private void Start()
    {
        if (playerTransform == null)
        {
            playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        // Sambungkan ActionLock dari combat
        if (Combat != null)
        {
            Combat.OnSkillStart += OnActionStart;
            Combat.OnSkillEnd += OnActionEnd;
        }

        BuildAttackTree();

        // Set initial facing
        prevPos = transform.position;
        if (playerTransform != null)
        {
            UpdateDesiredFacing();
            ApplyFacing();
        }
    }

    // ============================================================
    // UPDATE LOOP
    // ============================================================
    public void Update()
    {
        // 1. Cari Player Terus Menerus jika hilang
        if (playerTransform == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) playerTransform = p.transform;
            else return;
        }

        // 2. UPDATE FACING - SELALU update facing ke arah player
        UpdateDesiredFacing();

        // 3. Jalankan Logika Movement (Hanya jika tidak sedang attack)
        if (!isPerformingAction && Movement != null)
        {
            Movement.Update();
            UpdateLocomotionAnim();
        }

        // 4. Jalankan Tree
        EvaluateAttackTree();
    }

    private void LateUpdate()
    {
        // Apply facing visual setiap frame
        ApplyFacing();
    }

    private void UpdateDesiredFacing()
    {
        if (!playerTransform) return;

        // Gunakan posisi world yang lebih akurat
        Vector3 enemyPos = transform.position;
        Vector3 playerPos = playerTransform.position;

        float dx = playerPos.x - enemyPos.x;

        // Tentukan facing berdasarkan posisi player
        desiredFacingRight = dx >= 0f;
    }

    private void ApplyFacing()
    {
        if (spriteRenderer == null) return;

        if (invertFlipX)
        {
            // Sprite default menghadap KIRI
            spriteRenderer.flipX = desiredFacingRight;
        }
        else
        {
            // Sprite default menghadap KANAN
            spriteRenderer.flipX = !desiredFacingRight;
        }
    }

    // ============================================================
    // ACTION LOCK
    // ============================================================
    public void OnActionStart()
    {
        isPerformingAction = true;
        if (Movement) Movement.enabled = false;
    }

    public void OnActionEnd()
    {
        isPerformingAction = false;
        if (Movement) Movement.enabled = true;
    }

    // ============================================================
    // RANGE CHECK
    // ============================================================
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
        var nodes = new List<Node> {
            new SwordSlashComboNode(this),
            new SwordWhirlwindNode(this),
            new SwordChargedStrikeNode(this),
            new SwordRiposteNode(this) };

        var weights = new List<float> { 1f, 1f, 1f, 1f };
        offensiveTree = new WeightedRandomSelector(nodes, weights);
    }

    // ============================================================
    // EVALUATE BEHAVIOR TREE
    // ============================================================
    private void EvaluateAttackTree()
    {
        if (isPerformingAction) return;
        if (playerTransform == null) return;
        if (!IsInAttackRange()) return;

        var dda = DDAController.Instance;
        if (dda == null) return;

        var style = dda.currentPlayerPlaystyle;

        switch (style)
        {
            case PlayerPlaystyle.OffensiveDominant:
                balancedChosen = false;
                offensiveTree?.Evaluate();
                break;

            case PlayerPlaystyle.DefensiveDominant:
                balancedChosen = false;
                if (defensiveTree != null) defensiveTree.Evaluate();
                else offensiveTree?.Evaluate();
                break;

            case PlayerPlaystyle.Balanced:
            default:
                if (!balancedChosen)
                {
                    balancedChosen = true;
                    balancedOffensive = Random.value > 0.5f;
                }

                if (balancedOffensive)
                {
                    offensiveTree?.Evaluate();
                }
                else
                {
                    if (defensiveTree != null) defensiveTree.Evaluate();
                    else offensiveTree?.Evaluate();
                }
                break;
        }
    }

    // ============================================================
    // GIZMOS
    // ============================================================
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