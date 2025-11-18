using UnityEngine;
using System.Collections;

public class Sword_SlashCombo : MonoBehaviour, ISkill
{
    [Header("Slash Settings")]
    public float attackRadius = 1.5f;
    public float attackAngle = 90f;

    public float delaySlash1 = 0.1f;     // jeda sebelum hit 1
    public float delaySlash2 = 0.15f;    // jeda sebelum hit 2 (sesuaikan dengan anim)
    public float chainWindow = 0.45f;    // waktu tekan chain

    [Header("Hitbox / Gizmo Offset")]
    // offset dari posisi player ke depan pedang
    public Vector2 hitOffset = new Vector2(0.5f, 0f);

    [Header("Gizmos")]
    public Color chain1Color = Color.yellow;
    public Color chain2Color = Color.red;
    public int arcSegments = 20;

    private CharacterBase character;
    private SkillBase skillBase;
    private PlayerAnimation anim;

    private bool isBusy = false;
    private bool chainRequested = false;
    private int chain = 0; // 1 = slash1, 2 = slash2

    void Awake()
    {
        character   = GetComponentInParent<CharacterBase>();
        skillBase   = GetComponentInParent<SkillBase>();
        anim        = GetComponentInParent<PlayerAnimation>();

        if (!anim)
            Debug.LogWarning("Sword_SlashCombo: PlayerAnimation tidak ditemukan!");
    }

    public void TriggerSkill()
    {
        if (isBusy) return;
        if (!character || !character.CanAct()) return;

        StartCoroutine(ComboRoutine());
    }

    private IEnumerator ComboRoutine()
    {
        isBusy = true;
        chain  = 1;

        // Lock movement / aksi lain
        if (skillBase != null)
            skillBase.SendMessage("LockAction", SendMessageOptions.DontRequireReceiver);

        // ============================
        //           SLASH 1
        // ============================
        anim.PlaySlash1();

        yield return new WaitForSeconds(delaySlash1);
        PerformSlash();              // HIT 1

        // tunggu input chain
        yield return StartCoroutine(WaitChainInput());
        if (!chainRequested)
        {
            EndCombo();
            yield break;
        }

        // ============================
        //           SLASH 2
        // ============================
        chain = 2;

        anim.PlaySlash2();

        // tunggu timing pedang nyabet di animasi Slash2
        yield return new WaitForSeconds(delaySlash2);
        PerformSlash();              // HIT 2

        EndCombo();
    }

    private IEnumerator WaitChainInput()
    {
        chainRequested = false;
        float timer = 0f;

        while (timer < chainWindow)
        {
            // tekan angka 1 untuk chain
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                chainRequested = true;
                yield break;
            }

            timer += Time.deltaTime;
            yield return null;
        }
    }

    // ======================
    // HIT DETECTION
    // ======================
    private void PerformSlash()
    {
        if (!character) return;

        // origin + offset ke depan pedang
        Vector3 offset = new Vector3(hitOffset.x, hitOffset.y, 0f);
        if (!character.isFacingRight)
            offset.x = -offset.x;

        Vector3 origin = character.transform.position + offset;
        Vector3 dir    = character.isFacingRight ? Vector3.right : Vector3.left;

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, attackRadius);

        foreach (var h in hits)
        {
            var target = h.GetComponent<CharacterBase>();
            if (!target || target == character) continue;

            Vector2 toTarget    = (target.transform.position - origin).normalized;
            float   angleBetween = Vector2.Angle(dir, toTarget);

            if (angleBetween <= attackAngle * 0.5f)
                target.TakeDamage(character.attack);
        }
    }

    private void EndCombo()
    {
        isBusy = false;
        chain  = 0;

        if (skillBase != null)
            skillBase.SendMessage("ReleaseLock", SendMessageOptions.DontRequireReceiver);
    }

#if UNITY_EDITOR
void OnDrawGizmos()
{
    if (!character || !anim || !anim.animator) return;

    // Cek state animasi
    var state = anim.animator.GetCurrentAnimatorStateInfo(0);

    bool isSlash1 = state.IsName("Slash_Combo1 0");
    bool isSlash2 = state.IsName("Slash_Combo2 0");

    if (!isSlash1 && !isSlash2)
        return; // ❌ Tidak sedang animasi slash → Gizmo tidak tampil

    // Pilih warna
    Gizmos.color = isSlash1 ? chain1Color : chain2Color;

    // offset hitbox
    Vector3 offset = new Vector3(hitOffset.x, hitOffset.y, 0f);
    if (!character.isFacingRight)
        offset.x = -offset.x;

    Vector3 origin = character.transform.position + offset;
    Vector3 dir    = character.isFacingRight ? Vector3.right : Vector3.left;

    // Hit arc
    float startAngle = -attackAngle * 0.5f;
    float stepAngle  = attackAngle / Mathf.Max(1, arcSegments);

    Vector3 prev = origin + Quaternion.Euler(0, 0, startAngle) * dir * attackRadius;

    for (int i = 1; i <= arcSegments; i++)
    {
        float ang  = startAngle + stepAngle * i;
        Vector3 nxt = origin + Quaternion.Euler(0, 0, ang) * dir * attackRadius;

        Gizmos.DrawLine(prev, nxt);
        prev = nxt;
    }
}
#endif
}