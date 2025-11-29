using UnityEngine;
using System.Collections;

public class Enemy_Sword_SlashCombo : MonoBehaviour
{
    [Header("Attack Settings")]
    public float attackRadius = 2f;                
    public float attackAngle = 90f;
    public Vector2 hitOffset = new Vector2(1.1f, 0f);   

    [Header("Timings")]
    public float windupTime = 0.15f;
    public float activeTime = 0.05f;
    public float recoveryTime = 0.30f;

    [Header("Mask")]
    public LayerMask hitMask;

    private EnemyAI ai;
    private EnemyCombatController combat;
    private CharacterBase selfStats;
    private bool busy = false;

    private void Awake()
    {
        ai = GetComponentInParent<EnemyAI>();
        combat = GetComponentInParent<EnemyCombatController>();
        selfStats = GetComponentInParent<CharacterBase>();
    }

    public void Trigger()
    {
        if (!busy)
            StartCoroutine(SlashRoutine());
    }

    private IEnumerator SlashRoutine()
    {
        busy = true;

        combat.InvokeSkillStart();

        if (ai.Animation != null)
            ai.Animation.PlaySlash1();

        yield return new WaitForSeconds(windupTime);

        PerformSlash();

        yield return new WaitForSeconds(activeTime);
        yield return new WaitForSeconds(recoveryTime);

        combat.InvokeSkillEnd();

        busy = false;
    }

    private void PerformSlash()
    {
        Vector3 offset = hitOffset;

        if (!ai.IsFacingRight)
            offset.x *= -1;

        Vector3 origin = ai.transform.position + (Vector3)offset;
        Vector3 dir = ai.IsFacingRight ? Vector3.right : Vector3.left;

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, attackRadius, hitMask);

        foreach (var h in hits)
        {
            // FIXED: DETECT CharacterBase on parent
            CharacterBase cb = h.GetComponentInParent<CharacterBase>();

            if (!cb || cb == selfStats) continue;

            Vector2 toTarget = cb.transform.position - origin;
            float angle = Vector2.Angle(dir, toTarget);

            if (angle <= attackAngle * 0.5f)
                cb.TakeDamage(ai.AttackPower, ai.gameObject);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!ai) return;

        Gizmos.color = Color.yellow;

        Vector3 offset = hitOffset;
        if (ai != null && !ai.IsFacingRight) offset.x *= -1;

        Gizmos.DrawWireSphere(ai.transform.position + offset, attackRadius);
    }
}
