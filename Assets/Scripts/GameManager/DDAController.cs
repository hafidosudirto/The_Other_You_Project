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

    // ===============================
    // PROFILE VERSION — untuk cache invalidation di EnemyAI
    // ===============================
    public int ProfileVersion { get; private set; } = 0;

    // ===============================
    // SWORD SKILL WEIGHTS
    // Urutan indeks harus sinkron dengan DataTracker:
    // [0] SlashCombo
    // [1] Whirlwind
    // [2] ChargedStrike
    // [3] Riposte
    // ===============================
    private float[] swordSkillWeights = new float[] { 25f, 25f, 25f, 25f };

    // ===============================
    // DEFENSE PROFILE UNTUK STAGE BERIKUTNYA
    // ===============================
    private float defenseDashWeight = 0f;
    private float defenseRiposteWeight = 0f;
    private bool hasDefenseProfile = false;

    public bool HasDefenseProfile => hasDefenseProfile;

    // ===============================
    // KUMULATIF WEAPON ANALYSIS
    // ===============================
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

    // =========================================================
    // FINAL PROFILE UPDATE
    // =========================================================
    public void UpdatePlayerProfile(
        int offensive,
        int defensive,
        int swordUsage,
        int bowUsage,
        int gauntletUsage,
        int[] swordSkillCounts,
        int dashCount,
        int riposteCount)
    {
        // 1. Analisis playstyle
        if (offensive > defensive)
            currentPlayerPlaystyle = PlayerPlaystyle.OffensiveDominant;
        else if (defensive > offensive)
            currentPlayerPlaystyle = PlayerPlaystyle.DefensiveDominant;
        else
            currentPlayerPlaystyle = PlayerPlaystyle.Balanced;

        // 2. Update weapon counter kumulatif
        swordCount += swordUsage;
        bowCount += bowUsage;
        gauntletCount += gauntletUsage;

        // 3. Analisis senjata dominan
        currentPlayerDominantWeapon = AnalyzeDominantWeapon();

        // 4. Normalisasi sword skill weights
        NormalizeSwordSkillWeights(swordSkillCounts);

        // 5. Normalisasi defense weights
        NormalizeDefenseWeights(dashCount, riposteCount);

        // 6. Increment version
        ProfileVersion++;

        Debug.Log(
            $"[DDA] Playstyle={currentPlayerPlaystyle}, Weapon={currentPlayerDominantWeapon}, " +
            $"Version={ProfileVersion}, SwordWeights=[{swordSkillWeights[0]:F1}, {swordSkillWeights[1]:F1}, {swordSkillWeights[2]:F1}, {swordSkillWeights[3]:F1}], " +
            $"DefenseWeights=[Dash={defenseDashWeight:F1}, Riposte={defenseRiposteWeight:F1}], HasDefenseProfile={hasDefenseProfile}"
        );
    }

    // =========================================================
    // API UNTUK ENEMY ADAPTIVE PROFILE
    // =========================================================
    public float[] GetCurrentSwordSkillWeightsCopy()
    {
        return (float[])swordSkillWeights.Clone();
    }

    public float GetCurrentDefenseDashWeight()
    {
        return defenseDashWeight;
    }

    public float GetCurrentDefenseRiposteWeight()
    {
        return defenseRiposteWeight;
    }

    // =========================================================
    // NORMALIZATION
    // =========================================================
    private void NormalizeSwordSkillWeights(int[] counts)
    {
        if (counts == null || counts.Length != 4)
        {
            swordSkillWeights = new float[] { 25f, 25f, 25f, 25f };
            return;
        }

        int total = 0;
        for (int i = 0; i < 4; i++)
            total += Mathf.Max(0, counts[i]);

        if (total == 0)
        {
            swordSkillWeights = new float[] { 25f, 25f, 25f, 25f };
            return;
        }

        for (int i = 0; i < 4; i++)
            swordSkillWeights[i] = (Mathf.Max(0, counts[i]) / (float)total) * 100f;
    }

    private void NormalizeDefenseWeights(int dashCount, int riposteCount)
    {
        dashCount = Mathf.Max(0, dashCount);
        riposteCount = Mathf.Max(0, riposteCount);

        int total = dashCount + riposteCount;

        // Tidak ada histori defense valid -> jangan fallback 50/50.
        if (total <= 0)
        {
            hasDefenseProfile = false;
            defenseDashWeight = 0f;
            defenseRiposteWeight = 0f;
            return;
        }

        hasDefenseProfile = true;
        defenseDashWeight = (dashCount / (float)total) * 100f;
        defenseRiposteWeight = (riposteCount / (float)total) * 100f;
    }

    // =========================================================
    // WEAPON DOMINANCE
    // =========================================================
    private WeaponType AnalyzeDominantWeapon()
    {
        int max = Mathf.Max(swordCount, Mathf.Max(bowCount, gauntletCount));

        if (max == 0)
            return WeaponType.None;

        int tieCount = 0;
        if (swordCount == max) tieCount++;
        if (bowCount == max) tieCount++;
        if (gauntletCount == max) tieCount++;

        if (tieCount > 1)
            return WeaponType.None;

        if (swordCount == max) return WeaponType.Sword;
        if (bowCount == max) return WeaponType.Bow;
        return WeaponType.Gauntlet;
    }

    public void ResetWeaponAnalysis()
    {
        swordCount = 0;
        bowCount = 0;
        gauntletCount = 0;
    }
}