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

    [Header("Skill Roots")]
    [SerializeField] private Transform skillRootSword;
    [SerializeField] private Transform skillRootBow;

    [Header("Sword Skills - Auto / Manual Assign")]
    [SerializeField] private Enemy_Sword_SlashCombo _slashCombo;
    [SerializeField] private Enemy_Sword_ChargedStrike _chargedStrike;
    [SerializeField] private Enemy_Sword_Whirlwind _whirlwind;
    [SerializeField] private Enemy_Sword_Riposte _riposte;

    [Header("Bow Skills - Auto / Manual Assign")]
    [SerializeField] private Enemy_Bow_QuickShot _quickShotBow;
    [SerializeField] private Enemy_Bow_FullDraw _fullDrawBow;
    [SerializeField] private Enemy_Bow_PiercingShot _piercingBow;
    [SerializeField] private Enemy_Bow_ConcussiveShot _concussiveBow;

    [Header("Legacy Stats Compatibility")]
    public EnemyCombatStatsData stats = new EnemyCombatStatsData();

    [Header("Fallback Attack Power")]
    [Min(0f)][SerializeField] private float attackPowerOverride = 10f;

    [Header("Defense Reaction Settings")]
    [Min(0f)][SerializeField] private float riposteCooldown = 0.75f;
    [Min(0f)][SerializeField] private float concussiveCooldown = 1.0f;
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
    private float _lastConcussiveTime = -999f;

    public event Action OnSkillStart;
    public event Action OnSkillEnd;

    public Enemy_Sword_SlashCombo slashCombo => _slashCombo;
    public Enemy_Sword_ChargedStrike chargedStrike => _chargedStrike;
    public Enemy_Sword_Whirlwind whirlwind => _whirlwind;
    public Enemy_Sword_Riposte riposte => _riposte;

    public Enemy_Bow_QuickShot quickShotBow => _quickShotBow;
    public Enemy_Bow_FullDraw fullDrawBow => _fullDrawBow;
    public Enemy_Bow_PiercingShot piercingBow => _piercingBow;
    public Enemy_Bow_ConcussiveShot concussiveBow => _concussiveBow;

    public bool HasSwordSkills =>
        _slashCombo != null || _chargedStrike != null || _whirlwind != null || _riposte != null;

    public bool HasBowSkills =>
        _quickShotBow != null || _fullDrawBow != null || _piercingBow != null || _concussiveBow != null;

    public float AttackPower => stats != null ? Mathf.Max(0f, stats.attack) : Mathf.Max(0f, attackPowerOverride);

    public bool IsBusy => (ai != null && ai.isPerformingAction) || _skillBusyCounter > 0 || _reactionBusyCounter > 0;
    public bool IsNotBusy => !IsBusy;

    public bool IsPlayerAttacking => playerSensor != null && playerSensor.IsAttacking;
    public bool HasNewPlayerAttack => playerSensor != null && playerSensor.OffensiveTriggerId != _lastConsumedOffensiveTriggerId;
    public bool CanRiposteCurrentPlayerAction => playerSensor != null && playerSensor.IsCurrentActionRipostable;

    // Menentukan apakah musuh sedang pegang panah dan player terlalu dekat
    public bool CanConcussiveCurrentSituation
    {
        get
        {
            if (_concussiveBow == null) return false;
            if (Time.time < _lastConcussiveTime + concussiveCooldown) return false;

            float distanceToPlayer = 0f;
            if (ai != null && ai.playerTransform != null)
                distanceToPlayer = Vector2.Distance(transform.position, ai.playerTransform.position);

            return _concussiveBow.CanTrigger(distanceToPlayer);
        }
    }

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
        if (ai == null) ai = GetComponent<EnemyAI>();
        if (enemyDash == null) enemyDash = GetComponent<Enemy_Dash>();
        if (playerSensor == null) playerSensor = FindObjectOfType<PlayerAttackSensor>();
        if (stats == null) stats = new EnemyCombatStatsData();
        if (stats.attack <= 0f) stats.attack = attackPowerOverride;

        AutoAssignAllSkills();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            if (ai == null) ai = GetComponent<EnemyAI>();
            if (enemyDash == null) enemyDash = GetComponent<Enemy_Dash>();
            AutoAssignAllSkills();
        }
    }
