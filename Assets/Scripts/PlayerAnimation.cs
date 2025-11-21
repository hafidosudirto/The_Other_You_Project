using UnityEngine;

/// <summary>
/// Satu pintu untuk semua animasi player.
/// Script lain (movement, skill, skill sword, dll)
/// cukup memanggil fungsi di sini, tidak menyentuh Animator langsung.
/// </summary>
public class PlayerAnimation : MonoBehaviour
{
    [Header("References")]
    [SerializeField] public Animator animator;

    // Hash parameter animator
    private static readonly int HashMoveSpeed = Animator.StringToHash("MoveSpeed");

    private static readonly int HashSlash1   = Animator.StringToHash("Slash1");
    private static readonly int HashSlash2   = Animator.StringToHash("Slash2");

    // Whirlwind trigger
    private static readonly int HashWhirlwind = Animator.StringToHash("Whirlwind");

    // ===============================
    // R I P O S T E   A N I M A T I O N
    // ===============================
    private static readonly int HashRiposteStance  = Animator.StringToHash("Riposte_Stance");
    private static readonly int HashRiposteAttack  = Animator.StringToHash("Riposte_Parry_Attack");

    // Disiapkan untuk nanti (hurt / dead)
    private static readonly int HashIsDead = Animator.StringToHash("IsDead");
    private static readonly int HashHurt   = Animator.StringToHash("Hurt");

    private static readonly int HashBowChargeStart = Animator.StringToHash("Bow_ChargeStart");
    private static readonly int HashBowRelease     = Animator.StringToHash("Bow_Release");


    private void Awake()
    {
        if (!animator)
            animator = GetComponentInChildren<Animator>();

        if (!animator)
            Debug.LogWarning("PlayerAnimation: Animator tidak ditemukan di child.");
    }

    // =========================
    //  MOVEMENT
    // =========================
    public void SetMoveSpeed(float speed)
    {
        if (!animator) return;
        animator.SetFloat(HashMoveSpeed, Mathf.Abs(speed));
    }

    // =========================
    //  COMBAT / SLASH COMBO
    // =========================
    public void PlaySlash1()
    {
        if (!animator) return;
        animator.ResetTrigger(HashSlash2);
        animator.SetTrigger(HashSlash1);
    }

    public void PlaySlash2()
    {
        if (!animator) return;
        animator.ResetTrigger(HashSlash1);
        animator.SetTrigger(HashSlash2);
    }

    // =========================
    //  WHIRLWIND
    // =========================
    public void PlayWhirlwind()
    {
        if (!animator) return;
        animator.SetTrigger(HashWhirlwind);
    }

    // =========================
    //  R I P O S T E
    // =========================

    public void PlayRiposteStance()
    {
        if (!animator) return;
        animator.SetTrigger(HashRiposteStance);
    }

    public void PlayRiposteAttack()
    {
        if (!animator) return;
        animator.SetTrigger(HashRiposteAttack);
    }

    // =========================
    //  HURT / DEATH
    // =========================
    public void PlayHurt()
    {
        if (!animator) return;
        animator.SetTrigger(HashHurt);
    }

    public void SetDead(bool isDead)
    {
        if (!animator) return;
        animator.SetBool(HashIsDead, isDead);
    }

    private static readonly int HashQuickShot = Animator.StringToHash("QuickShot");

    public void PlayQuickShot()
    {
        if (!animator) return;
        animator.SetTrigger(HashQuickShot);
    }

    public void PlayBowChargeStart()
    {
        if (!animator) return;
        animator.SetTrigger(HashBowChargeStart);
    }

    public void PlayBowRelease()
    {
        if (!animator) return;
        animator.SetTrigger(HashBowRelease);
    }


}
