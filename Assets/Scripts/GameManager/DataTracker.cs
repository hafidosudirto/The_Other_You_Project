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
    SpreadArrow = 1,
    FullDraw = 2,
    FullDrawFullCharge = 3,
    ConcussiveShot = 4,

    // Alias kompatibilitas: Piercing sekarang dianggap FullDraw penuh.
    PiercingShot = FullDrawFullCharge
}

public class DataTracker : MonoBehaviour
{
    public static DataTracker Instance { get; private set; }

    private const int SwordSkillSlotTotal = 4;
    private const int BowSkillSlotTotal = 5;

    private int offensiveCount;
    private int defensiveCount;

    private int swordUsageCount;
    private int bowUsageCount;
    private int gauntletUsageCount;

    private readonly int[] swordSkillCounts = new int[SwordSkillSlotTotal];
    private readonly int[] bowSkillCounts = new int[BowSkillSlotTotal];

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

    private readonly int[] debugSwordSkillCounts = new int[SwordSkillSlotTotal];
    private readonly int[] debugBowSkillCounts = new int[BowSkillSlotTotal];

    private int dashCount;
    private int riposteCount;
    private int bowConcussiveCount;

    [Header("Runtime Player Weapon")]
    [SerializeField] private WeaponType activePlayerWeapon = WeaponType.None;
    private WeaponType lastUsedWeapon = WeaponType.None;

    [Header("Distance Tracking")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform enemyTransform;
    [SerializeField] private float distanceCheckInterval = 0.1f;
    [SerializeField] private float idleMovementThreshold = 0.03f;

    public PlayerDistanceState CurrentDistanceState { get; private set; }
    public WeaponType ActivePlayerWeapon => activePlayerWeapon;
    public WeaponType LastUsedWeapon => lastUsedWeapon;

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

    private void OnEnable()
    {
        PlayerPrefabSwitchManager.OnActiveWeaponChanged += SetActiveWeapon;
    }

    private void OnDisable()
    {
        PlayerPrefabSwitchManager.OnActiveWeaponChanged -= SetActiveWeapon;
    }

    private void Start()
    {
        RefreshActivePlayerReference();

        if (enemyTransform == null)
            Debug.LogWarning("[DataTracker] Enemy Transform belum di-assign.");

        ResetDistanceBaseline();

        lastCheckTime = Time.time;
        UpdateSkillDebugUI();
    }

    private void Update()
    {
        if (playerTransform == null || !playerTransform.gameObject.activeInHierarchy)
            RefreshActivePlayerReference();

        if (Time.time > lastCheckTime + distanceCheckInterval)
        {
            TrackPlayerMovement();
            lastCheckTime = Time.time;
        }
    }

    public void SetPlayerTransform(Transform newPlayerTransform)
    {
        playerTransform = newPlayerTransform;

        WeaponType resolvedWeapon = ResolveWeaponFromTransform(playerTransform);
        SetActiveWeapon(resolvedWeapon);

        ResetDistanceBaseline();

        DebugHub.DDA(
            $"[DataTracker] Player transform diperbarui -> " +
            $"{(playerTransform != null ? playerTransform.name : "NULL")} | Weapon={activePlayerWeapon}"
        );
    }

    public void SetEnemyTransform(Transform newEnemyTransform)
    {
        enemyTransform = newEnemyTransform;
        ResetDistanceBaseline();
    }

    public void SetActiveWeapon(WeaponType newWeapon)
    {
        if (newWeapon == WeaponType.None)
        {
            WeaponType resolvedFromTransform = ResolveWeaponFromTransform(playerTransform);

            if (resolvedFromTransform != WeaponType.None)
                newWeapon = resolvedFromTransform;
        }

        activePlayerWeapon = newWeapon;

        if (newWeapon != WeaponType.None)
            lastUsedWeapon = newWeapon;
    }

    private void RefreshActivePlayerReference()
    {
        Transform activePlayer = FindActivePlayerTransform();

        if (activePlayer != null)
            SetPlayerTransform(activePlayer);
    }

    private Transform FindActivePlayerTransform()
    {
        PlayerWeaponIdentity[] identities = FindObjectsOfType<PlayerWeaponIdentity>(true);

        foreach (PlayerWeaponIdentity identity in identities)
        {
            if (identity == null)
                continue;

            if (!identity.gameObject.activeInHierarchy)
                continue;

            if (identity.currentWeapon == WeaponType.Sword || identity.currentWeapon == WeaponType.Bow)
                return identity.transform;
        }

        foreach (PlayerWeaponIdentity identity in identities)
        {
            if (identity == null)
                continue;

            if (!identity.gameObject.activeInHierarchy)
                continue;

            if (identity.currentWeapon == WeaponType.None)
                return identity.transform;
        }

        try
        {
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");

            if (taggedPlayer != null && taggedPlayer.activeInHierarchy)
                return taggedPlayer.transform;
        }
        catch (UnityException)
        {
            Debug.LogWarning("[DataTracker] Tag Player belum dibuat.");
        }

        Player fallbackPlayer = FindObjectOfType<Player>();

        if (fallbackPlayer != null)
            return fallbackPlayer.transform;

        return null;
    }

    private WeaponType ResolveWeaponType(WeaponType explicitWeapon)
    {
        if (explicitWeapon != WeaponType.None)
            return explicitWeapon;

        if (activePlayerWeapon != WeaponType.None)
            return activePlayerWeapon;

        if (PlayerPrefabSwitchManager.CurrentWeapon != WeaponType.None)
            return PlayerPrefabSwitchManager.CurrentWeapon;

        WeaponType fromTransform = ResolveWeaponFromTransform(playerTransform);

        if (fromTransform != WeaponType.None)
            return fromTransform;

        return lastUsedWeapon;
    }

    private WeaponType ResolveWeaponFromTransform(Transform source)
    {
        if (source == null)
            return WeaponType.None;

        PlayerWeaponIdentity identity = source.GetComponent<PlayerWeaponIdentity>();

        if (identity == null)
            identity = source.GetComponentInChildren<PlayerWeaponIdentity>(true);

        if (identity == null)
            identity = source.GetComponentInParent<PlayerWeaponIdentity>();

        if (identity != null)
            return identity.currentWeapon;

        Player player = source.GetComponent<Player>();

        if (player == null)
            player = source.GetComponentInChildren<Player>(true);

        if (player == null)
            player = source.GetComponentInParent<Player>();

        if (player != null)
            return player.weaponType;

        return WeaponType.None;
    }

    private void ResetDistanceBaseline()
    {
        if (playerTransform == null)
            return;

        lastPlayerPos = playerTransform.position;

        if (enemyTransform != null)
            lastDistance = Vector2.Distance(playerTransform.position, enemyTransform.position);
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
            CurrentDistanceState = currentDistance < lastDistance
                ? PlayerDistanceState.Chase
                : PlayerDistanceState.Retreat;
        }

        lastPlayerPos = currentPlayerPos;
        lastDistance = currentDistance;
    }

