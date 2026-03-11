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

    // ============================================================
    // ENERGY (dibayar di SkillBase saat OnPress)
    // ============================================================
    [Header("Energy")]
    [SerializeField, Min(0f)] private float energyCost = 10f;

    public float EnergyCost => energyCost;

    // Skill normal: energi dipotong di SkillBase ketika slot ditekan (OnPress)
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
        // Hentikan coroutine pada skill ini (tidak memakai StopAllCoroutines agar tidak mematikan coroutine lain)
        StopAllCoroutines();

        // Pulihkan state player + movement
        if (player != null)
        {
            player.moveSpeed = originalSpeed;
            player.isAttacking = false;
        }

        if (mover != null)
            mover.UnlockExternal();

        // Catatan: animasi di-reset bila Anda punya fungsi khusus.
        // Jika tidak ada, setidaknya tidak memanggil PlayWhirlwind lagi.
        // (Bila Anda punya ResetWhirlwind/ResetSlashFlags, silakan panggil di sini.)
        // if (anim != null) anim.ResetSlashFlags();

        isActive = false;
        staggerTimers.Clear();
    }

    public void TriggerSkill(int slotIndex)
    {
        // Sudah sedang Whirlwind → tolak
        if (isActive)
            return;

        // Tidak ada player atau tidak boleh bertindak → tolak
        if (player == null || !player.CanAct())
            return;

        // Selama sedang melakukan serangan besar lain → tolak
        if (player.isAttacking)
            return;

        // FAIL-SAFE: kalau ada jalur lain memanggil TriggerSkill tanpa melewati SkillBase gating
        if (!HasEnoughEnergyToStart())
        {
            DebugHub.Warning($"ENERGY KURANG: Whirlwind butuh {energyCost}.");
            return;
        }

        StartCoroutine(WhirlwindRoutine());
    }

    private IEnumerator WhirlwindRoutine()
    {
        isActive = true;
        tDuration = duration;
        tHit = 0f;

        // Hard stop jika energi sudah habis saat baru mulai (kasus edge)
        if (!HasAnyEnergyLeft())
        {
            ForceStopWhirlwind();
            yield break;
        }

        // Simpan kecepatan awal & set flag menyerang
        if (player != null)
        {
            originalSpeed = player.moveSpeed;
            player.moveSpeed *= speedMultiplierWhileActive;
            player.isAttacking = true;
        }

        // Lock input gerak agar tidak bisa jalan/dash via MoveKeyboard
        if (mover != null)
            mover.LockExternal(duration, true);

        // Mainkan animasi
        if (anim != null)
            anim.PlayWhirlwind();

        staggerTimers.Clear();

        while (tDuration > 0f)
        {
            // Jika energi habis di tengah, berhenti total (aturan keras)
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

        // Pulihkan kecepatan & flag menyerang, unlock movement
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

        foreach (Collider2D h in hits)
        {
            CharacterBase target = h.GetComponent<CharacterBase>();
            if (target == null || target == player)
                continue;

            float dmg = player.attack * damageMultiplier;
            target.TakeDamage(dmg);

            if (CanStagger(target))
            {
                Vector2 dir = (target.transform.position - player.transform.position).normalized;
                target.ApplyStagger(dir, knockForce, staggerDuration);
                staggerTimers[target] = staggerCooldown;
            }
        }
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
        // Pastikan state dipulihkan jika object dimatikan saat Whirlwind aktif
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