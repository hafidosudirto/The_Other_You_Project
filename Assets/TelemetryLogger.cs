using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// TelemetryLogger — modul pencatat telemetri untuk Dynamic Difficulty Testing (Bab 4).
///
/// Pengamat pasif: tidak mengubah logika game. Menulis satu baris CSV per stage ke
/// Application.persistentDataPath. Satu berkas = satu sesi (satu senjata, replay dari stage 1).
///
/// Dirancang persis untuk arsitektur The Other You:
///   - StageManager (FSM orchestrator)  -> stat & komposisi minion (T1)
///   - DataTracker  (counter per-stage)  -> skill pemain (T2)        [counter di-reset tiap stage]
///   - DDAController (profil & bobot)     -> bobot skill boss (T3)
///
/// CATATAN PENTING TENTANG TIMING (sesuai kode aslimu):
///   DataTracker.FinalizeStageData() memanggil DDA.UpdatePlayerProfile() LALU ResetData().
///   Dengan finalizeDataBeforeBoss=true DAN finalizeDataAfterBoss=true, FinalizeStageData()
///   terpanggil DUA KALI per stage. Karena itu T2/T3 hanya ditangkap SEKALI per stage
///   (penjaga _t2t3Captured), yaitu pada finalisasi sebelum boss saat counter masih berisi
///   data nyata. Panggilan kedua (data nol) otomatis diabaikan.
/// </summary>
public class TelemetryLogger : MonoBehaviour
{
    public static TelemetryLogger Instance { get; private set; }

    public enum SessionWeapon { Sword, Bow }

    // Slot mengikuti enum di DataTracker.cs / DDAController.cs.
    private static readonly string[] SwordSkillNames = { "Slash", "Whirlwind", "Charged", "Riposte" };       // 4 slot
    private static readonly string[] BowSkillNames = { "Quick", "Spread", "FullDraw", "FullCharge", "Concussive" }; // 5 slot

    private const int SwordSlots = 4;
    private const int BowSlots = 5;

    // Fallback stat dasar minion bila prefab CharacterBase tidak terbaca (Enemy.cs: HP50/Atk8/Def2/Spd2).
    private const float FallbackBaseHP = 50f, FallbackBaseAtk = 8f, FallbackBaseDef = 2f, FallbackBaseSpd = 2f;

    private StreamWriter _writer;
    private string _filePath;
    private SessionWeapon _sessionWeapon;
    private bool _sessionActive;

    private StageRow _row;
    private bool _rowOpen;
    private bool _t2t3Captured;

    private readonly int[] _bossSkillExec = new int[BowSlots]; // muat 5 slot (bow); sword pakai 0..3

    // -----------------------------------------------------------------------------
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnApplicationQuit() => EndSession();

    // =============================================================================
    // SESI
    // =============================================================================
    public void BeginSession(SessionWeapon weapon)
    {
        EndSession();
        _sessionWeapon = weapon;

        string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        string fileName = $"telemetry_{weapon.ToString().ToLowerInvariant()}_{ts}.csv";
        _filePath = Path.Combine(Application.persistentDataPath, fileName);

        _writer = new StreamWriter(_filePath, false, Encoding.UTF8);
        _writer.WriteLine(BuildHeader());
        _writer.Flush();
        _sessionActive = true;

        Debug.Log($"[TelemetryLogger] Sesi {weapon} dimulai. CSV: {_filePath}");
    }

    public void EndSession()
    {
        if (_rowOpen) FlushStageRow();
        if (_writer != null)
        {
            _writer.Flush();
            _writer.Close();
            _writer = null;
            Debug.Log($"[TelemetryLogger] Sesi ditutup. Berkas: {_filePath}");
        }
        _sessionActive = false;
    }

