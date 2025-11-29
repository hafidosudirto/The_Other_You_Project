using UnityEngine;
using System.Collections;

public class Enemy_Sword_ChargedStrike : MonoBehaviour
{
    public float attackRadius = 1.6f;
    public float attackAngle = 120f;
    public float damageMultiplier = 2f;

    public float windupTime = 0.5f;
    public float activeTime = 0.1f;
    public float recoveryTime = 0.5f;

    public LayerMask hitMask;

    private EnemyAI ai;
    private EnemyCombatController combat;
    private CharacterBase selfStats;

    private void Awake()
    {
        ai = GetComponentInParent<EnemyAI>();
        combat = GetComponentInParent<EnemyCombatController>();
        selfStats = GetComponentInParent<CharacterBase>();
    }

    public void Trigger()
    {
        StartCoroutine(ChargeRoutine());
    }

    private IEnumerator ChargeRoutine()
    {
        combat.InvokeSkillStart();

        yield return new WaitForSeconds(windupTime);

        PerformAttack();
        yield return new WaitForSeconds(activeTime);

        yield return new WaitForSeconds(recoveryTime);
        combat.InvokeSkillEnd();
    }

    private void PerformAttack()
    {
        Vector3 origin = ai.transform.position;
        Vector3 dir = ai.IsFacingRight ? Vector3.right : Vector3.left;

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, attackRadius, hitMask);

        foreach (var h in hits)
        {
            CharacterBase cb = h.GetComponent<CharacterBase>();
            if (!cb || cb == selfStats) continue;

            float angle = Vector2.Angle(dir, (cb.transform.position - origin));
            if (angle <= attackAngle * 0.5f)
                cb.TakeDamage(ai.AttackPower * damageMultiplier, ai.gameObject);
        }
    }
}
