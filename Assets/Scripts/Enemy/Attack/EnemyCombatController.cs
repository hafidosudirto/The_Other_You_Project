using System;
using System.Collections;
using UnityEngine;

[System.Serializable]
public sealed class EnemyCombatStatsData
{
    public float attack = 10f;
}

[RequireComponent(typeof(EnemyAI))]
public sealed class EnemyCombatController : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private EnemyAI ai;
    [SerializeField] private Enemy_Dash enemyDash;
    [SerializeField] private PlayerAttackSensor playerSensor;

    [Header("Skill Root (struktur prefab lama)")]
    [SerializeField] private Transform skillRootSword;

    [Header("Sword Skills - Auto / Manual Assign")]
    [SerializeField] private Enemy_Sword_SlashCombo _slashCombo;
    [SerializeField] private Enemy_Sword_ChargedStrike _chargedStrike;
    [SerializeField] private Enemy_Sword_Whirlwind _whirlwind;
    [SerializeField] private Enemy_Sword_Riposte _riposte;

    [Header("Legacy Stats Compatibility")]
    public EnemyCombatStatsData stats = new EnemyCombatStatsData();

    [Header("Fallback Attack Power")]
    [Min(0f)][SerializeField] private float attackPowerOverride = 10f;

    [Header("Defense Reaction Settings")]
    [Min(0f)][SerializeField] private float riposteCooldown = 0.75f;
    [Min(0f)][SerializeField] private float lengahBusyDuration = 0.28f;

    [Header("Balanced Openings")]
    [Tooltip("Semakin besar, semakin sering enemy memberi celah di mode Balanced.")]
    [Min(0)][SerializeField] private int balancedLengahWeight = 45;

    [Tooltip("Sedikit celah pada mode OffensiveDominant agar enemy tidak terlalu pasif.")]
    [Min(0)][SerializeField] private int offensiveLengahWeight = 20;

    [Tooltip("Biasanya 0 agar mode DefensiveDominant benar-benar reaktif.")]
    [Min(0)][SerializeField] private int defensiveLengahWeight = 0;

    private int _skillBusyCounter = 0;
    private int _reactionBusyCounter = 0;
    private int _lastConsumedOffensiveTriggerId = -1;
    private float _lastRiposteTime = -999f;

    public event Action OnSkillStart;
    public event Action OnSkillEnd;

    // =========================================================
    // API lama yang masih dipakai node / EnemyAI lama
    // =========================================================
    public Enemy_Sword_SlashCombo slashCombo => _slashCombo;
    public Enemy_Sword_ChargedStrike chargedStrike => _chargedStrike;
    public Enemy_Sword_Whirlwind whirlwind => _whirlwind;
    public Enemy_Sword_Riposte riposte => _riposte;

    public float AttackPower
    {
        get
        {
            if (stats != null)
                return Mathf.Max(0f, stats.attack);

            return Mathf.Max(0f, attackPowerOverride);
        }
    }

    public bool IsBusy =>
        (ai != null && ai.isPerformingAction) ||
        _skillBusyCounter > 0 ||
        _reactionBusyCounter > 0;

    public bool IsNotBusy => !IsBusy;

    // =========================================================
    // API untuk defense tree reaktif
    // =========================================================
    public bool IsPlayerAttacking =>
        playerSensor != null && playerSensor.IsAttacking;

    public bool HasNewPlayerAttack =>
        playerSensor != null &&
        playerSensor.OffensiveTriggerId != _lastConsumedOffensiveTriggerId;

    public bool CanRiposteCurrentPlayerAction =>
        playerSensor != null && playerSensor.IsCurrentActionRipostable;

    public bool HasDefenseProfile
    {
        get
        {
            var dda = DDAController.Instance;
            return dda != null && dda.HasDefenseProfile;
        }
    }

    private void Awake()
    {
        if (ai == null)
            ai = GetComponent<EnemyAI>();

        if (enemyDash == null)
            enemyDash = GetComponent<Enemy_Dash>();

        if (playerSensor == null)
            playerSensor = FindObjectOfType<PlayerAttackSensor>();

        if (stats == null)
            stats = new EnemyCombatStatsData();

        if (stats.attack <= 0f)
            stats.attack = attackPowerOverride;

        AutoAssignSwordSkills();
    }

    // =========================================================
    // Auto-assign skill sesuai struktur:
    // Enemy
    //  └─ SkillRoot_Sword
    //      ├─ Enemy_Sword_SlashCombo
    //      ├─ Enemy_Sword_ChargedStrike
    //      ├─ Enemy_Sword_Riposte
    //      └─ Enemy_Sword_Whirlwind
    // =========================================================
    private void AutoAssignSwordSkills()
    {
        Transform root = skillRootSword;

        if (root == null)
            root = FindChildRecursive(transform, "SkillRoot_Sword");

        if (root == null)
        {
            Debug.LogWarning("[EnemyCombatController] SkillRoot_Sword tidak ditemukan. Auto-assign skill sword dilewati.");
            return;
        }

        if (_slashCombo == null)
            _slashCombo = root.GetComponentInChildren<Enemy_Sword_SlashCombo>(true);

        if (_chargedStrike == null)
            _chargedStrike = root.GetComponentInChildren<Enemy_Sword_ChargedStrike>(true);

        if (_whirlwind == null)
            _whirlwind = root.GetComponentInChildren<Enemy_Sword_Whirlwind>(true);

        if (_riposte == null)
            _riposte = root.GetComponentInChildren<Enemy_Sword_Riposte>(true);
    }

    private Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root == null)
            return null;

        if (root.name == targetName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), targetName);
            if (found != null)
                return found;
        }

        return null;
    }

    // =========================================================
    // Kompatibilitas lifecycle skill lama
    // =========================================================
    public void InvokeSkillStart()
    {
        _skillBusyCounter++;
        OnSkillStart?.Invoke();
    }

    public void InvokeSkillEnd()
    {
        if (_skillBusyCounter > 0)
            _skillBusyCounter--;

        OnSkillEnd?.Invoke();
    }

    // =========================================================
    // Bobot defense dibaca dari DDAController
    // =========================================================
    public int GetLengahWeight()
    {
        var dda = DDAController.Instance;

        // Jika belum ada histori defense valid dari stage sebelumnya,
        // enemy jangan menebak dash/riposte. Beri opening penuh saja.
        if (dda == null || !dda.HasDefenseProfile)
            return 100;

        switch (dda.currentPlayerPlaystyle)
        {
            case PlayerPlaystyle.Balanced:
                return Mathf.Max(0, balancedLengahWeight);

            case PlayerPlaystyle.OffensiveDominant:
                return Mathf.Max(0, offensiveLengahWeight);

            case PlayerPlaystyle.DefensiveDominant:
                return Mathf.Max(0, defensiveLengahWeight);

            default:
                return 100;
        }
    }

    public int GetDashWeight()
    {
        var dda = DDAController.Instance;

        if (dda == null || !dda.HasDefenseProfile)
            return 0;

        int rawDash = Mathf.Clamp(Mathf.RoundToInt(dda.GetCurrentDefenseDashWeight()), 0, 100);

        // Kalau histori dash player nol, enemy juga wajib nol.
        if (rawDash <= 0)
            return 0;

        int budget = Mathf.Clamp(100 - GetLengahWeight(), 0, 100);
        return Mathf.RoundToInt((rawDash / 100f) * budget);
    }

    public int GetRiposteWeight()
    {
        var dda = DDAController.Instance;

        if (dda == null || !dda.HasDefenseProfile)
            return 0;

        int rawRiposte = Mathf.Clamp(Mathf.RoundToInt(dda.GetCurrentDefenseRiposteWeight()), 0, 100);

        if (rawRiposte <= 0)
            return 0;

        int budget = Mathf.Clamp(100 - GetLengahWeight(), 0, 100);
        return Mathf.RoundToInt((rawRiposte / 100f) * budget);
    }

    // =========================================================
    // Reaksi defense
    // =========================================================
    public bool StartLengah()
    {
        if (!IsNotBusy)
            return false;

        if (!HasNewPlayerAttack)
            return false;

        ConsumeCurrentAttackTrigger();
        _reactionBusyCounter++;
        StartCoroutine(ReleaseReactionBusy(lengahBusyDuration));
        return true;
    }

    public bool StartDash()
    {
        if (!IsNotBusy)
            return false;

        if (enemyDash == null)
            return false;

        if (ai != null && ai.playerTransform != null)
            enemyDash.SetPlayer(ai.playerTransform);

        bool started = enemyDash.TryDashAwayFromPlayer();
        if (!started)
            return false;

        ConsumeCurrentAttackTrigger();
        _reactionBusyCounter++;
        StartCoroutine(ReleaseReactionBusy(enemyDash.DashDuration));
        return true;
    }

    public bool StartRiposte()
    {
        if (!IsNotBusy)
            return false;

        if (!CanRiposteCurrentPlayerAction)
            return false;

        if (Time.time < _lastRiposteTime + riposteCooldown)
            return false;

        if (_riposte == null)
            return false;

        float distanceToPlayer = 0f;
        if (ai != null && ai.playerTransform != null)
            distanceToPlayer = Vector2.Distance(transform.position, ai.playerTransform.position);

        if (!_riposte.CanTrigger(distanceToPlayer))
            return false;

        bool started = _riposte.TryStartRiposte();
        if (!started)
            return false;

        _lastRiposteTime = Time.time;
        ConsumeCurrentAttackTrigger();
        return true;
    }

    private void ConsumeCurrentAttackTrigger()
    {
        if (playerSensor == null)
            return;

        _lastConsumedOffensiveTriggerId = playerSensor.OffensiveTriggerId;
    }

    private IEnumerator ReleaseReactionBusy(float duration)
    {
        yield return new WaitForSeconds(duration);

        if (_reactionBusyCounter > 0)
            _reactionBusyCounter--;
    }
}