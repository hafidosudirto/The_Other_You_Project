using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyMovementFSM : MonoBehaviour
{
    public enum MoveState
    {
        Idle,
        Chase,
        Retreat
    }

    private MoveState currentState;

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Rigidbody2D rb;

    [Header("Movement Settings")]
    public float moveSpeed = 3f;
    public float accel = 10f;

    [Header("Sword Safe Zone")]
    public float idealSwordDistance = 1.8f;
    public float tolerance = 0.25f;

    private float MinSafe => idealSwordDistance - tolerance;
    private float MaxSafe => idealSwordDistance + tolerance;

    private void Start()
    {
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player").transform;

        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        currentState = MoveState.Idle;
    }

    public void Update()
    {
        UpdateState();
        RunState();
    }

    // ======================================================================
    //  FSM STATE DECISION (ANTI-JITTER FINAL)
    // ======================================================================

    private void UpdateState()
    {
        float distance = Vector2.Distance(transform.position, player.position);
        var playerTrend = DataTracker.Instance.CurrentDistanceState;

        // ==========================================================
        // 1. PLAYER TREND PRIORITY (anti jitter utama)
        // ==========================================================
        if (playerTrend == PlayerDistanceState.Chase)
        {
            // Player mendekat → enemy harus retreat
            SwitchState(MoveState.Retreat);
            return;
        }

        if (playerTrend == PlayerDistanceState.Retreat)
        {
            // Player menjauh → enemy harus chase
            SwitchState(MoveState.Chase);
            return;
        }

        // ==========================================================
        // 2. PLAYER IDLE → gunakan safezone
        // ==========================================================
        if (playerTrend == PlayerDistanceState.Idle)
        {
            // Jarak aman → tetap diam
            if (distance >= MinSafe && distance <= MaxSafe)
            {
                SwitchState(MoveState.Idle);
                return;
            }

            // Terlalu dekat → mundur
            if (distance < MinSafe)
            {
                SwitchState(MoveState.Retreat);
                return;
            }

            // Terlalu jauh → kejar
            if (distance > MaxSafe)
            {
                SwitchState(MoveState.Chase);
                return;
            }
        }
    }

    // ======================================================================
    //   FSM STATE EXECUTION
    // ======================================================================

    private void RunState()
    {
        switch (currentState)
        {
            case MoveState.Idle: DoIdle(); break;
            case MoveState.Chase: DoChase(); break;
            case MoveState.Retreat: DoRetreat(); break;
        }
    }

    private void SwitchState(MoveState newState)
    {
        if (newState == currentState) return;
        currentState = newState;
    }

    // ======================================================================
    //  STATE LOGIC
    // ======================================================================

    private void DoIdle()
    {
        rb.velocity = Vector2.Lerp(rb.velocity, Vector2.zero, Time.deltaTime * accel);
    }

    private void DoChase()
    {
        Vector2 dir = (player.position - transform.position).normalized;
        Vector2 targetVel = dir * moveSpeed;

        rb.velocity = Vector2.Lerp(rb.velocity, targetVel, Time.deltaTime * accel);
    }

    private void DoRetreat()
    {
        Vector2 dir = (transform.position - player.position).normalized;
        Vector2 targetVel = dir * moveSpeed;

        rb.velocity = Vector2.Lerp(rb.velocity, targetVel, Time.deltaTime * accel);
    }
}

