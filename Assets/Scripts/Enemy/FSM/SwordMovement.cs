using UnityEngine;

[DisallowMultipleComponent]
public class EnemySwordMovement : MonoBehaviour
{
    public void Align(
        Transform player,
        Rigidbody2D rb,
        float moveSpeed,
        float accel,
        float verticalTolerance
    )
    {
        if (player == null || rb == null)
            return;

        float diffY = player.position.y - transform.position.y;

        // [PERBAIKAN JITTER]: Smooth braking.
        // Melambat perlahan saat mendekati target Y agar tidak overshoot.
        float smoothMult = Mathf.Clamp01(Mathf.Abs(diffY) / (verticalTolerance * 2f));
        float dirY = Mathf.Sign(diffY) * smoothMult;

        rb.velocity = Vector2.Lerp(
            rb.velocity,
            new Vector2(0f, dirY * moveSpeed),
            Time.deltaTime * accel
        );
    }

    public void Chase(
        Transform player,
        Rigidbody2D rb,
        float moveSpeed,
        float accel
    )
    {
        if (player == null || rb == null)
            return;

        float dirX = Mathf.Sign(player.position.x - transform.position.x);

        rb.velocity = Vector2.Lerp(
            rb.velocity,
            new Vector2(dirX * moveSpeed, 0f),
            Time.deltaTime * accel
        );
    }

    public void Retreat(
        Transform player,
        Rigidbody2D rb,
        float moveSpeed,
        float accel
    )
    {
        if (player == null || rb == null)
            return;

        float dirX = -Mathf.Sign(player.position.x - transform.position.x);

        rb.velocity = Vector2.Lerp(
            rb.velocity,
            new Vector2(dirX * moveSpeed, 0f),
            Time.deltaTime * accel
        );
    }
}