using UnityEngine;
using System.Collections;

public class Sword_ChargedStrike : MonoBehaviour, ISkill, IEnergySkill
{
    [Header("Energy (ChargedStrike pays on Release)")]
    [SerializeField, Min(0f)] private float energyCost = 25f;

    public float EnergyCost => energyCost;
    public bool PayEnergyInSkillBase => false;

    [Header("Charge Settings")]
    public float maxChargeTime = 2.0f;
    public float minDamageMultiplier = 1f;
    public float maxDamageMultiplier = 3f;

    [Header("Attack Settings")]
    public float attackRadius = 1.6f;
    public float attackAngle = 100f;

    [Header("Stagger Settings")]
    public float knockbackForce = 8f;
    public float stunDuration = 0.4f;

    [Header("Strike Timing (Percent of strike clip length)")]
    [Range(0f, 1f)] public float strikeActiveStart = 0.35f;
    [Range(0f, 1f)] public float strikeActiveEnd = 0.55f;
    [Tooltip("Dipakai jika gagal membaca panjang clip dari Animator (detik).")]
    public float strikeClipFallbackLength = 0.35f;

    [Header("SFX Timing")]
    [Tooltip("Aktifkan jika SFX Charged Strike ingin dikendalikan dari script ini, bukan dari Animation Event.")]
    public bool playSfxFromScript = true;

    [Tooltip("Suara charge diputar selama tombol Charged Strike masih ditahan.")]
    public bool playChargeSfxWhileCharging = true;

    [Tooltip("Jika true, suara charge akan di-loop sampai tombol dilepas atau skill dibatalkan.")]
    public bool loopChargeSfx = true;

    [Range(0f, 1f)]
    [Tooltip("Volume khusus untuk suara charge. Tidak memengaruhi suara release dan hit.")]
    public float chargeSfxVolume = 1f;

    [Tooltip("Suara release/ayunan kuat diputar tepat saat hitbox Charged Strike aktif.")]
    public bool playReleaseSfxOnActiveFrame = true;

    [Tooltip("Suara hit hanya dimainkan satu kali untuk satu Charged Strike, walaupun musuh yang terkena lebih dari satu.")]
    public bool playHitSfxOncePerStrike = true;

    [Header("Gizmo (Shown on Strike Damage)")]
    public Color gizmoColor = new Color(1f, 0.6f, 0f);
    public float gizmoRadius = 1.4f;
    public float gizmoAngle = 100f;
    public int gizmoSegments = 20;
    public float strikeGizmoShowTime = 0.08f;

    private Player player;
    private PlayerAnimation anim;
    private MoveKeyboard mover;
    private SkillBase skillBase;

    private Animator unityAnimator;

    private bool isCharging = false;
    private float chargeTimer = 0f;

    private bool showGizmo = false;
    private Coroutine gizmoRoutine;

    private int mySlotIndex = 0;
    private Coroutine runningRoutine;
    private AudioSource chargeSfxSource;

    void Awake()
    {
        player = GetComponentInParent<Player>();
        anim = GetComponentInParent<PlayerAnimation>();
        mover = GetComponentInParent<MoveKeyboard>();
        skillBase = GetComponentInParent<SkillBase>();

        unityAnimator = GetComponentInParent<Animator>();
        if (unityAnimator == null && anim != null && anim.animator != null)
            unityAnimator = anim.animator;
    }

    public void TriggerSkill(int slotIndex)
    {
        if (skillBase != null && EnergyCost > 0f)
        {
            var character = GetComponentInParent<CharacterBase>();
            if (character != null && character.CurrentEnergy < EnergyCost)
            {
                DebugHub.Warning($"ENERGY KURANG: ChargedStrike butuh {EnergyCost}.");
                return;
            }
        }

        if (isCharging) return;

        if (player != null)
        {
            if (!player.CanAct()) return;
            if (player.isAttacking) return;
        }

        mySlotIndex = slotIndex;

        if (runningRoutine != null)
            StopCoroutine(runningRoutine);

        runningRoutine = StartCoroutine(ChargeRoutine());
    }

    private KeyCode GetHoldKey()
    {
        if (skillBase == null) return KeyCode.None;

        switch (mySlotIndex)
        {
            case 0: return skillBase.slot1Key;
            case 1: return skillBase.slot2Key;
            case 2: return skillBase.slot3Key;
            case 3: return skillBase.slot4Key;
            default: return KeyCode.None;
        }
    }

    private IEnumerator ChargeRoutine()
    {
        isCharging = true;
        chargeTimer = 0f;

        if (player != null)
            player.isAttacking = true;

        KeyCode holdKey = GetHoldKey();
        if (holdKey == KeyCode.None)
        {
            ResetAllState();
            yield break;
        }

        if (mover != null)
            mover.LockExternal(999f);

        if (anim != null)
            anim.SetCharging(true);

        // SFX charge diputar selama tombol skill masih ditahan.
        PlayChargeSfx();

        yield return null;

        while (Input.GetKey(holdKey))
        {
            chargeTimer += Time.deltaTime;
            chargeTimer = Mathf.Clamp(chargeTimer, 0f, maxChargeTime);
            yield return null;
        }

        StopChargeSfx();

        isCharging = false;

        if (anim != null)
            anim.SetCharging(false);

        if (!TrySpendEnergyOnRelease())
        {
            CancelAfterInsufficientEnergy();
            runningRoutine = null;
            yield break;
        }

        // Hanya dihitung sebagai penggunaan riil jika release berhasil dan energi terpotong.
        if (DataTracker.Instance != null)
            DataTracker.Instance.RecordSwordChargedStrike();

        float chargePercent = (maxChargeTime > 0f) ? (chargeTimer / maxChargeTime) : 1f;
        float multiplier = Mathf.Lerp(minDamageMultiplier, maxDamageMultiplier, chargePercent);
        multiplier = Mathf.Round(multiplier);

        yield return StartCoroutine(StrikeRoutine(multiplier));

        runningRoutine = null;
    }

