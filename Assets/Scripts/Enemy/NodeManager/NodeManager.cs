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
    [SerializeField] private float maxMoveSpeed = 3f;

    [Header("Action State")]
    public bool isPerformingAction = false;

    private Rigidbody2D rb2d;
    private bool desiredFacingRight = true;
    public bool IsFacingRight => desiredFacingRight;

    public int ForwardSign => desiredFacingRight ? 1 : -1;
    public Vector2 ForwardDir => new Vector2(ForwardSign, 0f);
    public bool VisualFacingRight => desiredFacingRight;

    // Sub-trees references
    private Sword_AttackTree swordTree;
    private Bow_AttackTree bowTree;

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
        if (ResolveWeaponMode() == EnemyWeaponMode.Sword && swordTree != null)
            swordTree.Initialize(this);
        else if (ResolveWeaponMode() == EnemyWeaponMode.Bow && bowTree != null)
            bowTree.Initialize(this);
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

        // Jalankan FSM Pergerakan
        if (Movement != null)
        {
            Movement.Tick();
        }

        // --- FIX ERROR 1: UPDATE ANIMASI JALAN ---
        // Baca kecepatan fisik musuh, lalu kirimkan ke Animator
        if (Animation != null && rb2d != null)
        {
            Animation.SetMoveSpeed(rb2d.velocity.magnitude);
        }

        // Mencegah Behavior Tree memikirkan serangan baru jika musuh masih mengeksekusi aksi/animasi
        if (isPerformingAction)
            return;

        // Evaluasi logika serangan sesuai senjata aktif
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

    public EnemyWeaponMode ResolveWeaponMode() => weaponMode;

    private void UpdateDesiredFacing()
    {
        if (playerTransform != null)
        {
            desiredFacingRight = playerTransform.position.x > transform.position.x;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = invertFlipX ? desiredFacingRight : !desiredFacingRight;
        }
        else if (spriteVisual != null)
        {
            float scaleX = Mathf.Abs(spriteVisual.localScale.x);
            spriteVisual.localScale = new Vector3(desiredFacingRight ? scaleX : -scaleX, spriteVisual.localScale.y, spriteVisual.localScale.z);
        }
    }

    public Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root.name == targetName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), targetName);
            if (found != null) return found;
        }
        return null;
    }

    // --- FUNGSI & PROPERTI TAMBAHAN UNTUK MEMPERBAIKI ERROR CS1061 ---

    public bool IsInAttackRange()
    {
        if (playerTransform == null) return false;
        float distance = Vector2.Distance(transform.position, playerTransform.position);
        return distance <= attackRange;
    }

    public void OnActionStart()
    {
        isPerformingAction = true;
    }

    public void OnActionEnd()
    {
        isPerformingAction = false;
    }

    [Header("Stage Settings")]
    public int attackTokens = 3;
    public bool isBoss = false; 

    public void InitializeStageEnemy(CharacterBase character, int tokens, bool bossStatus)
    {
        this.attackTokens = tokens;
        this.isBoss = bossStatus; 

        if (character != null)
        {
            if (Combat != null && Combat.stats != null)
                Combat.stats.attack = character.attack;

            if (Movement != null)
                Movement.moveSpeed = character.moveSpeed;
        }

        if (adaptiveProfile != null)
            adaptiveProfile.RefreshFromDDA();
    }
}