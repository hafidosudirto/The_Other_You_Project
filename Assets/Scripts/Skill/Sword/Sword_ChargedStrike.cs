using UnityEngine;
using System.Collections;

public class Sword_ChargedStrike : MonoBehaviour, ISkill, IEnergySkill
{
    // ============================================================
    //  ENERGY (SPECIAL CASE: PAY ON RELEASE)
    // ============================================================
    [Header("Energy (ChargedStrike pays on Release)")]
    [SerializeField, Min(0f)] private float energyCost = 25f;

    public float EnergyCost => energyCost;

    // ChargedStrike: dipotong di dalam skill saat tombol dilepas (bukan oleh SkillBase saat tombol ditekan).
    public bool PayEnergyInSkillBase => false;

    // ============================================================
    //  SETTINGS
    // ============================================================
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

    [Header("Gizmo (Shown on Strike Damage)")]
    public Color gizmoColor = new Color(1f, 0.6f, 0f);
    public float gizmoRadius = 1.4f;
    public float gizmoAngle = 100f;
    public int gizmoSegments = 20;

    [Tooltip("Berapa lama gizmo tampil setelah strike damage dieksekusi (detik).")]
    public float strikeGizmoShowTime = 0.08f;

    // ============================================================
    //  INTERNAL
    // ============================================================
    private Player player;
    private PlayerAnimation anim;
    private MoveKeyboard mover;
    private SkillBase skillBase;

    private Animator unityAnimator;

    private bool isCharging = false;
    private float chargeTimer = 0f;

    // Gizmo hanya untuk momen strike damage
    private bool showGizmo = false;
    private Coroutine gizmoRoutine;

    private int mySlotIndex = 0;
    private Coroutine runningRoutine;

    // ============================================================
    void Awake()
    {
        player = GetComponentInParent<Player>();
        anim = GetComponentInParent<PlayerAnimation>();
        mover = GetComponentInParent<MoveKeyboard>();
        skillBase = GetComponentInParent<SkillBase>();

        // Coba ambil Animator dari parent. Jika PlayerAnimation punya referensi animator, pakai itu.
        unityAnimator = GetComponentInParent<Animator>();
        if (unityAnimator == null && anim != null && anim.animator != null)
            unityAnimator = anim.animator;
    }

    // ============================================================
    //  SKILL TRIGGER
    // ============================================================
    public void TriggerSkill(int slotIndex)
    {
        // FAIL-SAFE: jika energi sudah 0 sebelum charge
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

    // ============================================================
    //  CHARGE ROUTINE
    // ============================================================
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

        // Lock movement while charging (nilai besar; nanti di-strike diganti durasi klip)
        if (mover != null)
            mover.LockExternal(999f);

        // Play charging animation
        if (anim != null)
            anim.SetCharging(true);

        // Pastikan animator sempat update
        yield return null;

        while (Input.GetKey(holdKey))
        {
            chargeTimer += Time.deltaTime;
            chargeTimer = Mathf.Clamp(chargeTimer, 0f, maxChargeTime);
            yield return null;
        }

        // RELEASE
        isCharging = false;

        // Stop charging -> start strike anim
        if (anim != null)
            anim.SetCharging(false);

        // ============================================================
        //  ENERGY SPEND ON RELEASE (FINAL DECISION)
        // ============================================================

        if (!TrySpendEnergyOnRelease())
        {
            // Energi tidak cukup -> batalkan strike, reset state, dan lepaskan lock
            CancelAfterInsufficientEnergy();
            runningRoutine = null;
            yield break;
        }

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
            return false; // ✅ FAIL-CLOSED
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
        if (mover != null)
            mover.UnlockExternal(); // lebih aman daripada LockExternal(0f)

        if (player != null)
            player.isAttacking = false;

        if (anim != null)
            anim.SetCharging(false);

        isCharging = false;
        chargeTimer = 0f;
    }

    // ============================================================
    //  STRIKE ROUTINE (SYNC TO ANIM CLIP LENGTH)
    // ============================================================
    private IEnumerator StrikeRoutine(float multiplier)
    {
        // Tunggu agar transisi dari charging -> strike benar-benar masuk state strike
        yield return null;
        yield return null;

        float clipLen = GetCurrentClipLengthOrFallback();
        if (clipLen <= 0f) clipLen = strikeClipFallbackLength;

        float startP = Mathf.Clamp01(strikeActiveStart);
        float endP = Mathf.Clamp01(strikeActiveEnd);
        if (endP < startP) endP = startP;

        float startT = clipLen * startP;
        float endT = clipLen * endP;

        // Lock movement selama strike clip
        if (mover != null)
            mover.LockExternal(clipLen);

        if (startT > 0f)
            yield return new WaitForSeconds(startT);

        // Pada momen ini damage dieksekusi (dan gizmo akan menyala dari dalam PerformChargedStrike)
        PerformChargedStrike(multiplier);

        // Tahan sampai akhir window (opsional)
        float activeDur = Mathf.Max(0f, endT - startT);
        if (activeDur > 0f)
            yield return new WaitForSeconds(activeDur);

        // Tahan sampai animasi selesai agar isAttacking tidak turun terlalu cepat
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

    // ============================================================
    //  STRIKE DAMAGE (GIZMO DI-AKTIFKAN DI SINI)
    // ============================================================
    private void PerformChargedStrike(float multiplier)
    {
        if (!player) return;

        // === INI YANG ANDA MINTA: gizmo dinyalakan saat strike damage dieksekusi ===
        ShowStrikeGizmoBriefly();

        Vector3 origin = player.transform.position;
        Vector3 dir = player.isFacingRight ? Vector3.right : Vector3.left;

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, attackRadius);

        foreach (Collider2D hit in hits)
        {
            CharacterBase target = hit.GetComponent<CharacterBase>();
            if (!target || target == player) continue;

            Vector2 toTarget = (target.transform.position - origin).normalized;
            float angle = Vector2.Angle(dir, toTarget);

            if (angle <= attackAngle * 0.5f)
            {
                float damage = player.attack * multiplier;

                // Damage only — tidak menaikkan DDA offensive
                target.TakeDamage(damage, null);

                Vector2 knockDir = (target.transform.position - origin).normalized;
                target.ApplyStagger(knockDir, knockbackForce, stunDuration);
            }
        }
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

    // ============================================================
    //  SAFETY RESET
    // ============================================================
    private void OnDisable()
    {
        StopAllCoroutines();
        runningRoutine = null;
        gizmoRoutine = null;

        ResetAllState();
    }

    private void ResetAllState()
    {
        isCharging = false;
        chargeTimer = 0f;

        showGizmo = false;

        if (anim != null)
            anim.SetCharging(false);

        if (player != null)
            player.isAttacking = false;
    }

    // ============================================================
    //  GIZMO DRAW
    // ============================================================
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showGizmo || player == null)
            return;

        Vector3 origin = player.transform.position;
        Vector3 dir = player.isFacingRight ? Vector3.right : Vector3.left;

        Gizmos.color = gizmoColor;

        // Anda bisa memilih pakai gizmoRadius/gizmoAngle atau pakai attackRadius/attackAngle.
        // Agar konsisten dengan damage, saya pakai attackRadius/attackAngle.
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