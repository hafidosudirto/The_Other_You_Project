using UnityEngine;
using System.Collections;

public class Sword_SlashCombo : MonoBehaviour, ISkill
{
    [Header("Slash Settings")]
    public float attackRadius = 1.5f;
    public float attackAngle = 90f;

    public float delaySlash1 = 0.1f;
    public float delaySlash2 = 0.15f;
    public float chainWindow = 0.45f;

    [Header("Hitbox / Gizmo Offset")]
    public Vector2 hitOffset = new Vector2(0.5f, 0f);

    [Header("Gizmos")]
    public Color chain1Color = Color.yellow;
    public Color chain2Color = Color.red;
    public int arcSegments = 20;

    private CharacterBase character;
    private SkillBase skillBase;
    private PlayerAnimation anim;

    [HideInInspector]
    public bool isBusy = false;
    private bool chainRequested = false;

    private int mySlotIndex = 1;

    // Flag untuk prefab tanpa animator
    private bool isSlash2 = false;

    void Awake()
    {
        character = GetComponentInParent<CharacterBase>();
        skillBase = GetComponentInParent<SkillBase>();
        anim      = GetComponentInParent<PlayerAnimation>();
    }

    public void TriggerSkill(int slotIndex)
    {
        if (isBusy) return;
        if (!character || !character.CanAct()) return;

        mySlotIndex = slotIndex;
        StartCoroutine(ComboRoutine());
        DataTracker.Instance.RecordAction(PlayerActionType.Offensive, WeaponType.Sword);

    }

    private IEnumerator ComboRoutine()
    {
        isBusy = true;
        isSlash2 = false;

        // if (skillBase != null)
        //     skillBase.skillLocked = true;

        // ====================
        // SLASH 1
        // ====================
        if (anim != null)
            anim.PlaySlash1();

        // CAST Slash1 → +0.5
        // if (skillBase != null)
        //     skillBase.RegisterSkillCast(mySlotIndex);

        yield return new WaitForSeconds(delaySlash1);
        PerformSlash();

        // ====================
        // WAIT FOR CHAIN
        // ====================
        yield return StartCoroutine(WaitChainInput());
        if (!chainRequested)
        {
            EndCombo();
            yield break;
        }

        // ====================
        // SLASH 2 (CHAIN)
        // ====================
        isSlash2 = true;

        // CAST Slash2 → +0.5
        // if (skillBase != null)
        //     skillBase.RegisterSkillCast(mySlotIndex);

        if (anim != null)
            anim.PlaySlash2();

        yield return new WaitForSeconds(delaySlash2);
        PerformSlash();

        EndCombo();
    }

    private IEnumerator WaitChainInput()
    {
        chainRequested = false;
        float timer = 0f;

        while (timer < chainWindow)
        {
            if (Input.GetKeyDown(skillBase.slot1Key))
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
        if (!character) return;

        Vector3 offset = new Vector3(hitOffset.x, hitOffset.y, 0f);
        if (!character.isFacingRight)
            offset.x = -offset.x;

        Vector3 origin = character.transform.position + offset;
        Vector3 dir = character.isFacingRight ? Vector3.right : Vector3.left;

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, attackRadius);

        foreach (var h in hits)
        {
            var target = h.GetComponent<CharacterBase>();
            if (!target || target == character) continue;

            Vector2 toTarget = (target.transform.position - origin).normalized;
            float angleBetween = Vector2.Angle(dir, toTarget);

            if (angleBetween <= attackAngle * 0.5f)
                target.TakeDamage(character.attack);
        }
    }

    private void EndCombo()
    {
        isBusy = false;
        isSlash2 = false;

        // if (skillBase != null)
        //     skillBase.ReleaseLock();
    }

    void OnDisable()
    {
        // if (skillBase != null)
        //     skillBase.ReleaseLock();

        isBusy = false;
        chainRequested = false;
        isSlash2 = false;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!character) return;

        bool show1 = false;
        bool show2 = false;

        // Jika ada animator
        if (anim != null && anim.animator != null)
        {
            var state = anim.animator.GetCurrentAnimatorStateInfo(0);

            show1 = state.IsName("Slash_Combo1") || state.IsName("Slash_Combo1 0");
            show2 = state.IsName("Slash_Combo2") || state.IsName("Slash_Combo2 0");
        }
        else
        {
            // Tanpa animator → pakai flag internal
            if (isBusy && !isSlash2) show1 = true;
            if (isBusy &&  isSlash2) show2 = true;
        }

        if (!show1 && !show2)
            return;

        Gizmos.color = show2 ? chain2Color : chain1Color;

        Vector3 offset = new Vector3(hitOffset.x, hitOffset.y, 0f);
        if (character && !character.isFacingRight)
            offset.x = -offset.x;

        Vector3 origin = character.transform.position + offset;
        Vector3 dir = (character && character.isFacingRight) ? Vector3.right : Vector3.left;

        float startAngle = -attackAngle * 0.5f;
        float step = attackAngle / Mathf.Max(1, arcSegments);

        Vector3 prev = origin + Quaternion.Euler(0, 0, startAngle) * dir * attackRadius;

        for (int i = 1; i <= arcSegments; i++)
        {
            float ang = startAngle + step * i;
            Vector3 next = origin + Quaternion.Euler(0, 0, ang) * dir * attackRadius;

            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
#endif
}