    // =============================================================================
    // T1 — dipanggil di akhir SpawningMinionsPhase.SpawnMinionWaveRoutine()
    // =============================================================================
    /// <summary>
    /// Catat stat & komposisi awal stage (T1). Bila stageDisplay == 1 (run baru) atau belum ada
    /// sesi aktif, otomatis membuka berkas CSV baru sesuai senjata sesi.
    /// </summary>
    public void RecordStageStart_T1(
        int stageDisplay, int meleeCount, int rangeCount, float statMultiplier,
        GameObject minionPrefabForBaseStats, WeaponType sessionWeapon)
    {
        SessionWeapon sw = sessionWeapon == WeaponType.Bow ? SessionWeapon.Bow : SessionWeapon.Sword;

        // Buka berkas baru di awal run (stage 1) atau bila belum ada sesi.
        if (!_sessionActive || stageDisplay <= 1)
            BeginSession(sw);

        if (_rowOpen) FlushStageRow(); // amankan stage sebelumnya bila belum sempat di-flush

        // Stat dasar minion dari prefab (tidak ter-multiply karena prefab asset tak pernah di-Instantiate).
        float bHP = FallbackBaseHP, bAtk = FallbackBaseAtk, bDef = FallbackBaseDef, bSpd = FallbackBaseSpd;
        if (minionPrefabForBaseStats != null)
        {
            CharacterBase baseRef = minionPrefabForBaseStats.GetComponent<CharacterBase>();
            if (baseRef == null) baseRef = minionPrefabForBaseStats.GetComponentInChildren<CharacterBase>(true);
            if (baseRef != null) { bHP = baseRef.maxHP; bAtk = baseRef.attack; bDef = baseRef.defense; bSpd = baseRef.moveSpeed; }
        }

        float m = Mathf.Max(0.01f, statMultiplier);
        _row = new StageRow
        {
            stage = stageDisplay,
            meleeCount = meleeCount,
            rangeCount = rangeCount,
            statMultiplier = m,
            maxHP = bHP * m,
            attack = bAtk * m,
            defense = bDef * m,
            moveSpeed = bSpd * m
        };
        _rowOpen = true;
        _t2t3Captured = false;
        Array.Clear(_bossSkillExec, 0, _bossSkillExec.Length);
    }

    // =============================================================================
    // T2 + T3 — dipanggil di dalam DataTracker.FinalizeStageData() SEBELUM ResetData()
    // =============================================================================
    /// <summary>
    /// Catat skill pemain stage ini (T2, counter mentah per-stage) + bobot/profil boss (T3).
    /// Hanya ditangkap sekali per stage; panggilan finalisasi sesudah boss (data nol) diabaikan.
    /// </summary>
    public void RecordT2T3(
        int[] swordSkillCounts, int[] bowSkillCounts,
        int offensive, int defensive, int dash, int riposte, int bowConcussive,
        DDAController dda)
    {
        if (!_sessionActive || !_rowOpen || _t2t3Captured) return;

        // T2: counter per-stage (DataTracker sudah reset tiap stage, jadi ini murni aksi stage ini).
        CopyIntoI(_row.swordCounts, swordSkillCounts, SwordSlots);
        CopyIntoI(_row.bowCounts, bowSkillCounts, BowSlots);
        _row.offensive = offensive;
        _row.defensive = defensive;
        _row.dash = dash;
        _row.riposte = riposte;
        _row.bowConcussive = bowConcussive;

        // T3: profil & bobot ternormalisasi (UpdatePlayerProfile sudah dijalankan sebelum titik ini).
        if (dda != null)
        {
            _row.profileVersion = dda.ProfileVersion;
            _row.playstyle = dda.currentPlayerPlaystyle.ToString();
            _row.dominantWeapon = dda.currentPlayerDominantWeapon.ToString();
            CopyIntoF(_row.swordWeights, dda.GetCurrentSwordSkillWeightsCopy(), SwordSlots);
            CopyIntoF(_row.bowWeights, dda.GetCurrentBowSkillWeightsCopy(), BowSlots);
            _row.defenseDashWeight = dda.GetCurrentDefenseDashWeight();
            _row.defenseRiposteWeight = dda.GetCurrentDefenseRiposteWeight();
        }
        _t2t3Captured = true;
    }

    // =============================================================================
    // T3 (eksekusi) — dipanggil dari node skill boss setiap boss mengeksekusi skill
    // =============================================================================
    /// <summary>
    /// Catat satu eksekusi skill boss. skillIndex memakai slot senjata sesi
    /// (Sword 0..3, Bow 0..4). Mengisi kolom jumlah skill boss (Tabel 4.8 / 4.12).
    /// </summary>
    public void RecordBossSkillExecuted(int skillIndex)
    {
        if (!_sessionActive || !_rowOpen) return;
        if (skillIndex < 0 || skillIndex >= _bossSkillExec.Length) return;
        _bossSkillExec[skillIndex]++;
    }

    // =============================================================================
    // FLUSH — dipanggil di StageClearedPhase.EnterPhase()
    // =============================================================================
    public void FlushStageRow()
    {
        if (!_sessionActive || !_rowOpen || _writer == null) return;
        for (int i = 0; i < _bossSkillExec.Length; i++) _row.bossExec[i] = _bossSkillExec[i];
        _writer.WriteLine(BuildRow(_row));
        _writer.Flush();
        _rowOpen = false;
    }

