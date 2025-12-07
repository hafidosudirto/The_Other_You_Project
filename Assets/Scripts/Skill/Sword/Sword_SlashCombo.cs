using UnityEngine;
using System.Collections;

public class Sword_SlashCombo : MonoBehaviour, ISkill
{
    [Header("Slash Settings")]
    public float attackRadius = 1.5f;
    public float attackAngle  = 90f;

    [Tooltip("Delay sebelum hit Slash1 kena musuh")]
    public float delaySlash1  = 0.1f;

    [Tooltip("Delay sebelum hit Slash2 kena musuh")]
    public float delaySlash2  = 0.15f;

    [Tooltip("Lama waktu untuk input combo setelah Slash1 mulai mengenai")]
    public float chainWindow  = 0.45f;

    [Header("Hitbox / Gizmo Offset")]
    public Vector2 hitOffset = new Vector2(0.5f, 0f);

    [Header("Gizmos")]
    [Tooltip("Lama gizmo arc tampil setelah hit (detik)")]
    public float gizmoShowTime = 0.08f;
    public Color chain1Color   = Color.yellow;
    public Color chain2Color   = Color.red;
    public int arcSegments     = 20;

    [Header("Combo Cooldown")]
    [Tooltip("Jeda setelah satu combo selesai sebelum bisa cast lagi")]
    public float comboCooldown = 0.2f;

    private CharacterBase   character;
    private SkillBase       skillBase;
    private PlayerAnimation anim;
    private MoveKeyboard    mover;
    private Player          player;

    private bool isBusy         = false;
    private bool chainRequested = false;
    private bool isSlash2Phase  = false;

    // Untuk gizmo window
    private bool showHitArc = false;

    // Slot skill ini dipasang di skillBase slot ke berapa
    private int   mySlotIndex   = 0;
    private float lastComboTime = -999f;

    void Awake()
    {
        character  = GetComponentInParent<CharacterBase>();
        skillBase  = GetComponentInParent<SkillBase>();
        anim       = GetComponentInParent<PlayerAnimation>();
        mover      = GetComponentInParent<MoveKeyboard>();
        player     = GetComponentInParent<Player>();
    }

    //=====================================================================
    // DIPANGGIL DARI SkillBase SAAT TOMBOL SLOT DITEKAN
    //=====================================================================
    public void TriggerSkill(int slotIndex)
    {
        // Sedang menjalankan combo → tolak
        if (isBusy)
            return;

        // Cooldown selesai?
        if (Time.time < lastComboTime + comboCooldown)
            return;

        if (!character || !character.CanAct())
            return;

        mySlotIndex = slotIndex;
        StartCoroutine(ComboRoutine());
    }

    //=====================================================================
    // LOGIKA KOMBO: SLASH1 -> (OPSIONAL) SLASH2
    //=====================================================================
    private IEnumerator ComboRoutine()
    {
        isBusy         = true;
        isSlash2Phase  = false;
        chainRequested = false;

        if (player != null)
            player.isAttacking = true;

        //------------------ SLASH 1 ------------------
        // Lock movement selama fase Slash1 + sedikit margin
        float slash1LockDuration = delaySlash1 + chainWindow * 0.7f;

        if (mover != null)
            mover.TriggerSlash1(slash1LockDuration);
        else if (anim != null)
            anim.SetSlash1(true);

        if (skillBase != null)
            DebugHub.Skill("[SlashCombo] Slash1 CAST");

        // Tunggu momen hit Slash1
        yield return new WaitForSeconds(delaySlash1);
        PerformSlash();

        //------------------ TUNGGU INPUT COMBO ------------------
        yield return StartCoroutine(WaitChainInput_AutoBuffer());

        if (anim != null)
            anim.SetSlash1(false);

        // Jika pemain TIDAK menekan input untuk Slash2
        if (!chainRequested)
        {
            // Sedikit waktu supaya animasi Slash1 selesai transisi ke Idle
            yield return new WaitForSeconds(0.05f);

            if (anim != null)
                anim.ResetSlashFlags();

            isBusy        = false;
            isSlash2Phase = false;

            if (player != null)
                player.isAttacking = false;

            lastComboTime = Time.time;
            yield break;
        }

        //------------------ SLASH 2 ------------------
        isSlash2Phase = true;

        // Lock movement selama Slash2 (lebih pendek, tapi tetap aman)
        float slash2LockDuration = delaySlash2 + 0.3f;

        if (mover != null)
            mover.TriggerSlash2(slash2LockDuration);
        else if (anim != null)
            anim.SetSlash2(true);

        if (skillBase != null)
            DebugHub.Skill("[SlashCombo] Slash2 CAST");

        yield return new WaitForSeconds(delaySlash2);
        PerformSlash();

        // Sedikit waktu agar animasi Slash2 terlihat utuh
        yield return new WaitForSeconds(0.05f);

        // Reset flag Slash1 & Slash2 supaya Animator bersih
        if (anim != null)
            anim.ResetSlashFlags();

        isBusy        = false;
        isSlash2Phase = false;

        if (player != null)
            player.isAttacking = false;

        lastComboTime = Time.time;
    }

