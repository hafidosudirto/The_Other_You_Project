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

    [Header("SFX Timing")]
    [Tooltip("Aktifkan jika SFX Whirlwind musuh ingin dikendalikan dari script ini, bukan dari Animation Event.")]
    public bool playSfxFromScript = true;

    [Tooltip("Suara Whirlwind diputar satu kali saat skill pertama kali aktif.")]
    public bool playWhirlwindSfxOnStart = true;

    [Tooltip("Suara hit hanya dimainkan satu kali untuk satu tick damage, walaupun target yang terkena lebih dari satu.")]
    public bool playHitSfxOncePerTick = true;

    private NodeManager ai;
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
        ai = GetComponentInParent<NodeManager>();
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

        if (playWhirlwindSfxOnStart)
            PlayWhirlwindSfx();

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
        bool hasHit = false;

        foreach (var h in hits)
        {
            CharacterBase cb = h.GetComponentInParent<CharacterBase>();
            if (!cb || cb == selfStats) continue;

            cb.TakeDamage(ai.AttackPower * damageMultiplier, ai.gameObject);
            hasHit = true;

            if (!playHitSfxOncePerTick)
                PlayHitSfx();
        }

        if (hasHit && playHitSfxOncePerTick)
            PlayHitSfx();
    }

    private void PlayWhirlwindSfx()
    {
        if (SFXManager.Instance == null) return;
        PlaySfx(SFXManager.Instance.swordWhirlwind);
    }

    private void PlayHitSfx()
    {
        if (SFXManager.Instance == null) return;
        PlaySfx(SFXManager.Instance.swordHit);
    }

    private void PlaySfx(AudioClip clip)
    {
        if (!playSfxFromScript) return;
        if (clip == null) return;
        if (SFXManager.Instance == null) return;
        if (SFXManager.Instance.sfxSource == null) return;

        SFXManager.Instance.PlaySFX(clip);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        if (!busy) return;

        if (ai == null) ai = GetComponentInParent<NodeManager>();
        if (ai == null) return;

        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(ai.transform.position, radius);
    }
#endif
}