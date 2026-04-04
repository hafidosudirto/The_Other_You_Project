using UnityEngine;

public enum PlayerActionType
{
    Offensive,
    Defensive,
}

public enum WeaponType
{
    None,
    Sword,
    Bow,
    Gauntlet
}

public enum PlayerDistanceState
{
    Idle,
    Chase,
    Retreat
}

public enum SwordSkillSlot
{
    SlashCombo = 0,
    Whirlwind = 1,
    ChargedStrike = 2,
    Riposte = 3,
}

public class DataTracker : MonoBehaviour
{
    public static DataTracker Instance { get; private set; }

    private int offensiveCount;
    private int defensiveCount;

    private int swordUsageCount;
    private int bowUsageCount;
    private int gauntletUsageCount;

    private readonly int[] swordSkillCounts = new int[4];

    private WeaponType lastUsedWeapon = WeaponType.None;

    [Header("Distance Tracking")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform enemyTransform;
    [SerializeField] private float distanceCheckInterval = 0.1f;
    [SerializeField] private float idleMovementThreshold = 0.03f;

    public PlayerDistanceState CurrentDistanceState { get; private set; }

    private Vector3 lastPlayerPos;
    private float lastDistance;
    private float lastCheckTime;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (playerTransform == null)
            playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (enemyTransform == null)
            Debug.LogWarning("[DataTracker] Enemy Transform belum di-assign.");

        if (playerTransform != null && enemyTransform != null)
        {
            lastPlayerPos = playerTransform.position;
            lastDistance = Vector2.Distance(playerTransform.position, enemyTransform.position);
        }

        lastCheckTime = Time.time;
    }

    private void Update()
    {
        if (Time.time > lastCheckTime + distanceCheckInterval)
        {
            TrackPlayerMovement();
            lastCheckTime = Time.time;
        }
    }

    private void TrackPlayerMovement()
    {
        if (playerTransform == null || enemyTransform == null)
            return;

        Vector3 currentPlayerPos = playerTransform.position;
        Vector3 delta = currentPlayerPos - lastPlayerPos;

        float playerMovement = delta.magnitude;
        float currentDistance = Vector2.Distance(currentPlayerPos, enemyTransform.position);

        if (playerMovement < idleMovementThreshold)
            CurrentDistanceState = PlayerDistanceState.Idle;
        else
            CurrentDistanceState = (currentDistance < lastDistance)
                ? PlayerDistanceState.Chase
                : PlayerDistanceState.Retreat;

        lastPlayerPos = currentPlayerPos;
        lastDistance = currentDistance;
    }

    // ================================================================
    // GENERIC ACTION TRACKING (BACKWARD COMPATIBLE)
    // ================================================================
    public void RecordAction(PlayerActionType actionType, WeaponType weaponType)
    {
        if (weaponType != WeaponType.None)
            lastUsedWeapon = weaponType;

        AddPlaystyleCount(actionType);
        AddWeaponUsage(weaponType);

        DebugHub.DDA($"Action Recorded -> O={offensiveCount}, D={defensiveCount}, Weapon={lastUsedWeapon}");
    }

    // ================================================================
    // REAL SWORD SKILL TRACKING (UNTUK NORMALISASI RIIL)
    // ================================================================
    public void RecordSwordSkill(SwordSkillSlot slot, PlayerActionType actionType)
    {
        AddPlaystyleCount(actionType);

        lastUsedWeapon = WeaponType.Sword;
        swordUsageCount++;
        swordSkillCounts[(int)slot]++;

        DebugHub.DDA(
            $"Sword Skill Recorded -> {slot} | O={offensiveCount}, D={defensiveCount}, " +
            $"SwordUse={swordUsageCount}, SkillCounts=[{swordSkillCounts[0]}, {swordSkillCounts[1]}, {swordSkillCounts[2]}, {swordSkillCounts[3]}]"
        );
    }

    public void RecordSwordSlashCombo() => RecordSwordSkill(SwordSkillSlot.SlashCombo, PlayerActionType.Offensive);
    public void RecordSwordWhirlwind() => RecordSwordSkill(SwordSkillSlot.Whirlwind, PlayerActionType.Offensive);
    public void RecordSwordChargedStrike() => RecordSwordSkill(SwordSkillSlot.ChargedStrike, PlayerActionType.Offensive);
    public void RecordSwordRiposte() => RecordSwordSkill(SwordSkillSlot.Riposte, PlayerActionType.Defensive);

    private void AddPlaystyleCount(PlayerActionType actionType)
    {
        if (actionType == PlayerActionType.Offensive)
            offensiveCount++;
        else
            defensiveCount++;
    }

    private void AddWeaponUsage(WeaponType weaponType)
    {
        switch (weaponType)
        {
            case WeaponType.Sword:
                swordUsageCount++;
                break;
            case WeaponType.Bow:
                bowUsageCount++;
                break;
            case WeaponType.Gauntlet:
                gauntletUsageCount++;
                break;
        }
    }

    public void FinalizeStageData()
    {
        if (DDAController.Instance == null)
        {
            Debug.LogWarning("[DataTracker] DDAController missing!");
            return;
        }

        DDAController.Instance.UpdatePlayerProfile(
            offensiveCount,
            defensiveCount,
            swordUsageCount,
            bowUsageCount,
            gauntletUsageCount,
            swordSkillCounts
        );

        DebugHub.DDA(
            $"Stage Final Data Sent -> O={offensiveCount}, D={defensiveCount}, " +
            $"WeaponUse[S={swordUsageCount}, B={bowUsageCount}, G={gauntletUsageCount}], " +
            $"SwordSkillCounts=[{swordSkillCounts[0]}, {swordSkillCounts[1]}, {swordSkillCounts[2]}, {swordSkillCounts[3]}]"
        );

        ResetStageData();
    }

    private void ResetStageData()
    {
        offensiveCount = 0;
        defensiveCount = 0;

        swordUsageCount = 0;
        bowUsageCount = 0;
        gauntletUsageCount = 0;

        for (int i = 0; i < swordSkillCounts.Length; i++)
            swordSkillCounts[i] = 0;

        lastUsedWeapon = WeaponType.None;
    }
}
