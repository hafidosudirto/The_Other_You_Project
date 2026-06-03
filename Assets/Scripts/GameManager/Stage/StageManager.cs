using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// ORCHESTRATOR (Context dari Finite State Machine).
///
/// Setelah refactor, StageManager hanya bertugas:
/// <list type="bullet">
/// <item>Menyimpan seluruh referensi GLOBAL (prefab player/minion/boss, titik spawn, formula progresi, dll).</item>
/// <item>Menampung dua modul KOMPOSISI: <see cref="StageStatManager"/> (stat &amp; attack token)
/// dan <see cref="StageVisualDebug"/> (OnGUI/Gizmos/teks debug mengambang).</item>
/// <item>Mengatur transisi antar FASE (<see cref="StagePhaseBase"/>) lewat <see cref="ChangeState"/>.</item>
/// <item>Menyediakan primitif yang dipakai fase (spawn enemy, deteksi senjata, reset DDA, dll).</item>
/// <item>FACADE API token publik yang dipanggil script musuh/EnemyDeathHandler (didelegasikan ke Stats).</item>
/// </list>
///
/// FIX BUG GAME OVER: <see cref="Awake"/> membersihkan SELURUH state runtime (termasuk membangun
/// ulang instance fase sehingga runtime boss bersih) dan memanggil <c>ResetAll()</c> pada kedua
/// modul komposisi setiap kali scene dimuat ulang. Dengan begitu tidak ada data sisa run sebelumnya
/// yang membuat game macet/tidak bisa dimainkan ulang setelah Game Over.
/// </summary>
public class StageManager : MonoBehaviour
{
    // =====================================================================
    // FRESH RUN (STATIC) — sengaja TIDAK direset di Awake agar bertahan reload scene.
    // =====================================================================

    private const string FreshRunRequestPrefsKey = "THE_OTHER_YOU_STAGE_MANAGER_FRESH_RUN_REQUEST";
    private static bool freshRunRequestedInMemory;

    public static void RequestFreshRunOnNextGameplayLoad()
    {
        freshRunRequestedInMemory = true;
        PlayerPrefs.SetInt(FreshRunRequestPrefsKey, 1);
        PlayerPrefs.Save();

        Debug.Log("[STAGE MANAGER] Fresh run diminta. StageManager berikutnya akan reset dari stage awal.");
    }

    private static bool ConsumeFreshRunRequest()
    {
        bool requested =
            freshRunRequestedInMemory ||
            PlayerPrefs.GetInt(FreshRunRequestPrefsKey, 0) == 1;

        freshRunRequestedInMemory = false;

        if (PlayerPrefs.HasKey(FreshRunRequestPrefsKey))
        {
            PlayerPrefs.DeleteKey(FreshRunRequestPrefsKey);
            PlayerPrefs.Save();
        }

        return requested;
    }

    // =====================================================================
    // ENUM STATE
    // =====================================================================

    public enum StageState
    {
        SpawningMinions,
        FightingMinions,
        FightingBoss,
        StageCleared
    }

    [HideInInspector] public StageState currentState;

    // =====================================================================
    // KONFIGURASI GLOBAL (SERIALIZED)
    // =====================================================================

    [Header("Player")]
    public Transform playerTransform;
    public Transform nextStagePlayerStartPoint;
    public float fallbackPlayerStartX = -8f;
    public float rightTransitionX = 18f;

    [Header("Player Prefabs")]
    public GameObject playerSwordPrefab;
    public GameObject playerBowPrefab;
    public WeaponType selectedPlayerWeapon = WeaponType.Sword;
    public Transform playerSpawnPoint;

    [Header("Minion Prefabs")]
    public GameObject meleeMinionPrefab;
    public GameObject rangeMinionPrefab;

    [Header("Minion Spawn Points")]
    public List<Transform> minionSpawnPoints;

    [Header("Boss")]
    public GameObject bossPrefabSword;
    public GameObject bossPrefabBow;
    public Transform bossSpawnPoint;

    [Header("Boss UI")]
    [SerializeField] private BossHPBarUI bossHPBarUI;
    [SerializeField] private bool autoFindBossHPBarUI = true;

    [Header("Boss Defeat Progression Fix")]
    [Tooltip("Jika aktif, collider boss dimatikan setelah boss kalah agar player tidak tertahan mayat/collider boss ketika berjalan ke area next stage.")]
    [SerializeField] private bool disableBossCollidersOnDefeat = true;

    [Tooltip("Jika aktif, tag boss diubah menjadi Untagged setelah kalah agar tidak lagi dihitung sebagai Enemy aktif.")]
    [SerializeField] private bool untagBossOnDefeat = true;

    [Header("Progression Formula")]
    public int startingStageNumber = 0;
    public int baseSpawnToken = 3;
    public int spawnTokenIncreasePerStage = 1;
    public float statAmplifyPerStage = 0.1f;

    [Header("Minion Variant Ratio")]
    [Range(0f, 1f)]
    public float dominantVariantRatio = 0.6f;

    [Header("DDA Snapshot Timing")]
    public bool finalizeDataBeforeBoss = true;
    public bool finalizeDataAfterBoss = true;

    [Header("DDA Reset Timing")]
    public bool resetDDAAndDataTrackerOnStageCleared = true;

    [Tooltip("Wajib true jika data adaptasi ingin dipakai untuk stage berikutnya. Jika false, DDAController akan ikut direset dan musuh kembali tidak adaptif.")]
    [SerializeField] private bool preserveDDAProfileForNextStage = true;

    [Header("Fresh Run Reset")]
    [Tooltip("Jika true, permintaan Play Again/Game Over akan memaksa StageManager memulai ulang dari startingStageNumber.")]
    [SerializeField] private bool consumeFreshRunRequestOnStart = true;

    [Tooltip("Jika true, DataTracker dan DDAController ikut direset saat Play Again agar run baru tidak membawa data adaptasi run sebelumnya.")]
    [SerializeField] private bool resetDDAProfileOnFreshRun = true;

    [Tooltip("Jika true, player dipindahkan lagi ke playerSpawnPoint/fallbackPlayerStartX ketika run baru dimulai.")]
    [SerializeField] private bool movePlayerToSpawnOnFreshRun = true;

    [Tooltip("Jika true, HP player dikembalikan ke maxHP ketika run baru dimulai.")]
    [SerializeField] private bool resetPlayerHealthOnFreshRun = true;

    [Tooltip("Jika true, enemy/proyektil lama yang masih hidup karena DontDestroyOnLoad akan dihapus saat run baru dimulai.")]
    [SerializeField] private bool destroyRuntimeCombatObjectsOnFreshRun = true;

    [SerializeField]
    private string[] freshRunDestroyTags =
    {
        "Enemy",
        "Spawn",
        "Projectile"
    };

    [Header("Prefab Switch Integration")]
    [SerializeField] private bool waitUntilPlayerWeaponSelectedBeforeStart = true;
    [SerializeField] private bool preferPrefabSwitchActivePlayer = true;

    [Header("Stage Transition")]
    [SerializeField] private CanvasGroup blackScreenCanvasGroup;
    [SerializeField] private float blackScreenFadeDuration = 0.5f;
    [SerializeField] private float blackScreenHoldDuration = 0.5f;

    [Header("Stage Start Delay")]
    [SerializeField] private float minionSpawnDelayOnStageStart = 2f;

    [Header("Player Regen on Next Stage")]
    public bool regenPlayerOnNextStage = true;
    [SerializeField] private float nextStagePlayerFlatHPRegen = 25f;
    [Range(0f, 1f)]
    [SerializeField] private float nextStagePlayerPercentHPRegen = 0f;
    [SerializeField] private bool clampPlayerRegenToMaxHP = true;

    [Header("Composition Modules")]
    [Tooltip("Modul stat & attack token. Otomatis ditambahkan saat runtime bila kosong.")]
    [SerializeField] private StageStatManager statManager;
    [Tooltip("Modul visual debug (OnGUI/Gizmos). Otomatis ditambahkan saat runtime bila kosong.")]
    [SerializeField] private StageVisualDebug visualDebug;

