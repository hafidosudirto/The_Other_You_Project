using UnityEngine;
using System.Collections;

public class Enemy_Sword_ChargedStrike : MonoBehaviour
{
    [Header("Hit Settings")]
    public float attackRadius = 1.6f;
    public float attackAngle = 100f;
    public float damageMultiplier = 2f;
    public Vector2 hitOffset = new Vector2(1.1f, 0f);

    [Header("Knockback (Stagger)")]
    public float knockbackForce = 8f;
    public float stunDuration = 0.4f;

    [Header("Timing")]
    public float windupTime = 0.5f;
    public float activeTime = 0.1f;
    public float recoveryTime = 0.5f;

    [Header("Anti-spam")]
    public float cooldown = 0.75f;

    [Header("Mask")]
    public LayerMask hitMask;

    [Header("SFX Timing")]
    [Tooltip("Aktifkan jika SFX Charged Strike musuh ingin dikendalikan dari script ini, bukan dari Animation Event.")]
    public bool playSfxFromScript = true;

    [Tooltip("Suara charge diputar selama fase windup/charging musuh berlangsung.")]
    public bool playChargeSfxDuringWindup = true;

    [Tooltip("Jika true, suara charge akan di-loop sampai fase windup selesai atau skill dibatalkan.")]
    public bool loopChargeSfx = true;

    [Range(0f, 1f)]
    [Tooltip("Volume khusus untuk suara charge musuh. Tidak memengaruhi suara release dan hit.")]
    public float chargeSfxVolume = 1f;

    [Tooltip("Suara release/ayunan kuat diputar saat Charged Strike dilepaskan.")]
    public bool playReleaseSfxOnActiveFrame = true;

    [Tooltip("Suara hit hanya dimainkan satu kali untuk satu Charged Strike, walaupun target yang terkena lebih dari satu.")]
    public bool playHitSfxOncePerStrike = true;

    private NodeManager ai;
    private EnemyCombatController combat;
    private EnemyMovementFSM movementFSM;
    private CharacterBase selfStats;

    private bool busy = false;
    private float nextReadyTime = 0f;

    private Coroutine activeRoutine;
    private bool skillStartInvoked = false;
    private bool movementLockedByThisSkill = false;
    private AudioSource chargeSfxSource;

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

        activeRoutine = StartCoroutine(ChargeRoutine());
    }

    private IEnumerator ChargeRoutine()
    {
        busy = true;
        nextReadyTime = Time.time + cooldown;

        BeginSkillState(GetEstimatedLockDuration());

        ai?.Animation?.SetCharging(true);

        // SFX charge diputar selama fase windup/charging musuh berlangsung.
        PlayChargeSfx();

        yield return new WaitForSeconds(windupTime);

        StopChargeSfx();

        ai?.Animation?.SetCharging(false);
        ai?.Animation?.PlaySlash2();

        if (playReleaseSfxOnActiveFrame)
            PlayReleaseSfx();

        PerformAttack();

        yield return new WaitForSeconds(activeTime);
        yield return new WaitForSeconds(recoveryTime);

        ForceEndSkillState();
        activeRoutine = null;
    }

    private float GetEstimatedLockDuration()
    {
        return Mathf.Max(0.05f, windupTime + activeTime + recoveryTime + 0.1f);
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
        StopChargeSfx();

        ai?.Animation?.SetCharging(false);

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

    private void PerformAttack()
    {
        if (ai == null) return;

        int sign = ai.ForwardSign;
        Vector3 dir = ai.ForwardDir;

        Vector3 origin = ai.transform.position + new Vector3(hitOffset.x * sign, hitOffset.y, 0f);

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, attackRadius, hitMask);
        bool hasHit = false;

        foreach (var h in hits)
        {
            CharacterBase cb = h.GetComponentInParent<CharacterBase>();
            if (!cb || cb == selfStats) continue;

            Vector2 toTarget = cb.transform.position - origin;
            float angle = Vector2.Angle(dir, toTarget);

            if (angle > attackAngle * 0.5f) continue;

            float dmg = ai.AttackPower * damageMultiplier;

            cb.TakeDamage(dmg, ai.gameObject);
            hasHit = true;

            if (!cb.isRiposteStance)
            {
                Vector2 knockDir = toTarget.normalized;
                cb.ApplyStagger(knockDir, knockbackForce, stunDuration);
            }

            if (!playHitSfxOncePerStrike)
                PlayHitSfx();
        }

        if (hasHit && playHitSfxOncePerStrike)
            PlayHitSfx();
    }

    private void PlayChargeSfx()
    {
        if (!playSfxFromScript) return;
        if (!playChargeSfxDuringWindup) return;
        if (SFXManager.Instance == null) return;
        if (SFXManager.Instance.swordCharge == null) return;

        if (loopChargeSfx)
        {
            EnsureChargeSfxSource();
            if (chargeSfxSource == null) return;

            chargeSfxSource.clip = SFXManager.Instance.swordCharge;
            chargeSfxSource.loop = true;
            chargeSfxSource.volume = chargeSfxVolume;

            if (!chargeSfxSource.isPlaying)
                chargeSfxSource.Play();
        }
        else
        {
            PlaySfx(SFXManager.Instance.swordCharge);
        }
    }

    private void StopChargeSfx()
    {
        if (chargeSfxSource == null) return;

        if (chargeSfxSource.isPlaying)
            chargeSfxSource.Stop();

        chargeSfxSource.clip = null;
    }

    private void EnsureChargeSfxSource()
    {
        if (chargeSfxSource != null) return;

        chargeSfxSource = gameObject.AddComponent<AudioSource>();
        chargeSfxSource.playOnAwake = false;
        chargeSfxSource.loop = true;
        chargeSfxSource.volume = chargeSfxVolume;

        if (SFXManager.Instance != null && SFXManager.Instance.sfxSource != null)
        {
            AudioSource referenceSource = SFXManager.Instance.sfxSource;
            chargeSfxSource.outputAudioMixerGroup = referenceSource.outputAudioMixerGroup;
            chargeSfxSource.spatialBlend = referenceSource.spatialBlend;
            chargeSfxSource.rolloffMode = referenceSource.rolloffMode;
            chargeSfxSource.minDistance = referenceSource.minDistance;
            chargeSfxSource.maxDistance = referenceSource.maxDistance;
            chargeSfxSource.priority = referenceSource.priority;
        }
    }

    private void PlayReleaseSfx()
    {
        if (SFXManager.Instance == null) return;
        PlaySfx(SFXManager.Instance.swordSlash2);
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
}