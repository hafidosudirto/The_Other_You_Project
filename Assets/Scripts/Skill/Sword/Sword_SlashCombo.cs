using UnityEngine;
using System.Collections;

public class Sword_SlashCombo : MonoBehaviour, ISkill, IEnergySkill
{
    [Header("Slash1 Settings")]
    public float attackRadius1 = 1.5f;
    public float attackAngle1 = 90f;
    public Vector2 hitOffset1 = new Vector2(0.5f, 0f);

    [Header("Slash2 Settings")]
    public float attackRadius2 = 1.5f;
    public float attackAngle2 = 90f;
    public Vector2 hitOffset2 = new Vector2(0.5f, 0f);

    [Header("Timing Settings (detik)")]
    [Tooltip("Waktu dari awal animasi Slash1 sampai hitbox Slash1 aktif.")]
    public float delaySlash1 = 0.18f;

    [Tooltip("Waktu dari awal animasi Slash2 sampai hitbox Slash2 aktif.")]
    public float delaySlash2 = 0.22f;

    [Tooltip("Lama jendela input Slash2 SETELAH Slash1 mengenai (durasi combo window).")]
    public float chainWindow = 0.35f;

    [Header("Gizmos")]
    [Tooltip("Lama gizmo arc tampil setelah hit (detik).")]
    public float gizmoShowTime = 0.06f;
    public Color chain1Color = Color.yellow;
    public Color chain2Color = Color.white;
    public int arcSegments = 20;

    [Header("Combo Cooldown")]
    [Tooltip("Jeda setelah satu combo selesai sebelum bisa cast lagi (0 = boleh langsung ulang).")]
    public float comboCooldown = 0f;

    [Tooltip("Jeda minimal setelah Slash1 mengenai sebelum input Slash2 dianggap valid.\n"
           + "Kalau terlalu kecil, spam sangat cepat bisa langsung menghabisi combo.\n"
           + "Kalau terlalu besar, Slash2 terasa telat.")]
    public float minTimeBeforeChain = 0.09f;

    private CharacterBase character;
    private SkillBase skillBase;
    private PlayerAnimation anim;
    private MoveKeyboard mover;
    private Player player;

    private bool isBusy = false;
    private bool chainRequested = false;
    private bool isSlash2Phase = false;
    private bool bufferedChainInput = false;

    private bool showHitArc = false;

    private int mySlotIndex = 0;
    private float lastComboTime = -999f;

    [Header("Energy")]
    [SerializeField, Min(0f)] private float energyCost = 10f;

    public float EnergyCost => energyCost;
    public bool PayEnergyInSkillBase => true;

    private void OnValidate()
    {
        if (gizmoShowTime >= minTimeBeforeChain)
            gizmoShowTime = Mathf.Max(0f, minTimeBeforeChain - 0.01f);

        energyCost = Mathf.Max(0f, energyCost);
    }

