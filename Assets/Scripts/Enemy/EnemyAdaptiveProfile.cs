using System.Collections;
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

    private void Start()
    {
        ApplyMirrorPlaystyle();
        ApplyWeaponResponse();
        PrintDebug();
    }

    // =============================================================
    // 1. MIRROR PLAYER PLAYSTYLE (THE OTHER YOU)
    // =============================================================
    private void ApplyMirrorPlaystyle()
    {
        var playerStyle = DDAController.Instance.currentPlayerPlaystyle;

        switch (playerStyle)
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

    // =============================================================
    // 2. WEAPON-AWARE RESPONSE (MASIH MIRROR STYLE)
    // =============================================================
    private void ApplyWeaponResponse()
    {
        var weapon = DDAController.Instance.currentPlayerDominantWeapon;

        switch (weapon)
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

    private void PrintDebug()
    {
        Debug.Log($"[EnemyAdaptiveProfile] FINAL → " +
                  $"CombatStyle = {combatStyle}, " +
                  $"WeaponResponse = {weaponResponse}");
    }
}

