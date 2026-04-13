using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class ConcussiveArrowVisual : MonoBehaviour
{
    [Header("Flight Settings")]
    public float speed = 12f;
    public float gravity = 8f;
    public float rayDistance = 0.25f;
    public LayerMask groundMask;

    [Header("Hit Area")]
    public GameObject hitAreaPrefab;

    [Header("Runtime Stats")]
    public Player owner;
    public float damage;
    public float knockback;
    public float stun;
    public float radius;

    private Vector2 velocity;
    private float dir;
    private float lifeTimer;
    private bool hasExploded;

    private Collider2D myCollider;

    // =====================================================
    // AWAKE
    // =====================================================
    private void Awake()
    {
        myCollider = GetComponent<Collider2D>();
    }

    // =====================================================
    // INIT
    // =====================================================
    public void Init(Player ownerPlayer, float direction, float dmg, float kb, float st, float rad)
    {
        owner = ownerPlayer;
        dir = direction;
        damage = dmg;
        knockback = kb;
        stun = st;
        radius = rad;

        // Panah sedikit menukik ke bawah
        velocity = new Vector2(speed * dir, -speed * 0.08f);

        IgnoreOwnerCollision();
    }

    // =====================================================
    // IGNORE OWNER COLLISION
    // =====================================================
    private void IgnoreOwnerCollision()
    {
        if (owner == null || myCollider == null)
            return;

        Collider2D[] ownerColliders = owner.GetComponentsInChildren<Collider2D>();
        foreach (Collider2D col in ownerColliders)
        {
            if (col == null) continue;
            Physics2D.IgnoreCollision(myCollider, col, true);
        }
    }

    // =====================================================
    // UPDATE
    // =====================================================
    private void Update()
    {
        if (hasExploded)
            return;

        lifeTimer += Time.deltaTime;
        if (lifeTimer > 3f)
        {
            Destroy(gameObject);
            return;
        }

        // Gravity
        velocity.y -= gravity * Time.deltaTime;

        // Movement
        transform.position += (Vector3)(velocity * Time.deltaTime);

        // Rotate mengikuti arah gerak
        if (velocity.sqrMagnitude > 0.01f)
        {
            float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        // Deteksi tanah memakai raycast ke arah gerak
        if (velocity.sqrMagnitude > 0.0001f)
        {
            Vector2 rayDir = velocity.normalized;
            RaycastHit2D hit = Physics2D.Raycast(transform.position, rayDir, rayDistance, groundMask);

            if (hit.collider != null)
            {
                TriggerExplosion(hit.point);
            }
        }
    }

    // =====================================================
    // COLLISION
    // =====================================================
    private void OnTriggerEnter2D(Collider2D col)
    {
        if (hasExploded) return;
        CheckEnemyHit(col);
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (hasExploded) return;
        CheckEnemyHit(col.collider);
    }

    private void CheckEnemyHit(Collider2D col)
    {
        if (col == null) return;

        CharacterBase target = col.GetComponent<CharacterBase>();
        if (target == null)
            target = col.GetComponentInParent<CharacterBase>();

        if (target == null)
            return;

        // Abaikan owner sendiri
        if (owner != null && target.transform.root == owner.transform.root)
            return;

        Vector2 hitPoint = col.ClosestPoint(transform.position);
        TriggerExplosion(hitPoint);
    }

    // =====================================================
    // EXPLOSION
    // =====================================================
    private void TriggerExplosion(Vector2 point)
    {
        if (hasExploded)
            return;

        hasExploded = true;

        if (hitAreaPrefab != null)
        {
            GameObject area = Instantiate(hitAreaPrefab, point, Quaternion.identity);
            ConcussiveHitArea hitArea = area.GetComponent<ConcussiveHitArea>();

            if (hitArea != null)
                hitArea.Setup(owner, damage, knockback, stun, radius);
        }

        Destroy(gameObject);
    }
}