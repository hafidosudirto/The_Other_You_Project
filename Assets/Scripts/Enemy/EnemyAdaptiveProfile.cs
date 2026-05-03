using System.Collections.Generic;
using UnityEngine;

public enum EnemyCombatStyle
{
    Balanced,
    Offensive,
    Defensive
}

public enum EnemyWeaponResponse
{
    None,
    VsSword,
    VsBow,
    VsGauntlet
}

public class EnemyAdaptiveProfile : MonoBehaviour
{
    [Header("Final AI Settings (Computed)")]
    public EnemyCombatStyle combatStyle = EnemyCombatStyle.Balanced;
    public EnemyWeaponResponse weaponResponse = EnemyWeaponResponse.None;

    [Header("Normalized Attack Weights (Bow Slot 3 = Concussive)")]
    [SerializeField] private float[] normalizedSwordWeights = new float[] { 25f, 25f, 25f, 25f };
    [SerializeField] private float[] normalizedBowWeights = new float[] { 25f, 25f, 25f, 25f };

    [Header("Normalized Defense Weights")]
    [SerializeField] private float normalizedDashWeight = 0f;
    [SerializeField] private float normalizedRiposteWeight = 0f;

    private void Start()
    {
        RefreshFromDDA();
        PrintDebug();
    }

    public void RefreshFromDDA()
    {
        ApplyMirrorPlaystyle();
        ApplyWeaponResponse();
        CopyRealNormalizedSwordWeights();
        CopyRealNormalizedBowWeights();
        CopyRealNormalizedDefenseWeights();
    }

    private void ApplyMirrorPlaystyle()
    {
        var dda = DDAController.Instance;
        if (dda == null)
        {
            combatStyle = EnemyCombatStyle.Balanced;
            return;
        }

        switch (dda.currentPlayerPlaystyle)
        {
            case PlayerPlaystyle.OffensiveDominant:
                combatStyle = EnemyCombatStyle.Offensive;
                break;
            case PlayerPlaystyle.DefensiveDominant:
                combatStyle = EnemyCombatStyle.Defensive;
                break;
            case PlayerPlaystyle.Balanced:
            default:
                combatStyle = EnemyCombatStyle.Balanced;
                break;
        }
    }

    private void ApplyWeaponResponse()
    {
        var dda = DDAController.Instance;
        if (dda == null)
        {
            weaponResponse = EnemyWeaponResponse.None;
            return;
        }

        switch (dda.currentPlayerDominantWeapon)
        {
            case WeaponType.Sword:
                weaponResponse = EnemyWeaponResponse.VsSword;
                break;
            case WeaponType.Bow:
                weaponResponse = EnemyWeaponResponse.VsBow;
                break;
            case WeaponType.Gauntlet:
                weaponResponse = EnemyWeaponResponse.VsGauntlet;
                break;
            default:
                weaponResponse = EnemyWeaponResponse.None;
                break;
        }
    }

    private void CopyRealNormalizedSwordWeights()
    {
        var dda = DDAController.Instance;
        if (dda == null)
        {
            ResetToDefaultSwordWeights();
            return;
        }

        float[] copy = dda.GetCurrentSwordSkillWeightsCopy();
        if (copy == null || copy.Length != 4)
        {
            ResetToDefaultSwordWeights();
            return;
        }

        // Pengaman ukuran array Sword
        if (normalizedSwordWeights == null || normalizedSwordWeights.Length != 4)
        {
            normalizedSwordWeights = new float[4];
        }

        for (int i = 0; i < 4; i++)
            normalizedSwordWeights[i] = copy[i];
    }

    private void CopyRealNormalizedBowWeights()
    {
        var dda = DDAController.Instance;
        if (dda == null || !dda.HasBowSkillProfile)
        {
            ResetToDefaultBowWeights();
            return;
        }

        float[] copy = dda.GetCurrentBowSkillWeightsCopy();
        if (copy == null || copy.Length != 4)
        {
            ResetToDefaultBowWeights();
            return;
        }

        // FIX: Pengaman ukuran array Bow. Paksa resize jadi 4 jika Inspector masih menyimpan ukuran lama.
        if (normalizedBowWeights == null || normalizedBowWeights.Length != 4)
        {
            normalizedBowWeights = new float[4];
        }

        for (int i = 0; i < 4; i++)
            normalizedBowWeights[i] = copy[i];
    }

    private void CopyRealNormalizedDefenseWeights()
    {
        var dda = DDAController.Instance;
        if (dda == null || !dda.HasDefenseProfile)
        {
            normalizedDashWeight = 0f;
            normalizedRiposteWeight = 0f;
            return;
        }

        normalizedDashWeight = dda.GetCurrentDefenseDashWeight();
        normalizedRiposteWeight = dda.GetCurrentDefenseRiposteWeight();
    }

    private void ResetToDefaultSwordWeights()
    {
        if (normalizedSwordWeights == null || normalizedSwordWeights.Length != 4)
        {
            normalizedSwordWeights = new float[4];
        }
        for (int i = 0; i < normalizedSwordWeights.Length; i++)
            normalizedSwordWeights[i] = 25f;
    }

    private void ResetToDefaultBowWeights()
    {
        if (normalizedBowWeights == null || normalizedBowWeights.Length != 4)
        {
            normalizedBowWeights = new float[4];
        }
        for (int i = 0; i < normalizedBowWeights.Length; i++)
            normalizedBowWeights[i] = 25f;
    }

    public IReadOnlyList<float> GetSwordSkillWeights() => normalizedSwordWeights;

    public IReadOnlyList<float> GetBowSkillWeights() => normalizedBowWeights;

    public float[] GetSwordSkillWeightsCopy()
    {
        return (float[])normalizedSwordWeights.Clone();
    }

    public float[] GetBowSkillWeightsCopy()
    {
        return (float[])normalizedBowWeights.Clone();
    }

    private void PrintDebug()
    {
        Debug.Log(
            $"[EnemyAdaptiveProfile] FINAL -> " +
            $"CombatStyle={combatStyle}, WeaponResponse={weaponResponse}, " +
            $"SwordWeights=[{normalizedSwordWeights[0]:F2}, {normalizedSwordWeights[1]:F2}, {normalizedSwordWeights[2]:F2}, {normalizedSwordWeights[3]:F2}], " +
            $"BowWeights=[{normalizedBowWeights[0]:F2}, {normalizedBowWeights[1]:F2}, {normalizedBowWeights[2]:F2}, {normalizedBowWeights[3]:F2}], " +
            $"DefenseWeights=[Dash={normalizedDashWeight:F2}, Riposte={normalizedRiposteWeight:F2}]"
        );
    }
}