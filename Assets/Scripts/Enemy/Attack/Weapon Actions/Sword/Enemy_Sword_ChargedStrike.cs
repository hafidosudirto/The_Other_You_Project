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
    private EnemyMovementFSM movementFSM;
    private CharacterBase selfStats;

    private bool busy = false;
    private float nextReadyTime = 0f;

    private Coroutine activeRoutine;
    private bool skillStartInvoked = false;
    private bool movementLockedByThisSkill = false;

    private void Awake()
    {
        ai = GetComponentInParent<EnemyAI>();
        combat = GetComponentInParent<EnemyCombatController>();
        movementFSM = GetComponentInParent<EnemyMovementFSM>();
        selfStats = GetComponentInParent<CharacterBase>();
    }

    private void OnDisable()
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        ForceEndSkillState();
    }

    public void Trigger()
    {
        if (busy) return;
        if (Time.time < nextReadyTime) return;

        activeRoutine = StartCoroutine(ChargeRoutine());
    }

    private IEnumerator ChargeRoutine()
    {
        busy = true;
        nextReadyTime = Time.time + cooldown;

        BeginSkillState(GetEstimatedLockDuration());

        ai?.Animation?.SetCharging(true);

        yield return new WaitForSeconds(windupTime);

        ai?.Animation?.SetCharging(false);
        ai?.Animation?.PlaySlash2();

        PerformAttack();

        yield return new WaitForSeconds(activeTime);
        yield return new WaitForSeconds(recoveryTime);

        ForceEndSkillState();
        activeRoutine = null;
    }

    private float GetEstimatedLockDuration()
    {
        return Mathf.Max(0.05f, windupTime + activeTime + recoveryTime + 0.1f);
    }

    private void BeginSkillState(float lockDuration)
    {
        movementLockedByThisSkill = false;
        skillStartInvoked = false;

        if (movementFSM != null)
        {
            movementFSM.LockExternal(lockDuration, true);
            movementLockedByThisSkill = true;
        }

        combat?.InvokeSkillStart();
        skillStartInvoked = true;
    }

    private void ForceEndSkillState()
    {
        ai?.Animation?.SetCharging(false);

        if (skillStartInvoked)
        {
            combat?.InvokeSkillEnd();
            skillStartInvoked = false;
        }

        if (movementLockedByThisSkill)
        {
            movementFSM?.UnlockExternal(true);
            movementLockedByThisSkill = false;
        }

        busy = false;
    }

    private void PerformAttack()
    {
        if (ai == null) return;

        int sign = ai.ForwardSign;
        Vector3 dir = ai.ForwardDir;

        Vector3 origin = ai.transform.position + new Vector3(hitOffset.x * sign, hitOffset.y, 0f);

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, attackRadius, hitMask);

        foreach (var h in hits)
        {
            CharacterBase cb = h.GetComponentInParent<CharacterBase>();
            if (!cb || cb == selfStats) continue;

            Vector2 toTarget = cb.transform.position - origin;
            float angle = Vector2.Angle(dir, toTarget);

            if (angle > attackAngle * 0.5f) continue;

            float dmg = ai.AttackPower * damageMultiplier;

            cb.TakeDamage(dmg, ai.gameObject);

            if (!cb.isRiposteStance)
            {
                Vector2 knockDir = toTarget.normalized;
                cb.ApplyStagger(knockDir, knockbackForce, stunDuration);
            }
        }
    }
}