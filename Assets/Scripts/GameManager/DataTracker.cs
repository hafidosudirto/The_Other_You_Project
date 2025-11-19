using System.Collections;
using System.Collections.Generic;
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
    // Singelton Pattern
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

    // fungsi utama yang akan dipanggil oleh skrip skill pemain
    public void RecordAction (PlayerActionType actionType, WeaponType weaponType)
    {
        // Simpan senjata terakhir yang digunakan
        if (weaponType != WeaponType.None)
        {
            lastUsedWeapon = weaponType;
        }

        // Hitung aksi berdasarkan tipe
        if (actionType == PlayerActionType.Offensive)
        {
            OffensiveCount++;
        }
        else if (actionType == PlayerActionType.Defensive)
        {
            DefensiveCount++;
        }

        Debug.Log($"[DataTracker] Aksi dicatat: {actionType} (Senjata: {weaponType}). Total O: {OffensiveCount} / D: {DefensiveCount}");
        CheckForAnalysis();
    }

    // Periksa apakah sudah waktunya untuk analisis
    private void CheckForAnalysis()
    {
        int totalActions = OffensiveCount + DefensiveCount;
        if (totalActions >= actionBatchSize)
        {
            SendDataToDDA();
            // Reset hitungan setelah analisis
            OffensiveCount = 0;
            DefensiveCount = 0;
        }
    }

    // Kirim data ke DDA (Dynamic Difficulty Adjustment)
    private void SendDataToDDA()
    {
        if (DDAController.Instance != null)
        {
            Debug.Log($"[DataTracker] Mengirim batch data ke DDA... (O: {OffensiveCount}, D: {DefensiveCount}, W: {lastUsedWeapon})");
            DDAController.Instance.UpdatePlayerPlaystyle(OffensiveCount, DefensiveCount, lastUsedWeapon);
        }
        else
        {
            Debug.LogWarning("DDAController tidak ditemukan!");
        }
    }
}
