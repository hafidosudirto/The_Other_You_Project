using UnityEngine;

public class MoveKeyboard : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 4f;
    private Vector2 input;
    private Rigidbody2D rb;

    [Header("References (opsional)")]
    public PlayerAnimation anim;   // boleh null
    public Player player;          // boleh null

    // ======================================================
    // SYSTEM LOCK
    // ======================================================
    // Lock karena serangan (Slash Combo / skill lain)
    private bool isAttackLocked = false;
    private float attackLockTimer = 0f;

    // Lock dari luar (Dash, ChargedStrike, dsb.)
    private bool externalLock = false;
    private float externalLockTimer = 0f;

    // Apakah external lock juga memaksa velocity = 0?
    //  - true  → gerakan benar-benar berhenti (ChargedStrike, stun, dsb.)
    //  - false → input dibekukan, tetapi skrip lain boleh mengatur velocity (Dash)
    private bool externalStopsVelocity = true;

    // Menyimpan arah terakhir untuk prefab yang tidak punya Player
    private float lastMoveX = 1f;

    // ======================================================
    private void Awake()
    {
        // Cari Rigidbody2D di objek ini, parent, atau child (WAJIB ADA)
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = GetComponentInParent<Rigidbody2D>();
        if (rb == null) rb = GetComponentInChildren<Rigidbody2D>();

        if (rb == null)
        {
            Debug.LogError("[MoveKeyboard] Rigidbody2D tidak ditemukan di hierarki objek ini. Skrip akan non-aktif.");
        }

        // ------------ REFERENSI OPSIONAL --------------

        // Cari PlayerAnimation di child dulu (biasanya animator ada di bawah sprite)
        if (anim == null)
        {
            anim = GetComponentInChildren<PlayerAnimation>();
            if (anim == null)
                anim = GetComponentInParent<PlayerAnimation>();
        }

        // Cari Player (turunan CharacterBase)
        if (player == null)
        {
            player = GetComponent<Player>();
            if (player == null)
                player = GetComponentInParent<Player>();
            if (player == null)
                player = GetComponentInChildren<Player>();
        }
    }

    private void Update()
    {
        // Satu-satunya syarat wajib: punya Rigidbody2D
        if (rb == null)
            return;

        // ======================================================
        // 1. EXTERNAL LOCK (Dash, ChargedStrike, dsb.)
        // ======================================================
        if (externalLock)
        {
            externalLockTimer -= Time.deltaTime;
            if (externalLockTimer <= 0f)
                externalLock = false;

            // Bekukan input gerak
            input = Vector2.zero;

            if (externalStopsVelocity)
            {
                rb.velocity = Vector2.zero;
            }

            if (anim != null)
                anim.SetMoveSpeed(0);

            return;
        }

        // ======================================================
        // 2. ATTACK LOCK (Slash Combo, Whirlwind, dll.)
        // ======================================================
        if (isAttackLocked)
        {
            attackLockTimer -= Time.deltaTime;
            if (attackLockTimer <= 0f)
                isAttackLocked = false;

            rb.velocity = Vector2.zero;

            if (anim != null)
                anim.SetMoveSpeed(0);

            return;
        }

        // ======================================================
        // 3. NORMAL MOVEMENT
        // ======================================================
        input = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );

        bool isMoving = input.sqrMagnitude > 0.01f;

        if (anim != null)
            anim.SetMoveSpeed(isMoving ? 1f : 0f);

        // Update arah hadap (kalau ada Player, sinkron dengan isFacingRight)
        if (input.x > 0.1f)
        {
            lastMoveX = 1f;

            if (anim != null)
                anim.SetFlip(false);

            if (player != null)
                player.isFacingRight = true;
        }
        else if (input.x < -0.1f)
        {
            lastMoveX = -1f;

            if (anim != null)
                anim.SetFlip(true);

            if (player != null)
                player.isFacingRight = false;
        }
    }

    private void FixedUpdate()
    {
        if (rb == null)
            return;

        // externalLock: jangan sentuh velocity jika eksternal yang mengatur
        if (externalLock)
        {
            if (externalStopsVelocity)
            {
                rb.velocity = Vector2.zero;
            }
            return;
        }

        // attack lock
        if (isAttackLocked)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        // normal movement
        rb.velocity = input.normalized * moveSpeed;
    }

    // ============================================================
    //  API UNTUK SLASH COMBO (BOOLEAN ANIMATOR)
    // ============================================================
    public void TriggerSlash1(float duration)
    {
        LockMovement(duration);

        if (anim != null)
        {
            anim.SetSlash1(true);
            anim.SetSlash2(false);
        }
    }

    public void TriggerSlash2(float duration)
    {
        LockMovement(duration);

        if (anim != null)
        {
            anim.SetSlash2(true);
        }
    }

    private void LockMovement(float duration)
    {
        isAttackLocked = true;
        attackLockTimer = duration;

        rb.velocity = Vector2.zero;

        if (anim != null)
            anim.SetMoveSpeed(0);
    }

    // ============================================================
    // EXTERNAL LOCK SYSTEM → DIPAKAI OLEH DASH & CHARGED STRIKE
    // ============================================================
    public void LockExternal(float duration, bool stopVelocity = true)
    {
        externalLock = true;
        externalLockTimer = duration;
        externalStopsVelocity = stopVelocity;

        if (stopVelocity)
        {
            rb.velocity = Vector2.zero;
            if (anim != null)
                anim.SetMoveSpeed(0);
        }
    }

    public void UnlockExternal()
    {
        externalLock = false;
        externalLockTimer = 0f;
        externalStopsVelocity = true;
    }

    /// <summary>
    /// Digunakan oleh Dash. Kalau ada Player → pakai isFacingRight.
    /// Kalau tidak ada, pakai arah input terakhir, default ke +1.
    /// </summary>
    public float GetFacingDirection()
    {
        if (player != null)
        {
            return player.isFacingRight ? 1f : -1f;
        }

        if (Mathf.Abs(lastMoveX) > 0.01f)
            return Mathf.Sign(lastMoveX);

        return 1f;
    }
}
