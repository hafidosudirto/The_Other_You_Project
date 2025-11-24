using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class EnemyAI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerTransform;

    [Header("Base Movement Settings")]
    [SerializeField] private float baseMoveSpeed = 3f;
    [SerializeField] private float retreatSpeedMultiplier = 1.3f;

    // Weapon adaptive range
    private float weaponRange = 1.5f;

    // Adaptive Safe Zone Distances
    private float chaseDistance;
    private float retreatDistance;

    // Behavior Tree root
    private Node movementRoot;

    private void Start()
    {
        // Cari player kalau belum di-assign
        if (playerTransform == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj)
                playerTransform = playerObj.transform;
        }

        // Ambil weapon player dari DDAController → musuh mirror player
        InitializeWeaponAdaptiveRanges();

        // Build Movement BT
        BuildMovementTree();
    }

    // ================================================================
    //  ADAPTIVE SAFE ZONE CONFIGURATION
    // ================================================================
    private void InitializeWeaponAdaptiveRanges()
    {
        // Default range
        weaponRange = 1.5f;

        if (DDAController.Instance != null)
        {
            switch (DDAController.Instance.currentPlayerWeapon)
            {
                case WeaponType.Gauntlet:
                    weaponRange = 1.0f;  // range dekat
                    break;
                case WeaponType.Sword:
                    weaponRange = 1.6f;  // range sedang
                    break;
                case WeaponType.Bow:
                    weaponRange = 6.0f;  // range jauh
                    break;
            }
        }

        // Safe zone distances berdasarkan weapon
        chaseDistance = weaponRange * 1.8f;   // terlalu jauh → kejar
        retreatDistance = weaponRange * 0.6f;   // terlalu dekat → mundur
    }

    // ================================================================
    //  UPDATE LOOP
    // ================================================================
    private void Update()
    {
        if (movementRoot != null)
            movementRoot.Evaluate();
    }

    // ================================================================
    //  SAFE ZONE LOGIC
    // ================================================================
    public PlayerDistanceState EvaluateDistanceToPlayer()
    {
        if (playerTransform == null)
            return PlayerDistanceState.Idle;

        float dist = Vector2.Distance(transform.position, playerTransform.position);

        if (dist > chaseDistance)
            return PlayerDistanceState.Chase;     // player terlalu jauh → kejar

        if (dist < retreatDistance)
            return PlayerDistanceState.Retreat;   // player terlalu dekat → mundur

        return PlayerDistanceState.Idle;          // di jarak ideal → diam
    }

    // ================================================================
    //  BUILD MOVEMENT BEHAVIOR TREE
    // ================================================================
    private void BuildMovementTree()
    {
        Node chaseSequence = new Sequence(new List<Node>
        {
            new IsPlayerTooFarNode(this),
            new ChaseActionNode(this)
        });

        Node retreatSequence = new Sequence(new List<Node>
        {
            new IsPlayerTooCloseNode(this),
            new RetreatActionNode(this)
        });

        Node idleSequence = new Sequence(new List<Node>
        {
            new IsPlayerInSafeZoneNode(this),
            new IdleActionNode(this)
        });

        movementRoot = new Selector(new List<Node>
        {
            chaseSequence,
            retreatSequence,
            idleSequence
        });
    }

    // ================================================================
    //  MOVEMENT EXECUTION
    // ================================================================
    public void ChasePlayer()
    {
        if (playerTransform == null) return;

        Vector3 dir = (playerTransform.position - transform.position).normalized;
        transform.position += dir * baseMoveSpeed * Time.deltaTime;
    }

    public void RetreatFromPlayer()
    {
        if (playerTransform == null) return;

        Vector3 dir = (transform.position - playerTransform.position).normalized;
        float speed = baseMoveSpeed * retreatSpeedMultiplier;
        transform.position += dir * speed * Time.deltaTime;
    }

    public void StopMoving()
    {
        // Jika pakai Rigidbody:
        // rb.velocity = Vector2.zero;
    }
}