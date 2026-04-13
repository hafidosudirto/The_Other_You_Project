using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class CrabMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 2f;
    
    [Header("Attack Settings")]
    [SerializeField] private float damageAmount = 10f;
    [SerializeField] private float attackCooldown = 1.2f;

    private Rigidbody2D rb;
    private Animator anim;
    private Transform playerTransform;
    private CharacterBase playerStats;
    private float nextAttackTime;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        
        // Pengaturan Fisika Dasar
        rb.gravityScale = 0;      // Agar tidak jatuh ke bawah layar
        rb.freezeRotation = true; // Agar tidak berputar saat menabrak

        // Cari Player sekali saja di awal
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) 
        {
            playerTransform = playerObj.transform;
            playerStats = playerObj.GetComponent<CharacterBase>();
        }
    }

    void Update()
    {
        // Berhenti jika player tidak ada atau sudah mati
        if (playerTransform == null || (playerStats != null && playerStats.currentHP <= 0)) 
        {
            rb.velocity = Vector2.zero;
            HandleAnimation(false);
            return;
        }

        MoveTowardsPlayer();
    }

    void MoveTowardsPlayer()
    {
        // Kalkulasi arah menuju player (Full Chase)
        Vector2 direction = (playerTransform.position - transform.position).normalized;
        
        // Gerakkan Rigidbody
        rb.velocity = direction * moveSpeed;

        // Atur Arah Hadap (Flip) berdasarkan sumbu X
        if (direction.x != 0)
        {
            float flipDir = (direction.x > 0) ? 1f : -1f;
            Vector3 scale = transform.localScale;
            scale.x = flipDir * Mathf.Abs(scale.x);
            transform.localScale = scale;
        }

        HandleAnimation(true);
    }

    void HandleAnimation(bool moving)
    {
        if (anim != null)
        {
            // Parameter 'isWalking' (Bool) di Animator
            anim.SetBool("isWalking", moving);
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        // Logika Damage jika menabrak Player
        if (collision.gameObject.CompareTag("Player") && Time.time >= nextAttackTime)
        {
            if (playerStats != null && playerStats.currentHP > 0)
            {
                // Memanggil fungsi dari CharacterBase.cs
                playerStats.TakeDamage(damageAmount, gameObject);
                
                nextAttackTime = Time.time + attackCooldown;
                Debug.Log($"Kepiting menjepit! HP Player: {playerStats.currentHP}");
            }
        }
    }
}