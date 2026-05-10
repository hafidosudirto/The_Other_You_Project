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

    private const int SwordSkillSlotTotal = 4;
    private const int BowSkillSlotTotal = 5;

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
    // BOW SKILL WEIGHTS (UPDATED: 5 SLOTS)
    // [0] QuickShot
    // [1] SpreadArrow
    // [2] FullDraw
    // [3] FullDrawFullCharge / Piercing
    // [4] ConcussiveShot
    // =========================================================
    private float[] bowSkillWeights = new float[] { 33.34f, 33.33f, 33.33f, 0f, 0f };
    private bool hasBowSkillProfile = false;

    public bool HasBowSkillProfile => hasBowSkillProfile;

    // =========================================================
    // DEFENSE PROFILE
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
        int concussiveCount
    )
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

        // 4. Normalisasi sword
        NormalizeSwordSkillWeights(swordSkillCounts);

        // 5. Normalisasi bow 5 slot
        NormalizeBowSkillWeights(bowSkillCounts);

        // 6. Normalisasi defense
        NormalizeDefenseWeights(dashCount, riposteCount);

        // 7. Versioning
        ProfileVersion++;

        Debug.Log(
            $"[DDA] Playstyle={currentPlayerPlaystyle}, " +
            $"Weapon={currentPlayerDominantWeapon}, " +
            $"Version={ProfileVersion}, " +
            $"SwordWeights=[{swordSkillWeights[0]:F1}, {swordSkillWeights[1]:F1}, {swordSkillWeights[2]:F1}, {swordSkillWeights[3]:F1}], " +
            $"BowWeights=[Quick={bowSkillWeights[0]:F1}, Spread={bowSkillWeights[1]:F1}, FullDraw={bowSkillWeights[2]:F1}, FullCharge={bowSkillWeights[3]:F1}, Concussive={bowSkillWeights[4]:F1}], " +
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

    public float GetBowSkillWeight(BowSkillSlot slot)
    {
        int index = (int)slot;

        if (index < 0 || index >= bowSkillWeights.Length)
            return 0f;

        return bowSkillWeights[index];
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
        if (counts == null || counts.Length < SwordSkillSlotTotal)
            return;

        int total = 0;

        for (int i = 0; i < SwordSkillSlotTotal; i++)
            total += Mathf.Max(0, counts[i]);

        if (total <= 0)
        {
            swordSkillWeights = new float[] { 25f, 25f, 25f, 25f };
            return;
        }

        for (int i = 0; i < SwordSkillSlotTotal; i++)
            swordSkillWeights[i] = (Mathf.Max(0, counts[i]) / (float)total) * 100f;
    }

    private void NormalizeBowSkillWeights(int[] counts)
    {
        int[] normalizedCounts = BuildCompatibleBowCounts(counts);

        int total = 0;

        for (int i = 0; i < normalizedCounts.Length; i++)
            total += Mathf.Max(0, normalizedCounts[i]);

        if (total <= 0)
        {
            /*
             * Default sengaja hanya ofensif dasar.
             * Concussive dan FullCharge tidak diberi bobot default agar tidak muncul
             * sebelum player benar-benar memakainya.
             */
            bowSkillWeights = new float[] { 33.34f, 33.33f, 33.33f, 0f, 0f };
            hasBowSkillProfile = false;
            return;
        }

        hasBowSkillProfile = true;

        if (bowSkillWeights == null || bowSkillWeights.Length != BowSkillSlotTotal)
            bowSkillWeights = new float[BowSkillSlotTotal];

        for (int i = 0; i < BowSkillSlotTotal; i++)
        {
            bowSkillWeights[i] =
                (Mathf.Max(0, normalizedCounts[i]) / (float)total) * 100f;
        }
    }

    private int[] BuildCompatibleBowCounts(int[] counts)
    {
        int[] result = new int[BowSkillSlotTotal];

        if (counts == null)
            return result;

        /*
         * Format baru:
         * [0] QuickShot
         * [1] SpreadArrow
         * [2] FullDraw
         * [3] FullDrawFullCharge
         * [4] ConcussiveShot
         */
        if (counts.Length >= 5)
        {
            for (int i = 0; i < BowSkillSlotTotal; i++)
                result[i] = Mathf.Max(0, counts[i]);

            return result;
        }

        /*
         * Format lama:
         * [0] QuickShot
         * [1] SpreadArrow / Piercing lama
         * [2] FullDraw
         * [3] ConcussiveShot
         */
        if (counts.Length == 4)
        {
            result[(int)BowSkillSlot.QuickShot] = Mathf.Max(0, counts[0]);
            result[(int)BowSkillSlot.SpreadArrow] = Mathf.Max(0, counts[1]);
            result[(int)BowSkillSlot.FullDraw] = Mathf.Max(0, counts[2]);
            result[(int)BowSkillSlot.FullDrawFullCharge] = 0;
            result[(int)BowSkillSlot.ConcussiveShot] = Mathf.Max(0, counts[3]);
        }

        return result;
    }

    private void NormalizeDefenseWeights(int dashCount, int riposteCount)
    {
        dashCount = Mathf.Max(0, dashCount);
        riposteCount = Mathf.Max(0, riposteCount);

        int total = dashCount + riposteCount;

        if (total <= 0)
        {
            defenseDashWeight = 0f;
            defenseRiposteWeight = 0f;
            hasDefenseProfile = false;
            return;
        }

        hasDefenseProfile = true;

        defenseDashWeight = (dashCount / (float)total) * 100f;
        defenseRiposteWeight = (riposteCount / (float)total) * 100f;
    }

    // =========================================================
    // WEAPON DOMINANCE & RESET
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

        if (swordCount == max)
            return WeaponType.Sword;

        if (bowCount == max)
            return WeaponType.Bow;

        return WeaponType.Gauntlet;
    }

    public void ResetWeaponAnalysis()
    {
        swordCount = 0;
        bowCount = 0;
        gauntletCount = 0;
    }

    public void ResetDDA()
    {
        currentPlayerPlaystyle = PlayerPlaystyle.Balanced;
        currentPlayerDominantWeapon = WeaponType.None;

        swordSkillWeights = new float[] { 25f, 25f, 25f, 25f };

        /*
         * Default Bow hanya Quick, Spread, FullDraw biasa.
         * FullCharge dan Concussive tidak default agar tidak muncul tanpa data.
         */
        bowSkillWeights = new float[] { 33.34f, 33.33f, 33.33f, 0f, 0f };

        defenseDashWeight = 0f;
        defenseRiposteWeight = 0f;

        hasBowSkillProfile = false;
        hasDefenseProfile = false;

        ResetWeaponAnalysis();

        ProfileVersion++;

        Debug.Log("[DDA] Semua bobot dan profil DDA telah direset untuk stage berikutnya.");
    }
}