using UnityEngine;
using System.Collections.Generic;

public class EnemyAI : MonoBehaviour
{
    [Header("References")]
    public Transform playerTransform;

    public EnemyCombatController Combat { get; private set; }
    public EnemyMovementFSM Movement { get; private set; }
    public EnemyAnimation Animation { get; private set; }  

    // ---- Facing helper ----
    public bool IsFacingRight
    {
        get => transform.localScale.x > 0f;
        set
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (value ? 1f : -1f);
            transform.localScale = scale;
        }
    }

    [Header("Ranges")]
    public float attackRange = 1.8f;

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
        Animation = GetComponent<EnemyAnimation>();    // FIX 3: load animation reference
    }

    private void Start()
    {
        if (playerTransform == null)
            playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;

        // Sambungkan ActionLock dari combat
        Combat.OnSkillStart += OnActionStart;
        Combat.OnSkillEnd += OnActionEnd;

        BuildOffensiveTree();
        BuildDefensiveTree();
    }

    // ============================================================
    // UPDATE LOOP
    // ============================================================
    public void Update()
    {
        if (!isPerformingAction)
            Movement.Update();        

        EvaluateAttackTree();
        AutoFacePlayer();
    }

    // ============================================================
    // AUTO FACE PLAYER
    // ============================================================
    private void AutoFacePlayer()
    {
        if (!playerTransform) return;

        IsFacingRight = playerTransform.position.x > transform.position.x;
    }

    // ============================================================
    // ACTION LOCK
    // ============================================================
    public void OnActionStart()
    {
        isPerformingAction = true;
        Movement.enabled = false;
    }

    public void OnActionEnd()
    {
        isPerformingAction = false;
        Movement.enabled = true;
    }

    // ============================================================
    // RANGE CHECK
    // ============================================================
    public bool IsInAttackRange()
    {
        if (!playerTransform) return false;
        return Vector2.Distance(transform.position, playerTransform.position) <= attackRange;
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

    // ============================================================
    // BUILD OFFENSIVE TREE
    // ============================================================
    private void BuildOffensiveTree()
    {
        offensiveTree = new RandomSelector(new List<Node>
        {
            new SwordSlashComboNode(this),
            new SwordWhirlwindNode(this),
            new SwordChargedStrikeNode(this),
            new SwordRiposteNode(this)
        });
    }

    // ============================================================
    // BUILD DEFENSIVE TREE (random)
    // ============================================================
    private void BuildDefensiveTree()
    {
        defensiveTree = new RandomSelector(new List<Node>
        {
            new DashOutNode(this),
            new SwordRiposteNode(this),
            new SwordSlashComboNode(this)
        });
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
                offensiveTree.Evaluate();
                break;

            case PlayerPlaystyle.DefensiveDominant:
                balancedChosen = false;
                defensiveTree.Evaluate();
                break;

            case PlayerPlaystyle.Balanced:
            default:
                if (!balancedChosen)
                {
                    balancedChosen = true;
                    balancedOffensive = Random.value > 0.5f;
                }

                if (balancedOffensive)
                    offensiveTree.Evaluate();
                else
                    defensiveTree.Evaluate();
                break;
        }
    }

    // ============================================================
    // GIZMOS
    // ============================================================
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
