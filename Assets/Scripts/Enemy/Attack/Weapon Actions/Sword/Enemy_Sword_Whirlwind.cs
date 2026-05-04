using UnityEngine;
using System.Collections;

public class Enemy_Sword_Whirlwind : MonoBehaviour
{
    public float radius = 2.0f;
    public float duration = 1.5f;
    public float damageMultiplier = 1.25f;
    public float tickInterval = 0.25f;

    [Header("Anti-spam")]
    public float cooldown = 0.8f;

    public LayerMask hitMask;

    [Header("Gizmos")]
    public Color gizmoColor = Color.cyan;

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

        activeRoutine = StartCoroutine(WhirlwindRoutine());
    }

    private IEnumerator WhirlwindRoutine()
    {
        busy = true;
        nextReadyTime = Time.time + cooldown;

        BeginSkillState(GetEstimatedLockDuration());

        ai?.Animation?.PlayWhirlwind();

        float elapsed = 0f;
        float tick = Mathf.Max(0.05f, tickInterval);

        while (elapsed < duration)
        {
            PerformDamageTick();
            yield return new WaitForSeconds(tick);
            elapsed += tick;
        }

        ForceEndSkillState();
        activeRoutine = null;
    }

    private float GetEstimatedLockDuration()
    {
        return Mathf.Max(0.05f, duration + 0.1f);
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

    private void PerformDamageTick()
    {
        if (ai == null) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(ai.transform.position, radius, hitMask);

        foreach (var h in hits)
        {
            CharacterBase cb = h.GetComponentInParent<CharacterBase>();
            if (!cb || cb == selfStats) continue;

            cb.TakeDamage(ai.AttackPower * damageMultiplier, ai.gameObject);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        if (!busy) return;

        if (ai == null) ai = GetComponentInParent<EnemyAI>();
        if (ai == null) return;

        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(ai.transform.position, radius);
    }
#endif
}