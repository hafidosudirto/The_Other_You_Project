using System;
using System.IO;
using System.Globalization;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PUSAT TELEMETRY (singleton, DontDestroyOnLoad).
///
/// Mencatat empat berkas CSV per sesi di <c>Application.persistentDataPath/Telemetry</c>:
/// <list type="bullet">
/// <item><b>stage_summary</b> — 1 baris per stage selesai. Memuat seluruh kolom DDA/profil
/// (offensive/defensive, skill weights, dll) PLUS metrik baru (waktu, damage, APM, movement, fps, dll).</item>
/// <item><b>events</b> — log berstempel waktu: skill cast/hit, death, kill, pause/resume, reaction.</item>
/// <item><b>skills</b> — agregat per-skill per-stage: casts, hits, success_rate, damage.</item>
/// <item><b>heatmap</b> — occupancy grid posisi player (ditulis saat sesi berakhir).</item>
/// </list>
///
/// Sebagian besar metrik di-<i>self-track</i> (APM/IPM, movement, FPS, pause/quit/crash) agar
/// perubahan di file lain minimal. Sisanya diisi lewat API publik yang dipanggil dari hook:
/// <see cref="RecordDamage"/>, <see cref="RecordSkillCast"/>, <see cref="RecordPlayerDeath"/>,
/// <see cref="RecordEnemyKilled"/>, <see cref="BeginStage"/>, <see cref="CaptureStageProfileSnapshot"/>,
/// <see cref="LogStageSummary"/>, <see cref="RecordReactionStimulus"/>, <see cref="RecordReactionResponse"/>.
/// </summary>
public class TelemetryLogger : MonoBehaviour
{
    public static TelemetryLogger Instance;

    // =====================================================================
    // KONFIGURASI
    // =====================================================================

    [Header("Sampling")]
    [Tooltip("Interval (detik) pengambilan sampel posisi player untuk distance & heatmap.")]
    [SerializeField] private float movementSampleInterval = 0.1f;
    [Tooltip("Ukuran sel grid heatmap dalam satuan world.")]
    [SerializeField] private float heatmapCellSize = 1f;
    [Tooltip("FPS di bawah ambang ini dihitung sebagai 'fps drop'.")]
    [SerializeField] private float fpsDropThreshold = 30f;
    [Tooltip("Jendela waktu (detik) untuk mengaitkan damage ke skill terakhir yang di-cast (success_rate).")]
    [SerializeField] private float skillHitAttributionWindow = 1.5f;
    [Tooltip("Jeda maksimal (detik) antar hit agar masih dihitung satu combo.")]
    [SerializeField] private float comboWindow = 1.2f;
    [Tooltip("Jendela maksimal (detik) stimulus->response agar dihitung sebagai reaksi valid.")]
    [SerializeField] private float reactionWindow = 2f;

    // =====================================================================
    // METADATA SESI
    // =====================================================================

    private const string PlayerIdPrefsKey = "TELEMETRY_ANON_PLAYER_ID";
    private const string DirtySessionPrefsKey = "TELEMETRY_SESSION_DIRTY"; // deteksi crash

    private string _sessionId;
    private string _playerId;
    private string _buildVersion;
    private string _sessionWeapon = "Unknown";
    private float _sessionStartRealtime;

    // =====================================================================
    // WRITER
    // =====================================================================

    private string _eventPath, _stagePath, _skillPath, _heatmapPath;
    private StreamWriter _eventWriter, _stageWriter, _skillWriter;

    // =====================================================================
    // STATE PER-STAGE (akumulator telemetry milik sendiri)
    // =====================================================================

    private int _currentStage = -1;
    private bool _stageSummaryWritten;
    private float _stageStartRealtime;

    private float _stageDamageDealt;
    private float _stageDamageTaken;
    private int _stageDeaths;
    private int _stageMinionKills;
    private float _stageMinionKillTimeSum;
    private float _stageBossKillTime; // -1 jika belum
    private int _stageInputs;          // untuk IPM
    private int _stageActions;         // untuk APM
    private float _stageDistance;
    private int _stageRangedShots;
    private int _stageRangedHits;
    private int _stageMaxCombo;
    private int _stageCurrentCombo;
    private float _stageLastComboTime;

    // FPS
    private int _stageFrameCount;
    private float _stageFpsSum;
    private float _stageMinFps;
    private int _stageFpsDrops;

    // pause
    private int _stagePauseCount;

