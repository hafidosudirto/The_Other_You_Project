using UnityEngine;

public class EnemyAnimation : MonoBehaviour
{
    [Header("References")]
    public Animator animator;
    private SpriteRenderer sr;

    // =========================
    // HASH PARAMETERS
    // =========================
    private static readonly int HashMoveSpeed = Animator.StringToHash("MoveSpeed");
    private static readonly int HashQuickShot = Animator.StringToHash("QuickShot");
    private static readonly int HashIsCharging = Animator.StringToHash("IsCharging");
    private static readonly int HashChargeRelease = Animator.StringToHash("ChargeRelease");
    private static readonly int HashPiercing = Animator.StringToHash("Piercing");
    private static readonly int HashConcussive = Animator.StringToHash("Concussive");

    // Sword / kompatibilitas lama
    private static readonly int HashSlash1 = Animator.StringToHash("Slash1");
    private static readonly int HashSlash2 = Animator.StringToHash("Slash2");
    private static readonly int HashDash = Animator.StringToHash("Dash");
    private static readonly int HashWhirlwind = Animator.StringToHash("Whirlwind");
    private static readonly int HashRiposteReady = Animator.StringToHash("RiposteReady");
    private static readonly int HashRiposteCounter = Animator.StringToHash("RiposteCounter");

    private void Awake()
    {
        if (!animator)
            animator = GetComponentInChildren<Animator>(true);

        sr = GetComponentInChildren<SpriteRenderer>(true);

        if (sr == null)
            Debug.LogError($"[EnemyAnimation] SpriteRenderer tidak ditemukan di {gameObject.name}");

        if (animator == null)
            Debug.LogError($"[EnemyAnimation] Animator tidak ditemukan di {gameObject.name}");
    }

    public void SetMoveSpeed(float speed)
    {
        if (animator != null)
            animator.SetFloat(HashMoveSpeed, speed);
    }

    public void SetFlip(bool flip)
    {
        if (sr != null)
            sr.flipX = flip;
    }

    // =========================
    // BOW
    // =========================
    public void PlayQuickShot()
    {
        if (animator == null) return;

        ResetBowTriggers();
        animator.SetTrigger(HashQuickShot);
    }

    public void TriggerBowChargeStart()
    {
        if (animator == null) return;

        animator.ResetTrigger(HashChargeRelease);
        animator.SetBool(HashIsCharging, true);
    }

    public void TriggerBowChargeRelease()
    {
        if (animator == null) return;

        animator.SetBool(HashIsCharging, false);
        animator.SetTrigger(HashChargeRelease);
    }

    public void PlayPiercingShot()
    {
        if (animator == null) return;

        ResetBowTriggers();
        animator.SetTrigger(HashPiercing);
    }

    public void PlayConcussiveShot()
    {
        if (animator == null) return;

        ResetBowTriggers();
        animator.SetTrigger(HashConcussive);
    }

    private void ResetBowTriggers()
    {
        if (animator == null) return;

        animator.ResetTrigger(HashQuickShot);
        animator.ResetTrigger(HashChargeRelease);
        animator.ResetTrigger(HashPiercing);
        animator.ResetTrigger(HashConcussive);
    }

    // =========================
    // SWORD / KOMPATIBILITAS LAMA
    // =========================
    public void SetCharging(bool value)
    {
        if (animator != null)
            animator.SetBool(HashIsCharging, value);
    }

    public void PlayDash()
    {
        if (animator != null)
            animator.SetTrigger(HashDash);
    }

    public void PlaySlash1()
    {
        if (animator == null) return;
        animator.ResetTrigger(HashSlash1);
        animator.SetTrigger(HashSlash1);
    }

    public void PlaySlash2()
    {
        if (animator == null) return;
        animator.ResetTrigger(HashSlash2);
        animator.SetTrigger(HashSlash2);
    }

    public void PlayWhirlwind()
    {
        if (animator != null)
            animator.SetTrigger(HashWhirlwind);
    }

    public void SetRiposteReady(bool isReady)
    {
        if (animator != null)
            animator.SetBool(HashRiposteReady, isReady);
    }

    public void TriggerRiposteCounter()
    {
        if (animator == null) return;

        animator.ResetTrigger(HashRiposteCounter);
        animator.SetTrigger(HashRiposteCounter);
    }
}