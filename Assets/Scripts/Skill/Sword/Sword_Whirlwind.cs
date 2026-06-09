using UnityEngine;
using System.Collections;

public class Sword_Whirlwind : MonoBehaviour, ISkill, IEnergySkill, ISkillCooldownInfo, ISkillReadinessInfo
{
    [Header("Whirlwind Settings")]
    public float radius = 2f;
    public float duration = 1.5f;
    public float hitInterval = 0.3f;

    [Header("Damage / Tuning")]
    [Tooltip("Atur angka damage flat per tick Whirlwind di sini.\n" +
             "Damage = angka pasti, TIDAK dikali player.attack (sama seperti damageQuickShot).")]
    [SerializeField] private SwordWhirlwindDamage damageConfig = new SwordWhirlwindDamage();

    [Header("Movement")]
    [Tooltip("Rasio kecepatan selama Whirlwind (0 = diam, 1 = normal).")]
    public float speedMultiplierWhileActive = 0.4f;

    [Header("Stagger / Tuning")]
    [Tooltip("Atur stagger tiap tick Whirlwind.\nImmunity duration diatur terpusat di StaggerCooldownSettings asset.")]
    [SerializeField] private SkillStaggerConfig staggerConfig = new SkillStaggerConfig
    {
        knockbackForce = 2.5f,
        staggerDuration = 0.35f
    };

    [Header("SFX Timing")]
    [Tooltip("Aktifkan jika SFX Whirlwind ingin dikendalikan dari script ini, bukan dari Animation Event.")]
    public bool playSfxFromScript = true;

    [Tooltip("Suara Whirlwind diputar satu kali saat skill pertama kali aktif.")]
    public bool playWhirlwindSfxOnStart = true;

    [Tooltip("Suara hit hanya dimainkan satu kali untuk satu tick damage, walaupun musuh yang terkena lebih dari satu.")]
    public bool playHitSfxOncePerTick = true;

    private Player player;
    private PlayerAnimation anim;
    private MoveKeyboard mover;
    private SkillBase skillBase;
    private CharacterBase character;

    private bool isActive = false;
    private float tDuration;
    private float tHit;
    private float originalSpeed;

    [Header("Energy")]
    [SerializeField, Min(0f)] private float energyCost = 10f;

    public float EnergyCost => energyCost;
    public bool PayEnergyInSkillBase => true;

    // Data baca untuk SkillIndexHUDController.
    // Whirlwind tidak menambah cooldown baru. Durasi visual mengikuti duration yang sudah ada.
    public bool HasCooldown => duration > 0.05f;
    public float CooldownDuration => Mathf.Max(0f, duration);
    public float CooldownRemaining => isActive ? Mathf.Max(0f, tDuration) : 0f;
    public bool IsCooldownReady => !isActive && CooldownRemaining <= 0.05f;
    public bool IsSkillBusy => isActive;
    public bool IsSkillReady => !isActive && player != null && player.CanAct() && HasEnoughEnergyToStart();
    public bool isCasting => isActive;
    public float cooldownDuration => CooldownDuration;

    private void Awake()
    {
        player = GetComponentInParent<Player>();
        anim = GetComponentInParent<PlayerAnimation>();
        mover = GetComponentInParent<MoveKeyboard>();
        skillBase = GetComponentInParent<SkillBase>();
        character = GetComponentInParent<CharacterBase>();
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

    private bool ShouldStopBecauseEnergyEmpty()
    {
        // Jika energy sudah dibayar oleh SkillBase, Whirlwind tetap boleh berjalan
        // walaupun sisa energy menjadi 0. Ini mencegah skill batal setelah pembayaran yang sah.
        if (PayEnergyInSkillBase && skillBase != null)
            return false;

        return !HasAnyEnergyLeft();
    }

    private void ForceStopWhirlwind()
    {
        StopAllCoroutines();

        if (player != null)
        {
            player.moveSpeed = originalSpeed;
            player.isAttacking = false;
        }

        if (mover != null)
            mover.UnlockExternal();

        isActive = false;
    }

    public void TriggerSkill(int slotIndex)
    {
        if (isActive)
            return;

        if (player == null || !player.CanAct())
            return;

        if (player.isAttacking)
            return;

        if (skillBase == null && !HasEnoughEnergyToStart())
        {
            DebugHub.Warning($"ENERGY KURANG: Whirlwind butuh {energyCost}.");
            return;
        }

        if (DataTracker.Instance != null)
            DataTracker.Instance.RecordSwordWhirlwind();

        StartCoroutine(WhirlwindRoutine());
    }

    private IEnumerator WhirlwindRoutine()
    {
        if (player != null)
            originalSpeed = player.moveSpeed;

        isActive = true;
        tDuration = duration;
        tHit = 0f;

        SkillIndexHUDController.NotifyGlobalSkillUsed(this);

        if (ShouldStopBecauseEnergyEmpty())
        {
            ForceStopWhirlwind();
            yield break;
        }

        if (player != null)
        {
            player.moveSpeed = originalSpeed * speedMultiplierWhileActive;
            player.isAttacking = true;
        }

        if (mover != null)
            mover.LockExternal(duration, true);

        if (anim != null)
            anim.PlayWhirlwind();

        // SFX angin putar diputar satu kali saat Whirlwind pertama kali aktif.
        if (playWhirlwindSfxOnStart)
            PlayWhirlwindSfx();

        while (tDuration > 0f)
        {
            if (ShouldStopBecauseEnergyEmpty())
            {
                ForceStopWhirlwind();
                yield break;
            }

            tDuration -= Time.deltaTime;
            tHit -= Time.deltaTime;

            if (tHit <= 0f)
            {
                ApplyWhirlwindDamage();
                tHit = hitInterval;
            }

            yield return null;
        }

        if (player != null)
        {
            player.moveSpeed = originalSpeed;
            player.isAttacking = false;
        }

        if (mover != null)
            mover.UnlockExternal();

        isActive = false;
    }

    private void ApplyWhirlwindDamage()
    {
        if (player == null) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(player.transform.position, radius);
        bool hasHit = false;

        foreach (Collider2D h in hits)
        {
            CharacterBase target = h.GetComponent<CharacterBase>();
            if (target == null || target == player)
                continue;

            float dmg = damageConfig.damagePerTick;
            target.TakeDamage(dmg);
            hasHit = true;

            Vector2 dir = (target.transform.position - player.transform.position).normalized;
            staggerConfig.Apply(target, dir);

            if (!playHitSfxOncePerTick)
                PlayHitSfx();
        }

        // SFX hit hanya diputar jika tick damage benar-benar mengenai minimal satu musuh.
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

    private void OnDisable()
    {
        if (player != null)
        {
            player.moveSpeed = originalSpeed;
            player.isAttacking = false;
        }

        if (mover != null)
            mover.UnlockExternal();

        isActive = false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying)
            return;

        if (!isActive)
            return;

        if (player == null)
            player = GetComponentInParent<Player>();
        if (player == null)
            return;

        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.4f);
        Gizmos.DrawWireSphere(player.transform.position, radius);
    }
#endif
}