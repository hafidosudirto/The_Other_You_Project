using UnityEngine;
using System.Collections;

public class Sword_ChargedStrike : MonoBehaviour, ISkill, IEnergySkill, ISkillCooldownInfo, ISkillReadinessInfo
{
    [Header("Energy (ChargedStrike pays on Release)")]
    [SerializeField, Min(0f)] private float energyCost = 25f;

    public float EnergyCost => energyCost;
    public bool PayEnergyInSkillBase => false;

    // Data baca untuk SkillIndexHUDController.
    // Tidak menambah angka tuning baru. Durasi visual diambil dari durasi clip/fallback yang sudah ada.
    public bool HasCooldown => cooldownDuration > 0.05f;
    public float CooldownDuration => Mathf.Max(0f, cooldownDuration);
    public float CooldownRemaining => Mathf.Max(0f, recoveryEndTime - Time.time);
    public bool IsCooldownReady => CooldownRemaining <= 0.05f;
    public bool IsSkillBusy => isCharging || isRecovering;
    public bool IsSkillReady => !IsSkillBusy && IsCooldownReady && HasEnoughEnergyToStart();
    public bool isCasting => IsSkillBusy;
    public float cooldownDuration => Mathf.Max(0f, GetCurrentClipLengthOrFallback());

    [Header("Charge Settings")]
    public float maxChargeTime = 2.0f;

    [Header("Damage / Tuning")]
    [Tooltip("Atur angka damage flat Charged Strike di sini (damage minimum & maksimum sesuai charge).\n" +
             "Damage = angka pasti, TIDAK dikali player.attack (sama seperti damageQuickShot).")]
    [SerializeField] private SwordChargedStrikeDamage damageConfig = new SwordChargedStrikeDamage();

    [Header("Attack Settings")]
    public float attackRadius = 1.6f;
    public float attackAngle = 100f;

    [Tooltip("Geser pusat hitbox ke DEPAN (arah hadap) agar lingkaran serang menutupi seluruh " +
             "efek visual tebasan yang menjorok jauh ke depan. X positif = ke arah hadap player.\n" +
             "Naikkan X (mis. 0.8–1.2) bila ujung efek masih tidak kena hit.")]
    public Vector2 hitOffset = new Vector2(0.8f, 0f);

    [Header("Stagger / Tuning")]
    [Tooltip("Atur stagger tiap hit Charged Strike.\nImmunity duration diatur terpusat di StaggerCooldownSettings asset.")]
    [SerializeField] private SkillStaggerConfig staggerConfig = new SkillStaggerConfig
    {
        knockbackForce = 8f,
        staggerDuration = 0.4f
    };

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
    private bool isRecovering = false;
    private float chargeTimer = 0f;
    private float recoveryEndTime = -999f;

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

    private bool HasEnoughEnergyToStart()
    {
        float cost = Mathf.Max(0f, EnergyCost);
        if (cost <= 0f) return true;

        CharacterBase character = GetComponentInParent<CharacterBase>();
        if (character == null) return true;

        return character.CurrentEnergy + 1e-6f >= cost;
    }

    public void TriggerSkill(int slotIndex)
    {
        if (!HasEnoughEnergyToStart())
        {
            DebugHub.Warning($"ENERGY KURANG: ChargedStrike butuh {EnergyCost}.");
            return;
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
        float damage = damageConfig.HitungDamage(chargePercent);

        yield return StartCoroutine(StrikeRoutine(damage));

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

    private IEnumerator StrikeRoutine(float damage)
    {
        isRecovering = true;

        yield return null;
        yield return null;

        float clipLen = GetCurrentClipLengthOrFallback();
        if (clipLen <= 0f) clipLen = strikeClipFallbackLength;

        recoveryEndTime = Time.time + Mathf.Max(0f, clipLen);
        SkillIndexHUDController.NotifyGlobalSkillUsed(this);

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

        PerformChargedStrike(damage);

        float activeDur = Mathf.Max(0f, endT - startT);
        if (activeDur > 0f)
            yield return new WaitForSeconds(activeDur);

        float tail = Mathf.Max(0f, clipLen - endT);
        if (tail > 0f)
            yield return new WaitForSeconds(tail);

        if (player != null)
            player.isAttacking = false;

        isRecovering = false;
        recoveryEndTime = -999f;
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

    private void PerformChargedStrike(float damage)
    {
        if (!player) return;

        ShowStrikeGizmoBriefly();

        // Geser pusat hitbox ke arah hadap agar lingkaran serang menutupi seluruh efek tebasan.
        Vector3 offset = new Vector3(hitOffset.x, hitOffset.y, 0f);
        if (!player.isFacingRight)
            offset.x = -offset.x;

        Vector3 origin = player.transform.position + offset;
        Vector3 dir = player.isFacingRight ? Vector3.right : Vector3.left;

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, attackRadius);
        bool hasHit = false;

        foreach (Collider2D hit in hits)
        {
            CharacterBase target = hit.GetComponentInParent<CharacterBase>();
            if (!target || target == player) continue;

            // Cek cone dari TITIK TERDEKAT collider ke origin, bukan dari pivot
            // (transform.position) yang biasanya di kaki. Memakai pivot membuat musuh
            // dekat ditolak karena vektor ke kaki menukik tajam — hanya ujung pedang
            // yang kena. ClosestPoint membuat musuh besar/tinggi tetap terdeteksi.
            Vector2 closestPoint = hit.ClosestPoint(origin);
            Vector2 toTarget = closestPoint - (Vector2)origin;

            // Origin di dalam collider (overlap penuh) → jarak ~0, langsung kena.
            bool insideCollider = toTarget.sqrMagnitude < 0.0001f;
            float angle = insideCollider ? 0f : Vector2.Angle(dir, toTarget.normalized);

            if (angle <= attackAngle * 0.5f)
            {
                target.TakeDamage(damage, null);

                Vector2 knockDir = insideCollider ? (Vector2)dir : toTarget.normalized;
                staggerConfig.Apply(target, knockDir);

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
        isRecovering = false;
        chargeTimer = 0f;
        recoveryEndTime = -999f;
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

        // Pakai offset + radius/angle ASLI agar gizmo mencerminkan hitbox sebenarnya,
        // sehingga mudah di-align dengan efek visual tebasan.
        Vector3 offset = new Vector3(hitOffset.x, hitOffset.y, 0f);
        if (!player.isFacingRight)
            offset.x = -offset.x;

        Vector3 origin = player.transform.position + offset;
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