    // =====================================================================
    // STATE RUNTIME (PRIVATE) + EKSPOS UNTUK FASE/MODUL
    // =====================================================================

    private bool stageHasStarted;
    private int currentStage;
    private bool isChangingStage = false;

    // Perlindungan Grace Period agar stage tidak tereksekusi oleh data kadaluwarsa saat frame pertama.
    private const float GRACE_PERIOD_DURATION = 1.5f;

    // FIX BLACKSCREEN PLAY AGAIN: batas waktu (detik) menunggu pemilihan senjata player
    // sebelum stage dimulai. Bila terlampaui (mis. setelah Game Over state pemilihan senjata
    // tidak pernah ter-set ulang), stage tetap dipaksa mulai dengan player fallback agar
    // tidak macet di layar hitam yang hanya menyisakan UI HP.
    private const float PLAYER_WEAPON_SELECTION_TIMEOUT = 5f;

    private CharacterBase playerCharacterCache;
    private float previousPlayerHP = -1f;

    // FSM
    private readonly Dictionary<StageState, StagePhaseBase> phases = new Dictionary<StageState, StagePhaseBase>();
    private StagePhaseBase currentPhase;

    // ---- Properti yang dikonsumsi modul komposisi & fase ----

    /// <summary>Modul komposisi: stat &amp; attack token.</summary>
    public StageStatManager Stats => statManager;

    /// <summary>Modul komposisi: visual debug.</summary>
    public StageVisualDebug Visual => visualDebug;

    /// <summary>State FSM aktif saat ini.</summary>
    public StageState CurrentState => currentState;

    /// <summary>Nomor stage internal mentah (bisa negatif relatif startingStageNumber).</summary>
    public int CurrentStageInternal => currentStage;

    /// <summary>Waktu (Time.time) ketika state aktif terakhir kali dimasuki. Dipakai grace period.</summary>
    public float StateEnterTime { get; set; }

    /// <summary>Jumlah musuh aktif menurut hitungan event (bukan scan fisik).</summary>
    public int ActiveEnemiesCount { get; set; }

    /// <summary>True selama proses transisi minion -&gt; boss berlangsung.</summary>
    public bool IsTransitioningToBoss { get; set; }

    /// <summary>True bila konfigurasi boss gagal (prefab null), agar fallback tidak loop.</summary>
    public bool BossSpawnConfigurationFailed { get; set; }

    /// <summary>Apakah grace period state saat ini sudah lewat.</summary>
    public bool IsPastGracePeriod() => Time.time - StateEnterTime >= GRACE_PERIOD_DURATION;

    // ---- Akses konfigurasi untuk fase boss ----
    public bool UntagBossOnDefeat => untagBossOnDefeat;
    public bool DisableBossCollidersOnDefeat => disableBossCollidersOnDefeat;
    public float MinionSpawnDelayOnStageStart => minionSpawnDelayOnStageStart;

    // ---- Akses instance fase (dipakai antar-fase) ----
    public SpawningMinionsPhase SpawningPhase { get; private set; }
    public FightingMinionsPhase MinionsPhase { get; private set; }
    public FightingBossPhase BossPhase { get; private set; }
    public StageClearedPhase ClearedPhase { get; private set; }

    // =====================================================================
    // LIFECYCLE
    // =====================================================================

    private void Awake()
    {
        // FIX BUG GAME OVER:
        // Setiap kali scene gameplay dimuat (termasuk setelah Game Over), Awake membersihkan
        // SEMUA state runtime dan memori modul komposisi. Instance fase dibangun ulang dari nol
        // sehingga runtime boss (currentBossObject/bossDefeatHandled) ikut bersih. Tanpa ini,
        // data sisa run sebelumnya bisa membuat game macet di state tertentu.
        EnsureCompositionModules();
        InitializePhases();
        HardResetRuntimeState();

        statManager.ResetAll();
        visualDebug.ResetAll();

        // FIX BLACKSCREEN PLAY AGAIN: bersihkan overlay hitam segera saat scene gameplay dimuat.
        // Bila run sebelumnya (atau sekuens Game Over) meninggalkan overlay dalam keadaan hitam
        // penuh, tanpa ini layar bisa tetap hitam sampai reset di InitStageRoutine berjalan
        // beberapa frame kemudian — atau selamanya bila proses start terhambat.
        SetBlackScreenInstant(0f, false);

        // CATATAN: freshRunRequestedInMemory (static) sengaja TIDAK direset di sini agar
        // permintaan Play Again dapat bertahan melewati reload scene dan tetap men-trigger
        // fresh run pada Start().
    }

    private void OnEnable()
    {
        PlayerPrefabSwitchManager.OnActiveWeaponChanged += HandleActiveWeaponChanged;
    }

    private void OnDisable()
    {
        PlayerPrefabSwitchManager.OnActiveWeaponChanged -= HandleActiveWeaponChanged;
    }

    private void Start()
    {
        // FIX FRESH RUN: hentikan semua coroutine lama agar tidak ada NextStageTransition
        // atau SpawnMinionWave dari run sebelumnya yang masih menggantung (defensif).
        StopAllCoroutines();

        // FIX FRESH RUN: paksa flag stageHasStarted = false sejak awal agar Update() tidak
        // menjalankan logika fase sebelum stage benar-benar dimulai.
        stageHasStarted = false;
        currentState = StageState.SpawningMinions;

        bool freshRunRequested = consumeFreshRunRequestOnStart && ConsumeFreshRunRequest();
        StartCoroutine(InitStageRoutine(freshRunRequested));
    }

    public void ForceRestartFromFirstStage()
    {
        StopAllCoroutines();

        RequestFreshRunOnNextGameplayLoad();
        bool freshRunRequested = ConsumeFreshRunRequest();

        StartCoroutine(InitStageRoutine(freshRunRequested));
    }

    private void EnsureCompositionModules()
    {
        if (statManager == null)
            statManager = GetComponent<StageStatManager>();

        if (statManager == null)
            statManager = gameObject.AddComponent<StageStatManager>();

        if (visualDebug == null)
            visualDebug = GetComponent<StageVisualDebug>();

        if (visualDebug == null)
            visualDebug = gameObject.AddComponent<StageVisualDebug>();

        statManager.Initialize(this);
        visualDebug.Initialize(this);
    }

    private void InitializePhases()
    {
        SpawningPhase = new SpawningMinionsPhase();
        MinionsPhase = new FightingMinionsPhase();
        BossPhase = new FightingBossPhase();
        ClearedPhase = new StageClearedPhase();

        SpawningPhase.Initialize(this);
        MinionsPhase.Initialize(this);
        BossPhase.Initialize(this);
        ClearedPhase.Initialize(this);

        phases.Clear();
        phases[StageState.SpawningMinions] = SpawningPhase;
        phases[StageState.FightingMinions] = MinionsPhase;
        phases[StageState.FightingBoss] = BossPhase;
        phases[StageState.StageCleared] = ClearedPhase;

        currentPhase = null;
    }

    private void HardResetRuntimeState()
    {
        stageHasStarted = false;
        currentState = StageState.SpawningMinions;
        currentStage = startingStageNumber;
        StateEnterTime = Time.time;

        ActiveEnemiesCount = 0;
        isChangingStage = false;
        IsTransitioningToBoss = false;
        BossSpawnConfigurationFailed = false;

        playerCharacterCache = null;
        previousPlayerHP = -1f;

        if (BossPhase != null)
            BossPhase.ResetBossRuntime();
    }

    private IEnumerator InitStageRoutine(bool freshRunRequested)
    {
        // Tunggu 2 frame agar Unity sempat memproses 100% Destroy objek memori lama
        // sebelum kita men-spawn stage baru.
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        ResetStageManagerRuntimeToInitialState(freshRunRequested);
        yield return StartCoroutine(StartStageAfterPlayerWeaponReady());
    }

