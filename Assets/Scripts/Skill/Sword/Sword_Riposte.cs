using UnityEngine;
using System.Collections;

public class Sword_Riposte : MonoBehaviour, ISkill
{
    private CharacterBase character;
    private SkillBase     skillBase;
    private Player        player;

    [Header("Animation")]
    public PlayerAnimation anim;

    [Header("Riposte Settings")]
    public float stanceDuration = 0.6f;
    public float cooldownTime   = 1.0f;

    private bool  isOnCooldown = false;
    private bool  isActive     = false;
    private float stanceTimer  = 0f;

    [Header("Follow-Up Attack")]
    public float dashDistance = 2.5f;
    public float dashSpeed    = 10f;
    public float hitRadius    = 0.8f;
    public LayerMask enemyLayer;

    private bool   isDashing  = false;
    private Vector3 dashStart;
    private Vector3 dashTarget;
    private int     mySlotIndex;

    void Awake()
    {
        character = GetComponentInParent<CharacterBase>();
        skillBase = GetComponentInParent<SkillBase>();
        player    = GetComponentInParent<Player>();

        if (anim == null)
            anim = GetComponentInParent<PlayerAnimation>();
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

                // reset riposte ability
                if (character != null)
                    character.canRiposte = true;

                // MATIKAN ANIMASI STANCE
                if (anim != null)
                    anim.SetRiposteReady(false);

                // Jika stance berakhir tanpa dash follow-up,
                // pastikan player tidak dianggap menyerang lagi
                if (player != null && !isDashing)
                    player.isAttacking = false;
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
        Debug.Log("[Riposte] TriggerSkill dipanggil, slot = " + slotIndex);

        if (isActive || isDashing || isOnCooldown)
        {
            Debug.Log("[Riposte] Gagal: isActive/isDashing/isOnCooldown = " + isActive + "/" + isDashing + "/" + isOnCooldown);
            return;
        }

        if (character == null || !character.CanAct())
        {
            Debug.Log("[Riposte] Gagal: character null atau !CanAct()");
            return;
        }

        // Tambahan: jangan boleh *cast* kalau sedang melakukan serangan besar lain
        if (player != null && player.isAttacking)
        {
            Debug.Log("[Riposte] Gagal: player.isAttacking = true");
            return;
        }

        mySlotIndex = slotIndex;
        TryActivateRiposte();
    }

    private void TryActivateRiposte()
    {
        Debug.Log("[Riposte] TryActivateRiposte()");

        if (character == null)
        {
            Debug.Log("[Riposte] Gagal: character null");
            return;
        }

        Debug.Log("[Riposte] canRiposte = " + character.canRiposte + ", isOnCooldown = " + isOnCooldown);

        if (!character.canRiposte || isOnCooldown)
        {
            Debug.Log("[Riposte] Gagal: !canRiposte atau isOnCooldown");
            return;
        }

        character.ActivateRiposte();
        isActive    = true;
        stanceTimer = stanceDuration;

        // Selama stance aktif, anggap player sedang "attacking"
        if (player != null)
            player.isAttacking = true;

        Debug.Log("[Riposte] BERHASIL: stance aktif, timer = " + stanceTimer);

        if (anim != null)
        {
            anim.SetRiposteReady(true);
            Debug.Log("[Riposte] SetRiposteReady(true) dikirim ke Animator");
        }
        else
        {
            Debug.Log("[Riposte] Gagal: anim == null");
        }

        StartCoroutine(StartCooldown());
    }

    // ---------------------------------------
    // TRIGGER FOLLOW-UP DASH AFTER PARRY
    // Called by CharacterBase.Parry()
    // ---------------------------------------
    public void TriggerFollowUpDash()
    {
        if (character == null)
            return;

        if (!isActive)  return;
        if (isDashing)  return;

        Vector3 dir = (character.isFacingRight ? Vector3.right : Vector3.left);

        dashStart  = character.transform.position;
        dashTarget = dashStart + dir * dashDistance;

        isDashing = true;
        isActive  = false;

        // MATIKAN ANIMASI STANCE & MAINKAN COUNTER
        if (anim != null)
        {
            anim.SetRiposteReady(false);
            anim.TriggerRiposteCounter();
        }

        // Pastikan selama dash follow-up juga dianggap sedang menyerang
        if (player != null)
            player.isAttacking = true;

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

            // Dash selesai → serangan selesai
            if (player != null)
                player.isAttacking = false;

            // if (skillBase != null)
            //     skillBase.ReleaseLock();
        }
    }

    // ---------------------------------------
    // DAMAGE ON DASH END
    // ---------------------------------------
    private void PerformFollowUpDamage()
    {
        Vector2 start = dashStart;
        Vector2 end   = dashTarget;

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
        // if (skillBase != null)
        //     skillBase.ReleaseLock();

        if (character != null)
            character.canRiposte = true;

        // Pastikan animasi stance mati
        if (anim != null)
            anim.SetRiposteReady(false);

        // Pastikan flag global serangan dimatikan
        if (player != null)
            player.isAttacking = false;

        isActive  = false;
        isDashing = false;
    }

    // ---------------------------------------
    // SIMPLE GIZMO (ORI style)
    // ---------------------------------------
    void OnDrawGizmos()
    {
        if (!Application.isPlaying || character == null) return;

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
