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

    // Slash1 & Slash2 sekarang dipakai sebagai TRIGGER di Animator
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
    // SWORD: SLASH COMBO (TRIGGER)
    // =========================
    // Slash1 & Slash2 di Animator HARUS bertipe TRIGGER

    public void SetSlash1(bool value)
    {
        if (value)
        {
            // Pastikan trigger bersih dulu lalu picu
            animator.ResetTrigger(HashSlash1);
            animator.SetTrigger(HashSlash1);
        }
        else
        {
            // Bersihkan jika masih tersisa
            animator.ResetTrigger(HashSlash1);
        }
    }

    public void SetSlash2(bool value)
    {
        if (value)
        {
            animator.ResetTrigger(HashSlash2);
            animator.SetTrigger(HashSlash2);
        }
        else
        {
            animator.ResetTrigger(HashSlash2);
        }
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
        // Aman dipanggil kapan pun untuk mengosongkan kedua trigger
        animator.ResetTrigger(HashSlash1);
        animator.ResetTrigger(HashSlash2);
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
