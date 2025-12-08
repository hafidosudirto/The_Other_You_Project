using UnityEngine;
using System.Collections;

public class Enemy_Sword_Whirlwind : MonoBehaviour
{
    public float radius = 2.0f;
    public float duration = 1.5f;
    public float damageMultiplier = 1.25f;

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
        StartCoroutine(WhirlwindRoutine());
    }

    private IEnumerator WhirlwindRoutine()
    {
        combat.InvokeSkillStart();

        float time = duration;
        while (time > 0)
        {
            time -= Time.deltaTime;
            PerformDamage();
            yield return new WaitForSeconds(0.25f);
        }

        combat.InvokeSkillEnd();
    }

    private void PerformDamage()
    {
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(ai.transform.position, radius, hitMask);

        foreach (var h in hits)
        {
            CharacterBase cb = h.GetComponent<CharacterBase>();
            if (!cb || cb == selfStats) continue;

            cb.TakeDamage(ai.AttackPower * damageMultiplier, ai.gameObject);
        }
    }

    private void OnDrawGizmos()
    {
        if (ai == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(ai.transform.position, radius);
    }
}