    public void RecordAction(PlayerActionType actionType, WeaponType weaponType)
    {
        weaponType = ResolveWeaponType(weaponType);

        if (weaponType != WeaponType.None)
            lastUsedWeapon = weaponType;

        AddPlaystyleCount(actionType);
        AddWeaponUsage(weaponType);

        DebugHub.DDA(
            $"Action Recorded -> O={offensiveCount}, D={defensiveCount}, " +
            $"Weapon={lastUsedWeapon}, ActiveWeapon={activePlayerWeapon}"
        );
    }

    public void RecordDefenseDash(WeaponType weaponType = WeaponType.None)
    {
        weaponType = ResolveWeaponType(weaponType);

        if (weaponType != WeaponType.None)
            lastUsedWeapon = weaponType;

        defensiveCount++;
        dashCount++;

        AddWeaponUsage(weaponType);

        // Dash = aksi defensif; jika menyusul stimulus musuh, dihitung sebagai reaksi.
        if (TelemetryLogger.Instance != null)
            TelemetryLogger.Instance.RecordReactionResponse();

        DebugHub.DDA(
            $"Defense Dash Recorded -> O={offensiveCount}, D={defensiveCount}, " +
            $"Dash={dashCount}, Riposte={riposteCount}, Weapon={lastUsedWeapon}"
        );
    }

