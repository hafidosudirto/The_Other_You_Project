using UnityEngine;

/// <summary>
/// Konfigurasi damage untuk Sword_Riposte.
///
/// Damage di sini bersifat FLAT (angka pasti), sama persis konsepnya dengan
/// "damageQuickShot" pada Bow_QuickShot. Angka ini TIDAK dikalikan player.attack,
/// jadi nilai yang kamu isi = damage yang benar-benar masuk ke musuh.
///
/// Riposte itu satu serangan balasan (counter) saat dash mengenai musuh,
/// jadi cukup satu angka damage.
/// </summary>
[System.Serializable]
public class SwordRiposteDamage
{
    [Header("Damage Flat")]
    [Tooltip("Damage serangan balasan (counter) Riposte saat dash mengenai musuh.\n" +
             "Angka pasti yang masuk ke musuh, TIDAK dikali player.attack.")]
    [Min(0f)]
    public float damageRiposte = 12f;
}
