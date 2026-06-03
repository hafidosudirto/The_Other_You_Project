using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// MODUL KOMPOSISI (Utilitas).
///
/// Mengurus seluruh hal yang berkaitan dengan STAT dan ATTACK TOKEN:
/// <list type="bullet">
/// <item><see cref="StatsSnapshot"/> &amp; perhitungan stat multiplier per stage (kenaikan 10% / 0.1).</item>
/// <item>Inisialisasi stat <see cref="CharacterBase"/> musuh/boss sesuai multiplier.</item>
/// <item>Sistem Attack Token: batas maksimal minion yang boleh menyerang bersamaan,
/// pengambilan/pelepasan token, sinkronisasi ke komponen enemy, dan registrasi data runtime.</item>
/// </list>
///
/// Komponen ini dipasang pada GameObject yang sama dengan <see cref="StageManager"/>
/// (dipakai lewat KOMPOSISI, bukan pewarisan). <see cref="StageManager"/> meng-expose
/// instance ini melalui properti <c>Stats</c> dan mendelegasikan API token publiknya ke sini.
/// </summary>
[DisallowMultipleComponent]
public class StageStatManager : MonoBehaviour
{
    // ---------------------------------------------------------------------
    // STRUCTS / DATA RUNTIME
    // ---------------------------------------------------------------------

    /// <summary>Snapshot stat sebuah character pada satu titik waktu (untuk before/after debug).</summary>
    [Serializable]
    public struct StatsSnapshot
    {
        public float maxHP;
        public float currentHP;
        public float attack;
        public float defense;
        public float moveSpeed;
    }

    /// <summary>
    /// Data runtime per-enemy yang dilacak StageManager: kepemilikan token serangan,
    /// snapshot stat sebelum/sesudah amplifikasi, dan metadata debug.
    /// </summary>
    public class StageEnemyRuntimeDebugData
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

    // ---------------------------------------------------------------------
    // KONFIGURASI (pindahan dari StageManager)
    // ---------------------------------------------------------------------

    [Header("Concurrent Minion Attack Token Progression")]
    [Tooltip("Jika aktif, batas token awal dihitung dari StageManager.baseSpawnToken * initialTokenRatioFromBaseMinion, lalu dibulatkan.")]
    public bool deriveBaseMinionAttackTokenFromBaseMinionRatio = true;
    [Range(0f, 2f)]
    public float initialTokenRatioFromBaseMinion = 0.7f;
    [Tooltip("Batas dasar jumlah minion yang boleh menyerang secara bersamaan. Dipakai jika derive... = false.")]
    public int baseMinionAttackToken = 2;
    [Tooltip("Kenaikan batas jumlah minion yang boleh menyerang secara bersamaan setiap stage.")]
    public int minionAttackTokenIncreasePerStage = 2;

    [Header("Concurrent Attack Token Runtime")]
    [Tooltip("Jika true, minion tetap boleh menyerang ketika data runtime tidak ditemukan. Mencegah prefab lama terkunci total.")]
    public bool allowAttackWhenRuntimeDataMissing = true;
    [Tooltip("Jeda minimal penulisan debug ketika token penuh agar panel tidak terlalu ramai.")]
    public float attackTokenDeniedDebugCooldown = 0.25f;

    // ---------------------------------------------------------------------
    // RINGKASAN STAGE (dipakai panel visual debug)
    // ---------------------------------------------------------------------

    [HideInInspector] public int lastStageTotalMinions = 0;
    [HideInInspector] public int lastStageMeleeCount = 0;
    [HideInInspector] public int lastStageRangeCount = 0;
    [HideInInspector] public int lastStageMinionAttackTokens = 0;
    [HideInInspector] public float lastStageStatMultiplier = 1f;
    [HideInInspector] public string lastStagePlaystyle = "Balanced";
    [HideInInspector] public float lastPlayerRegenAmount = 0f;

    // ---------------------------------------------------------------------
    // STATE RUNTIME TOKEN
    // ---------------------------------------------------------------------

    private readonly List<StageEnemyRuntimeDebugData> enemyRuntimeDebugData = new List<StageEnemyRuntimeDebugData>();
    private int activeConcurrentAttackTokens = 0;
    private int currentConcurrentAttackTokenLimit = 0;
    private float lastAttackTokenDeniedDebugTime = -999f;
    private string lastTokenDebugMessage = "Belum ada pemakaian token serangan bersamaan.";

    private StageManager manager;

    // Properti baca-only untuk dikonsumsi modul visual debug.
    public IReadOnlyList<StageEnemyRuntimeDebugData> EnemyRuntimeData => enemyRuntimeDebugData;
    public int ActiveConcurrentAttackTokens => activeConcurrentAttackTokens;
    public int ConcurrentAttackTokenLimit => currentConcurrentAttackTokenLimit;
    public string LastTokenDebugMessage => lastTokenDebugMessage;

    // ---------------------------------------------------------------------
    // INISIALISASI & RESET
    // ---------------------------------------------------------------------

    public void Initialize(StageManager owner)
    {
        manager = owner;
    }

