using UnityEngine;
using System.Collections;

public class Enemy_Sword_ChargedStrike : MonoBehaviour
{
    [Header("Hit Settings")]
    public float attackRadius = 1.6f;
    public float attackAngle = 100f;             
    public float damageMultiplier = 2f;
    public Vector2 hitOffset = new Vector2(1.1f, 0f);

    [Header("Knockback (Stagger)")]
    public float knockbackForce = 8f;           
    public float stunDuration = 0.4f;             

    [Header("Timing")]
    public float windupTime = 0.5f;
    public float activeTime = 0.1f;
    public float recoveryTime = 0.5f;

    [Header("Anti-spam")]
    public float cooldown = 0.75f;

    [Header("Mask")]
    public LayerMask hitMask;

    private EnemyAI ai;
    private EnemyCombatController combat;
    private CharacterBase selfStats;

    private bool busy = false;
    private float nextReadyTime = 0f;

    private void Awake()
    {
        ai = GetComponentInParent<EnemyAI>();
        combat = GetComponentInParent<EnemyCombatController>();
        selfStats = GetComponentInParent<CharacterBase>();
    }

    public void Trigger()
    {
        if (busy) return;
        if (Time.time < nextReadyTime) return;

        StartCoroutine(ChargeRoutine());
    }

    private IEnumerator ChargeRoutine()
    {
        busy = true;
        nextReadyTime = Time.time + cooldown;

        combat?.InvokeSkillStart();

        // Windup anim (agar tidak ada momen "hit terasa jalan tapi animasi tidak")
        ai?.Animation?.SetCharging(true);

        yield return new WaitForSeconds(windupTime);

        // Hit frame: matikan charging lalu tebas
        ai?.Animation?.SetCharging(false);
        ai?.Animation?.PlaySlash2();

        PerformAttack();

        yield return new WaitForSeconds(activeTime);
        yield return new WaitForSeconds(recoveryTime);

        combat?.InvokeSkillEnd();
        busy = false;
    }

    private void PerformAttack()
    {
        if (ai == null) return;

        // Gunakan arah visual final dari EnemyAI (aman terhadap invertFlipX)
        int sign = ai.ForwardSign;
        Vector3 dir = ai.ForwardDir;

        Vector3 origin = ai.transform.position + new Vector3(hitOffset.x * sign, hitOffset.y, 0f);

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, attackRadius, hitMask);

        foreach (var h in hits)
        {
            // Penting: ambil dari parent agar konsisten meski collider ada di child
            CharacterBase cb = h.GetComponentInParent<CharacterBase>();
            if (!cb || cb == selfStats) continue;

            Vector2 toTarget = (cb.transform.position - origin);
            float angle = Vector2.Angle(dir, toTarget);

            if (angle > attackAngle * 0.5f) continue;

            float dmg = ai.AttackPower * damageMultiplier;

            // Damage
            cb.TakeDamage(dmg, ai.gameObject);

            // Knockback + stun
            // Catatan: bila target sedang riposte stance, damage diparry dan idealnya tidak diberi stagger.
            // Ini mencegah kasus "parry tetapi tetap terpental".
            if (!cb.isRiposteStance)
            {
                Vector2 knockDir = toTarget.normalized;
                cb.ApplyStagger(knockDir, knockbackForce, stunDuration);
            }
        }
    }
}
