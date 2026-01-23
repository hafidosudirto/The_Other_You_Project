using System.Collections;
using UnityEngine;

public sealed class Enemy_Dash : MonoBehaviour
{
    [Header("Dash Settings")]
    public float dashSpeed = 10f;
    public float dashDuration = 0.15f;
    public float dashCooldown = 0.20f;

    [Header("References")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform player;

    [Header("Animator Trigger Name")]
    [SerializeField] private string dashTrigger = "Dash";

    private bool _isDashing;
    private float _lastDashTime = -999f;

    public float DashDuration => dashDuration;

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    public void SetPlayer(Transform p) => player = p;

    public bool TryDashAwayFromPlayer()
    {
        if (rb == null || player == null) return false;
        if (_isDashing) return false;
        if (Time.time < _lastDashTime + dashCooldown) return false;

        StartCoroutine(DashRoutine());
        return true;
    }

    private IEnumerator DashRoutine()
    {
        _isDashing = true;
        _lastDashTime = Time.time;

        Vector2 enemyPos = rb.position;
        Vector2 playerPos = (Vector2)player.position;

        // Arah menjauh dari player (hanya sumbu X)
        float directionX = enemyPos.x - playerPos.x;

        // Jika musuh tepat di posisi player (sangat jarang), default ke kanan
        if (Mathf.Abs(directionX) < 0.0001f)
        {
            directionX = 1f;
        }

        // Normalisasi arah: 1 jika musuh di kanan player, -1 jika di kiri
        Vector2 awayDirection = new Vector2(Mathf.Sign(directionX), 0f);

        float distance = dashSpeed * dashDuration;
        Vector2 targetPos = enemyPos + awayDirection * distance;

        // Trigger animasi dash
        if (animator != null) animator.SetTrigger(dashTrigger);

        float timer = 0f;
        rb.velocity = Vector2.zero;

        // Dash menggunakan interpolasi dengan easing
        while (timer < dashDuration)
        {
            yield return new WaitForFixedUpdate();
            timer += Time.fixedDeltaTime;

            float t = Mathf.Clamp01(timer / dashDuration);
            float eased = 1f - (1f - t) * (1f - t); // ease-out

            Vector2 newPos = Vector2.Lerp(enemyPos, targetPos, eased);
            rb.MovePosition(newPos);
        }

        rb.velocity = Vector2.zero;
        _isDashing = false;
    }
}