    private void ResetStageManagerRuntimeToInitialState(bool freshRunRequested)
    {
        // FIX FRESH RUN: pastikan stage belum dianggap "berjalan" selama proses reset.
        stageHasStarted = false;

        currentStage = startingStageNumber;
        currentState = StageState.SpawningMinions;
        StateEnterTime = Time.time;

        ActiveEnemiesCount = 0;
        isChangingStage = false;
        IsTransitioningToBoss = false;
        BossSpawnConfigurationFailed = false;

        BossPhase.ResetBossRuntime();

        playerCharacterCache = null;
        previousPlayerHP = -1f;

        statManager.ResetAll();
        visualDebug.ResetAll();

        SetBlackScreenInstant(0f, false);
        EnsureBossUIReference();

        if (freshRunRequested)
        {
            Debug.Log("[STAGE MANAGER] Fresh run diproses. Stage direset ke: " + startingStageNumber);

            // FIX SPAWN PLAY AGAIN: setiap langkah reset fresh-run dilindungi try-catch terpisah.
            // Bila salah satu langkah gagal (mis. reset DDA via reflection melempar exception pada
            // proyek tertentu), sisa proses TETAP berjalan dan yang terpenting StartStage/BeginStage
            // tetap terpanggil. Tanpa ini, kegagalan di sini membuat stage tidak pernah dimulai
            // sehingga minion/boss tidak muncul dan transisi tidak terjadi saat Play Again.
            if (destroyRuntimeCombatObjectsOnFreshRun)
            {
                try
                {
                    DestroyFreshRunRuntimeCombatObjects();
                }
                catch (Exception ex)
                {
                    Debug.LogError("[STAGE MANAGER] DestroyFreshRunRuntimeCombatObjects gagal: " + ex);
                }
            }

            try
            {
                ResetRuntimeDataForFreshRun();
            }
            catch (Exception ex)
            {
                Debug.LogError("[STAGE MANAGER] ResetRuntimeDataForFreshRun gagal: " + ex);
            }

            try
            {
                ResetPlayerForFreshRun();
            }
            catch (Exception ex)
            {
                Debug.LogError("[STAGE MANAGER] ResetPlayerForFreshRun gagal: " + ex);
            }
        }
        else
        {
            // FIX FRESH RUN: walau bukan fresh run formal, tetap pastikan player diposisikan
            // di spawn point agar tidak ter-trigger NextStage karena posisi lama.
            EnsurePlayerPositionedAtSpawnSafely();
        }
    }

    private IEnumerator StartStageAfterPlayerWeaponReady()
    {
        yield return null;

        EnsurePlayerReference();
        CachePlayerHealthForTokenDebug();

        if (waitUntilPlayerWeaponSelectedBeforeStart)
        {
            // FIX BLACKSCREEN PLAY AGAIN: gunakan timeout agar loop tidak menggantung selamanya.
            // Sebelumnya, bila setelah Game Over state pemilihan senjata kembali ke None dan
            // player belum ter-spawn oleh PlayerPrefabSwitchManager, GetCurrentActivePlayerWeapon()
            // selalu mengembalikan None -> loop ini tak pernah selesai -> BeginStage() tak pernah
            // dipanggil -> kamera tak mendapat target -> layar hitam dengan hanya UI HP tersisa.
            float waitStartTime = Time.unscaledTime;

            while (GetCurrentActivePlayerWeapon() == WeaponType.None)
            {
                EnsurePlayerReference();

                if (Time.unscaledTime - waitStartTime >= PLAYER_WEAPON_SELECTION_TIMEOUT)
                {
                    Debug.LogWarning(
                        "[STAGE MANAGER] Timeout menunggu pemilihan senjata player. " +
                        "Stage dilanjutkan dengan senjata fallback (" + selectedPlayerWeapon + ") " +
                        "agar tidak macet di layar hitam."
                    );
                    break;
                }

                yield return null;
            }
        }

        // FIX BLACKSCREEN PLAY AGAIN: pastikan selalu ada player aktif sebelum stage mulai.
        // Bila player tetap tidak ditemukan (mis. akibat timeout di atas), spawn player fallback
        // dari senjata terakhir yang diketahui. Tanpa player, kamera tidak punya target dan
        // layar tampak hitam meski stage berjalan.
        EnsurePlayerReference();

        if (playerTransform == null)
        {
            Debug.LogWarning(
                "[STAGE MANAGER] Player tidak ditemukan saat stage akan dimulai. " +
                "Membuat player fallback agar game tetap dapat dimainkan."
            );
            SpawnFallbackPlayerFromSelectedWeapon();
        }

        UpdateSelectedPlayerWeaponFromActivePlayer();
        AssignPlayerReferenceToRuntimeSystems();

        stageHasStarted = true;

        // FIX SPAWN PLAY AGAIN: log penanda agar mudah memastikan via Console bahwa proses init
        // benar-benar mencapai titik mulai stage (BeginStage). Jika log ini TIDAK muncul saat
        // Play Again, berarti masalah ada sebelum titik ini (mis. loop tunggu senjata / reset).
        Debug.Log("[STAGE MANAGER] Init selesai. Memanggil BeginStage untuk stage pertama.");

        try
        {
            BeginStage();
        }
        catch (Exception ex)
        {
            // Jika BeginStage gagal, jangan biarkan diam — tampilkan agar tidak salah didiagnosa
            // sebagai "minion tidak spawn tanpa sebab".
            Debug.LogError("[STAGE MANAGER] BeginStage melempar exception: " + ex);
        }
    }

    private void HandleActiveWeaponChanged(WeaponType newWeapon)
    {
        if (newWeapon == WeaponType.Sword || newWeapon == WeaponType.Bow)
            selectedPlayerWeapon = newWeapon;

        EnsurePlayerReference();
        AssignPlayerReferenceToRuntimeSystems();

        Debug.Log("[STAGE MANAGER] Active weapon dari prefab switch terbaca: " + newWeapon);
    }

    private void Update()
    {
        // FIX FRESH RUN: jangan jalankan logika fase sebelum stage benar-benar dimulai.
        if (!stageHasStarted)
        {
            statManager.RefreshConcurrentAttackTokenDebugState();
            statManager.PruneRuntimeData();
            visualDebug.PruneFloatingLines();
            return;
        }

        if (currentPhase != null)
        {
            currentPhase.UpdatePhase();
        }

        statManager.RefreshConcurrentAttackTokenDebugState();
        statManager.PruneRuntimeData();
        visualDebug.PruneFloatingLines();
    }

    // =====================================================================
    // FSM: TRANSISI STATE
    // =====================================================================

    /// <summary>Pindah ke state baru: ExitPhase lama -&gt; set currentState -&gt; EnterPhase baru.</summary>
    public void ChangeState(StageState newState)
    {
        if (currentPhase != null)
        {
            currentPhase.ExitPhase();
        }

        currentState = newState;
        currentPhase = phases[newState];
        currentPhase.EnterPhase();
    }

    /// <summary>Mulai (atau mulai ulang) sebuah stage dari fase SpawningMinions.</summary>
    public void BeginStage()
    {
        Debug.Log("--- MEMULAI STAGE " + GetDisplayedStageNumber() + " ---");

        ActiveEnemiesCount = 0;
        IsTransitioningToBoss = false;
        BossSpawnConfigurationFailed = false;
        BossPhase.ResetBossRuntime();

        statManager.ResetForNewStage();

        // FIX FRESH RUN: pastikan isChangingStage juga bersih agar CheckStageTransition
        // dapat berfungsi normal pada stage baru.
        isChangingStage = false;

        if (bossHPBarUI != null)
        {
            bossHPBarUI.SetBossPrefabs(bossPrefabSword, bossPrefabBow);
            bossHPBarUI.Hide();
        }

        ChangeState(StageState.SpawningMinions);
    }

    // =====================================================================
    // PROGRESI
    // =====================================================================

