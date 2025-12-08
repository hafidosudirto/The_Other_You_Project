using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum PlayerPlaystyle
{
    Balanced,
    OffensiveDominant,
    DefensiveDominant,
}

public class DDAController : MonoBehaviour
{
    public static DDAController Instance { get; private set; }

    // ===============================
    // FINAL RESULT PER STAGE
    // ===============================
    public PlayerPlaystyle currentPlayerPlaystyle { get; private set; } = PlayerPlaystyle.Balanced;
    public WeaponType currentPlayerDominantWeapon { get; private set; } = WeaponType.None;

    // Counter untuk analisis weapon
    private int swordCount;
    private int bowCount;
    private int gauntletCount;

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

    // DIPANGGIL OLEH DataTracker per stage
    public void UpdatePlayerPlaystyle(int offensive, int defensive, WeaponType lastWeaponUsed)
    {
        // ===============================
        // 1. ANALISIS PLAYSTYLE
        // ===============================
        if (offensive > defensive)
            currentPlayerPlaystyle = PlayerPlaystyle.OffensiveDominant;
        else if (defensive > offensive)
            currentPlayerPlaystyle = PlayerPlaystyle.DefensiveDominant;
        else
            currentPlayerPlaystyle = PlayerPlaystyle.Balanced;

        // ===============================
        // 2. UPDATE COUNTER WEAPON
        // ===============================
        if (lastWeaponUsed != WeaponType.None)
        {
            switch (lastWeaponUsed)
            {
                case WeaponType.Sword:
                    swordCount++;
                    break;

                case WeaponType.Bow:
                    bowCount++;
                    break;

                case WeaponType.Gauntlet:
                    gauntletCount++;
                    break;
            }
        }

        // ===============================
        // 3. ANALISIS SENJATA DOMINAN
        // ===============================
        currentPlayerDominantWeapon = AnalyzeDominantWeapon();

        Debug.Log($"[DDA] Playstyle: {currentPlayerPlaystyle}, Weapon Dominant: {currentPlayerDominantWeapon}");
    }

    // ===============================
    // SUB-FUNGSI: Tentukan Weapon Dominant
    // ===============================
    private WeaponType AnalyzeDominantWeapon()
    {
        // Ambil angka paling besar
        int max = Mathf.Max(swordCount, Mathf.Max(bowCount, gauntletCount));

        // Jika semua nol → Tidak ada dominan
        if (max == 0)
            return WeaponType.None;

        // Jika lebih dari satu sama besarnya → None (tidak dominan)
        int tieCount = 0;
        if (swordCount == max) tieCount++;
        if (bowCount == max) tieCount++;
        if (gauntletCount == max) tieCount++;

        if (tieCount > 1)
            return WeaponType.None;

        // Kembalikan weapon yang paling sering
        if (swordCount == max) return WeaponType.Sword;
        if (bowCount == max) return WeaponType.Bow;
        return WeaponType.Gauntlet;
    }

    // ===============================
    // Reset saat stage selesai (opsional)
    // ===============================
    public void ResetWeaponAnalysis()
    {
        swordCount = 0;
        bowCount = 0;
        gauntletCount = 0;
    }
}

