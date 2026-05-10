using UnityEngine;

public class NodeManager : MonoBehaviour
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

    [Header("Combat Stats")]
    public float AttackPower = 10f;
    public float attackRange = 2f;

    [Header("Adaptive")]
    public EnemyAdaptiveProfile adaptiveProfile;

    [Header("Visual Facing")]
    [SerializeField] private Transform spriteVisual;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private bool invertFlipX = false;
    [SerializeField] private float maxMoveSpeed = 4f;

    [Header("Bow Legacy Movement Preset")]
    [Tooltip("Radius maksimal Bow mulai merespons player. Nilai ini sengaja besar agar Bow tidak diam saat player jauh.")]
    [SerializeField] private float bowSenseRange = 15f;

    [Tooltip("Jarak default Bow jika belum ada skill Bow yang sedang diantrekan.")]
    [SerializeField] private float bowDesiredRange = 5.25f;

    [Tooltip("Jarak minimum default Bow jika belum ada skill Bow yang sedang diantrekan.")]
    [SerializeField] private float bowMinimumRange = 2.25f;

    [SerializeField] private float bowRangeTolerance = 0.25f;

    [Header("Sword Legacy Movement Preset")]
    [SerializeField] private float swordDesiredRange = 1.8f;
    [SerializeField] private float swordSenseRange = 3.2f;
    [SerializeField] private float swordRangeTolerance = 0.25f;

    [Header("Action Lock")]
    public bool isPerformingAction = false;

    [Header("Movement Bounds")]
    [SerializeField] private bool limitYPosition = true;
    [SerializeField] private float minYPosition = -4f;
    [SerializeField] private float maxYPosition = 1.6f;

    private Rigidbody2D rb2d;
    private bool desiredFacingRight = true;
    private Vector3 prevPos;

    private Sword_AttackTree swordTree;
    private Bow_AttackTree bowTree;

    private bool swordTreeInitialized;
    private bool bowTreeInitialized;
    private bool combatEventsSubscribed;

    public EnemyWeaponMode ActiveWeaponMode => ResolveWeaponMode();

    public bool IsFacingRight => desiredFacingRight;
    public int ForwardSign => desiredFacingRight ? 1 : -1;
    public Vector2 ForwardDir => new Vector2(ForwardSign, 0f);
    public bool VisualFacingRight => desiredFacingRight;

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

        swordTree = GetComponent<Sword_AttackTree>();
        bowTree = GetComponent<Bow_AttackTree>();
    }

    private void OnEnable()
    {
        SubscribeCombatEvents();
    }

    private void Start()
    {
        EnsurePlayerReference();
        InitializeAttackTreeForCurrentWeapon();
        ApplyMovementPresetForCurrentWeapon();

        prevPos = transform.position;

        if (playerTransform != null)
        {
            UpdateDesiredFacing();
            ApplyFacing();
        }
    }

    private void OnDisable()
    {
        UnsubscribeCombatEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeCombatEvents();
    }

    private void Update()
    {
        if (!EnsurePlayerReference())
            return;

        InitializeAttackTreeForCurrentWeapon();

        UpdateDesiredFacing();
        ApplyMovementPresetForCurrentWeapon();

        /*
         * Ini sengaja meniru pola EnemyAI lama:
         * Movement hanya berjalan ketika musuh tidak sedang melakukan action.
         * Dengan pola ini Bow tidak akan menyerang sambil berjalan.
         */
        if (!isPerformingAction && Combat != null && !Combat.IsBusy && Movement != null && Movement.enabled)
        {
            Movement.SetPlayer(playerTransform);
            Movement.Tick();
        }

        UpdateLocomotionAnim();

        if (!isPerformingAction && Combat != null && !Combat.IsBusy)
            EvaluateAttackTree();
    }

    private void LateUpdate()
    {
        ApplyFacing();
        ClampYPosition();
    }

    private void SubscribeCombatEvents()
    {
        if (combatEventsSubscribed)
            return;

        if (Combat == null)
            Combat = GetComponent<EnemyCombatController>();

        if (Combat == null)
            return;

        Combat.OnSkillStart += OnActionStart;
        Combat.OnSkillEnd += OnActionEnd;

        combatEventsSubscribed = true;
    }

    private void UnsubscribeCombatEvents()
    {
        if (!combatEventsSubscribed)
            return;

        if (Combat != null)
        {
            Combat.OnSkillStart -= OnActionStart;
            Combat.OnSkillEnd -= OnActionEnd;
        }

        combatEventsSubscribed = false;
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

    public EnemyWeaponMode ResolveWeaponMode()
    {
        if (weaponMode != EnemyWeaponMode.Auto)
            return weaponMode;

        if (Combat != null && Combat.HasBowSkills)
            return EnemyWeaponMode.Bow;

        if (bowTree != null && swordTree == null)
            return EnemyWeaponMode.Bow;

        if (swordTree != null && bowTree == null)
            return EnemyWeaponMode.Sword;

        Transform skillRootBow = FindChildRecursive(transform, "SkillRoot_Bow");
        if (skillRootBow != null)
            return EnemyWeaponMode.Bow;

        return EnemyWeaponMode.Sword;
    }

    private void InitializeAttackTreeForCurrentWeapon()
    {
        EnemyWeaponMode currentMode = ResolveWeaponMode();

        if (currentMode == EnemyWeaponMode.Bow && bowTree != null)
        {
            if (!bowTreeInitialized)
            {
                bowTree.Initialize(this);
                bowTreeInitialized = true;
            }

            attackRange = bowTree.attackRange;
            bowTree.bowSenseRange = bowSenseRange;
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
        }
    }

    private void ApplyMovementPresetForCurrentWeapon()
    {
        if (Movement == null)
            return;

        Movement.SetPlayer(playerTransform);

        if (ResolveWeaponMode() == EnemyWeaponMode.Bow)
        {
            Movement.SetMovementMode(EnemyMovementFSM.CombatMovementMode.Bow);

            float desiredRange = bowDesiredRange;
            float minimumRange = bowMinimumRange;

            /*
             * Ini inti perilaku lama:
             * Bow mengejar berdasarkan range skill Bow yang sedang dipilih oleh DDA.
             * Jika DDA memilih QuickShot, Bow mengejar sampai range QuickShot.
             * Jika DDA memilih FullDraw, Bow mengejar sampai range FullDraw.
             */
            if (bowTree != null)
            {
                desiredRange = bowTree.GetDesiredRangeForMovement(bowDesiredRange);
                minimumRange = bowTree.GetMinimumRangeForMovement(bowMinimumRange);
            }

            Movement.SetDesiredRange(Mathf.Max(0.1f, desiredRange));
            Movement.SetMinimumCombatRange(Mathf.Max(0f, minimumRange));
            Movement.rangeTolerance = bowRangeTolerance;
        }
        else
        {
            Movement.SetMovementMode(EnemyMovementFSM.CombatMovementMode.Sword);
            Movement.SetDesiredRange(Mathf.Max(0.1f, swordDesiredRange));
            Movement.SetMinimumCombatRange(0f);
            Movement.rangeTolerance = swordRangeTolerance;
        }
    }

    private void EvaluateAttackTree()
    {
        if (playerTransform == null)
            return;

        EnemyWeaponMode currentMode = ResolveWeaponMode();

        if (currentMode == EnemyWeaponMode.Bow)
        {
            if (bowTree != null)
                bowTree.EvaluateTree();

            return;
        }

        if (currentMode == EnemyWeaponMode.Sword)
        {
            float distance = Vector2.Distance(transform.position, playerTransform.position);

            if (distance > swordSenseRange)
                return;

            if (swordTree != null)
                swordTree.EvaluateTree();
        }
    }

    private void UpdateLocomotionAnim()
    {
        if (Animation == null)
            return;

        if (isPerformingAction || Combat != null && Combat.IsBusy)
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
        if (playerTransform == null)
            return;

        /*
         * Saat action, arah hadap tidak diubah oleh velocity.
         * Arah hadap dikunci ke player di OnActionStart().
         */
        if (isPerformingAction || Combat != null && Combat.IsBusy)
            return;

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
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = invertFlipX ? desiredFacingRight : !desiredFacingRight;
            return;
        }

        if (spriteVisual != null)
        {
            float scaleX = Mathf.Abs(spriteVisual.localScale.x);

            spriteVisual.localScale = new Vector3(
                desiredFacingRight ? scaleX : -scaleX,
                spriteVisual.localScale.y,
                spriteVisual.localScale.z
            );
        }
    }

    private void ClampYPosition()
    {
        if (!limitYPosition)
            return;

        Vector2 currentPosition = rb2d != null ? rb2d.position : (Vector2)transform.position;
        float clampedY = Mathf.Clamp(currentPosition.y, minYPosition, maxYPosition);

        if (Mathf.Approximately(currentPosition.y, clampedY))
            return;

        if (rb2d != null)
        {
            rb2d.position = new Vector2(currentPosition.x, clampedY);

            Vector2 currentVelocity = rb2d.velocity;

            if (currentPosition.y < minYPosition && currentVelocity.y < 0f ||
                currentPosition.y > maxYPosition && currentVelocity.y > 0f)
            {
                rb2d.velocity = new Vector2(currentVelocity.x, 0f);
            }
        }
        else
        {
            Vector3 position = transform.position;
            transform.position = new Vector3(position.x, clampedY, position.z);
        }
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

        if (rb2d != null)
            rb2d.velocity = Vector2.zero;

        /*
         * Ini mengikuti perilaku EnemyAI lama:
         * saat skill dimulai, Movement benar-benar dimatikan.
         */
        if (Movement != null)
        {
            Movement.StopImmediately();
            Movement.enabled = false;
        }

        UpdateLocomotionAnim();
    }

    public void OnActionEnd()
    {
        isPerformingAction = false;

        if (Movement != null)
        {
            Movement.enabled = true;
            Movement.ForceClearAllMovementLocks(true);
            Movement.SetPlayer(playerTransform);
        }

        prevPos = transform.position;
    }

    public bool IsInAttackRange()
    {
        if (playerTransform == null)
            return false;

        float distance = Vector2.Distance(transform.position, playerTransform.position);
        return distance <= attackRange;
    }

    public float GetBowSenseRange()
    {
        return bowSenseRange;
    }

    public Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root == null)
            return null;

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

    public void InitializeStageEnemy(CharacterBase character, int tokens, bool bossStatus)
    {
        if (character != null)
        {
            if (Combat != null && Combat.stats != null)
                Combat.stats.attack = character.attack;

            if (Movement != null)
                Movement.moveSpeed = character.moveSpeed;
        }

        adaptiveProfile?.RefreshFromDDA();

        EnsurePlayerReference();
        InitializeAttackTreeForCurrentWeapon();
        ApplyMovementPresetForCurrentWeapon();
    }

    private void OnDrawGizmosSelected()
    {
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