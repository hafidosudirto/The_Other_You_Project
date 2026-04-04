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

        yield return null;

        while (Input.GetKey(holdKey))
        {
            chargeTimer += Time.deltaTime;
            chargeTimer = Mathf.Clamp(chargeTimer, 0f, maxChargeTime);
            yield return null;
        }

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
