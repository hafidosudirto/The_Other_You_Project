using UnityEngine;
using System.Collections;

public class Sword_Riposte : MonoBehaviour, ISkill, IEnergySkill
{
    private CharacterBase character;
    private Player player;
    private MoveKeyboard mover;
    private Rigidbody2D rb;

    [Header("Animation")]
    public PlayerAnimation anim;

    [Header("Riposte Settings")]
    [Tooltip("Durasi karakter berada dalam kondisi Riposte Ready.")]
    public float stanceDuration = 0.6f;

    [Tooltip("Cooldown setelah Riposte dipakai.")]
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

    [Header("Energy")]
    [SerializeField, Min(0f)] private float energyCost = 10f;

    public float EnergyCost => energyCost;

    // Energy dipotong langsung di script ini,
    // supaya hanya berkurang kalau Riposte benar-benar berhasil aktif.
    public bool PayEnergyInSkillBase => false;

    private bool movementLockedByThisSkill = false;
    private Coroutine cooldownRoutine;

    private void Awake()
    {
        character = GetComponentInParent<CharacterBase>();
        player = GetComponentInParent<Player>();
        mover = GetComponentInParent<MoveKeyboard>();

        if (mover == null)
            mover = GetComponentInChildren<MoveKeyboard>();

        if (anim == null)
            anim = GetComponentInParent<PlayerAnimation>();

        if (anim == null)
            anim = GetComponentInChildren<PlayerAnimation>();

        if (character != null)
            rb = character.GetComponent<Rigidbody2D>();

        if (rb == null)
            rb = GetComponentInParent<Rigidbody2D>();

        if (rb == null)
            rb = GetComponentInChildren<Rigidbody2D>();
    }

    private void Update()
    {
        if (isActive)
        {
            // Kunci gerakan dipertahankan setiap frame.
            // Ini mencegah kasus lock habis lebih dulu tetapi animasi masih Riposte Ready.
            MaintainRiposteMovementLock();

            stanceTimer -= Time.deltaTime;

            if (stanceTimer <= 0f)
            {
                EndRiposteReady();
            }
        }

        if (isDashing)
        {
            // Saat Riposte Counter, input player juga tetap dikunci.
            MaintainRiposteMovementLock();
            DashForward();
        }
    }

    public void TriggerSkill(int slotIndex)
    {
        if (isActive)
            return;

        if (isDashing)
            return;

        if (isOnCooldown)
            return;

        if (character == null)
            return;

        if (!character.CanAct())
            return;

        if (player != null && player.isAttacking)
            return;

        TryActivateRiposte();
    }

    private void TryActivateRiposte()
    {
        if (character == null)
            return;

        if (!character.canRiposte)
            return;

        if (isOnCooldown)
            return;

        if (!HasEnoughEnergyToStart())
        {
            Debug.LogWarning($"[Sword_Riposte] Energy kurang. Riposte butuh {energyCost} energy.");
            return;
        }

        if (!character.TrySpendEnergy(energyCost))
        {
            Debug.LogWarning($"[Sword_Riposte] Energy kurang. Riposte butuh {energyCost} energy.");
            return;
        }

        character.ActivateRiposte();

        isActive = true;
        isDashing = false;
        stanceTimer = stanceDuration;

        if (player != null)
            player.isAttacking = true;

        LockRiposteMovement();

        if (anim != null)
        {
            anim.SetMoveSpeed(0f);
            anim.SetRiposteReady(true);
        }

        if (DataTracker.Instance != null)
            DataTracker.Instance.RecordSwordRiposte();

        StartRiposteCooldown();
    }

    private void EndRiposteReady()
    {
        isActive = false;
        stanceTimer = 0f;

        if (character != null)
        {
            character.EndRiposteStance();
        }

        if (anim != null)
        {
            anim.SetRiposteReady(false);
            anim.SetMoveSpeed(0f);
        }

        if (player != null && !isDashing)
            player.isAttacking = false;

        if (!isDashing)
            UnlockRiposteMovement();
    }

    public void TriggerFollowUpDash()
    {
        if (character == null)
            return;

        if (!isActive)
            return;

        if (isDashing)
            return;

        Vector3 dir = character.isFacingRight ? Vector3.right : Vector3.left;

        dashStart = character.transform.position;
        dashTarget = dashStart + dir * dashDistance;

        isActive = false;
        isDashing = true;
        stanceTimer = 0f;

        LockRiposteMovement();

        if (anim != null)
        {
            anim.SetMoveSpeed(0f);
            anim.SetRiposteReady(false);
            anim.TriggerRiposteCounter();
        }

        if (player != null)
            player.isAttacking = true;

        if (character != null)
            character.isRiposteStance = false;
    }

