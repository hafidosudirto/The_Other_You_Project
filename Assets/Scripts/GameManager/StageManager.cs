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

    private int currentStage;
    private int activeEnemiesCount = 0;
    private bool isChangingStage = false;

    private void Start()
    {
        currentStage = startingStageNumber;
        activeEnemiesCount = 0;
        isChangingStage = false;

        SetBlackScreenInstant(0f, false);

        StartStage();
    }

    private void Update()
    {
        CheckStageTransition();
    }

    private void StartStage()
    {
        Debug.Log("--- MEMULAI STAGE " + GetDisplayedStageNumber() + " ---");

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
            $"Spawn: {totalMinions} Minions " +
            $"({meleeCount} Melee, {rangeCount} Range). " +
            $"Token: {minionAttackTokens}. Stat Mult: {statMultiplier}x"
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
        GameObject enemy = Instantiate(prefab, spawnPoint.position, Quaternion.identity);

        enemy.tag = "Enemy";
        SetLayerRecursively(enemy, LayerMask.NameToLayer("Enemy"));

        CharacterBase character = enemy.GetComponent<CharacterBase>();

        if (character == null)
        {
            Debug.LogWarning(
                $"[STAGE MANAGER] Prefab {prefab.name} tidak memiliki CharacterBase atau turunan Enemy.cs."
            );

            Destroy(enemy);
            return;
        }

        InitializeCharacterBaseStats(character, statMultiplier);
        InitializeStageCombatController(enemy, character, attackTokens);
        InitializeDeathHandler(enemy);

        activeEnemiesCount++;

        Debug.Log(
            $"[STAGE MANAGER] Spawn enemy: {enemy.name} | " +
            $"HP: {character.currentHP}/{character.maxHP} | " +
            $"Attack: {character.attack} | " +
            $"Defense: {character.defense} | " +
            $"MoveSpeed: {character.moveSpeed} | " +
            $"Token: {attackTokens}"
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

    private void InitializeStageCombatController(GameObject enemy, CharacterBase character, int attackTokens)
    {
        if (enemy == null || character == null)
            return;

        MinionMeleeCombatController meleeController = enemy.GetComponent<MinionMeleeCombatController>();
        if (meleeController != null)
        {
            meleeController.InitializeStageEnemy(character, attackTokens);
        }

        MinionRangedCombatController rangedController = enemy.GetComponent<MinionRangedCombatController>();
        if (rangedController != null)
        {
            rangedController.InitializeStageEnemy(character, attackTokens);
        }

        if (meleeController == null && rangedController == null)
        {
            Debug.LogWarning(
                $"[STAGE MANAGER] {enemy.name} tidak memiliki MinionMeleeCombatController atau MinionRangedCombatController."
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
        activeEnemiesCount--;

        if (activeEnemiesCount <= 0)
        {
            activeEnemiesCount = 0;

            if (currentState == StageState.FightingMinions)
            {
                StartCoroutine(TransitionToBoss());
            }
            else if (currentState == StageState.FightingBoss)
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
        }
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
        float statMultiplier = 1f + (statAmplifyPerStage * currentStage);

        Debug.Log($"Boss membaca DDA: Menirukan senjata dominan player yaitu {dominantWeapon}");

        if (bossToSpawn == null)
        {
            Debug.LogWarning("[STAGE MANAGER] Boss prefab belum diisi.");
            yield break;
        }

        if (bossSpawnPoint == null)
        {
            Debug.LogWarning("[STAGE MANAGER] Boss spawn point belum diisi.");
            yield break;
        }

        GameObject boss = Instantiate(bossToSpawn, bossSpawnPoint.position, Quaternion.identity);
        boss.tag = "Enemy";
        SetLayerRecursively(boss, LayerMask.NameToLayer("Enemy"));

        CharacterBase bossCharacter = boss.GetComponent<CharacterBase>();
        if (bossCharacter != null)
        {
            InitializeCharacterBaseStats(bossCharacter, statMultiplier);
        }

        EnemyDeathHandler deathHandler = boss.GetComponent<EnemyDeathHandler>();
        if (deathHandler != null)
        {
            deathHandler.Init(this);
        }
        else
        {
            Debug.LogWarning("[STAGE MANAGER] Boss tidak memiliki EnemyDeathHandler.");
        }

        activeEnemiesCount++;
        currentState = StageState.FightingBoss;

        Debug.Log("BOSS MUNCUL!");
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