#endif

    public void RefreshSkillReferences() => AutoAssignAllSkills();
    public void AutoAssignAllSkills() { AutoAssignSwordSkills(); AutoAssignBowSkills(); }

    private void AutoAssignSwordSkills()
    {
        Transform root = skillRootSword;
        if (root == null) root = FindChildRecursive(transform, "SkillRoot_Sword");
        if (root == null) return;

        skillRootSword = root;
        if (_slashCombo == null) _slashCombo = root.GetComponentInChildren<Enemy_Sword_SlashCombo>(true);
        if (_chargedStrike == null) _chargedStrike = root.GetComponentInChildren<Enemy_Sword_ChargedStrike>(true);
        if (_whirlwind == null) _whirlwind = root.GetComponentInChildren<Enemy_Sword_Whirlwind>(true);
        if (_riposte == null) _riposte = root.GetComponentInChildren<Enemy_Sword_Riposte>(true);
    }

    private void AutoAssignBowSkills()
    {
        Transform root = skillRootBow;
        if (root == null) root = FindChildRecursive(transform, "SkillRoot_Bow");
        if (root == null) return;

        skillRootBow = root;
        if (_quickShotBow == null) _quickShotBow = root.GetComponentInChildren<Enemy_Bow_QuickShot>(true);
        if (_fullDrawBow == null) _fullDrawBow = root.GetComponentInChildren<Enemy_Bow_FullDraw>(true);
        if (_piercingBow == null) _piercingBow = root.GetComponentInChildren<Enemy_Bow_PiercingShot>(true);
        if (_concussiveBow == null) _concussiveBow = root.GetComponentInChildren<Enemy_Bow_ConcussiveShot>(true);
    }

    private Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root == null) return null;
        if (root.name == targetName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), targetName);
            if (found != null) return found;
        }
        return null;
    }

    public void InvokeSkillStart() { _skillBusyCounter++; OnSkillStart?.Invoke(); }
    public void InvokeSkillEnd() { if (_skillBusyCounter > 0) _skillBusyCounter--; OnSkillEnd?.Invoke(); }

    // =========================================================
    // DDA Defense & Reaction Weights
    // =========================================================
    public int GetLengahWeight()
    {
        var dda = DDAController.Instance;
        if (dda == null || !dda.HasDefenseProfile) return 100;
        switch (dda.currentPlayerPlaystyle)
        {
            case PlayerPlaystyle.Balanced: return Mathf.Max(0, balancedLengahWeight);
            case PlayerPlaystyle.OffensiveDominant: return Mathf.Max(0, offensiveLengahWeight);
            case PlayerPlaystyle.DefensiveDominant: return Mathf.Max(0, defensiveLengahWeight);
            default: return 100;
        }
    }

    public int GetDashWeight()
    {
        var dda = DDAController.Instance;
        if (dda == null || !dda.HasDefenseProfile) return 0;
        int rawDash = Mathf.Clamp(Mathf.RoundToInt(dda.GetCurrentDefenseDashWeight()), 0, 100);
        if (rawDash <= 0) return 0;
        int budget = Mathf.Clamp(100 - GetLengahWeight(), 0, 100);
        return Mathf.RoundToInt((rawDash / 100f) * budget);
    }

    public int GetRiposteWeight()
    {
        var dda = DDAController.Instance;
        if (dda == null || !dda.HasDefenseProfile) return 0;
        int rawRiposte = Mathf.Clamp(Mathf.RoundToInt(dda.GetCurrentDefenseRiposteWeight()), 0, 100);
        if (rawRiposte <= 0) return 0;
        int budget = Mathf.Clamp(100 - GetLengahWeight(), 0, 100);
        return Mathf.RoundToInt((rawRiposte / 100f) * budget);
    }

    public int GetConcussiveWeight()
    {
        var dda = DDAController.Instance;
        if (dda == null || !dda.HasBowSkillProfile) return 0;

        float[] bowWeights = dda.GetCurrentBowSkillWeightsCopy();
        if (bowWeights == null || bowWeights.Length < 4) return 0;

        // Murni mengambil persentase bobot slot ke-3 (Concussive)
        return Mathf.Clamp(Mathf.RoundToInt(bowWeights[3]), 0, 100);
    }

    // =========================================================
    // Reactions
    // =========================================================

    // Lengah akibat serangan pemain
    public bool StartLengah()
    {
        if (!IsNotBusy || !HasNewPlayerAttack) return false;
        ConsumeCurrentAttackTrigger();
        _reactionBusyCounter++;
        StartCoroutine(ReleaseReactionBusy(lengahBusyDuration));
        return true;
    }

    // Lengah akibat JARAK dekat pemain (Reaksi Bow)
    public bool StartLengahDistance()
    {
        if (!IsNotBusy) return false;
        _reactionBusyCounter++;
        StartCoroutine(ReleaseReactionBusy(lengahBusyDuration));
        return true;
    }

    public bool StartDash()
    {
        if (!IsNotBusy || enemyDash == null) return false;
        if (ai != null && ai.playerTransform != null) enemyDash.SetPlayer(ai.playerTransform);
        if (!enemyDash.TryDashAwayFromPlayer()) return false;

        ConsumeCurrentAttackTrigger();
        _reactionBusyCounter++;
        StartCoroutine(ReleaseReactionBusy(enemyDash.DashDuration));
        return true;
    }

    public bool StartRiposte()
    {
        if (!IsNotBusy || !CanRiposteCurrentPlayerAction || Time.time < _lastRiposteTime + riposteCooldown || _riposte == null) return false;

        float distanceToPlayer = 0f;
        if (ai != null && ai.playerTransform != null)
            distanceToPlayer = Vector2.Distance(transform.position, ai.playerTransform.position);

        if (!_riposte.CanTrigger(distanceToPlayer) || !_riposte.TryStartRiposte()) return false;

        _lastRiposteTime = Time.time;
        ConsumeCurrentAttackTrigger();
        return true;
    }

    public bool StartConcussive()
    {
        if (!IsNotBusy || !CanConcussiveCurrentSituation) return false;

        _concussiveBow.Trigger();

        _lastConcussiveTime = Time.time;
        return true;
    }

    // =========================================================
    // DDA Execution Logic untuk Bow Defense
    // =========================================================
    public bool TryExecuteBowDefenseReaction()
    {
        if (!IsNotBusy || !CanConcussiveCurrentSituation) return false;

        int concWeight = GetConcussiveWeight();

        // Roll murni 0 - 99.
        // Jika concWeight = 100 (player selalu pakai Concussive), peluang AI membalas = 100%.
        int roll = UnityEngine.Random.Range(0, 100);

        if (roll < concWeight)
        {
            return StartConcussive();
        }
        else
        {
            return StartLengahDistance();
        }
    }

    private void ConsumeCurrentAttackTrigger()
    {
        if (playerSensor != null) _lastConsumedOffensiveTriggerId = playerSensor.OffensiveTriggerId;
    }

    private IEnumerator ReleaseReactionBusy(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (_reactionBusyCounter > 0) _reactionBusyCounter--;
    }
}