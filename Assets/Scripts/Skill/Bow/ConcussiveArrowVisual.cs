using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class ConcussiveArrowVisual : MonoBehaviour
{
    [Header("Flight Settings")]
    public float speed = 12f;
    public float gravity = 8f;

    [Tooltip("0 = lurus, 0.2 = agak turun, 0.35 = lebih curam.")]
    [Range(0f, 1f)]
    public float initialDownFactor = 0.22f;

    [Tooltip("Raycast pendek ke arah gerak panah untuk mendeteksi tanah/tembok.")]
    public float rayDistance = 0.15f;

    [Tooltip("Waktu sebelum proyektil mulai boleh menabrak apa pun.")]
    public float armDelay = 0.05f;

    [Tooltip("Batas hidup proyektil.")]
    public float lifeTime = 3f;

    [Header("Rotation")]
    [Tooltip("0 jika sprite sudah menghadap arah gerak yang benar. Coba 180 jika terbalik.")]
    public float angleOffset = 0f;

    [Header("Hit Area")]
    public GameObject hitAreaPrefab;
    public LayerMask groundMask;

    [Header("Runtime Stats")]
    public Player owner;
    public float damage;
    public float knockback;
    public float stun;
    public float radius;

    private Vector2 velocity;
    private float dir;
    private float lifeTimer;
    private float armTimer;
    private bool initialized;
    private bool exploded;

    public void Init(Player ownerPlayer, float direction, float dmg, float kb, float st, float rad)
    {
        owner = ownerPlayer;
        dir = (direction >= 0f) ? 1f : -1f;
        damage = dmg;
        knockback = kb;
        stun = st;
        radius = rad;

        lifeTimer = 0f;
        armTimer = 0f;
        exploded = false;
        initialized = true;

        transform.rotation = Quaternion.identity;

        Vector3 s = transform.localScale;
        transform.localScale = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));

        velocity = new Vector2(speed * dir, -speed * initialDownFactor);

        UpdateRotation();
    }

    private void Update()
    {
        if (!initialized || exploded)
            return;

        float dt = Time.deltaTime;

        lifeTimer += dt;
        armTimer += dt;

        if (lifeTimer >= lifeTime)
        {
            Destroy(gameObject);
            return;
        }

        velocity.y -= gravity * dt;
        transform.position += (Vector3)(velocity * dt);

        UpdateRotation();

        if (armTimer < armDelay)
            return;

        Vector2 rayDir = velocity.sqrMagnitude > 0.0001f ? velocity.normalized : new Vector2(dir, 0f);
        RaycastHit2D hit = Physics2D.Raycast(transform.position, rayDir, rayDistance, groundMask);
        if (hit.collider != null)
        {
            TriggerExplosion(hit.point);
        }
    }

    private void UpdateRotation()
    {
        if (velocity.sqrMagnitude <= 0.0001f)
            return;

        float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle + angleOffset);
    }

    private CharacterBase ResolveCharacter(Collider2D col)
    {
        if (col == null)
            return null;

        CharacterBase target = col.GetComponent<CharacterBase>();
        if (target != null)
            return target;

        target = col.GetComponentInParent<CharacterBase>();
        if (target != null)
            return target;

        if (col.transform.root != null)
            return col.transform.root.GetComponent<CharacterBase>();

        return null;
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (exploded || armTimer < armDelay || col == null)
            return;

        if (owner != null && col.transform.root == owner.transform.root)
            return;

        CharacterBase enemy = ResolveCharacter(col);
        if (enemy != null)
        {
            if (owner != null && enemy == owner)
                return;

            TriggerExplosion(transform.position);
            return;
        }

        int otherLayerMask = 1 << col.gameObject.layer;
        if ((groundMask.value & otherLayerMask) != 0)
        {
            TriggerExplosion(transform.position);
        }
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (exploded || armTimer < armDelay || col == null || col.collider == null)
            return;

        if (owner != null && col.collider.transform.root == owner.transform.root)
            return;

        CharacterBase enemy = ResolveCharacter(col.collider);
        if (enemy != null)
        {
            if (owner != null && enemy == owner)
                return;

            TriggerExplosion(transform.position);
            return;
        }

        int otherLayerMask = 1 << col.collider.gameObject.layer;
        if ((groundMask.value & otherLayerMask) != 0)
        {
            TriggerExplosion(transform.position);
        }
    }

    private void TriggerExplosion(Vector2 point)
    {
        if (exploded)
            return;

        exploded = true;

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