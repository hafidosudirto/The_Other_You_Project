using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyMovementFSM : MonoBehaviour
{
    private EnemyAI ai;

    public enum MoveState { Idle, Aligning, Chase }

    [Header("Debug Info")]
    [SerializeField] private MoveState currentState;

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Rigidbody2D rb;

    [Header("Movement Settings")]
    public float moveSpeed = 4f;
    public float accel = 40f; 

    [Header("LF2 Style Logic")]
    public float stopDistanceX = 5.0f;
    public float verticalTolerance = 0.2f;

    [Header("Dynamic Skill Range")]
    public float desiredRange = 1.5f;   // default, nanti diubah BT
    public float rangeTolerance = 0.25f;

    private void Awake()
    {
        ai = GetComponent<EnemyAI>();
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        currentState = MoveState.Idle;
    }

    public void Update()
    {
        UpdateState();
        RunState();
        UpdateAnimation();
    }

    private void UpdateAnimation()
    {
        if (ai != null && ai.Animation != null && rb != null)
        {
            // Ambil kecepatan saat ini (0 jika diam, ~moveSpeed jika jalan)
            float currentSpeed = rb.velocity.magnitude;

            // Normalisasi nilai agar cocok dengan Blend Tree (misal 0 sampai 1)
            // Jika Anda pakai Blend Tree sederhana, kirim currentSpeed langsung juga bisa
            float normalizedSpeed = currentSpeed / moveSpeed;

            // Kirim ke script EnemyAnimation
            ai.Animation.SetMoveSpeed(normalizedSpeed);
        }
    }

    public void SetDesiredRange(float range)
    {
        desiredRange = range;
    }


    private void UpdateState()
    {
        if (player == null) return;

        Vector2 toPlayer = player.position - transform.position;
        float absDistX = Mathf.Abs(toPlayer.x);
        float absDistY = Mathf.Abs(toPlayer.y);

        // 1. Vertical alignment
        if (absDistY > verticalTolerance)
        {
            SwitchState(MoveState.Aligning);
            return;
        }

        // 2. Horizontal movement based on DESIRED RANGE (skillRange)
        if (absDistX > desiredRange + rangeTolerance)
        {
            // terlalu jauh → chase
            SwitchState(MoveState.Chase);
            return;
        }

        // 3. Jika sudah dalam window skill range → Idle
        SwitchState(MoveState.Idle);
    }


    private void RunState()
    {
        switch (currentState)
        {
            case MoveState.Idle:
                rb.velocity = Vector2.Lerp(rb.velocity, Vector2.zero, Time.deltaTime * accel);
                break;
            case MoveState.Aligning:
                DoAlignManeuver();
                break;
            case MoveState.Chase:
                DoChase();
                break;
        }
    }

    private void SwitchState(MoveState newState)
    {
        if (newState == currentState) return;
        currentState = newState;
    }

    public void StopImmediately()
    {
        if (rb != null) rb.velocity = Vector2.zero;
        SwitchState(MoveState.Idle);
    }

    private void DoAlignManeuver()
    {
        float dirY = Mathf.Sign(player.position.y - transform.position.y);
        Vector2 targetVel = new Vector2(0, dirY * moveSpeed);
        rb.velocity = Vector2.Lerp(rb.velocity, targetVel, Time.deltaTime * accel);
    }

    private void DoChase()
    {
        Vector2 dir = (player.position - transform.position).normalized;
        Vector2 targetVel = dir * moveSpeed;

        // Instant turn logic
        if (Vector2.Dot(rb.velocity.normalized, dir) < 0)
            rb.velocity = targetVel;
        else
            rb.velocity = Vector2.Lerp(rb.velocity, targetVel, Time.deltaTime * accel);
    }
}