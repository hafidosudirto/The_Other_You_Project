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

public class DataTracker : MonoBehaviour
{
    public static DataTracker Instance { get; private set; }

    // ===============================
    // ACTION + WEAPON TRACKING
    // ===============================
    private int OffensiveCount;
    private int DefensiveCount;

    // Senjata terakhir yang dipakai player
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

    // ================================================================
    // MOVEMENT / DISTANCE STATE TRACKER
    // ================================================================
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

    // ================================================================
    // ACTION TRACKING (NO BATCH)
    // ================================================================
    public void RecordAction(PlayerActionType actionType, WeaponType weaponType)
    {
        // simpan weapon yang dipakai pemain
        if (weaponType != WeaponType.None)
            lastUsedWeapon = weaponType;

        // catat playstyle
        if (actionType == PlayerActionType.Offensive)
            OffensiveCount++;
        else
            DefensiveCount++;

        DebugHub.DDA($"Action Recorded → O={OffensiveCount}, D={DefensiveCount}, Weapon={lastUsedWeapon}");
    }

    // ================================================================
    // STAGE FINALIZATION (Dipanggil ketika enemy mati)
    // ================================================================
    public void FinalizeStageData()
    {
        if (DDAController.Instance == null)
        {
            Debug.LogWarning("[DataTracker] DDAController missing!");
            return;
        }

        DDAController.Instance.UpdatePlayerPlaystyle(
            OffensiveCount,
            DefensiveCount,
            lastUsedWeapon
        );

        DebugHub.DDA($"Stage Final Data Sent → Offensive={OffensiveCount}, Defensive={DefensiveCount}, Weapon={lastUsedWeapon}");

        ResetStageData();
    }

    private void ResetStageData()
    {
        OffensiveCount = 0;
        DefensiveCount = 0;
        lastUsedWeapon = WeaponType.None;
    }
}