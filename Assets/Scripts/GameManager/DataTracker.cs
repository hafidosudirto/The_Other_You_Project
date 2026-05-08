using UnityEngine;
using TMPro;

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

public enum BowSkillSlot
{
    QuickShot = 0,
    PiercingShot = 1,
    FullDraw = 2,
    ConcussiveShot = 3
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
    private readonly int[] bowSkillCounts = new int[4];

    // =========================================================
    // REAL-TIME PLAYER SKILL DEBUG UI
    // Catatan penting:
    // - Data ini bukan hasil DDA.
    // - Data ini hanya mencatat skill yang benar-benar dikeluarkan player pada stage berjalan.
    // - Data ini tidak ikut direset oleh FinalizeStageData().
    // - Reset debug dilakukan saat masuk ke stage berikutnya melalui ResetSkillDebugData().
    // =========================================================
    [Header("Real-Time Player Skill Debug UI - Sword")]
    [SerializeField] private TMP_Text swordSkillDebugText;
    [SerializeField] private GameObject swordSkillDebugPanel;
    [SerializeField] private bool showSwordSkillDebugPanel = true;
    [SerializeField] private string swordSkillDebugTitle = "SWORD SKILL DEBUG";

    [Header("Real-Time Player Skill Debug UI - Bow")]
    [SerializeField] private TMP_Text bowSkillDebugText;
    [SerializeField] private GameObject bowSkillDebugPanel;
    [SerializeField] private bool showBowSkillDebugPanel = true;
    [SerializeField] private string bowSkillDebugTitle = "BOW SKILL DEBUG";

    private readonly int[] debugSwordSkillCounts = new int[4];
    private readonly int[] debugBowSkillCounts = new int[4];

    private int dashCount;
    private int riposteCount;
    private int bowConcussiveCount;

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
        UpdateSkillDebugUI();
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
        {
            CurrentDistanceState = PlayerDistanceState.Idle;
        }
        else
        {
            CurrentDistanceState = (currentDistance < lastDistance)
                ? PlayerDistanceState.Chase
                : PlayerDistanceState.Retreat;
        }