    private void Awake()
    {
        character = GetComponentInParent<CharacterBase>();
        skillBase = GetComponentInParent<SkillBase>();
        anim = GetComponentInParent<PlayerAnimation>();
        mover = GetComponentInParent<MoveKeyboard>();
        player = GetComponentInParent<Player>();
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

    private void ForceStopCombo()
    {
        StopAllCoroutines();

        isBusy = false;
        chainRequested = false;
        isSlash2Phase = false;
        bufferedChainInput = false;
        showHitArc = false;

        if (anim != null)
            anim.ResetSlashFlags();

        if (player != null)
            player.isAttacking = false;
    }

    public void TriggerSkill(int slotIndex)
    {
        if (isBusy)
        {
            if (!isSlash2Phase && slotIndex == mySlotIndex)
                bufferedChainInput = true;
            return;
        }

        if (Time.time < lastComboTime + comboCooldown)
            return;

        if (player != null && player.isAttacking)
            return;

        if (character == null || !character.CanAct())
            return;

        if (!HasEnoughEnergyToStart())
        {
            DebugHub.Warning($"ENERGY KURANG: Slash Combo butuh {energyCost}.");
            return;
        }

        mySlotIndex = slotIndex;
        StartCoroutine(ComboRoutine());

        if (DataTracker.Instance != null)
            DataTracker.Instance.RecordSwordSlashCombo();
    }

    private IEnumerator ComboRoutine()
    {
        isBusy = true;
        isSlash2Phase = false;
        chainRequested = false;
        bufferedChainInput = false;

        if (!HasAnyEnergyLeft())
        {
            ForceStopCombo();
            yield break;
        }

        if (player != null)
            player.isAttacking = true;

        float slash1LockDuration = delaySlash1 + chainWindow * 0.7f;

        if (mover != null)
            mover.TriggerSlash1(slash1LockDuration);

        if (anim != null)
            anim.SetSlash1(true);

        if (skillBase != null)
            DebugHub.Skill("[SlashCombo] Slash1 CAST");

        if (!HasAnyEnergyLeft())
        {
            ForceStopCombo();
            yield break;
        }

        yield return new WaitForSeconds(delaySlash1);

        if (!HasAnyEnergyLeft())
        {
            ForceStopCombo();
            yield break;
        }

        PerformSlash();

        yield return new WaitForSeconds(minTimeBeforeChain);
        yield return StartCoroutine(WaitChainInput_AutoBuffer());

        if (anim != null)
            anim.SetSlash1(false);

        if (!chainRequested)
        {
            yield return new WaitForSeconds(0.05f);

            if (anim != null)
                anim.ResetSlashFlags();

            isBusy = false;
            isSlash2Phase = false;
            bufferedChainInput = false;

            if (player != null)
                player.isAttacking = false;

            lastComboTime = Time.time;
            yield break;
        }

        if (!HasAnyEnergyLeft())
        {
            ForceStopCombo();
            lastComboTime = Time.time;
            yield break;
        }

        isSlash2Phase = true;

        float slash2LockDuration = delaySlash2 + 0.3f;

        if (mover != null)
            mover.TriggerSlash2(slash2LockDuration);

        if (anim != null)
            anim.SetSlash2(true);

        if (skillBase != null)
            DebugHub.Skill("[SlashCombo] Slash2 CAST");

        yield return new WaitForSeconds(delaySlash2);

        if (!HasAnyEnergyLeft())
        {
            ForceStopCombo();
            lastComboTime = Time.time;
            yield break;
        }

        PerformSlash();

        yield return new WaitForSeconds(0.05f);

        if (anim != null)
            anim.ResetSlashFlags();

        isBusy = false;
        isSlash2Phase = false;
        bufferedChainInput = false;

        if (player != null)
            player.isAttacking = false;

        lastComboTime = Time.time;
    }

    private KeyCode GetMyKey()
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

    private IEnumerator WaitChainInput_AutoBuffer()
    {
        chainRequested = false;
        float timer = 0f;

        KeyCode comboKey = GetMyKey();

        if (bufferedChainInput)
        {
            chainRequested = true;
            bufferedChainInput = false;
            yield break;
        }

        while (timer < chainWindow)
        {
            if (!HasAnyEnergyLeft())
                yield break;

            if (comboKey != KeyCode.None && Input.GetKeyDown(comboKey))
            {
                chainRequested = true;
                yield break;
            }

            timer += Time.deltaTime;
            yield return null;
        }
    }

    private void PerformSlash()
    {
        if (character == null) return;

        float radius = isSlash2Phase ? attackRadius2 : attackRadius1;
        float angle = isSlash2Phase ? attackAngle2 : attackAngle1;
        Vector2 offset2D = isSlash2Phase ? hitOffset2 : hitOffset1;

        Vector3 offset = new Vector3(offset2D.x, offset2D.y, 0f);
        if (!character.isFacingRight)
            offset.x = -offset.x;

        Vector3 origin = character.transform.position + offset;
        Vector3 dir = character.isFacingRight ? Vector3.right : Vector3.left;

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, radius);

        foreach (var h in hits)
        {
            var target = h.GetComponent<CharacterBase>();
            if (!target || target == character) continue;

            Vector2 toTarget = (target.transform.position - origin).normalized;
            float angleBetween = Vector2.Angle(dir, toTarget);

            if (angleBetween <= angle * 0.5f)
                target.TakeDamage(character.attack);
        }

        if (gameObject.activeInHierarchy)
            StartCoroutine(ShowHitArcWindow());
    }

    private IEnumerator ShowHitArcWindow()
    {
        showHitArc = true;
        yield return new WaitForSeconds(gizmoShowTime);
        showHitArc = false;
    }

    private void OnDisable()
    {
        ForceStopCombo();
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        if (!isBusy || !showHitArc) return;

        if (!character)
        {
            character = GetComponentInParent<CharacterBase>();
            if (!character) return;
        }

        Gizmos.color = isSlash2Phase ? chain2Color : chain1Color;

        float radius = isSlash2Phase ? attackRadius2 : attackRadius1;
        float angle = isSlash2Phase ? attackAngle2 : attackAngle1;
        Vector2 offset2D = isSlash2Phase ? hitOffset2 : hitOffset1;

        Vector3 offset = new Vector3(offset2D.x, offset2D.y, 0f);
        if (!character.isFacingRight)
            offset.x = -offset.x;

        Vector3 origin = character.transform.position + offset;
        Vector3 dir = character.isFacingRight ? Vector3.right : Vector3.left;

        float startAngle = -angle * 0.5f;
        float step = angle / Mathf.Max(1, arcSegments);

        Vector3 prev = origin + Quaternion.Euler(0, 0, startAngle) * dir * radius;

        for (int i = 1; i <= arcSegments; i++)
        {
            float ang = startAngle + step * i;
            Vector3 next = origin + Quaternion.Euler(0, 0, ang) * dir * radius;
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
#endif
}
