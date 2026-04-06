using System.Collections;
using UnityEngine;

public class Dash : MonoBehaviour, ISkill
{
    [Header("Dash Settings")]
    [Tooltip("Semakin besar, jarak dash makin jauh. Jarak kira-kira ≈ dashSpeed * dashDuration.")]
    public float dashSpeed = 10f;

    [Tooltip("Semakin kecil, dash makin singkat dan terasa lebih snap (0.1–0.2 cocok untuk action).")]
    public float dashDuration = 0.15f;

    [Tooltip("Jeda sebelum boleh dash lagi.")]
    public float dashCooldown = 0.2f;

    [Header("References (opsional)")]
    public PlayerAnimation anim;
    public Player player;

    [Header("Input (opsional, jika ingin dash pakai tombol langsung)")]
    public KeyCode dashKey = KeyCode.LeftShift;

    private MoveKeyboard mover;
    private Rigidbody2D rb;

    private bool isDashing = false;
    private float lastDashTime = -999f;

    private void Awake()
    {
        if (player == null)
            player = GetComponent<Player>();

        if (anim == null)
            anim = GetComponentInChildren<PlayerAnimation>();

        rb = GetComponent<Rigidbody2D>();
        if (rb == null && player != null)
            rb = player.GetComponent<Rigidbody2D>();

        if (rb == null)
            Debug.LogError("[Dash] Rigidbody2D tidak ditemukan di hierarki objek ini. Dash tidak dapat dijalankan.");

        mover = GetComponent<MoveKeyboard>();
        if (mover == null)
            mover = GetComponentInChildren<MoveKeyboard>();
    }

    private void Update()
    {
        if (rb == null)
            return;

        if (Input.GetKeyDown(dashKey))
            TryStartDash();
    }

    public void TriggerSkill(int slotIndex)
    {
        TryStartDash();
    }

    private void TryStartDash()
    {
        if (rb == null)
            return;

        if (isDashing)
            return;

        if (Time.time < lastDashTime + dashCooldown)
            return;

        if (player != null)
        {
            if (!player.CanAct())
                return;

            if (player.isAttacking)
                return;
        }

        // Dash harus dicatat spesifik sebagai dashCount,
        // bukan sekadar defensive action umum.
        if (DataTracker.Instance != null)
            DataTracker.Instance.RecordDefenseDash(WeaponType.Sword);

        StartCoroutine(DashRoutine());
    }

    private IEnumerator DashRoutine()
    {
        if (rb == null)
            yield break;

        isDashing = true;
        lastDashTime = Time.time;

        float facingX;

        if (mover != null)
            facingX = mover.GetFacingDirection();
        else if (player != null && player.isFacingRight)
            facingX = 1f;
        else
            facingX = -1f;

        Vector2 dir = new Vector2(facingX, 0f).normalized;
        Vector2 startPos = rb.position;
        float distance = dashSpeed * dashDuration;
        Vector2 targetPos = startPos + dir * distance;

        if (mover != null)
            mover.LockExternal(dashDuration + 0.05f, stopVelocity: false);

        if (anim != null)
            anim.PlayDash();

        float timer = 0f;
        rb.velocity = Vector2.zero;

        while (timer < dashDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / dashDuration);
            float eased = 1f - (1f - t) * (1f - t);

            Vector2 newPos = Vector2.Lerp(startPos, targetPos, eased);
            rb.MovePosition(newPos);

            yield return null;
        }

        rb.velocity = Vector2.zero;

        if (mover != null)
            mover.UnlockExternal();

        isDashing = false;
    }
}