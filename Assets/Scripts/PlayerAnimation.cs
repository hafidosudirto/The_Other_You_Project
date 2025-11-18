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

    // Disiapkan untuk nanti (hurt / dead)
    private static readonly int HashIsDead   = Animator.StringToHash("IsDead");
    private static readonly int HashHurt     = Animator.StringToHash("Hurt");

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

    /// <summary>
    /// Dipanggil dari script movement (contoh: MoveKeyboard)
    /// dengan nilai kecepatan absolut (0 = diam).
    /// </summary>
    public void SetMoveSpeed(float speed)
    {
        if (!animator) return;
        animator.SetFloat(HashMoveSpeed, Mathf.Abs(speed));
    }

    // =========================
    //  COMBAT / SLASH COMBO
    // =========================

    /// <summary>
    /// Mainkan animasi Slash Combo 1.
    /// Dipanggil dari Sword_SlashCombo saat hit pertama.
    /// </summary>
    public void PlaySlash1()
    {
        if (!animator) return;

        // Pastikan trigger lain di-reset supaya nggak nyangkut
        animator.ResetTrigger(HashSlash2);
        animator.SetTrigger(HashSlash1);
    }

    /// <summary>
    /// Mainkan animasi Slash Combo 2.
    /// Dipanggil dari Sword_SlashCombo saat chain kedua.
    /// </summary>
    public void PlaySlash2()
    {
        if (!animator) return;

        animator.ResetTrigger(HashSlash1);
        animator.SetTrigger(HashSlash2);
    }

    // =========================
    //  DAMAGE / DEATH (buat nanti)
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
}
