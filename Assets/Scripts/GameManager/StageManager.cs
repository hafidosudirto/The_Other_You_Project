using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

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

    [Header("Progression Formula")]
    public int startingStageNumber = 0;
    public int baseSpawnToken = 3;
    public int spawnTokenIncreasePerStage = 1;
    public float statAmplifyPerStage = 0.1f;

    [Header("Minion Attack Token Progression")]
    public bool deriveBaseMinionAttackTokenFromBaseMinionRatio = true;
    [Range(0f, 2f)]
    public float initialTokenRatioFromBaseMinion = 0.7f;
    public int baseMinionAttackToken = 2;
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

    [Header("Minion Attack Token Runtime Debug")]
    public bool enforceMinionAttackTokenFallback = true;
    [SerializeField] private float tokenHitDetectionRadius = 4f;
    [SerializeField] private bool tokenFallbackUseNearestEnemyIfNoEnemyInRange = true;
    [SerializeField] private bool setMinionAttackToZeroWhenTokenEmpty = true;
    [SerializeField] private bool useReflectionToDisableAttackWhenTokenEmpty = true;
    [SerializeField] private float tokenConsumptionCooldown = 0.15f;

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
    private string lastTokenDebugMessage = "Belum ada konsumsi token.";
    private float lastDirectTokenConsumptionTime = -999f;

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

        CheckStageTransition();
        CheckMinionWaveClearedFallback();
        CheckMinionAttackTokenConsumptionFallback();
        PruneRuntimeDebugData();
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
        enemyRuntimeDebugData.Clear();
        lastTokenDebugMessage = "Belum ada konsumsi token pada stage ini.";
        lastDirectTokenConsumptionTime = -999f;

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
        lastStageStatMultiplier = statMultiplier;
        lastStagePlaystyle = playerPlaystyle;

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
            $"[TOKEN] Setiap minion mendapat {minionAttackTokens} token serangan."
        );

        Debug.Log(
            $"Spawn: {totalMinions} Minions " +
            $"({meleeCount} Melee, {rangeCount} Range). " +
            $"Token: {minionAttackTokens}. Stat Mult: {statMultiplier}x"
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
            $"Token: {attackTokens} | " +
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
            if (activeEnemiesCount <= 0)
            {
                HandleBossDefeated();
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

    private void TryStartBossTransition(string reason)
    {
        if (currentState != StageState.FightingMinions)
            return;

        if (isTransitioningToBoss || bossSpawnConfigurationFailed)
            return;

        int aliveEnemies = CountAliveEnemyObjectsInScene();

        if (activeEnemiesCount > 0 && aliveEnemies > 0)
            return;

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
            bossCharacter = boss.GetComponentInChildren<CharacterBase>();
        }

        if (bossCharacter != null)
        {
            StatsSnapshot beforeStats = CaptureStats(bossCharacter);
            InitializeCharacterBaseStats(bossCharacter, statMultiplier);
            StatsSnapshot afterStats = CaptureStats(bossCharacter);

            InitializeStageCombatController(boss, bossCharacter, 999, true);
            RegisterRuntimeEnemyDebug(boss, bossCharacter, 999, statMultiplier, beforeStats, afterStats, true);
        }
        else
        {
            Debug.LogWarning("[STAGE MANAGER] Boss tidak memiliki CharacterBase atau turunannya.");
        }

        InitializeDeathHandler(boss);

        activeEnemiesCount = 1;
        currentState = StageState.FightingBoss;
        isTransitioningToBoss = false;

        Debug.Log(
            "[STAGE MANAGER] BOSS MUNCUL. " +
            $"Prefab: {boss.name} | DominantWeapon: {dominantWeapon} | ActiveCount: {activeEnemiesCount}"
        );
    }

    private void HandleBossDefeated()
    {
        if (finalizeDataAfterBoss)
        {
            Debug.Log("DDA Data Difinalisasi Setelah Boss");
        }

        currentState = StageState.StageCleared;

        if (resetDDAAndDataTrackerOnStageCleared)
        {
            ResetDDAAndDataTracker();
        }

        Debug.Log("STAGE CLEAR! DDA dan DataTracker direset. Berjalanlah ke kanan untuk lanjut.");
    }

    private void CheckStageTransition()
    {
        if (isChangingStage)
            return;

        if (currentState == StageState.StageCleared &&
            playerTransform != null &&
            playerTransform.position.x >= rightTransitionX)
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

    private void CheckMinionAttackTokenConsumptionFallback()
    {
        CharacterBase playerCharacter = GetPlayerCharacter();

        if (playerCharacter == null)
        {
            previousPlayerHP = -1f;
            return;
        }

        if (previousPlayerHP < 0f)
        {
            previousPlayerHP = playerCharacter.currentHP;
            return;
        }

        float hpLoss = previousPlayerHP - playerCharacter.currentHP;

        if (Time.time - lastDirectTokenConsumptionTime < tokenConsumptionCooldown)
        {
            previousPlayerHP = playerCharacter.currentHP;
            return;
        }

        if (enforceMinionAttackTokenFallback &&
            currentState == StageState.FightingMinions &&
            hpLoss > 0.001f)
        {
            bool consumed = TryConsumeLikelyMinionAttackToken(hpLoss);

            if (!consumed)
            {
                lastTokenDebugMessage =
                    $"HP player turun {hpLoss:0.##}, tetapi minion pemilik token tidak ditemukan.";

                AddFloatingDebugLine("[TOKEN] HP player turun, tetapi sumber minion tidak terdeteksi.");
            }
        }

        previousPlayerHP = playerCharacter.currentHP;
    }

    private bool TryConsumeLikelyMinionAttackToken(float hpLoss)
    {
        if (playerTransform == null)
            return false;

        StageEnemyRuntimeDebugData selectedEnemy = null;
        float selectedDistance = float.MaxValue;
        bool selectedIsInsideRadius = false;

        foreach (StageEnemyRuntimeDebugData data in enemyRuntimeDebugData)
        {
            if (!IsValidAliveRuntimeEnemy(data))
                continue;

            if (data.isBoss)
                continue;

            if (data.remainingAttackTokens <= 0)
                continue;

            if (Time.time - data.lastTokenConsumedTime < tokenConsumptionCooldown)
                continue;

            float distance = Vector3.Distance(playerTransform.position, data.enemy.transform.position);
            bool insideRadius = distance <= tokenHitDetectionRadius;

            if (!insideRadius && !tokenFallbackUseNearestEnemyIfNoEnemyInRange)
                continue;

            if (insideRadius)
            {
                if (!selectedIsInsideRadius || distance < selectedDistance)
                {
                    selectedEnemy = data;
                    selectedDistance = distance;
                    selectedIsInsideRadius = true;
                }
            }
            else if (!selectedIsInsideRadius && distance < selectedDistance)
            {
                selectedEnemy = data;
                selectedDistance = distance;
            }
        }

        if (selectedEnemy == null)
            return false;

        ConsumeAttackToken(selectedEnemy, hpLoss, $"Fallback HP | Distance: {selectedDistance:0.##}", true);

        return true;
    }

    public bool HasAttackTokenRuntimeData(GameObject enemy)
    {
        return FindRuntimeDebugDataForEnemy(enemy) != null;
    }

    public int GetRemainingAttackTokensForEnemy(GameObject enemy, int fallbackValue = 0)
    {
        StageEnemyRuntimeDebugData data = FindRuntimeDebugDataForEnemy(enemy);

        if (data == null)
            return Mathf.Max(0, fallbackValue);

        if (data.isBoss)
            return data.initialAttackTokens;

        return Mathf.Max(0, data.remainingAttackTokens);
    }

    public bool TryConsumeAttackTokenForEnemy(GameObject enemy, float damageAmount = 0f, string source = "Direct Attack")
    {
        StageEnemyRuntimeDebugData data = FindRuntimeDebugDataForEnemy(enemy);

        if (data == null)
        {
            string enemyName = enemy != null ? enemy.name : "Enemy tidak diketahui";
            lastTokenDebugMessage = $"{enemyName}: data token tidak ditemukan di StageManager.";
            AddFloatingDebugLine($"[TOKEN WARNING] {enemyName} belum terdaftar di StageManager.");
            return false;
        }

        if (data.isBoss)
        {
            lastTokenDebugMessage = $"{data.enemy.name}: boss bypass token.";
            return true;
        }

        if (!IsValidAliveRuntimeEnemy(data))
        {
            lastTokenDebugMessage = $"{data.enemy.name}: tidak valid atau sudah mati, token tidak dikonsumsi.";
            return false;
        }

        if (data.remainingAttackTokens <= 0)
        {
            SyncRuntimeAttackTokenToEnemy(data);
            ApplyAttackTokenExhaustedEffect(data);
            lastTokenDebugMessage = $"{data.enemy.name}: token sudah habis, serangan ditolak.";
            AddFloatingDebugLine($"[TOKEN DITOLAK] {data.enemy.name} mencoba menyerang saat token habis.");
            return false;
        }

        lastDirectTokenConsumptionTime = Time.time;
        ConsumeAttackToken(data, damageAmount, source, false);
        return true;
    }

    public void FinalizeAttackTokenConsumptionForEnemy(GameObject enemy)
    {
        StageEnemyRuntimeDebugData data = FindRuntimeDebugDataForEnemy(enemy);

        if (data == null || data.isBoss)
            return;

        if (data.remainingAttackTokens <= 0)
        {
            data.pendingTokenExhaustedEffect = false;
            ApplyAttackTokenExhaustedEffect(data);
        }
    }

    private void ConsumeAttackToken(
        StageEnemyRuntimeDebugData data,
        float damageAmount,
        string source,
        bool applyExhaustedImmediately
    )
    {
        if (data == null)
            return;

        data.remainingAttackTokens = Mathf.Max(0, data.remainingAttackTokens - 1);
        data.lastTokenConsumedTime = Time.time;
        data.pendingTokenExhaustedEffect = data.remainingAttackTokens <= 0;

        SyncRuntimeAttackTokenToEnemy(data);

        string damageText = damageAmount > 0.001f
            ? $" | Damage: {damageAmount:0.##}"
            : string.Empty;

        lastTokenDebugMessage =
            $"{data.enemy.name}: token {data.remainingAttackTokens}/" +
            $"{data.initialAttackTokens}{damageText} | Sumber: {source}.";

        AddFloatingDebugLine(
            $"[TOKEN] {data.enemy.name} memakai 1 token " +
            $"({data.remainingAttackTokens}/{data.initialAttackTokens})."
        );

        Debug.Log(
            $"[STAGE MANAGER] Token minion berkurang: {data.enemy.name} | " +
            $"Sisa Token: {data.remainingAttackTokens}/{data.initialAttackTokens} | " +
            $"Damage: {damageAmount:0.##} | Source: {source}"
        );

        if (data.remainingAttackTokens <= 0 && applyExhaustedImmediately)
        {
            data.pendingTokenExhaustedEffect = false;
            ApplyAttackTokenExhaustedEffect(data);
        }
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

    private void ApplyAttackTokenExhaustedEffect(StageEnemyRuntimeDebugData data)
    {
        if (data == null || data.tokenExhaustedEffectApplied)
            return;

        data.tokenExhaustedEffectApplied = true;
        data.remainingAttackTokens = 0;
        SyncRuntimeAttackTokenToEnemy(data);

        if (data.character != null && setMinionAttackToZeroWhenTokenEmpty)
        {
            data.character.attack = 0f;
        }

        bool reflectionDisabledSomething = false;

        if (useReflectionToDisableAttackWhenTokenEmpty && data.enemy != null)
        {
            reflectionDisabledSomething = TryDisableAttackThroughReflection(data.enemy);
        }

        string reflectionStatus = reflectionDisabledSomething
            ? "Komponen serangan juga dinonaktifkan melalui reflection."
            : "Serangan dinetralkan melalui nilai attack CharacterBase.";

        lastTokenDebugMessage =
            $"{data.enemy.name}: TOKEN HABIS. {reflectionStatus}";

        AddFloatingDebugLine($"[TOKEN HABIS] {data.enemy.name} tidak boleh menyerang lagi.");

        Debug.Log(
            $"[STAGE MANAGER] Token habis pada {data.enemy.name}. {reflectionStatus}"
        );
    }

    private void SyncRuntimeAttackTokenToEnemy(StageEnemyRuntimeDebugData data)
    {
        if (data == null || data.enemy == null || data.isBoss)
            return;

        SyncAttackTokenToEnemyComponents(data.enemy, data.remainingAttackTokens);
    }

    private bool SyncAttackTokenToEnemyComponents(GameObject enemy, int remainingTokens)
    {
        if (enemy == null)
            return false;

        bool changed = false;
        remainingTokens = Mathf.Max(0, remainingTokens);

        MonoBehaviour[] behaviours = enemy.GetComponentsInChildren<MonoBehaviour>(true);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null)
                continue;

            Type behaviourType = behaviour.GetType();

            changed |= TryInvokeIntMethod(behaviour, behaviourType, "SetRemainingAttackTokens", remainingTokens);
            changed |= TryInvokeIntMethod(behaviour, behaviourType, "SetAttackTokens", remainingTokens);
            changed |= TrySetIntMember(behaviour, behaviourType, "attackTokens", remainingTokens);
            changed |= TrySetIntMember(behaviour, behaviourType, "remainingAttackTokens", remainingTokens);
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
            statMultiplier = statMultiplier,
            beforeStats = beforeStats,
            afterStats = afterStats,
            originalAttack = character.attack,
            lastTokenConsumedTime = -999f,
            tokenExhaustedEffectApplied = false,
            pendingTokenExhaustedEffect = false
        };

        enemyRuntimeDebugData.Add(data);
        SyncRuntimeAttackTokenToEnemy(data);
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

            if (data == null || data.enemy == null)
            {
                enemyRuntimeDebugData.RemoveAt(i);
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
            $"Token Serangan Minion: {lastStageMinionAttackTokens} per minion",
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
            $"Fallback Token: {(enforceMinionAttackTokenFallback ? "ON" : "OFF")} | Radius: {tokenHitDetectionRadius:0.#}",
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
                : $"Token: {data.remainingAttackTokens}/{data.initialAttackTokens}";

            GUILayout.Label(
                $"{enemyType} {data.enemy.name} | {tokenText} | HP {data.character.currentHP:0.#}/{data.character.maxHP:0.#} | ATK {data.character.attack:0.#}",
                data.remainingAttackTokens <= 0 && !data.isBoss ? debugWarningStyle : debugTextStyle
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
            float labelHeight = data.isBoss ? 112f : 106f;
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
                    $"Token {data.remainingAttackTokens}/{data.initialAttackTokens}\n" +
                    $"HP {data.character.currentHP:0.#}/{data.character.maxHP:0.#}\n" +
                    $"Stat x{data.statMultiplier:0.00} (+{GetStatIncreasePercent(data.statMultiplier):0.#}%)\n" +
                    $"HP+ {data.beforeStats.maxHP:0.#}->{data.afterStats.maxHP:0.#}\n" +
                    $"ATK {data.beforeStats.attack:0.#}->{data.character.attack:0.#}";

                if (data.remainingAttackTokens <= 0)
                {
                    labelText += "\nTOKEN HABIS";
                }
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
