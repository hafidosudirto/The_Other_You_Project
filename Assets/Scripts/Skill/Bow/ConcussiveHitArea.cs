using UnityEngine;

public class ConcussiveHitArea : MonoBehaviour
{
    [Header("Stats")]
    public float damage = 10f;
    public float knockback = 6f;
    public float stun = 0.35f;
    public float radius = 1.4f;

    [Header("Owner")]
    public CharacterBase owner;

    [Header("Filter")]
    public LayerMask hitMask = ~0;   // set di prefab ke Enemy

    [Header("Lifetime")]
    [Tooltip("Berapa lama area ini hidup setelah aktif.")]
    public float lifeTime = 0.15f;

    bool hasTriggered;

    // Dipanggil dari skill untuk setup value
    public void Setup(CharacterBase ownerCharacter, float dmg, float kb, float st, float rad)
    {
        owner = ownerCharacter;
        damage = dmg;
        knockback = kb;
        stun = st;
        radius = rad;
    }

    void OnEnable()
    {
        TriggerHit();

        if (lifeTime > 0f)
            Destroy(gameObject, lifeTime);
    }

    public void TriggerHit()
    {
        if (hasTriggered) return;
        hasTriggered = true;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius, hitMask);
        foreach (var h in hits)
        {
            CharacterBase target = h.GetComponent<CharacterBase>();
            if (target == null)
                continue;

            // jangan pukul diri sendiri
            if (owner != null && target == owner)
                continue;

            Vector2 hitPoint = h.ClosestPoint(transform.position);
            ApplyEffectsToTarget(target, hitPoint);
        }
    }

    void ApplyEffectsToTarget(CharacterBase target, Vector2 hitPoint)
    {
        // Damage
        if (damage > 0f)
        {
            GameObject source = owner != null ? owner.gameObject : gameObject;
            target.TakeDamage(damage, source);
        }

        // Knockback
        if (knockback > 0f)
        {
            Vector2 dir = ((Vector2)target.transform.position - hitPoint).normalized;
            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector2.up;

            target.ApplyKnockback(dir, knockback);
        }

        // Stun
        if (stun > 0f)
        {
            target.ApplyStun(stun);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
#endif
}
