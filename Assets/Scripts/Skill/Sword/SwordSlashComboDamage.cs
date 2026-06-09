using UnityEngine;

/// <summary>
/// Konfigurasi damage untuk Sword_SlashCombo.
///
/// Damage di sini bersifat FLAT (angka pasti), sama persis konsepnya dengan
/// "damageQuickShot" pada Bow_QuickShot. Angka ini TIDAK dikalikan player.attack,
/// jadi nilai yang kamu isi = damage yang benar-benar masuk ke musuh.
///
/// SlashCombo punya dua ayunan: Slash1 (ayunan pertama) dan Slash2 (lanjutan combo),
/// jadi damage-nya dipisah supaya bisa di-tuning sendiri-sendiri.
/// </summary>
[System.Serializable]
public class SwordSlashComboDamage
{
    [Header("Damage Flat (per ayunan)")]
    [Tooltip("Damage ayunan pertama (Slash1).\n" +
             "Angka pasti yang masuk ke musuh, TIDAK dikali player.attack.")]
    [Min(0f)]
    public float damageSlash1 = 6f;

    [Tooltip("Damage ayunan kedua (Slash2), yaitu lanjutan combo setelah Slash1 mengenai.\n" +
             "Angka pasti yang masuk ke musuh, TIDAK dikali player.attack.")]
    [Min(0f)]
    public float damageSlash2 = 9f;
}
