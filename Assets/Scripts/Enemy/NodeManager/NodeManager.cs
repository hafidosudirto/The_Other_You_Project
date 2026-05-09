using UnityEngine;

public class NodeManager : MonoBehaviour
{
    public enum EnemyWeaponMode { Auto, Sword, Bow }

    [Header("References")]
    public Transform playerTransform;
    public EnemyCombatController Combat { get; private set; }
    public EnemyMovementFSM Movement { get; private set; }
    public EnemyAnimation Animation { get; private set; }

    [Header("Combat Mode")]
    [SerializeField] private EnemyWeaponMode weaponMode = EnemyWeaponMode.Auto;

    [Header("Combat Stats")]
    public float AttackPower = 10f;
    public float attackRange = 2f;

    [Header("Adaptive")]
    public EnemyAdaptiveProfile adaptiveProfile;

    [Header("Visual Facing")]
    [SerializeField] private Transform spriteVisual;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private bool invertFlipX = false;

    [Header("Animation")]
    [SerializeField] private bool sideViewWalkUsesHorizontalSpeedOnly = true;

    [Header("Bow Movement Defaults")]
    [SerializeField] private float bowDesiredRangeMultiplier = 0.75f;
    [SerializeField] private float bowMinimumRangeMultiplier = 0.35f;
    [SerializeField] private float bowRangeTolerance = 0.5f;

    [Header("Sword Movement Defaults")]
    [SerializeField] private float swordDesiredRangeMultiplier = 0.85f;
    [SerializeField] private float swordRangeTolerance = 0.25f;

    [Header("Action State")]
    public bool isPerformingAction = false;

    [Header("Stage Settings")]
    public int attackTokens = 3;
    public bool isBoss = false;

    private Rigidbody2D rb2d;
    private bool desiredFacingRight = true;
    private Sword_AttackTree swordTree;
    private Bow_AttackTree bowTree;
    private bool swordTreeInitialized;
    private bool bowTreeInitialized;

    public bool IsFacingRight => desiredFacingRight;
    public int ForwardSign => desiredFacingRight ? 1 : -1;
    public Vector2 ForwardDir => new Vector2(ForwardSign, 0f);
    public bool VisualFacingRight => desiredFacingRight;

    private void Awake()
    {
        Combat = GetComponent<EnemyCombatController>();
        Movement = GetComponent<EnemyMovementFSM>();
        Animation = GetComponentInChildren<EnemyAnimation>();
        rb2d = GetComponent<Rigidbody2D>();

        swordTree = GetComponent<Sword_AttackTree>();
        bowTree = GetComponent<Bow_AttackTree>();
    }

    private void Start()
    {
        EnsurePlayerReference();
        ConfigureForResolvedWeaponMode();
    }

    private void Update()
    {
        if (!EnsurePlayerReference())
            return;

        UpdateDesiredFacing();
        UpdateWalkAnimationParameter();

        if (isPerformingAction)
            return;

        EnemyWeaponMode currentMode = ResolveWeaponMode();
        if (currentMode == EnemyWeaponMode.Sword && swordTree != null)
        {
            swordTree.EvaluateTree();
        }
        else if (currentMode == EnemyWeaponMode.Bow && bowTree != null)
        {
            bowTree.EvaluateTree();
        }
    }

    private void FixedUpdate()
    {
        if (!EnsurePlayerReference())
            return;

        if (Movement != null)
        {
            Movement.SetPlayer(playerTransform);
            Movement.Tick();
        }
    }

    public EnemyWeaponMode ResolveWeaponMode()
    {
        if (weaponMode != EnemyWeaponMode.Auto)
            return weaponMode;

        if (bowTree != null && swordTree == null)
            return EnemyWeaponMode.Bow;

        if (swordTree != null && bowTree == null)
            return EnemyWeaponMode.Sword;

        if (bowTree != null)
            return EnemyWeaponMode.Bow;

        return EnemyWeaponMode.Sword;
    }

