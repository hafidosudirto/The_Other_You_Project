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

public class DataTracker : MonoBehaviour
{
    public static DataTracker Instance { get; private set; }

    [SerializeField] private int actionBatchSize = 3;

    private int OffensiveCount;
    private int DefensiveCount;
    private WeaponType lastUsedWeapon = WeaponType.None;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    // ------------------ RECORD ACTION ------------------
    public void RecordAction(PlayerActionType actionType, WeaponType weaponType)
    {
        if (weaponType != WeaponType.None)
            lastUsedWeapon = weaponType;

        if (actionType == PlayerActionType.Offensive)
            OffensiveCount++;
        else
            DefensiveCount++;

        DebugHub.DDA($"Action: {actionType}, Weapon: {weaponType} → O={OffensiveCount}, D={DefensiveCount}");

        CheckForAnalysis();
    }

    // ------------------ CHECK ANALYTICS ------------------
    private void CheckForAnalysis()
    {
        int totalActions = OffensiveCount + DefensiveCount;

        if (totalActions >= actionBatchSize)
        {
            SendDataToDDA();

            // Reset
            OffensiveCount = 0;
            DefensiveCount = 0;
        }
    }

    // ------------------ SEND TO DDA ------------------
    private void SendDataToDDA()
    {
        if (DDAController.Instance != null)
        {
            DebugHub.DDA($"Send Batch → O={OffensiveCount}, D={DefensiveCount}, W={lastUsedWeapon}");
            DDAController.Instance.UpdatePlayerPlaystyle(
                OffensiveCount,
                DefensiveCount,
                lastUsedWeapon
            );
        }
        else
        {
            DebugHub.Warning("DDAController tidak ditemukan!");
        }
    }
}
