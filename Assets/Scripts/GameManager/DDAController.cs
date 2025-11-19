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
    // Singleton Pattern
    public static DDAController Instance { get; private set; }

    // Status playstyle pemain saat ini
    public PlayerPlaystyle currentPlayerPlaystyle { get; private set; } = PlayerPlaystyle.Balanced;

    // Senjata Terakhir yang digunakan pemain
    public WeaponType currentPlayerWeapon { get; private set; } = WeaponType.None;

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

    // Fungsi untuk memperbarui playstyle pemain berdasarkan data dari DataTracker
    public void UpdatePlayerPlaystyle(int offensiveCount, int defensiveCount, WeaponType lastUsedWeapon)
    {
        // Analisis playstyle
        if (offensiveCount > defensiveCount)
        {
            currentPlayerPlaystyle = PlayerPlaystyle.OffensiveDominant;
        }
        else if (defensiveCount > offensiveCount)
        {
            currentPlayerPlaystyle = PlayerPlaystyle.DefensiveDominant;
        }
        else
        {
            currentPlayerPlaystyle = PlayerPlaystyle.Balanced;
        }

        // Simpan senjata terakhir yang digunakan pemain
        currentPlayerWeapon = lastUsedWeapon;
    }
}
