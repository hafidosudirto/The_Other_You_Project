using UnityEngine;

// SkillBase memegang 4 slot skill untuk 1 set weapon.
// Input angka 1–4 akan memicu slot sesuai indeks.
// Setiap slot diisi script yang mengimplementasikan ISkill.
//
// Di versi ini ada "global lock" supaya
// hanya 1 skill yang bisa aktif pada satu waktu.
// Skill yang sudah selesai WAJIB memanggil ReleaseLock()
// dari SkillBase (biasanya di akhir coroutine skill).

public class SkillBase : MonoBehaviour
{
    [Header("Referensi Pemilik Skill")]
    public Player player;

    [Header("Slot Skill (drag & drop MonoBehaviour yang implement ISkill)")]
    public MonoBehaviour slot1;
    public MonoBehaviour slot2;
    public MonoBehaviour slot3;
    public MonoBehaviour slot4;

    [HideInInspector]
    public bool skillLocked = false;   // true = sedang ada skill yang jalan

    void Awake()
    {
        if (player == null)
            player = GetComponentInParent<Player>();
    }

    void Start()
    {
        if (player != null)
            Debug.Log("SkillBase aktif. Player terhubung: " + player.name);
        else
            Debug.LogWarning("SkillBase belum punya referensi Player.");
    }

    // Dipanggil kalau suatu saat ingin set pemilik skill dari luar.
    public void Initialize(Player owner)
    {
        player = owner;
    }

    void Update()
    {
        if (!player) return;

        // Input utama skill set
        if (Input.GetKeyDown(KeyCode.Alpha1)) TriggerSlot(1);
        if (Input.GetKeyDown(KeyCode.Alpha2)) TriggerSlot(2);
        if (Input.GetKeyDown(KeyCode.Alpha3)) TriggerSlot(3);
        if (Input.GetKeyDown(KeyCode.Alpha4)) TriggerSlot(4);
    }

    // Fungsi utama pemicu skill berdasarkan index slot
    public void TriggerSlot(int index)
    {
        if (player == null)
        {
            Debug.LogWarning("SkillBase: Player belum di-set.");
            return;
        }

        // Kalau sedang ada skill lain yang berjalan, abaikan input baru.
        if (skillLocked)
        {
            // Bisa di-uncomment kalau mau lihat debug:
            // Debug.Log("SkillBase: Skill masih terkunci, tunggu skill sebelumnya selesai.");
            return;
        }

        ISkill skill = GetSkillFromIndex(index);

        if (skill == null)
        {
            Debug.Log($"Slot {index} belum diisi skill");
            return;
        }

        // Kunci semua skill lain.
        skillLocked = true;

        // Jalankan skill yang ada di slot.
        skill.TriggerSkill();
    }

    // Dipanggil oleh skill ketika selesai melakukan aksinya.
    // Contoh: di akhir coroutine SlashCombo / ChargedStrike / Whirlwind / Riposte.
    public void ReleaseLock()
    {
        skillLocked = false;
        // Debug.Log("SkillBase: Lock skill dilepas, input baru diizinkan.");
    }

    // Helper untuk mengambil ISkill dari slot sesuai index.
    private ISkill GetSkillFromIndex(int index)
    {
        MonoBehaviour raw = null;

        switch (index)
        {
            case 1: raw = slot1; break;
            case 2: raw = slot2; break;
            case 3: raw = slot3; break;
            case 4: raw = slot4; break;
        }

        return raw as ISkill;   // cast MonoBehaviour → ISkill
    }
}
