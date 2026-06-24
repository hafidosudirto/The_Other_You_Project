using System.Collections.Generic;

/// <summary>
/// Lapisan terjemahan: mengubah istilah teknis HFSM (jalur state & pemicu)
/// menjadi bahasa mekanik yang mudah dipahami penguji/orang awam.
///
/// Dipisah dari logika state agar tanggung jawab jelas: state & controller
/// tetap memakai istilah teknis (untuk akurasi & dokumentasi), sedangkan
/// penyajian ke layar memakai kelas ini. Satu sumber kebenaran untuk label.
/// </summary>
public static class PlayerStateLabels
{
    // Jalur state teknis -> nama mekanik yang mudah dipahami.
    private static readonly Dictionary<string, string> StateFriendly = new Dictionary<string, string>
    {
        { "Locomotion/Idle",     "Diam" },
        { "Locomotion/Move",     "Bergerak" },
        { "Locomotion/Dash",     "Menghindar (Dash)" },
        { "Combat/Sword",        "Serangan Pedang" },
        { "Combat/Bow",          "Serangan Panah" },
        { "Interrupt/Staggered", "Terhuyung" },
        { "Interrupt/Dead",      "Kalah" },
        { "(belum mulai)",       "(belum mulai)" },
        { "(stopped)",           "(berhenti)" },
        { "-",                   "-" },
        { "",                    "-" },
    };

    // Pemicu teknis -> penjelasan sebab dalam bahasa awam.
    private static readonly Dictionary<string, string> TriggerFriendly = new Dictionary<string, string>
    {
        { "diam (default)",                "tidak ada input gerak" },
        { "velocity > threshold",          "pemain berjalan" },
        { "sedang dash",                   "menekan tombol dash" },
        { "skill sword aktif",             "memakai skill pedang" },
        { "skill bow aktif",               "memakai skill panah" },
        { "skill bow (charge Full Draw)",  "menahan / charge panah (Full Draw)" },
        { "isStaggered == true",           "terkena serangan musuh" },
        { "currentHP <= 0",                "darah (HP) habis" },
        { "character null (fallback)",     "-" },
        { "-",                                           "-" },
        { "",                                            "-" },
    };

    /// <summary>Nama mekanik untuk satu jalur state, mis. "Combat/Sword" -> "Serangan Pedang".</summary>
    public static string State(string path)
    {
        if (string.IsNullOrEmpty(path))
            return "-";

        return StateFriendly.TryGetValue(path, out string friendly) ? friendly : path;
    }

    /// <summary>Penjelasan sebab untuk satu pemicu transisi.</summary>
    public static string Trigger(string trigger)
    {
        if (string.IsNullOrEmpty(trigger))
            return "-";

        return TriggerFriendly.TryGetValue(trigger, out string friendly) ? friendly : trigger;
    }

    /// <summary>
    /// Terjemahkan satu baris transisi "A -> B" menjadi "A_awam → B_awam".
    /// Dipakai untuk daftar riwayat agar ikut terbaca awam.
    /// </summary>
    public static string TransitionLine(string line)
    {
        if (string.IsNullOrEmpty(line))
            return line;

        int idx = line.IndexOf("->");
        if (idx < 0)
            return State(line.Trim());

        string left = line.Substring(0, idx).Trim();
        string right = line.Substring(idx + 2).Trim();
        return State(left) + " → " + State(right);
    }
}
