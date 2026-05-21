using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// Revisi: attack token berfungsi sebagai batas jumlah minion yang boleh menyerang bersamaan.
public class StageManager : MonoBehaviour
{
    public enum StageState
    {
        SpawningMinions,
        FightingMinions,
        FightingBoss,
        StageCleared
    }

    [HideInInspector] public StageState currentState;

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

    [Header("Concurrent Minion Attack Token Progression")]
    [Tooltip("Jika aktif, batas token awal dihitung dari baseSpawnToken * initialTokenRatioFromBaseMinion, lalu dibulatkan.")]
    public bool deriveBaseMinionAttackTokenFromBaseMinionRatio = true;
    [Range(0f, 2f)]
    public float initialTokenRatioFromBaseMinion = 0.7f;
    [Tooltip("Batas dasar jumlah minion yang boleh menyerang secara bersamaan. Digunakan jika deriveBaseMinionAttackTokenFromBaseMinionRatio = false.")]
    public int baseMinionAttackToken = 2;
    [Tooltip("Kenaikan batas jumlah minion yang boleh menyerang secara bersamaan setiap stage.")]
    public int minionAttackTokenIncreasePerStage = 2;

    [Header("Minion Variant Ratio")]
    [Range(0f, 1f)]
    public float dominantVariantRatio = 0.6f;

    [Header("DDA Snapshot Timing")]
    public bool finalizeDataBeforeBoss = true;
    public bool finalizeDataAfterBoss = true;

    [Header("DDA Reset Timing")]
    public bool resetDDAAndDataTrackerOnStageCleared = true;

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

    [Header("Concurrent Attack Token Runtime")]
    [Tooltip("Jika true, minion tetap boleh menyerang ketika data runtime tidak ditemukan. Aktifkan untuk mencegah prefab lama terkunci total.")]
    [SerializeField] private bool allowAttackWhenRuntimeDataMissing = true;
    [Tooltip("Jeda minimal penulisan debug ketika token penuh agar panel tidak terlalu ramai.")]
    [SerializeField] private float attackTokenDeniedDebugCooldown = 0.25f;

    [Header("Visual Debug Overlay")]
    public bool showStageRuntimeDebug = true;
    public bool showEnemyWorldDebugLabels = true;
    public KeyCode toggleDebugOverlayKey = KeyCode.F6;
    public Vector2 debugPanelPosition = new Vector2(16f, 16f);
    public float debugPanelWidth = 430f;
    public int maxEnemyRowsInDebugPanel = 14;
    public int maxFloatingDebugLines = 12;
    public float floatingDebugLineDuration = 8f;

    private int currentStage;
    private int activeEnemiesCount = 0;
    private bool isChangingStage = false;
    private bool isTransitioningToBoss = false;
    private bool bossSpawnConfigurationFailed = false;

    // Runtime boss yang benar adalah instance hasil Instantiate, bukan prefab asset di Project.
    private GameObject currentBossObject;
    private CharacterBase currentBossCharacter;
    private bool bossDefeatHandled = false;

    private readonly List<StageEnemyRuntimeDebugData> enemyRuntimeDebugData = new List<StageEnemyRuntimeDebugData>();
    private readonly List<StageFloatingDebugLine> floatingDebugLines = new List<StageFloatingDebugLine>();

    private CharacterBase playerCharacterCache;
    private float previousPlayerHP = -1f;
    private int lastStageTotalMinions = 0;
    private int lastStageMeleeCount = 0;
    private int lastStageRangeCount = 0;
    private int lastStageMinionAttackTokens = 0;
    private float lastStageStatMultiplier = 1f;
    private string lastStagePlaystyle = "Balanced";
    private float lastPlayerRegenAmount = 0f;
    private string lastTokenDebugMessage = "Belum ada pemakaian token serangan bersamaan.";
    private int activeConcurrentAttackTokens = 0;
    private int currentConcurrentAttackTokenLimit = 0;
    private float lastAttackTokenDeniedDebugTime = -999f;

    private GUIStyle debugPanelStyle;
    private GUIStyle debugHeaderStyle;
    private GUIStyle debugTextStyle;
    private GUIStyle debugWarningStyle;
    private GUIStyle worldDebugLabelStyle;

