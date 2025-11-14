using UnityEngine;

// Script ini mengatur movement dasar Enemy.
// Enemy memakai Rigidbody2D Kinematic sehingga movement dilakukan manual.
// AI memiliki tiga state: Chase, Idle, Retreat.
// Pemilihan state memakai probabilitas yang bisa diatur lewat Inspector.
//
// Flow AI:
// - Jika Player berada dalam detectionRadius, AI aktif.
// - AI memilih state berdasarkan probability slider.
// - State berlangsung beberapa detik lalu di-random ulang.
// - Enemy selalu menghadap Player.

[RequireComponent(typeof(Enemy))]
public class Move_Chase : MonoBehaviour
{
    public enum AIState
    {
        Chasing,
        Idling,
        Retreating
    }

    private Enemy enemy;
    private Transform player;

    // Radius di mana Enemy mulai aktif mengejar Player.
    public float detectionRadius = 8f;

    // Lama durasi state (acak antara min–max)
    public float minActionTime = 1f;
    public float maxActionTime = 2.5f;

    // Action Probabilities (%) — seperti screenshot kamu
    [Header("Action Probabilities (%)")]
    [Range(0, 100)] public float chaseProbability = 60f;
    [Range(0, 100)] public float idleProbability = 30f;
    [Range(0, 100)] public float retreatProbability = 20f;

    private AIState currentState;
    private float actionTimer;

    void Awake()
    {
        enemy = GetComponent<Enemy>();

        // Mencari Player pertama kali berdasarkan tag.
        GameObject target = GameObject.FindGameObjectWithTag("Player");
        if (target != null)
            player = target.transform;
    }

    void Start()
    {
        ChooseNewAction();
    }

    void Update()
    {
        // Jika Player diswap (W0 → W1 → W2), ambil ulang berdasarkan tag.
        if (player == null)
        {
            GameObject target = GameObject.FindGameObjectWithTag("Player");
            if (target != null) player = target.transform;
        }

        if (player == null)
            return;

        // Jika Enemy tidak bisa bergerak (stagger atau mati), hentikan movement.
        if (!enemy.CanAct())
            return;

        float distance = Vector2.Distance(transform.position, player.position);

        // Jika Player terlalu jauh, hentikan movement tapi tetap flip.
        if (distance > detectionRadius)
        {
            FlipTowardsPlayer();
            return;
        }

        // Timer untuk durasi state.
        actionTimer -= Time.deltaTime;
        if (actionTimer <= 0f)
            ChooseNewAction();

        ExecuteAction();
        FlipTowardsPlayer();
    }

    // Memilih state baru berdasarkan probabilitas.
    void ChooseNewAction()
    {
        float total = chaseProbability + idleProbability + retreatProbability;
        float randomPoint = Random.Range(0, total);

        if (randomPoint < chaseProbability)
        {
            currentState = AIState.Chasing;
        }
        else if (randomPoint < chaseProbability + idleProbability)
        {
            currentState = AIState.Idling;
        }
        else
        {
            currentState = AIState.Retreating;
        }

        actionTimer = Random.Range(minActionTime, maxActionTime);
    }

    // Eksekusi gerakan sesuai state.
    void ExecuteAction()
    {
        Vector3 pos = transform.position;
        Vector3 targetPos = player.position;

        float speed = enemy.moveSpeed * Time.deltaTime;

        switch (currentState)
        {
            case AIState.Chasing:
                transform.position = Vector2.MoveTowards(pos, targetPos, speed);
                break;

            case AIState.Idling:
                // Diam di tempat.
                break;

            case AIState.Retreating:
                transform.position = Vector2.MoveTowards(pos, targetPos, -speed);
                break;
        }
    }

    // Enemy menghadap ke Player.
    void FlipTowardsPlayer()
    {
        if (player == null) return;

        if (player.position.x > transform.position.x && !enemy.isFacingRight)
            enemy.Flip();
        else if (player.position.x < transform.position.x && enemy.isFacingRight)
            enemy.Flip();
    }

    // Visual radius detection.
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
