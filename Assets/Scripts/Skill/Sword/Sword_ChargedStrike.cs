using UnityEngine;
using System.Collections;

public class Sword_ChargedStrike : MonoBehaviour, ISkill
{
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

    [Header("Gizmo Settings")]
    public Color gizmoColor = new Color(1f, 0.6f, 0f);
    public float gizmoRadius = 1.4f;
    public float gizmoAngle = 100f;
    public int gizmoSegments = 20;

    // ============================================================
    //  INTERNAL
    // ============================================================
    private Player player;
    private SkillBase skillBase;
    private PlayerAnimation anim;
    private MoveKeyboard mover;

    private bool isCharging = false;
    private float chargeTimer = 0f;
    private bool showGizmo = false;
    private int mySlotIndex = 0;

    // ============================================================
    void Awake()
    {
        player    = GetComponentInParent<Player>();
        anim      = GetComponentInParent<PlayerAnimation>();
        mover     = GetComponentInParent<MoveKeyboard>();
        skillBase = GetComponentInParent<SkillBase>();
    }

    // ============================================================
    //  TRIGGER BY SKILLBASE
    // ============================================================
    public void TriggerSkill(int slotIndex)
    {
        // Jangan mulai kalau sedang charge
        if (isCharging) return;

        // Hormati state global player
        if (player != null)
        {
            // Tidak bisa cast kalau player tidak boleh bertindak
            if (!player.CanAct())
                return;

            // Selama sedang melakukan serangan besar lain, ChargedStrike dimatikan
            if (player.isAttacking)
                return;
        }

        mySlotIndex = slotIndex;
        StartCoroutine(ChargeRoutine());
    }

    // Tombol mana yang harus ditahan, mengikuti index slot
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
    //  MAIN CHARGE ROUTINE
    // ============================================================
    private IEnumerator ChargeRoutine()
    {
        isCharging  = true;
        chargeTimer = 0f;
        showGizmo   = true;

        // Selama proses charge+strike, anggap player sedang attacking
        if (player != null)
            player.isAttacking = true;

        KeyCode holdKey = GetHoldKey();

        if (holdKey == KeyCode.None)
        {
            // Safety kalau konfigurasi salah
            isCharging = false;
            showGizmo  = false;

            if (anim != null)
                anim.SetCharging(false);

            if (player != null)
                player.isAttacking = false;

            yield break;
        }

        // 🔒 Lock movement while charging
        if (mover != null)
            mover.LockExternal(999f);

        // 🔥 Play charging animation (set bool isCharging = true di Animator)
        if (anim != null)
            anim.SetCharging(true);

        // Pastikan minimal 1 frame penuh dalam kondisi "charging"
        yield return null;

        // ==========================
        //  HOLD CHARGE
        // ==========================
        while (Input.GetKey(holdKey))
        {
            chargeTimer += Time.deltaTime;
            chargeTimer = Mathf.Clamp(chargeTimer, 0, maxChargeTime);
            yield return null;
        }

        // RELEASE INPUT
        isCharging = false;
        showGizmo  = false;

        // 🔥 STOP charging animation → START STRIKE animation
        if (anim != null)
            anim.SetCharging(false);

        // Hitbox multiplier
        float chargePercent = (maxChargeTime > 0f)
            ? chargeTimer / maxChargeTime
            : 1f;

        float multiplier = Mathf.Lerp(minDamageMultiplier, maxDamageMultiplier, chargePercent);
        multiplier = Mathf.Round(multiplier);

        PerformChargedStrike(multiplier);

        // Lock movement selama animasi strike
        if (mover != null)
            mover.LockExternal(0.35f);

        // Tahan flag attacking sampai animasi strike kurang-lebih selesai
        yield return new WaitForSeconds(0.35f);

        if (player != null)
            player.isAttacking = false;
    }

    // ============================================================
    //   STRIKE DAMAGE
    // ============================================================
    private void PerformChargedStrike(float multiplier)
    {
        if (!player) return;

        Vector3 origin = player.transform.position;
        Vector3 dir    = player.isFacingRight ? Vector3.right : Vector3.left;

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

    // ============================================================
    //   SAFETY RESET
    // ============================================================
    void OnDisable()
    {
        isCharging  = false;
        showGizmo   = false;
        chargeTimer = 0f;

        if (anim != null)
            anim.SetCharging(false);

        if (player != null)
            player.isAttacking = false;
    }

    // ============================================================
    //   GIZMO DRAW
    // ============================================================
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showGizmo || player == null)
            return;

        Vector3 origin = player.transform.position;
        Vector3 dir    = player.isFacingRight ? Vector3.right : Vector3.left;

        Gizmos.color = gizmoColor;

        float startAngle = -gizmoAngle * 0.5f;
        float step       = gizmoAngle / Mathf.Max(1, gizmoSegments);

        Vector3 prev = origin + Quaternion.Euler(0, 0, startAngle) * dir * gizmoRadius;

        for (int i = 1; i <= gizmoSegments; i++)
        {
            float ang  = startAngle + step * i;
            Vector3 next = origin + Quaternion.Euler(0, 0, ang) * dir * gizmoRadius;

            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
#endif
}
