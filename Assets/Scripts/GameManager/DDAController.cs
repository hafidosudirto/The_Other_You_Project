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

    // =========================================================
    // FINAL RESULT PER STAGE
    // =========================================================
    public PlayerPlaystyle currentPlayerPlaystyle { get; private set; } = PlayerPlaystyle.Balanced;
    public WeaponType currentPlayerDominantWeapon { get; private set; } = WeaponType.None;

    // =========================================================
    // PROFILE VERSION
    // =========================================================
    public int ProfileVersion { get; private set; } = 0;

    // =========================================================
    // SWORD SKILL WEIGHTS
    // [0] SlashCombo
    // [1] Whirlwind
    // [2] ChargedStrike
    // [3] Riposte
    // =========================================================
    private float[] swordSkillWeights = new float[] { 25f, 25f, 25f, 25f };

    // =========================================================
    // BOW SKILL WEIGHTS (UPDATED: 4 SLOTS)
    // [0] QuickShot
    // [1] PiercingShot
    // [2] FullDraw
    // [3] ConcussiveShot (Reaktif / Defense)
    // =========================================================
    private float[] bowSkillWeights = new float[] { 25f, 25f, 25f, 25f };
    private bool hasBowSkillProfile = false;

    public bool HasBowSkillProfile => hasBowSkillProfile;

    // =========================================================
    // DEFENSE PROFILE
    // (Hanya untuk Dash & Riposte. Concussive sekarang pindah ke Bow)
    // =========================================================
    private float defenseDashWeight = 0f;
    private float defenseRiposteWeight = 0f;
    private bool hasDefenseProfile = false;

    public bool HasDefenseProfile => hasDefenseProfile;

    // =========================================================
    // KUMULATIF WEAPON ANALYSIS
    // =========================================================
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
    // Menerima 10 Parameter dari DataTracker
    // =========================================================
    public void UpdatePlayerProfile(
        int offensive,
        int defensive,
        int swordUsage,
        int bowUsage,
        int gauntletUsage,
        int[] swordSkillCounts,
        int[] bowSkillCounts,
        int dashCount,
        int riposteCount,
        int concussiveCount) // Parameter tetap diterima agar DataTracker tidak error
    {
        // 1. Analisis playstyle
        if (offensive > defensive)
            currentPlayerPlaystyle = PlayerPlaystyle.OffensiveDominant;
        else if (defensive > offensive)
            currentPlayerPlaystyle = PlayerPlaystyle.DefensiveDominant;
        else
            currentPlayerPlaystyle = PlayerPlaystyle.Balanced;

        // 2. Update weapon counter kumulatif
        swordCount += Mathf.Max(0, swordUsage);
        bowCount += Mathf.Max(0, bowUsage);
        gauntletCount += Mathf.Max(0, gauntletUsage);

        // 3. Analisis senjata dominan
        currentPlayerDominantWeapon = AnalyzeDominantWeapon();

        // 4. Normalisasi sword (4 slot)
        NormalizeSwordSkillWeights(swordSkillCounts);

        // 5. Normalisasi bow (4 slot: 3 Attack + 1 Concussive)
        NormalizeBowSkillWeights(bowSkillCounts);

        // 6. Normalisasi defense (Hanya Dash & Riposte)
        NormalizeDefenseWeights(dashCount, riposteCount);

        // 7. Versioning
        ProfileVersion++;

        Debug.Log(
            $"[DDA] Playstyle={currentPlayerPlaystyle}, " +
            $"Weapon={currentPlayerDominantWeapon}, " +
            $"Version={ProfileVersion}, " +
            $"SwordWeights=[{swordSkillWeights[0]:F1}, {swordSkillWeights[1]:F1}, {swordSkillWeights[2]:F1}, {swordSkillWeights[3]:F1}], " +
            $"BowWeights=[{bowSkillWeights[0]:F1}, {bowSkillWeights[1]:F1}, {bowSkillWeights[2]:F1}, {bowSkillWeights[3]:F1}], " +
            $"HasBowProfile={hasBowSkillProfile}, " +
            $"DefenseWeights=[Dash={defenseDashWeight:F1}, Riposte={defenseRiposteWeight:F1}], " +
            $"HasDefenseProfile={hasDefenseProfile}"
        );
    }

    // =========================================================
    // API UNTUK ENEMY AI / NODEMANAGER
    // =========================================================
    public float[] GetCurrentSwordSkillWeightsCopy()
    {
        return (float[])swordSkillWeights.Clone();
    }

    public float[] GetCurrentBowSkillWeightsCopy()
    {
        return (float[])bowSkillWeights.Clone();
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
    // NORMALIZATION MEMORY FIX
    // =========================================================
    private void NormalizeSwordSkillWeights(int[] counts)
    {
        if (counts == null || counts.Length != 4) return;

        int total = 0;
        for (int i = 0; i < counts.Length; i++)
            total += Mathf.Max(0, counts[i]);

        // Jika tidak ada data, simpan memori dari stage sebelumnya
        if (total <= 0) return;

        for (int i = 0; i < counts.Length; i++)
            swordSkillWeights[i] = (Mathf.Max(0, counts[i]) / (float)total) * 100f;
    }

    private void NormalizeBowSkillWeights(int[] counts)
    {
        // Cek struktur array baru (4 slot)
        if (counts == null || counts.Length != 4) return;

        int total = 0;
        for (int i = 0; i < counts.Length; i++)
            total += Mathf.Max(0, counts[i]);

        // Jika tidak ada data panah, simpan memori
        if (total <= 0) return;

        hasBowSkillProfile = true;

        for (int i = 0; i < counts.Length; i++)
            bowSkillWeights[i] = (Mathf.Max(0, counts[i]) / (float)total) * 100f;
    }

    private void NormalizeDefenseWeights(int dashCount, int riposteCount)
    {
        dashCount = Mathf.Max(0, dashCount);
        riposteCount = Mathf.Max(0, riposteCount);

        int total = dashCount + riposteCount;

        if (total <= 0) return;

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

    public void ResetWeaponAnalysis()
    {
        swordCount = 0;
        bowCount = 0;
        gauntletCount = 0;
    }
}