    // Snapshot profil (diambil dari DataTracker SEBELUM reset pra-boss)
    private bool _hasProfileSnapshot;
    private int _snapOffensive, _snapDefensive, _snapDash, _snapRiposte, _snapConcussive, _snapSwordUse, _snapBowUse;

    // Per-skill per-stage: key = "Weapon/SkillName"
    private class SkillStat { public int casts; public int hits; public float damage; }
    private readonly Dictionary<string, SkillStat> _stageSkillStats = new Dictionary<string, SkillStat>();

    // Atribusi damage->skill
    private string _activeSkillKey;
    private WeaponType _activeSkillWeapon = WeaponType.None;
    private float _activeSkillTime = -999f;

    // Reaksi
    private float _lastStimulusTime = -999f;
    private readonly List<float> _reactionTimes = new List<float>();

    // time_between_deaths (lintas-stage, berbasis waktu sesi)
    private float _lastDeathRealtime = -1f;

    // Heatmap occupancy: key = "cellX,cellY" -> count (akumulasi sepanjang sesi)
    private readonly Dictionary<long, int> _heatmap = new Dictionary<long, int>();

    // Snapshot cast skill musuh terakhir — dipakai agar baris CSV lengkap
    // (cast_post_x/y, target_pos_x/y, is_hit, profil, distribusi, node BT)
    // ditulis satu kali saat hit/cleanup, bukan dua kali (cast+hit) yang bisa
    // terduplikasi bila skill di-trigger cepat beruntun.
    private struct PendingEnemyCast
    {
        public bool active;
        public string skillName;
        public Vector2 casterPos;
        public Vector2 targetPos;
        public bool isHit;
        public string playerProfile;
        public string skillDistribution;
        public string activeBtNode;
        public float castRealtime;
    }
    private PendingEnemyCast _pendingEnemyCast;

    // Active node BT terakhir (di-update dari Node.Evaluate()).
    private static string s_lastBtNodeName = string.Empty;
    public static void NotifyBtNodeEvaluated(string nodeName)
    {
        if (!string.IsNullOrEmpty(nodeName))
            s_lastBtNodeName = nodeName;
    }

    // Movement sampling
    private Transform _playerTransform;
    private Vector3 _lastSamplePos;
    private bool _hasLastSamplePos;
    private float _lastSampleTime;

    private bool _stageActive;

    // =====================================================================
    // LIFECYCLE
    // =====================================================================

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitSessionMetadata();
        InitFiles();
        DetectPreviousCrash();