    public void RecordSwordSkill(SwordSkillSlot slot, PlayerActionType actionType)
    {
        SetActiveWeapon(WeaponType.Sword);
        lastUsedWeapon = WeaponType.Sword;

        AddPlaystyleCount(actionType);
        swordUsageCount++;

        int idx = (int)slot;

        if (idx >= 0 && idx < swordSkillCounts.Length)
            swordSkillCounts[idx]++;

        if (slot == SwordSkillSlot.Riposte)
            riposteCount++;

        if (TelemetryLogger.Instance != null)
            TelemetryLogger.Instance.RecordSkillCast(WeaponType.Sword, slot.ToString(), actionType);

        RecordSwordSkillForDebug(slot);

        DebugHub.DDA(
            $"Sword Skill Recorded -> {slot} | O={offensiveCount}, D={defensiveCount}, " +
            $"Dash={dashCount}, Riposte={riposteCount}, SwordUse={swordUsageCount}, " +
            $"SwordSkillCounts=[{swordSkillCounts[0]}, {swordSkillCounts[1]}, {swordSkillCounts[2]}, {swordSkillCounts[3]}]"
        );
    }

    public void RecordSwordSlashCombo()
    {
        RecordSwordSkill(SwordSkillSlot.SlashCombo, PlayerActionType.Offensive);
    }

    public void RecordSwordWhirlwind()
    {
        RecordSwordSkill(SwordSkillSlot.Whirlwind, PlayerActionType.Offensive);
    }

    public void RecordSwordChargedStrike()
    {
        RecordSwordSkill(SwordSkillSlot.ChargedStrike, PlayerActionType.Offensive);
    }

    public void RecordSwordRiposte()
    {
        RecordSwordSkill(SwordSkillSlot.Riposte, PlayerActionType.Defensive);
    }

    public void RecordBowSkill(BowSkillSlot slot)
    {
        SetActiveWeapon(WeaponType.Bow);
        lastUsedWeapon = WeaponType.Bow;

        if (slot == BowSkillSlot.ConcussiveShot)
            AddPlaystyleCount(PlayerActionType.Defensive);
        else
            AddPlaystyleCount(PlayerActionType.Offensive);

        bowUsageCount++;

        int idx = (int)slot;

        if (idx >= 0 && idx < bowSkillCounts.Length)
            bowSkillCounts[idx]++;

        if (slot == BowSkillSlot.ConcussiveShot)
            bowConcussiveCount++;

        if (TelemetryLogger.Instance != null)
        {
            PlayerActionType bowAction = slot == BowSkillSlot.ConcussiveShot
                ? PlayerActionType.Defensive
                : PlayerActionType.Offensive;
            TelemetryLogger.Instance.RecordSkillCast(WeaponType.Bow, slot.ToString(), bowAction);
        }

        RecordBowSkillForDebug(slot);

        DebugHub.DDA(
            $"Bow Skill Recorded -> {slot} | O={offensiveCount}, D={defensiveCount}, " +
            $"BowUse={bowUsageCount}, " +
            $"BowSkillCounts=[{bowSkillCounts[0]}, {bowSkillCounts[1]}, {bowSkillCounts[2]}, {bowSkillCounts[3]}, {bowSkillCounts[4]}], " +
            $"BowConcussiveTotal={bowConcussiveCount}"
        );
    }

    public void RecordBowQuickShot()
    {
        RecordBowSkill(BowSkillSlot.QuickShot);
    }

    public void RecordBowSpreadArrow()
    {
        RecordBowSkill(BowSkillSlot.SpreadArrow);
    }

    public void RecordBowFullDraw()
    {
        RecordBowSkill(BowSkillSlot.FullDraw);
    }

    public void RecordBowFullDrawNormal()
    {
        RecordBowSkill(BowSkillSlot.FullDraw);
    }

    public void RecordBowFullDrawFullCharge()
    {
        RecordBowSkill(BowSkillSlot.FullDrawFullCharge);
    }

    public void RecordBowFullDrawPiercing()
    {
        RecordBowSkill(BowSkillSlot.FullDrawFullCharge);
    }

    public void RecordBowPiercingShot()
    {
        RecordBowSkill(BowSkillSlot.FullDrawFullCharge);
    }

