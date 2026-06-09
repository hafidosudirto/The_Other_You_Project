using UnityEngine;

/// <summary>
/// Konfigurasi damage untuk Sword_Whirlwind.
///
/// Damage di sini bersifat FLAT (angka pasti), sama persis konsepnya dengan
/// "damageQuickShot" pada Bow_QuickShot. Angka ini TIDAK dikalikan player.attack,
/// jadi nilai yang kamu isi = damage yang benar-benar masuk ke musuh.
///
/// Whirlwind memberi damage berulang ("tick") setiap beberapa saat selama berputar.
/// Angka di bawah adalah damage untuk SATU tick, bukan total seluruh putaran.
/// </summary>
[System.Serializable]
public class SwordWhirlwindDamage
{
    [Header("Damage Flat (per tick)")]
    [Tooltip("Damage untuk SATU tick Whirlwind (satu kali pukulan, terjadi tiap hitInterval detik).\n" +
             "Angka pasti yang masuk ke musuh per tick, TIDAK dikali player.attack.\n" +
             "Total damage = angka ini x berapa kali tick terjadi selama durasi Whirlwind.")]
    [Min(0f)]
    public float damagePerTick = 4f;
}
