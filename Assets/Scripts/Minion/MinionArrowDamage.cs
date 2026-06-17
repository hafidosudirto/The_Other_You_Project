using UnityEngine;

/// <summary>
/// Damage handler KHUSUS untuk Minion Projectile (panah Minion Range).
///
/// Aturan hit:
///   - Hanya target yang ber-tag "Player" ATAU berada di Layer Player (default 6) yang dihitung hit.
///   - Musuh lain (Minion melee, Boss, dll) diabaikan total — panah TIDAK friendly-fire ke mereka.
///   - Saat mengenai Player, panggil TakeDamage lalu Destroy projectile (kecuali piercing).
///
/// Berlaku sebagai komponen terpisah dari ArrowDamage (yang dipakai boss/player)
/// agar logika filter Minion vs Boss tidak tercampur.
/// </summary>
[DisallowMultipleComponent]
public class MinionArrowDamage : MonoBehaviour
{
    [Header("Arrow Behaviour")]
    [Tooltip("Hancurkan projectile setelah mengenai Player.")]
    public bool destroyOnHit = true;

    [Tooltip("Damage base. Dikirim oleh MinionRange_Projectile saat spawn.")]
    public float baseDamage = 5f;

    [Tooltip("Pemilik panah (Minion). Tidak boleh null saat runtime.")]
    public CharacterBase owner;

    [Header("Optional Effects")]
    public float knockbackForce = 0f;
    public float stunDuration = 0f;

    [Header("Debug")]
    public bool showDebug = true;

    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void SetOwner(CharacterBase ownerCharacter)
    {
        owner = ownerCharacter;
    }

    public void SetStats(float damage, float knockback = 0f, float stun = 0f)
    {
        baseDamage = damage;
        knockbackForce = knockback;
        stunDuration = stun;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleHit(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleHit(collision.collider);
    }

    private void HandleHit(Collider2D other)
    {
        if (other == null)
            return;

        // Abaikan diri sendiri & owner (jika somehow projectile menyentuh parent).
        if (owner != null && other.gameObject == owner.gameObject)
            return;

        if (other.gameObject == gameObject)
            return;

        // FILTER UTAMA: hanya Player yang boleh dihitung sebagai hit.
        // Musuh lain (Minion melee, dll) diabaikan total — panah TIDAK friendly-fire.
        if (!IsPlayerTarget(other))
        {
            if (showDebug)
            {
                Debug.Log(
                    $"[MinionArrowDamage] {transform.root.name} panah melewati " +
                    $"'{other.name}' (layer={LayerMask.LayerToName(other.gameObject.layer)}, " +
                    $"tag='{other.tag}') — bukan Player.",
                    this
                );
            }
            return;
        }

        CharacterBase target = other.GetComponent<CharacterBase>();
        if (target == null)
            target = other.GetComponentInParent<CharacterBase>();

        if (target == null)
            return;

        Vector2 hitPoint = other.ClosestPoint(transform.position);
        ApplyDamageAndEffects(target, hitPoint);

        if (destroyOnHit)
            Destroy(gameObject);
    }

    /// <summary>
    /// True jika collider adalah Player: tag == "Player" atau layer == Player (default 6).
    /// </summary>
    private bool IsPlayerTarget(Collider2D other)
    {
        if (other.CompareTag("Player"))
            return true;

        // Fallback: cek layer Player (default layer 6 = "Player" di TagManager standar Unity).
        // Tag adalah sumber utama; layer sebagai cadangan untuk object yang tidak di-tag.
        if (other.gameObject.layer == LayerMask.NameToLayer("Player"))
            return true;

        return false;
    }

    private void ApplyDamageAndEffects(CharacterBase target, Vector2 hitPoint)
    {
        if (baseDamage > 0f)
        {
            GameObject source = owner != null ? owner.gameObject : gameObject;
            target.TakeDamage(baseDamage, source);

            if (showDebug)
            {
                Debug.Log(
                    $"<color=red>[MINION ARROW HIT]</color> {transform.root.name} panah " +
                    $"mengenai Player '{target.name}' — damage {baseDamage}.",
                    this
                );
            }
        }

        // Knockback horizontal mengikuti arah terbang panah.
        if (knockbackForce > 0f)
        {
            float dirX = 0f;
            if (rb != null && Mathf.Abs(rb.velocity.x) > 0.0001f)
                dirX = Mathf.Sign(rb.velocity.x);
            else if (owner != null)
                dirX = owner.isFacingRight ? 1f : -1f;
            else
                dirX = target.transform.position.x >= transform.position.x ? 1f : -1f;

            target.ApplyKnockback(new Vector2(dirX, 0f), knockbackForce);
        }

        if (stunDuration > 0f)
            target.ApplyStun(stunDuration);
    }
}