    public int GetDisplayedStageNumber()
    {
        return GetStageProgressionIndex() + 1;
    }

    public int GetStageProgressionIndex()
    {
        return Mathf.Max(0, currentStage - startingStageNumber);
    }

    public int CalculateTotalMinions(int stageProgressionIndex)
    {
        return Mathf.Max(0, baseSpawnToken + (spawnTokenIncreasePerStage * stageProgressionIndex));
    }

    // =====================================================================
    // EVENT KEMATIAN MUSUH (FACADE PUBLIK — dipanggil EnemyDeathHandler)
    // =====================================================================

    public void OnEnemyDied()
    {
        if (currentState == StageState.StageCleared)
            return;

        ActiveEnemiesCount = Mathf.Max(0, ActiveEnemiesCount - 1);

        Debug.Log(
            $"[STAGE MANAGER] OnEnemyDied terpanggil. " +
            $"State: {currentState} | Sisa activeEnemiesCount: {ActiveEnemiesCount}"
        );

        // Kunci pelindung agar data kematian sisa objek lama tidak merusak jalannya wave baru.
        if (!IsPastGracePeriod())
            return;

        if (currentPhase != null)
        {
            currentPhase.OnEnemyDied();
        }
    }

    public int CountAliveEnemyObjectsInScene()
    {
        GameObject[] enemies;

        try
        {
            enemies = GameObject.FindGameObjectsWithTag("Enemy");
        }
        catch (UnityException)
        {
            Debug.LogWarning("[STAGE MANAGER] Tag Enemy belum dibuat di Project Settings > Tags and Layers.");
            return ActiveEnemiesCount;
        }

        int aliveCount = 0;

        foreach (GameObject enemy in enemies)
        {
            if (enemy == null || !enemy.activeInHierarchy)
                continue;

            CharacterBase character = enemy.GetComponent<CharacterBase>();

            if (character == null)
            {
                character = enemy.GetComponentInChildren<CharacterBase>();
            }

            if (character == null)
            {
                // Jika tidak ada CharacterBase, objek tetap dihitung hidup
                // agar StageManager tidak salah menganggap musuh sudah mati.
                aliveCount++;
                continue;
            }

            if (character.currentHP > 0f)
            {
                aliveCount++;
            }
        }

        return aliveCount;
    }

    // =====================================================================
    // TRANSISI STAGE (dipanggil StageClearedPhase)
    // =====================================================================

    public void CheckStageTransition()
    {
        if (isChangingStage)
            return;

        if (currentState != StageState.StageCleared)
            return;

        if (playerTransform == null)
        {
            EnsurePlayerReference();
        }

        if (playerTransform == null)
            return;

        if (playerTransform.position.x >= rightTransitionX)
        {
            NextStage();
        }
    }

    private void NextStage()
    {
        if (isChangingStage)
            return;

        StartCoroutine(NextStageTransitionRoutine());
    }

    private IEnumerator NextStageTransitionRoutine()
    {
        if (playerTransform == null)
        {
            Debug.LogWarning("[STAGE MANAGER] Player transform belum diisi.");
            yield break;
        }

        isChangingStage = true;

        Debug.Log("[STAGE MANAGER] Transisi menuju stage berikutnya dimulai.");

        yield return StartCoroutine(FadeBlackScreen(1f));

        if (blackScreenHoldDuration > 0f)
        {
            yield return new WaitForSeconds(blackScreenHoldDuration);
        }

        float startX = nextStagePlayerStartPoint != null
            ? nextStagePlayerStartPoint.position.x
            : fallbackPlayerStartX;

        playerTransform.position = new Vector3(
            startX,
            playerTransform.position.y,
            playerTransform.position.z
        );

        currentStage++;

        RegeneratePlayerHealthForNextStage();
        TryResetSkillDebugData();

        yield return StartCoroutine(FadeBlackScreen(0f));

        BeginStage();

        isChangingStage = false;

        Debug.Log("[STAGE MANAGER] Transisi menuju stage berikutnya selesai.");
    }

    private IEnumerator FadeBlackScreen(float targetAlpha)
    {
        if (blackScreenCanvasGroup == null)
            yield break;

        float startAlpha = blackScreenCanvasGroup.alpha;
        float elapsedTime = 0f;

        if (targetAlpha > 0f)
        {
            SetBlackScreenInstant(startAlpha, true);
        }

        if (blackScreenFadeDuration <= 0f)
        {
            SetBlackScreenInstant(targetAlpha, targetAlpha > 0.001f);
            yield break;
        }

        while (elapsedTime < blackScreenFadeDuration)
        {
            elapsedTime += Time.deltaTime;

            float progress = Mathf.Clamp01(elapsedTime / blackScreenFadeDuration);
            blackScreenCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);

            yield return null;
        }

