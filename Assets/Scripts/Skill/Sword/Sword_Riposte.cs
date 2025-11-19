using UnityEngine;
using System.Collections;

public class Sword_Riposte : MonoBehaviour, ISkill
{
    private CharacterBase character;
    private SkillBase skillBase;

    [Header("Riposte Skill Settings")]
    public KeyCode activateKey = KeyCode.Alpha4;
    public float stanceDuration = 0.6f;
    public float cooldownTime = 1.0f;

    private bool isOnCooldown = false;
    private bool isActive = false;
    private float stanceTimer = 0f;

    [Header("Follow-Up Attack (FUA) Settings")]
    public float dashDistance = 2.5f;
    public float dashSpeed = 10f;
    public float hitRadius = 0.8f;

    public float dashKnockbackForce = 6f;
    public float dashStunDuration = 0.3f;

    public LayerMask enemyLayer;

    private bool isDashing = false;
    private bool stunApplied = false;

    private Vector3 dashStart;
    private Vector3 dashTarget;

    [Header("Gizmo Settings")]
    public Color activeColor = new Color(0f, 1f, 0.8f, 0.4f);
    public Color dashColor = new Color(0f, 0.5f, 1f, 0.4f);
    public float stanceRadius = 1.2f;
    public int arcSegments = 20;
    public float stanceAngle = 100f;

    void Awake()
    {
        skillBase = GetComponentInParent<SkillBase>();
        character = GetComponentInParent<CharacterBase>();
    }

    void Update()
    {
        if (Input.GetKeyDown(activateKey))
            TryActivateRiposte();

        if (isActive)
        {
            stanceTimer -= Time.deltaTime;
            if (stanceTimer <= 0f)
            {
                isActive = false;
                if (character != null)
                    character.EndRiposteStance();
            }
        }

        if (isDashing)
            DashForward();
    }

    public void TriggerSkill()
    {
        TryActivateRiposte();
    }

    public void TryActivateRiposte()
    {
        if (isOnCooldown) return;
        if (character == null) return;
        if (!character.canRiposte) return;
        if (!character.CanAct()) return;

        // Catat aksi defensif ke DataTracker
        DataTracker.Instance.RecordAction(PlayerActionType.Defensive, WeaponType.Sword);

        character.ActivateRiposte();
        isActive = true;
        stanceTimer = stanceDuration;

        StartCoroutine(StartCooldown());

        Debug.Log($"{gameObject.name} entered Riposte stance.");
    }

    // Dipanggil dari CharacterBase saat Parry sukses
    public void TriggerFollowUpDash()
    {
        if (isDashing) return;

        Vector3 dir = character.isFacingRight ? Vector3.right : Vector3.left;

        dashStart = character.transform.position;
        dashTarget = dashStart + dir * dashDistance;

        isDashing = true;
        stunApplied = false;

        Debug.Log("⚔️ Riposte FUA: Dash started!");
    }

    private void DashForward()
    {
        // LANGSUNG BERIKAN STUN SAAT DASH MULAI
        if (!stunApplied)
        {
            ApplyDashStun();
            stunApplied = true;
        }

        character.transform.position = Vector3.MoveTowards(
            character.transform.position,
            dashTarget,
            dashSpeed * Time.deltaTime
        );

        if (Vector3.Distance(character.transform.position, dashTarget) <= 0.05f)
        {
            isDashing = false;

            // Berikan damage setelah dash selesai
            PerformFollowUpDamage();

            if (skillBase != null)
                skillBase.ReleaseLock();
        }
    }

    // STUN diberikan di awal dash
    private void ApplyDashStun()
    {
        RaycastHit2D[] hits = Physics2D.CircleCastAll(
            dashStart,
            hitRadius,
            character.isFacingRight ? Vector2.right : Vector2.left,
            0.1f,
            enemyLayer
        );

        foreach (var h in hits)
        {
            CharacterBase enemy = h.collider.GetComponent<CharacterBase>();
            if (enemy == null) continue;

            Vector2 dir = character.isFacingRight ? Vector2.right : Vector2.left;

            enemy.ApplyStagger(dir, dashKnockbackForce, dashStunDuration);

            Debug.Log($"⚡ Riposte STUN → {enemy.name}");
        }
    }

    // DAMAGE diberikan setelah dash selesai
    private void PerformFollowUpDamage()
    {
        Vector2 start = dashStart;
        Vector2 end = dashTarget;

        float dist = Vector2.Distance(start, end);
        Vector2 dir = (end - start).normalized;

        RaycastHit2D[] hits = Physics2D.CircleCastAll(
            start,
            hitRadius,
            dir,
            dist,
            enemyLayer
        );

        foreach (RaycastHit2D h in hits)
        {
            CharacterBase enemy = h.collider.GetComponent<CharacterBase>();
            if (enemy == null) continue;

            enemy.TakeDamage(character.attack);

            Debug.Log($"💥 Riposte DAMAGE → {enemy.name}");
        }
    }

    private IEnumerator StartCooldown()
    {
        isOnCooldown = true;
        yield return new WaitForSeconds(cooldownTime + stanceDuration);
        isOnCooldown = false;
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        Vector3 center = (character != null)
            ? character.transform.position
            : transform.position;

        Vector3 facingDir =
            (character != null && character.isFacingRight)
            ? Vector3.right
            : Vector3.left;

        if (isActive)
        {
            Gizmos.color = activeColor;

            float startAngle = -stanceAngle / 2f;
            float step = stanceAngle / arcSegments;

            Vector3 prev =
                center + Quaternion.Euler(0, 0, startAngle) * facingDir * stanceRadius;

            for (int i = 1; i <= arcSegments; i++)
            {
                float a = startAngle + step * i;
                Vector3 next =
                    center + Quaternion.Euler(0, 0, a) * facingDir * stanceRadius;

                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        if (isDashing)
        {
            Gizmos.color = dashColor;
            Gizmos.DrawLine(dashStart, dashTarget);
            Gizmos.DrawWireSphere(dashTarget, hitRadius);
        }
    }
}
