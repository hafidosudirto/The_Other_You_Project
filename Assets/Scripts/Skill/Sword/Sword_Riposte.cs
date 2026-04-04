using UnityEngine;
using System.Collections;

public class Sword_Riposte : MonoBehaviour, ISkill, IEnergySkill
{
    private CharacterBase character;
    private SkillBase skillBase;
    private Player player;

    [Header("Animation")]
    public PlayerAnimation anim;

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

    [Header("Energy")]
    [SerializeField, Min(0f)] private float energyCost = 10f;

    public float EnergyCost => energyCost;
    public bool PayEnergyInSkillBase => true;

    private void Awake()
    {
        character = GetComponentInParent<CharacterBase>();
        skillBase = GetComponentInParent<SkillBase>();
        player = GetComponentInParent<Player>();

        if (anim == null)
            anim = GetComponentInParent<PlayerAnimation>();
    }

    private bool HasEnoughEnergyToStart()
    {
        if (character == null) return false;
        return character.CurrentEnergy + 1e-6f >= energyCost;
    }

    private bool HasAnyEnergyLeft()
    {
        if (character == null) return false;
        return character.CurrentEnergy > 0f;
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
            anim.SetRiposteReady(false);

        if (player != null)
            player.isAttacking = false;
    }

    private void Update()
    {
        if ((isActive || isDashing) && !HasAnyEnergyLeft())
        {
            ForceStopRiposte(true);
            return;
        }

        if (isActive)
        {
            stanceTimer -= Time.deltaTime;

            if (stanceTimer <= 0f)
            {
                isActive = false;

                if (character != null)
                    character.EndRiposteStance();

                if (character != null)
                    character.canRiposte = true;

                if (anim != null)
                    anim.SetRiposteReady(false);

                if (player != null && !isDashing)
                    player.isAttacking = false;
            }
        }

        if (isDashing)
            DashForward();
    }

    public void TriggerSkill(int slotIndex)
    {
        if (isActive || isDashing || isOnCooldown)
            return;

        if (character == null || !character.CanAct())
            return;

        if (player != null && player.isAttacking)
            return;

        if (!HasEnoughEnergyToStart())
        {
            DebugHub.Warning($"ENERGY KURANG: Riposte butuh {energyCost}.");
            return;
        }

        mySlotIndex = slotIndex;
        TryActivateRiposte();
    }

    private void TryActivateRiposte()
    {
        if (character == null)
            return;

        if (!character.canRiposte || isOnCooldown)
            return;

        if (!HasAnyEnergyLeft())
        {
            ForceStopRiposte(true);
            return;
        }

        character.ActivateRiposte();
        isActive = true;
        stanceTimer = stanceDuration;

        if (player != null)
            player.isAttacking = true;

        if (anim != null)
            anim.SetRiposteReady(true);

        if (DataTracker.Instance != null)
            DataTracker.Instance.RecordSwordRiposte();

        StartCoroutine(StartCooldown());
    }

    public void TriggerFollowUpDash()
    {
        if (character == null)
            return;

        if (!isActive) return;
        if (isDashing) return;

        if (!HasAnyEnergyLeft())
        {
            ForceStopRiposte(true);
            return;
        }

        Vector3 dir = character.isFacingRight ? Vector3.right : Vector3.left;

        dashStart = character.transform.position;
        dashTarget = dashStart + dir * dashDistance;

        isDashing = true;
        isActive = false;

        if (anim != null)
        {
            anim.SetRiposteReady(false);
            anim.TriggerRiposteCounter();
        }

        if (player != null)
            player.isAttacking = true;
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

        if (Vector3.Distance(character.transform.position, dashTarget) <= 0.05f)
        {
            isDashing = false;

            if (HasAnyEnergyLeft())
                PerformFollowUpDamage();

            if (character != null)
                character.canRiposte = true;

            if (player != null)
                player.isAttacking = false;
        }
    }

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

    private void OnDisable()
    {
        ForceStopRiposte(true);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
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
#endif
}
