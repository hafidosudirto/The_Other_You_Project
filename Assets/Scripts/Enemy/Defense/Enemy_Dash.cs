using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Enemy_Dash : MonoBehaviour
{
    [Header("Dash Settings")]
    [Tooltip("Kecepatan konstan saat melakukan dash maju.")]
    [SerializeField] private float dashSpeed = 6f; 

    [Tooltip("Durasi waktu musuh melakukan dash (dalam detik).")]
    [SerializeField] private float dashDuration = 1f;

    [Tooltip("Waktu istirahat setelah dash agar musuh tidak langsung nempel lagi.")]
    [SerializeField] private float recoveryTime = 0.25f;

    [Tooltip("Jeda waktu (cooldown) sebelum musuh bisa dash lagi.")]
    [SerializeField] private float dashCooldown = 2f;

    private Rigidbody2D rb;
    private EnemyAI ai;
    private EnemyAnimation enemyAnim;

    private bool isDashing = false;
    private float lastDashTime = -99f;

    // Pastikan durasi total terbaca oleh EnemyCombatController
    public float DashDuration => dashDuration + recoveryTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        ai = GetComponent<EnemyAI>();
        enemyAnim = GetComponentInChildren<EnemyAnimation>(true);
    }

    public void SetPlayer(Transform player) { }

    public bool TryDashAwayFromPlayer()
    {
        if (isDashing || Time.time < lastDashTime + dashCooldown)
            return false;

        if (ai != null && ai.isPerformingAction)
            return false;

        StartCoroutine(PerformDashRoutine());
        return true;
    }

    private IEnumerator PerformDashRoutine()
    {
        isDashing = true;
        lastDashTime = Time.time;

        if (ai != null) ai.OnActionStart();
        if (enemyAnim != null) enemyAnim.PlayDash();

        // Dash MAJU lurus menirukan player
        float directionX = ai != null ? ai.ForwardSign : 1f;

        // [PERBAIKAN KUNCI]: Ubah jadi Kinematic agar tidak terhalang collider atau gesekan tanah
        bool wasKinematic = rb.isKinematic;
        rb.isKinematic = true;

        float timer = 0f;
        while (timer < dashDuration)
        {
            // Karena kinematic, kita gerakkan menggunakan velocity, bukan addforce
            rb.velocity = new Vector2(directionX * dashSpeed, 0f);

            timer += Time.deltaTime;
            yield return null;
        }

        // Rem mendadak dan kembalikan ke tipe semula
        rb.velocity = Vector2.zero;
        rb.isKinematic = wasKinematic;

        // Jeda sejenak
        yield return new WaitForSeconds(recoveryTime);

        isDashing = false;
        if (ai != null) ai.OnActionEnd();
    }
}