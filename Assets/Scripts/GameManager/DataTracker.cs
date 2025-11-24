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

    [Header("Action Tracking")]
    [SerializeField] private int actionBatchSize = 3;

    private int OffensiveCount;
    private int DefensiveCount;
    private WeaponType lastUsedWeapon = WeaponType.None;

    [Header("Distance Tracking")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform enemyTransform;

    [SerializeField] private float distanceCheckInterval = 0.1f;     // lebih responsif
    [SerializeField] private float idleMovementThreshold = 0.03f;    // threshold idle kecil

    public PlayerDistanceState CurrentDistanceState { get; private set; }

    private float lastCheckTime;
    private Vector3 lastPlayerPos;
    private float lastDistance;

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

        // ---- 1. PLAYER IDLE ----
        if (playerMovement < idleMovementThreshold)
        {
            CurrentDistanceState = PlayerDistanceState.Idle;
        }
        else
        {
            // ---- 2. PLAYER MOVING: tentukan mendekat / menjauh ----
            if (currentDistance < lastDistance)
            {
                CurrentDistanceState = PlayerDistanceState.Chase; // mendekat
            }
            else
            {
                CurrentDistanceState = PlayerDistanceState.Retreat; // menjauh
            }
        }

        // Debugging
        Debug.Log($"<color=yellow>[DataTracker] State={CurrentDistanceState}, Player={playerMovement}</color>");

        // Save values for next frame
        lastPlayerPos = currentPlayerPos;
        lastDistance = currentDistance;
    }

    // =====================================================================
    // PLAYER ACTION TRACKING
    // =====================================================================

    public void RecordAction(PlayerActionType actionType, WeaponType weaponType)
    {
        if (weaponType != WeaponType.None)
            lastUsedWeapon = weaponType;

        if (actionType == PlayerActionType.Offensive)
            OffensiveCount++;
        else
            DefensiveCount++;

        CheckForAnalysis();
    }

    private void CheckForAnalysis()
    {
        int totalActions = OffensiveCount + DefensiveCount;

        if (totalActions >= actionBatchSize)
        {
            SendDataToDDA();

            OffensiveCount = 0;
            DefensiveCount = 0;
        }
    }

    private void SendDataToDDA()
    {
        if (DDAController.Instance != null)
        {
            DDAController.Instance.UpdatePlayerPlaystyle(
                OffensiveCount,
                DefensiveCount,
                lastUsedWeapon
            );
        }
        else
        {
            Debug.LogWarning("DDAController tidak ditemukan!");
        }
    }
}

