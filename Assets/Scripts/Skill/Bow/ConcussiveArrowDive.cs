using UnityEngine;

public class ConcussiveArrowDive : MonoBehaviour
{
    [Header("Flight")]
    [SerializeField] private float speed = 8f;
    [SerializeField] private float gravity = 18f;
    [SerializeField] private float lifeTime = 2f;

    [Header("Rotation")]
    [SerializeField] private float angleOffset = 0f;

    [Header("Impact")]
    [SerializeField] private GameObject hitAreaPrefab;
    [SerializeField] private LayerMask groundMask;

    private Vector2 velocity;
    private bool initialized = false;
    private bool hasImpacted = false;
    private float timer = 0f;

    // dirX biasanya -1 atau 1
    // initialUpwardVelocity memberi efek "naik dulu lalu nukik"
    public void Init(int dirX, float startSpeed, float initialUpwardVelocity, float customGravity)
    {
        speed = startSpeed;
        gravity = customGravity;

        velocity = new Vector2(dirX * speed, initialUpwardVelocity);
        initialized = true;

        UpdateRotation();
    }

    private void Update()
    {
        if (!initialized || hasImpacted)
            return;

        float dt = Time.deltaTime;
        timer += dt;

        if (timer >= lifeTime)
        {
            Destroy(gameObject);
            return;
        }

        // gerak parabola sederhana
        velocity.y -= gravity * dt;
        transform.position += (Vector3)(velocity * dt);

        UpdateRotation();

        // cek tanah dengan ray pendek ke arah bawah
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 0.08f, groundMask);
        if (hit.collider != null)
        {
            Impact(hit.point);
        }
    }

    private void UpdateRotation()
    {
        if (velocity.sqrMagnitude <= 0.0001f)
            return;

        float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle + angleOffset);
    }

    private void Impact(Vector2 point)
    {
        if (hasImpacted)
            return;

        hasImpacted = true;

        if (hitAreaPrefab != null)
        {
            Instantiate(hitAreaPrefab, point, Quaternion.identity);
        }

        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasImpacted)
            return;

        // bila ingin langsung meledak saat menyentuh enemy / obstacle
        if (other.CompareTag("Enemy"))
        {
            Impact(transform.position);
        }
    }
}