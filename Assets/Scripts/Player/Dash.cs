using System.Collections;
using UnityEngine;

public class Dash : MonoBehaviour, ISkill
{
    [Header("Dash Settings")]
    [Tooltip("Semakin besar, jarak dash makin jauh. Jarak kira-kira ≈ dashSpeed * dashDuration.")]
    public float dashSpeed = 10f;
    [Tooltip("Semakin kecil, dash makin singkat dan terasa lebih 'snap' (0.1–0.2 cocok untuk action).")]
    public float dashDuration = 0.15f;
    [Tooltip("Jeda sebelum boleh dash lagi.")]
    public float dashCooldown = 0.2f;

    [Header("References (opsional)")]
    public PlayerAnimation anim;   // boleh null
    public Player player;          // boleh null

    [Header("Input (opsional, jika ingin dash pakai tombol langsung)")]
    public KeyCode dashKey = KeyCode.LeftShift;

    private MoveKeyboard mover;    // boleh null
    private Rigidbody2D rb;        // WAJIB ADA

    private bool isDashing = false;
    private float lastDashTime = -999f;

    private void Awake()
    {
        // Referensi Player (opsional)
        if (player == null)
            player = GetComponent<Player>();

        // Cari PlayerAnimation di child (biasanya di objek Sprite)
        if (anim == null)
            anim = GetComponentInChildren<PlayerAnimation>();

        // Rigidbody2D utama (WAJIB)
        rb = GetComponent<Rigidbody2D>();
        if (rb == null && player != null)
            rb = player.GetComponent<Rigidbody2D>();

        if (rb == null)
        {
            Debug.LogError("[Dash] Rigidbody2D tidak ditemukan di hierarki objek ini. Dash tidak dapat dijalankan.");
        }

        // MoveKeyboard (pengendali gerak normal, opsional)
        mover = GetComponent<MoveKeyboard>();
        if (mover == null)
            mover = GetComponentInChildren<MoveKeyboard>();
    }

    private void Update()
    {
        if (rb == null)
            return;

        // Opsional: dash langsung dari tombol
        if (Input.GetKeyDown(dashKey))
        {
            TryStartDash();
        }
    }

    // Dipanggil dari SkillBase / sistem skill
    public void TriggerSkill(int slotIndex)
    {
        TryStartDash();
    }

    private void TryStartDash()
    {
        if (rb == null)
            return;

        // Sudah sedang dash → abaikan
        if (isDashing)
            return;

        // Masih cooldown
        if (Time.time < lastDashTime + dashCooldown)
            return;

        // Kalau punya Player, hormati state global-nya
        if (player != null)
        {
            // Tidak bisa dash kalau memang tidak boleh bertindak
            if (!player.CanAct())
                return;

            // Tambahan: selama sedang menyerang / casting skill besar, dash dimatikan
            if (player.isAttacking)
                return;
        }

        StartCoroutine(DashRoutine());
    }


    private IEnumerator DashRoutine()
    {
        if (rb == null)
            yield break;

        isDashing = true;
        lastDashTime = Time.time;

        // ===== 1. Tentukan arah dash (kanan / kiri) =====
        float facingX;

        if (mover != null)
        {
            // GetFacingDirection() dari MoveKeyboard mengembalikan float: +1 atau -1
            facingX = mover.GetFacingDirection();
        }
        else if (player != null && player.isFacingRight)
        {
            facingX = 1f;
        }
        else
        {
            // Default: menghadap kiri kalau tidak ada informasi
            facingX = -1f;
        }

        Vector2 dir = new Vector2(facingX, 0f).normalized;

        // ===== 2. Hitung jarak dan posisi target =====
        Vector2 startPos = rb.position;
        float distance = dashSpeed * dashDuration;   // jarak total
        Vector2 targetPos = startPos + dir * distance;

        // ===== 3. Kunci input gerak normal selama dash =====
        if (mover != null)
        {
            // Selama dash, MoveKeyboard tidak mengubah posisi
            mover.LockExternal(dashDuration + 0.05f, stopVelocity: false);
        }

        // ===== 4. Mainkan animasi dash (jika ada) =====
        if (anim != null)
            anim.PlayDash();

        float timer = 0f;
        rb.velocity = Vector2.zero;

        // ===== 5. Gerakkan pemain dengan lerp + easing =====
        while (timer < dashDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / dashDuration);

            // Ease-out sederhana: cepat di awal, melambat di akhir
            float eased = 1f - (1f - t) * (1f - t);

            Vector2 newPos = Vector2.Lerp(startPos, targetPos, eased);
            rb.MovePosition(newPos);

            yield return null;
        }

        // Pastikan berhenti di akhir dash
        rb.velocity = Vector2.zero;

        // Lepas kunci gerak normal
        if (mover != null)
            mover.UnlockExternal();

        isDashing = false;
    }
}
