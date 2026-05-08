using UnityEngine;

public class EnemyAnimation : MonoBehaviour
{
    [Header("References")]
    public Animator animator;
    private SpriteRenderer sr;
    private NodeManager nodeManager;

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

        // Mengambil referensi NodeManager (berada di object yang sama atau parent)
        nodeManager = GetComponentInParent<NodeManager>();

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
        ResetAllTriggers();
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
        ResetAllTriggers();
        animator.SetTrigger(HashPiercing);
    }

    public void PlayConcussiveShot()
    {
        if (animator == null) return;
        ResetAllTriggers();
        animator.SetTrigger(HashConcussive);
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
        if (animator == null) return;
        ResetAllTriggers();
        animator.SetTrigger(HashDash);
    }

    public void PlaySlash1()
    {
        if (animator == null) return;
        ResetAllTriggers();
        animator.SetTrigger(HashSlash1);
    }

    public void PlaySlash2()
    {
        if (animator == null) return;
        ResetAllTriggers();
        animator.SetTrigger(HashSlash2);
    }

    public void PlayWhirlwind()
    {
        if (animator == null) return;
        ResetAllTriggers();
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
        ResetAllTriggers();
        animator.SetTrigger(HashRiposteCounter);
    }

    // =========================
    // UTILITY & ANIMATION EVENTS
    // =========================

    /// <summary>
    /// Mencegah trigger menumpuk dan menyebabkan animasi nyangkut di aksi sebelumnya
    /// </summary>
    private void ResetAllTriggers()
    {
        if (animator == null) return;

        animator.ResetTrigger(HashQuickShot);
        animator.ResetTrigger(HashChargeRelease);
        animator.ResetTrigger(HashPiercing);
        animator.ResetTrigger(HashConcussive);
        animator.ResetTrigger(HashSlash1);
        animator.ResetTrigger(HashSlash2);
        animator.ResetTrigger(HashDash);
        animator.ResetTrigger(HashWhirlwind);
        animator.ResetTrigger(HashRiposteCounter);
    }

    /// <summary>
    /// Panggil fungsi ini melalui fitur "Animation Event" di frame terakhir 
    /// pada setiap klip animasi serangan (Slash, Shot, dll) di tab Animation Unity.
    /// Ini memastikan AI kembali berpikir setelah selesai mengayunkan senjata.
    /// </summary>
    public void EndCurrentAction()
    {
        if (nodeManager != null)
        {
            nodeManager.OnActionEnd();
        }
    }
}