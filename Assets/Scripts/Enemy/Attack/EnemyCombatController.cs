using UnityEngine;
using System;

public class EnemyCombatController : MonoBehaviour
{
    // Event untuk Action-Lock (EnemyAI / MovementFSM dengar ini)
    public event Action OnSkillStart;
    public event Action OnSkillEnd;

    /// <summary>
    /// Global "busy" flag untuk mencegah BT node menembakkan skill lain saat skill berjalan.
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

    // Dipanggil skill saat dimulai
    public void InvokeSkillStart()
    {
        IsBusy = true;
        OnSkillStart?.Invoke();
    }

    // Dipanggil skill saat selesai
    public void InvokeSkillEnd()
    {
        IsBusy = false;
        OnSkillEnd?.Invoke();
    }

    // =========================================================
    // Tambahan API untuk Defense Tree (agar Enemy_DefenseTree tidak error)
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

    // Dipakai Enemy_DefenseTree
    public bool IsNotBusy => !IsBusy;

    // Dipakai Enemy_DefenseTree
    public bool IsPlayerAttacking => playerAttackSensor != null && playerAttackSensor.IsAttacking;

    // Dipakai Enemy_DefenseTree: mencegah defense berulang untuk serangan player yang sama
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
        // *Best-effort wiring* (tetap disarankan isi manual di Inspector)
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

    // 50% cabang lengah: tidak melakukan skill, tetapi menghabiskan kesempatan respon
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

    // 25% cabang dash: jika tidak bisa dash (cooldown/null), jatuh ke lengah tetapi tetap "menghabiskan" respon
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

    // 25% cabang riposte: memakai API baru TryStartRiposte (akan kita tambahkan di Enemy_Sword_Riposte)
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
