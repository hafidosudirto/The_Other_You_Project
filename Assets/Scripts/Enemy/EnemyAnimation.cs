using UnityEngine;

public class EnemyAnimation : MonoBehaviour
{
    [SerializeField] public Animator animator;
    [SerializeField] private float moveSpeedDamp = 0.08f;
    private SpriteRenderer sr;

    // Sama dengan PlayerAnimation
    private static readonly int HashMoveSpeed = Animator.StringToHash("MoveSpeed");

    private static readonly int HashSlash1 = Animator.StringToHash("Slash1");
    private static readonly int HashSlash2 = Animator.StringToHash("Slash2");
    private static readonly int HashWhirlwind = Animator.StringToHash("Whirlwind");

    private static readonly int HashCharging = Animator.StringToHash("isCharging");

    private static readonly int HashRiposteReady = Animator.StringToHash("RiposteReady");
    private static readonly int HashRiposteCounter = Animator.StringToHash("RiposteCounter");

    private void Awake()
    {
        // Gunakan (true) agar tetap mencari meskipun objek sedang nonaktif
        if (animator == null) animator = GetComponentInChildren<Animator>(true);

        // Ini krusial: ambil SpriteRenderer di hierarki bawah
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>(true);

        if (sr == null) Debug.LogError($"[EnemyAnimation] SpriteRenderer tidak ditemukan di {gameObject.name}");
    }

    public void SetFlip(bool flipX)
    {
        if (sr != null)
        {
            sr.flipX = flipX;
        }
    }

    // Locomotion sama dengan player
    public void SetMoveSpeed(float speed01)
    {
        if (!animator) return;

        // Haluskan perubahan MoveSpeed (Unity menyediakan overload dampTime/deltaTime) :contentReference[oaicite:3]{index=3}
        animator.SetFloat(HashMoveSpeed, speed01, moveSpeedDamp, Time.deltaTime);
    }

    // Sword
    public void PlaySlash1() { if (animator) animator.SetTrigger(HashSlash1); }
    public void PlaySlash2() { if (animator) animator.SetTrigger(HashSlash2); }
    public void PlayWhirlwind() { if (animator) animator.SetTrigger(HashWhirlwind); }

    // Charged
    public void SetCharging(bool v) { if (animator) animator.SetBool(HashCharging, v); }

    // Riposte (sama dengan player)
    public void SetRiposteReady(bool isReady) { if (animator) animator.SetBool(HashRiposteReady, isReady); }
    public void TriggerRiposteCounter()
    {
        if (!animator) return;
        animator.ResetTrigger(HashRiposteCounter);
        animator.SetTrigger(HashRiposteCounter);
    }
}
