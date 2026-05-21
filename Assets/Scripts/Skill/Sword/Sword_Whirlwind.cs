using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Sword_Whirlwind : MonoBehaviour, ISkill, IEnergySkill
{
    [Header("Whirlwind Settings")]
    public float radius = 2f;
    public float duration = 1.5f;
    public float damageMultiplier = 1.2f;
    public float hitInterval = 0.3f;

    [Header("Movement")]
    [Tooltip("Rasio kecepatan selama Whirlwind (0 = diam, 1 = normal).")]
    public float speedMultiplierWhileActive = 0.4f;

    [Header("Stagger / Knockback")]
    public float knockForce = 2.5f;
    public float staggerDuration = 0.35f;
    public float staggerCooldown = 0.7f;

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

    private Dictionary<CharacterBase, float> staggerTimers = new Dictionary<CharacterBase, float>();

    [Header("Energy")]
    [SerializeField, Min(0f)] private float energyCost = 10f;

    public float EnergyCost => energyCost;
    public bool PayEnergyInSkillBase => true;

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
        staggerTimers.Clear();
    }

    public void TriggerSkill(int slotIndex)
    {
        if (isActive)
            return;

        if (player == null || !player.CanAct())
            return;

        if (player.isAttacking)
            return;

        if (!HasEnoughEnergyToStart())
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

        if (!HasAnyEnergyLeft())
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

        staggerTimers.Clear();

        // SFX angin putar diputar satu kali saat Whirlwind pertama kali aktif.
        if (playWhirlwindSfxOnStart)
            PlayWhirlwindSfx();

        while (tDuration > 0f)
        {
            if (!HasAnyEnergyLeft())
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

            UpdateStaggerTimers();
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

            float dmg = player.attack * damageMultiplier;
            target.TakeDamage(dmg);
            hasHit = true;

            if (CanStagger(target))
            {
                Vector2 dir = (target.transform.position - player.transform.position).normalized;
                target.ApplyStagger(dir, knockForce, staggerDuration);
                staggerTimers[target] = staggerCooldown;
            }

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

    private void UpdateStaggerTimers()
    {
        var keys = new List<CharacterBase>(staggerTimers.Keys);
        foreach (CharacterBase cb in keys)
        {
            staggerTimers[cb] -= Time.deltaTime;
            if (staggerTimers[cb] <= 0f)
                staggerTimers.Remove(cb);
        }
    }

    private bool CanStagger(CharacterBase target)
    {
        return !staggerTimers.ContainsKey(target);
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
        staggerTimers.Clear();
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