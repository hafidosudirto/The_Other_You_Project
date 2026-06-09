using UnityEngine;

/// <summary>
/// Konfigurasi damage untuk Sword_ChargedStrike.
///
/// Damage di sini bersifat FLAT (angka pasti), sama persis konsepnya dengan
/// "damageQuickShot" pada Bow_QuickShot. Angka ini TIDAK dikalikan player.attack,
/// jadi nilai yang kamu isi = damage yang benar-benar masuk ke musuh.
///
/// ChargedStrike punya mekanik "charge" (ditahan dulu baru dilepas), jadi damage-nya
/// dipisah jadi dua: damage saat baru ditekan (charge 0%) dan damage saat charge penuh.
/// </summary>
[System.Serializable]
public class SwordChargedStrikeDamage
{
    [Header("Damage Flat (per tingkat charge)")]
    [Tooltip("Damage saat tombol dilepas TANPA ditahan (charge 0%).\n" +
             "Angka pasti yang masuk ke musuh, TIDAK dikali player.attack.")]
    [Min(0f)]
    public float damageChargeMinimum = 5f;

    [Tooltip("Damage saat charge PENUH (ditahan sampai maxChargeTime, charge 100%).\n" +
             "Di antara 0% dan 100%, damage dihitung mulus (lerp) sesuai lama menahan tombol.")]
    [Min(0f)]
    public float damageChargeMaksimum = 15f;

    [Header("Opsi")]
    [Tooltip("Jika aktif, damage hasil charge dibulatkan ke angka bulat terdekat.\n" +
             "Membuat damage terasa bertingkat / rapi (mis. 5, 6, 7 ... 15) seperti perilaku lama.\n" +
             "Matikan kalau kamu ingin damah pecahan (mis. 7.3).")]
    public bool bulatkanDamage = true;

    /// <summary>
    /// Hitung damage flat berdasarkan persentase charge (0 = belum di-charge, 1 = charge penuh).
    /// </summary>
    public float HitungDamage(float chargePercent01)
    {
        chargePercent01 = Mathf.Clamp01(chargePercent01);
        float damage = Mathf.Lerp(damageChargeMinimum, damageChargeMaksimum, chargePercent01);

        if (bulatkanDamage)
            damage = Mathf.Round(damage);

        return Mathf.Max(0f, damage);
    }
}