        _sessionStartRealtime = Time.realtimeSinceStartup;
        _stageMinFps = float.MaxValue;
        _stageBossKillTime = -1f;
    }

    private void InitSessionMetadata()
    {
        _sessionId = Guid.NewGuid().ToString("N");
        _buildVersion = Application.version;

        // player_id anonim: GUID acak persisten (tidak mengandung info pribadi).
        _playerId = PlayerPrefs.GetString(PlayerIdPrefsKey, string.Empty);
        if (string.IsNullOrEmpty(_playerId))
        {
            _playerId = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(PlayerIdPrefsKey, _playerId);
            PlayerPrefs.Save();
        }
    }

    private void InitFiles()
    {
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string folder = Path.Combine(Application.persistentDataPath, "Telemetry");
        Directory.CreateDirectory(folder);

        _eventPath = Path.Combine(folder, $"events_{stamp}.csv");
        _stagePath = Path.Combine(folder, $"stage_summary_{stamp}.csv");
        _skillPath = Path.Combine(folder, $"skills_{stamp}.csv");
        _heatmapPath = Path.Combine(folder, $"heatmap_{stamp}.csv");

        _eventWriter = new StreamWriter(_eventPath, false, Encoding.UTF8);
        _stageWriter = new StreamWriter(_stagePath, false, Encoding.UTF8);
        _skillWriter = new StreamWriter(_skillPath, false, Encoding.UTF8);

        _eventWriter.WriteLine("timestamp_utc,session_time,session_id,stage_number,event_type,detail,value,skill_name,cast_post_x,cast_post_y,target_pos_x,target_pos_y,is_hit,player_profile,skill_distribution,active_bt_node");
        _eventWriter.Flush();

        _stageWriter.WriteLine(
            "session_id,player_id,build_version,timestamp_utc," +
            "session_weapon,stage_number,profile_version,playstyle,dominant_weapon,boss_weapon," +
            "offensive_count,defensive_count,dash_count,riposte_count,concussive_count," +
            "sword_use,bow_use,melee_count,range_count,total_minions,attack_tokens," +
            "stat_multiplier,max_hp,attack," +
            "sword_w_slash,sword_w_whirl,sword_w_charged,sword_w_riposte," +
            "bow_w_quick,bow_w_spread,bow_w_fulldraw,bow_w_fullcharge,bow_w_concussive," +
            "time_in_stage,time_to_kill_boss,avg_time_to_kill_minion," +
            "damage_dealt,damage_taken,deaths_in_stage," +
            "apm,ipm,distance_traveled,average_speed," +
            "max_combo,ranged_shots,ranged_hits,ranged_accuracy," +
            "avg_fps,min_fps,fps_drops,pause_count,avg_reaction_time");
        _stageWriter.Flush();

        _skillWriter.WriteLine("session_id,stage_number,weapon,skill_name,casts,hits,success_rate,damage_dealt");
        _skillWriter.Flush();

        Debug.Log($"[Telemetry] session={_sessionId} player={_playerId} build={_buildVersion}");
        Debug.Log($"[Telemetry] Stage log: {_stagePath}");
    }

    private void DetectPreviousCrash()
    {
        // Jika sesi sebelumnya tidak menutup writer dengan bersih (OnApplicationQuit tidak dipanggil),
        // flag dirty masih bernilai 1 -> indikasikan crash/force-close.
        if (PlayerPrefs.GetInt(DirtySessionPrefsKey, 0) == 1)
        {
            LogEvent("crash_detected", "previous_session_did_not_close_cleanly", 0f);
        }

        PlayerPrefs.SetInt(DirtySessionPrefsKey, 1);
        PlayerPrefs.Save();
    }

    void Update()
    {
        if (!_stageActive)
            return;

        TrackInputForApm();
        TrackFps();
        TrackMovementAndHeatmap();
    }

    // =====================================================================
    // SELF-TRACK: APM / IPM
    // =====================================================================

    private void TrackInputForApm()
    {
        // IPM: setiap penekanan tombol/mouse dihitung sebagai 1 input mentah.
        if (Input.anyKeyDown)
            _stageInputs++;
    }

    // =====================================================================
    // SELF-TRACK: FPS
    // =====================================================================

    private void TrackFps()
    {
        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f)
            return;

        float fps = 1f / dt;
        _stageFrameCount++;
        _stageFpsSum += fps;
        if (fps < _stageMinFps) _stageMinFps = fps;
        if (fps < fpsDropThreshold) _stageFpsDrops++;
    }

    // =====================================================================
    // SELF-TRACK: MOVEMENT + HEATMAP
    // =====================================================================

    private void TrackMovementAndHeatmap()
    {
        if (Time.unscaledTime < _lastSampleTime + movementSampleInterval)
            return;

        _lastSampleTime = Time.unscaledTime;

        Transform player = ResolvePlayerTransform();
        if (player == null)
            return;

        Vector3 pos = player.position;

        if (_hasLastSamplePos)
            _stageDistance += Vector2.Distance(pos, _lastSamplePos);

        _lastSamplePos = pos;
        _hasLastSamplePos = true;

        // Heatmap occupancy (akumulatif sepanjang sesi).
        int cx = Mathf.FloorToInt(pos.x / Mathf.Max(0.01f, heatmapCellSize));
        int cy = Mathf.FloorToInt(pos.y / Mathf.Max(0.01f, heatmapCellSize));
        long key = ((long)cx << 32) ^ (uint)cy;
        _heatmap.TryGetValue(key, out int count);
        _heatmap[key] = count + 1;
    }

    private Transform ResolvePlayerTransform()
    {
        if (_playerTransform != null && _playerTransform.gameObject.activeInHierarchy)
            return _playerTransform;

        try
        {
            GameObject tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null && tagged.activeInHierarchy)
            {
                _playerTransform = tagged.transform;
                return _playerTransform;
            }
        }
        catch (UnityException) { /* tag belum dibuat */ }

        Player p = FindObjectOfType<Player>();
        if (p != null)
            _playerTransform = p.transform;

        return _playerTransform;
    }

    // =====================================================================
    // API: SESI & STAGE LIFECYCLE
    // =====================================================================

    public void SetSessionWeapon(string weapon)
    {
        if (!string.IsNullOrEmpty(weapon))
            _sessionWeapon = weapon;
    }

    /// <summary>Dipanggil StageManager.BeginStage. Mereset akumulator per-stage & memulai timer.</summary>
    public void BeginStage(int stageNumber)
    {
        _currentStage = stageNumber;
        _stageActive = true;
        _stageStartRealtime = Time.realtimeSinceStartup;

        _stageDamageDealt = 0f;
        _stageDamageTaken = 0f;
        _stageDeaths = 0;
        _stageMinionKills = 0;
        _stageMinionKillTimeSum = 0f;
        _stageBossKillTime = -1f;
        _stageInputs = 0;
        _stageActions = 0;
        _stageDistance = 0f;
        _stageRangedShots = 0;
        _stageRangedHits = 0;
        _stageMaxCombo = 0;
        _stageCurrentCombo = 0;
        _stageLastComboTime = -999f;

        _stageFrameCount = 0;
        _stageFpsSum = 0f;
        _stageMinFps = float.MaxValue;
        _stageFpsDrops = 0;
        _stagePauseCount = 0;

        _hasProfileSnapshot = false;
        _stageSummaryWritten = false;
        _stageSkillStats.Clear();
        _reactionTimes.Clear();

        _hasLastSamplePos = false;

        LogEvent("stage_begin", "stage", stageNumber);
    }

    /// <summary>
    /// Dipanggil DataTracker.FinalizeStageData TEPAT SEBELUM ResetData(), agar count per-stage
    /// (yang akan di-reset) tersimpan untuk baris stage_summary yang ditulis saat boss kalah.
    /// </summary>
    public void CaptureStageProfileSnapshot(
        int offensive, int defensive, int dash, int riposte, int concussive, int swordUse, int bowUse)
    {
        // Hanya ambil snapshot pertama per stage (pra-boss). Finalize pasca-boss biasanya bernilai 0.
        if (_hasProfileSnapshot && offensive + defensive + swordUse + bowUse == 0)
            return;

        _snapOffensive = offensive;
        _snapDefensive = defensive;
        _snapDash = dash;
        _snapRiposte = riposte;
        _snapConcussive = concussive;
        _snapSwordUse = swordUse;
        _snapBowUse = bowUse;
        _hasProfileSnapshot = true;
    }

    // =====================================================================
    // API: DAMAGE (dipanggil CharacterBase.TakeDamage)
    // =====================================================================

    /// <summary>
    /// Mencatat damage final yang terjadi. Penentuan dealt/taken otomatis: bila korban adalah
    /// <see cref="Player"/> -> damage_taken; selain itu dianggap musuh terkena player -> damage_dealt.
    /// </summary>
    public void RecordDamage(CharacterBase victim, GameObject attacker, float amount)
    {
        if (amount <= 0f)
            return;

        bool victimIsPlayer = victim is Player;

        if (victimIsPlayer)
        {
            _stageDamageTaken += amount;
            // Player kena pukul -> putus combo.
            _stageCurrentCombo = 0;
        }
        else
        {
            _stageDamageDealt += amount;
            AttributeDamageToActiveSkill(amount);
            RegisterComboHit();
        }
    }

    private void RegisterComboHit()
    {
        float now = Time.realtimeSinceStartup;

        if (now - _stageLastComboTime > comboWindow)
            _stageCurrentCombo = 0;

        _stageCurrentCombo++;
        _stageLastComboTime = now;

        if (_stageCurrentCombo > _stageMaxCombo)
            _stageMaxCombo = _stageCurrentCombo;
    }

    private void AttributeDamageToActiveSkill(float amount)
    {
        if (_activeSkillKey == null)
            return;

        if (Time.realtimeSinceStartup - _activeSkillTime > skillHitAttributionWindow)
            return;

        SkillStat stat = GetOrCreateSkillStat(_activeSkillKey);
        stat.hits++;
        stat.damage += amount;

        if (_activeSkillWeapon == WeaponType.Bow)
            _stageRangedHits++;

        LogEvent("skill_hit", _activeSkillKey, amount);
    }

    // =====================================================================
    // API: SKILL CAST (dipanggil DataTracker.RecordSwordSkill/RecordBowSkill)
    // =====================================================================

    public void RecordSkillCast(WeaponType weapon, string skillName, PlayerActionType actionType)
    {
        if (string.IsNullOrEmpty(skillName))
            return;

        string key = $"{weapon}/{skillName}";
        SkillStat stat = GetOrCreateSkillStat(key);
        stat.casts++;

        _activeSkillKey = key;
        _activeSkillWeapon = weapon;
        _activeSkillTime = Time.realtimeSinceStartup;

        // APM: cast skill termasuk "action" bermakna.
        _stageActions++;

        if (weapon == WeaponType.Bow)
            _stageRangedShots++;

        LogEvent("skill_cast", key, 0f);
    }

    private SkillStat GetOrCreateSkillStat(string key)
    {
        if (!_stageSkillStats.TryGetValue(key, out SkillStat stat))
        {
            stat = new SkillStat();
            _stageSkillStats[key] = stat;
        }
        return stat;
    }

    // =====================================================================
    // API: KEMATIAN & KILL
    // =====================================================================

    public void RecordPlayerDeath()
    {
        _stageDeaths++;

        float now = Time.realtimeSinceStartup;
        float timeBetweenDeaths = _lastDeathRealtime >= 0f ? now - _lastDeathRealtime : -1f;
        _lastDeathRealtime = now;

        LogEvent("player_death", "stage_time", now - _stageStartRealtime);
        LogEvent("time_between_deaths", "seconds", timeBetweenDeaths);
    }

    /// <summary>Dipanggil EnemyDeathHandler. isBoss ditentukan pemanggil (fallback via stage state).</summary>
    public void RecordEnemyKilled(bool isBoss)
    {
        float t = Time.realtimeSinceStartup - _stageStartRealtime;

        if (isBoss)
        {
            _stageBossKillTime = t;
            LogEvent("boss_kill", "time_to_kill", t);
        }
        else
        {
            _stageMinionKills++;
            _stageMinionKillTimeSum += t;
            LogEvent("enemy_kill", "time_to_kill", t);
        }
    }

    // =====================================================================
    // API: REACTION TIME
    // =====================================================================

    /// <summary>Tandai munculnya stimulus musuh (mis. telegraf serangan).</summary>
    public void RecordReactionStimulus()
    {
        _lastStimulusTime = Time.realtimeSinceStartup;
    }

    /// <summary>Tandai aksi defensif player (dash/parry). Hitung selisih dari stimulus terakhir.</summary>
    public void RecordReactionResponse()
    {
        _stageActions++;

        if (_lastStimulusTime < 0f)
            return;

        float rt = Time.realtimeSinceStartup - _lastStimulusTime;
        _lastStimulusTime = -999f;

        if (rt < 0f || rt > reactionWindow)
            return;

        _reactionTimes.Add(rt);
        LogEvent("reaction", "seconds", rt);
    }

    /// <summary>Aksi bermakna umum (mis. dash) untuk APM, tanpa kaitan reaksi.</summary>
    public void RecordAction()
    {
        _stageActions++;
    }

    // =====================================================================
    // PENULISAN BARIS STAGE_SUMMARY
    // =====================================================================

    /// <summary>
    /// Ditulis StageManager saat boss kalah (sebelum DDA reset). Menggabungkan snapshot profil,
    /// nilai live dari DDA/Stage, dan akumulator telemetry per-stage.
    /// </summary>
    public void LogStageSummary(
        int stage, int profileVersion, string playstyle, string dominantWeapon, string bossWeapon,
        int melee, int range, int totalMinions, int attackTokens,
        float statMul, float maxHp, float attack,
        float[] swordWeights, float[] bowWeights)
    {
        if (_stageWriter == null)
            return;

        // Cegah baris ganda dalam stage yang sama; flag direset tiap BeginStage (aman untuk Play Again).
        if (_stageSummaryWritten)
            return;
        _stageSummaryWritten = true;

        float SW(int i) => (swordWeights != null && i < swordWeights.Length) ? swordWeights[i] : 0f;
        float BW(int i) => (bowWeights != null && i < bowWeights.Length) ? bowWeights[i] : 0f;

        float timeInStage = Time.realtimeSinceStartup - _stageStartRealtime;
        float activeMinutes = Mathf.Max(1e-4f, timeInStage / 60f);
        float apm = _stageActions / activeMinutes;
        float ipm = _stageInputs / activeMinutes;
        float avgSpeed = timeInStage > 0f ? _stageDistance / timeInStage : 0f;
        float avgMinionKill = _stageMinionKills > 0 ? _stageMinionKillTimeSum / _stageMinionKills : 0f;
        float rangedAcc = _stageRangedShots > 0 ? (_stageRangedHits / (float)_stageRangedShots) * 100f : 0f;
        float avgFps = _stageFrameCount > 0 ? _stageFpsSum / _stageFrameCount : 0f;
        float minFps = _stageMinFps == float.MaxValue ? 0f : _stageMinFps;
        float avgReaction = ComputeAvgReaction();

        var sb = new StringBuilder();
        sb.Append(_sessionId).Append(',');
        sb.Append(_playerId).Append(',');
        sb.Append(_buildVersion).Append(',');
        sb.Append(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)).Append(',');

        sb.Append(_sessionWeapon).Append(',');
        sb.Append(stage).Append(',');
        sb.Append(profileVersion).Append(',');
        sb.Append(playstyle).Append(',');
        sb.Append(dominantWeapon).Append(',');
        sb.Append(bossWeapon).Append(',');

        sb.Append(_snapOffensive).Append(',');
        sb.Append(_snapDefensive).Append(',');
        sb.Append(_snapDash).Append(',');
        sb.Append(_snapRiposte).Append(',');
        sb.Append(_snapConcussive).Append(',');
        sb.Append(_snapSwordUse).Append(',');
        sb.Append(_snapBowUse).Append(',');

        sb.Append(melee).Append(',');
        sb.Append(range).Append(',');
        sb.Append(totalMinions).Append(',');
        sb.Append(attackTokens).Append(',');

        sb.Append(F(statMul)).Append(',');
        sb.Append(F(maxHp)).Append(',');
        sb.Append(F(attack)).Append(',');

        sb.Append(F(SW(0))).Append(',').Append(F(SW(1))).Append(',').Append(F(SW(2))).Append(',').Append(F(SW(3))).Append(',');
        sb.Append(F(BW(0))).Append(',').Append(F(BW(1))).Append(',').Append(F(BW(2))).Append(',').Append(F(BW(3))).Append(',').Append(F(BW(4))).Append(',');

        sb.Append(F(timeInStage)).Append(',');
        sb.Append(F(_stageBossKillTime)).Append(',');
        sb.Append(F(avgMinionKill)).Append(',');

        sb.Append(F(_stageDamageDealt)).Append(',');
        sb.Append(F(_stageDamageTaken)).Append(',');
        sb.Append(_stageDeaths).Append(',');

        sb.Append(F(apm)).Append(',');
        sb.Append(F(ipm)).Append(',');
        sb.Append(F(_stageDistance)).Append(',');
        sb.Append(F(avgSpeed)).Append(',');

        sb.Append(_stageMaxCombo).Append(',');
        sb.Append(_stageRangedShots).Append(',');
        sb.Append(_stageRangedHits).Append(',');
        sb.Append(F(rangedAcc)).Append(',');

        sb.Append(F(avgFps)).Append(',');
        sb.Append(F(minFps)).Append(',');
        sb.Append(_stageFpsDrops).Append(',');
        sb.Append(_stagePauseCount).Append(',');
        sb.Append(F(avgReaction));

        _stageWriter.WriteLine(sb.ToString());
        _stageWriter.Flush();

        WriteSkillRows(stage);

        // Flush cast musuh yang masih tertunda agar tidak hilang saat stage berakhir.
        if (_pendingEnemyCast.active)
        {
            SetLastEnemySkillCastHit(_pendingEnemyCast.isHit);
        }

        LogEvent("stage_summary_written", "stage", stage);
    }

    private float ComputeAvgReaction()
    {
        if (_reactionTimes.Count == 0)
            return 0f;

        float sum = 0f;
        foreach (float r in _reactionTimes)
            sum += r;

        return sum / _reactionTimes.Count;
    }

    private void WriteSkillRows(int stage)
    {
        if (_skillWriter == null)
            return;

        foreach (var kvp in _stageSkillStats)
        {
            string key = kvp.Key;
            SkillStat stat = kvp.Value;

            string weapon = key;
            string skillName = key;
            int slash = key.IndexOf('/');
            if (slash >= 0)
            {
                weapon = key.Substring(0, slash);
                skillName = key.Substring(slash + 1);
            }

            float successRate = stat.casts > 0 ? (stat.hits / (float)stat.casts) * 100f : 0f;

            _skillWriter.WriteLine(
                $"{_sessionId},{stage},{weapon},{skillName},{stat.casts},{stat.hits}," +
                $"{F(successRate)},{F(stat.damage)}");
        }

        _skillWriter.Flush();
    }

    // =====================================================================
    // EVENT LOG
    // =====================================================================

    private void LogEvent(string eventType, string detail, float value)
    {
        if (_eventWriter == null)
            return;

        string ts = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        float sessionTime = Time.realtimeSinceStartup - _sessionStartRealtime;

        // Kolom tambahan dikosongkan untuk event non-cast-musuh agar format CSV konsisten.
        _eventWriter.WriteLine(
            $"{ts},{F(sessionTime)},{_sessionId},{_currentStage},{eventType},{detail},{F(value)},,,,,,,,");
        _eventWriter.Flush();
    }

    /// <summary>
    /// Baris event khusus cast skill musuh. Menuliskan timestamp, koordinat cast & target,
    /// status hit, profil player saat itu, snapshot bobot DDA, dan nama node BT yang aktif.
    /// </summary>
    private void LogEnemySkillCast(
        string skillName,
        Vector2 casterPos,
        Vector2 targetPos,
        bool isHit,
        string playerProfile,
        string skillDistribution,
        string activeBtNode)
    {
        if (_eventWriter == null)
            return;

        string ts = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        float sessionTime = Time.realtimeSinceStartup - _sessionStartRealtime;

        _eventWriter.WriteLine(
            $"{ts},{F(sessionTime)},{_sessionId},{_currentStage},enemy_skill_cast,{EscapeCsv(skillName)},{F(0f)}," +
            $"{EscapeCsv(skillName)}," +
            $"{F(casterPos.x)},{F(casterPos.y)}," +
            $"{F(targetPos.x)},{F(targetPos.y)}," +
            $"{(isHit ? "true" : "false")}," +
            $"{EscapeCsv(playerProfile ?? string.Empty)}," +
            $"{EscapeCsv(skillDistribution ?? string.Empty)}," +
            $"{EscapeCsv(activeBtNode ?? string.Empty)}");
        _eventWriter.Flush();
    }

    // Escapes CSV field (jika mengandung koma, kutip, atau newline).
    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        bool mustQuote = value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
        if (!mustQuote)
            return value;

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    // =====================================================================
    // API: ENEMY SKILL CAST
    // =====================================================================

    /// <summary>
    /// Dipanggil di awal Trigger() skill musuh. Hanya menyimpan snapshot ke memori;
    /// baris CSV lengkap baru ditulis saat <see cref="SetLastEnemySkillCastHit"/>
    /// dipanggil (atau stage berakhir dengan cast belum terekam).
    /// </summary>
    public void RecordEnemySkillCast(
        string skillName,
        Vector2 casterPos,
        Vector2 targetPos,
        bool isHit,
        string playerProfile,
        string skillDistribution,
        string activeBtNode)
    {
        if (string.IsNullOrEmpty(skillName))
            return;

        // Flush cast sebelumnya (kalau ada) yang tidak pernah menerima hasil hit.
        // Ini mencegah satu baris tertahan indefinitely bila skill dibatalkan/disable.
        if (_pendingEnemyCast.active)
        {
            LogEnemySkillCast(
                _pendingEnemyCast.skillName,
                _pendingEnemyCast.casterPos,
                _pendingEnemyCast.targetPos,
                _pendingEnemyCast.isHit,
                _pendingEnemyCast.playerProfile,
                _pendingEnemyCast.skillDistribution,
                _pendingEnemyCast.activeBtNode);
        }

        _pendingEnemyCast = new PendingEnemyCast
        {
            active = true,
            skillName = skillName,
            casterPos = casterPos,
            targetPos = targetPos,
            isHit = isHit,
            playerProfile = playerProfile,
            skillDistribution = skillDistribution,
            activeBtNode = activeBtNode,
            castRealtime = Time.realtimeSinceStartup
        };
    }

    /// <summary>
    /// Tandai apakah cast musuh terakhir mengenai target. Setelah dipanggil,
    /// baris CSV lengkap langsung ditulis dan slot cast dikosongkan.
    /// </summary>
    public void SetLastEnemySkillCastHit(bool isHit)
    {
        if (!_pendingEnemyCast.active)
            return;

        _pendingEnemyCast.isHit = isHit;
        LogEnemySkillCast(
            _pendingEnemyCast.skillName,
            _pendingEnemyCast.casterPos,
            _pendingEnemyCast.targetPos,
            _pendingEnemyCast.isHit,
            _pendingEnemyCast.playerProfile,
            _pendingEnemyCast.skillDistribution,
            _pendingEnemyCast.activeBtNode);

        _pendingEnemyCast = default;
    }

    /// <summary>Snapshot profil player saat ini (mis. "OffensiveDominant").</summary>
    public static string GetCurrentPlayerPlaystyle()
    {
        DDAController dda = DDAController.Instance;
        if (dda == null)
            return string.Empty;
        return dda.currentPlayerPlaystyle.ToString();
    }

    /// <summary>
    /// Snapshot distribusi skill DDA yang diberikan ke musuh, diserialisasi ke string.
    /// Untuk Bow 5-slot: [Q=…,S=…,F=…,FC=…,C=…]. Untuk Sword 4-slot: [Slash,Whirl,Charged,Rip].
    /// </summary>
    public static string FormatSkillDistribution(bool isBow)
    {
        DDAController dda = DDAController.Instance;
        if (dda == null)
            return string.Empty;

        if (isBow)
        {
            float[] w = dda.GetCurrentBowSkillWeightsCopy();
            if (w == null || w.Length == 0)
                return string.Empty;
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "[Q={0:F1},S={1:F1},F={2:F1},FC={3:F1},C={4:F1}]",
                w.Length > 0 ? w[0] : 0f,
                w.Length > 1 ? w[1] : 0f,
                w.Length > 2 ? w[2] : 0f,
                w.Length > 3 ? w[3] : 0f,
                w.Length > 4 ? w[4] : 0f);
        }
        else
        {
            float[] w = dda.GetCurrentSwordSkillWeightsCopy();
            if (w == null || w.Length == 0)
                return string.Empty;
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "[Slash={0:F1},Whirl={1:F1},Charged={2:F1},Rip={3:F1}]",
                w.Length > 0 ? w[0] : 0f,
                w.Length > 1 ? w[1] : 0f,
                w.Length > 2 ? w[2] : 0f,
                w.Length > 3 ? w[3] : 0f);
        }
    }

    /// <summary>Nama node BT yang sedang aktif (snapshot dari <see cref="NotifyBtNodeEvaluated"/>).</summary>
    public static string GetLastActiveBtNode()
    {
        return s_lastBtNodeName ?? string.Empty;
    }

    /// <summary>
    /// Helper yang dipanggil oleh skill musuh di Trigger() untuk mengisi semua field
    /// sekaligus dari konteks yang tersedia (NodeManager + DDAController + BT node).
    /// </summary>
    public void RecordEnemySkillCastFromContext(
        NodeManager caster,
        string skillName,
        bool isBow)
    {
        if (caster == null)
            return;

        Vector2 casterPos = caster.transform != null
            ? (Vector2)caster.transform.position
            : Vector2.zero;
        Vector2 targetPos = caster.playerTransform != null
            ? (Vector2)caster.playerTransform.position
            : Vector2.zero;

        RecordEnemySkillCast(
            skillName,
            casterPos,
            targetPos,
            false, // default: belum hit; diupdate via SetLastEnemySkillCastHit
            GetCurrentPlayerPlaystyle(),
            FormatSkillDistribution(isBow),
            GetLastActiveBtNode());
    }

    // =====================================================================
    // PAUSE / FOCUS / QUIT / CRASH
    // =====================================================================

    void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            _stagePauseCount++;
            LogEvent("pause", "paused", 1f);
        }
        else
        {
            LogEvent("resume", "resumed", 0f);
        }
    }

    void OnApplicationFocus(bool focus)
    {
        if (!focus)
            LogEvent("focus_lost", "lost", 1f);
    }

    void OnApplicationQuit()
    {
        LogEvent("quit", "clean", 0f);
        WriteHeatmap();

        // Tandai sesi ditutup bersih (bukan crash).
        PlayerPrefs.SetInt(DirtySessionPrefsKey, 0);
        PlayerPrefs.Save();

        CloseWriters();
    }

    void OnDisable() { CloseWriters(); }

    private void WriteHeatmap()
    {
        if (_heatmap.Count == 0 || string.IsNullOrEmpty(_heatmapPath))
            return;

        try
        {
            using (var w = new StreamWriter(_heatmapPath, false, Encoding.UTF8))
            {
                w.WriteLine("cell_x,cell_y,occupancy_samples,cell_size");
                foreach (var kvp in _heatmap)
                {
                    int cx = (int)(kvp.Key >> 32);
                    int cy = (int)(uint)kvp.Key;
                    w.WriteLine($"{cx},{cy},{kvp.Value},{F(heatmapCellSize)}");
                }
            }
            Debug.Log($"[Telemetry] Heatmap ditulis: {_heatmapPath} ({_heatmap.Count} sel).");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Telemetry] Gagal menulis heatmap: {ex.Message}");
        }
    }

    private void CloseWriters()
    {
        _eventWriter?.Flush(); _eventWriter?.Close(); _eventWriter = null;
        _stageWriter?.Flush(); _stageWriter?.Close(); _stageWriter = null;
        _skillWriter?.Flush(); _skillWriter?.Close(); _skillWriter = null;
    }

    // Format angka pakai titik desimal (bukan koma) supaya CSV konsisten.
    private string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}
