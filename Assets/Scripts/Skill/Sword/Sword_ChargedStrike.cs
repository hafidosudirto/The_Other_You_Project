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

    private bool isCharging = false;
    private float chargeTimer = 0f;
    private bool showGizmo = false;

    void Awake()
    {
        skillBase = GetComponentInParent<SkillBase>();
        player = GetComponentInParent<Player>();
    }

    public void TriggerSkill()
    {
        if (isCharging) return;

        StartCoroutine(ChargeRoutine());
    }

    private IEnumerator ChargeRoutine()
    {
        isCharging = true;
        chargeTimer = 0f;
        showGizmo = true;

        Debug.Log("Mulai Charge Attack");

        // HOLD Key: selama Alpha2 ditekan, charge meningkat
        while (Input.GetKey(KeyCode.Alpha2))
        {
            chargeTimer += Time.deltaTime;
            chargeTimer = Mathf.Clamp(chargeTimer, 0, maxChargeTime);
            yield return null;
        }

        // KEY dilepas
        showGizmo = false;
        isCharging = false;

        float chargePercent = chargeTimer / maxChargeTime;
        float multiplier = Mathf.Lerp(minDamageMultiplier, maxDamageMultiplier, chargePercent);

        PerformChargedStrike(multiplier);

        // SANGAT PENTING: buka lock skill setelah serangan selesai
        if (skillBase != null)
            skillBase.ReleaseLock();
    }

    private void PerformChargedStrike(float multiplier)
    {
        if (player == null) return;

        Vector3 origin = player.transform.position;
        Vector3 dir = player.isFacingRight ? Vector3.right : Vector3.left;

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, attackRadius);

        foreach (Collider2D hit in hits)
        {
            CharacterBase target = hit.GetComponent<CharacterBase>();
            if (target == null) continue;
            if (target == player) continue;

            Vector3 toTarget = (target.transform.position - origin).normalized;
            float angle = Vector3.Angle(dir, toTarget);

            if (angle <= attackAngle * 0.5f)
            {
                float damage = player.attack * multiplier;

                // Damage
                target.TakeDamage(damage, player.gameObject);

                // Knockback + stun (fit for kinematic enemies)
                Vector2 knockDir = (target.transform.position - origin).normalized;
                target.ApplyStagger(knockDir, knockbackForce, stunDuration);

                Debug.Log("Charged Strike hit " + target.name +
                          " | Dmg Multiplier: " + multiplier +
                          " | Force: " + knockbackForce +
                          " | Stun: " + stunDuration);
            }
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!Application.isPlaying || !showGizmo)
            return;

        if (player == null)
            player = GetComponentInParent<Player>();

        Vector3 origin = player.transform.position;
        Vector3 dir = player.isFacingRight ? Vector3.right : Vector3.left;

        Gizmos.color = gizmoColor;

        float startAngle = -gizmoAngle * 0.5f;
        float step = gizmoAngle / gizmoSegments;

        Vector3 prev = origin + Quaternion.Euler(0, 0, startAngle) * dir * gizmoRadius;

        for (int i = 1; i <= gizmoSegments; i++)
        {
            float current = startAngle + step * i;
            Vector3 next = origin + Quaternion.Euler(0, 0, current) * dir * gizmoRadius;

            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
#endif
}
