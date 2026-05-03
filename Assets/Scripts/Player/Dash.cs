using System.Collections;
using UnityEngine;

public class Dash : MonoBehaviour, ISkill
{
    [Header("Dash Settings")]
    [Tooltip("Kecepatan dash. Jarak kira-kira = dashSpeed * dashDuration.")]
    public float dashSpeed = 10f;

    [Tooltip("Durasi dash. Umumnya 0.10 sampai 0.18 cocok untuk action 2D.")]
    public float dashDuration = 0.15f;

    [Tooltip("Jeda sebelum dash boleh dipakai lagi.")]
    public float dashCooldown = 0.2f;

    [Header("Energy Cost")]
    [SerializeField, Min(0f)] private float dashEnergyCost = 15f;

    [Tooltip("Biasanya isi dengan Player, karena Player mewarisi CharacterBase.")]
    [SerializeField] private CharacterBase energyOwner;

    [Header("References")]
    public PlayerAnimation anim;
    public Player player;

    [Header("Input")]
    [Tooltip("Aktifkan jika dash dipanggil langsung dari script ini, misalnya Left Shift.")]
    public bool useDirectInput = true;

    public KeyCode dashKey = KeyCode.LeftShift;

    private MoveKeyboard mover;
    private Rigidbody2D rb;

    private bool isDashing = false;
    private float lastDashTime = -999f;
    private Coroutine dashRoutine;

    private void Awake()
    {
        if (player == null)
            player = GetComponentInParent<Player>();

        if (energyOwner == null)
            energyOwner = GetComponentInParent<CharacterBase>();

        if (anim == null)
            anim = GetComponentInChildren<PlayerAnimation>();

        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = GetComponentInParent<Rigidbody2D>();
        if (rb == null && player != null)
            rb = player.GetComponent<Rigidbody2D>();

        if (rb == null)
            Debug.LogError("[Dash] Rigidbody2D tidak ditemukan. Dash tidak dapat dijalankan.");

        mover = GetComponent<MoveKeyboard>();
        if (mover == null)
            mover = GetComponentInParent<MoveKeyboard>();
        if (mover == null)
            mover = GetComponentInChildren<MoveKeyboard>();
    }

    private void Update()
    {
        if (!useDirectInput)
            return;

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
        if (!CanStartDash())
            return;

        if (!TryPayEnergy())
            return;

        if (DataTracker.Instance != null)
            DataTracker.Instance.RecordDefenseDash(WeaponType.Sword);

        dashRoutine = StartCoroutine(DashRoutine());
    }

    private bool CanStartDash()
    {
        if (rb == null)
            return false;

        if (isDashing)
            return false;

        if (Time.time < lastDashTime + dashCooldown)
            return false;

        if (player != null)
        {
            if (!player.CanAct())
                return false;

            if (player.isAttacking)
                return false;
        }

        return true;
    }

    private bool TryPayEnergy()
    {
        if (dashEnergyCost <= 0f)
            return true;

        if (energyOwner == null)
        {
            Debug.LogWarning("[Dash] Energy Owner belum di-assign. Dash dibatalkan agar tidak gratis.");
            return false;
        }

        if (!energyOwner.TrySpendEnergy(dashEnergyCost))
        {
            Debug.LogWarning($"[Dash] Energy kurang. Dash butuh {dashEnergyCost} energy.");
            return false;
        }

        return true;
    }

    private IEnumerator DashRoutine()
    {
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

        if (mover != null)
            mover.LockExternal(dashDuration + 0.01f, stopVelocity: false);

        if (anim != null)
        {
            anim.SetMoveSpeed(0f);
            anim.PlayDash();
        }

        rb.velocity = Vector2.zero;

        float elapsed = 0f;

        while (elapsed < dashDuration)
        {
            float stepTime = Mathf.Min(Time.fixedDeltaTime, dashDuration - elapsed);
            Vector2 nextPosition = rb.position + dir * dashSpeed * stepTime;

            rb.MovePosition(nextPosition);

            elapsed += stepTime;
            yield return new WaitForFixedUpdate();
        }

        rb.velocity = Vector2.zero;

        if (mover != null)
            mover.UnlockExternal();

        if (anim != null)
            anim.SetMoveSpeed(0f);

        isDashing = false;
        dashRoutine = null;
    }

    private void OnDisable()
    {
        if (dashRoutine != null)
        {
            StopCoroutine(dashRoutine);
            dashRoutine = null;
        }

        if (rb != null)
            rb.velocity = Vector2.zero;

        if (mover != null)
            mover.UnlockExternal();

        isDashing = false;
    }
}