    private void ConfigureForResolvedWeaponMode()
    {
        EnemyWeaponMode currentMode = ResolveWeaponMode();

        if (Movement != null)
            Movement.SetPlayer(playerTransform);

        if (currentMode == EnemyWeaponMode.Bow && bowTree != null)
        {
            if (!bowTreeInitialized)
            {
                bowTree.Initialize(this);
                bowTreeInitialized = true;
            }

            attackRange = bowTree.attackRange;

            if (Movement != null)
            {
                Movement.SetMovementMode(EnemyMovementFSM.CombatMovementMode.Bow);
                Movement.SetDesiredRange(Mathf.Max(0.1f, bowTree.attackRange * bowDesiredRangeMultiplier));
                Movement.SetMinimumCombatRange(Mathf.Max(0.1f, bowTree.attackRange * bowMinimumRangeMultiplier));
                Movement.rangeTolerance = bowRangeTolerance;
            }

            bowTree.SyncWeightsFromDDA();
            return;
        }

        if (currentMode == EnemyWeaponMode.Sword && swordTree != null)
        {
            if (!swordTreeInitialized)
            {
                swordTree.Initialize(this);
                swordTreeInitialized = true;
            }

            attackRange = swordTree.attackRange;

            if (Movement != null)
            {
                Movement.SetMovementMode(EnemyMovementFSM.CombatMovementMode.Sword);
                Movement.SetDesiredRange(Mathf.Max(0.1f, swordTree.attackRange * swordDesiredRangeMultiplier));
                Movement.SetMinimumCombatRange(0f);
                Movement.rangeTolerance = swordRangeTolerance;
            }
        }
    }

    private bool EnsurePlayerReference()
    {
        if (playerTransform != null)
            return true;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return false;

        playerTransform = player.transform;
        if (Movement != null)
            Movement.SetPlayer(playerTransform);

        return true;
    }

    private void UpdateWalkAnimationParameter()
    {
        if (Animation == null || rb2d == null)
            return;

        float moveSpeedForAnimation = sideViewWalkUsesHorizontalSpeedOnly
            ? Mathf.Abs(rb2d.velocity.x)
            : rb2d.velocity.magnitude;

        Animation.SetMoveSpeed(moveSpeedForAnimation);
    }

    private void UpdateDesiredFacing()
    {
        if (playerTransform != null)
            desiredFacingRight = playerTransform.position.x > transform.position.x;

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = invertFlipX ? desiredFacingRight : !desiredFacingRight;
        }
        else if (spriteVisual != null)
        {
            float scaleX = Mathf.Abs(spriteVisual.localScale.x);
            spriteVisual.localScale = new Vector3(
                desiredFacingRight ? scaleX : -scaleX,
                spriteVisual.localScale.y,
                spriteVisual.localScale.z
            );
        }
    }

    public Transform FindChildRecursive(Transform root, string targetName)
    {
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

    public bool IsInAttackRange()
    {
        if (playerTransform == null)
            return false;

        float distance = Vector2.Distance(transform.position, playerTransform.position);
        return distance <= attackRange;
    }

    public void OnActionStart(float movementLockDuration = -1f)
    {
        isPerformingAction = true;
        Movement?.NotifyAttackStarted(movementLockDuration);
    }

    public void OnActionEnd()
    {
        isPerformingAction = false;
        Movement?.NotifyAttackEnded();
    }

    public void InitializeStageEnemy(CharacterBase character, int tokens, bool bossStatus)
    {
        attackTokens = tokens;
        isBoss = bossStatus;

        if (character != null)
        {
            if (Combat != null && Combat.stats != null)
                Combat.stats.attack = character.attack;

            if (Movement != null)
                Movement.moveSpeed = character.moveSpeed;
        }

        adaptiveProfile?.RefreshFromDDA();
        EnsurePlayerReference();
        ConfigureForResolvedWeaponMode();
    }
}
