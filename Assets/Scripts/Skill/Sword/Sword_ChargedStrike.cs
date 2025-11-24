using UnityEngine;
using System.Collections;

public class Sword_ChargedStrike : MonoBehaviour, ISkill
{
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

    private Player player;
    private SkillBase skillBase;
    private PlayerAnimation anim;

    private bool isCharging = false;
    private float chargeTimer = 0f;
    private bool showGizmo = false;
    private int mySlotIndex = 0;

    void Awake()
    {
        skillBase = GetComponentInParent<SkillBase>();
        player = GetComponentInParent<Player>();
        anim   = GetComponentInParent<PlayerAnimation>();
    }

    // ============================================================
    //   CAST (TRIGGER)
    //   🔥 SkillBase sudah menghitung CAST → Jangan hitung lagi.
    // ============================================================
    public void TriggerSkill(int slotIndex)
    {
        if (isCharging) return;

        mySlotIndex = slotIndex;

        // ❌ Dihapus — CAST sudah dilakukan oleh SkillBase
        // skillBase.RegisterSkillCast(mySlotIndex);

        StartCoroutine(ChargeRoutine());
    }

    private IEnumerator ChargeRoutine()
    {
        isCharging = true;
        showGizmo = true;
        chargeTimer = 0f;

        // if (skillBase != null)
        //     skillBase.skillLocked = true;

        KeyCode holdKey = skillBase.slot2Key;

        while (Input.GetKey(holdKey))
        {
            chargeTimer += Time.deltaTime;
            chargeTimer = Mathf.Clamp(chargeTimer, 0, maxChargeTime);
            yield return null;
        }

        showGizmo = false;
        isCharging = false;

        float chargePercent = chargeTimer / maxChargeTime;
        float multiplier = Mathf.Lerp(minDamageMultiplier, maxDamageMultiplier, chargePercent);

        multiplier = Mathf.Round(multiplier);

        PerformChargedStrike(multiplier);

        // if (skillBase != null)
        //     skillBase.ReleaseLock();
    }

    // ============================================================
    //   HIT — DAMAGE ONLY (NO DDA)
    // ============================================================
    private void PerformChargedStrike(float multiplier)
    {
        if (!player) return;

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

                // Damage only — tidak memicu count Offensive
                target.TakeDamage(damage, null);

                Vector2 knockDir = (target.transform.position - origin).normalized;
                target.ApplyStagger(knockDir, knockbackForce, stunDuration);
                
            }
        }
    }

    void OnDisable()
    {
        // if (skillBase != null)
        //     skillBase.ReleaseLock();

        isCharging = false;
        showGizmo = false;
        chargeTimer = 0f;
    }
    #if UNITY_EDITOR
void OnDrawGizmos()
{
    if (!showGizmo || player == null) 
        return;

    Vector3 origin = player.transform.position;
    Vector3 dir = player.isFacingRight ? Vector3.right : Vector3.left;

    Gizmos.color = gizmoColor;

    float startAngle = -gizmoAngle * 0.5f;
    float step = gizmoAngle / Mathf.Max(1, gizmoSegments);

    // Titik awal garis
    Vector3 prev = origin + Quaternion.Euler(0, 0, startAngle) * dir * gizmoRadius;

    for (int i = 1; i <= gizmoSegments; i++)
    {
        float ang = startAngle + step * i;
        Vector3 next = origin + Quaternion.Euler(0, 0, ang) * dir * gizmoRadius;

        Gizmos.DrawLine(prev, next);
        prev = next;
    }
}
#endif
}
