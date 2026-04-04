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

/// <summary>
/// Adaptive profile final:
/// - Mirror playstyle pemain dari DDAController
/// - Ambil weapon response dari senjata dominan pemain
/// - Ambil bobot Sword musuh LANGSUNG dari hasil normalisasi riil DDAController
/// </summary>
public class EnemyAdaptiveProfile : MonoBehaviour
{
    [Header("Final AI Settings (Computed)")]
    public EnemyCombatStyle combatStyle = EnemyCombatStyle.Balanced;
    public EnemyWeaponResponse weaponResponse = EnemyWeaponResponse.None;

    [Header("Normalized Sword Weights")]
    [SerializeField] private float[] normalizedSwordWeights = new float[] { 25f, 25f, 25f, 25f };

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
            ResetToDefaultWeights();
            return;
        }

        float[] copy = dda.GetCurrentSwordSkillWeightsCopy();
        if (copy == null || copy.Length != 4)
        {
            ResetToDefaultWeights();
            return;
        }

        for (int i = 0; i < 4; i++)
            normalizedSwordWeights[i] = copy[i];
    }

    private void ResetToDefaultWeights()
    {
        for (int i = 0; i < normalizedSwordWeights.Length; i++)
            normalizedSwordWeights[i] = 25f;
    }

    public IReadOnlyList<float> GetSwordSkillWeights() => normalizedSwordWeights;

    public float[] GetSwordSkillWeightsCopy()
    {
        return (float[])normalizedSwordWeights.Clone();
    }

    private void PrintDebug()
    {
        Debug.Log(
            $"[EnemyAdaptiveProfile] FINAL -> " +
            $"CombatStyle={combatStyle}, WeaponResponse={weaponResponse}, " +
            $"SwordWeights=[{normalizedSwordWeights[0]:F2}, {normalizedSwordWeights[1]:F2}, {normalizedSwordWeights[2]:F2}, {normalizedSwordWeights[3]:F2}]"
        );
    }
}
