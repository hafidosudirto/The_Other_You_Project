using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    [Header("References")]
    public Animator animator;
    private SpriteRenderer sr;

    // =========================
    // HASH PARAMETERS
    // =========================
    private static readonly int HashMoveSpeed        = Animator.StringToHash("MoveSpeed");
    private static readonly int HashDash             = Animator.StringToHash("Dash");

    // Slash1 & Slash2 sekarang dipakai sebagai BOOLEAN di Animator
    private static readonly int HashSlash1           = Animator.StringToHash("Slash1");
    private static readonly int HashSlash2           = Animator.StringToHash("Slash2");
    private static readonly int HashWhirlwind        = Animator.StringToHash("Whirlwind");

    private static readonly int HashQuickShot        = Animator.StringToHash("QuickShot");
    private static readonly int HashBowChargeStart   = Animator.StringToHash("Bow_ChargeStart");
    private static readonly int HashBowChargeRelease = Animator.StringToHash("Bow_ChargeRelease");

    // Charged Strike
    private static readonly int HashCharging         = Animator.StringToHash("isCharging");

    // =========================
    // RIPOSTE
    // =========================
    private static readonly int HashRiposteReady     = Animator.StringToHash("RiposteReady");
    private static readonly int HashRiposteCounter   = Animator.StringToHash("RiposteCounter");

    private void Awake()
    {
        if (!animator)
            animator = GetComponentInChildren<Animator>();

        sr = GetComponentInChildren<SpriteRenderer>();
    }

    // =========================
    // MOVEMENT
    // =========================
    public void SetMoveSpeed(float speed)
    {
        animator.SetFloat(HashMoveSpeed, speed);
    }

    public void SetFlip(bool flip)
    {
        if (sr) sr.flipX = flip;
    }

    // =========================
    // GLOBAL BOOLS
    // =========================
    public void SetCharging(bool value)
    {
        animator.SetBool(HashCharging, value);
    }

    // =========================
    // DASH
    // =========================
    public void PlayDash()
    {
        animator.SetTrigger(HashDash);
    }

    // =========================
    // SWORD: SLASH COMBO (BOOL)
    // =========================
    // Slash1 & Slash2 di Animator harus TIPE BOOL

    public void SetSlash1(bool value)
    {
        animator.SetBool(HashSlash1, value);
    }

    public void SetSlash2(bool value)
    {
        animator.SetBool(HashSlash2, value);
    }

    // Wrapper kalau masih ada kode lama yang memanggil "PlaySlash1/2"
    public void PlaySlash1()
    {
        SetSlash1(true);
    }

    public void PlaySlash2()
    {
        SetSlash2(true);
    }

    public void ResetSlashFlags()
    {
        animator.SetBool(HashSlash1, false);
        animator.SetBool(HashSlash2, false);
    }

    public void PlayWhirlwind()
    {
        animator.SetTrigger(HashWhirlwind);
    }

    // =========================
    // RIPOSTE
    // =========================
    /// <summary>
    /// Mengatur bool RiposteReady untuk masuk/keluar stance riposte.
    /// </summary>
    public void SetRiposteReady(bool isReady)
    {
        animator.SetBool(HashRiposteReady, isReady);
    }

    /// <summary>
    /// Memicu animasi Riposte_Counter (follow-up attack).
    /// </summary>
    public void TriggerRiposteCounter()
    {
        // Optional: pastikan trigger bersih dulu
        animator.ResetTrigger(HashRiposteCounter);
        animator.SetTrigger(HashRiposteCounter);
    }

    // =========================
    // BOW
    // =========================
    public void PlayQuickShot()
    {
        animator.SetTrigger(HashQuickShot);
    }

    public void TriggerBowChargeStart()
    {
        animator.SetTrigger(HashBowChargeStart);
    }

    public void TriggerBowChargeRelease()
    {
        animator.SetTrigger(HashBowChargeRelease);
    }
}
