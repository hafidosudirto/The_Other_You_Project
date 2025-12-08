using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Sword_Whirlwind : MonoBehaviour, ISkill
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

    private bool isActive = false;
    private float tDuration;
    private float tHit;
    private float originalSpeed;

    private Dictionary<CharacterBase, float> staggerTimers = new Dictionary<CharacterBase, float>();

    private void Awake()
    {
        player     = GetComponentInParent<Player>();
        anim       = GetComponentInParent<PlayerAnimation>();
        mover      = GetComponentInParent<MoveKeyboard>();
        skillBase  = GetComponentInParent<SkillBase>();
    }

    public void TriggerSkill(int slotIndex)
    {
        // Sudah sedang Whirlwind → tolak
        if (isActive)
            return;

        // Tidak ada player atau tidak boleh bertindak → tolak
        if (player == null || !player.CanAct())
            return;

        // Selama sedang melakukan serangan besar lain, Whirlwind dimatikan
        if (player.isAttacking)
            return;

        StartCoroutine(WhirlwindRoutine());
    }

    private IEnumerator WhirlwindRoutine()
    {
        isActive   = true;
        tDuration  = duration;
        tHit       = 0f;

        // Simpan kecepatan awal & set flag menyerang
        if (player != null)
        {
            originalSpeed       = player.moveSpeed;
            player.moveSpeed   *= speedMultiplierWhileActive;
            player.isAttacking  = true;
        }

        // Lock input gerak agar tidak bisa jalan/dash via MoveKeyboard
        if (mover != null)
            mover.LockExternal(duration, true);

        // Mainkan animasi
        if (anim != null)
            anim.PlayWhirlwind();

        // Reset dictionary stagger timer
        staggerTimers.Clear();

        while (tDuration > 0f)
        {
            tDuration -= Time.deltaTime;
            tHit      -= Time.deltaTime;

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
            player.moveSpeed    = originalSpeed;
            player.isAttacking  = false;
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

            // Damage
            float dmg = player.attack * damageMultiplier;
            target.TakeDamage(dmg);

            // Knockback + stagger dengan cooldown
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
        if (player != null)
        {
            player.moveSpeed   = originalSpeed;
            player.isAttacking = false;
        }

        isActive = false;
        staggerTimers.Clear();
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Saat tidak play, tidak usah gambar apa-apa
        if (!Application.isPlaying)
            return;

        // Saat play, hanya tampilkan gizmo kalau Whirlwind sedang aktif
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
