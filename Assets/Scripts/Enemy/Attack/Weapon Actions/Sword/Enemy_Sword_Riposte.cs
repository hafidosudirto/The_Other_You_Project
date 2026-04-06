using UnityEngine;
using System.Collections;

public class Enemy_Sword_Riposte : MonoBehaviour
{
    [Header("Stance")]
    [Tooltip("Durasi window riposte (stance). Jika player tidak menyerang di window ini, tidak ada counter.")]
    public float stanceDuration = 0.45f;

    [Header("Counter Dash (samakan konsep dengan player)")]
    public float dashDistance = 2.5f;
    public float dashSpeed = 10f;
    public float hitRadius = 0.8f;

    [Tooltip("Layer target yang akan terkena counter (biasanya Player).")]
    public LayerMask targetLayer;

    [Header("Damage Multiplier")]
    public float counterDamageMultiplier = 1.4f;

    [Header("Anti-spam")]
    public float cooldown = 0.9f;

    [Header("Range Gate")]
    [Tooltip("Tambahan toleransi jarak agar riposte tidak terlalu mudah gagal di tepi range.")]
    public float extraRangeTolerance = 0.25f;

    [Header("Gizmos (opsional)")]
    public float gizmoShowTime = 0.06f;
    public Color gizmoColor = Color.magenta;

    private EnemyAI ai;
    private EnemyCombatController combat;
    private CharacterBase selfStats;

    private bool busy = false;
    private float nextReadyTime = 0f;

    // stance & dash state
    private bool isStanceActive = false;
    private bool isDashing = false;
    private float stanceTimer = 0f;

    private Vector3 dashStart;
    private Vector3 dashTarget;

    // Gizmo snapshot untuk lintasan dash
    private bool showDashLine = false;
    private Vector3 lastDashStart;
    private Vector3 lastDashTarget;

    // Gate: counter hanya boleh terjadi jika ada serangan player selama stance.
    private bool playerAttackDetectedDuringStance = false;

    public bool IsStanceActive => isStanceActive;
    public bool IsActive => busy;

    private void Awake()
    {
        ai = GetComponentInParent<EnemyAI>();
        combat = GetComponentInParent<EnemyCombatController>();
        selfStats = GetComponentInParent<CharacterBase>();
    }

    public bool CanTrigger(float distanceToPlayer)
    {
        if (busy) return false;
        if (Time.time < nextReadyTime) return false;

        if (ai == null || ai.playerTransform == null)
            return true;

        float allowedRange = Mathf.Max(0.1f, ai.attackRange + extraRangeTolerance);
        return distanceToPlayer <= allowedRange;
    }

    public bool TryStartRiposte()
    {
        if (busy) return false;
        if (Time.time < nextReadyTime) return false;

        float distanceToPlayer = 0f;
        if (ai != null && ai.playerTransform != null)
            distanceToPlayer = Vector2.Distance(ai.transform.position, ai.playerTransform.position);

        if (!CanTrigger(distanceToPlayer))
            return false;

        StartCoroutine(RiposteRoutine());
        return true;
    }

    // API lama
    public void Trigger()
    {
        TryStartRiposte();
    }

    // Dipanggil dari CharacterBase.TakeDamage ketika enemy sedang stance dan player menyerang
    public void NotifyPlayerAttackAttempt(GameObject attacker = null)
    {
        if (!isStanceActive) return;
        playerAttackDetectedDuringStance = true;
    }

    public void TriggerFollowUpDash()
    {
        if (!isStanceActive) return;
        if (isDashing) return;
        if (ai == null) return;
        if (!playerAttackDetectedDuringStance) return;

        Vector3 dir = Vector3.right;

        if (ai.playerTransform != null)
        {
            float sx = Mathf.Sign(ai.playerTransform.position.x - ai.transform.position.x);
            if (sx == 0f) sx = ai.ForwardSign;
            dir = new Vector3(sx, 0f, 0f);
        }
        else
        {
            dir = ai.ForwardDir;
        }

        dashStart = ai.transform.position;
        dashTarget = dashStart + dir.normalized * dashDistance;

        isDashing = true;
        isStanceActive = false;

        ai?.Animation?.SetRiposteReady(false);
        ai?.Animation?.TriggerRiposteCounter();
    }

    private IEnumerator RiposteRoutine()
    {
        busy = true;
        nextReadyTime = Time.time + cooldown;

        combat?.InvokeSkillStart();

        isStanceActive = true;
        isDashing = false;
        stanceTimer = stanceDuration;
        playerAttackDetectedDuringStance = false;

        ai?.Animation?.SetRiposteReady(true);

        while (stanceTimer > 0f && !isDashing)
        {
            stanceTimer -= Time.deltaTime;

            if (playerAttackDetectedDuringStance)
            {
                TriggerFollowUpDash();
                break;
            }

            yield return null;
        }

        // stance habis tanpa counter
        if (!isDashing)
        {
            ai?.Animation?.SetRiposteReady(false);
            isStanceActive = false;
            playerAttackDetectedDuringStance = false;

            combat?.InvokeSkillEnd();
            busy = false;
            yield break;
        }

        // counter dash
        yield return DashForwardAndDamage();

        isStanceActive = false;
        playerAttackDetectedDuringStance = false;

        combat?.InvokeSkillEnd();
        busy = false;
    }

    private IEnumerator DashForwardAndDamage()
    {
        if (ai == null)
        {
            isDashing = false;
            yield break;
        }

        lastDashStart = dashStart;
        lastDashTarget = dashTarget;

        while (Vector3.Distance(ai.transform.position, dashTarget) > 0.05f)
        {
            ai.transform.position = Vector3.MoveTowards(
                ai.transform.position,
                dashTarget,
                dashSpeed * Time.deltaTime
            );
            yield return null;
        }

        isDashing = false;

        PerformFollowUpDamage(dashStart, dashTarget);

        if (gameObject.activeInHierarchy)
            StartCoroutine(ShowDashLineWindow());
    }

    private void PerformFollowUpDamage(Vector3 start, Vector3 end)
    {
        Vector2 s = start;
        Vector2 e = end;

        Vector2 dir = (e - s);
        float dist = dir.magnitude;
        if (dist <= 0.0001f) return;

        dir /= dist;

        RaycastHit2D[] hits = Physics2D.CircleCastAll(
            s,
            hitRadius,
            dir,
            dist,
            targetLayer
        );

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            CharacterBase cb = h.collider.GetComponentInParent<CharacterBase>();
            if (cb == null || cb == selfStats) continue;

            float baseAtk = (combat != null) ? combat.AttackPower : (selfStats != null ? selfStats.attack : 10f);
            cb.TakeDamage(baseAtk * counterDamageMultiplier, ai != null ? ai.gameObject : gameObject);
        }
    }

    private IEnumerator ShowDashLineWindow()
    {
        showDashLine = true;
        yield return new WaitForSeconds(gizmoShowTime);
        showDashLine = false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        if (!showDashLine) return;

        Gizmos.color = gizmoColor;
        Gizmos.DrawLine(lastDashStart, lastDashTarget);
        Gizmos.DrawWireSphere(lastDashTarget, hitRadius);
    }
#endif
}