    public void RecordBowConcussiveShot()
    {
        RecordBowSkill(BowSkillSlot.ConcussiveShot);
    }

    // Opsional: dapat dipanggil oleh script skill baru jika nama method Record... belum disambungkan.
    public void RecordSkillByName(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return;

        string key = skillName.ToLowerInvariant()
            .Replace(" ", "")
            .Replace("_", "")
            .Replace("-", "");

        switch (key)
        {
            case "slashcombo":
            case "swordslashcombo":
                RecordSwordSlashCombo();
                break;

            case "whirlwind":
            case "swordwhirlwind":
                RecordSwordWhirlwind();
                break;

            case "chargedstrike":
            case "swordchargedstrike":
                RecordSwordChargedStrike();
                break;

            case "riposte":
            case "swordriposte":
                RecordSwordRiposte();
                break;

            case "quickshot":
            case "bowquickshot":
                RecordBowQuickShot();
                break;

            case "spreadarrow":
            case "bowspreadarrow":
                RecordBowSpreadArrow();
                break;

            case "fulldraw":
            case "bowfulldraw":
            case "fulldrawnormal":
                RecordBowFullDraw();
                break;

            case "fulldrawfullcharge":
            case "fullcharge":
            case "piercingshot":
            case "bowpiercingshot":
                RecordBowFullDrawFullCharge();
                break;

            case "concussiveshot":
            case "bowconcussiveshot":
                RecordBowConcussiveShot();
                break;

            default:
                Debug.LogWarning("[DataTracker] Nama skill belum dikenali: " + skillName);
                break;
        }
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
            $"BowSkillCounts=[{bowSkillCounts[0]}, {bowSkillCounts[1]}, {bowSkillCounts[2]}, {bowSkillCounts[3]}, {bowSkillCounts[4]}], " +
            $"Defense=[Dash={dashCount}, Riposte={riposteCount}, Concussive={bowConcussiveCount}]"
        );

        // Simpan snapshot per-stage SEBELUM di-reset, untuk baris telemetry stage_summary.
        if (TelemetryLogger.Instance != null)
            TelemetryLogger.Instance.CaptureStageProfileSnapshot(
                offensiveCount, defensiveCount, dashCount, riposteCount,
                bowConcussiveCount, swordUsageCount, bowUsageCount);

        ResetData();
    }

    public void ResetData()
    {
        offensiveCount = 0;
        defensiveCount = 0;

        swordUsageCount = 0;
        bowUsageCount = 0;
        gauntletUsageCount = 0;

        for (int i = 0; i < swordSkillCounts.Length; i++)
            swordSkillCounts[i] = 0;

        for (int i = 0; i < bowSkillCounts.Length; i++)
            bowSkillCounts[i] = 0;

        dashCount = 0;
        riposteCount = 0;
        bowConcussiveCount = 0;

        DebugHub.DDA("[DataTracker] Runtime counter direset. Profil DDA tidak ikut direset.");
    }

    public void ResetTracker()
    {
        ResetData();
    }

    public void ResetAll()
    {
        ResetData();
    }

    public void ClearData()
    {
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
            "QuickShot | SpreadArrow | FullDraw | FullCharge | Concussive\n" +
            $"{FormatPercentRow(debugBowSkillCounts)}\n" +
            $"{FormatCountRow(debugBowSkillCounts)}";
    }

    private string FormatPercentRow(int[] counts)
    {
        float[] weights = BuildPercentArray(counts);
        string result = "";

        for (int i = 0; i < weights.Length; i++)
        {
            result += $"{weights[i]:F0}%";

            if (i < weights.Length - 1)
                result += ", ";
        }

        return result;
    }

    private string FormatCountRow(int[] counts)
    {
        string result = "Count: ";

        for (int i = 0; i < counts.Length; i++)
        {
            result += counts[i].ToString();

            if (i < counts.Length - 1)
                result += ", ";
        }

        return result;
    }

    private float[] BuildPercentArray(int[] counts)
    {
        float[] result = new float[counts.Length];

        int total = 0;

        for (int i = 0; i < counts.Length; i++)
            total += counts[i];

        if (total <= 0)
            return result;

        for (int i = 0; i < counts.Length; i++)
            result[i] = counts[i] * 100f / total;

        return result;
    }
}