    private bool TrySpendEnergyOnRelease()
    {
        float cost = Mathf.Max(0f, EnergyCost);
        if (cost <= 0f) return true;

        if (skillBase == null)
        {
            DebugHub.Warning("[Sword_ChargedStrike] SkillBase tidak ditemukan. Batalkan strike.");
            return false;
        }

        bool ok = skillBase.TrySpendEnergy(cost);
        if (!ok)
        {
            DebugHub.Warning($"ENERGY KURANG: ChargedStrike butuh {cost}.");
            return false;
        }

        return true;
    }

    private void CancelAfterInsufficientEnergy()
    {
        StopChargeSfx();

        if (mover != null)
            mover.UnlockExternal();

        if (player != null)
            player.isAttacking = false;

        if (anim != null)
            anim.SetCharging(false);

        isCharging = false;
        chargeTimer = 0f;
    }

    private IEnumerator StrikeRoutine(float multiplier)
    {
        yield return null;
        yield return null;

        float clipLen = GetCurrentClipLengthOrFallback();
        if (clipLen <= 0f) clipLen = strikeClipFallbackLength;

        float startP = Mathf.Clamp01(strikeActiveStart);
        float endP = Mathf.Clamp01(strikeActiveEnd);
        if (endP < startP) endP = startP;

        float startT = clipLen * startP;
        float endT = clipLen * endP;

        if (mover != null)
            mover.LockExternal(clipLen);

        if (startT > 0f)
            yield return new WaitForSeconds(startT);

        // SFX ayunan kuat diputar pada timing yang sama dengan aktifnya hitbox Charged Strike.
        if (playReleaseSfxOnActiveFrame)
            PlayReleaseSfx();

        PerformChargedStrike(multiplier);

        float activeDur = Mathf.Max(0f, endT - startT);
        if (activeDur > 0f)
            yield return new WaitForSeconds(activeDur);

        float tail = Mathf.Max(0f, clipLen - endT);
        if (tail > 0f)
            yield return new WaitForSeconds(tail);

        if (player != null)
            player.isAttacking = false;
    }

    private float GetCurrentClipLengthOrFallback()
    {
        if (unityAnimator == null)
            return strikeClipFallbackLength;

        var infos = unityAnimator.GetCurrentAnimatorClipInfo(0);
        if (infos != null && infos.Length > 0 && infos[0].clip != null)
            return infos[0].clip.length;

        return strikeClipFallbackLength;
    }

    private void PerformChargedStrike(float multiplier)
    {
        if (!player) return;

        ShowStrikeGizmoBriefly();

        Vector3 origin = player.transform.position;
        Vector3 dir = player.isFacingRight ? Vector3.right : Vector3.left;

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, attackRadius);
        bool hasHit = false;

        foreach (Collider2D hit in hits)
        {
            CharacterBase target = hit.GetComponent<CharacterBase>();
            if (!target || target == player) continue;

            Vector2 toTarget = (target.transform.position - origin).normalized;
            float angle = Vector2.Angle(dir, toTarget);

            if (angle <= attackAngle * 0.5f)
            {
                float damage = player.attack * multiplier;
                target.TakeDamage(damage, null);

                Vector2 knockDir = (target.transform.position - origin).normalized;
                target.ApplyStagger(knockDir, knockbackForce, stunDuration);

                hasHit = true;

                if (!playHitSfxOncePerStrike)
                    PlayHitSfx();
            }
        }

        // SFX hit hanya diputar jika Charged Strike benar-benar mengenai minimal satu musuh.
        if (hasHit && playHitSfxOncePerStrike)
            PlayHitSfx();
    }

    private void PlayChargeSfx()
    {
        if (!playSfxFromScript) return;
        if (!playChargeSfxWhileCharging) return;
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

    private void ShowStrikeGizmoBriefly()
    {
        showGizmo = true;

        if (gizmoRoutine != null)
            StopCoroutine(gizmoRoutine);

        gizmoRoutine = StartCoroutine(HideStrikeGizmoAfterTime());
    }

    private IEnumerator HideStrikeGizmoAfterTime()
    {
        yield return new WaitForSeconds(strikeGizmoShowTime);
        showGizmo = false;
        gizmoRoutine = null;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        runningRoutine = null;
        gizmoRoutine = null;

        ResetAllState();
    }

    private void ResetAllState()
    {
        StopChargeSfx();

        isCharging = false;
        chargeTimer = 0f;
        showGizmo = false;

        if (anim != null)
            anim.SetCharging(false);

        if (player != null)
            player.isAttacking = false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showGizmo || player == null)
            return;

        Vector3 origin = player.transform.position;
        Vector3 dir = player.isFacingRight ? Vector3.right : Vector3.left;

        Gizmos.color = gizmoColor;

        float radius = attackRadius;
        float angleTotal = attackAngle;

        float startAngle = -angleTotal * 0.5f;
        float step = angleTotal / Mathf.Max(1, gizmoSegments);

        Vector3 prev = origin + Quaternion.Euler(0, 0, startAngle) * dir * radius;

        for (int i = 1; i <= gizmoSegments; i++)
        {
            float ang = startAngle + step * i;
            Vector3 next = origin + Quaternion.Euler(0, 0, ang) * dir * radius;
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
#endif
}
