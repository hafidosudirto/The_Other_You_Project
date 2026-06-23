using UnityEngine;

public class ArrowDamage : MonoBehaviour
{
    [Header("Arrow Behaviour")]
    [Tooltip("Kalau aktif, panah hilang setelah kena target. Kalau false, panah bisa tetap lanjut.")]
    public bool destroyOnHit = true;

    [Tooltip("Kalau aktif, panah bisa tembus target dan tidak langsung hilang saat kena musuh.")]
    public bool piercing = false;

    [Tooltip("Kalau aktif, panah membawa efek ledak area. Biasanya dipakai untuk varian khusus, bukan Quick Shot biasa.")]
    public bool concussive = false;

    [Tooltip("Radius ledak untuk panah tipe concussive. Kalau Anda ingin ubah ledakan skill, cek Bow_ConcussiveShot.cs atau ConcussiveHitArea.cs.")]
    public float explosionRadius = 1.5f;

    [Header("Debug / Owner")]
    [Tooltip("Pemilik panah ini. Biasanya diisi otomatis oleh skill saat panah dibuat.")]
    [SerializeField] public CharacterBase owner;

    [Header("Hidden Runtime Stats")]
    [HideInInspector] public float baseDamage = 10f;
    [HideInInspector] public float knockbackForce = 0f;
    [HideInInspector] public float stunDuration = 0f;

    // Stagger config dari skill — dikirim via SetStaggerConfig().
    // Jika null, pakai knockback lama tanpa immunity.
    [HideInInspector] public SkillStaggerConfig staggerConfig;

    private Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void SetOwner(CharacterBase ownerCharacter)
    {
        owner = ownerCharacter;
    }

    // Dipanggil dari skill Bow:
    // - Bow_QuickShot.cs
    // - Bow_FullDraw.cs
    // - Bow_PiercingShot.cs
    // dll
    public void SetStats(
        float damage,
        float knockback,
        float stun,
        bool isPiercing,
        bool isConcussive = false
    )
    {
        baseDamage = damage;
        knockbackForce = knockback;
        stunDuration = stun;
        piercing = isPiercing;
        concussive = isConcussive;
    }

    public void SetStaggerConfig(SkillStaggerConfig config)
    {
        staggerConfig = config;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        CharacterBase target = other.GetComponent<CharacterBase>();
        if (target == null)
            return;

        if (owner != null && other.gameObject == owner.gameObject)
            return;

        Vector2 hitPoint = other.ClosestPoint(transform.position);
        ApplyEffectsToTarget(target, hitPoint, applyDamage: true);

        // Telemetry: jika panah ini milik musuh dan mengenai target (player), tandai cast musuh sebagai hit.
        if (owner is Enemy && target is Player && TelemetryLogger.Instance != null)
            TelemetryLogger.Instance.SetLastEnemySkillCastHit(true);

        if (concussive && explosionRadius > 0f)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
            foreach (var h in hits)
            {
                if (h == other) continue;

                CharacterBase extraTarget = h.GetComponent<CharacterBase>();
                if (extraTarget == null || extraTarget == target)
                    continue;

                Vector2 extraPoint = h.ClosestPoint(transform.position);
                ApplyEffectsToTarget(extraTarget, extraPoint, applyDamage: false);
            }
        }

        if (!piercing && destroyOnHit)
        {
            Destroy(gameObject);
        }
    }

    private void ApplyEffectsToTarget(CharacterBase target, Vector2 hitPoint, bool applyDamage)
    {
        if (applyDamage && baseDamage > 0f)
        {
            GameObject source = owner != null ? owner.gameObject : gameObject;
            target.TakeDamage(baseDamage, source);
        }

        // Hitung arah knockback/stagger (selalu horizontal)
        float arahX = 0f;
        if (rb != null && Mathf.Abs(rb.velocity.x) > 0.0001f)
            arahX = Mathf.Sign(rb.velocity.x);
        else if (owner != null)
            arahX = owner.isFacingRight ? 1f : -1f;
        else
            arahX = target.transform.position.x >= transform.position.x ? 1f : -1f;

        Vector2 arahDorong = new Vector2(arahX, 0f);

        if (staggerConfig != null && staggerConfig.aktif)
        {
            // Pakai stagger config dari skill — immunity check + durasi khusus per skill
            staggerConfig.Apply(target, arahDorong);
        }
        else if (knockbackForce > 0f)
        {
            // Fallback: knockback lama (tanpa immunity, durasi 0.15s)
            target.ApplyKnockback(arahDorong, knockbackForce);
        }

        if (stunDuration > 0f)
            target.ApplyStun(stunDuration);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (concussive && explosionRadius > 0f)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
#endif
}