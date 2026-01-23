using System.Collections;
using UnityEngine;

public class Enemy_SkillBase : MonoBehaviour
{
    public enum SkillKind
    {
        SlashCombo,
        Whirlwind,
        ChargedStrike,
        Riposte
    }

    [Header("Identity")]
    public SkillKind kind = SkillKind.SlashCombo;

    [Header("BT / Range")]
    public float skillRange = 1.8f;
    public float rangeTolerance = 0.15f;

    [Header("Timing")]
    public float windup = 0.15f;
    public float activeTime = 0.10f;
    public float recovery = 0.25f;

    [Header("Anti-spam")]
    public float cooldown = 0.40f;

    [Header("Damage")]
    public LayerMask hitMask;
    public float damageMultiplier = 1f;

    // ====== Sword: cone (slash/charged/riposte counter) ======
    [Header("Cone Hit (Slash/Charged/Riposte)")]
    public float coneRadius = 2.0f;
    public float coneAngle = 90f;
    public Vector2 coneOffset = new Vector2(1.1f, 0f);

    // ====== Sword: whirlwind AoE ======
    [Header("Whirlwind AoE")]
    public float aoeRadius = 2.0f;
    public float aoeTickInterval = 0.25f;

    // ====== Sword: riposte stance ======
    [Header("Riposte")]
    public float stanceDuration = 0.45f;

    // ===== runtime =====
    public bool IsActive => isActive;
    private bool isActive = false;

    private float nextReadyTime = 0f;

    private EnemyAI ai;
    private EnemyCombatController combat;
    private CharacterBase selfStats;

    private void Awake()
    {
        // Penting: skill biasanya ada di child; ambil referensi dari parent/root. :contentReference[oaicite:1]{index=1}
        ai = GetComponentInParent<EnemyAI>();
        combat = GetComponentInParent<EnemyCombatController>();
        selfStats = GetComponentInParent<CharacterBase>();
    }

    public bool CanTrigger(float distanceToPlayer)
    {
        if (isActive) return false;
        if (Time.time < nextReadyTime) return false;
        if (combat != null && combat.IsBusy) return false;
        return distanceToPlayer <= skillRange + rangeTolerance;
    }

    public void Trigger()
    {
        if (isActive) return;
        if (Time.time < nextReadyTime) return;

        // Catatan: StartCoroutine berpotensi “stack” jika dipanggil berulang tanpa guard. :contentReference[oaicite:2]{index=2}
        StartCoroutine(SkillRoutine());
    }

    private IEnumerator SkillRoutine()
    {
        isActive = true;
        nextReadyTime = Time.time + cooldown;

        combat?.InvokeSkillStart();

        try
        {
            // WINDUP
            OnWindupStart();
            yield return new WaitForSeconds(windup);

            // ACTIVE
            OnActiveStart();
            yield return DoActivePhase();

            // tahan fase aktif bila diperlukan (mis. whirlwind tick)
            yield return new WaitForSeconds(activeTime);

            // RECOVERY
            OnRecoveryStart();
            yield return new WaitForSeconds(recovery);
        }
        finally
        {
            combat?.InvokeSkillEnd();
            isActive = false;
        }
    }

    // =======================
    // Hooks animasi (opsional)
    // =======================
    private void OnWindupStart()
    {
        if (ai == null || ai.Animation == null) return;

        if (kind == SkillKind.ChargedStrike)
            ai.Animation.SetCharging(true);

        if (kind == SkillKind.Riposte)
            ai.Animation.SetRiposteReady(true);
    }

    private void OnActiveStart()
    {
        if (ai == null || ai.Animation == null) return;

        switch (kind)
        {
            case SkillKind.SlashCombo:
                ai.Animation.PlaySlash1();
                break;

            case SkillKind.Whirlwind:
                ai.Animation.PlayWhirlwind();
                break;

            case SkillKind.ChargedStrike:
                ai.Animation.SetCharging(false);
                ai.Animation.PlaySlash2();
                break;

            case SkillKind.Riposte:
                ai.Animation.SetRiposteReady(false);
                ai.Animation.TriggerRiposteCounter();
                break;
        }
    }

    private void OnRecoveryStart()
    {
        if (ai == null || ai.Animation == null) return;

        // pastikan flag tidak tertinggal
        ai.Animation.SetCharging(false);
        ai.Animation.SetRiposteReady(false);
    }

    // =======================
    // Implementasi damage
    // =======================
    private IEnumerator DoActivePhase()
    {
        switch (kind)
        {
            case SkillKind.Whirlwind:
                yield return DoWhirlwindTicks();
                yield break;

            case SkillKind.Riposte:
                // stance dulu, lalu counter
                yield return new WaitForSeconds(stanceDuration);
                PerformConeHit(coneRadius, 120f, coneOffset, damageMultiplier);
                yield break;

            case SkillKind.ChargedStrike:
                PerformConeHit(coneRadius, 120f, coneOffset, damageMultiplier);
                yield break;

            case SkillKind.SlashCombo:
            default:
                PerformConeHit(coneRadius, coneAngle, coneOffset, damageMultiplier);
                yield break;
        }
    }

    private IEnumerator DoWhirlwindTicks()
    {
        float elapsed = 0f;
        float tick = Mathf.Max(0.05f, aoeTickInterval);

        while (elapsed < activeTime)
        {
            PerformAoeHit(aoeRadius, damageMultiplier);
            yield return new WaitForSeconds(tick);
            elapsed += tick;
        }
    }

    private void PerformAoeHit(float radius, float dmgMul)
    {
        if (ai == null) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(ai.transform.position, radius, hitMask);
        for (int i = 0; i < hits.Length; i++)
        {
            var cb = hits[i].GetComponentInParent<CharacterBase>();
            if (cb == null || cb == selfStats) continue;

            cb.TakeDamage(ai.AttackPower * dmgMul, ai.gameObject);
        }
    }

    private void PerformConeHit(float radius, float angleDeg, Vector2 offset, float dmgMul)
    {
        if (ai == null) return;

        Vector3 off = offset;
        if (!ai.IsFacingRight) off.x *= -1f;

        Vector3 origin = ai.transform.position + off;
        Vector2 dir = ai.IsFacingRight ? Vector2.right : Vector2.left;

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, radius, hitMask);
        for (int i = 0; i < hits.Length; i++)
        {
            var cb = hits[i].GetComponentInParent<CharacterBase>();
            if (cb == null || cb == selfStats) continue;

            Vector2 toTarget = (Vector2)(cb.transform.position - origin);
            float ang = Vector2.Angle(dir, toTarget);

            if (ang <= angleDeg * 0.5f)
                cb.TakeDamage(ai.AttackPower * dmgMul, ai.gameObject);
        }
    }
}