        lastPlayerPos = currentPlayerPos;
        lastDistance = currentDistance;
    }

    public void RecordAction(PlayerActionType actionType, WeaponType weaponType)
    {
        if (weaponType != WeaponType.None)
            lastUsedWeapon = weaponType;

        AddPlaystyleCount(actionType);
        AddWeaponUsage(weaponType);

        DebugHub.DDA(
            $"Action Recorded -> O={offensiveCount}, D={defensiveCount}, Weapon={lastUsedWeapon}"
        );
    }

    public void RecordDefenseDash(WeaponType weaponType = WeaponType.None)
    {
        if (weaponType != WeaponType.None)
            lastUsedWeapon = weaponType;

        defensiveCount++;
        dashCount++;
        AddWeaponUsage(weaponType);

        DebugHub.DDA(
            $"Defense Dash Recorded -> O={offensiveCount}, D={defensiveCount}, " +
            $"Dash={dashCount}, Riposte={riposteCount}, Weapon={lastUsedWeapon}"
        );
    }

    public void RecordSwordSkill(SwordSkillSlot slot, PlayerActionType actionType)
    {
        lastUsedWeapon = WeaponType.Sword;

        AddPlaystyleCount(actionType);
        swordUsageCount++;

        int idx = (int)slot;
        if (idx >= 0 && idx < swordSkillCounts.Length)
            swordSkillCounts[idx]++;

        if (slot == SwordSkillSlot.Riposte)
            riposteCount++;

        RecordSwordSkillForDebug(slot);

        DebugHub.DDA(
            $"Sword Skill Recorded -> {slot} | O={offensiveCount}, D={defensiveCount}, " +
            $"Dash={dashCount}, Riposte={riposteCount}, SwordUse={swordUsageCount}, " +
            $"SwordSkillCounts=[{swordSkillCounts[0]}, {swordSkillCounts[1]}, {swordSkillCounts[2]}, {swordSkillCounts[3]}]"
        );
    }

    public void RecordSwordSlashCombo() => RecordSwordSkill(SwordSkillSlot.SlashCombo, PlayerActionType.Offensive);
    public void RecordSwordWhirlwind() => RecordSwordSkill(SwordSkillSlot.Whirlwind, PlayerActionType.Offensive);
    public void RecordSwordChargedStrike() => RecordSwordSkill(SwordSkillSlot.ChargedStrike, PlayerActionType.Offensive);
    public void RecordSwordRiposte() => RecordSwordSkill(SwordSkillSlot.Riposte, PlayerActionType.Defensive);

    public void RecordBowSkill(BowSkillSlot slot)
    {
        lastUsedWeapon = WeaponType.Bow;

        AddPlaystyleCount(PlayerActionType.Offensive);
        bowUsageCount++;

        int idx = (int)slot;
        if (idx >= 0 && idx < bowSkillCounts.Length)
            bowSkillCounts[idx]++;

        RecordBowSkillForDebug(slot);

        DebugHub.DDA(
            $"Bow Skill Recorded -> {slot} | O={offensiveCount}, D={defensiveCount}, " +
            $"BowUse={bowUsageCount}, BowSkillCounts=[{bowSkillCounts[0]}, {bowSkillCounts[1]}, {bowSkillCounts[2]}, {bowSkillCounts[3]}]"
        );
    }

    public void RecordBowQuickShot() => RecordBowSkill(BowSkillSlot.QuickShot);
    public void RecordBowPiercingShot() => RecordBowSkill(BowSkillSlot.PiercingShot);
    public void RecordBowFullDraw() => RecordBowSkill(BowSkillSlot.FullDraw);

    public void RecordBowConcussiveShot()
    {
        lastUsedWeapon = WeaponType.Bow;

        AddPlaystyleCount(PlayerActionType.Defensive);
        bowUsageCount++;

        bowConcussiveCount++;
        bowSkillCounts[3]++;

        RecordBowSkillForDebug(BowSkillSlot.ConcussiveShot);

        DebugHub.DDA(
            $"Bow Concussive (DEFENSE) Recorded -> O={offensiveCount}, D={defensiveCount}, " +
            $"BowUse={bowUsageCount}, BowSkillCounts=[{bowSkillCounts[0]}, {bowSkillCounts[1]}, {bowSkillCounts[2]}, {bowSkillCounts[3]}], " +
            $"BowConcussiveTotal={bowConcussiveCount}"
        );
    }

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
            swordSkillCounts,
            bowSkillCounts,
            dashCount,
            riposteCount,
            bowConcussiveCount
        );

        DebugHub.DDA(
            $"Stage Final Data Sent -> O={offensiveCount}, D={defensiveCount}, " +
            $"WeaponUse[S={swordUsageCount}, B={bowUsageCount}, G={gauntletUsageCount}], " +
            $"SwordSkillCounts=[{swordSkillCounts[0]}, {swordSkillCounts[1]}, {swordSkillCounts[2]}, {swordSkillCounts[3]}], " +
            $"BowSkillCounts=[{bowSkillCounts[0]}, {bowSkillCounts[1]}, {bowSkillCounts[2]}, {bowSkillCounts[3]}], " +
            $"Defense=[Dash={dashCount}, Riposte={riposteCount}, Concussive={bowConcussiveCount}]"
        );

        ResetData();
    }

    private void RecordSwordSkillForDebug(SwordSkillSlot slot)
    {
        int idx = (int)slot;
        if (idx >= 0 && idx < debugSwordSkillCounts.Length)
            debugSwordSkillCounts[idx]++;

        UpdateSkillDebugUI();
    }

    private void RecordBowSkillForDebug(BowSkillSlot slot)
    {
        int idx = (int)slot;
        if (idx >= 0 && idx < debugBowSkillCounts.Length)
            debugBowSkillCounts[idx]++;

        UpdateSkillDebugUI();
    }

    public void ResetSkillDebugData()
    {
        for (int i = 0; i < debugSwordSkillCounts.Length; i++)
            debugSwordSkillCounts[i] = 0;

        for (int i = 0; i < debugBowSkillCounts.Length; i++)
            debugBowSkillCounts[i] = 0;

        UpdateSkillDebugUI();
        Debug.Log("[DataTracker] Real-time skill debug player direset untuk stage berikutnya.");
    }

    public float[] GetCurrentSwordSkillDebugWeightsCopy()
    {
        return BuildPercentArray(debugSwordSkillCounts);
    }

    public float[] GetCurrentBowSkillDebugWeightsCopy()
    {
        return BuildPercentArray(debugBowSkillCounts);
    }

    private void UpdateSkillDebugUI()
    {
        UpdateSwordSkillDebugUI();
        UpdateBowSkillDebugUI();
    }

    private void UpdateSwordSkillDebugUI()
    {
        if (swordSkillDebugPanel != null)
            swordSkillDebugPanel.SetActive(showSwordSkillDebugPanel);

        if (swordSkillDebugText == null)
            return;

        swordSkillDebugText.SetText(BuildSwordSkillDebugText());
    }

    private void UpdateBowSkillDebugUI()
    {
        if (bowSkillDebugPanel != null)
            bowSkillDebugPanel.SetActive(showBowSkillDebugPanel);

        if (bowSkillDebugText == null)
            return;

        bowSkillDebugText.SetText(BuildBowSkillDebugText());
    }

    private string BuildSwordSkillDebugText()
    {
        return
            $"{swordSkillDebugTitle}\n" +
            "SlashCombo | Whirlwind | ChargedStrike | Riposte\n" +
            $"{FormatPercentRow(debugSwordSkillCounts)}\n" +
            $"{FormatCountRow(debugSwordSkillCounts)}";
    }

    private string BuildBowSkillDebugText()
    {
        return
            $"{bowSkillDebugTitle}\n" +
            "QuickShot | PiercingShot | FullDraw | ConcussiveShot\n" +
            $"{FormatPercentRow(debugBowSkillCounts)}\n" +
            $"{FormatCountRow(debugBowSkillCounts)}";
    }

    private string FormatPercentRow(int[] counts)
    {
        float[] weights = BuildPercentArray(counts);
        return $"{weights[0]:F0}%, {weights[1]:F0}%, {weights[2]:F0}%, {weights[3]:F0}%";
    }

    private string FormatCountRow(int[] counts)
    {
        return $"{counts[0]}, {counts[1]}, {counts[2]}, {counts[3]}";
    }

    private float[] BuildPercentArray(int[] counts)
    {
        float[] weights = new float[4];

        int total = 0;
        for (int i = 0; i < counts.Length; i++)
            total += Mathf.Max(0, counts[i]);

        if (total <= 0)
            return weights;

        for (int i = 0; i < counts.Length; i++)
            weights[i] = (Mathf.Max(0, counts[i]) / (float)total) * 100f;

        return weights;
    }

    public void ResetData()
    {
        offensiveCount = 0;
        defensiveCount = 0;

        swordUsageCount = 0;
        bowUsageCount = 0;
        gauntletUsageCount = 0;

        dashCount = 0;
        riposteCount = 0;
        bowConcussiveCount = 0;

        for (int i = 0; i < swordSkillCounts.Length; i++)
            swordSkillCounts[i] = 0;

        for (int i = 0; i < bowSkillCounts.Length; i++)
            bowSkillCounts[i] = 0;

        lastUsedWeapon = WeaponType.None;
    }
}