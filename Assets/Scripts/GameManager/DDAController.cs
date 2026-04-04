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
    // [BARU] PROFILE VERSION — untuk cache invalidation di EnemyAI
    // ===============================
    public int ProfileVersion { get; private set; } = 0;

    // ===============================
    // [BARU] SWORD SKILL WEIGHTS — hasil normalisasi dari usage count
    // ===============================
    private float[] swordSkillWeights = new float[] { 25f, 25f, 25f, 25f };

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

    // ===============================
    // [BARU] UpdatePlayerProfile — dipanggil DataTracker.FinalizeStageData()
    // Menggantikan UpdatePlayerPlaystyle() yang signature-nya berbeda
    // ===============================
    public void UpdatePlayerProfile(
        int offensive,
        int defensive,
        int swordUsage,
        int bowUsage,
        int gauntletUsage,
        int[] swordSkillCounts)
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

        // 5. Increment version agar EnemyAI tahu perlu refresh
        ProfileVersion++;

        Debug.Log(
            $"[DDA] Playstyle={currentPlayerPlaystyle}, Weapon={currentPlayerDominantWeapon}, " +
            $"Version={ProfileVersion}, SwordWeights=[{swordSkillWeights[0]:F1}, " +
            $"{swordSkillWeights[1]:F1}, {swordSkillWeights[2]:F1}, {swordSkillWeights[3]:F1}]"
        );
    }

    // ===============================
    // [BARU] GetCurrentSwordSkillWeightsCopy — dipanggil EnemyAdaptiveProfile
    // ===============================
    public float[] GetCurrentSwordSkillWeightsCopy()
    {
        return (float[])swordSkillWeights.Clone();
    }

    // ===============================
    // SUB: Normalisasi sword skill jadi total = 100
    // ===============================
    private void NormalizeSwordSkillWeights(int[] counts)
    {
        if (counts == null || counts.Length != 4)
        {
            swordSkillWeights = new float[] { 25f, 25f, 25f, 25f };
            return;
        }

        int total = 0;
        for (int i = 0; i < 4; i++) total += counts[i];

        if (total == 0)
        {
            swordSkillWeights = new float[] { 25f, 25f, 25f, 25f };
            return;
        }

        for (int i = 0; i < 4; i++)
            swordSkillWeights[i] = (counts[i] / (float)total) * 100f;
    }

    // ===============================
    // SUB: Tentukan Weapon Dominant
    // ===============================
    private WeaponType AnalyzeDominantWeapon()
    {
        int max = Mathf.Max(swordCount, Mathf.Max(bowCount, gauntletCount));

        if (max == 0) return WeaponType.None;

        int tieCount = 0;
        if (swordCount == max) tieCount++;
        if (bowCount == max) tieCount++;
        if (gauntletCount == max) tieCount++;

        if (tieCount > 1) return WeaponType.None;

        if (swordCount == max) return WeaponType.Sword;
        if (bowCount == max) return WeaponType.Bow;
        return WeaponType.Gauntlet;
    }

    // ===============================
    // Reset weapon analysis (opsional, per-stage)
    // ===============================
    public void ResetWeaponAnalysis()
    {
        swordCount = 0;
        bowCount = 0;
        gauntletCount = 0;
    }
}