    private void DashForward()
    {
        if (character == null)
        {
            ForceStopRiposte(true);
            return;
        }

        character.transform.position = Vector3.MoveTowards(
            character.transform.position,
            dashTarget,
            dashSpeed * Time.deltaTime
        );

        if (rb != null)
            rb.velocity = Vector2.zero;

        if (Vector3.Distance(character.transform.position, dashTarget) <= 0.05f)
        {
            isDashing = false;

            PerformFollowUpDamage();

            if (character != null)
            {
                character.isRiposteStance = false;
            }

            if (player != null)
                player.isAttacking = false;

            if (anim != null)
            {
                anim.SetRiposteReady(false);
                anim.SetMoveSpeed(0f);
            }

            UnlockRiposteMovement();
        }
    }

    private void PerformFollowUpDamage()
    {
        if (character == null)
            return;

        Vector2 start = dashStart;
        Vector2 end = dashTarget;
        Vector2 direction = (end - start).normalized;
        float distance = Vector2.Distance(start, end);

        RaycastHit2D[] hits = Physics2D.CircleCastAll(
            start,
            hitRadius,
            direction,
            distance,
            enemyLayer
        );

        foreach (RaycastHit2D h in hits)
        {
            CharacterBase enemy = h.collider.GetComponent<CharacterBase>();

            if (enemy != null && enemy != character)
            {
                enemy.TakeDamage(character.attack);
            }
        }
    }

    private bool HasEnoughEnergyToStart()
    {
        if (character == null)
            return false;

        return character.HasEnergy(energyCost);
    }

    private void LockRiposteMovement()
    {
        movementLockedByThisSkill = true;

        if (player != null)
        {
            player.lockMovement = true;
        }

        if (mover != null)
        {
            // Durasi dibuat pendek tetapi nanti dipanggil ulang setiap frame oleh MaintainRiposteMovementLock().
            // Jadi lock tidak akan putus selama isActive / isDashing masih true.
            mover.LockExternal(0.2f, stopVelocity: true);
        }

        if (rb != null)
            rb.velocity = Vector2.zero;

        if (anim != null)
            anim.SetMoveSpeed(0f);
    }

    private void MaintainRiposteMovementLock()
    {
        if (!movementLockedByThisSkill)
            movementLockedByThisSkill = true;

        if (player != null)
        {
            player.lockMovement = true;
        }

        if (mover != null)
        {
            mover.LockExternal(0.2f, stopVelocity: true);
        }

        if (rb != null)
            rb.velocity = Vector2.zero;

        if (anim != null)
            anim.SetMoveSpeed(0f);
    }

    private void UnlockRiposteMovement()
    {
        if (movementLockedByThisSkill)
        {
            if (player != null)
                player.lockMovement = false;

            if (mover != null)
                mover.UnlockExternal();

            if (rb != null)
                rb.velocity = Vector2.zero;

            if (anim != null)
                anim.SetMoveSpeed(0f);

            movementLockedByThisSkill = false;
        }
    }

    private void ForceStopRiposte(bool resetCooldownUsable = true)
    {
        isActive = false;
        isDashing = false;
        stanceTimer = 0f;

        if (character != null)
        {
            character.isRiposteStance = false;

            if (resetCooldownUsable)
                character.canRiposte = true;
        }

        if (anim != null)
        {
            anim.SetRiposteReady(false);
            anim.SetMoveSpeed(0f);
        }

        if (player != null)
            player.isAttacking = false;

        UnlockRiposteMovement();
    }

    private void StartRiposteCooldown()
    {
        if (cooldownRoutine != null)
            StopCoroutine(cooldownRoutine);

        cooldownRoutine = StartCoroutine(StartCooldown());
    }

    private IEnumerator StartCooldown()
    {
        isOnCooldown = true;

        yield return new WaitForSeconds(cooldownTime + stanceDuration);

        isOnCooldown = false;
        cooldownRoutine = null;
    }

    private void OnDisable()
    {
        ForceStopRiposte(true);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || character == null)
            return;

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
#endif
}