    private void Start()
    {
        currentStage = startingStageNumber;
        activeEnemiesCount = 0;
        isChangingStage = false;
        isTransitioningToBoss = false;
        bossSpawnConfigurationFailed = false;

        SetBlackScreenInstant(0f, false);
        EnsureBossUIReference();

        EnsurePlayerReference();
        CachePlayerHealthForTokenDebug();

        StartStage();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleDebugOverlayKey))
        {
            showStageRuntimeDebug = !showStageRuntimeDebug;
            showEnemyWorldDebugLabels = showStageRuntimeDebug;
        }

        // --- REVISI: Cek apakah musuh sudah mati semua ---
        if (currentState == StageState.FightingMinions || currentState == StageState.FightingBoss)
        {
            ValidateEnemyCount();
        }

        CheckStageTransition();
        CheckMinionWaveClearedFallback();
        CheckBossDefeatedFallback();
        RefreshConcurrentAttackTokenDebugState();
        PruneRuntimeDebugData();

    }


    private void ValidateEnemyCount()
    {
        // Mencari objek secara langsung yang masih memiliki tag "Enemy" atau "Boss"
        GameObject[] activeEnemies = GameObject.FindGameObjectsWithTag("Enemy");
        int totalActive = activeEnemies.Length;

        // Jika tidak ada lagi musuh dengan tag tersebut (sudah di-untag atau destroy)
        if (totalActive <= 0 && currentState != StageState.StageCleared)
        {
            currentState = StageState.StageCleared;
            Debug.Log("[StageManager] Semua musuh mati! State -> StageCleared. Berjalanlah ke titik X: " + rightTransitionX);
        }
    }


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

    private void EnsurePlayerReference()
    {
        if (playerTransform == null)
        {
            try
            {
                GameObject existingPlayer = GameObject.FindGameObjectWithTag("Player");

                if (existingPlayer != null)
                {
                    playerTransform = existingPlayer.transform;
                }
            }
            catch (UnityException)
            {
                Debug.LogWarning("[STAGE MANAGER] Tag Player belum dibuat di Project Settings > Tags and Layers.");
            }
        }

        if (playerTransform == null)
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
                "[STAGE MANAGER] Player dibuat dari prefab: " +
                selectedPlayerPrefab.name +
                " | Weapon: " +
                selectedPlayerWeapon
            );
        }

        AssignPlayerReferenceToRuntimeSystems();
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

        if (DataTracker.Instance != null)
        {
            DataTracker.Instance.SetPlayerTransform(playerTransform);
        }
        else
        {
            Debug.LogWarning("[STAGE MANAGER] DataTracker.Instance belum tersedia.");
        }
    }

    private void StartStage()
    {
        Debug.Log("--- MEMULAI STAGE " + GetDisplayedStageNumber() + " ---");

        activeEnemiesCount = 0;
        isTransitioningToBoss = false;
        bossSpawnConfigurationFailed = false;
        currentBossObject = null;
        currentBossCharacter = null;
        bossDefeatHandled = false;
        enemyRuntimeDebugData.Clear();
        activeConcurrentAttackTokens = 0;
        currentConcurrentAttackTokenLimit = 0;
        lastAttackTokenDeniedDebugTime = -999f;
        lastTokenDebugMessage = "Belum ada minion yang memakai token serangan pada stage ini.";

        if (bossHPBarUI != null)
        {
            bossHPBarUI.SetBossPrefabs(bossPrefabSword, bossPrefabBow);
            bossHPBarUI.Hide();
        }

        currentState = StageState.SpawningMinions;
        AddFloatingDebugLine($"[STAGE] Mulai Stage {GetDisplayedStageNumber()}.");
        StartCoroutine(SpawnMinionWave());
    }

    private int GetDisplayedStageNumber()
    {
        return GetStageProgressionIndex() + 1;
    }

    private int GetStageProgressionIndex()
    {
        return Mathf.Max(0, currentStage - startingStageNumber);
    }

    private int CalculateTotalMinions(int stageProgressionIndex)
    {
        return Mathf.Max(0, baseSpawnToken + (spawnTokenIncreasePerStage * stageProgressionIndex));
    }

    // Token di sini adalah kapasitas global: jumlah maksimal minion yang boleh menyerang bersamaan.
    private int CalculateMinionAttackTokens(int stageProgressionIndex)
    {
        int resolvedBaseMinionAttackToken = deriveBaseMinionAttackTokenFromBaseMinionRatio
            ? Mathf.RoundToInt(baseSpawnToken * initialTokenRatioFromBaseMinion)
            : baseMinionAttackToken;

        return Mathf.Max(
            0,
            resolvedBaseMinionAttackToken + (minionAttackTokenIncreasePerStage * stageProgressionIndex)
        );
    }

    private IEnumerator SpawnMinionWave()
    {
        if (minionSpawnDelayOnStageStart > 0f)
        {
            Debug.Log(
                $"[STAGE MANAGER] Stage {GetDisplayedStageNumber()} dimulai. " +
                $"Menunggu {minionSpawnDelayOnStageStart} detik sebelum spawn minion."
            );

            yield return new WaitForSeconds(minionSpawnDelayOnStageStart);
        }

        int stageProgressionIndex = GetStageProgressionIndex();
        int totalMinions = CalculateTotalMinions(stageProgressionIndex);
        int minionAttackTokens = CalculateMinionAttackTokens(stageProgressionIndex);
        float statMultiplier = 1f + (statAmplifyPerStage * stageProgressionIndex);

        int meleeCount = 0;
        int rangeCount = 0;

        string playerPlaystyle = GetPlayerPlaystyleFromDDA();

        lastStageTotalMinions = totalMinions;
        lastStageMeleeCount = 0;
        lastStageRangeCount = 0;
        lastStageMinionAttackTokens = minionAttackTokens;
        currentConcurrentAttackTokenLimit = minionAttackTokens;
        activeConcurrentAttackTokens = 0;
        lastStageStatMultiplier = statMultiplier;
        lastStagePlaystyle = playerPlaystyle;
        lastTokenDebugMessage = $"Token aktif {activeConcurrentAttackTokens}/{currentConcurrentAttackTokenLimit}.";

        if (playerPlaystyle == "OffensiveDominant")
        {
            meleeCount = Mathf.RoundToInt(totalMinions * dominantVariantRatio);
            rangeCount = totalMinions - meleeCount;
        }
        else if (playerPlaystyle == "DefensiveDominant")
        {
            rangeCount = Mathf.RoundToInt(totalMinions * dominantVariantRatio);
            meleeCount = totalMinions - rangeCount;
        }
        else
        {
            meleeCount = Mathf.RoundToInt(totalMinions * 0.5f);
            rangeCount = totalMinions - meleeCount;
        }

        lastStageMeleeCount = meleeCount;
        lastStageRangeCount = rangeCount;

        AddFloatingDebugLine(
            $"[STAT] Stage {GetDisplayedStageNumber()} | Stat minion x{statMultiplier:0.00} " +
            $"(+{GetStatIncreasePercent(statMultiplier):0.#}%)."
        );
        AddFloatingDebugLine(
            $"[TOKEN] Batas minion menyerang bersamaan: {minionAttackTokens}."
        );

        Debug.Log(
            $"Spawn: {totalMinions} Minions " +
            $"({meleeCount} Melee, {rangeCount} Range). " +
            $"Concurrent Attack Token Limit: {minionAttackTokens}. Stat Mult: {statMultiplier}x"
        );

        // State dipindahkan sebelum instantiate agar OnEnemyDied tidak terlewat
        // bila ada musuh yang mati sangat cepat setelah dibuat.
        currentState = StageState.FightingMinions;

        int spawnedCount = 0;

        for (int i = 0; i < meleeCount; i++)
        {
            if (SpawnEnemy(meleeMinionPrefab, statMultiplier, minionAttackTokens))
                spawnedCount++;
        }

        for (int i = 0; i < rangeCount; i++)
        {
            if (SpawnEnemy(rangeMinionPrefab, statMultiplier, minionAttackTokens))
                spawnedCount++;
        }

        if (spawnedCount <= 0)
        {
            Debug.LogWarning(
                "[STAGE MANAGER] Tidak ada minion yang berhasil dibuat. " +
                "Boss akan langsung dicoba untuk dimunculkan."
            );

            TryStartBossTransition("Tidak ada minion yang berhasil dibuat.");
        }
    }

    private bool SpawnEnemy(GameObject prefab, float statMultiplier, int attackTokens)
    {
        if (prefab == null)
        {
            Debug.LogWarning("[STAGE MANAGER] Prefab enemy belum diisi.");
            return false;
        }

        if (minionSpawnPoints == null || minionSpawnPoints.Count == 0)
        {
            Debug.LogWarning("[STAGE MANAGER] Tidak ada minion spawn point.");
            return false;
        }

        Transform spawnPoint = minionSpawnPoints[UnityEngine.Random.Range(0, minionSpawnPoints.Count)];
        GameObject enemy = Instantiate(prefab, spawnPoint.position, Quaternion.identity);

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

        StatsSnapshot beforeStats = CaptureStats(character);
        InitializeCharacterBaseStats(character, statMultiplier);
        StatsSnapshot afterStats = CaptureStats(character);

        InitializeStageCombatController(enemy, character, attackTokens, false);
        InitializeDeathHandler(enemy);
        RegisterRuntimeEnemyDebug(enemy, character, attackTokens, statMultiplier, beforeStats, afterStats, false);

        activeEnemiesCount++;

        Debug.Log(
            $"[STAGE MANAGER] Spawn minion: {enemy.name} | " +
            $"HP: {character.currentHP}/{character.maxHP} | " +
            $"Attack: {character.attack} | " +
            $"MoveSpeed: {character.moveSpeed} | " +
            $"ConcurrentAttackTokenLimit: {attackTokens} | " +
            $"ActiveCount: {activeEnemiesCount}"
        );

        return true;
    }

    private void InitializeCharacterBaseStats(CharacterBase character, float statMultiplier)
    {
        if (character == null)
            return;

        statMultiplier = Mathf.Max(0.01f, statMultiplier);

        character.maxHP *= statMultiplier;
        character.currentHP = character.maxHP;

        character.attack *= statMultiplier;
        character.defense *= statMultiplier;
        character.moveSpeed *= statMultiplier;
    }

    private void InitializeStageCombatController(GameObject enemy, CharacterBase character, int attackTokens, bool isBoss = false)
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

    private void InitializeDeathHandler(GameObject enemy)
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

    private void SetLayerRecursively(GameObject obj, int layer)
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

    public void OnEnemyDied()
    {
        if (currentState == StageState.StageCleared)
            return;

        activeEnemiesCount = Mathf.Max(0, activeEnemiesCount - 1);

        Debug.Log(
            $"[STAGE MANAGER] OnEnemyDied terpanggil. " +
            $"State: {currentState} | Sisa activeEnemiesCount: {activeEnemiesCount}"
        );

        if (currentState == StageState.FightingMinions)
        {
            if (activeEnemiesCount <= 0 || CountAliveEnemyObjectsInScene() <= 0)
            {
                TryStartBossTransition("Semua minion terdeteksi kalah.");
            }
        }
        else if (currentState == StageState.FightingBoss)
        {
            // Beberapa prefab boss hanya memanggil OnEnemyDied(), tetapi objeknya tidak langsung Destroy.
            // Karena itu, validasi utama tetap memakai HP/runtime boss yang sedang dilawan.
            if (IsCurrentBossDefeatedForProgression())
            {
                HandleBossDefeated();
            }
            else if (activeEnemiesCount <= 0)
            {
                // Pengaman jika event kematian minion lama terlambat masuk ketika boss sudah aktif.
                activeEnemiesCount = 1;
            }
        }
    }

    private void CheckMinionWaveClearedFallback()
    {
        if (currentState != StageState.FightingMinions)
            return;

        if (isTransitioningToBoss || bossSpawnConfigurationFailed)
            return;

        if (activeEnemiesCount <= 0)
        {
            TryStartBossTransition("activeEnemiesCount sudah 0.");
            return;
        }

        int aliveEnemies = CountAliveEnemyObjectsInScene();

        if (aliveEnemies <= 0)
        {
            Debug.LogWarning(
                "[STAGE MANAGER] Fallback mendeteksi tidak ada Enemy hidup di scene, " +
                "walaupun activeEnemiesCount belum 0. Kemungkinan EnemyDeathHandler tidak memanggil OnEnemyDied()."
            );

            activeEnemiesCount = 0;
            TryStartBossTransition("Fallback scan mendeteksi semua minion telah kalah.");
        }
    }

    private int CountAliveEnemyObjectsInScene()
    {
        GameObject[] enemies;

        try
        {
            enemies = GameObject.FindGameObjectsWithTag("Enemy");
        }
        catch (UnityException)
        {
            Debug.LogWarning("[STAGE MANAGER] Tag Enemy belum dibuat di Project Settings > Tags and Layers.");
            return activeEnemiesCount;
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

    private void CheckBossDefeatedFallback()
    {
        if (currentState != StageState.FightingBoss)
            return;

        if (bossDefeatHandled)
            return;

        if (IsCurrentBossDefeatedForProgression())
        {
            Debug.LogWarning(
                "[STAGE MANAGER] Fallback mendeteksi boss sudah kalah. " +
                "Stage akan dipaksa menjadi StageCleared."
            );

            activeEnemiesCount = 0;
            HandleBossDefeated();
        }
    }

    private bool IsCurrentBossDefeatedForProgression()
    {
        if (currentBossObject == null)
            return true;

        if (currentBossCharacter == null)
        {
            currentBossCharacter = currentBossObject.GetComponent<CharacterBase>();

            if (currentBossCharacter == null)
                currentBossCharacter = currentBossObject.GetComponentInChildren<CharacterBase>(true);
        }

        if (currentBossCharacter == null)
            return false;

        return currentBossCharacter.currentHP <= 0f;
    }

    private void TryStartBossTransition(string reason)
    {
        if (currentState != StageState.FightingMinions)
            return;

        if (isTransitioningToBoss || bossSpawnConfigurationFailed)
            return;

        int aliveEnemies = CountAliveEnemyObjectsInScene();

        if (activeEnemiesCount > 0 && aliveEnemies > 0)
            return;

        ReleaseAllConcurrentAttackTokens("Transisi ke boss");
        activeEnemiesCount = 0;
        isTransitioningToBoss = true;

        Debug.Log($"[STAGE MANAGER] Transisi ke boss dimulai. Alasan: {reason}");
        StartCoroutine(TransitionToBoss());
    }

    private IEnumerator TransitionToBoss()
    {
        if (finalizeDataBeforeBoss)
        {
            Debug.Log("DDA Data Difinalisasi Sebelum Boss");

            if (DataTracker.Instance != null)
            {
                DataTracker.Instance.FinalizeStageData();
            }
        }

        yield return new WaitForSeconds(1f);

        string dominantWeapon = GetDominantWeaponFromDDA();
        GameObject bossToSpawn = dominantWeapon == "Bow" ? bossPrefabBow : bossPrefabSword;
        float statMultiplier = 1f + (statAmplifyPerStage * GetStageProgressionIndex());

        Debug.Log($"Boss membaca DDA: Menirukan senjata dominan player yaitu {dominantWeapon}");

        if (bossToSpawn == null)
        {
            bossSpawnConfigurationFailed = true;
            isTransitioningToBoss = false;

            if (bossHPBarUI != null)
            {
                bossHPBarUI.Hide();
            }

            Debug.LogError(
                "[STAGE MANAGER] Boss prefab belum diisi. " +
                "Isi bossPrefabSword dan bossPrefabBow di Inspector."
            );
            yield break;
        }

        if (bossSpawnPoint == null)
        {
            bossSpawnConfigurationFailed = true;
            isTransitioningToBoss = false;

            if (bossHPBarUI != null)
            {
                bossHPBarUI.Hide();
            }

            Debug.LogError(
                "[STAGE MANAGER] Boss spawn point belum diisi. " +
                "Isi bossSpawnPoint di Inspector."
            );
            yield break;
        }

        GameObject boss = Instantiate(bossToSpawn, bossSpawnPoint.position, Quaternion.identity);
        boss.tag = "Enemy";
        SetLayerRecursively(boss, LayerMask.NameToLayer("Enemy"));

        CharacterBase bossCharacter = boss.GetComponent<CharacterBase>();

        if (bossCharacter == null)
        {
            bossCharacter = boss.GetComponentInChildren<CharacterBase>(true);
        }

        currentBossObject = boss;
        currentBossCharacter = bossCharacter;
        bossDefeatHandled = false;
        activeEnemiesCount = 1;
        currentState = StageState.FightingBoss;
        isTransitioningToBoss = false;

        if (bossCharacter != null)
        {
            StatsSnapshot beforeStats = CaptureStats(bossCharacter);
            InitializeCharacterBaseStats(bossCharacter, statMultiplier);
            StatsSnapshot afterStats = CaptureStats(bossCharacter);

            InitializeStageCombatController(boss, bossCharacter, 999, true);
            InitializeDeathHandler(boss);
            RegisterRuntimeEnemyDebug(boss, bossCharacter, 999, statMultiplier, beforeStats, afterStats, true);

            if (bossHPBarUI != null)
            {
                bossHPBarUI.ShowBossByPrefab(bossToSpawn, boss, bossCharacter);
            }
        }
        else
        {
            Debug.LogWarning("[STAGE MANAGER] Boss tidak memiliki CharacterBase atau turunannya.");
            InitializeDeathHandler(boss);

            if (bossHPBarUI != null)
            {
                bossHPBarUI.Hide();
            }
        }

        Debug.Log(
            "[STAGE MANAGER] BOSS MUNCUL. " +
            $"Prefab: {boss.name} | DominantWeapon: {dominantWeapon} | ActiveCount: {activeEnemiesCount}"
        );
    }

    private void HandleBossDefeated()
    {
        if (bossDefeatHandled)
            return;

        bossDefeatHandled = true;
        activeEnemiesCount = 0;
        ReleaseAllConcurrentAttackTokens("Boss kalah");

        // PENTING: boss sering masih menyisakan collider ketika animasi mati berjalan.
        // Jika collider tetap aktif, player dapat tertahan dan tidak pernah mencapai rightTransitionX.
        PrepareDefeatedBossForStageTransition();

        if (finalizeDataAfterBoss)
        {
            Debug.Log("DDA Data Difinalisasi Setelah Boss");

            if (DataTracker.Instance != null)
            {
                DataTracker.Instance.FinalizeStageData();
            }
        }

        if (bossHPBarUI != null)
        {
            bossHPBarUI.Hide();
        }

        currentBossCharacter = null;
        currentBossObject = null;
        currentState = StageState.StageCleared;

        if (resetDDAAndDataTrackerOnStageCleared)
        {
            ResetDDAAndDataTracker();
        }

        Debug.Log(
            "STAGE CLEAR! Boss sudah kalah. " +
            $"State sekarang: {currentState}. Player harus melewati X >= {rightTransitionX:0.##} untuk lanjut stage."
        );
    }

    private void PrepareDefeatedBossForStageTransition()
    {
        if (currentBossObject == null)
            return;

        if (untagBossOnDefeat)
        {
            try
            {
                currentBossObject.tag = "Untagged";
            }
            catch (UnityException)
            {
                // Diabaikan; tag Untagged selalu ada, tetapi try-catch tetap aman untuk prefab lama.
            }
        }

        if (!disableBossCollidersOnDefeat)
            return;

        Collider2D[] colliders2D = currentBossObject.GetComponentsInChildren<Collider2D>(true);

        foreach (Collider2D collider2D in colliders2D)
        {
            if (collider2D != null)
                collider2D.enabled = false;
        }

        Collider[] colliders3D = currentBossObject.GetComponentsInChildren<Collider>(true);

        foreach (Collider collider3D in colliders3D)
        {
            if (collider3D != null)
                collider3D.enabled = false;
        }
    }

    private void CheckStageTransition()
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

        StartStage();

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

    private CharacterBase GetPlayerCharacter()
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

    private void RegeneratePlayerHealthForNextStage()
    {
        lastPlayerRegenAmount = 0f;

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
        lastPlayerRegenAmount = playerCharacter.currentHP - beforeHP;
        previousPlayerHP = playerCharacter.currentHP;

        AddFloatingDebugLine(
            $"[REGEN] Player +{lastPlayerRegenAmount:0.#} HP " +
            $"({beforeHP:0.#} -> {playerCharacter.currentHP:0.#}/{playerCharacter.maxHP:0.#})."
        );

        Debug.Log(
            $"[STAGE MANAGER] Player regen setelah next stage: " +
            $"+{lastPlayerRegenAmount:0.##} HP | " +
            $"{beforeHP:0.##} -> {playerCharacter.currentHP:0.##}/{playerCharacter.maxHP:0.##}"
        );
    }

    private void ReleaseAllConcurrentAttackTokens(string source)
    {
        bool changed = false;

        foreach (StageEnemyRuntimeDebugData data in enemyRuntimeDebugData)
        {
            if (data == null || data.isBoss)
                continue;

            if (!data.isHoldingConcurrentAttackToken)
                continue;

            data.isHoldingConcurrentAttackToken = false;
            changed = true;
        }

        if (changed)
        {
            activeConcurrentAttackTokens = 0;
            lastTokenDebugMessage = $"Semua token serangan dilepas. Sumber: {source}.";
            AddFloatingDebugLine($"[TOKEN RESET] {source}.");
            SyncAllConcurrentAttackTokenRuntimeToEnemies();
        }
        else
        {
            activeConcurrentAttackTokens = 0;
        }
    }

    private void RefreshConcurrentAttackTokenDebugState()
    {
        int countedTokens = CountActiveConcurrentAttackTokens();

        if (countedTokens != activeConcurrentAttackTokens)
        {
            activeConcurrentAttackTokens = countedTokens;
            SyncAllConcurrentAttackTokenRuntimeToEnemies();
        }
    }

    private int CountActiveConcurrentAttackTokens()
    {
        int count = 0;

        foreach (StageEnemyRuntimeDebugData data in enemyRuntimeDebugData)
        {
            if (data == null || data.isBoss)
                continue;

            if (!data.isHoldingConcurrentAttackToken)
                continue;

            if (IsValidAliveRuntimeEnemy(data))
            {
                count++;
            }
        }

        return count;
    }

    public bool HasAttackTokenRuntimeData(GameObject enemy)
    {
        return FindRuntimeDebugDataForEnemy(enemy) != null;
    }

    public int GetCurrentConcurrentAttackTokenLimit()
    {
        return Mathf.Max(0, currentConcurrentAttackTokenLimit);
    }

    public int GetActiveConcurrentAttackTokenCount()
    {
        RefreshConcurrentAttackTokenDebugState();
        return Mathf.Max(0, activeConcurrentAttackTokens);
    }

    public int GetAvailableConcurrentAttackTokenCount()
    {
        RefreshConcurrentAttackTokenDebugState();
        return Mathf.Max(0, currentConcurrentAttackTokenLimit - activeConcurrentAttackTokens);
    }

    // Nama lama dipertahankan sebagai compatibility bridge untuk script yang sebelumnya membaca sisa token.
    // Nilai yang dikembalikan sekarang adalah slot serangan global yang masih kosong, bukan stok token per minion.
    public int GetRemainingAttackTokensForEnemy(GameObject enemy, int fallbackValue = 0)
    {
        StageEnemyRuntimeDebugData data = FindRuntimeDebugDataForEnemy(enemy);

        if (data != null && data.isHoldingConcurrentAttackToken)
            return Mathf.Max(0, currentConcurrentAttackTokenLimit - activeConcurrentAttackTokens + 1);

        return GetAvailableConcurrentAttackTokenCount();
    }

    public bool CanEnemyAcquireMinionAttackToken(GameObject enemy)
    {
        StageEnemyRuntimeDebugData data = FindRuntimeDebugDataForEnemy(enemy);

        if (data == null)
            return allowAttackWhenRuntimeDataMissing;

        if (data.isBoss)
            return true;

        if (!IsValidAliveRuntimeEnemy(data))
            return false;

        if (data.isHoldingConcurrentAttackToken)
            return true;

        RefreshConcurrentAttackTokenDebugState();
        return activeConcurrentAttackTokens < currentConcurrentAttackTokenLimit;
    }

    public bool TryAcquireMinionAttackToken(GameObject enemy, string source = "Minion Attack")
    {
        StageEnemyRuntimeDebugData data = FindRuntimeDebugDataForEnemy(enemy);

        if (data == null)
        {
            string enemyName = enemy != null ? enemy.name : "Enemy tidak diketahui";
            lastTokenDebugMessage = allowAttackWhenRuntimeDataMissing
                ? $"{enemyName}: data runtime tidak ditemukan, serangan diizinkan sebagai fallback."
                : $"{enemyName}: data runtime tidak ditemukan, serangan ditolak.";

            if (Time.time - lastAttackTokenDeniedDebugTime >= attackTokenDeniedDebugCooldown)
            {
                AddFloatingDebugLine($"[TOKEN WARNING] {lastTokenDebugMessage}");
                lastAttackTokenDeniedDebugTime = Time.time;
            }

            return allowAttackWhenRuntimeDataMissing;
        }

        if (data.isBoss)
        {
            lastTokenDebugMessage = $"{data.enemy.name}: boss bypass token serangan minion.";
            return true;
        }

        if (!IsValidAliveRuntimeEnemy(data))
        {
            lastTokenDebugMessage = $"{data.enemy.name}: tidak valid atau sudah mati, token tidak diberikan.";
            return false;
        }

        if (data.isHoldingConcurrentAttackToken)
        {
            return true;
        }

        RefreshConcurrentAttackTokenDebugState();

        if (currentConcurrentAttackTokenLimit <= 0)
        {
            lastTokenDebugMessage = $"{data.enemy.name}: token stage bernilai 0, serangan ditolak.";
            return false;
        }

        if (activeConcurrentAttackTokens >= currentConcurrentAttackTokenLimit)
        {
            data.lastTokenDeniedTime = Time.time;
            lastTokenDebugMessage =
                $"{data.enemy.name}: menunggu token kosong " +
                $"({activeConcurrentAttackTokens}/{currentConcurrentAttackTokenLimit} sedang dipakai).";

            if (Time.time - lastAttackTokenDeniedDebugTime >= attackTokenDeniedDebugCooldown)
            {
                AddFloatingDebugLine(
                    $"[TOKEN PENUH] {data.enemy.name} menunggu slot " +
                    $"({activeConcurrentAttackTokens}/{currentConcurrentAttackTokenLimit})."
                );
                lastAttackTokenDeniedDebugTime = Time.time;
            }

            SyncConcurrentAttackTokenToEnemy(data);
            return false;
        }

        data.isHoldingConcurrentAttackToken = true;
        data.lastTokenConsumedTime = Time.time;
        activeConcurrentAttackTokens = Mathf.Clamp(activeConcurrentAttackTokens + 1, 0, currentConcurrentAttackTokenLimit);

        lastTokenDebugMessage =
            $"{data.enemy.name}: memakai token serangan " +
            $"({activeConcurrentAttackTokens}/{currentConcurrentAttackTokenLimit}) | Sumber: {source}.";

        AddFloatingDebugLine(
            $"[TOKEN AMBIL] {data.enemy.name} " +
            $"({activeConcurrentAttackTokens}/{currentConcurrentAttackTokenLimit})."
        );

        Debug.Log(
            $"[STAGE MANAGER] Token serangan bersamaan diambil: {data.enemy.name} | " +
            $"Aktif: {activeConcurrentAttackTokens}/{currentConcurrentAttackTokenLimit} | Source: {source}"
        );

        SyncAllConcurrentAttackTokenRuntimeToEnemies();
        return true;
    }

    public void ReleaseMinionAttackToken(GameObject enemy, string source = "Attack Finished")
    {
        StageEnemyRuntimeDebugData data = FindRuntimeDebugDataForEnemy(enemy);

        if (data == null || data.isBoss)
            return;

        ReleaseMinionAttackToken(data, source);
    }

    private void ReleaseMinionAttackToken(StageEnemyRuntimeDebugData data, string source)
    {
        if (data == null || data.isBoss)
            return;

        if (!data.isHoldingConcurrentAttackToken)
            return;

        data.isHoldingConcurrentAttackToken = false;
        activeConcurrentAttackTokens = Mathf.Max(0, activeConcurrentAttackTokens - 1);

        lastTokenDebugMessage =
            $"{data.enemy.name}: melepas token serangan " +
            $"({activeConcurrentAttackTokens}/{currentConcurrentAttackTokenLimit}) | Sumber: {source}.";

        AddFloatingDebugLine(
            $"[TOKEN LEPAS] {data.enemy.name} " +
            $"({activeConcurrentAttackTokens}/{currentConcurrentAttackTokenLimit})."
        );

        Debug.Log(
            $"[STAGE MANAGER] Token serangan bersamaan dilepas: {data.enemy.name} | " +
            $"Aktif: {activeConcurrentAttackTokens}/{currentConcurrentAttackTokenLimit} | Source: {source}"
        );

        SyncAllConcurrentAttackTokenRuntimeToEnemies();
    }

    // Compatibility bridge untuk script lama yang sebelumnya memanggil consume/finalize.
    // Pada sistem baru, consume = acquire slot, finalize = release slot.
    public bool TryConsumeAttackTokenForEnemy(GameObject enemy, float damageAmount = 0f, string source = "Direct Attack")
    {
        return TryAcquireMinionAttackToken(enemy, source);
    }

    public void FinalizeAttackTokenConsumptionForEnemy(GameObject enemy)
    {
        ReleaseMinionAttackToken(enemy, "FinalizeAttackTokenConsumptionForEnemy");
    }

    private StageEnemyRuntimeDebugData FindRuntimeDebugDataForEnemy(GameObject enemy)
    {
        if (enemy == null)
            return null;

        Transform enemyTransform = enemy.transform;

        foreach (StageEnemyRuntimeDebugData data in enemyRuntimeDebugData)
        {
            if (data == null || data.enemy == null)
                continue;

            if (data.enemy == enemy)
                return data;

            Transform registeredTransform = data.enemy.transform;

            if (enemyTransform.IsChildOf(registeredTransform) || registeredTransform.IsChildOf(enemyTransform))
                return data;
        }

        return null;
    }

    private void SyncConcurrentAttackTokenToEnemy(StageEnemyRuntimeDebugData data)
    {
        if (data == null || data.enemy == null || data.isBoss)
            return;

        SyncStageAttackTokenToEnemyComponents(
            data.enemy,
            data.isHoldingConcurrentAttackToken,
            activeConcurrentAttackTokens,
            currentConcurrentAttackTokenLimit
        );
    }

    private void SyncAllConcurrentAttackTokenRuntimeToEnemies()
    {
        foreach (StageEnemyRuntimeDebugData data in enemyRuntimeDebugData)
        {
            if (data == null || data.isBoss || data.enemy == null)
                continue;

            SyncConcurrentAttackTokenToEnemy(data);
        }
    }

    private bool SyncStageAttackTokenToEnemyComponents(
        GameObject enemy,
        bool isUsingToken,
        int activeTokens,
        int tokenLimit
    )
    {
        if (enemy == null)
            return false;

        bool changed = false;
        activeTokens = Mathf.Max(0, activeTokens);
        tokenLimit = Mathf.Max(0, tokenLimit);

        MonoBehaviour[] behaviours = enemy.GetComponentsInChildren<MonoBehaviour>(true);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null)
                continue;

            Type behaviourType = behaviour.GetType();

            changed |= TryInvokeBoolIntIntMethod(
                behaviour,
                behaviourType,
                "SetStageAttackTokenRuntime",
                isUsingToken,
                activeTokens,
                tokenLimit
            );

            changed |= TryInvokeBoolIntIntMethod(
                behaviour,
                behaviourType,
                "SetConcurrentAttackTokenRuntime",
                isUsingToken,
                activeTokens,
                tokenLimit
            );

            changed |= TrySetBoolMember(behaviour, behaviourType, "hasActiveAttackToken", isUsingToken);
            changed |= TrySetBoolMember(behaviour, behaviourType, "hasReservedAttackToken", isUsingToken);
            changed |= TrySetBoolMember(behaviour, behaviourType, "isHoldingAttackToken", isUsingToken);

            changed |= TrySetIntMember(behaviour, behaviourType, "attackTokens", tokenLimit);
            changed |= TrySetIntMember(behaviour, behaviourType, "stageAttackTokenLimit", tokenLimit);
            changed |= TrySetIntMember(behaviour, behaviourType, "attackTokenCapacityInStage", tokenLimit);
            changed |= TrySetIntMember(behaviour, behaviourType, "activeAttackersInStage", activeTokens);
            changed |= TrySetIntMember(behaviour, behaviourType, "concurrentAttackTokenCapacity", tokenLimit);
            changed |= TrySetIntMember(behaviour, behaviourType, "activeConcurrentAttackTokens", activeTokens);
        }

        return changed;
    }

    private bool TryDisableAttackThroughReflection(GameObject enemy)
    {
        if (enemy == null)
            return false;

        bool changed = false;
        MonoBehaviour[] behaviours = enemy.GetComponentsInChildren<MonoBehaviour>(true);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null)
                continue;

            Type behaviourType = behaviour.GetType();
            string typeName = behaviourType.Name.ToLowerInvariant();

            if (!typeName.Contains("attack") &&
                !typeName.Contains("combat") &&
                !typeName.Contains("node") &&
                !typeName.Contains("enemy"))
            {
                continue;
            }

            changed |= TryInvokeAttackDisableMethod(behaviour, behaviourType, "SetCanAttack", false);
            changed |= TryInvokeAttackDisableMethod(behaviour, behaviourType, "SetAttackEnabled", false);
            changed |= TryInvokeAttackDisableMethod(behaviour, behaviourType, "SetCanUseAttack", false);
            changed |= TryInvokeAttackDisableMethod(behaviour, behaviourType, "DisableAttack", false);
            changed |= TryInvokeAttackDisableMethod(behaviour, behaviourType, "StopAttacking", false);

            changed |= TrySetBoolMember(behaviour, behaviourType, "canAttack", false);
            changed |= TrySetBoolMember(behaviour, behaviourType, "CanAttack", false);
            changed |= TrySetBoolMember(behaviour, behaviourType, "attackEnabled", false);
            changed |= TrySetBoolMember(behaviour, behaviourType, "isAttackEnabled", false);
            changed |= TrySetBoolMember(behaviour, behaviourType, "canUseAttack", false);
        }

        return changed;
    }

    private bool TryInvokeAttackDisableMethod(MonoBehaviour target, Type targetType, string methodName, bool value)
    {
        MethodInfo method = targetType.GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (method == null)
            return false;

        ParameterInfo[] parameters = method.GetParameters();

        try
        {
            if (parameters.Length == 0)
            {
                method.Invoke(target, null);
                return true;
            }

            if (parameters.Length == 1 && parameters[0].ParameterType == typeof(bool))
            {
                method.Invoke(target, new object[] { value });
                return true;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"[STAGE MANAGER] Gagal memanggil {targetType.Name}.{methodName}(): {exception.Message}"
            );
        }

        return false;
    }

    private bool TryInvokeIntMethod(MonoBehaviour target, Type targetType, string methodName, int value)
    {
        MethodInfo method = targetType.GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (method == null)
            return false;

        ParameterInfo[] parameters = method.GetParameters();

        if (parameters.Length != 1 || parameters[0].ParameterType != typeof(int))
            return false;

        try
        {
            method.Invoke(target, new object[] { value });
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"[STAGE MANAGER] Gagal memanggil {targetType.Name}.{methodName}(int): {exception.Message}"
            );
            return false;
        }
    }

    private bool TryInvokeBoolIntIntMethod(
        MonoBehaviour target,
        Type targetType,
        string methodName,
        bool boolValue,
        int firstIntValue,
        int secondIntValue
    )
    {
        MethodInfo method = targetType.GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (method == null)
            return false;

        ParameterInfo[] parameters = method.GetParameters();

        if (parameters.Length != 3 ||
            parameters[0].ParameterType != typeof(bool) ||
            parameters[1].ParameterType != typeof(int) ||
            parameters[2].ParameterType != typeof(int))
        {
            return false;
        }

        try
        {
            method.Invoke(target, new object[] { boolValue, firstIntValue, secondIntValue });
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"[STAGE MANAGER] Gagal memanggil {targetType.Name}.{methodName}(bool, int, int): {exception.Message}"
            );
            return false;
        }
    }

    private bool TrySetIntMember(MonoBehaviour target, Type targetType, string memberName, int value)
    {
        bool changed = false;

        FieldInfo field = targetType.GetField(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (field != null && field.FieldType == typeof(int))
        {
            try
            {
                field.SetValue(target, value);
                changed = true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[STAGE MANAGER] Gagal mengubah field int {targetType.Name}.{memberName}: {exception.Message}"
                );
            }
        }

        PropertyInfo property = targetType.GetProperty(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (property != null && property.PropertyType == typeof(int) && property.CanWrite)
        {
            try
            {
                property.SetValue(target, value, null);
                changed = true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[STAGE MANAGER] Gagal mengubah property int {targetType.Name}.{memberName}: {exception.Message}"
                );
            }
        }

        return changed;
    }

    private bool TrySetBoolMember(MonoBehaviour target, Type targetType, string memberName, bool value)
    {
        bool changed = false;

        FieldInfo field = targetType.GetField(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (field != null && field.FieldType == typeof(bool))
        {
            try
            {
                field.SetValue(target, value);
                changed = true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[STAGE MANAGER] Gagal mengubah field {targetType.Name}.{memberName}: {exception.Message}"
                );
            }
        }

        PropertyInfo property = targetType.GetProperty(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (property != null && property.PropertyType == typeof(bool) && property.CanWrite)
        {
            try
            {
                property.SetValue(target, value, null);
                changed = true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[STAGE MANAGER] Gagal mengubah property {targetType.Name}.{memberName}: {exception.Message}"
                );
            }
        }

        return changed;
    }

    private void RegisterRuntimeEnemyDebug(
        GameObject enemy,
        CharacterBase character,
        int attackTokens,
        float statMultiplier,
        StatsSnapshot beforeStats,
        StatsSnapshot afterStats,
        bool isBoss
    )
    {
        if (enemy == null || character == null)
            return;

        StageEnemyRuntimeDebugData data = new StageEnemyRuntimeDebugData
        {
            enemy = enemy,
            character = character,
            isBoss = isBoss,
            initialAttackTokens = Mathf.Max(0, attackTokens),
            remainingAttackTokens = Mathf.Max(0, attackTokens),
            isHoldingConcurrentAttackToken = false,
            lastTokenDeniedTime = -999f,
            statMultiplier = statMultiplier,
            beforeStats = beforeStats,
            afterStats = afterStats,
            originalAttack = character.attack,
            lastTokenConsumedTime = -999f,
            tokenExhaustedEffectApplied = false,
            pendingTokenExhaustedEffect = false
        };

        enemyRuntimeDebugData.Add(data);
        SyncConcurrentAttackTokenToEnemy(data);
    }

    private bool IsValidAliveRuntimeEnemy(StageEnemyRuntimeDebugData data)
    {
        if (data == null || data.enemy == null || data.character == null)
            return false;

        if (!data.enemy.activeInHierarchy)
            return false;

        return data.character.currentHP > 0f;
    }

    private void PruneRuntimeDebugData()
    {
        for (int i = enemyRuntimeDebugData.Count - 1; i >= 0; i--)
        {
            StageEnemyRuntimeDebugData data = enemyRuntimeDebugData[i];

            if (data == null)
            {
                enemyRuntimeDebugData.RemoveAt(i);
                continue;
            }

            bool shouldReleaseToken = data.isHoldingConcurrentAttackToken && !data.isBoss;
            bool shouldRemove = data.enemy == null || !IsValidAliveRuntimeEnemy(data);

            if (shouldRemove)
            {
                if (shouldReleaseToken)
                {
                    data.isHoldingConcurrentAttackToken = false;
                    activeConcurrentAttackTokens = Mathf.Max(0, activeConcurrentAttackTokens - 1);
                    SyncAllConcurrentAttackTokenRuntimeToEnemies();
                }

                if (data.enemy == null)
                {
                    enemyRuntimeDebugData.RemoveAt(i);
                }
            }
        }

        float now = Time.time;

        for (int i = floatingDebugLines.Count - 1; i >= 0; i--)
        {
            if (now - floatingDebugLines[i].createdAt > floatingDebugLineDuration)
            {
                floatingDebugLines.RemoveAt(i);
            }
        }
    }

    private StatsSnapshot CaptureStats(CharacterBase character)
    {
        if (character == null)
            return new StatsSnapshot();

        return new StatsSnapshot
        {
            maxHP = character.maxHP,
            currentHP = character.currentHP,
            attack = character.attack,
            defense = character.defense,
            moveSpeed = character.moveSpeed
        };
    }

    private float GetStatIncreasePercent(float statMultiplier)
    {
        return (statMultiplier - 1f) * 100f;
    }

    private void AddFloatingDebugLine(string message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        floatingDebugLines.Add(new StageFloatingDebugLine
        {
            message = message,
            createdAt = Time.time
        });

        while (floatingDebugLines.Count > maxFloatingDebugLines)
        {
            floatingDebugLines.RemoveAt(0);
        }
    }

    private void OnGUI()
    {
        if (!showStageRuntimeDebug && !showEnemyWorldDebugLabels)
            return;

        InitializeDebugGUIStyles();

        if (showStageRuntimeDebug)
        {
            DrawStageDebugPanel();
        }

        if (showEnemyWorldDebugLabels)
        {
            DrawEnemyWorldDebugLabels();
        }
    }

    private void InitializeDebugGUIStyles()
    {
        if (debugTextStyle != null)
            return;

        debugPanelStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.UpperLeft,
            padding = new RectOffset(12, 12, 10, 10)
        };

        debugHeaderStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        debugTextStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            normal = { textColor = Color.white },
            wordWrap = true
        };

        debugWarningStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.yellow },
            wordWrap = true
        };

        worldDebugLabelStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white },
            wordWrap = true
        };
    }

    private void DrawStageDebugPanel()
    {
        float panelHeight = Mathf.Min(Screen.height - debugPanelPosition.y - 12f, 520f);

        GUILayout.BeginArea(
            new Rect(debugPanelPosition.x, debugPanelPosition.y, debugPanelWidth, panelHeight),
            debugPanelStyle
        );

        GUILayout.Label("STAGE RUNTIME DEBUG", debugHeaderStyle);
        GUILayout.Label($"Toggle: {toggleDebugOverlayKey} | State: {currentState}", debugTextStyle);
        GUILayout.Space(4f);

        GUILayout.Label($"Stage Tampil: {GetDisplayedStageNumber()} | Stage Internal: {currentStage}", debugTextStyle);
        GUILayout.Label($"Playstyle DDA: {lastStagePlaystyle}", debugTextStyle);
        GUILayout.Label(
            $"Spawn Token: {lastStageTotalMinions} | Melee: {lastStageMeleeCount} | Range: {lastStageRangeCount}",
            debugTextStyle
        );
        GUILayout.Label(
            $"Stat Bertambah: x{lastStageStatMultiplier:0.00} (+{GetStatIncreasePercent(lastStageStatMultiplier):0.#}%)",
            debugWarningStyle
        );
        GUILayout.Label(
            $"Token Serangan Bersamaan: {activeConcurrentAttackTokens}/{lastStageMinionAttackTokens} aktif",
            debugWarningStyle
        );

        CharacterBase playerCharacter = GetPlayerCharacter();

        if (playerCharacter != null)
        {
            GUILayout.Label(
                $"Player HP: {playerCharacter.currentHP:0.#}/{playerCharacter.maxHP:0.#} | Regen Terakhir: +{lastPlayerRegenAmount:0.#}",
                debugTextStyle
            );
        }
        else
        {
            GUILayout.Label("Player HP: CharacterBase tidak ditemukan.", debugWarningStyle);
        }

        GUILayout.Label(
            $"Makna Token: batas minion yang boleh berada di state Attack secara bersamaan",
            debugTextStyle
        );
        GUILayout.Label($"Token Debug: {lastTokenDebugMessage}", debugTextStyle);
        GUILayout.Space(6f);

        GUILayout.Label("Enemy Runtime", debugHeaderStyle);

        int shownRows = 0;

        foreach (StageEnemyRuntimeDebugData data in enemyRuntimeDebugData)
        {
            if (!IsValidAliveRuntimeEnemy(data))
                continue;

            if (shownRows >= maxEnemyRowsInDebugPanel)
            {
                GUILayout.Label("...enemy lain disembunyikan karena batas baris debug.", debugTextStyle);
                break;
            }

            string enemyType = data.isBoss ? "BOSS" : "MINION";
            string tokenText = data.isBoss
                ? "Token: boss bypass"
                : data.isHoldingConcurrentAttackToken
                    ? $"Token: ATTACKING ({activeConcurrentAttackTokens}/{currentConcurrentAttackTokenLimit})"
                    : $"Token: waiting/free ({activeConcurrentAttackTokens}/{currentConcurrentAttackTokenLimit})";

            GUILayout.Label(
                $"{enemyType} {data.enemy.name} | {tokenText} | HP {data.character.currentHP:0.#}/{data.character.maxHP:0.#} | ATK {data.character.attack:0.#}",
                data.isHoldingConcurrentAttackToken && !data.isBoss ? debugWarningStyle : debugTextStyle
            );
            GUILayout.Label(
                $"  Stat naik: HP {data.beforeStats.maxHP:0.#}->{data.afterStats.maxHP:0.#} | " +
                $"ATK {data.beforeStats.attack:0.#}->{data.afterStats.attack:0.#} | " +
                $"DEF {data.beforeStats.defense:0.#}->{data.afterStats.defense:0.#} | " +
                $"SPD {data.beforeStats.moveSpeed:0.#}->{data.afterStats.moveSpeed:0.#}",
                debugTextStyle
            );

            shownRows++;
        }

        if (shownRows == 0)
        {
            GUILayout.Label("Tidak ada enemy hidup yang terdaftar.", debugTextStyle);
        }

        GUILayout.Space(6f);
        GUILayout.Label("Floating Debug", debugHeaderStyle);

        foreach (StageFloatingDebugLine line in floatingDebugLines)
        {
            float age = Time.time - line.createdAt;
            GUILayout.Label($"[{age:0.0}s] {line.message}", debugTextStyle);
        }

        GUILayout.EndArea();
    }

    private void DrawEnemyWorldDebugLabels()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
            return;

        foreach (StageEnemyRuntimeDebugData data in enemyRuntimeDebugData)
        {
            if (!IsValidAliveRuntimeEnemy(data))
                continue;

            Vector3 worldPosition = data.enemy.transform.position + Vector3.up * 1.7f;
            Vector3 screenPosition = mainCamera.WorldToScreenPoint(worldPosition);

            if (screenPosition.z < 0f)
                continue;

            float labelWidth = data.isBoss ? 190f : 160f;
            float labelHeight = data.isBoss ? 112f : 122f;
            float x = screenPosition.x - (labelWidth * 0.5f);
            float y = Screen.height - screenPosition.y - labelHeight;

            string labelText;

            if (data.isBoss)
            {
                labelText =
                    $"BOSS\n" +
                    $"HP {data.character.currentHP:0.#}/{data.character.maxHP:0.#}\n" +
                    $"Stat x{data.statMultiplier:0.00} (+{GetStatIncreasePercent(data.statMultiplier):0.#}%)\n" +
                    $"HP+ {data.beforeStats.maxHP:0.#}->{data.afterStats.maxHP:0.#}\n" +
                    $"ATK {data.beforeStats.attack:0.#}->{data.character.attack:0.#}";
            }
            else
            {
                labelText =
                    $"MINION\n" +
                    $"Token {(data.isHoldingConcurrentAttackToken ? "ATTACKING" : "READY/WAIT")}\n" +
                    $"Slot {activeConcurrentAttackTokens}/{currentConcurrentAttackTokenLimit}\n" +
                    $"HP {data.character.currentHP:0.#}/{data.character.maxHP:0.#}\n" +
                    $"Stat x{data.statMultiplier:0.00} (+{GetStatIncreasePercent(data.statMultiplier):0.#}%)\n" +
                    $"HP+ {data.beforeStats.maxHP:0.#}->{data.afterStats.maxHP:0.#}\n" +
                    $"ATK {data.beforeStats.attack:0.#}->{data.character.attack:0.#}";
            }

            GUI.Label(new Rect(x, y, labelWidth, labelHeight), labelText, worldDebugLabelStyle);
        }
    }

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

        resetSkillDebugMethod.Invoke(DataTracker.Instance, null);

        Debug.Log("[STAGE MANAGER] Skill debug player direset untuk stage berikutnya.");
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

            method.Invoke(instance, null);

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
            Type[] types = assembly.GetTypes();

            foreach (Type type in types)
            {
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

    private class StageEnemyRuntimeDebugData
    {
        public GameObject enemy;
        public CharacterBase character;
        public bool isBoss;
        public int initialAttackTokens;
        public int remainingAttackTokens;
        public bool isHoldingConcurrentAttackToken;
        public float lastTokenDeniedTime;
        public float statMultiplier;
        public StatsSnapshot beforeStats;
        public StatsSnapshot afterStats;
        public float originalAttack;
        public float lastTokenConsumedTime;
        public bool tokenExhaustedEffectApplied;
        public bool pendingTokenExhaustedEffect;
    }

    private struct StatsSnapshot
    {
        public float maxHP;
        public float currentHP;
        public float attack;
        public float defense;
        public float moveSpeed;
    }

    private struct StageFloatingDebugLine
    {
        public string message;
        public float createdAt;
    }

    private string GetPlayerPlaystyleFromDDA()
    {
        if (DDAController.Instance != null)
        {
            return DDAController.Instance.currentPlayerPlaystyle.ToString();
        }

        if (currentStage == 0)
            return "Balanced";

        return "OffensiveDominant";
    }

    private string GetDominantWeaponFromDDA()
    {
        if (DDAController.Instance != null)
        {
            if (DDAController.Instance.currentPlayerDominantWeapon == WeaponType.Bow)
                return "Bow";

            if (DDAController.Instance.currentPlayerDominantWeapon == WeaponType.Sword)
                return "Sword";
        }

        return "Sword";
    }
}
