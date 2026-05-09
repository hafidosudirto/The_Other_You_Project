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

    [Header("Minion Prefabs")]
    public GameObject meleeMinionPrefab;
    public GameObject rangeMinionPrefab;

    [Header("Minion Spawn Points")]
    public List<Transform> minionSpawnPoints;

    [Header("Boss")]
    public GameObject bossPrefabSword;
    public GameObject bossPrefabBow;
    public Transform bossSpawnPoint;

    [Header("Boss Spawn Debug")]
    [Tooltip("Aktifkan sementara bila ingin memaksa boss bow keluar tanpa menunggu hasil DDA.")]
    [SerializeField] private bool forceBowBossForDebug = false;

    [Tooltip("Jika DDA memilih Bow tetapi prefab bow kosong, StageManager boleh memakai boss sword agar stage tidak macet.")]
    [SerializeField] private bool allowSwordFallbackIfBowMissing = true;

    [Tooltip("Jika DDA memilih Sword tetapi prefab sword kosong, StageManager boleh memakai boss bow agar stage tidak macet.")]
    [SerializeField] private bool allowBowFallbackIfSwordMissing = true;

    [Tooltip("Biarkan boss tetap spawn walaupun CharacterBase tidak ditemukan. Ini membantu debugging prefab boss.")]
    [SerializeField] private bool allowBossSpawnWithoutCharacterBase = true;

    [Tooltip("Tampilkan log rinci agar penyebab boss tidak muncul terlihat jelas di Console.")]
    [SerializeField] private bool verboseBossSpawnLog = true;

    [Header("Progression Formula")]
    public int startingStageNumber = 0;
    public int baseSpawnToken = 2;
    public int spawnTokenIncreasePerStage = 1;
    public float statAmplifyPerStage = 0.1f;
    public int baseMinionAttackToken = 3;
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

    [Header("Enemy Tracking Recovery")]
    [Tooltip("Aktifkan agar StageManager tetap dapat mendeteksi musuh mati walaupun EnemyDeathHandler/OnEnemyDied tidak terpanggil.")]
    [SerializeField] private bool useEnemyTrackingWatchdog = true;

    [Tooltip("Jeda pemeriksaan ulang daftar musuh aktif. Nilai kecil membantu debugging spawn boss.")]
    [SerializeField] private float enemyTrackingCheckInterval = 0.25f;

    [Tooltip("Jika CharacterBase.currentHP <= 0, musuh dianggap sudah mati walaupun objek belum Destroy karena masih memainkan animasi mati.")]
    [SerializeField] private bool treatZeroHpAsDead = true;

    [Tooltip("Pasang komponen tracker runtime pada setiap musuh yang di-spawn StageManager.")]
    [SerializeField] private bool attachRuntimeEnemyTracker = true;

    private int currentStage;
    private int activeEnemiesCount = 0;
    private bool isChangingStage = false;
    private bool isTransitioningToBoss = false;
    private readonly List<GameObject> trackedEnemies = new List<GameObject>();
    private float nextEnemyTrackingCheckTime = 0f;
    private bool suppressEnemyTrackerCallbacks = false;

    private void Start()
    {
        currentStage = startingStageNumber;
        activeEnemiesCount = 0;
        isChangingStage = false;
        isTransitioningToBoss = false;

        SetBlackScreenInstant(0f, false);

        StartStage();
    }

    private void Update()
    {
        CheckStageTransition();
        UpdateEnemyTrackingWatchdog();
    }

    private void StartStage()
    {
        Debug.Log("--- MEMULAI STAGE " + GetDisplayedStageNumber() + " ---");

        activeEnemiesCount = 0;
        isTransitioningToBoss = false;
        trackedEnemies.Clear();
        nextEnemyTrackingCheckTime = 0f;
        currentState = StageState.SpawningMinions;

        StartCoroutine(SpawnMinionWave());
    }

    private int GetDisplayedStageNumber()
    {
        return currentStage - startingStageNumber + 1;
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

        int totalMinions = baseSpawnToken + (spawnTokenIncreasePerStage * currentStage);
        int minionAttackTokens = baseMinionAttackToken + (minionAttackTokenIncreasePerStage * currentStage);
        float statMultiplier = 1f + (statAmplifyPerStage * currentStage);

        totalMinions = Mathf.Max(0, totalMinions);
        statMultiplier = Mathf.Max(0.01f, statMultiplier);

        int meleeCount = 0;
        int rangeCount = 0;

        string playerPlaystyle = GetPlayerPlaystyleFromDDA();

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

        Debug.Log(
            $"[STAGE MANAGER] Spawn: {totalMinions} Minions " +
            $"({meleeCount} Melee, {rangeCount} Range). " +
            $"Token: {minionAttackTokens}. Stat Mult: {statMultiplier}x. " +
            $"Playstyle DDA: {playerPlaystyle}"
        );

        for (int i = 0; i < meleeCount; i++)
        {
            SpawnEnemy(meleeMinionPrefab, statMultiplier, minionAttackTokens);
        }

        for (int i = 0; i < rangeCount; i++)
        {
            SpawnEnemy(rangeMinionPrefab, statMultiplier, minionAttackTokens);
        }

        currentState = StageState.FightingMinions;
        ReconcileTrackedEnemies(true);

        Debug.Log(
            $"[STAGE MANAGER] Wave minion selesai dibuat. " +
            $"Enemy aktif yang terdaftar: {activeEnemiesCount}. State: {currentState}"
        );

        if (activeEnemiesCount <= 0)
        {
            Debug.LogWarning(
                "[STAGE MANAGER] Tidak ada minion aktif setelah wave dibuat. " +
                "StageManager langsung mencoba transisi ke boss."
            );

            StartBossTransitionIfNeeded();
        }

        yield return null;
    }

    private void SpawnEnemy(GameObject prefab, float statMultiplier, int attackTokens)
    {
        if (prefab == null)
        {
            Debug.LogWarning("[STAGE MANAGER] Prefab enemy belum diisi.");
            return;
        }

        if (minionSpawnPoints == null || minionSpawnPoints.Count == 0)
        {
            Debug.LogWarning("[STAGE MANAGER] Tidak ada minion spawn point.");
            return;
        }

        Transform spawnPoint = minionSpawnPoints[UnityEngine.Random.Range(0, minionSpawnPoints.Count)];
        if (spawnPoint == null)
        {
            Debug.LogWarning("[STAGE MANAGER] Salah satu minion spawn point bernilai null.");
            return;
        }

        GameObject enemy = Instantiate(prefab, spawnPoint.position, Quaternion.identity);
        enemy.SetActive(true);

        PrepareSpawnedEnemyObject(enemy);

        CharacterBase character = enemy.GetComponent<CharacterBase>();

        if (character == null)
        {
            Debug.LogWarning(
                $"[STAGE MANAGER] Prefab minion {prefab.name} tidak memiliki CharacterBase atau turunan Enemy.cs. " +
                "Objek minion dihancurkan agar penghitung stage tidak macet."
            );

            Destroy(enemy);
            return;
        }

        InitializeCharacterBaseStats(character, statMultiplier);

        InitializeStageCombatController(enemy, character, attackTokens, false);
        InitializeDeathHandler(enemy);
        RegisterSpawnedEnemy(enemy);

        Debug.Log(
            $"[STAGE MANAGER] Spawn minion: {enemy.name} | " +
            $"HP: {character.currentHP}/{character.maxHP} | " +
            $"Attack: {character.attack} | " +
            $"MoveSpeed: {character.moveSpeed} | " +
            $"Token: {attackTokens} | " +
            $"Enemy aktif: {activeEnemiesCount}"
        );
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
        if (enemy == null)
            return;

        if (character == null)
        {
            Debug.LogWarning(
                $"[STAGE MANAGER] {enemy.name} tidak memiliki CharacterBase. " +
                "StageManager tetap mencoba menginisialisasi AI agar objek tidak gagal spawn diam-diam."
            );
        }

        NodeManager nodeManager = enemy.GetComponent<NodeManager>();
        MinionMeleeController meleeMinion = enemy.GetComponent<MinionMeleeController>();

        if (nodeManager != null)
        {
            nodeManager.InitializeStageEnemy(character, attackTokens, isBoss);
        }
        else if (meleeMinion != null)
        {
            meleeMinion.InitializeStageEnemy(character, attackTokens, isBoss);
        }
        else
        {
            Debug.LogWarning(
                $"[STAGE MANAGER] {enemy.name} tidak memiliki sistem AI " +
                "(NodeManager atau MinionMeleeController). Normalisasi DDA gagal diterapkan."
            );
        }
    }

    private void InitializeDeathHandler(GameObject enemy)
    {
        if (enemy == null)
            return;

        EnemyDeathHandler deathHandler = enemy.GetComponent<EnemyDeathHandler>();

        if (deathHandler != null)
        {
            deathHandler.Init(this);
        }
        else
        {
            Debug.LogWarning(
                $"[STAGE MANAGER] {enemy.name} tidak memiliki EnemyDeathHandler. " +
                "Stage tidak akan lanjut jika enemy ini mati."
            );
        }
    }

    private void PrepareSpawnedEnemyObject(GameObject enemy)
    {
        if (enemy == null)
            return;

        enemy.tag = "Enemy";
        SetLayerRecursively(enemy, LayerMask.NameToLayer("Enemy"));
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
        Debug.Log(
            $"[STAGE MANAGER] OnEnemyDied terpanggil. " +
            $"Sebelum rekonsiliasi: {activeEnemiesCount}. State: {currentState}. " +
            $"Sedang transisi boss: {isTransitioningToBoss}"
        );

        int before = activeEnemiesCount;
        int removedByReconcile = ReconcileTrackedEnemies(true);

        // Fallback untuk kasus EnemyDeathHandler memanggil OnEnemyDied(), tetapi objek musuh belum Destroy
        // dan currentHP belum sempat terbaca <= 0 pada frame yang sama.
        if (removedByReconcile <= 0 && before > 0)
        {
            activeEnemiesCount = Mathf.Max(0, before - 1);
            Debug.LogWarning(
                $"[STAGE MANAGER] OnEnemyDied tidak menemukan objek yang bisa dihapus dari tracker. " +
                $"Menggunakan fallback decrement: {before} -> {activeEnemiesCount}."
            );
        }

        TryAdvanceStageAfterEnemyCountChanged("OnEnemyDied");
    }

    private void StartBossTransitionIfNeeded()
    {
        if (isTransitioningToBoss)
        {
            Debug.Log("[STAGE MANAGER] Transisi ke boss sudah berjalan. Pemanggilan ganda diabaikan.");
            return;
        }

        isTransitioningToBoss = true;
        StartCoroutine(TransitionToBoss());
    }

    private IEnumerator TransitionToBoss()
    {
        Debug.Log("[STAGE MANAGER] TransitionToBoss() dimulai.");

        if (finalizeDataBeforeBoss)
        {
            Debug.Log("[STAGE MANAGER] DDA Data Difinalisasi Sebelum Boss.");

            if (DataTracker.Instance != null)
            {
                try
                {
                    DataTracker.Instance.FinalizeStageData();
                }
                catch (Exception ex)
                {
                    Debug.LogError(
                        "[STAGE MANAGER] Exception saat DataTracker.FinalizeStageData(). " +
                        "Boss tetap akan dicoba untuk di-spawn.\n" + ex
                    );
                }
            }
            else
            {
                Debug.LogWarning("[STAGE MANAGER] DataTracker.Instance null. FinalizeStageData dilewati.");
            }
        }

        yield return new WaitForSeconds(1f);

        string dominantWeapon;
        bool usedFallback;
        GameObject bossToSpawn = ResolveBossPrefab(out dominantWeapon, out usedFallback);
        float statMultiplier = Mathf.Max(0.01f, 1f + (statAmplifyPerStage * currentStage));

        if (verboseBossSpawnLog)
        {
            Debug.Log(
                $"[STAGE MANAGER] Hasil pemilihan boss | " +
                $"DominantWeapon: {dominantWeapon} | " +
                $"ForceBowDebug: {forceBowBossForDebug} | " +
                $"UsedFallback: {usedFallback} | " +
                $"BossSword: {(bossPrefabSword != null ? bossPrefabSword.name : "NULL")} | " +
                $"BossBow: {(bossPrefabBow != null ? bossPrefabBow.name : "NULL")} | " +
                $"Selected: {(bossToSpawn != null ? bossToSpawn.name : "NULL")}"
            );
        }

        if (bossToSpawn == null)
        {
            Debug.LogError(
                "[STAGE MANAGER] Boss tidak bisa di-spawn karena bossPrefabSword dan bossPrefabBow tidak tersedia. " +
                "Isi referensi prefab boss pada Inspector StageManager."
            );

            isTransitioningToBoss = false;
            yield break;
        }

        if (bossSpawnPoint == null)
        {
            Debug.LogError(
                "[STAGE MANAGER] Boss tidak bisa di-spawn karena bossSpawnPoint belum diisi pada Inspector StageManager."
            );

            isTransitioningToBoss = false;
            yield break;
        }

        Debug.Log(
            $"[STAGE MANAGER] Mencoba spawn boss prefab: {bossToSpawn.name} | " +
            $"PrefabActiveSelf: {bossToSpawn.activeSelf} | " +
            $"SpawnPoint: {bossSpawnPoint.position} | " +
            $"StatMultiplier: {statMultiplier}"
        );

        GameObject boss = null;

        try
        {
            boss = Instantiate(bossToSpawn, bossSpawnPoint.position, Quaternion.identity);
        }
        catch (Exception ex)
        {
            Debug.LogError("[STAGE MANAGER] Exception saat Instantiate boss.\n" + ex);
            isTransitioningToBoss = false;
            yield break;
        }

        if (boss == null)
        {
            Debug.LogError("[STAGE MANAGER] Instantiate boss menghasilkan null.");
            isTransitioningToBoss = false;
            yield break;
        }

        boss.SetActive(true);
        PrepareSpawnedEnemyObject(boss);

        CharacterBase bossCharacter = boss.GetComponent<CharacterBase>();
        if (bossCharacter != null)
        {
            InitializeCharacterBaseStats(bossCharacter, statMultiplier);
        }
        else
        {
            Debug.LogWarning(
                $"[STAGE MANAGER] Boss {boss.name} tidak memiliki CharacterBase atau turunan Enemy.cs. " +
                "Periksa prefab boss bow."
            );

            if (!allowBossSpawnWithoutCharacterBase)
            {
                Debug.LogError(
                    "[STAGE MANAGER] allowBossSpawnWithoutCharacterBase = false. Boss dihancurkan agar stage tidak berada pada state tidak valid."
                );

                Destroy(boss);
                isTransitioningToBoss = false;
                yield break;
            }
        }

        InitializeStageCombatController(boss, bossCharacter, 999, true);
        InitializeDeathHandler(boss);

        trackedEnemies.Clear();
        RegisterSpawnedEnemy(boss);
        currentState = StageState.FightingBoss;
        isTransitioningToBoss = false;

        Debug.Log(
            $"[STAGE MANAGER] BOSS MUNCUL | " +
            $"Nama: {boss.name} | " +
            $"ActiveSelf: {boss.activeSelf} | " +
            $"ActiveInHierarchy: {boss.activeInHierarchy} | " +
            $"State: {currentState} | " +
            $"Enemy aktif: {activeEnemiesCount}"
        );
    }

    private GameObject ResolveBossPrefab(out string dominantWeapon, out bool usedFallback)
    {
        usedFallback = false;

        dominantWeapon = forceBowBossForDebug ? "Bow" : GetDominantWeaponFromDDA();

        GameObject selectedBoss = dominantWeapon == "Bow" ? bossPrefabBow : bossPrefabSword;

        if (selectedBoss != null)
            return selectedBoss;

        if (dominantWeapon == "Bow")
        {
            Debug.LogWarning("[STAGE MANAGER] DDA memilih Bow, tetapi bossPrefabBow null.");

            if (allowSwordFallbackIfBowMissing && bossPrefabSword != null)
            {
                usedFallback = true;
                return bossPrefabSword;
            }
        }
        else
        {
            Debug.LogWarning("[STAGE MANAGER] DDA memilih Sword, tetapi bossPrefabSword null.");

            if (allowBowFallbackIfSwordMissing && bossPrefabBow != null)
            {
                usedFallback = true;
                return bossPrefabBow;
            }
        }

        return null;
    }

    private void HandleBossDefeated()
    {
        if (finalizeDataAfterBoss)
        {
            Debug.Log("[STAGE MANAGER] DDA Data Difinalisasi Setelah Boss.");
        }

        currentState = StageState.StageCleared;
        isTransitioningToBoss = false;
        trackedEnemies.Clear();
        activeEnemiesCount = 0;

        if (resetDDAAndDataTrackerOnStageCleared)
        {
            ResetDDAAndDataTracker();
        }

        Debug.Log("[STAGE MANAGER] STAGE CLEAR! DDA dan DataTracker direset. Berjalanlah ke kanan untuk lanjut.");
    }

    private void RegisterSpawnedEnemy(GameObject enemy)
    {
        if (enemy == null)
            return;

        if (!trackedEnemies.Contains(enemy))
            trackedEnemies.Add(enemy);

        activeEnemiesCount = trackedEnemies.Count;

        if (attachRuntimeEnemyTracker)
        {
            StageSpawnedEnemyTracker tracker = enemy.GetComponent<StageSpawnedEnemyTracker>();
            if (tracker == null)
                tracker = enemy.AddComponent<StageSpawnedEnemyTracker>();

            tracker.Bind(this);
        }

        Debug.Log(
            $"[STAGE MANAGER] Tracker musuh ditambahkan: {enemy.name}. " +
            $"Total tracked enemy: {trackedEnemies.Count}."
        );
    }

    public void NotifyTrackedEnemyDestroyed(GameObject enemy)
    {
        if (suppressEnemyTrackerCallbacks)
            return;

        int removed = ReconcileTrackedEnemies(true);

        if (removed <= 0 && enemy != null)
        {
            // Unity dapat membuat referensi GameObject tampak null saat proses Destroy.
            // Karena itu, jika rekonsiliasi biasa gagal, coba hapus berdasarkan instance ID.
            for (int i = trackedEnemies.Count - 1; i >= 0; i--)
            {
                GameObject tracked = trackedEnemies[i];
                if (tracked == enemy)
                {
                    trackedEnemies.RemoveAt(i);
                    removed++;
                    break;
                }
            }

            activeEnemiesCount = Mathf.Max(0, trackedEnemies.Count);
        }

        if (removed > 0)
        {
            Debug.Log(
                $"[STAGE MANAGER] Tracker mendeteksi musuh hilang/destroyed. " +
                $"Removed: {removed}. Sisa enemy aktif: {activeEnemiesCount}. State: {currentState}."
            );
        }

        TryAdvanceStageAfterEnemyCountChanged("NotifyTrackedEnemyDestroyed");
    }

    private void UpdateEnemyTrackingWatchdog()
    {
        if (!useEnemyTrackingWatchdog)
            return;

        if (Time.time < nextEnemyTrackingCheckTime)
            return;

        nextEnemyTrackingCheckTime = Time.time + Mathf.Max(0.05f, enemyTrackingCheckInterval);

        if (currentState != StageState.FightingMinions && currentState != StageState.FightingBoss)
            return;

        int removed = ReconcileTrackedEnemies(false);

        if (removed > 0)
        {
            Debug.Log(
                $"[STAGE MANAGER] Watchdog tracker menghapus {removed} musuh yang sudah mati/hilang. " +
                $"Sisa enemy aktif: {activeEnemiesCount}. State: {currentState}."
            );
        }

        TryAdvanceStageAfterEnemyCountChanged("EnemyTrackingWatchdog");
    }

    private int ReconcileTrackedEnemies(bool logRemoved)
    {
        int before = trackedEnemies.Count;

        for (int i = trackedEnemies.Count - 1; i >= 0; i--)
        {
            GameObject enemy = trackedEnemies[i];
            string reason = string.Empty;

            if (ShouldRemoveTrackedEnemy(enemy, out reason))
            {
                string enemyName = enemy != null ? enemy.name : "NULL/Destroyed";
                trackedEnemies.RemoveAt(i);

                if (logRemoved)
                {
                    Debug.Log(
                        $"[STAGE MANAGER] Tracker menghapus enemy: {enemyName}. " +
                        $"Alasan: {reason}."
                    );
                }
            }
        }

        activeEnemiesCount = Mathf.Max(0, trackedEnemies.Count);
        return Mathf.Max(0, before - trackedEnemies.Count);
    }

    private bool ShouldRemoveTrackedEnemy(GameObject enemy, out string reason)
    {
        reason = string.Empty;

        if (enemy == null)
        {
            reason = "GameObject null atau sudah Destroy";
            return true;
        }

        if (!enemy.activeInHierarchy)
        {
            reason = "GameObject tidak aktif di hierarchy";
            return true;
        }

        if (treatZeroHpAsDead)
        {
            CharacterBase character = enemy.GetComponent<CharacterBase>();
            if (character != null && character.currentHP <= 0f)
            {
                reason = $"CharacterBase.currentHP <= 0 ({character.currentHP})";
                return true;
            }
        }

        return false;
    }

    private void TryAdvanceStageAfterEnemyCountChanged(string source)
    {
        if (activeEnemiesCount > 0)
            return;

        if (currentState == StageState.FightingMinions)
        {
            Debug.Log(
                $"[STAGE MANAGER] Semua minion dianggap selesai oleh {source}. " +
                "StageManager memulai transisi ke boss."
            );

            StartBossTransitionIfNeeded();
        }
        else if (currentState == StageState.FightingBoss)
        {
            Debug.Log(
                $"[STAGE MANAGER] Boss dianggap selesai oleh {source}. " +
                "StageManager menyelesaikan stage."
            );

            HandleBossDefeated();
        }
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

        TryResetSkillDebugData();

        yield return StartCoroutine(FadeBlackScreen(0f));

        suppressEnemyTrackerCallbacks = true;
        trackedEnemies.Clear();
        activeEnemiesCount = 0;
        suppressEnemyTrackerCallbacks = false;

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

        try
        {
            resetSkillDebugMethod.Invoke(DataTracker.Instance, null);
            Debug.Log("[STAGE MANAGER] Skill debug player direset untuk stage berikutnya.");
        }
        catch (Exception ex)
        {
            Debug.LogError("[STAGE MANAGER] Exception saat ResetSkillDebugData().\n" + ex);
        }
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

            try
            {
                method.Invoke(instance, null);
                Debug.Log($"[STAGE MANAGER] {typeName}.{methodName}() berhasil dipanggil.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[STAGE MANAGER] Exception saat memanggil {typeName}.{methodName}().\n{ex}");
                return false;
            }
        }

        return false;
    }

    private Type FindTypeByName(string typeName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (Assembly assembly in assemblies)
        {
            Type[] types;

            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types;
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
        if (targetType == null)
            return null;

        PropertyInfo instanceProperty = targetType.GetProperty(
            "Instance",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (instanceProperty != null)
        {
            try
            {
                object propertyInstance = instanceProperty.GetValue(null);
                if (propertyInstance != null)
                    return propertyInstance;
            }
            catch
            {
                // Abaikan dan lanjut mencari lewat field atau scene object.
            }
        }

        FieldInfo instanceField = targetType.GetField(
            "Instance",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (instanceField != null)
        {
            try
            {
                object fieldInstance = instanceField.GetValue(null);
                if (fieldInstance != null)
                    return fieldInstance;
            }
            catch
            {
                // Abaikan dan lanjut mencari lewat scene object.
            }
        }

        UnityEngine.Object sceneObject = FindObjectOfType(targetType);

        if (sceneObject != null)
            return sceneObject;

        return null;
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


public class StageSpawnedEnemyTracker : MonoBehaviour
{
    private StageManager owner;

    public void Bind(StageManager stageManager)
    {
        owner = stageManager;
    }

    private void OnDestroy()
    {
        if (owner != null)
        {
            owner.NotifyTrackedEnemyDestroyed(gameObject);
        }
    }

    private void OnDisable()
    {
        if (owner != null && gameObject.scene.IsValid())
        {
            owner.NotifyTrackedEnemyDestroyed(gameObject);
        }
    }
}
