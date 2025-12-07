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

    //=====================================================
    // INIT
    //=====================================================
    public void Init(Player ownerPlayer, float direction, float dmg, float kb, float st, float rad)
    {
        owner = ownerPlayer;
        dir = direction;
        damage = dmg;
        knockback = kb;
        stun = st;
        radius = rad;

        // initial velocity miring sedikit turun
        velocity = new Vector2(speed * dir, -speed * 0.30f);
    }

    //=====================================================
    // UPDATE
    //=====================================================
    void Update()
    {
        // life fallback
        lifeTimer += Time.deltaTime;
        if (lifeTimer > 3f)   // Safety destroy
        {
            Destroy(gameObject);
            return;
        }

        // velocity affected by gravity
        velocity.y -= gravity * Time.deltaTime;

        // movement
        transform.position += (Vector3)(velocity * Time.deltaTime);

        // rotate arrow following velocity
        if (velocity.sqrMagnitude > 0.01f)
        {
            float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        // RAYCAST KE ARAH ARROW TERBANG, bukan ke bawah
        Vector2 rayDir = velocity.normalized;
        RaycastHit2D hit = Physics2D.Raycast(transform.position, rayDir, rayDistance, groundMask);
        if (hit.collider != null)
        {
            TriggerExplosion(hit.point);
        }
    }

    //=====================================================
    // COLLISION
    //=====================================================
    void OnTriggerEnter2D(Collider2D col)
    {
        CheckEnemyHit(col);
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        CheckEnemyHit(col.collider);
    }

    void CheckEnemyHit(Collider2D col)
    {
        if (col == null) return;

        CharacterBase enemy = col.GetComponent<CharacterBase>();
        if (enemy != null)
        {
            TriggerExplosion(transform.position);
        }
    }

    //=====================================================
    // EXPLOSION
    //=====================================================
    void TriggerExplosion(Vector2 point)
    {
        if (hitAreaPrefab != null)
        {
            GameObject area = Instantiate(hitAreaPrefab, point, Quaternion.identity);
            ConcussiveHitArea c = area.GetComponent<ConcussiveHitArea>();

            if (c != null)
                c.Setup(owner, damage, knockback, stun, radius);
        }

        Destroy(gameObject);
    }
}