    // =============================================================================
    // CSV
    // =============================================================================
    private string BuildHeader()
    {
        var sb = new StringBuilder();
        sb.Append("session_weapon,stage,profile_version,playstyle,dominant_weapon,boss_type,");
        sb.Append("melee_count,range_count,stat_multiplier,max_hp,attack,defense,move_speed,");
        // T2 counter pemain
        for (int i = 0; i < SwordSlots; i++) sb.Append($"ps_{SwordSkillNames[i]},");
        for (int i = 0; i < BowSlots; i++) sb.Append($"pb_{BowSkillNames[i]},");
        sb.Append("offensive,defensive,dash,riposte_act,bow_concussive_act,");
        // T3 bobot boss
        for (int i = 0; i < SwordSlots; i++) sb.Append($"ws_{SwordSkillNames[i]},");
        for (int i = 0; i < BowSlots; i++) sb.Append($"wb_{BowSkillNames[i]},");
        sb.Append("w_def_dash,w_def_riposte,");
        // T3 jumlah eksekusi boss (5 slot; sword pakai 0..3)
        for (int i = 0; i < BowSlots; i++) sb.Append($"be_{i},");
        sb.Append("be_total,timestamp");
        return sb.ToString();
    }

    private string BuildRow(StageRow r)
    {
        // boss_type ditentukan dari senjata sesi (sesi Sword -> BossSword, sesi Bow -> BossBow).
        string bossType = _sessionWeapon == SessionWeapon.Bow ? "BossBow" : "BossSword";

        var sb = new StringBuilder();
        sb.Append(_sessionWeapon).Append(',');
        sb.Append(r.stage).Append(',');
        sb.Append(r.profileVersion).Append(',');
        sb.Append(Esc(r.playstyle)).Append(',');
        sb.Append(Esc(r.dominantWeapon)).Append(',');
        sb.Append(Esc(bossType)).Append(',');
        sb.Append(r.meleeCount).Append(',');
        sb.Append(r.rangeCount).Append(',');
        sb.Append(F(r.statMultiplier)).Append(',');
        sb.Append(F(r.maxHP)).Append(',');
        sb.Append(F(r.attack)).Append(',');
        sb.Append(F(r.defense)).Append(',');
        sb.Append(F(r.moveSpeed)).Append(',');
        for (int i = 0; i < SwordSlots; i++) sb.Append(r.swordCounts[i]).Append(',');
        for (int i = 0; i < BowSlots; i++) sb.Append(r.bowCounts[i]).Append(',');
        sb.Append(r.offensive).Append(',');
        sb.Append(r.defensive).Append(',');
        sb.Append(r.dash).Append(',');
        sb.Append(r.riposte).Append(',');
        sb.Append(r.bowConcussive).Append(',');
        for (int i = 0; i < SwordSlots; i++) sb.Append(F(r.swordWeights[i])).Append(',');
        for (int i = 0; i < BowSlots; i++) sb.Append(F(r.bowWeights[i])).Append(',');
        sb.Append(F(r.defenseDashWeight)).Append(',');
        sb.Append(F(r.defenseRiposteWeight)).Append(',');
        int total = 0;
        for (int i = 0; i < BowSlots; i++) { sb.Append(r.bossExec[i]).Append(','); total += r.bossExec[i]; }
        sb.Append(total).Append(',');
        sb.Append(DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
        return sb.ToString();
    }

    // =============================================================================
    // Helpers
    // =============================================================================
    private static void CopyIntoI(int[] dest, int[] src, int n)
    {
        for (int i = 0; i < n; i++) dest[i] = (src != null && i < src.Length) ? src[i] : 0;
    }
    private static void CopyIntoF(float[] dest, float[] src, int n)
    {
        for (int i = 0; i < n; i++) dest[i] = (src != null && i < src.Length) ? src[i] : 0f;
    }
    private static string F(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);
    private static string Esc(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.IndexOf(',') >= 0 || s.IndexOf('"') >= 0) return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    private class StageRow
    {
        public int stage, profileVersion;
        public string playstyle = "", dominantWeapon = "";
        public int meleeCount, rangeCount;
        public float statMultiplier, maxHP, attack, defense, moveSpeed;
        public int[] swordCounts = new int[SwordSlots];
        public int[] bowCounts = new int[BowSlots];
        public int offensive, defensive, dash, riposte, bowConcussive;
        public float[] swordWeights = new float[SwordSlots];
        public float[] bowWeights = new float[BowSlots];
        public float defenseDashWeight, defenseRiposteWeight;
        public int[] bossExec = new int[BowSlots];
    }
}