    //=====================================================================
    // AUTO-BUFFER COMBO (PAKAI SLOT TEMPAT SKILL DIPASANG)
    //=====================================================================
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
        float timer    = 0f;

        KeyCode comboKey = GetMyKey();

        while (timer < chainWindow)
        {
            if (comboKey != KeyCode.None && Input.GetKeyDown(comboKey))
            {
                chainRequested = true;
                // tidak perlu langsung break, cukup tandai
            }

            timer += Time.deltaTime;
            yield return null;
        }
    }

    //=====================================================================
    // LOGIKA HITBOX
    //=====================================================================
    private void PerformSlash()
    {
        if (!character) return;

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

            Vector2 toTarget = (target.transform.position - origin).normalized;
            float angleBetween = Vector2.Angle(dir, toTarget);

            if (angleBetween <= attackAngle * 0.5f)
            {
                target.TakeDamage(character.attack);
            }
        }

        // Setelah proses hit selesai, tampilkan gizmo arc sebentar
        if (gameObject.activeInHierarchy)
            StartCoroutine(ShowHitArcWindow());
    }

    private IEnumerator ShowHitArcWindow()
    {
        showHitArc = true;
        yield return new WaitForSeconds(gizmoShowTime);
        showHitArc = false;
    }

    //=====================================================================
    // FAIL-SAFE
    //=====================================================================
    void OnDisable()
    {
        isBusy         = false;
        chainRequested = false;
        isSlash2Phase  = false;
        showHitArc     = false;

        if (anim != null)
            anim.ResetSlashFlags();

        if (player != null)
            player.isAttacking = false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Jangan gambar apa pun di Edit Mode
        if (!Application.isPlaying)
            return;

        // Hanya gambar ketika combo sedang berjalan dan window hit aktif
        if (!isBusy || !showHitArc)
            return;

        if (!character)
        {
            character = GetComponentInParent<CharacterBase>();
            if (!character) return;
        }

        Gizmos.color = isSlash2Phase ? chain2Color : chain1Color;

        Vector3 offset = new Vector3(hitOffset.x, hitOffset.y, 0f);
        if (!character.isFacingRight)
            offset.x = -offset.x;

        Vector3 origin = character.transform.position + offset;
        Vector3 dir    = character.isFacingRight ? Vector3.right : Vector3.left;

        float startAngle = -attackAngle * 0.5f;
        float step       = attackAngle / Mathf.Max(1, arcSegments);

        Vector3 prev = origin + Quaternion.Euler(0, 0, startAngle) * dir * attackRadius;

        for (int i = 1; i <= arcSegments; i++)
        {
            float ang  = startAngle + step * i;
            Vector3 next = origin + Quaternion.Euler(0, 0, ang) * dir * attackRadius;
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
#endif
}
