using UnityEngine;

/// <summary>
/// ScriptableObject terpusat untuk immunity duration stagger.
///
/// CARA PAKAI:
///   1. Klik kanan di Project window → Create → Game → Stagger Cooldown Settings
///   2. Simpan asset di Assets/Resources/ dengan nama persis "StaggerCooldownSettings"
///      (Assets/Resources/StaggerCooldownSettings.asset)
///   3. Ubah immunityDuration di sini — berlaku otomatis untuk semua skill
///      tanpa harus buka file tiap skill.
///
/// Jika asset tidak ditemukan, sistem pakai fallback 0.4 detik.
/// </summary>
[CreateAssetMenu(fileName = "StaggerCooldownSettings", menuName = "Game/Stagger Cooldown Settings")]
public class StaggerCooldownSettings : ScriptableObject
{
    private static StaggerCooldownSettings _instance;

    /// <summary>
    /// Singleton otomatis di-load dari Resources/StaggerCooldownSettings.
    /// Tidak perlu assign manual — cukup letakkan asset di folder Resources.
    /// </summary>
    public static StaggerCooldownSettings Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<StaggerCooldownSettings>("StaggerCooldownSettings");

            return _instance;
        }
    }

    [Header("Enemy Immunity Window")]
    [Tooltip("Jeda minimum (detik) sebelum enemy yang sama bisa di-stagger lagi.\n\n" +
             "Ubah HANYA di sini — berlaku untuk semua 8 skill tanpa buka file tiap skill.\n\n" +
             "0 = enemy bisa di-stagger terus tanpa jeda (tidak disarankan).\n" +
             "0.4 = jeda 400ms — cukup untuk visibilitas hit-stun tanpa terlalu kebal.")]
    [Min(0f)]
    public float immunityDuration = 0.4f;
}
