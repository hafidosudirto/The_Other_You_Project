using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Player))]
public class Dash : MonoBehaviour
{
    private Rigidbody2D rb;
    private Player player;

    public float dashForce = 20f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;

    public bool isDashing { get; private set; }

    private float dashTime;
    private float dashCooldownTimer;
    private Vector2 dashDirection;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        player = GetComponent<Player>();

        // Kinematic mode
        rb.bodyType = RigidbodyType2D.Kinematic;
    }

    void Update()
    {
        if (dashCooldownTimer > 0)
            dashCooldownTimer -= Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.LeftShift) && !isDashing && dashCooldownTimer <= 0)
            StartDash();

        if (isDashing && Time.time >= dashTime)
            EndDash();
    }

    void StartDash()
    {
        isDashing = true;

        dashTime = Time.time + dashDuration;
        dashCooldownTimer = dashCooldown;

        dashDirection = player.isFacingRight ? Vector2.right : Vector2.left;

        Debug.Log("Player Dash!");

        // Catat aksi defensif ke DataTracker
        DataTracker.Instance.RecordAction(PlayerActionType.Defensive, WeaponType.None);
    }

    void FixedUpdate()
    {
        if (isDashing)
        {
            // Kinematic dash menggunakan MovePosition
            rb.MovePosition(rb.position + dashDirection * dashForce * Time.fixedDeltaTime);
        }
    }

    void EndDash()
    {
        isDashing = false;
    }
}
