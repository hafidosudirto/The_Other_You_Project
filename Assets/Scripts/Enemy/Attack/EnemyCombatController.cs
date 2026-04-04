using UnityEngine;
using System;

public class EnemyCombatController : MonoBehaviour
{
    // Event untuk Action-Lock
    public event Action OnSkillStart;
    public event Action OnSkillEnd;

    /// <summary>
    /// Global busy flag untuk mencegah BT menembakkan skill lain saat skill berjalan.
    /// </summary>
    public bool IsBusy { get; private set; }

    [Header("Stats (ambil dari CharacterBase)")]
    public CharacterBase stats;

    public float AttackPower => stats != null ? stats.attack : 10f;

    [Header("Sword Skills")]
    public Enemy_Sword_SlashCombo slashCombo;
    public Enemy_Sword_Whirlwind whirlwind;
    public Enemy_Sword_ChargedStrike chargedStrike;
    public Enemy_Sword_Riposte riposte;

    private void Awake()
    {
        if (stats == null)
            stats = GetComponent<CharacterBase>();

        if (slashCombo == null)
            slashCombo = GetComponentInChildren<Enemy_Sword_SlashCombo>();

        if (whirlwind == null)
            whirlwind = GetComponentInChildren<Enemy_Sword_Whirlwind>();

        if (chargedStrike == null)
            chargedStrike = GetComponentInChildren<Enemy_Sword_ChargedStrike>();

        if (riposte == null)
            riposte = GetComponentInChildren<Enemy_Sword_Riposte>();
    }

    public void InvokeSkillStart()
    {
        IsBusy = true;
        OnSkillStart?.Invoke();
    }

    public void InvokeSkillEnd()
    {
        IsBusy = false;
        OnSkillEnd?.Invoke();
    }

    // =========================================================
    // API untuk Defense Tree
    // =========================================================

    [Header("Defense Tree Wiring")]
    [SerializeField] private PlayerAttackSensor playerAttackSensor;
    [SerializeField] private Enemy_Dash dash;
    [SerializeField] private Transform playerTransform;

    [Header("Defense Timing")]
    [SerializeField] private float lengahDuration = 0.20f;
    [SerializeField] private float reactionCooldown = 0.05f;

    private int _lastReactedAttackId = 0;
    private float _nextAllowedReactionTime = 0f;

    public bool IsNotBusy => !IsBusy;
    public bool IsPlayerAttacking => playerAttackSensor != null && playerAttackSensor.IsAttacking;

    public bool HasNewPlayerAttack
    {
        get
        {
            if (playerAttackSensor == null) return false;
            int id = playerAttackSensor.AttackId;
            return id != 0 && id != _lastReactedAttackId;
        }
    }

    private void Start()
    {
        if (playerAttackSensor == null)
            playerAttackSensor = FindObjectOfType<PlayerAttackSensor>();

        if (playerTransform == null && playerAttackSensor != null)
            playerTransform = playerAttackSensor.transform;

        if (dash == null)
            dash = GetComponentInChildren<Enemy_Dash>();

        if (dash != null && playerTransform != null)
            dash.SetPlayer(playerTransform);
    }

    private bool CanReactNow()
    {
        if (!IsPlayerAttacking) return false;
        if (!HasNewPlayerAttack) return false;
        if (!IsNotBusy) return false;
        if (Time.time < _nextAllowedReactionTime) return false;
        return true;
    }

    private void ConsumeReaction()
    {
        if (playerAttackSensor != null)
            _lastReactedAttackId = playerAttackSensor.AttackId;

        _nextAllowedReactionTime = Time.time + reactionCooldown;
    }

    public bool StartLengah()
    {
        if (!CanReactNow()) return false;

        ConsumeReaction();
        StartCoroutine(LengahRoutine());
        return true;
    }

    private System.Collections.IEnumerator LengahRoutine()
    {
        InvokeSkillStart();
        yield return new WaitForSeconds(lengahDuration);
        InvokeSkillEnd();
    }

    public bool StartDash()
    {
        if (!CanReactNow()) return false;

        ConsumeReaction();

        if (dash == null)
        {
            StartCoroutine(LengahRoutine());
            return true;
        }

        if (playerTransform != null)
            dash.SetPlayer(playerTransform);

        bool ok = dash.TryDashAwayFromPlayer();
        if (!ok)
        {
            StartCoroutine(LengahRoutine());
            return true;
        }

        StartCoroutine(LockRoutine(dash.DashDuration));
        return true;
    }

    private System.Collections.IEnumerator LockRoutine(float duration)
    {
        InvokeSkillStart();
        yield return new WaitForSeconds(duration);
        InvokeSkillEnd();
    }

    public bool StartRiposte()
    {
        if (!CanReactNow()) return false;

        ConsumeReaction();

        if (riposte == null)
        {
            StartCoroutine(LengahRoutine());
            return true;
        }

        bool ok = riposte.TryStartRiposte();
        if (!ok)
        {
            StartCoroutine(LengahRoutine());
            return true;
        }

        return true;
    }
}