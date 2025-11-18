using UnityEngine;

[RequireComponent(typeof(Player))]
[RequireComponent(typeof(Rigidbody2D))]
public class Move_Keyboard : MonoBehaviour
{
    private Player player;
    private Rigidbody2D rb;
    private Vector2 movement;

    private Dash dash;

    void Awake()
    {
        player = GetComponent<Player>();
        rb = GetComponent<Rigidbody2D>();

        // Wajib: Kinematic TIDAK memakai velocity
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.freezeRotation = true;

        dash = GetComponent<Dash>();
    }

    void Update()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        movement = new Vector2(h, v).normalized * player.moveSpeed;

        if (h > 0 && !player.isFacingRight)
            player.Flip();
        else if (h < 0 && player.isFacingRight)
            player.Flip(); 
            
    }

    void FixedUpdate()
    {
        if (dash != null && dash.isDashing)
            return;

        // Kinematic movement memakai MovePosition
        rb.MovePosition(rb.position + movement * Time.fixedDeltaTime);
    }
}
