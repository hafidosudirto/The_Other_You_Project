using UnityEngine;
using System.Collections;

public class Enemy_Sword_SlashCombo : MonoBehaviour
{
    [Header("Attack Settings")]
    public float attackRadius = 2f;

    [Tooltip("Sudut serang untuk perhitungan damage (derajat).")]
    public float attackAngle = 90f;

    public Vector2 hitOffset = new Vector2(1.1f, 0f);

    [Header("Combo 2")]
    [Range(0f, 1f)] public float chainChance = 0.60f;
    public float chainExtraDelay = 0.12f;

    [Header("Close-Contact Fix (agar tetap kena saat menempel)")]
    public bool centerAtPivotWhenClose = true;
    public float pivotCenterDistance = 0.55f;
    [Range(0f, 1f)] public float closeOffsetMultiplier = 0.15f;

    [Header("Timings")]
    public float windupTime = 0.15f;
    public float activeTime = 0.05f;
    public float recoveryTime = 0.30f;

    [Header("Anti-spam")]
    public float cooldown = 0.25f;

    [Header("Mask")]
    public LayerMask hitMask;

    [Header("Gizmos (seperti player)")]
    public float gizmoShowTime = 0.06f;
    public Color gizmoColor = Color.yellow;
    public int arcSegments = 20;

    [Tooltip("Sudut gizmo (derajat)")]
    public float gizmoArcAngle = 60f;

    private EnemyAI ai;
    private EnemyCombatController combat;
    private EnemyMovementFSM movementFSM;
    private CharacterBase selfStats;

    private bool busy = false;
    private float nextReadyTime = 0f;

    private Coroutine activeRoutine;
    private bool skillStartInvoked = false;
    private bool movementLockedByThisSkill = false;

    private bool showHitArc = false;

    private Vector3 lastOrigin;
    private Vector3 lastDir;
    private float lastRadius;
    private float lastAngleForGizmo;

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
        showHitArc = false;
    }

    public void Trigger()
    {
        if (busy) return;
        if (Time.time < nextReadyTime) return;

        activeRoutine = StartCoroutine(ComboRoutine());
    }

    private IEnumerator ComboRoutine()
    {
        busy = true;
        nextReadyTime = Time.time + cooldown;

        BeginSkillState(GetEstimatedLockDuration());

        ai?.Animation?.PlaySlash1();
        yield return new WaitForSeconds(windupTime);

        PerformSlash(ai != null ? ai.AttackPower : 10f);
        yield return new WaitForSeconds(activeTime);

        bool canChain = false;
        if (ai != null && ai.playerTransform != null)
        {
            float dist = Vector2.Distance(ai.transform.position, ai.playerTransform.position);
            canChain = (dist <= attackRadius) && (Random.value <= chainChance);
        }

        if (canChain)
        {
            yield return new WaitForSeconds(chainExtraDelay);

            ai?.Animation?.PlaySlash2();
            yield return new WaitForSeconds(windupTime * 0.85f);

            PerformSlash((ai != null ? ai.AttackPower : 10f) * 1.05f);
            yield return new WaitForSeconds(activeTime);
        }

        yield return new WaitForSeconds(recoveryTime);

        ForceEndSkillState();
        activeRoutine = null;
    }

    private float GetEstimatedLockDuration()
    {
        float hit1Duration = windupTime + activeTime;
        float possibleHit2Duration = chainExtraDelay + (windupTime * 0.85f) + activeTime;
        return Mathf.Max(0.05f, hit1Duration + possibleHit2Duration + recoveryTime + 0.1f);
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

    private void PerformSlash(float damage)
    {
        if (ai == null) return;

        int sign = ai.ForwardSign;
        Vector3 dir = ai.ForwardDir;

        float mul = 1f;
        if (centerAtPivotWhenClose && ai.playerTransform != null)
        {
            float dist = Vector2.Distance(ai.transform.position, ai.playerTransform.position);
            if (dist <= pivotCenterDistance)
                mul = Mathf.Clamp01(closeOffsetMultiplier);
        }

        Vector3 origin = ai.transform.position + new Vector3(hitOffset.x * sign * mul, hitOffset.y, 0f);

        lastOrigin = origin;
        lastDir = dir;
        lastRadius = attackRadius;
        lastAngleForGizmo = gizmoArcAngle;

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, attackRadius, hitMask);

        foreach (var h in hits)
        {
            CharacterBase cb = h.GetComponentInParent<CharacterBase>();
            if (!cb || cb == selfStats) continue;

            Vector2 toTarget = cb.transform.position - origin;
            float angle = Vector2.Angle(dir, toTarget);

            if (angle <= attackAngle * 0.5f)
                cb.TakeDamage(damage, ai.gameObject);
        }

        if (gameObject.activeInHierarchy)
            StartCoroutine(ShowHitArcWindow());
    }

    private IEnumerator ShowHitArcWindow()
    {
        showHitArc = true;
        yield return new WaitForSeconds(gizmoShowTime);
        showHitArc = false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        if (!showHitArc) return;

        Gizmos.color = gizmoColor;

        float startAngle = -lastAngleForGizmo * 0.5f;
        float step = lastAngleForGizmo / Mathf.Max(1, arcSegments);

        Vector3 prev = lastOrigin + Quaternion.Euler(0, 0, startAngle) * lastDir * lastRadius;

        for (int i = 1; i <= arcSegments; i++)
        {
            float ang = startAngle + step * i;
            Vector3 next = lastOrigin + Quaternion.Euler(0, 0, ang) * lastDir * lastRadius;
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
#endif
}