        SetBlackScreenInstant(targetAlpha, targetAlpha > 0.001f);
    }

    private void SetBlackScreenInstant(float alpha, bool blockRaycasts)
    {
        if (blackScreenCanvasGroup == null)
            return;

        blackScreenCanvasGroup.alpha = Mathf.Clamp01(alpha);
        blackScreenCanvasGroup.blocksRaycasts = blockRaycasts;
        blackScreenCanvasGroup.interactable = blockRaycasts;
    }

    // =====================================================================
    // PLAYER REFERENCE & FALLBACK
    // =====================================================================

    private void EnsurePlayerReference()
    {
        if (preferPrefabSwitchActivePlayer)
        {
            Transform activePlayer = FindActivePlayerFromScene();

            if (activePlayer != null)
                playerTransform = activePlayer;
        }

        if (playerTransform == null || !playerTransform.gameObject.activeInHierarchy)
        {
            try
            {
                GameObject existingPlayer = GameObject.FindGameObjectWithTag("Player");

                if (existingPlayer != null && existingPlayer.activeInHierarchy)
                    playerTransform = existingPlayer.transform;
            }
            catch (UnityException)
            {
                Debug.LogWarning("[STAGE MANAGER] Tag Player belum dibuat di Project Settings > Tags and Layers.");
            }
        }

        if (playerTransform == null && !waitUntilPlayerWeaponSelectedBeforeStart)
        {
            SpawnFallbackPlayerFromSelectedWeapon();
        }

        UpdateSelectedPlayerWeaponFromActivePlayer();
        AssignPlayerReferenceToRuntimeSystems();
    }

    private Transform FindActivePlayerFromScene()
    {
        PlayerWeaponIdentity[] identities = FindObjectsOfType<PlayerWeaponIdentity>(true);

        foreach (PlayerWeaponIdentity identity in identities)
        {
            if (identity == null)
                continue;

            if (!identity.gameObject.activeInHierarchy)
                continue;

            if (identity.currentWeapon == WeaponType.Sword || identity.currentWeapon == WeaponType.Bow)
                return identity.transform;
        }

        foreach (PlayerWeaponIdentity identity in identities)
        {
            if (identity == null)
                continue;

            if (!identity.gameObject.activeInHierarchy)
                continue;

            if (identity.currentWeapon == WeaponType.None)
                return identity.transform;
        }

        try
        {
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");

            if (taggedPlayer != null && taggedPlayer.activeInHierarchy)
                return taggedPlayer.transform;
        }
        catch (UnityException)
        {
            Debug.LogWarning("[STAGE MANAGER] Tag Player belum dibuat.");
        }

        Player fallbackPlayer = FindObjectOfType<Player>();

        if (fallbackPlayer != null)
            return fallbackPlayer.transform;

        return null;
    }

    private void SpawnFallbackPlayerFromSelectedWeapon()
    {
        GameObject selectedPlayerPrefab = selectedPlayerWeapon == WeaponType.Bow
            ? playerBowPrefab
            : playerSwordPrefab;

        if (selectedPlayerPrefab == null)
        {
            Debug.LogError(
                "[STAGE MANAGER] Player prefab belum diisi. " +
                "Isi playerSwordPrefab dan playerBowPrefab di Inspector."
            );
            return;
        }

        Vector3 spawnPosition = playerSpawnPoint != null
            ? playerSpawnPoint.position
            : new Vector3(fallbackPlayerStartX, 0f, 0f);

        Quaternion spawnRotation = playerSpawnPoint != null
            ? playerSpawnPoint.rotation
            : Quaternion.identity;

        GameObject playerObject = Instantiate(selectedPlayerPrefab, spawnPosition, spawnRotation);

        try
        {
            playerObject.tag = "Player";
        }
        catch (UnityException)
        {
            Debug.LogWarning("[STAGE MANAGER] Tag Player belum dibuat. Player tetap dibuat, tetapi tag tidak dapat diatur.");
        }

        playerTransform = playerObject.transform;

        Debug.Log(
            "[STAGE MANAGER] Player fallback dibuat dari prefab: " +
            selectedPlayerPrefab.name +
            " | Weapon: " +
            selectedPlayerWeapon
        );
    }

    private void AssignPlayerReferenceToRuntimeSystems()
    {
        if (playerTransform == null)
            return;

        CameraFollow cameraFollow = FindObjectOfType<CameraFollow>();

        if (cameraFollow != null)
        {
            cameraFollow.SetTarget(playerTransform);
        }
        else
        {
            Debug.LogWarning("[STAGE MANAGER] CameraFollow tidak ditemukan di scene.");
        }

        WeaponType activeWeapon = GetCurrentActivePlayerWeapon();

        if (DataTracker.Instance != null)
        {
            DataTracker.Instance.SetPlayerTransform(playerTransform);
            DataTracker.Instance.SetActiveWeapon(activeWeapon);
        }
        else
        {
            Debug.LogWarning("[STAGE MANAGER] DataTracker.Instance belum tersedia.");
        }
    }

    private void EnsurePlayerPositionedAtSpawnSafely()
    {
        EnsurePlayerReference();

        if (playerTransform == null)
            return;

        if (playerTransform.position.x < rightTransitionX - 1f)
            return;

        Vector3 spawnPosition = playerSpawnPoint != null
            ? playerSpawnPoint.position
            : new Vector3(fallbackPlayerStartX, playerTransform.position.y, playerTransform.position.z);

        playerTransform.position = spawnPosition;

        Debug.Log(
            "[STAGE MANAGER] Player dipindahkan ke spawn point pengaman karena posisi lama " +
            "mendekati/melewati rightTransitionX. Posisi baru: " + playerTransform.position
        );
    }

    public CharacterBase GetPlayerCharacter()
    {
        if (playerTransform == null)
        {
            playerCharacterCache = null;
            return null;
        }

        if (playerCharacterCache != null)
            return playerCharacterCache;

        playerCharacterCache = playerTransform.GetComponent<CharacterBase>();

        if (playerCharacterCache == null)
        {
            playerCharacterCache = playerTransform.GetComponentInChildren<CharacterBase>();
        }

        return playerCharacterCache;
    }

    private void CachePlayerHealthForTokenDebug()
    {
        CharacterBase playerCharacter = GetPlayerCharacter();

        if (playerCharacter == null)
        {
            previousPlayerHP = -1f;
            return;
        }

        previousPlayerHP = playerCharacter.currentHP;
    }

    private void RegeneratePlayerHealthForNextStage()
    {
        statManager.lastPlayerRegenAmount = 0f;

        if (!regenPlayerOnNextStage)
        {
            CachePlayerHealthForTokenDebug();
            return;
        }

        CharacterBase playerCharacter = GetPlayerCharacter();

        if (playerCharacter == null)
        {
            Debug.LogWarning(
                "[STAGE MANAGER] Regen HP player gagal karena CharacterBase pada player tidak ditemukan."
            );
            previousPlayerHP = -1f;
            return;
        }

        float beforeHP = playerCharacter.currentHP;
        float flatRegen = Mathf.Max(0f, nextStagePlayerFlatHPRegen);
        float percentRegen = Mathf.Max(0f, nextStagePlayerPercentHPRegen) * playerCharacter.maxHP;
        float targetHP = beforeHP + flatRegen + percentRegen;

        if (clampPlayerRegenToMaxHP)
        {
            targetHP = Mathf.Min(playerCharacter.maxHP, targetHP);
        }

        playerCharacter.currentHP = Mathf.Max(0f, targetHP);
        statManager.lastPlayerRegenAmount = playerCharacter.currentHP - beforeHP;
        previousPlayerHP = playerCharacter.currentHP;

        visualDebug.AddFloatingDebugLine(
            $"[REGEN] Player +{statManager.lastPlayerRegenAmount:0.#} HP " +
            $"({beforeHP:0.#} -> {playerCharacter.currentHP:0.#}/{playerCharacter.maxHP:0.#})."
        );

        Debug.Log(
            $"[STAGE MANAGER] Player regen setelah next stage: " +
            $"+{statManager.lastPlayerRegenAmount:0.##} HP | " +
            $"{beforeHP:0.##} -> {playerCharacter.currentHP:0.##}/{playerCharacter.maxHP:0.##}"
        );
    }

    // =====================================================================
    // SPAWN PRIMITIF (dipakai fase spawn & boss)
    // =====================================================================

    public bool SpawnEnemy(GameObject prefab, float statMultiplier, int attackTokens)
    {
        if (prefab == null)
        {
            Debug.LogWarning("[STAGE MANAGER] Prefab enemy belum diisi.");
            return false;
        }

        Vector3 spawnPos = transform.position;
        bool spawnPointUsed = false;

        if (minionSpawnPoints != null && minionSpawnPoints.Count > 0)
        {
            int totalValidSpawnPoints = 0;
            foreach (Transform sp in minionSpawnPoints)
            {
                if (sp != null) totalValidSpawnPoints++;
            }

            if (totalValidSpawnPoints == 0)
            {
                Debug.LogError(
                    "[STAGE MANAGER] SEMUA minion spawn points ter-Destroy / null! " +
                    "Kemungkinan terkena DestroyFreshRunRuntimeCombatObjects karena tag-nya cocok " +
                    "dengan freshRunDestroyTags. Lepas tag 'Spawn'/'Enemy'/'Projectile' dari spawn " +
                    "point di Inspector, ATAU pastikan spawn point itu di-assign ke minionSpawnPoints " +
                    "agar dilindungi oleh IsReferencedSpawnObject."
                );
            }
            else
            {
                for (int attempt = 0; attempt < minionSpawnPoints.Count; attempt++)
                {
                    Transform candidate = minionSpawnPoints[UnityEngine.Random.Range(0, minionSpawnPoints.Count)];

                    if (candidate != null)
                    {
                        spawnPos = candidate.position;
                        spawnPointUsed = true;
                        break;
                    }
                }

                if (!spawnPointUsed)
                {
                    Debug.LogWarning(
                        "[STAGE MANAGER] Gagal mendapatkan spawn point valid setelah beberapa percobaan. " +
                        "Menggunakan posisi StageManager sebagai fallback: " + spawnPos
                    );
                }
            }
        }

        GameObject enemy = Instantiate(prefab, spawnPos, Quaternion.identity);

        enemy.tag = "Enemy";
        SetLayerRecursively(enemy, LayerMask.NameToLayer("Enemy"));

        CharacterBase character = enemy.GetComponent<CharacterBase>();

        if (character == null)
        {
            character = enemy.GetComponentInChildren<CharacterBase>();
        }

        if (character == null)
        {
            Debug.LogWarning(
                $"[STAGE MANAGER] Prefab {prefab.name} tidak memiliki CharacterBase atau turunan Enemy.cs."
            );

            Destroy(enemy);
            return false;
        }

        StageStatManager.StatsSnapshot beforeStats = statManager.CaptureStats(character);
        statManager.InitializeCharacterBaseStats(character, statMultiplier);
        StageStatManager.StatsSnapshot afterStats = statManager.CaptureStats(character);

        InitializeStageCombatController(enemy, character, attackTokens, false);
        InitializeDeathHandler(enemy);
        statManager.RegisterRuntimeEnemyDebug(enemy, character, attackTokens, statMultiplier, beforeStats, afterStats, false);

        ActiveEnemiesCount++;

        Debug.Log(
            $"[STAGE MANAGER] Spawn minion: {enemy.name} | " +
            $"HP: {character.currentHP}/{character.maxHP} | " +
            $"Attack: {character.attack} | " +
            $"MoveSpeed: {character.moveSpeed} | " +
            $"ConcurrentAttackTokenLimit: {attackTokens} | " +
            $"ActiveCount: {ActiveEnemiesCount}"
        );

        return true;
    }

    public void InitializeStageCombatController(GameObject enemy, CharacterBase character, int attackTokens, bool isBoss = false)
    {
        if (enemy == null || character == null)
            return;

        bool initializedAnyController = false;

        NodeManager nodeManager = enemy.GetComponent<NodeManager>();

        if (nodeManager == null)
        {
            nodeManager = enemy.GetComponentInChildren<NodeManager>();
        }

        if (nodeManager != null)
        {
            nodeManager.InitializeStageEnemy(character, attackTokens, isBoss);
            initializedAnyController = true;
        }

        MonoBehaviour[] behaviours = enemy.GetComponentsInChildren<MonoBehaviour>(true);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null || behaviour == this)
                continue;

            if (nodeManager != null && ReferenceEquals(behaviour, nodeManager))
                continue;

            if (TryInvokeInitializeStageEnemy(behaviour, character, attackTokens, isBoss))
            {
                initializedAnyController = true;
            }
        }

        if (!initializedAnyController)
        {
            Debug.LogWarning(
                $"[STAGE MANAGER] {enemy.name} tidak memiliki controller dengan InitializeStageEnemy(CharacterBase, int, bool). " +
                "Token serangan tetap dicatat pada StageManager, tetapi script enemy perlu diintegrasikan agar token benar-benar membatasi serangan."
            );
        }
    }

    private bool TryInvokeInitializeStageEnemy(MonoBehaviour target, CharacterBase character, int attackTokens, bool isBoss)
    {
        if (target == null)
            return false;

        Type targetType = target.GetType();
        MethodInfo method = targetType.GetMethod(
            "InitializeStageEnemy",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (method == null)
            return false;

        ParameterInfo[] parameters = method.GetParameters();

        if (parameters.Length != 3 ||
            !typeof(CharacterBase).IsAssignableFrom(parameters[0].ParameterType) ||
            parameters[1].ParameterType != typeof(int) ||
            parameters[2].ParameterType != typeof(bool))
        {
            return false;
        }

        try
        {
            method.Invoke(target, new object[] { character, attackTokens, isBoss });
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"[STAGE MANAGER] Gagal menginisialisasi {targetType.Name}.InitializeStageEnemy(): {exception.Message}"
            );
            return false;
        }
    }

    public void InitializeDeathHandler(GameObject enemy)
    {
        if (enemy == null)
            return;

        EnemyDeathHandler deathHandler = enemy.GetComponent<EnemyDeathHandler>();

        if (deathHandler == null)
        {
            deathHandler = enemy.GetComponentInChildren<EnemyDeathHandler>();
        }

        if (deathHandler != null)
        {
            deathHandler.Init(this);
        }
        else
        {
            Debug.LogWarning(
                $"[STAGE MANAGER] {enemy.name} tidak memiliki EnemyDeathHandler. " +
                "Boss tetap akan dicoba muncul melalui fallback scan, tetapi sebaiknya prefab tetap diberi EnemyDeathHandler."
            );
        }
    }

    public void SetLayerRecursively(GameObject obj, int layer)
    {
        if (obj == null)
            return;

        if (layer < 0)
        {
            Debug.LogWarning("[STAGE MANAGER] Layer Enemy belum dibuat di Project Settings > Tags and Layers.");
            return;
        }

        obj.layer = layer;

        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    // =====================================================================
    // DETEKSI SENJATA (SISTEM SENJATA ADAPTIF)
    // =====================================================================

    private void UpdateSelectedPlayerWeaponFromActivePlayer()
    {
        WeaponType activeWeapon = GetCurrentActivePlayerWeapon();

        if (activeWeapon == WeaponType.Sword || activeWeapon == WeaponType.Bow)
            selectedPlayerWeapon = activeWeapon;
    }

    private WeaponType GetCurrentActivePlayerWeapon()
    {
        if (PlayerPrefabSwitchManager.CurrentWeapon == WeaponType.Sword ||
            PlayerPrefabSwitchManager.CurrentWeapon == WeaponType.Bow)
        {
            return PlayerPrefabSwitchManager.CurrentWeapon;
        }

        WeaponType fromPlayerTransform = GetWeaponFromTransform(playerTransform);

        if (fromPlayerTransform == WeaponType.Sword || fromPlayerTransform == WeaponType.Bow)
            return fromPlayerTransform;

        if (PlayerPrefabSwitchManager.Instance != null)
            return WeaponType.None;

        return selectedPlayerWeapon;
    }

    private WeaponType GetWeaponFromTransform(Transform source)
    {
        if (source == null)
            return WeaponType.None;

        PlayerWeaponIdentity identity = source.GetComponent<PlayerWeaponIdentity>();

        if (identity == null)
            identity = source.GetComponentInChildren<PlayerWeaponIdentity>(true);

        if (identity == null)
            identity = source.GetComponentInParent<PlayerWeaponIdentity>();

        if (identity != null)
            return identity.currentWeapon;

        Player player = source.GetComponent<Player>();

        if (player == null)
            player = source.GetComponentInChildren<Player>(true);

        if (player == null)
            player = source.GetComponentInParent<Player>();

        if (player != null)
            return player.weaponType;

        return WeaponType.None;
    }

    private string ConvertWeaponToBossKey(WeaponType weapon)
    {
        if (weapon == WeaponType.Bow)
            return "Bow";

        return "Sword";
    }

    public string GetPlayerPlaystyleFromDDA()
    {
        if (DDAController.Instance != null)
            return DDAController.Instance.currentPlayerPlaystyle.ToString();

        return "Balanced";
    }

    public string GetDominantWeaponFromDDA()
    {
        WeaponType dominantWeapon = WeaponType.None;

        if (DDAController.Instance != null)
        {
            dominantWeapon = DDAController.Instance.currentPlayerDominantWeapon;
        }

        if (dominantWeapon != WeaponType.Sword && dominantWeapon != WeaponType.Bow)
        {
            dominantWeapon = GetCurrentActivePlayerWeapon();
        }

        if (dominantWeapon != WeaponType.Sword && dominantWeapon != WeaponType.Bow)
        {
            dominantWeapon = selectedPlayerWeapon;
        }

        return ConvertWeaponToBossKey(dominantWeapon);
    }

    // =====================================================================
    // BOSS UI & DDA FINALISASI/RESET (dipanggil FightingBossPhase)
    // =====================================================================

    private void EnsureBossUIReference()
    {
        if (bossHPBarUI == null && autoFindBossHPBarUI)
        {
            bossHPBarUI = FindObjectOfType<BossHPBarUI>();
        }

        if (bossHPBarUI != null)
        {
            bossHPBarUI.SetBossPrefabs(bossPrefabSword, bossPrefabBow);
            bossHPBarUI.Hide();
        }
    }

    public void ShowBossHPBar(GameObject bossPrefab, GameObject bossInstance, CharacterBase bossCharacter)
    {
        if (bossHPBarUI != null)
        {
            bossHPBarUI.ShowBossByPrefab(bossPrefab, bossInstance, bossCharacter);
        }
    }

    public void HideBossHPBar()
    {
        if (bossHPBarUI != null)
        {
            bossHPBarUI.Hide();
        }
    }

    /// <summary>FASE MINION membaca/membekukan DDA SEBELUM boss (jika diaktifkan).</summary>
    public void FinalizeDDABeforeBossIfNeeded()
    {
        if (finalizeDataBeforeBoss)
        {
            Debug.Log("DDA Data Difinalisasi Sebelum Boss");

            if (DataTracker.Instance != null)
            {
                DataTracker.Instance.FinalizeStageData();
            }
        }
    }

    public void FinalizeDDAAfterBossIfNeeded()
    {
        if (finalizeDataAfterBoss)
        {
            Debug.Log("DDA Data Difinalisasi Setelah Boss");

            if (DataTracker.Instance != null)
            {
                DataTracker.Instance.FinalizeStageData();
            }
        }
    }

    public void ApplyStageClearedDDAReset()
    {
        if (!resetDDAAndDataTrackerOnStageCleared)
            return;

        if (preserveDDAProfileForNextStage)
        {
            ResetDataTrackerOnly();
        }
        else
        {
            ResetDDAAndDataTracker();
        }
    }

    // =====================================================================
    // FRESH RUN CLEANUP
    // =====================================================================

    private void DestroyFreshRunRuntimeCombatObjects()
    {
        if (freshRunDestroyTags == null)
            return;

        HashSet<GameObject> rootsToDestroy = new HashSet<GameObject>();
        int skippedSpawnPoints = 0;
        int skippedSceneObjects = 0;

        foreach (string targetTag in freshRunDestroyTags)
        {
            if (string.IsNullOrWhiteSpace(targetTag))
                continue;

            GameObject[] taggedObjects;

            try
            {
                taggedObjects = GameObject.FindGameObjectsWithTag(targetTag);
            }
            catch (UnityException)
            {
                Debug.LogWarning("[STAGE MANAGER] Fresh run melewati tag yang belum dibuat: " + targetTag);
                continue;
            }

            if (taggedObjects == null)
                continue;

            foreach (GameObject taggedObject in taggedObjects)
            {
                if (taggedObject == null)
                    continue;

                if (playerTransform != null && taggedObject.transform == playerTransform)
                    continue;

                GameObject root = taggedObject.transform.root.gameObject;

                if (root == null || root == gameObject)
                    continue;

                // FIX FRESH RUN: lindungi objek yang direferensikan sebagai spawn point di Inspector.
                if (IsReferencedSpawnObject(root) || IsReferencedSpawnObject(taggedObject))
                {
                    skippedSpawnPoints++;
                    continue;
                }

                // FIX FRESH RUN: hanya destroy objek runtime yang benar-benar bertahan dari run
                // sebelumnya (DontDestroyOnLoad). Objek scene yang fresh JANGAN diutak-atik.
                bool isDontDestroyOnLoadObject = taggedObject.scene.name == "DontDestroyOnLoad";

                if (!isDontDestroyOnLoadObject)
                {
                    if (!LooksLikeRuntimeCombatObject(root))
                    {
                        skippedSceneObjects++;
                        continue;
                    }
                }

                rootsToDestroy.Add(root);
            }
        }

        foreach (GameObject root in rootsToDestroy)
        {
            if (root == null)
                continue;

            if (playerTransform != null && root == playerTransform.gameObject)
                continue;

            Destroy(root);
        }

        ActiveEnemiesCount = 0;
        statManager.ResetForNewStage();

        Debug.Log(
            $"[STAGE MANAGER] Fresh run cleanup selesai. Dihancurkan: {rootsToDestroy.Count} root. " +
            $"Spawn points dilindungi: {skippedSpawnPoints}. Objek scene non-combat dilindungi: {skippedSceneObjects}."
        );
    }

    private bool IsReferencedSpawnObject(GameObject candidate)
    {
        if (candidate == null)
            return false;

        Transform candidateTransform = candidate.transform;

        if (IsTransformOrAncestorOf(candidateTransform, bossSpawnPoint)) return true;
        if (IsTransformOrAncestorOf(candidateTransform, playerSpawnPoint)) return true;
        if (IsTransformOrAncestorOf(candidateTransform, nextStagePlayerStartPoint)) return true;

        if (minionSpawnPoints != null)
        {
            foreach (Transform spawnPoint in minionSpawnPoints)
            {
                if (IsTransformOrAncestorOf(candidateTransform, spawnPoint))
                    return true;
            }
        }

        return false;
    }

    private bool IsTransformOrAncestorOf(Transform candidate, Transform target)
    {
        if (candidate == null || target == null)
            return false;

        if (candidate == target)
            return true;

        Transform cursor = target;
        while (cursor != null)
        {
            if (cursor == candidate)
                return true;
            cursor = cursor.parent;
        }

        return false;
    }

    private bool LooksLikeRuntimeCombatObject(GameObject root)
    {
        if (root == null)
            return false;

        if (root.GetComponentInChildren<CharacterBase>(true) != null)
            return true;

        Rigidbody2D[] rigidbodies = root.GetComponentsInChildren<Rigidbody2D>(true);

        foreach (Rigidbody2D rb in rigidbodies)
        {
            if (rb == null) continue;

            if (rb.simulated && rb.bodyType != RigidbodyType2D.Static)
                return true;
        }

        return false;
    }

    private void ResetRuntimeDataForFreshRun()
    {
        if (resetDDAProfileOnFreshRun)
        {
            ResetDDAAndDataTracker();
        }
        else
        {
            ResetDataTrackerOnly();
        }

        TryResetSkillDebugData();
    }

    private void ResetPlayerForFreshRun()
    {
        EnsurePlayerReference();

        if (playerTransform == null)
        {
            SpawnFallbackPlayerFromSelectedWeapon();
        }

        if (playerTransform == null)
        {
            Debug.LogWarning("[STAGE MANAGER] Fresh run tidak bisa mereset player karena playerTransform masih null.");
            return;
        }

        if (!playerTransform.gameObject.activeSelf)
        {
            playerTransform.gameObject.SetActive(true);
        }

        if (movePlayerToSpawnOnFreshRun)
        {
            Vector3 spawnPosition = playerSpawnPoint != null
                ? playerSpawnPoint.position
                : new Vector3(fallbackPlayerStartX, playerTransform.position.y, playerTransform.position.z);

            playerTransform.position = spawnPosition;
        }
        else if (playerTransform.position.x >= rightTransitionX - 1f)
        {
            Vector3 safePosition = new Vector3(
                fallbackPlayerStartX,
                playerTransform.position.y,
                playerTransform.position.z
            );

            playerTransform.position = safePosition;

            Debug.LogWarning(
                "[STAGE MANAGER] movePlayerToSpawnOnFreshRun=false tetapi player berada terlalu " +
                "ke kanan. Posisi player dipaksa ke fallbackPlayerStartX agar stage 1 tidak dilompati."
            );
        }

        playerCharacterCache = null;

        CharacterBase playerCharacter = GetPlayerCharacter();

        if (playerCharacter != null && resetPlayerHealthOnFreshRun)
        {
            playerCharacter.currentHP = Mathf.Max(1f, playerCharacter.maxHP);
            previousPlayerHP = playerCharacter.currentHP;
        }
        else
        {
            CachePlayerHealthForTokenDebug();
        }

        InvokeFreshRunResetMethodsOnPlayer();

        AssignPlayerReferenceToRuntimeSystems();

        Debug.Log("[STAGE MANAGER] Player direset untuk fresh run pada posisi: " + playerTransform.position);
    }

    private void InvokeFreshRunResetMethodsOnPlayer()
    {
        if (playerTransform == null)
            return;

        MonoBehaviour[] behaviours = playerTransform.GetComponentsInChildren<MonoBehaviour>(true);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null)
                continue;

            TryInvokeParameterlessMethod(behaviour, "ResetForNewRun");
            TryInvokeParameterlessMethod(behaviour, "ResetRuntimeState");
            TryInvokeParameterlessMethod(behaviour, "ResetPlayerState");
            TryInvokeParameterlessMethod(behaviour, "ResetEnergy");
            TryInvokeParameterlessMethod(behaviour, "ResetCooldowns");
            TryInvokeParameterlessMethod(behaviour, "ResetSkillState");
        }
    }

    private bool TryInvokeParameterlessMethod(object target, string methodName)
    {
        if (target == null || string.IsNullOrEmpty(methodName))
            return false;

        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (method == null)
            return false;

        if (method.GetParameters().Length != 0)
            return false;

        // FIX SPAWN PLAY AGAIN: jangan biarkan exception dari method reset milik komponen player
        // membatalkan proses fresh-run. Bila gagal, lanjutkan saja.
        try
        {
            method.Invoke(target, null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning(
                $"[STAGE MANAGER] Gagal memanggil {methodName}() pada {target.GetType().Name}: {ex.Message}"
            );
            return false;
        }

        return true;
    }

    // =====================================================================
    // RESET DDA / DATATRACKER (REFLECTION)
    // =====================================================================

    private void TryResetSkillDebugData()
    {
        if (DataTracker.Instance == null)
            return;

        MethodInfo resetSkillDebugMethod = DataTracker.Instance.GetType().GetMethod(
            "ResetSkillDebugData",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (resetSkillDebugMethod == null)
            return;

        if (resetSkillDebugMethod.GetParameters().Length > 0)
            return;

        // FIX SPAWN PLAY AGAIN: lindungi dari exception agar fresh-run tidak terhenti.
        try
        {
            resetSkillDebugMethod.Invoke(DataTracker.Instance, null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning(
                "[STAGE MANAGER] ResetSkillDebugData() melempar exception saat dipanggil: " + ex.Message
            );
            return;
        }

        Debug.Log("[STAGE MANAGER] Skill debug player direset untuk stage berikutnya.");
    }

    private void ResetDataTrackerOnly()
    {
        bool trackerReset = TryResetRuntimeObject(
            "DataTracker",
            "ResetData",
            "ResetTracker",
            "ResetAll",
            "ClearData",
            "Clear",
            "Reset"
        );

        if (!trackerReset)
        {
            Debug.LogWarning(
                "[STAGE MANAGER] DataTracker tidak berhasil direset. " +
                "Pastikan DataTracker memiliki method ResetData(), ResetAll(), ClearData(), atau Reset()."
            );
        }

        Debug.Log("[STAGE MANAGER] DataTracker direset, tetapi profil DDA dipertahankan untuk stage berikutnya.");
    }

    private void ResetDDAAndDataTracker()
    {
        bool trackerReset = TryResetRuntimeObject(
            "DataTracker",
            "ResetData",
            "ResetTracker",
            "ResetAll",
            "ClearData",
            "Clear",
            "Reset"
        );

        bool ddaReset = TryResetRuntimeObject(
            "DDAController",
            "ResetDDA",
            "ResetData",
            "ResetWeights",
            "ResetProfile",
            "ResetAll",
            "ClearData",
            "Clear",
            "Reset"
        );

        if (!trackerReset)
        {
            Debug.LogWarning(
                "[STAGE MANAGER] DataTracker tidak berhasil direset. " +
                "Pastikan DataTracker memiliki method ResetData(), ResetAll(), ClearData(), atau Reset()."
            );
        }

        if (!ddaReset)
        {
            Debug.LogWarning(
                "[STAGE MANAGER] DDAController tidak berhasil direset. " +
                "Pastikan DDAController memiliki method ResetDDA(), ResetWeights(), ResetAll(), ClearData(), atau Reset()."
            );
        }
    }

    private bool TryResetRuntimeObject(string typeName, params string[] resetMethodNames)
    {
        Type targetType = FindTypeByName(typeName);

        if (targetType == null)
        {
            Debug.LogWarning($"[STAGE MANAGER] Type {typeName} tidak ditemukan.");
            return false;
        }

        object instance = FindRuntimeInstance(targetType);

        if (instance == null)
        {
            Debug.LogWarning($"[STAGE MANAGER] Instance {typeName} tidak ditemukan di scene atau singleton.");
            return false;
        }

        foreach (string methodName in resetMethodNames)
        {
            MethodInfo method = targetType.GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );

            if (method == null)
                continue;

            if (method.GetParameters().Length > 0)
                continue;

            // FIX SPAWN PLAY AGAIN: bungkus Invoke agar exception dari implementasi reset
            // (mis. DataTracker/DDAController) tidak membatalkan seluruh init fresh-run.
            try
            {
                method.Invoke(instance, null);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[STAGE MANAGER] {typeName}.{methodName}() melempar exception saat dipanggil: {ex.Message}"
                );
                return false;
            }

            Debug.Log($"[STAGE MANAGER] {typeName}.{methodName}() berhasil dipanggil.");
            return true;
        }

        return false;
    }

    private Type FindTypeByName(string typeName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (Assembly assembly in assemblies)
        {
            Type[] types;

            // FIX SPAWN PLAY AGAIN: sebagian assembly (mis. plugin/editor pihak ketiga) dapat
            // melempar ReflectionTypeLoadException saat GetTypes(). Tanpa penjagaan ini, exception
            // merambat ke atas dan MEMBATALKAN seluruh proses reset fresh-run -> BeginStage() tidak
            // pernah dipanggil sehingga minion/boss tidak pernah muncul pada saat Play Again.
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types;
            }
            catch (Exception)
            {
                continue;
            }

            if (types == null)
                continue;

            foreach (Type type in types)
            {
                if (type == null)
                    continue;

                if (type.Name == typeName || type.FullName == typeName)
                    return type;
            }
        }

        return null;
    }

    private object FindRuntimeInstance(Type targetType)
    {
        PropertyInfo instanceProperty = targetType.GetProperty(
            "Instance",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (instanceProperty != null)
        {
            object propertyInstance = instanceProperty.GetValue(null);
            if (propertyInstance != null)
                return propertyInstance;
        }

        FieldInfo instanceField = targetType.GetField(
            "Instance",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (instanceField != null)
        {
            object fieldInstance = instanceField.GetValue(null);
            if (fieldInstance != null)
                return fieldInstance;
        }

        UnityEngine.Object sceneObject = FindObjectOfType(targetType);

        if (sceneObject != null)
            return sceneObject;

        return null;
    }

    // =====================================================================
    // FACADE API ATTACK TOKEN (didelegasikan ke StageStatManager)
    // Dipanggil oleh script musuh / EnemyDeathHandler melalui referensi StageManager.
    // =====================================================================

    public bool HasAttackTokenRuntimeData(GameObject enemy)
        => statManager.HasAttackTokenRuntimeData(enemy);

    public int GetCurrentConcurrentAttackTokenLimit()
        => statManager.GetCurrentConcurrentAttackTokenLimit();

    public int GetActiveConcurrentAttackTokenCount()
        => statManager.GetActiveConcurrentAttackTokenCount();

    public int GetAvailableConcurrentAttackTokenCount()
        => statManager.GetAvailableConcurrentAttackTokenCount();

    public int GetRemainingAttackTokensForEnemy(GameObject enemy, int fallbackValue = 0)
        => statManager.GetRemainingAttackTokensForEnemy(enemy, fallbackValue);

    public bool CanEnemyAcquireMinionAttackToken(GameObject enemy)
        => statManager.CanEnemyAcquireMinionAttackToken(enemy);

    public bool TryAcquireMinionAttackToken(GameObject enemy, string source = "Minion Attack")
        => statManager.TryAcquireMinionAttackToken(enemy, source);

    public void ReleaseMinionAttackToken(GameObject enemy, string source = "Attack Finished")
        => statManager.ReleaseMinionAttackToken(enemy, source);

    public bool TryConsumeAttackTokenForEnemy(GameObject enemy, float damageAmount = 0f, string source = "Direct Attack")
        => statManager.TryConsumeAttackTokenForEnemy(enemy, damageAmount, source);

    public void FinalizeAttackTokenConsumptionForEnemy(GameObject enemy)
        => statManager.FinalizeAttackTokenConsumptionForEnemy(enemy);
}