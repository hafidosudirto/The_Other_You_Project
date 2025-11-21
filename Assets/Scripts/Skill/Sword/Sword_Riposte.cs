using UnityEngine;
using System.Collections;

public class Sword_Riposte : MonoBehaviour, ISkill
{
    private CharacterBase character;
    private SkillBase skillBase;

    [Header("Riposte Settings")]
    public float stanceDuration = 0.6f;
    public float cooldownTime = 1.0f;

    private bool isOnCooldown = false;
    private bool isActive = false;
    private float stanceTimer = 0f;

    [Header("Follow-Up Attack")]
    public float dashDistance = 2.5f;
    public float dashSpeed = 10f;
    public float hitRadius = 0.8f;
    public LayerMask enemyLayer;

    private bool isDashing = false;
    private Vector3 dashStart;
    private Vector3 dashTarget;
    private int mySlotIndex;

    void Awake()
    {
        character = GetComponentInParent<CharacterBase>();
        skillBase = GetComponentInParent<SkillBase>();
    }

    void Update()
    {
        if (isActive)
        {
            stanceTimer -= Time.deltaTime;

            if (stanceTimer <= 0f)
            {
                isActive = false;

                if (character != null)
                    character.EndRiposteStance();

                // Release lock ONLY IF not dashing
                if (!isDashing && skillBase != null)
                    skillBase.ReleaseLock();

                // reset riposte ability
                if (character != null)
                    character.canRiposte = true;
            }
        }

        if (isDashing)
            DashForward();
    }

    // ---------------------------------------
    // SKILLBASE ENTRY
    // ---------------------------------------
    public void TriggerSkill(int slotIndex)
    {
        if (isActive || isDashing || isOnCooldown)
            return;

        if (character == null || !character.CanAct())
            return;

        mySlotIndex = slotIndex;

        // IMPORTANT:
        // Riposte stance SHOULD NOT register DDA
        // only FUA should
        TryActivateRiposte();
    }

    // ---------------------------------------
    // ACTIVATE RIPOSTE STANCE
    // ---------------------------------------
    private void TryActivateRiposte()
    {
        if (!character.canRiposte || isOnCooldown)
            return;

        if (skillBase != null)
            skillBase.skillLocked = true;

        character.ActivateRiposte();

        isActive = true;
        stanceTimer = stanceDuration;

        StartCoroutine(StartCooldown());
    }

    // ---------------------------------------
    // TRIGGER FOLLOW-UP DASH AFTER PARRY
    // Called by CharacterBase.Parry()
    // ---------------------------------------
    public void TriggerFollowUpDash()
    {
        if (!isActive) return;
        if (isDashing) return;

        Vector3 dir = (character.isFacingRight ? Vector3.right : Vector3.left);

        dashStart = character.transform.position;
        dashTarget = dashStart + dir * dashDistance;

        isDashing = true;
        isActive = false;

        // ❌ Jangan CAST Defensive lagi!
        // Riposte D-value sudah dihitung sekali saat Parry()
    }


    // ---------------------------------------
    // DASH MOVEMENT
    // ---------------------------------------
    private void DashForward()
    {
        character.transform.position = Vector3.MoveTowards(
            character.transform.position,
            dashTarget,
            dashSpeed * Time.deltaTime
        );

        if (Vector3.Distance(character.transform.position, dashTarget) <= 0.05f)
        {
            isDashing = false;
            PerformFollowUpDamage();

            // reset riposte usable
            if (character != null)
                character.canRiposte = true;

            if (skillBase != null)
                skillBase.ReleaseLock();
        }
    }

    // ---------------------------------------
    // DAMAGE ON DASH END
    // ---------------------------------------
    private void PerformFollowUpDamage()
    {
        Vector2 start = dashStart;
        Vector2 end = dashTarget;

        RaycastHit2D[] hits = Physics2D.CircleCastAll(
            start,
            hitRadius,
            (end - start).normalized,
            Vector2.Distance(start, end),
            enemyLayer
        );

        foreach (var h in hits)
        {
            CharacterBase enemy = h.collider.GetComponent<CharacterBase>();
            if (enemy != null && enemy != character)
                enemy.TakeDamage(character.attack);
        }
    }

    private IEnumerator StartCooldown()
    {
        isOnCooldown = true;
        yield return new WaitForSeconds(cooldownTime + stanceDuration);
        isOnCooldown = false;
    }

    // ---------------------------------------
    // FAIL-SAFE
    // ---------------------------------------
    void OnDisable()
    {
        if (skillBase != null)
            skillBase.ReleaseLock();

        if (character != null)
            character.canRiposte = true;

        isActive = false;
        isDashing = false;
    }

    // ---------------------------------------
    // SIMPLE GIZMO (ORI style)
    // ---------------------------------------
    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        Vector3 center = character.transform.position;
        Vector3 facing = character.isFacingRight ? Vector3.right : Vector3.left;

        if (isActive)
        {
            Gizmos.color = new Color(0f, 1f, 0.8f, 0.4f);
            Gizmos.DrawWireSphere(center + facing * 0.6f, hitRadius);
        }

        if (isDashing)
        {
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.4f);
            Gizmos.DrawLine(dashStart, dashTarget);
            Gizmos.DrawWireSphere(dashTarget, hitRadius);
        }
    }
}