    /// <summary>Reset penuh (dipanggil dari Awake/fresh-run StageManager). Membersihkan seluruh memori token.</summary>
    public void ResetAll()
    {
        enemyRuntimeDebugData.Clear();
        activeConcurrentAttackTokens = 0;
        currentConcurrentAttackTokenLimit = 0;
        lastAttackTokenDeniedDebugTime = -999f;
        lastTokenDebugMessage = "Belum ada pemakaian token serangan bersamaan.";

        lastStageTotalMinions = 0;
        lastStageMeleeCount = 0;
        lastStageRangeCount = 0;
        lastStageMinionAttackTokens = 0;
        lastStageStatMultiplier = 1f;
        lastStagePlaystyle = "Balanced";
        lastPlayerRegenAmount = 0f;
    }

    /// <summary>Reset ringan saat memulai stage baru (setara bagian token di StartStage lama).</summary>
    public void ResetForNewStage()
    {
        enemyRuntimeDebugData.Clear();
        activeConcurrentAttackTokens = 0;
        currentConcurrentAttackTokenLimit = 0;
        lastAttackTokenDeniedDebugTime = -999f;
        lastTokenDebugMessage = "Belum ada minion yang memakai token serangan pada stage ini.";
    }

    // ---------------------------------------------------------------------
    // STAT PROGRESSION
    // ---------------------------------------------------------------------

    /// <summary>Multiplier stat untuk stage tertentu: 1 + (statAmplifyPerStage * index). Default +10% per stage.</summary>
    public float GetStatMultiplier(int stageProgressionIndex)
    {
        float amplify = manager != null ? manager.statAmplifyPerStage : 0.1f;
        return 1f + (amplify * stageProgressionIndex);
    }

    /// <summary>Kapasitas global token: jumlah maksimal minion yang boleh menyerang bersamaan pada stage tsb.</summary>
    public int CalculateMinionAttackTokens(int stageProgressionIndex)
    {
        int baseSpawnToken = manager != null ? manager.baseSpawnToken : 3;

        int resolvedBaseMinionAttackToken = deriveBaseMinionAttackTokenFromBaseMinionRatio
            ? Mathf.RoundToInt(baseSpawnToken * initialTokenRatioFromBaseMinion)
            : baseMinionAttackToken;

        return Mathf.Max(
            0,
            resolvedBaseMinionAttackToken + (minionAttackTokenIncreasePerStage * stageProgressionIndex)
        );
    }

    /// <summary>Terapkan amplifikasi stat (HP, attack, defense, moveSpeed) ke character.</summary>
    public void InitializeCharacterBaseStats(CharacterBase character, float statMultiplier)
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

    public StatsSnapshot CaptureStats(CharacterBase character)
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

    public float GetStatIncreasePercent(float statMultiplier)
    {
        return (statMultiplier - 1f) * 100f;
    }

    // ---------------------------------------------------------------------
    // PENGATURAN LIMIT TOKEN PER STAGE
    // ---------------------------------------------------------------------

    /// <summary>Dipanggil fase spawn untuk menetapkan batas token konkuren stage ini.</summary>
    public void ConfigureStageTokenLimit(int minionAttackTokens)
    {
        currentConcurrentAttackTokenLimit = minionAttackTokens;
        activeConcurrentAttackTokens = 0;
        lastTokenDebugMessage = $"Token aktif {activeConcurrentAttackTokens}/{currentConcurrentAttackTokenLimit}.";
    }

    // ---------------------------------------------------------------------
    // REGISTRASI ENEMY
    // ---------------------------------------------------------------------

    public void RegisterRuntimeEnemyDebug(
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

    // ---------------------------------------------------------------------
    // API PUBLIK TOKEN (didelegasikan dari StageManager)
    // ---------------------------------------------------------------------

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

    public bool TryConsumeAttackTokenForEnemy(GameObject enemy, float damageAmount = 0f, string source = "Direct Attack")
    {
        return TryAcquireMinionAttackToken(enemy, source);
    }

    public void FinalizeAttackTokenConsumptionForEnemy(GameObject enemy)
    {
        ReleaseMinionAttackToken(enemy, "FinalizeAttackTokenConsumptionForEnemy");
    }

    /// <summary>Lepaskan SEMUA token (mis. saat transisi ke boss / boss kalah).</summary>
    public void ReleaseAllConcurrentAttackTokens(string source)
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

    // ---------------------------------------------------------------------
    // SINKRONISASI & PEMELIHARAAN
    // ---------------------------------------------------------------------

    public void RefreshConcurrentAttackTokenDebugState()
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
                behaviour, behaviourType, "SetStageAttackTokenRuntime",
                isUsingToken, activeTokens, tokenLimit);

            changed |= TryInvokeBoolIntIntMethod(
                behaviour, behaviourType, "SetConcurrentAttackTokenRuntime",
                isUsingToken, activeTokens, tokenLimit);

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

    public bool IsValidAliveRuntimeEnemy(StageEnemyRuntimeDebugData data)
    {
        if (data == null || data.enemy == null || data.character == null)
            return false;

        if (!data.enemy.activeInHierarchy)
            return false;

        return data.character.currentHP > 0f;
    }

    /// <summary>Buang data enemy yang sudah mati/hilang dan lepaskan token-nya bila perlu.</summary>
    public void PruneRuntimeData()
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
    }

    // ---------------------------------------------------------------------
    // REFLECTION HELPERS (set field/property/method pada komponen enemy)
    // ---------------------------------------------------------------------

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

    // ---------------------------------------------------------------------
    // HELPER INTERNAL
    // ---------------------------------------------------------------------

    private void AddFloatingDebugLine(string message)
    {
        if (manager != null && manager.Visual != null)
        {
            manager.Visual.AddFloatingDebugLine(message);
        }
    }
}